using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Speech.Synthesis;
using System.Text;
using NAudio.Wave;

/// <summary>
/// Standalone audio diagnostic for Herald C# rewrite.
/// Tests SAPI and EdgeTTS in isolation, outside of WinForms.
/// </summary>

Console.WriteLine("=== Herald Audio Diagnostic ===\n");

// --- Test 1: SAPI Synchronous ---
Console.Write("Test 1: SAPI synchronous Speak()... ");
try
{
    using var synth = new SpeechSynthesizer();
    synth.SetOutputToDefaultAudioDevice();
    Console.WriteLine($"Voice: {synth.Voice.Name}, Rate: {synth.Rate}");
    Console.Write("  Speaking... ");
    synth.Speak("Hello from SAPI synchronous test.");
    Console.WriteLine("PASS (audio should have played)");
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL: {ex.Message}");
}

Console.WriteLine();

// --- Test 2: SAPI Async on STA thread ---
Console.Write("Test 2: SAPI Speak() on STA thread... ");
try
{
    var done = new ManualResetEventSlim(false);
    Exception? threadEx = null;

    var thread = new Thread(() =>
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            synth.SetOutputToDefaultAudioDevice();
            synth.Speak("Hello from SAPI S T A thread test.");
        }
        catch (Exception ex)
        {
            threadEx = ex;
        }
        finally
        {
            done.Set();
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.IsBackground = true;
    thread.Start();

    done.Wait(TimeSpan.FromSeconds(15));

    if (threadEx != null)
        Console.WriteLine($"FAIL: {threadEx.Message}");
    else
        Console.WriteLine("PASS (audio should have played)");
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL: {ex.Message}");
}

Console.WriteLine();

// --- Test 3: EdgeTTS with DRM ---
Console.Write("Test 3: EdgeTTS WebSocket with DRM auth... ");
try
{
    var audioFile = await SynthesizeEdgeTts("Hello from Edge T T S diagnostic test.");
    if (audioFile == null)
    {
        Console.WriteLine("FAIL: No audio data received");
    }
    else
    {
        Console.WriteLine($"  Audio file: {audioFile} ({new FileInfo(audioFile).Length} bytes)");
        Console.Write("  Playing via NAudio... ");
        PlayMp3(audioFile);
        Console.WriteLine("PASS (audio should have played)");
        try { File.Delete(audioFile); } catch { }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL: {ex.Message}");
    if (ex.InnerException != null)
        Console.WriteLine($"  Inner: {ex.InnerException.Message}");
}

Console.WriteLine("\n=== Done ===");

// --- EdgeTTS synthesis with DRM ---
static async Task<string?> SynthesizeEdgeTts(string text)
{
    const string wssUrl = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";
    const string token = "6A5AA1D4EAFF4E9FB37E23D68491D6F4"; // pragma: allowlist secret
    const string origin = "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold";
    const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0";
    const string secMsGecVersion = "1-143.0.3650.75";

    var requestId = Guid.NewGuid().ToString("N");
    var secMsGec = GenerateSecMsGec(token);
    var muid = GenerateMuid();

    var url = $"{wssUrl}?TrustedClientToken={token}&ConnectionId={requestId}" +
              $"&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={secMsGecVersion}";

    Console.WriteLine($"\n  URL: ...&Sec-MS-GEC={secMsGec[..12]}...&Sec-MS-GEC-Version={secMsGecVersion}");

    using var ws = new ClientWebSocket();
    ws.Options.SetRequestHeader("Origin", origin);
    ws.Options.SetRequestHeader("User-Agent", userAgent);
    ws.Options.SetRequestHeader("Pragma", "no-cache");
    ws.Options.SetRequestHeader("Cache-Control", "no-cache");
    ws.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br, zstd");
    ws.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
    ws.Options.SetRequestHeader("Cookie", $"muid={muid};");

    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    Console.Write("  Connecting... ");
    await ws.ConnectAsync(new Uri(url), cts.Token);
    Console.WriteLine("connected!");

    // Config message
    var timestamp = DateToString();
    var configMsg =
        $"X-Timestamp:{timestamp}\r\n" +
        "Content-Type:application/json; charset=utf-8\r\n" +
        "Path:speech.config\r\n\r\n" +
        "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"}," +
        "\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}\r\n";

    await ws.SendAsync(Encoding.UTF8.GetBytes(configMsg), WebSocketMessageType.Text, true, cts.Token);

    // SSML message
    var escaped = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    var ssml = $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
               $"<voice name='en-US-AriaNeural'><prosody rate='+0%'>{escaped}</prosody></voice></speak>";

    var ssmlMsg =
        $"X-RequestId:{requestId}\r\n" +
        "Content-Type:application/ssml+xml\r\n" +
        $"X-Timestamp:{DateToString()}Z\r\n" +
        "Path:ssml\r\n\r\n" +
        ssml;

    await ws.SendAsync(Encoding.UTF8.GetBytes(ssmlMsg), WebSocketMessageType.Text, true, cts.Token);
    Console.Write("  SSML sent, receiving audio... ");

    // Receive
    var audioData = new MemoryStream();
    var msgBuffer = new MemoryStream();
    var recvBuffer = new byte[16384];

    while (true)
    {
        var result = await ws.ReceiveAsync(recvBuffer, cts.Token);
        if (result.MessageType == WebSocketMessageType.Close) break;

        msgBuffer.Write(recvBuffer, 0, result.Count);
        if (!result.EndOfMessage) continue;

        var fullMsg = msgBuffer.ToArray();
        msgBuffer.SetLength(0);

        if (result.MessageType == WebSocketMessageType.Binary && fullMsg.Length > 2)
        {
            int headerLen = (fullMsg[0] << 8) | fullMsg[1];
            int audioStart = 2 + headerLen;
            if (audioStart < fullMsg.Length)
                audioData.Write(fullMsg, audioStart, fullMsg.Length - audioStart);
        }
        else if (result.MessageType == WebSocketMessageType.Text)
        {
            var msg = Encoding.UTF8.GetString(fullMsg);
            if (msg.Contains("Path:turn.end"))
            {
                Console.WriteLine($"done ({audioData.Length} bytes)");
                break;
            }
        }
    }

    if (ws.State == WebSocketState.Open)
    {
        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch { }
    }

    if (audioData.Length == 0) return null;

    var tempFile = Path.Combine(Path.GetTempPath(), "herald_diag_edge.mp3");
    await File.WriteAllBytesAsync(tempFile, audioData.ToArray());
    return tempFile;
}

static void PlayMp3(string filePath)
{
    using var reader = new Mp3FileReader(filePath);
    using var waveOut = new WaveOutEvent();
    var done = new ManualResetEventSlim(false);
    waveOut.PlaybackStopped += (_, _) => done.Set();
    waveOut.Init(reader);
    waveOut.Play();
    done.Wait(TimeSpan.FromSeconds(15));
}

static string GenerateSecMsGec(string trustedClientToken)
{
    double ticks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    ticks += 11644473600; // WIN_EPOCH
    ticks -= ticks % 300; // round to 5 min
    long fileTime = (long)(ticks * 10_000_000);
    var strToHash = $"{fileTime}{trustedClientToken}";
    var hash = SHA256.HashData(Encoding.ASCII.GetBytes(strToHash));
    return Convert.ToHexString(hash);
}

static string GenerateMuid()
{
    return Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}

static string DateToString()
{
    var utc = DateTime.UtcNow;
    return utc.ToString("ddd MMM dd yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) +
           " GMT+0000 (Coordinated Universal Time)";
}
