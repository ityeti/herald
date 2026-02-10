namespace Herald.Tts;

/// <summary>
/// Abstract interface for TTS engines. Matches the Python BaseTTSEngine contract.
/// </summary>
public interface ITtsEngine : IDisposable
{
    /// <summary>Start speaking text asynchronously (non-blocking).</summary>
    void Speak(string text);

    /// <summary>Stop current speech immediately.</summary>
    void Stop();

    /// <summary>Pause playback.</summary>
    void Pause();

    /// <summary>Resume playback after pause.</summary>
    void Resume();

    /// <summary>True while audio is playing.</summary>
    bool IsSpeaking { get; }

    /// <summary>True while playback is paused.</summary>
    bool IsPaused { get; }

    /// <summary>True while generating audio (e.g. EdgeTTS network fetch).</summary>
    bool IsGenerating { get; }

    /// <summary>Speech rate in words-per-minute.</summary>
    int Rate { get; set; }

    /// <summary>Current voice name (e.g. "aria", "zira").</summary>
    string VoiceName { get; set; }

    /// <summary>Get list of available voice names for this engine.</summary>
    IReadOnlyList<string> GetAvailableVoices();

    /// <summary>Pre-generate audio for a line (for prefetch cache). No-op if not supported.</summary>
    void Prefetch(string text) { }

    /// <summary>Check if audio subsystem is healthy.</summary>
    bool CheckHealth();

    /// <summary>Reinitialize audio subsystem (e.g. after RDP reconnect).</summary>
    void ReinitializeAudio();
}
