using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.InteropServices;
using Herald.Config;
using Herald.Hotkeys;
using Herald.Ocr;
using Herald.Text;
using Herald.Tray;
using Herald.Tts;
using Herald.Util;
using Serilog;

namespace Herald;

/// <summary>
/// Herald entry point. Runs a WinForms message pump with:
/// - Action queue pattern (hotkeys enqueue, main loop processes)
/// - Line queue with navigation (next/prev/pause/stop)
/// - Prefetch cache for EdgeTTS
/// - OCR with region capture and persistent monitoring
/// - Heartbeat monitoring (60s health checks)
/// </summary>
internal static class Program
{
    // --- Win32 DPI awareness ---
    [DllImport("shcore.dll")]
    private static extern int SetProcessDpiAwareness(int value);

    // --- State ---
    private static Settings _settings = null!;
    private static ITtsEngine _engine = null!;
    private static TrayIcon _tray = null!;
    private static GlobalHotkeyManager _hotkeys = null!;
    private static SessionMonitor _sessionMonitor = null!;
    private static SingleInstance _singleInstance = null!;
    private static PersistentRegion? _persistentRegion;

    // Action queue: hotkey callbacks enqueue strings, main loop processes them
    private static readonly ConcurrentQueue<string> ActionQueue = new();

    // Auto-read queue: OCR text from persistent region arrives here
    private static readonly ConcurrentQueue<string> AutoReadQueue = new();

    // Line queue
    private static readonly List<string> LineQueue = new();
    private static int _currentLineIndex;
    private static bool _wasSpeaking;

    // Heartbeat
    private static DateTime _lastHeartbeat = DateTime.UtcNow;

    // Main loop timer
    private static System.Windows.Forms.Timer? _mainLoopTimer;

    // Voice maps for engine switching validation
    private static readonly HashSet<string> EdgeVoices = new(StringComparer.OrdinalIgnoreCase)
        { "aria", "guy", "jenny", "christopher" };
    private static readonly HashSet<string> SapiVoices = new(StringComparer.OrdinalIgnoreCase)
        { "zira", "david" };

