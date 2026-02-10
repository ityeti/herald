using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace Herald.Config;

/// <summary>
/// User-configurable settings. Shares the same JSON format as the Python version
/// so both can read/write config/settings.json interchangeably.
/// </summary>
public sealed class Settings
{
    // --- TTS ---
    [JsonPropertyName("engine")]
    public string Engine { get; set; } = Defaults.Engine;

    [JsonPropertyName("voice")]
    public string Voice { get; set; } = Defaults.Voice;

    [JsonPropertyName("rate")]
    public int Rate { get; set; } = Defaults.Rate;

    // --- Hotkeys ---
    [JsonPropertyName("hotkey_speak")]
    public string HotkeySpeak { get; set; } = Defaults.HotkeySpeak;

    [JsonPropertyName("hotkey_pause")]
    public string HotkeyPause { get; set; } = Defaults.HotkeyPause;

    [JsonPropertyName("hotkey_stop")]
    public string HotkeyStop { get; set; } = Defaults.HotkeyStop;

    [JsonPropertyName("hotkey_speed_up")]
    public string HotkeySpeedUp { get; set; } = Defaults.HotkeySpeedUp;

    [JsonPropertyName("hotkey_speed_down")]
    public string HotkeySpeedDown { get; set; } = Defaults.HotkeySpeedDown;

    [JsonPropertyName("hotkey_next")]
    public string HotkeyNext { get; set; } = Defaults.HotkeyNext;

    [JsonPropertyName("hotkey_prev")]
    public string HotkeyPrev { get; set; } = Defaults.HotkeyPrev;

    [JsonPropertyName("hotkey_ocr")]
    public string HotkeyOcr { get; set; } = Defaults.HotkeyOcr;

    [JsonPropertyName("hotkey_monitor")]
    public string HotkeyMonitor { get; set; } = Defaults.HotkeyMonitor;

    [JsonPropertyName("hotkey_quit")]
    public string HotkeyQuit { get; set; } = Defaults.HotkeyQuit;

    // --- Behavior ---
    [JsonPropertyName("line_delay")]
    public int LineDelay { get; set; } = Defaults.LineDelay;

    [JsonPropertyName("read_mode")]
    public string ReadMode { get; set; } = Defaults.ReadMode;

    [JsonPropertyName("log_preview")]
    public bool LogPreview { get; set; } = Defaults.LogPreview;

    [JsonPropertyName("auto_copy")]
    public bool AutoCopy { get; set; } = Defaults.AutoCopy;

    [JsonPropertyName("ocr_to_clipboard")]
    public bool OcrToClipboard { get; set; } = Defaults.OcrToClipboard;

    [JsonPropertyName("auto_read")]
    public bool AutoRead { get; set; } = Defaults.AutoRead;

    [JsonPropertyName("filter_code")]
    public bool FilterCode { get; set; } = Defaults.FilterCode;

    [JsonPropertyName("normalize_text")]
    public bool NormalizeText { get; set; } = Defaults.NormalizeText;

    // --- JSON options (shared) ---
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Resolve the settings.json path relative to the executable's grandparent
    /// (herald/config/settings.json), matching the Python layout.
    /// </summary>
    public static string GetSettingsPath()
    {
        // The exe lives in cs/Herald/bin/... during dev, or alongside Launch_Herald.bat in release.
        // Walk up to find the herald project root (contains "config" folder).
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, "config", "settings.json");
            if (File.Exists(candidate) || Directory.Exists(Path.Combine(dir, "config")))
                return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        // Fallback: config/ next to the exe
        return Path.Combine(AppContext.BaseDirectory, "config", "settings.json");
    }

    /// <summary>Load settings from disk, merging with defaults for any missing keys.</summary>
    public static Settings Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            Log.Information("No settings file found at {Path}, using defaults", path);
            return new Settings();
        }

        try
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<Settings>(json, JsonOpts) ?? new Settings();
            Log.Information("Settings loaded from {Path}", path);
            return settings;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load settings from {Path}, using defaults", path);
            return new Settings();
        }
    }

    /// <summary>Save current settings to disk.</summary>
    public void Save()
    {
        var path = GetSettingsPath();
        try
        {
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(this, JsonOpts);
            File.WriteAllText(path, json);
            Log.Debug("Settings saved to {Path}", path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings to {Path}", path);
        }
    }

    /// <summary>
    /// Get a hotkey value by its setting key name (e.g. "hotkey_speak").
    /// </summary>
    public string GetHotkey(string settingKey) => settingKey switch
    {
        "hotkey_speak" => HotkeySpeak,
        "hotkey_pause" => HotkeyPause,
        "hotkey_stop" => HotkeyStop,
        "hotkey_speed_up" => HotkeySpeedUp,
        "hotkey_speed_down" => HotkeySpeedDown,
        "hotkey_next" => HotkeyNext,
        "hotkey_prev" => HotkeyPrev,
        "hotkey_ocr" => HotkeyOcr,
        "hotkey_monitor" => HotkeyMonitor,
        "hotkey_quit" => HotkeyQuit,
        _ => throw new ArgumentException($"Unknown hotkey setting: {settingKey}")
    };

    /// <summary>
    /// Set a hotkey value by its setting key name.
    /// </summary>
    public void SetHotkey(string settingKey, string value)
    {
        switch (settingKey)
        {
            case "hotkey_speak": HotkeySpeak = value; break;
            case "hotkey_pause": HotkeyPause = value; break;
            case "hotkey_stop": HotkeyStop = value; break;
            case "hotkey_speed_up": HotkeySpeedUp = value; break;
            case "hotkey_speed_down": HotkeySpeedDown = value; break;
            case "hotkey_next": HotkeyNext = value; break;
            case "hotkey_prev": HotkeyPrev = value; break;
            case "hotkey_ocr": HotkeyOcr = value; break;
            case "hotkey_monitor": HotkeyMonitor = value; break;
            case "hotkey_quit": HotkeyQuit = value; break;
            default: throw new ArgumentException($"Unknown hotkey setting: {settingKey}");
        }
    }
}
