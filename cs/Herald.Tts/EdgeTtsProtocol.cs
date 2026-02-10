using System.Security.Cryptography;
using System.Text;

namespace Herald.Tts;

/// <summary>
/// Static protocol helpers extracted from EdgeTtsEngine for testability.
/// Handles DRM token generation, SSML construction, rate conversion, etc.
/// </summary>
internal static class EdgeTtsProtocol
{
    // Edge TTS voice mapping (short name -> full voice ID)
    internal static readonly Dictionary<string, string> VoiceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aria"] = "en-US-AriaNeural",
        ["guy"] = "en-US-GuyNeural",
        ["jenny"] = "en-US-JennyNeural",
        ["christopher"] = "en-US-ChristopherNeural",
    };

    internal const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";  // pragma: allowlist secret
    internal const string ChromiumFullVersion = "143.0.3650.75";
    internal const string SecMsGecVersion = $"1-{ChromiumFullVersion}";
    private const long WinEpoch = 11644473600;

    /// <summary>
    /// Convert WPM to edge-tts rate percentage string.
    /// 300 wpm = baseline (0%). Each 300 wpm = 100% change. Clamped -50% to +200%.
    /// </summary>
    internal static string WpmToEdgeRate(int wpm)
    {
        int percent = (wpm - 300) / 3;
        percent = Math.Clamp(percent, -50, 200);
        return percent >= 0 ? $"+{percent}%" : $"{percent}%";
    }

    /// <summary>
    /// Build SSML for Edge TTS with XML-escaped text.
    /// </summary>
    internal static string BuildSsml(string text, string voiceId, string ratePercent)
    {
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

    /// <summary>
    /// Generate the Sec-MS-GEC token: SHA-256 of Windows file time (rounded to 5 min) + TrustedClientToken.
    /// </summary>
    internal static string GenerateSecMsGec()
    {
        double ticks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        ticks += WinEpoch;
        ticks -= ticks % 300;
        long fileTime = (long)(ticks * 10_000_000);

        var strToHash = $"{fileTime}{TrustedClientToken}";
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(strToHash));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Generate a random MUID: 16 random bytes as uppercase hex (32 chars).
    /// </summary>
    internal static string GenerateMuid()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// MD5 hash of text for prefetch cache keying. Returns lowercase 12-char hex.
    /// </summary>
    internal static string TextHash(string text)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..12].ToLower();
    }

    /// <summary>
    /// Resolve a short voice name to the full Edge TTS voice ID.
    /// Returns the full ID or falls back to AriaNeural.
    /// </summary>
    internal static string ResolveVoice(string shortName)
    {
        return VoiceMap.GetValueOrDefault(shortName, "en-US-AriaNeural");
    }
}
