using System.Drawing;
using Herald.Config;
using Serilog;

namespace Herald.Tray;

/// <summary>
/// System tray icon and context menu using WinForms NotifyIcon.
/// Full menu with engine/voice/speed, toggles, hotkey config, and OCR options.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private TrayState _state = TrayState.Idle;

    // Cached icons for each state
    private readonly Icon _iconIdle;
    private readonly Icon _iconGenerating;
    private readonly Icon _iconSpeaking;
    private readonly Icon _iconPaused;

    // --- Callbacks ---
    public Action? OnQuit { get; set; }
    public Action? OnPauseToggle { get; set; }
    public Action<string>? OnVoiceChange { get; set; }
    public Action<int>? OnSpeedChange { get; set; }
    public Action<string>? OnEngineChange { get; set; }
    public Action<int>? OnLineDelayChange { get; set; }
    public Action<string>? OnReadModeChange { get; set; }
    public Action<bool>? OnLogPreviewChange { get; set; }
    public Action<bool>? OnAutoCopyChange { get; set; }
    public Action<bool>? OnOcrToClipboardChange { get; set; }
    public Action<bool>? OnAutoReadChange { get; set; }
    public Action<bool>? OnFilterCodeChange { get; set; }
    public Action<bool>? OnNormalizeTextChange { get; set; }
    public Action<string, string>? OnHotkeyChange { get; set; }
    public Action? OnResetHotkeys { get; set; }

    // Kokoro voices (best quality first)
    public static readonly (string id, string label)[] KokoroVoices =
    [
        ("heart", "Heart (Female, Premium)"),
        ("bella", "Bella (Female)"),
        ("nicole", "Nicole (Female)"),
        ("sarah", "Sarah (Female)"),
        ("nova", "Nova (Female)"),
        ("sky", "Sky (Female)"),
        ("michael", "Michael (Male)"),
        ("fenrir", "Fenrir (Male)"),
        ("puck", "Puck (Male)"),
        ("adam", "Adam (Male)"),
        ("emma", "Emma (British Female)"),
        ("daniel", "Daniel (British Male)"),
    ];

    // Edge voices
    public static readonly (string id, string label)[] EdgeVoices =
    [
        ("aria", "Aria (Female)"),
        ("guy", "Guy (Male)"),
        ("jenny", "Jenny (Female)"),
        ("christopher", "Christopher (Male)"),
    ];

    // SAPI voices
    public static readonly (string id, string label)[] SapiVoices =
    [
        ("zira", "Zira (Female, Offline)"),
        ("david", "David (Male, Offline)"),
    ];

    // Speed presets
    public static readonly (int wpm, string label)[] SpeedPresets =
    [
        (150, "150 wpm (Slow)"),
        (200, "200 wpm"),
        (300, "300 wpm"),
        (400, "400 wpm"),
        (500, "500 wpm"),
        (600, "600 wpm"),
        (700, "700 wpm"),
        (800, "800 wpm"),
        (900, "900 wpm (Default)"),
        (1000, "1000 wpm"),
        (1100, "1100 wpm"),
        (1200, "1200 wpm"),
        (1500, "1500 wpm (Max)"),
    ];

    // Line delay presets
    public static readonly (int ms, string label)[] DelayPresets =
    [
        (0, "No delay"),
        (100, "100ms"),
        (250, "250ms"),
        (500, "500ms"),
        (1000, "1 second"),
        (2000, "2 seconds"),
    ];

    // Read modes
    public static readonly (string id, string label)[] ReadModes =
    [
        ("lines", "Line by Line"),
        ("continuous", "Continuous"),
    ];

    // Hotkey presets per setting key
    private static readonly Dictionary<string, (string value, string label)[]> HotkeyPresets = new()
    {
        ["hotkey_speak"] =
        [
            ("ctrl+shift+s", "Ctrl + Shift + S"),
            ("ctrl+alt+s", "Ctrl + Alt + S"),
            ("f9", "F9"),
        ],
        ["hotkey_pause"] =
        [
            ("ctrl+shift+p", "Ctrl + Shift + P"),
            ("ctrl+alt+p", "Ctrl + Alt + P"),
            ("f10", "F10"),
        ],
        ["hotkey_stop"] =
        [
            ("escape", "Escape"),
            ("ctrl+shift+x", "Ctrl + Shift + X"),
            ("f11", "F11"),
        ],
        ["hotkey_speed_up"] =
        [
            ("ctrl+shift+]", "Ctrl + Shift + ]"),
            ("ctrl+shift+=", "Ctrl + Shift + ="),
        ],
        ["hotkey_speed_down"] =
        [
            ("ctrl+shift+[", "Ctrl + Shift + ["),
            ("ctrl+shift+-", "Ctrl + Shift + -"),
        ],
        ["hotkey_next"] =
        [
            ("ctrl+shift+n", "Ctrl + Shift + N"),
            ("ctrl+alt+n", "Ctrl + Alt + N"),
        ],
        ["hotkey_prev"] =
        [
            ("ctrl+shift+b", "Ctrl + Shift + B"),
            ("ctrl+alt+b", "Ctrl + Alt + B"),
        ],
        ["hotkey_ocr"] =
        [
            ("ctrl+shift+o", "Ctrl + Shift + O"),
            ("ctrl+alt+o", "Ctrl + Alt + O"),
        ],
        ["hotkey_monitor"] =
        [
            ("ctrl+shift+m", "Ctrl + Shift + M"),
            ("ctrl+alt+m", "Ctrl + Alt + M"),
        ],
        ["hotkey_quit"] =
        [
            ("ctrl+shift+q", "Ctrl + Shift + Q"),
            ("ctrl+alt+q", "Ctrl + Alt + Q"),
        ],
    };

    // Hotkey categories for organized display
    private static readonly (string category, string[] keys)[] HotkeyCategories =
    [
        ("Reading", ["hotkey_speak", "hotkey_pause", "hotkey_stop"]),
        ("Navigation", ["hotkey_next", "hotkey_prev", "hotkey_speed_up", "hotkey_speed_down"]),
        ("OCR", ["hotkey_ocr", "hotkey_monitor"]),
        ("App", ["hotkey_quit"]),
    ];

    // Friendly names for hotkey settings
    private static readonly Dictionary<string, string> HotkeyNames = new()
    {
        ["hotkey_speak"] = "Speak",
        ["hotkey_pause"] = "Pause/Resume",
        ["hotkey_stop"] = "Stop",
        ["hotkey_speed_up"] = "Speed Up",
        ["hotkey_speed_down"] = "Speed Down",
        ["hotkey_next"] = "Next Line",
        ["hotkey_prev"] = "Previous Line",
        ["hotkey_ocr"] = "OCR Region",
        ["hotkey_monitor"] = "Toggle Monitor",
        ["hotkey_quit"] = "Quit",
    };

    public TrayIcon()
    {
        _iconIdle = CreateSpeakerIcon(Color.Gray);
        _iconGenerating = CreateSpeakerIcon(Color.Orange);
        _iconSpeaking = CreateSpeakerIcon(Color.LimeGreen);
        _iconPaused = CreateSpeakerIcon(Color.Gold);
        _menu = new ContextMenuStrip();
    }

    public void Show(Settings settings)
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = _iconIdle,
            Text = "Herald — Text-to-Speech",
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _notifyIcon.DoubleClick += (_, _) => OnPauseToggle?.Invoke();
        RebuildMenu(settings);
    }

    public void RebuildMenu(Settings settings)
    {
        _menu.Items.Clear();

        // --- Header ---
        var header = new ToolStripMenuItem($"Herald v0.3.0 — {StateLabel()}") { Enabled = false };
        _menu.Items.Add(header);
        _menu.Items.Add(new ToolStripSeparator());

        // --- Engine ---
        var engineMenu = new ToolStripMenuItem("Engine");
        AddRadioItem(engineMenu, "Kokoro (Local Neural)", settings.Engine == "kokoro",
            () => OnEngineChange?.Invoke("kokoro"));
        AddRadioItem(engineMenu, "Edge (Online)", settings.Engine == "edge",
            () => OnEngineChange?.Invoke("edge"));
        AddRadioItem(engineMenu, "SAPI (Offline)", settings.Engine == "pyttsx3",
            () => OnEngineChange?.Invoke("pyttsx3"));
        _menu.Items.Add(engineMenu);

        // --- Voice ---
        var voiceMenu = new ToolStripMenuItem("Voice");
        var voices = settings.Engine switch
        {
            "kokoro" => KokoroVoices,
            "edge" => EdgeVoices,
            _ => SapiVoices,
        };
        foreach (var (id, label) in voices)
        {
            var voiceId = id;
            AddRadioItem(voiceMenu, label,
                string.Equals(settings.Voice, id, StringComparison.OrdinalIgnoreCase),
                () => OnVoiceChange?.Invoke(voiceId));
        }
        _menu.Items.Add(voiceMenu);

        // --- Speed ---
        var speedMenu = new ToolStripMenuItem($"Speed ({settings.Rate} wpm)");
        foreach (var (wpm, label) in SpeedPresets)
        {
            var speed = wpm;
            var displayLabel = GetSpeedLabel(wpm, settings.Engine);
            AddRadioItem(speedMenu, displayLabel, settings.Rate == wpm,
                () => OnSpeedChange?.Invoke(speed));
        }
        _menu.Items.Add(speedMenu);

        // --- Line Delay ---
        var delayMenu = new ToolStripMenuItem("Line Delay");
        foreach (var (ms, label) in DelayPresets)
        {
            var delay = ms;
            AddRadioItem(delayMenu, label, settings.LineDelay == ms,
                () => OnLineDelayChange?.Invoke(delay));
        }
        _menu.Items.Add(delayMenu);

        // --- Read Mode ---
        var modeMenu = new ToolStripMenuItem("Read Mode");
        foreach (var (id, label) in ReadModes)
        {
            var mode = id;
            AddRadioItem(modeMenu, label, settings.ReadMode == id,
                () => OnReadModeChange?.Invoke(mode));
        }
        _menu.Items.Add(modeMenu);

        _menu.Items.Add(new ToolStripSeparator());

        // --- Toggles ---
        AddToggle("Auto-Copy Selection", settings.AutoCopy,
            v => OnAutoCopyChange?.Invoke(v));
        AddToggle("Filter Code/URLs", settings.FilterCode,
            v => OnFilterCodeChange?.Invoke(v));
        AddToggle("Normalize Text", settings.NormalizeText,
            v => OnNormalizeTextChange?.Invoke(v));
        AddToggle("Log Preview", settings.LogPreview,
            v => OnLogPreviewChange?.Invoke(v));
        AddToggle("OCR to Clipboard", settings.OcrToClipboard,
            v => OnOcrToClipboardChange?.Invoke(v));
        AddToggle("Auto-Read Region", settings.AutoRead,
            v => OnAutoReadChange?.Invoke(v));

        _menu.Items.Add(new ToolStripSeparator());

        // --- Hotkeys ---
        var hotkeyMenu = new ToolStripMenuItem("Hotkeys");
        foreach (var (category, keys) in HotkeyCategories)
        {
            var catMenu = new ToolStripMenuItem(category);
            foreach (var key in keys)
            {
                var name = HotkeyNames.GetValueOrDefault(key, key);
                var current = settings.GetHotkey(key);
                var keyMenu = new ToolStripMenuItem($"{name}: {current}");

                if (HotkeyPresets.TryGetValue(key, out var presets))
                {
                    foreach (var (value, label) in presets)
                    {
                        var hkKey = key;
                        var hkValue = value;
                        AddRadioItem(keyMenu, label, current == value,
                            () => OnHotkeyChange?.Invoke(hkKey, hkValue));
                    }
                }
                catMenu.DropDownItems.Add(keyMenu);
            }
            hotkeyMenu.DropDownItems.Add(catMenu);
        }

        hotkeyMenu.DropDownItems.Add(new ToolStripSeparator());
        hotkeyMenu.DropDownItems.Add(new ToolStripMenuItem("Reset to Defaults",
            null, (_, _) => OnResetHotkeys?.Invoke()));

        _menu.Items.Add(hotkeyMenu);

        _menu.Items.Add(new ToolStripSeparator());

        // --- Quit ---
        _menu.Items.Add(new ToolStripMenuItem("Quit Herald", null, (_, _) => OnQuit?.Invoke()));
    }

    public void SetState(TrayState state)
    {
        if (_state == state) return;
        _state = state;

        if (_notifyIcon == null) return;

        _notifyIcon.Icon = state switch
        {
            TrayState.Generating => _iconGenerating,
            TrayState.Speaking => _iconSpeaking,
            TrayState.Paused => _iconPaused,
            _ => _iconIdle,
        };
        _notifyIcon.Text = $"Herald — {StateLabel()}";
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        _menu.Dispose();
        _iconIdle.Dispose();
        _iconGenerating.Dispose();
        _iconSpeaking.Dispose();
        _iconPaused.Dispose();
    }

    private string StateLabel() => _state switch
    {
        TrayState.Generating => "Generating...",
        TrayState.Speaking => "Speaking",
        TrayState.Paused => "Paused",
        _ => "Idle",
    };

    private void AddToggle(string text, bool isChecked, Action<bool> onChange)
    {
        var item = new ToolStripMenuItem(text)
        {
            Checked = isChecked,
            CheckOnClick = true,
        };
        item.CheckedChanged += (_, _) => onChange(item.Checked);
        _menu.Items.Add(item);
    }

    private static void AddRadioItem(ToolStripMenuItem parent, string text, bool isChecked, Action onClick)
    {
        var item = new ToolStripMenuItem(text)
        {
            Checked = isChecked,
            CheckOnClick = false,
        };
        item.Click += (_, _) => onClick();
        parent.DropDownItems.Add(item);
    }

    /// <summary>
    /// Get engine-aware label for a speed preset.
    /// Kokoro labels show the actual multiplier and clamping warnings.
    /// </summary>
    internal static string GetSpeedLabel(int wpm, string engine)
    {
        var baseLabel = SpeedPresets.FirstOrDefault(p => p.wpm == wpm).label
            ?? $"{wpm} wpm";

        if (!string.Equals(engine, "kokoro", StringComparison.OrdinalIgnoreCase))
            return baseLabel;

        float speed = wpm / 200f;

        if (wpm >= 700)
            return $"{baseLabel} — Kokoro capped 3.0x";
        if (wpm >= 400)
            return $"{baseLabel} — Kokoro {speed:F1}x";
        if (wpm >= 300)
            return $"{baseLabel} — Kokoro max quality";

        return baseLabel;
    }

    /// <summary>
    /// Create a simple speaker icon with the given color.
    /// 16x16 bitmap with a speaker shape.
    /// </summary>
    private static Icon CreateSpeakerIcon(Color color)
    {
        const int size = 16;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, 1.5f);

        // Speaker body
        g.FillRectangle(brush, 2, 5, 4, 6);

        // Speaker cone
        var cone = new Point[] { new(6, 5), new(10, 2), new(10, 13), new(6, 11) };
        g.FillPolygon(brush, cone);

        // Sound waves
        g.DrawArc(pen, 10, 4, 4, 8, -45, 90);

        return Icon.FromHandle(bmp.GetHicon());
    }
}

public enum TrayState
{
    Idle,
    Generating,
    Speaking,
    Paused,
}
