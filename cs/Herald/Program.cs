using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Herald.Config;
using Herald.Hotkeys;
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

    // Action queue: hotkey callbacks enqueue strings, main loop processes them
    private static readonly ConcurrentQueue<string> ActionQueue = new();

    // Line queue
    private static readonly List<string> LineQueue = new();
    private static int _currentLineIndex;
    private static bool _wasSpeaking;

    // Heartbeat
    private static DateTime _lastHeartbeat = DateTime.UtcNow;

    // Main loop timer
    private static System.Windows.Forms.Timer? _mainLoopTimer;

    // Edge voice map (for engine switching logic)
    private static readonly Dictionary<string, string> EdgeVoiceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aria"] = "en-US-AriaNeural",
        ["guy"] = "en-US-GuyNeural",
        ["jenny"] = "en-US-JennyNeural",
        ["christopher"] = "en-US-ChristopherNeural",
    };

    [STAThread]
    static void Main()
    {
        // DPI awareness (must be before any GUI)
        try { SetProcessDpiAwareness(2); } catch { }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Single instance check
        _singleInstance = new SingleInstance();
        if (!_singleInstance.TryAcquire())
        {
            MessageBox.Show("Herald is already running.", "Herald", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
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
    }

    // --- Main Loop (runs every 50ms) ---

    private static void MainLoopTick()
    {
        ProcessActionQueue();
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
            case "quit":
                OnQuit();
                break;
        }
    }

    private static void UpdateTrayState()
    {
        // Update tray icon to match engine state
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
                _currentLineIndex++;
                SpeakCurrentLine();
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
        // Auto-copy selection if enabled
        if (_settings.AutoCopy)
            ClipboardHelper.SimulateCopy();

        var text = ClipboardHelper.GetText();
        if (string.IsNullOrWhiteSpace(text))
        {
            Log.Debug("No text to speak");
            return;
        }

        // Filter and split into lines
        var lines = TextFilter.FilterAndSplit(text, _settings.FilterCode, _settings.NormalizeText);
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

        // Stop current speech and load new lines
        _engine.Stop();
        LineQueue.Clear();
        LineQueue.AddRange(lines);
        _currentLineIndex = 0;
        _wasSpeaking = false;

        SpeakCurrentLine();
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
        if (string.Equals(settings.Engine, "edge", StringComparison.OrdinalIgnoreCase))
            return new EdgeTtsEngine(settings.Voice, settings.Rate);
        else
            return new SapiEngine(settings.Voice, settings.Rate);
    }

    private static void SwitchEngine(string engineName)
    {
        _engine.Stop();
        _engine.Dispose();

        _settings.Engine = engineName;

        // Pick appropriate default voice for the engine
        if (engineName == "edge" && !EdgeVoiceMap.ContainsKey(_settings.Voice))
            _settings.Voice = "aria";
        else if (engineName != "edge" && _settings.Voice != "zira" && _settings.Voice != "david")
            _settings.Voice = "zira";

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
            [_settings.HotkeyQuit] = "quit",
        };

        foreach (var (hotkey, action) in map)
        {
            _hotkeys.Register(hotkey, action);
        }
    }

    // --- Session Change ---

    private static void OnSessionChanged()
    {
        // Delay before reinitializing (audio device needs time to reconnect)
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
        _hotkeys?.Dispose();
        _sessionMonitor?.Dispose();
        _engine?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        Log.Information("Herald stopped");
        Log.CloseAndFlush();
    }
}