    [STAThread]
    static int Main(string[] args)
    {
        // Headless smoke test mode
        if (args.Length > 0 && args[0] == "--test-audio")
        {
            return RunAudioSmokeTest();
        }

        // DPI awareness (must be before any GUI)
        try { SetProcessDpiAwareness(2); } catch { }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Single instance check
        _singleInstance = new SingleInstance();
        if (!_singleInstance.TryAcquire())
        {
            MessageBox.Show("Herald is already running.", "Herald", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }

        // Initialize logging
        Logging.Initialize();
        Log.Information("Herald starting (C# / .NET {Version})", Environment.Version);

        try
        {
            // Load settings
            _settings = Settings.Load();

            // Create TTS engine
            _engine = CreateEngine(_settings);
            Log.Information("TTS engine: {Engine}, voice: {Voice}, rate: {Rate}",
                _settings.Engine, _settings.Voice, _settings.Rate);

            // Create tray icon
            _tray = new TrayIcon();
            WireTrayCallbacks();
            _tray.Show(_settings);

            // Register hotkeys
            _hotkeys = new GlobalHotkeyManager();
            RegisterAllHotkeys();

            // Session monitor (RDP reconnect)
            _sessionMonitor = new SessionMonitor();
            _sessionMonitor.SessionChanged += OnSessionChanged;

            // Main loop timer (50ms — matches Python's loop interval)
            _mainLoopTimer = new System.Windows.Forms.Timer { Interval = Defaults.MainLoopIntervalMs };
            _mainLoopTimer.Tick += (_, _) => MainLoopTick();
            _mainLoopTimer.Start();

            // Check for updates in background
            _ = CheckForUpdatesAsync();

            Log.Information("Herald ready. Press {Hotkey} to speak selected text.", _settings.HotkeySpeak);
            _tray.ShowBalloon("Herald", "Ready. Press Ctrl+Shift+S to speak selected text.");

            // Run message pump (blocks until Application.Exit)
            Application.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Herald crashed");
            MessageBox.Show($"Herald crashed: {ex.Message}", "Herald Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Shutdown();
        }

        return 0;
    }

    // --- Smoke Test (headless, no speaker output) ---

    private static int RunAudioSmokeTest()
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "herald_smoketest.log");
        var results = new List<string>();
        int failures = 0;

        void LogResult(string test, bool pass, string detail = "")
        {
            var status = pass ? "PASS" : "FAIL";
            var line = $"[{status}] {test}" + (detail.Length > 0 ? $" — {detail}" : "");
            results.Add(line);
            Console.WriteLine(line);
            if (!pass) failures++;
        }

        Console.WriteLine("=== Herald Audio Smoke Test ===");
        results.Add($"Timestamp: {DateTime.UtcNow:u}");
        results.Add($"BaseDir: {AppContext.BaseDirectory}");
        results.Add("");

        // Test 1: SAPI synthesis to WAV file
        try
        {
            var wavPath = Path.Combine(Path.GetTempPath(), $"herald_smoke_sapi_{Guid.NewGuid():N}.wav");
            using var synth = new System.Speech.Synthesis.SpeechSynthesizer();
            synth.SetOutputToWaveFile(wavPath);
            synth.Speak("Herald smoke test SAPI synthesis.");
            synth.SetOutputToNull();

            var size = new FileInfo(wavPath).Length;
            LogResult("SAPI synthesis to WAV", size > 44, $"{size} bytes");
            try { File.Delete(wavPath); } catch { }
        }
        catch (Exception ex)
        {
            LogResult("SAPI synthesis to WAV", false, ex.Message);
        }

        // Test 2: EdgeTTS synthesis to MP3 file
        try
        {
            using var engine = new Tts.EdgeTtsEngine("aria", 300);
            var task = engine.SynthesizeToFileAsync("Herald smoke test Edge TTS synthesis.");
            task.Wait(TimeSpan.FromSeconds(30));
            var mp3Path = task.Result;

            if (mp3Path != null && File.Exists(mp3Path))
            {
                var size = new FileInfo(mp3Path).Length;
                LogResult("EdgeTTS synthesis to MP3", size > 100, $"{size} bytes");
                try { File.Delete(mp3Path); } catch { }
            }
            else
            {
                LogResult("EdgeTTS synthesis to MP3", false, "No file produced");
            }
        }
        catch (Exception ex)
        {
            LogResult("EdgeTTS synthesis to MP3", false, ex.Message);
        }

        // Write log file
        results.Add("");
        results.Add($"Result: {(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)")}");
        File.WriteAllLines(logPath, results);
        Console.WriteLine($"\nLog written to: {logPath}");
        Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)");

        return failures == 0 ? 0 : 1;
    }

    // --- Main Loop (runs every 50ms) ---

    private static void MainLoopTick()
    {
        ProcessActionQueue();
        ProcessAutoReadQueue();
        UpdateTrayState();
        MaybeHeartbeat();
    }

    private static void ProcessActionQueue()
    {
        while (ActionQueue.TryDequeue(out var action))
        {
            try
            {
                HandleAction(action);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling action: {Action}", action);
            }
        }
    }

    private static void ProcessAutoReadQueue()
    {
        if (!_settings.AutoRead) return;

        while (AutoReadQueue.TryDequeue(out var text))
        {
            SpeakText(text);
        }
    }

    private static void HandleAction(string action)
    {
        switch (action)
        {
            case "speak":
                OnSpeak();
                break;
            case "pause":
                OnPauseResume();
                break;
            case "stop":
                OnStop();
                break;
            case "next":
                OnNextLine();
                break;
            case "prev":
                OnPrevLine();
                break;
            case "speed_up":
                OnSpeedChange(Defaults.RateStep);
                break;
            case "speed_down":
                OnSpeedChange(-Defaults.RateStep);
                break;
            case "ocr":
                OnOcrRegion();
                break;
            case "monitor":
                OnToggleMonitor();
                break;
            case "quit":
                OnQuit();
                break;
            case "_advance":
                // Delayed line advance (from line delay timer)
                if (_currentLineIndex < LineQueue.Count - 1)
                {
                    _currentLineIndex++;
                    SpeakCurrentLine();
                }
                break;
        }
    }

    private static void UpdateTrayState()
    {
        if (_engine.IsGenerating)
            _tray.SetState(TrayState.Generating);
        else if (_engine.IsPaused)
            _tray.SetState(TrayState.Paused);
        else if (_engine.IsSpeaking)
            _tray.SetState(TrayState.Speaking);
        else
            _tray.SetState(TrayState.Idle);

        // Auto-advance to next line when current finishes
        if (_wasSpeaking && !_engine.IsSpeaking && !_engine.IsGenerating && !_engine.IsPaused)
        {
            _wasSpeaking = false;
            if (_currentLineIndex < LineQueue.Count - 1)
            {
                // Apply line delay if configured
                if (_settings.LineDelay > 0)
                {
                    Task.Run(async () =>
                    {
                        await Task.Delay(_settings.LineDelay);
                        ActionQueue.Enqueue("_advance");
                    });
                }
                else
                {
                    _currentLineIndex++;
                    SpeakCurrentLine();
                }
            }
        }

        if (_engine.IsSpeaking || _engine.IsGenerating)
            _wasSpeaking = true;
    }

    private static void MaybeHeartbeat()
    {
        if ((DateTime.UtcNow - _lastHeartbeat).TotalSeconds < Defaults.HeartbeatIntervalSec) return;
        _lastHeartbeat = DateTime.UtcNow;

        if (!_engine.CheckHealth())
        {
            Log.Warning("Engine health check failed, reinitializing audio");
            _engine.ReinitializeAudio();
        }

        Log.Debug("Heartbeat OK — lines={Lines}, index={Index}, speaking={Speaking}",
            LineQueue.Count, _currentLineIndex, _engine.IsSpeaking);
    }

    // --- Hotkey Actions ---

    private static void OnSpeak()
    {
        // Check for clipboard image first (before auto-copy overwrites it)
        using var clipboardImage = WinOcr.GetClipboardImage();
        if (clipboardImage != null)
        {
            Log.Debug("Clipboard contains image, running OCR");
            _ = OcrAndSpeakAsync(clipboardImage);
            return;
        }

        // Auto-copy selection if enabled
        if (_settings.AutoCopy)
            ClipboardHelper.SimulateCopy();

        var text = ClipboardHelper.GetText();
        if (string.IsNullOrWhiteSpace(text))
        {
            Log.Debug("No text to speak");
            return;
        }

        SpeakText(text);
    }

    private static void SpeakText(string text)
    {
        var lines = _settings.ReadMode == "continuous"
            ? [TextFilter.NormalizeForSpeech(text)]
            : TextFilter.FilterAndSplit(text, _settings.FilterCode, _settings.NormalizeText);

        if (lines.Count == 0)
        {
            Log.Debug("All lines filtered out");
            return;
        }

        if (_settings.LogPreview)
        {
            var preview = lines[0].Length > 80 ? lines[0][..80] + "..." : lines[0];
            Log.Information("Speaking {Count} line(s): {Preview}", lines.Count, preview);
        }

        _engine.Stop();
        LineQueue.Clear();
        LineQueue.AddRange(lines);
        _currentLineIndex = 0;
        _wasSpeaking = false;

        SpeakCurrentLine();
    }

    private static async Task OcrAndSpeakAsync(Bitmap image)
    {
        var text = await WinOcr.RecognizeAsync(image);
        if (string.IsNullOrWhiteSpace(text))
        {
            Log.Debug("OCR returned no text");
            return;
        }

        if (_settings.OcrToClipboard)
        {
            try { Clipboard.SetText(text); }
            catch (Exception ex) { Log.Debug(ex, "Failed to copy OCR text to clipboard"); }
        }

        // Enqueue for main thread processing
        AutoReadQueue.Enqueue(text);
    }

    private static void OnOcrRegion()
    {
        Log.Debug("OCR region capture requested");
        Task.Run(async () =>
        {
            var region = RegionSelector.SelectRegion();
            if (region == null)
            {
                Log.Debug("Region selection cancelled");
                return;
            }

            using var bmp = WinOcr.CaptureRegion(region.Value);
            if (bmp == null) return;

            var text = await WinOcr.RecognizeAsync(bmp);
            if (string.IsNullOrWhiteSpace(text))
            {
                Log.Debug("OCR returned no text from region");
                return;
            }

            if (_settings.OcrToClipboard)
            {
                // Must set clipboard from STA thread
                ActionQueue.Enqueue("_noop"); // Wake main thread
                try { Clipboard.SetText(text); }
                catch { }
            }

            AutoReadQueue.Enqueue(text);
        });
    }

    private static void OnToggleMonitor()
    {
        _persistentRegion ??= new PersistentRegion();

        if (_persistentRegion.IsActive)
        {
            _persistentRegion.Stop();
            Log.Information("Persistent region monitoring stopped");
        }
        else
        {
            Task.Run(() =>
            {
                var region = RegionSelector.SelectRegion();
                if (region == null) return;

                _persistentRegion.TextChanged += text => AutoReadQueue.Enqueue(text);
                _persistentRegion.Start(region.Value);
                Log.Information("Persistent region monitoring started");
            });
        }
    }

    private static void OnPauseResume()
    {
        if (_engine.IsPaused)
            _engine.Resume();
        else if (_engine.IsSpeaking)
            _engine.Pause();
    }

    private static void OnStop()
    {
        _engine.Stop();
        LineQueue.Clear();
        _currentLineIndex = 0;
        _wasSpeaking = false;
    }

    private static void OnNextLine()
    {
        if (_currentLineIndex < LineQueue.Count - 1)
        {
            _engine.Stop();
            _currentLineIndex++;
            _wasSpeaking = false;
            SpeakCurrentLine();
        }
    }

    private static void OnPrevLine()
    {
        if (_currentLineIndex > 0)
        {
            _engine.Stop();
            _currentLineIndex--;
            _wasSpeaking = false;
            SpeakCurrentLine();
        }
    }

    private static void OnSpeedChange(int delta)
    {
        var newRate = Math.Clamp(_settings.Rate + delta, Defaults.MinRate, Defaults.MaxRate);
        _settings.Rate = newRate;
        _engine.Rate = newRate;
        _settings.Save();
        _tray.RebuildMenu(_settings);
        Log.Information("Speed: {Rate} wpm", newRate);
    }

    private static void SpeakCurrentLine()
    {
        if (_currentLineIndex < 0 || _currentLineIndex >= LineQueue.Count) return;

        var line = LineQueue[_currentLineIndex];
        _engine.Speak(line);

        // Prefetch next lines
        for (int i = 1; i <= Defaults.PrefetchAhead; i++)
        {
            var idx = _currentLineIndex + i;
            if (idx < LineQueue.Count)
                _engine.Prefetch(LineQueue[idx]);
        }
    }

    // --- Engine Management ---

    private static ITtsEngine CreateEngine(Settings settings)
    {
        return settings.Engine.ToLowerInvariant() switch
        {
            "kokoro" => new KokoroEngine(settings.Voice, settings.Rate),
            "edge" => new EdgeTtsEngine(settings.Voice, settings.Rate),
            _ => new SapiEngine(settings.Voice, settings.Rate),
        };
    }

    private static void SwitchEngine(string engineName)
    {
        _engine.Stop();
        _engine.Dispose();

        _settings.Engine = engineName;

        // Reset voice to engine default if current voice isn't compatible
        switch (engineName)
        {
            case "kokoro" when !KokoroEngine.VoiceMap.ContainsKey(_settings.Voice):
                _settings.Voice = "heart";
                break;
            case "edge" when !EdgeVoices.Contains(_settings.Voice):
                _settings.Voice = "aria";
                break;
            case "pyttsx3" when !SapiVoices.Contains(_settings.Voice):
                _settings.Voice = "zira";
                break;
        }

        _engine = CreateEngine(_settings);
        _settings.Save();
        _tray.RebuildMenu(_settings);
        Log.Information("Switched to {Engine} engine, voice: {Voice}", engineName, _settings.Voice);
    }

    // --- Tray Callbacks ---

    private static void WireTrayCallbacks()
    {
        _tray.OnQuit = OnQuit;
        _tray.OnPauseToggle = () => ActionQueue.Enqueue("pause");
        _tray.OnEngineChange = engine => SwitchEngine(engine);

        _tray.OnVoiceChange = voice =>
        {
            _settings.Voice = voice;
            _engine.VoiceName = voice;
            _settings.Save();
            _tray.RebuildMenu(_settings);
            Log.Information("Voice changed to {Voice}", voice);
        };

        _tray.OnSpeedChange = speed =>
        {
            _settings.Rate = speed;
            _engine.Rate = speed;
            _settings.Save();
            _tray.RebuildMenu(_settings);
            Log.Information("Speed changed to {Speed} wpm", speed);
        };

        _tray.OnLineDelayChange = delay =>
        {
            _settings.LineDelay = delay;
            _settings.Save();
            _tray.RebuildMenu(_settings);
            Log.Information("Line delay changed to {Delay}ms", delay);
        };

        _tray.OnReadModeChange = mode =>
        {
            _settings.ReadMode = mode;
            _settings.Save();
            _tray.RebuildMenu(_settings);
            Log.Information("Read mode changed to {Mode}", mode);
        };

        _tray.OnLogPreviewChange = v => { _settings.LogPreview = v; _settings.Save(); };
        _tray.OnAutoCopyChange = v => { _settings.AutoCopy = v; _settings.Save(); };
        _tray.OnOcrToClipboardChange = v => { _settings.OcrToClipboard = v; _settings.Save(); };
        _tray.OnFilterCodeChange = v => { _settings.FilterCode = v; _settings.Save(); };
        _tray.OnNormalizeTextChange = v => { _settings.NormalizeText = v; _settings.Save(); };

        _tray.OnAutoReadChange = v =>
        {
            _settings.AutoRead = v;
            _settings.Save();
            Log.Information("Auto-read {State}", v ? "enabled" : "disabled");
        };

        _tray.OnHotkeyChange = (key, value) =>
        {
            _settings.SetHotkey(key, value);
            _settings.Save();
            // Re-register all hotkeys
            _hotkeys.UnregisterAll();
            RegisterAllHotkeys();
            _tray.RebuildMenu(_settings);
            Log.Information("Hotkey {Key} changed to {Value}", key, value);
        };

        _tray.OnResetHotkeys = () =>
        {
            foreach (var key in Defaults.HotkeySettingKeys)
            {
                var defaultValue = new Settings().GetHotkey(key);
                _settings.SetHotkey(key, defaultValue);
            }
            _settings.Save();
            _hotkeys.UnregisterAll();
            RegisterAllHotkeys();
            _tray.RebuildMenu(_settings);
            Log.Information("Hotkeys reset to defaults");
        };
    }

    // --- Hotkey Registration ---

    private static void RegisterAllHotkeys()
    {
        _hotkeys.HotkeyPressed += action => ActionQueue.Enqueue(action);

        var map = new Dictionary<string, string>
        {
            [_settings.HotkeySpeak] = "speak",
            [_settings.HotkeyPause] = "pause",
            [_settings.HotkeyStop] = "stop",
            [_settings.HotkeySpeedUp] = "speed_up",
            [_settings.HotkeySpeedDown] = "speed_down",
            [_settings.HotkeyNext] = "next",
            [_settings.HotkeyPrev] = "prev",
            [_settings.HotkeyOcr] = "ocr",
            [_settings.HotkeyMonitor] = "monitor",
            [_settings.HotkeyQuit] = "quit",
        };

        foreach (var (hotkey, action) in map)
        {
            _hotkeys.Register(hotkey, action);
        }
    }

    // --- Update Checker ---

    private static async Task CheckForUpdatesAsync()
    {
        // Small delay to avoid slowing startup
        await Task.Delay(3000);

        var update = await UpdateChecker.CheckForUpdateAsync();
        if (update.HasValue)
        {
            _tray.ShowBalloon("Herald Update Available",
                $"Version {update.Value.version} is available. Right-click tray icon for details.",
                ToolTipIcon.Info);
        }
    }

    // --- Session Change ---

    private static void OnSessionChanged()
    {
        Task.Run(async () =>
        {
            await Task.Delay(Defaults.RdpReconnectDelayMs);
            _engine.ReinitializeAudio();
            Log.Information("Audio reinitialized after session change");
        });
    }

    // --- Quit ---

    private static void OnQuit()
    {
        Log.Information("Herald shutting down");
        _mainLoopTimer?.Stop();
        Application.Exit();
    }

    private static void Shutdown()
    {
        _mainLoopTimer?.Dispose();
        _persistentRegion?.Dispose();
        _hotkeys?.Dispose();
        _sessionMonitor?.Dispose();
        _engine?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        Log.Information("Herald stopped");
        Log.CloseAndFlush();
    }
}
