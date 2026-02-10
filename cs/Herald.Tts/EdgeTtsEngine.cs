using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using NAudio.Wave;
using Serilog;

namespace Herald.Tts;

/// <summary>
/// Online TTS using Microsoft Edge's neural voices via WebSocket.
/// Port of the Python edge-tts library's protocol.
/// </summary>
public sealed class EdgeTtsEngine : ITtsEngine
{
    private const string WssUrl = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";
    private const string Origin = "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold";
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0";

    private const int PlaybackTimeoutSec = 30;
    private const int PrefetchCacheMax = 10;

    private volatile bool _speaking;
    private volatile bool _paused;
    private volatile bool _generating;
    private volatile bool _stopRequested;
    private int _rate;
    private string _voiceName;
    private readonly object _lock = new();

    // Prefetch cache: text hash -> temp audio file path
    private readonly ConcurrentDictionary<string, string> _prefetchCache = new();
    private readonly ConcurrentQueue<string> _prefetchOrder = new();
    private static int _fileCounter;

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
        var hash = EdgeTtsProtocol.TextHash(text);
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
                if (_stopRequested)
                {
                    _generating = false;
                    _speaking = false;
                    return;
                }
                if (audioFile == null)
                {
                    Log.Warning("EdgeTTS synthesis returned no audio file");
                    _generating = false;
                    _speaking = false;
                    return;
                }
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

    public IReadOnlyList<string> GetAvailableVoices() => EdgeTtsProtocol.VoiceMap.Keys.ToList();

    public void Prefetch(string text)
    {
        var hash = EdgeTtsProtocol.TextHash(text);
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

    // --- Internal: WebSocket Protocol ---

    internal async Task<string?> SynthesizeToFileAsync(string text)
    {
        var voiceId = EdgeTtsProtocol.ResolveVoice(_voiceName);
        var ratePercent = EdgeTtsProtocol.WpmToEdgeRate(_rate);
        var requestId = Guid.NewGuid().ToString("N");

        var audioData = new MemoryStream();

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Origin", Origin);
        ws.Options.SetRequestHeader("User-Agent", UserAgent);
        ws.Options.SetRequestHeader("Pragma", "no-cache");
        ws.Options.SetRequestHeader("Cache-Control", "no-cache");
        ws.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br, zstd");
        ws.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
        ws.Options.SetRequestHeader("Cookie", $"muid={EdgeTtsProtocol.GenerateMuid()};");

        var secMsGec = EdgeTtsProtocol.GenerateSecMsGec();
        var url = $"{WssUrl}?TrustedClientToken={EdgeTtsProtocol.TrustedClientToken}&ConnectionId={requestId}" +
                  $"&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={EdgeTtsProtocol.SecMsGecVersion}";
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(PlaybackTimeoutSec));

        try
        {
            Log.Debug("EdgeTTS connecting to WebSocket for voice {Voice}, rate {Rate}", voiceId, ratePercent);
            await ws.ConnectAsync(new Uri(url), cts.Token);
            Log.Debug("EdgeTTS WebSocket connected");

            // Send config message
            var configMsg =
                $"X-Timestamp:{DateToString()}\r\n" +
                "Content-Type:application/json; charset=utf-8\r\n" +
                "Path:speech.config\r\n\r\n" +
                "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"}," +
                "\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}\r\n";

            await ws.SendAsync(
                Encoding.UTF8.GetBytes(configMsg),
                WebSocketMessageType.Text, true, cts.Token);

            // Send SSML message
            var ssml = EdgeTtsProtocol.BuildSsml(text, voiceId, ratePercent);
            var ssmlMsg =
                $"X-RequestId:{requestId}\r\n" +
                "Content-Type:application/ssml+xml\r\n" +
                $"X-Timestamp:{DateToString()}Z\r\n" +
                "Path:ssml\r\n\r\n" +
                ssml;

            await ws.SendAsync(
                Encoding.UTF8.GetBytes(ssmlMsg),
                WebSocketMessageType.Text, true, cts.Token);

            Log.Debug("EdgeTTS SSML sent, waiting for audio data");

            // Receive audio data — accumulate full messages before parsing
            var msgBuffer = new MemoryStream();
            var recvBuffer = new byte[16384];

            while (true)
            {
                if (_stopRequested) return null;

                var result = await ws.ReceiveAsync(recvBuffer, cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;

                // Accumulate message fragments
                msgBuffer.Write(recvBuffer, 0, result.Count);

                if (!result.EndOfMessage) continue;

                // Full message received — process it
                var fullMsg = msgBuffer.ToArray();
                msgBuffer.SetLength(0);

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Binary: 2-byte big-endian header length, then header text, then audio bytes
                    if (fullMsg.Length > 2)
                    {
                        int headerLen = (fullMsg[0] << 8) | fullMsg[1];
                        int audioStart = 2 + headerLen;
                        if (audioStart < fullMsg.Length)
                        {
                            audioData.Write(fullMsg, audioStart, fullMsg.Length - audioStart);
                        }
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var msg = Encoding.UTF8.GetString(fullMsg);
                    if (msg.Contains("Path:turn.end"))
                    {
                        Log.Debug("EdgeTTS turn.end received, audio bytes: {Bytes}", audioData.Length);
                        break;
                    }
                }
            }

            if (ws.State == WebSocketState.Open)
            {
                try
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
                catch { }
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
            Log.Warning("EdgeTTS returned empty audio for text: {Text}",
                text.Length > 50 ? text[..50] + "..." : text);
            return null;
        }

        // Write to temp file
        var counter = Interlocked.Increment(ref _fileCounter);
        var tempFile = Path.Combine(Path.GetTempPath(), $"herald_edge_{counter}.mp3");
        await File.WriteAllBytesAsync(tempFile, audioData.ToArray());
        Log.Debug("EdgeTTS audio saved to {File} ({Bytes} bytes)", tempFile, audioData.Length);
        return tempFile;
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
        while (_prefetchCache.Count > PrefetchCacheMax && _prefetchOrder.TryDequeue(out var oldHash))
        {
            if (_prefetchCache.TryRemove(oldHash, out var oldFile))
            {
                try { File.Delete(oldFile); } catch { }
            }
        }
    }

    /// <summary>
    /// JavaScript-style date string for X-Timestamp header.
    /// </summary>
    private static string DateToString()
    {
        var utc = DateTime.UtcNow;
        return utc.ToString("ddd MMM dd yyyy HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture) +
            " GMT+0000 (Coordinated Universal Time)";
    }
}
