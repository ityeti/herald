using System.Drawing;
using Serilog;
using Herald.Config;

namespace Herald.Ocr;

/// <summary>
/// Persistent region monitor with border overlay.
/// Periodically captures a screen region, runs OCR, and fires when text changes.
/// Shows a visible border overlay around the monitored region.
/// </summary>
public sealed class PersistentRegion : IDisposable
{
    private Rectangle _region;
    private BorderOverlayForm? _borderForm;
    private System.Threading.Timer? _pollTimer;
    private string _lastText = "";
    private volatile bool _active;
    private readonly object _lock = new();

    /// <summary>Fired when OCR detects changed text in the region.</summary>
    public event Action<string>? TextChanged;

    public bool IsActive => _active;
    public Rectangle Region => _region;

    /// <summary>
    /// Start monitoring a screen region for text changes.
    /// </summary>
    public void Start(Rectangle region, double intervalSeconds = 2.5, double changeThreshold = 0.5)
    {
        Stop();

        _region = region;
        _active = true;
        _lastText = "";

        // Show border overlay
        ShowBorder(region);

        // Start polling timer
        var intervalMs = (int)(intervalSeconds * 1000);
        _pollTimer = new System.Threading.Timer(
            _ => PollRegion(changeThreshold),
            null,
            intervalMs,
            intervalMs);

        Log.Information("Persistent region started: {Region}, interval={Interval}s", region, intervalSeconds);
    }

    /// <summary>Stop monitoring and remove the border overlay.</summary>
    public void Stop()
    {
        _active = false;

        _pollTimer?.Dispose();
        _pollTimer = null;

        HideBorder();

        Log.Debug("Persistent region stopped");
    }

    /// <summary>Toggle the persistent region on/off. If off, prompts for region selection.</summary>
    public void Toggle()
    {
        if (_active)
        {
            Stop();
        }
        else
        {
            var region = RegionSelector.SelectRegion();
            if (region.HasValue)
            {
                Start(region.Value);
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private async void PollRegion(double changeThreshold)
    {
        if (!_active) return;

        try
        {
            using var bmp = WinOcr.CaptureRegion(_region);
            if (bmp == null) return;

            var text = await WinOcr.RecognizeAsync(bmp);
            if (string.IsNullOrWhiteSpace(text)) return;

            // Check if text has changed significantly
            var similarity = ComputeSimilarity(_lastText, text);
            if (similarity < (1.0 - changeThreshold))
            {
                _lastText = text;
                Log.Debug("Persistent region text changed ({Similarity:P0} similar)", similarity);
                TextChanged?.Invoke(text);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Persistent region poll error");
        }
    }

    /// <summary>
    /// Simple character-level similarity ratio between two strings.
    /// Returns 0.0 (completely different) to 1.0 (identical).
    /// </summary>
    private static double ComputeSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        int maxLen = Math.Max(a.Length, b.Length);
        int matchLen = Math.Min(a.Length, b.Length);
        int matches = 0;

        for (int i = 0; i < matchLen; i++)
        {
            if (a[i] == b[i]) matches++;
        }

        return (double)matches / maxLen;
    }

    private void ShowBorder(Rectangle region)
    {
        // Run on STA thread for WinForms
        var thread = new Thread(() =>
        {
            _borderForm = new BorderOverlayForm(region);
            Application.Run(_borderForm);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    private void HideBorder()
    {
        if (_borderForm != null)
        {
            try
            {
                _borderForm.Invoke(() => _borderForm.Close());
            }
            catch { }
            _borderForm = null;
        }
    }

    /// <summary>
    /// Transparent form that draws a colored border around the monitored region.
    /// </summary>
    private sealed class BorderOverlayForm : Form
    {
        private const int BorderWidth = 3;
        private readonly Rectangle _region;

        public BorderOverlayForm(Rectangle region)
        {
            _region = region;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            TransparencyKey = BackColor = Color.Magenta;

            // Position form around the region with border padding
            Location = new Point(region.X - BorderWidth, region.Y - BorderWidth);
            Size = new Size(region.Width + BorderWidth * 2, region.Height + BorderWidth * 2);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x80000 | 0x20 | 0x80;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(Color.Cyan, BorderWidth);
            e.Graphics.DrawRectangle(pen,
                BorderWidth / 2, BorderWidth / 2,
                Width - BorderWidth, Height - BorderWidth);
        }
    }
}
