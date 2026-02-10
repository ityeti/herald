using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using NAudio.Wave;
using Serilog;
using Herald.Config;

namespace Herald.Tts;

/// <summary>
/// Online TTS using Microsoft Edge's neural voices via WebSocket.
/// Port of the Python edge-tts library's protocol.
/// </summary>
public sealed class EdgeTtsEngine : ITtsEngine
{
    // Edge TTS voice mapping (short name → full voice ID)
    private static readonly Dictionary<string, string> VoiceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aria"] = "en-US-AriaNeural",
        ["guy"] = "en-US-GuyNeural",
        ["jenny"] = "en-US-JennyNeural",
        ["christopher"] = "en-US-ChristopherNeural",
    };

    private const string WssUrl = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";
    private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";  // pragma: allowlist secret
    private const string Origin = "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold";

    private volatile bool _speaking;
    private volatile bool _paused;
    private volatile bool _generating;
    private volatile bool _stopRequested;
    private int _rate;
    private string _voiceName;
    private readonly object _lock = new();

    // Prefetch cache: text hash → temp audio file path
    private readonly ConcurrentDictionary<string, string> _prefetchCache = new();
    private readonly ConcurrentQueue<string> _prefetchOrder = new();
    private int _fileCounter;

    // NAudio playback
    private WaveOutEvent? _waveOut;
    private Mp3FileReader? _mp3Reader;

    public EdgeTtsEngine(string voiceName = "aria", int rate = 900)
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
        set => _voiceName = value;
    }

    public void Speak(string text)
    {
        Stop();
        _stopRequested = false;

        // Check prefetch cache first
        var hash = TextHash(text);
        if (_prefetchCache.TryRemove(hash, out var cachedFile) && File.Exists(cachedFile))
        {
            Log.Debug("Prefetch cache hit for text hash {Hash}", hash);
            _speaking = true;
            Task.Run(() => PlayAudioFile(cachedFile));
            return;
        }

        _generating = true;
        _speaking = true;
        Task.Run(async () =>
        {
            string? audioFile = null;
            try
            {
                audioFile = await SynthesizeToFileAsync(text);
                if (_stopRequested || audioFile == null) return;
                _generating = false;
                PlayAudioFile(audioFile);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "EdgeTTS speak failed");
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

        Task.Run(async () =>
        {
            try
            {
                var file = await SynthesizeToFileAsync(text);
                if (file != null)
                {
                    _prefetchCache[hash] = file;
                    _prefetchOrder.Enqueue(hash);
                    EvictCache();
                    Log.Debug("Prefetched audio for hash {Hash}", hash);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Prefetch failed for hash {Hash}", hash);
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
            Log.Information("EdgeTTS audio reinitialized");
        }
    }

    public void Dispose()
    {
        Stop();
        // Clean up cached files
        foreach (var kvp in _prefetchCache)
        {
            try { File.Delete(kvp.Value); } catch { }
        }
        _prefetchCache.Clear();
    }

    // --- Private: WebSocket Protocol ---

    private async Task<string?> SynthesizeToFileAsync(string text)
    {
        var voiceId = VoiceMap.GetValueOrDefault(_voiceName, "en-US-AriaNeural");
        var ratePercent = WpmToEdgeRate(_rate);
        var requestId = Guid.NewGuid().ToString("N");

        var audioData = new MemoryStream();

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Origin", Origin);
        ws.Options.SetRequestHeader("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0");

        var url = $"{WssUrl}?TrustedClientToken={TrustedClientToken}&ConnectionId={requestId}";
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Defaults.PlaybackTimeoutSec));

        try
        {
            await ws.ConnectAsync(new Uri(url), cts.Token);

            // Send config message
            var configMsg =
                $"Content-Type:application/json; charset=utf-8\r\n" +
                $"Path:speech.config\r\n\r\n" +
                $"{{\"context\":{{\"synthesis\":{{\"audio\":{{\"metadataoptions\":{{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"}}," +
                $"\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}}}}}";

            await ws.SendAsync(
                Encoding.UTF8.GetBytes(configMsg),
                WebSocketMessageType.Text, true, cts.Token);

            // Send SSML message
            var ssml = BuildSsml(text, voiceId, ratePercent);
            var ssmlMsg =
                $"X-RequestId:{requestId}\r\n" +
                $"Content-Type:application/ssml+xml\r\n" +
                $"Path:ssml\r\n\r\n" +
                ssml;

            await ws.SendAsync(
                Encoding.UTF8.GetBytes(ssmlMsg),
                WebSocketMessageType.Text, true, cts.Token);

            // Receive audio data
            var buffer = new byte[8192];
            while (true)
            {
                if (_stopRequested) return null;

                var result = await ws.ReceiveAsync(buffer, cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Binary messages: 2-byte header length + header + audio data
                    if (result.Count > 2)
                    {
                        int headerLen = (buffer[0] << 8) | buffer[1];
                        int audioStart = 2 + headerLen;
                        if (audioStart < result.Count)
                        {
                            audioData.Write(buffer, audioStart, result.Count - audioStart);
                        }
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (msg.Contains("Path:turn.end"))
                        break;
                }
            }

            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Warning("EdgeTTS synthesis timed out");
            return null;
        }
        catch (WebSocketException ex)
        {
            Log.Error(ex, "EdgeTTS WebSocket error");
            return null;
        }

        if (audioData.Length == 0)
        {
            Log.Warning("EdgeTTS returned empty audio");
            return null;
        }

        // Write to temp file
        var counter = Interlocked.Increment(ref _fileCounter);
        var tempFile = Path.Combine(Path.GetTempPath(), $"herald_edge_{counter}.mp3");
        await File.WriteAllBytesAsync(tempFile, audioData.ToArray());
        return tempFile;
    }

    private static string BuildSsml(string text, string voiceId, string ratePercent)
    {
        // Escape XML special characters
        var escaped = text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");

        return $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
               $"<voice name='{voiceId}'>" +
               $"<prosody rate='{ratePercent}'>" +
               escaped +
               $"</prosody></voice></speak>";
    }

    private void PlayAudioFile(string filePath)
    {
        try
        {
            lock (_lock)
            {
                StopPlayback();
                _mp3Reader = new Mp3FileReader(filePath);
                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += (_, _) =>
                {
                    _speaking = false;
                    _paused = false;
                    // Clean up temp file
                    Task.Run(async () =>
                    {
                        await Task.Delay(500);
                        try { File.Delete(filePath); } catch { }
                    });
                };
                _waveOut.Init(_mp3Reader);
                _waveOut.Play();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to play audio file {File}", filePath);
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
            _mp3Reader?.Dispose();
        }
        catch { }
        _waveOut = null;
        _mp3Reader = null;
    }

    private void EvictCache()
    {
        while (_prefetchCache.Count > Defaults.PrefetchCacheMax && _prefetchOrder.TryDequeue(out var oldHash))
        {
            if (_prefetchCache.TryRemove(oldHash, out var oldFile))
            {
                try { File.Delete(oldFile); } catch { }
            }
        }
    }

    private static string TextHash(string text)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..12].ToLower();
    }

    /// <summary>
    /// Convert WPM to edge-tts rate percentage string.
    /// 300 wpm = baseline (0%). Each 300 wpm = 100% change. Clamped -50% to +200%.
    /// </summary>
    private static string WpmToEdgeRate(int wpm)
    {
        int percent = (wpm - 300) / 3;
        percent = Math.Clamp(percent, -50, 200);
        return percent >= 0 ? $"+{percent}%" : $"{percent}%";
    }
}
