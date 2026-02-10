using System.Runtime.InteropServices;
using Serilog;

namespace Herald.Util;

/// <summary>
/// Clipboard operations. Must be called from an STA thread (WinForms message pump).
/// </summary>
public static class ClipboardHelper
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_CONTROL = 0x11;
    private const byte VK_C = 0x43;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>Read text from the clipboard. Returns null if empty or not text.</summary>
    public static string? GetText()
    {
        try
        {
            if (Clipboard.ContainsText())
                return Clipboard.GetText();
        }
        catch (ExternalException ex)
        {
            Log.Debug(ex, "Clipboard access failed");
        }
        return null;
    }

    /// <summary>Check if clipboard contains an image.</summary>
    public static bool HasImage()
    {
        try
        {
            return Clipboard.ContainsImage();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Simulate Ctrl+C to copy the current selection.</summary>
    public static void SimulateCopy()
    {
        // Small delay to ensure the key state is clean
        Thread.Sleep(50);

        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_C, 0, 0, UIntPtr.Zero);
        keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        // Wait for clipboard to update
        Thread.Sleep(150);
    }
}
