using System.Speech.Synthesis;
using Serilog;

namespace Herald.Tts;

/// <summary>
/// Offline TTS using Windows SAPI5 via System.Speech.Synthesis.
/// Equivalent to the Python pyttsx3 engine.
///
/// Uses synchronous Speak() on a dedicated STA thread to ensure
/// audio output works reliably from any calling context.
/// </summary>
public sealed class SapiEngine : ITtsEngine
{
    private SpeechSynthesizer _synth;
    private volatile bool _speaking;
    private volatile bool _paused;
    private volatile bool _stopRequested;
    private string _voiceName;
    private int _rate;
    private readonly object _lock = new();
    private Thread? _speakThread;

    // Maps short names to SAPI voice name fragments
    private static readonly Dictionary<string, string> VoiceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zira"] = "Zira",
        ["david"] = "David",
    };

    public SapiEngine(string voiceName = "zira", int rate = 200)
    {
        _voiceName = voiceName;
        _rate = rate;
        _synth = CreateSynthesizer();
    }

    public bool IsSpeaking => _speaking;
    public bool IsPaused => _paused;
    public bool IsGenerating => false;

    public int Rate
    {
        get => _rate;
        set
        {
            _rate = value;
            lock (_lock)
            {
                _synth.Rate = WpmToSapiRate(value);
            }
        }
    }

    public string VoiceName
    {
        get => _voiceName;
        set
        {
            _voiceName = value;
            lock (_lock)
            {
                ApplyVoice(_synth, value);
            }
        }
    }

    public void Speak(string text)
    {
        Stop();
        _stopRequested = false;
        _speaking = true;
        _paused = false;

        // Run synchronous Speak() on a dedicated STA thread for reliable audio output.
        // SpeakAsync can silently fail when called from non-STA threads in WinForms apps.
        _speakThread = new Thread(() =>
        {
            try
            {
                Log.Debug("SAPI speaking on thread {ThreadId}, voice={Voice}, rate={Rate}",
                    Environment.CurrentManagedThreadId, _voiceName, WpmToSapiRate(_rate));
                lock (_lock)
                {
                    _synth.Speak(text);
                }
            }
            catch (Exception ex) when (!_stopRequested)
            {
                Log.Warning(ex, "SAPI speak error");
            }
            finally
            {
                if (!_stopRequested)
                {
                    _speaking = false;
                    _paused = false;
                }
            }
        });
        _speakThread.SetApartmentState(ApartmentState.STA);
        _speakThread.IsBackground = true;
        _speakThread.Name = "SAPI-Speak";
        _speakThread.Start();
    }

    public void Stop()
    {
        _stopRequested = true;
        lock (_lock)
        {
            if (_speaking || _paused)
            {
                _synth.SpeakAsyncCancelAll();
                _speaking = false;
                _paused = false;
            }
        }
        // Wait briefly for the speak thread to finish
        if (_speakThread is { IsAlive: true })
        {
            _speakThread.Join(500);
        }
        _speakThread = null;
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_speaking && !_paused)
            {
                _synth.Pause();
                _paused = true;
            }
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_paused)
            {
                _synth.Resume();
                _paused = false;
            }
        }
    }

    public IReadOnlyList<string> GetAvailableVoices()
    {
        var voices = new List<string>();
        foreach (var installed in _synth.GetInstalledVoices())
        {
            if (!installed.Enabled) continue;
            var name = installed.VoiceInfo.Name;
            // Match known short names
            foreach (var (shortName, fragment) in VoiceMap)
            {
                if (name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    voices.Add(shortName);
                    break;
                }
            }
        }
        return voices;
    }

    public bool CheckHealth() => true;

    public void ReinitializeAudio()
    {
        lock (_lock)
        {
            _synth.Dispose();
            _synth = CreateSynthesizer();
            Log.Information("SAPI synthesizer reinitialized");
        }
    }

    public void Dispose()
    {
        Stop();
        lock (_lock)
        {
            _synth.Dispose();
        }
    }

    private SpeechSynthesizer CreateSynthesizer()
    {
        var synth = new SpeechSynthesizer();
        synth.SetOutputToDefaultAudioDevice();
        synth.Rate = WpmToSapiRate(_rate);
        ApplyVoice(synth, _voiceName);

        // Log available voices on creation for diagnostics
        foreach (var v in synth.GetInstalledVoices())
        {
            if (v.Enabled)
                Log.Debug("SAPI available voice: {Voice}", v.VoiceInfo.Name);
        }

        return synth;
    }

    private static void ApplyVoice(SpeechSynthesizer synth, string shortName)
    {
        if (VoiceMap.TryGetValue(shortName, out var fragment))
        {
            try
            {
                foreach (var v in synth.GetInstalledVoices())
                {
                    if (v.Enabled && v.VoiceInfo.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    {
                        synth.SelectVoice(v.VoiceInfo.Name);
                        Log.Debug("SAPI voice set to {Voice}", v.VoiceInfo.Name);
                        return;
                    }
                }
                Log.Warning("SAPI voice '{Voice}' not found in installed voices", shortName);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to set SAPI voice {Voice}", shortName);
            }
        }
    }

    /// <summary>
    /// Convert WPM to SAPI rate (-10 to 10 scale).
    /// SAPI default is 200 WPM at rate 0. Roughly: rate = (wpm - 200) / 30, clamped.
    /// </summary>
    internal static int WpmToSapiRate(int wpm)
    {
        int rate = (wpm - 200) / 30;
        return Math.Clamp(rate, -10, 10);
    }
}
