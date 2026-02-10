using System.Speech.Synthesis;
using Serilog;

namespace Herald.Tts;

/// <summary>
/// Speaks error messages via SAPI as a last-resort fallback.
/// Static helper — does NOT depend on ITtsEngine (the broken engine shouldn't speak its own errors).
/// </summary>
public static class ErrorSpeaker
{
    public static void SpeakError(string? message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? "An error occurred" : message;
        Log.Warning("Speaking error via SAPI fallback: {Message}", text);

        try
        {
            using var synth = new SpeechSynthesizer();
            synth.Speak(text);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "SAPI fallback failed, beeping instead");
            try { Console.Beep(800, 300); } catch { }
        }
    }
}
