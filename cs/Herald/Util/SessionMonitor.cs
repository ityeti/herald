using System.Runtime.InteropServices;
using Serilog;

namespace Herald.Util;

/// <summary>
/// Monitors RDP/console session changes via WM_WTSSESSION_CHANGE.
/// Fires SessionChanged when the user reconnects, so audio can be reinitialized.
/// </summary>
public sealed class SessionMonitor : IDisposable
{
    [DllImport("wtsapi32.dll")]
    private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);

    [DllImport("wtsapi32.dll")]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    private const int NOTIFY_FOR_THIS_SESSION = 0;
    private const int WM_WTSSESSION_CHANGE = 0x02B1;
    private const int WTS_SESSION_UNLOCK = 0x8;
    private const int WTS_CONSOLE_CONNECT = 0x1;
    private const int WTS_REMOTE_CONNECT = 0x3;

    private readonly SessionMessageWindow _window;

    public event Action? SessionChanged;

    public SessionMonitor()
    {
        _window = new SessionMessageWindow(this);
        if (!WTSRegisterSessionNotification(_window.Handle, NOTIFY_FOR_THIS_SESSION))
        {
            Log.Warning("Failed to register for session notifications");
        }
        else
        {
            Log.Debug("Session monitor registered");
        }
    }

    public void Dispose()
    {
        WTSUnRegisterSessionNotification(_window.Handle);
        _window.Dispose();
    }

    internal void OnSessionChange(int reason)
    {
        if (reason is WTS_SESSION_UNLOCK or WTS_CONSOLE_CONNECT or WTS_REMOTE_CONNECT)
        {
            Log.Information("Session change detected (reason={Reason}), will reinitialize audio", reason);
            SessionChanged?.Invoke();
        }
    }

    private sealed class SessionMessageWindow : NativeWindow, IDisposable
    {
        private readonly SessionMonitor _monitor;

        public SessionMessageWindow(SessionMonitor monitor)
        {
            _monitor = monitor;
            CreateHandle(new CreateParams
            {
                Caption = "HeraldSessionMonitor",
                Style = 0,
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_WTSSESSION_CHANGE)
            {
                _monitor.OnSessionChange((int)m.WParam);
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose() => DestroyHandle();
    }
}
