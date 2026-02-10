using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;
using KokoroSharp.Utilities;
using NAudio.Wave;
using Serilog;

namespace Herald.Tts;

/// <summary>
/// Local neural TTS using Kokoro via KokoroSharp.
/// Apache 2.0 model, runs entirely offline on CPU after initial model download.
/// Studio-quality voices with ~2x realtime synthesis speed.
/// </summary>
public sealed class KokoroEngine : ITtsEngine
{
    private const int PrefetchCacheMax = 10;

    private KokoroWavSynthesizer? _synth;
    private KokoroVoice? _voice;

    private volatile bool _speaking;
    private volatile bool _paused;
    private volatile bool _generating;
    private volatile bool _stopRequested;
    private int _rate;
    private string _voiceName;
    private readonly object _lock = new();

    // Prefetch cache: text hash -> WAV bytes
    private readonly ConcurrentDictionary<string, byte[]> _prefetchCache = new();
    private readonly ConcurrentQueue<string> _prefetchOrder = new();

    // NAudio playback
    private WaveOutEvent? _waveOut;
    private RawSourceWaveStream? _rawStream;
    private MemoryStream? _waveStream;

    /// <summary>
    /// Kokoro voice mapping: short name -> Kokoro voice ID.
    /// Best voices first per category. See https://huggingface.co/hexgrad/Kokoro-82M/blob/main/VOICES.md
    /// </summary>
    internal static readonly Dictionary<string, string> VoiceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // American Female
        ["heart"] = "af_heart",           // Grade A (premium)
        ["bella"] = "af_bella",           // Grade A-
        ["nicole"] = "af_nicole",         // Grade B-
        ["sarah"] = "af_sarah",           // Grade C+
        ["nova"] = "af_nova",
        ["sky"] = "af_sky",
        ["alloy"] = "af_alloy",
        ["jessica"] = "af_jessica",
        ["kore"] = "af_kore",
        ["aoede"] = "af_aoede",
        ["river"] = "af_river",

        // American Male
        ["michael"] = "am_michael",       // Grade C+
        ["fenrir"] = "am_fenrir",         // Grade C+
        ["puck"] = "am_puck",             // Grade C+
        ["adam"] = "am_adam",
        ["echo"] = "am_echo",
        ["eric"] = "am_eric",
        ["liam"] = "am_liam",
        ["onyx"] = "am_onyx",

        // British Female
        ["emma"] = "bf_emma",             // Grade B-
        ["alice"] = "bf_alice",
        ["isabella"] = "bf_isabella",
        ["lily"] = "bf_lily",

