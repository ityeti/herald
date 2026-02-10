namespace Herald.Config;

/// <summary>
/// Default values matching the Python version's config.py constants.
/// </summary>
public static class Defaults
{
    // TTS
    public const string Engine = "edge";
    public const string Voice = "aria";
    public const int Rate = 900;
    public const int MinRate = 150;
    public const int MaxRate = 1500;
    public const int RateStep = 25;

    // Hotkeys
    public const string HotkeySpeak = "ctrl+shift+s";
    public const string HotkeyPause = "ctrl+shift+p";
    public const string HotkeyStop = "escape";
    public const string HotkeySpeedUp = "ctrl+shift+]";
    public const string HotkeySpeedDown = "ctrl+shift+[";
    public const string HotkeyNext = "ctrl+shift+n";
    public const string HotkeyPrev = "ctrl+shift+b";
    public const string HotkeyOcr = "ctrl+shift+o";
    public const string HotkeyMonitor = "ctrl+shift+m";
    public const string HotkeyQuit = "ctrl+shift+q";

    // Behavior
    public const int LineDelay = 0;
    public const string ReadMode = "lines";
    public const bool LogPreview = true;
    public const bool AutoCopy = true;
    public const bool OcrToClipboard = true;
    public const bool AutoRead = false;
    public const bool FilterCode = true;
    public const bool NormalizeText = true;

    // OCR polling
    public const double AutoReadInterval = 2.5;
    public const double AutoReadThreshold = 0.5;

    // Timing
    public const int MainLoopIntervalMs = 50;
    public const int HeartbeatIntervalSec = 60;
    public const int PrefetchAhead = 2;
    public const int PrefetchCacheMax = 10;
    public const int PlaybackTimeoutSec = 30;
    public const int RdpReconnectDelayMs = 1500;

    // All hotkey setting keys
    public static readonly string[] HotkeySettingKeys =
    [
        "hotkey_speak", "hotkey_pause", "hotkey_stop",
        "hotkey_speed_up", "hotkey_speed_down",
        "hotkey_next", "hotkey_prev",
        "hotkey_ocr", "hotkey_monitor",
        "hotkey_quit"
    ];
}
