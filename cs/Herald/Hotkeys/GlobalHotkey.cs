using System.Runtime.InteropServices;
using Serilog;

namespace Herald.Hotkeys;

/// <summary>
/// Global hotkey registration using Win32 RegisterHotKey API.
/// Hotkey callbacks only enqueue action strings — never block the message pump.
/// This matches the Python "action queue" pattern that prevents keyboard hook death.
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;

    // Modifier flags
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CTRL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private readonly HotkeyMessageWindow _window;
    private readonly Dictionary<int, string> _registeredActions = new();
    private int _nextId = 1;

    /// <summary>Fired when a hotkey is pressed. Value is the action name (e.g. "speak", "pause").</summary>
    public event Action<string>? HotkeyPressed;

    public GlobalHotkeyManager()
    {
        _window = new HotkeyMessageWindow(this);
    }

    /// <summary>
    /// Register a hotkey string (e.g. "ctrl+shift+s") mapped to an action name.
    /// Returns the hotkey ID, or -1 on failure.
    /// </summary>
    public int Register(string hotkeyString, string actionName)
    {
        if (!ParseHotkey(hotkeyString, out uint modifiers, out uint vk))
        {
            Log.Warning("Failed to parse hotkey: {Hotkey}", hotkeyString);
            return -1;
        }

        int id = _nextId++;
        modifiers |= MOD_NOREPEAT;

        if (!RegisterHotKey(_window.Handle, id, modifiers, vk))
        {
            var error = Marshal.GetLastWin32Error();
            Log.Warning("Failed to register hotkey {Hotkey} (error {Error})", hotkeyString, error);
            return -1;
        }

        _registeredActions[id] = actionName;
        Log.Debug("Registered hotkey {Hotkey} → {Action} (id={Id})", hotkeyString, actionName, id);
        return id;
    }

    /// <summary>Unregister all hotkeys.</summary>
    public void UnregisterAll()
    {
        foreach (var id in _registeredActions.Keys)
        {
            UnregisterHotKey(_window.Handle, id);
        }
        _registeredActions.Clear();
        _nextId = 1;
        Log.Debug("All hotkeys unregistered");
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.Dispose();
    }

    internal void OnHotkeyMessage(int id)
    {
        if (_registeredActions.TryGetValue(id, out var action))
        {
            HotkeyPressed?.Invoke(action);
        }
    }

    /// <summary>
    /// Parse a hotkey string like "ctrl+shift+s" into Win32 modifiers and virtual key code.
    /// </summary>
    private static bool ParseHotkey(string hotkey, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = hotkey.ToLower().Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        foreach (var part in parts[..^1]) // All but last = modifiers
        {
            switch (part)
            {
                case "ctrl" or "control": modifiers |= MOD_CTRL; break;
                case "shift": modifiers |= MOD_SHIFT; break;
                case "alt": modifiers |= MOD_ALT; break;
                case "win": modifiers |= MOD_WIN; break;
                default:
                    Log.Warning("Unknown modifier: {Mod}", part);
                    return false;
            }
        }

        var key = parts[^1]; // Last part = the key
        vk = KeyToVirtualKey(key);
        return vk != 0;
    }

    /// <summary>Map a key name to Win32 virtual key code.</summary>
    private static uint KeyToVirtualKey(string key) => key.ToLower() switch
    {
        // Letters
        var k when k.Length == 1 && char.IsAsciiLetter(k[0]) => (uint)char.ToUpper(k[0]),
        // Numbers
        var k when k.Length == 1 && char.IsDigit(k[0]) => (uint)k[0],
        // Function keys
        var k when k.StartsWith('f') && int.TryParse(k[1..], out int fn) && fn >= 1 && fn <= 24 => (uint)(0x6F + fn),
        // Special keys
        "escape" or "esc" => 0x1B,
        "space" => 0x20,
        "enter" or "return" => 0x0D,
        "tab" => 0x09,
        "backspace" => 0x08,
        "delete" or "del" => 0x2E,
        "insert" or "ins" => 0x2D,
        "home" => 0x24,
        "end" => 0x23,
        "pageup" or "pgup" => 0x21,
        "pagedown" or "pgdn" => 0x22,
        "up" => 0x26,
        "down" => 0x28,
        "left" => 0x25,
        "right" => 0x27,
        // Punctuation (OEM keys)
        "[" or "{" => 0xDB, // VK_OEM_4
        "]" or "}" => 0xDD, // VK_OEM_6
        ";" or ":" => 0xBA, // VK_OEM_1
        "'" or "\"" => 0xDE, // VK_OEM_7
        "," or "<" => 0xBC, // VK_OEM_COMMA
        "." or ">" => 0xBE, // VK_OEM_PERIOD
        "/" or "?" => 0xBF, // VK_OEM_2
        "\\" or "|" => 0xDC, // VK_OEM_5
        "`" or "~" => 0xC0, // VK_OEM_3
        "-" or "_" => 0xBD, // VK_OEM_MINUS
        "=" or "+" => 0xBB, // VK_OEM_PLUS
        _ => 0,
    };

    /// <summary>Hidden window to receive WM_HOTKEY messages.</summary>
    private sealed class HotkeyMessageWindow : NativeWindow, IDisposable
    {
        private readonly GlobalHotkeyManager _manager;

        public HotkeyMessageWindow(GlobalHotkeyManager manager)
        {
            _manager = manager;
            var cp = new CreateParams
            {
                Caption = "HeraldHotkeyWindow",
                Style = 0, // Not visible
            };
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                _manager.OnHotkeyMessage((int)m.WParam);
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            DestroyHandle();
        }
    }
}
