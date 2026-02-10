using System.Drawing;
using Herald.Config;
using Serilog;

namespace Herald.Tray;

/// <summary>
/// System tray icon and context menu using WinForms NotifyIcon.
/// Equivalent to Python's pystray-based tray_app.py.
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

    // Callbacks
    public Action? OnQuit { get; set; }
    public Action? OnPauseToggle { get; set; }
    public Action<string>? OnVoiceChange { get; set; }
    public Action<int>? OnSpeedChange { get; set; }
    public Action<string>? OnEngineChange { get; set; }

    // Edge voices
    public static readonly (string id, string label)[] EdgeVoices =
    [
        ("aria", "Aria (Female)"),
        ("guy", "Guy (Male)"),
        ("jenny", "Jenny (Female)"),
        ("christopher", "Christopher (Male)"),
    ];

    // Offline voices
    public static readonly (string id, string label)[] OfflineVoices =
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

        // Status header
        var header = new ToolStripMenuItem($"Herald v0.3.0 — {StateLabel()}") { Enabled = false };
        _menu.Items.Add(header);
        _menu.Items.Add(new ToolStripSeparator());

        // Engine submenu
        var engineMenu = new ToolStripMenuItem("Engine");
        AddRadioItem(engineMenu, "Edge (Online)", settings.Engine == "edge", () => OnEngineChange?.Invoke("edge"));
        AddRadioItem(engineMenu, "SAPI (Offline)", settings.Engine == "pyttsx3", () => OnEngineChange?.Invoke("pyttsx3"));
        _menu.Items.Add(engineMenu);

        // Voice submenu
        var voiceMenu = new ToolStripMenuItem("Voice");
        var voices = settings.Engine == "edge" ? EdgeVoices : OfflineVoices;
        foreach (var (id, label) in voices)
        {
            var isSelected = string.Equals(settings.Voice, id, StringComparison.OrdinalIgnoreCase);
            var voiceId = id;
            AddRadioItem(voiceMenu, label, isSelected, () => OnVoiceChange?.Invoke(voiceId));
        }
        _menu.Items.Add(voiceMenu);

        // Speed submenu
        var speedMenu = new ToolStripMenuItem("Speed");
        foreach (var (wpm, label) in SpeedPresets)
        {
            var isSelected = settings.Rate == wpm;
            var speed = wpm;
            AddRadioItem(speedMenu, label, isSelected, () => OnSpeedChange?.Invoke(speed));
        }
        _menu.Items.Add(speedMenu);

        _menu.Items.Add(new ToolStripSeparator());

        // Quit
        var quitItem = new ToolStripMenuItem("Quit Herald", null, (_, _) => OnQuit?.Invoke());
        _menu.Items.Add(quitItem);
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
    /// Create a simple speaker icon with the given color, matching the Python PIL-generated icons.
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

        // Speaker body (rectangle)
        g.FillRectangle(brush, 2, 5, 4, 6);

        // Speaker cone (triangle)
        var cone = new Point[] { new(6, 5), new(10, 2), new(10, 13), new(6, 11) };
        g.FillPolygon(brush, cone);

        // Sound waves (arcs)
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
