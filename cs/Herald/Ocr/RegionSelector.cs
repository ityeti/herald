using System.Drawing;
using System.Runtime.InteropServices;
using Serilog;

namespace Herald.Ocr;

/// <summary>
/// Transparent overlay form for selecting a screen region.
/// Replaces the Python tkinter subprocess approach with a native WinForms form.
/// Covers all monitors (virtual screen), draws selection rectangle with mouse.
/// </summary>
public sealed class RegionSelector
{
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private const int MinRegionSize = 10;

    /// <summary>
    /// Get the virtual screen bounds (all monitors combined).
    /// Coordinates can be negative on multi-monitor setups.
    /// </summary>
    public static Rectangle GetVirtualScreenBounds()
    {
        int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        return new Rectangle(left, top, width, height);
    }

    /// <summary>
    /// Show the region selector overlay and return the selected region.
    /// Returns null if cancelled (Escape) or region too small.
    /// Runs on a new STA thread to avoid blocking the main message pump.
    /// </summary>
    public static Rectangle? SelectRegion()
    {
        Rectangle? result = null;
        var done = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                using var form = new SelectorForm();
                Application.Run(form);
                result = form.SelectedRegion;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Region selector failed");
            }
            finally
            {
                done.Set();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        // Wait up to 120 seconds for selection
        done.Wait(TimeSpan.FromSeconds(120));
        return result;
    }

    private sealed class SelectorForm : Form
    {
        public Rectangle? SelectedRegion { get; private set; }

        private Point _startPoint;
        private Point _currentPoint;
        private bool _dragging;
        private bool _selectionMade;

        public SelectorForm()
        {
            var bounds = GetVirtualScreenBounds();

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(bounds.Left, bounds.Top);
            Size = new Size(bounds.Width, bounds.Height);
            TopMost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;
            BackColor = Color.Gray;
            Opacity = 0.3;
            DoubleBuffered = true;

            KeyPreview = true;
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    _selectionMade = false;
                    Close();
                }
            };

            MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _startPoint = e.Location;
                    _currentPoint = e.Location;
                    _dragging = true;
                }
            };

            MouseMove += (_, e) =>
            {
                if (_dragging)
                {
                    _currentPoint = e.Location;
                    Invalidate();
                }
            };

            MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && _dragging)
                {
                    _dragging = false;
                    _currentPoint = e.Location;
                    _selectionMade = true;
                    FinalizeSelection();
                    Close();
                }
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!_dragging) return;

            var rect = GetSelectionRect();
            if (rect.Width < 2 || rect.Height < 2) return;

            // Semi-transparent white fill
            using var fillBrush = new SolidBrush(Color.FromArgb(80, 255, 255, 255));
            e.Graphics.FillRectangle(fillBrush, rect);

            // Red outline
            using var pen = new Pen(Color.Red, 2);
            e.Graphics.DrawRectangle(pen, rect);

            // Size label
            var label = $"{rect.Width} x {rect.Height}";
            using var font = new Font("Arial", 12, FontStyle.Bold);
            var labelSize = e.Graphics.MeasureString(label, font);
            var labelX = rect.X + (rect.Width - labelSize.Width) / 2;
            var labelY = rect.Y - labelSize.Height - 4;
            if (labelY < 0) labelY = rect.Y + 4;

            using var bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
            e.Graphics.FillRectangle(bgBrush, labelX - 2, labelY - 1, labelSize.Width + 4, labelSize.Height + 2);
            e.Graphics.DrawString(label, font, Brushes.White, labelX, labelY);
        }

        private void FinalizeSelection()
        {
            if (!_selectionMade) return;

            var rect = GetSelectionRect();
            if (rect.Width < MinRegionSize || rect.Height < MinRegionSize)
            {
                Log.Debug("Region too small ({W}x{H}), ignoring", rect.Width, rect.Height);
                return;
            }

            // Convert from form-local coordinates to screen coordinates
            var bounds = GetVirtualScreenBounds();
            SelectedRegion = new Rectangle(
                rect.X + bounds.Left,
                rect.Y + bounds.Top,
                rect.Width,
                rect.Height
            );

            Log.Debug("Region selected: {Region}", SelectedRegion);
        }

        private Rectangle GetSelectionRect()
        {
            int x = Math.Min(_startPoint.X, _currentPoint.X);
            int y = Math.Min(_startPoint.Y, _currentPoint.Y);
            int w = Math.Abs(_currentPoint.X - _startPoint.X);
            int h = Math.Abs(_currentPoint.Y - _startPoint.Y);
            return new Rectangle(x, y, w, h);
        }
    }
}