        // British Male
        ["daniel"] = "bm_daniel",
        ["george"] = "bm_george",
        ["lewis"] = "bm_lewis",
        ["fable"] = "bm_fable",
    };

    /// <summary>
    /// Create a KokoroEngine. On first use, downloads the ONNX model (~320MB).
    /// </summary>
    public KokoroEngine(string voiceName = "heart", int rate = 200)
    {
        _voiceName = voiceName;
        _rate = rate;
    }

    public bool IsSpeaking => _speaking;
    public bool IsPaused => _paused;
    public bool IsGenerating => _generating;

    public int Rate
    {
        get => _rate;
        set => _rate = value;
    }

    public string VoiceName
    {
        get => _voiceName;
        set
        {
            _voiceName = value;
            if (_synth != null)
            {
                var voiceId = ResolveVoice(value);
                try
                {
                    _voice = KokoroVoiceManager.GetVoice(voiceId);
                    Log.Debug("Kokoro voice set to {Voice}", voiceId);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to set Kokoro voice {Voice}, keeping current", voiceId);
                }
            }
        }
    }

    public void Speak(string text)
    {
        Stop();
        _stopRequested = false;

        // Check prefetch cache first
        var hash = TextHash(text);
        if (_prefetchCache.TryRemove(hash, out var cached))
        {
            Log.Debug("Prefetch cache hit for text hash {Hash}", hash);
            _speaking = true;
            Task.Run(() => PlayWavBytes(cached));
            return;
        }

        _generating = true;
        _speaking = true;
        Task.Run(() =>
        {
            try
            {
                EnsureModelLoaded();

                var wavBytes = SynthesizeToWav(text);
                if (_stopRequested)
                {
                    _generating = false;
                    _speaking = false;
                    return;
                }
                if (wavBytes == null || wavBytes.Length == 0)
                {
                    Log.Warning("Kokoro synthesis returned no audio");
                    _generating = false;
                    _speaking = false;
                    return;
                }
                _generating = false;
                PlayWavBytes(wavBytes);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Kokoro speak failed");
                _generating = false;
                _speaking = false;
            }
        });
    }

    public void Stop()
    {
        _stopRequested = true;
        _generating = false;
        _paused = false;

        lock (_lock)
        {
            StopPlayback();
            _speaking = false;
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_speaking && !_paused && _waveOut != null)
            {
                _waveOut.Pause();
                _paused = true;
            }
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_paused && _waveOut != null)
            {
                _waveOut.Play();
                _paused = false;
            }
        }
    }

    public IReadOnlyList<string> GetAvailableVoices() => VoiceMap.Keys.ToList();

    public void Prefetch(string text)
    {
        var hash = TextHash(text);
        if (_prefetchCache.ContainsKey(hash)) return;

        Task.Run(() =>
        {
            try
            {
                EnsureModelLoaded();
                var wav = SynthesizeToWav(text);
                if (wav != null && wav.Length > 0)
                {
                    _prefetchCache[hash] = wav;
                    _prefetchOrder.Enqueue(hash);
                    EvictCache();
                    Log.Debug("Prefetched Kokoro audio for hash {Hash}", hash);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Kokoro prefetch failed for hash {Hash}", hash);
            }
        });
    }

    public bool CheckHealth()
    {
        lock (_lock)
        {
            return _waveOut == null || _waveOut.PlaybackState != PlaybackState.Stopped || !_speaking;
        }
    }

    public void ReinitializeAudio()
    {
        lock (_lock)
        {
            StopPlayback();
            Log.Information("Kokoro audio reinitialized");
        }
    }

    public void Dispose()
    {
        Stop();
        _prefetchCache.Clear();
        _synth?.Dispose();
    }

    // --- Internal ---

    private void EnsureModelLoaded()
    {
        if (_synth != null) return;

        lock (_lock)
        {
            if (_synth != null) return;

            // Try to find existing model first, otherwise trigger auto-download
            var modelPath = FindModelPath();
            if (modelPath == null)
            {
                Log.Information("Kokoro model not found locally, triggering download (~320MB)...");
                using var loader = KokoroTTS.LoadModel();
                modelPath = FindModelPath();
            }

            if (modelPath == null)
                throw new FileNotFoundException("Could not locate Kokoro ONNX model after download");

            Log.Information("Loading Kokoro model from {Path}", modelPath);
            _synth = new KokoroWavSynthesizer(modelPath);

            var voiceId = ResolveVoice(_voiceName);
            _voice = KokoroVoiceManager.GetVoice(voiceId);
            Log.Information("Kokoro TTS ready, voice: {Voice}", voiceId);
        }
    }

    private byte[]? SynthesizeToWav(string text)
    {
        if (_synth == null || _voice == null) return null;

        var config = new KokoroTTSPipelineConfig
        {
            Speed = WpmToKokoroSpeed(_rate),
        };

        return _synth.Synthesize(text, _voice, config);
    }

    private void PlayWavBytes(byte[] wavBytes)
    {
        try
        {
            lock (_lock)
            {
                StopPlayback();

                // KokoroSharp returns raw PCM: 16-bit signed, 24kHz, mono (no RIFF header)
                var pcmFormat = new WaveFormat(24000, 16, 1);
                _waveStream = new MemoryStream(wavBytes);
                _rawStream = new RawSourceWaveStream(_waveStream, pcmFormat);

                Log.Debug("Kokoro playback: {Length} bytes, {Duration:F1}s",
                    wavBytes.Length, _rawStream.TotalTime.TotalSeconds);

                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += (_, args) =>
                {
                    if (args.Exception != null)
                        Log.Error(args.Exception, "Kokoro PlaybackStopped with error");
                    _speaking = false;
                    _paused = false;
                };
                _waveOut.Init(_rawStream);
                _waveOut.Play();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to play Kokoro audio");
            _speaking = false;
            _generating = false;
        }
    }

    private void StopPlayback()
    {
        // Must be called under _lock
        try
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _rawStream?.Dispose();
            _waveStream?.Dispose();
        }
        catch { }
        _waveOut = null;
        _rawStream = null;
        _waveStream = null;
    }

    private void EvictCache()
    {
        while (_prefetchCache.Count > PrefetchCacheMax && _prefetchOrder.TryDequeue(out var oldHash))
        {
            _prefetchCache.TryRemove(oldHash, out _);
        }
    }

    /// <summary>
    /// Convert WPM to Kokoro speed multiplier.
    /// 200 WPM = speed 1.0 (normal). Kokoro recommended range: 0.5-1.3.
    /// We allow up to 3.0 for power users (quality may degrade above 1.3).
    /// </summary>
    public static float WpmToKokoroSpeed(int wpm)
    {
        float speed = wpm / 200f;
        return Math.Clamp(speed, 0.5f, 3.0f);
    }

    /// <summary>
    /// Resolve short voice name to Kokoro voice ID. Falls back to af_heart.
    /// </summary>
    internal static string ResolveVoice(string shortName)
    {
        return VoiceMap.GetValueOrDefault(shortName, "af_heart");
    }

    /// <summary>
    /// MD5 hash of text for prefetch cache keying. Returns lowercase 12-char hex.
    /// </summary>
    private static string TextHash(string text)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..12].ToLower();
    }

    /// <summary>
    /// Search for the Kokoro ONNX model in known locations.
    /// KokoroSharp downloads to the user's home directory as "kokoro.onnx".
    /// </summary>
    private static string? FindModelPath()
    {
        var modelNames = new[] { "kokoro.onnx", "kokoro-v1.0.onnx" };
        var searchDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KokoroSharp"),
        };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var name in modelNames)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path)) return path;
            }
        }

        return null;
    }
}
