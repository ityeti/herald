"""
Herald Region Selector - Standalone Helper

Shows a fullscreen overlay for selecting a screen region.
Outputs JSON with the selected region coordinates.

Usage:
    region_selector.exe <left> <top> <width> <height>

Output (stdout):
    {"region": [x1, y1, x2, y2]} on success
    {"region": null} on cancel/escape
"""

import tkinter as tk
import json
import sys
import ctypes

# Enable DPI awareness BEFORE creating any windows
try:
    ctypes.windll.shcore.SetProcessDpiAwareness(2)  # Per-monitor DPI aware
except Exception:
    try:
        ctypes.windll.user32.SetProcessDPIAware()
    except Exception:
        pass


class RegionSelector:
    def __init__(self, vscreen_left, vscreen_top, vscreen_width, vscreen_height):
        self.vscreen_left = vscreen_left
        self.vscreen_top = vscreen_top
        self.vscreen_width = vscreen_width
        self.vscreen_height = vscreen_height
        self.start_x = 0
        self.start_y = 0
        self.end_x = 0
        self.end_y = 0
        self.rect_id = None
        self.selection_made = False

    def run(self):
        self.root = tk.Tk()

        # Position window to cover entire virtual screen (all monitors)
        self.root.overrideredirect(True)  # Remove window decorations
        self.root.geometry(f"{self.vscreen_width}x{self.vscreen_height}+{self.vscreen_left}+{self.vscreen_top}")
        self.root.attributes('-alpha', 0.3)
        self.root.attributes('-topmost', True)
        self.root.configure(bg='gray')
        self.root.config(cursor="crosshair")

        self.canvas = tk.Canvas(self.root, bg='gray', highlightthickness=0)
        self.canvas.pack(fill=tk.BOTH, expand=True)

        self.label = tk.Label(
            self.root,
            text="Click and drag to select region. Press Escape to cancel.",
            font=("Arial", 14),
            bg='black', fg='white',
            padx=10, pady=5
        )
        self.label.place(relx=0.5, y=30, anchor='center')

        self.canvas.bind('<Button-1>', self._on_press)
        self.canvas.bind('<B1-Motion>', self._on_drag)
        self.canvas.bind('<ButtonRelease-1>', self._on_release)
        self.root.bind('<Escape>', self._on_escape)

        self.root.mainloop()

        if self.selection_made:
            # Convert canvas coordinates to screen coordinates
            x1 = min(self.start_x, self.end_x) + self.vscreen_left
            y1 = min(self.start_y, self.end_y) + self.vscreen_top
            x2 = max(self.start_x, self.end_x) + self.vscreen_left
            y2 = max(self.start_y, self.end_y) + self.vscreen_top
            if (x2 - x1) >= 10 and (y2 - y1) >= 10:
                print(json.dumps({"region": [x1, y1, x2, y2]}))
                return
        print(json.dumps({"region": None}))

    def _on_press(self, event):
        self.start_x = event.x
        self.start_y = event.y
        if self.rect_id:
            self.canvas.delete(self.rect_id)
        self.rect_id = self.canvas.create_rectangle(
            self.start_x, self.start_y, self.start_x, self.start_y,
            outline='red', width=2, fill='white', stipple='gray50'
        )

    def _on_drag(self, event):
        self.end_x = event.x
        self.end_y = event.y
        if self.rect_id:
            self.canvas.coords(self.rect_id, self.start_x, self.start_y, self.end_x, self.end_y)

    def _on_release(self, event):
        self.end_x = event.x
        self.end_y = event.y
        self.selection_made = True
        self.root.quit()
        self.root.destroy()

    def _on_escape(self, event):
        self.selection_made = False
        self.root.quit()
        self.root.destroy()


if __name__ == "__main__":
    # Parse virtual screen bounds from command line
    if len(sys.argv) >= 5:
        left = int(sys.argv[1])
        top = int(sys.argv[2])
        width = int(sys.argv[3])
        height = int(sys.argv[4])
    else:
        # Fallback - just use fullscreen on primary
        user32 = ctypes.windll.user32
        left, top = 0, 0
        width = user32.GetSystemMetrics(0)
        height = user32.GetSystemMetrics(1)

    RegionSelector(left, top, width, height).run()
