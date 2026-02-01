"""
Herald Overlay Border - Standalone Helper

Shows a persistent rounded border around a screen region.
Used for the persistent OCR region feature.

Usage:
    overlay_border.exe <x1> <y1> <x2> <y2>

Control:
    Send 'q' to stdin to quit, or press Escape.
"""

import tkinter as tk
import sys
import ctypes
import threading
import queue

# Enable DPI awareness
try:
    ctypes.windll.shcore.SetProcessDpiAwareness(2)
except Exception:
    try:
        ctypes.windll.user32.SetProcessDPIAware()
    except Exception:
        pass


class BorderOverlay:
    def __init__(self, x1, y1, x2, y2):
        # The capture region
        self.capture_x1 = x1
        self.capture_y1 = y1
        self.capture_x2 = x2
        self.capture_y2 = y2

        # Border sits OUTSIDE the capture region
        self.border_width = 4
        self.corner_radius = 12
        self.padding = self.border_width + 2  # Space between border and capture area

        # Overlay window is larger than capture region
        self.x1 = x1 - self.padding
        self.y1 = y1 - self.padding
        self.x2 = x2 + self.padding
        self.y2 = y2 + self.padding

        self.command_queue = queue.Queue()

    def run(self):
        self.root = tk.Tk()
        self.root.title("Herald OCR Region")

        # Calculate dimensions (includes padding for border)
        width = self.x2 - self.x1
        height = self.y2 - self.y1

        # Create a transparent window
        self.root.overrideredirect(True)
        self.root.geometry(f"{width}x{height}+{self.x1}+{self.y1}")
        self.root.attributes('-topmost', True)
        self.root.attributes('-transparentcolor', 'white')
        self.root.configure(bg='white')

        # Create canvas for border
        self.canvas = tk.Canvas(
            self.root,
            width=width,
            height=height,
            bg='white',
            highlightthickness=0
        )
        self.canvas.pack()

        # Colors - darker, more modern green
        border_color = '#2E7D32'  # Material Design Green 800

        # Draw rounded rectangle border
        self._draw_rounded_rect(
            self.padding - self.border_width // 2,
            self.padding - self.border_width // 2,
            width - self.padding + self.border_width // 2,
            height - self.padding + self.border_width // 2,
            self.corner_radius,
            border_color
        )

        # Close on Escape
        self.root.bind('<Escape>', lambda e: self.root.quit())

        # Start stdin reader thread
        self.stdin_thread = threading.Thread(target=self._read_stdin, daemon=True)
        self.stdin_thread.start()

        # Check for commands
        self.root.after(50, self._check_commands)

        self.root.mainloop()

    def _draw_rounded_rect(self, x1, y1, x2, y2, radius, color):
        """Draw a rounded rectangle border."""
        r = radius
        w = self.border_width

        # Four corner arcs
        self.canvas.create_arc(x1, y1, x1 + 2*r, y1 + 2*r, start=90, extent=90,
                               style='arc', outline=color, width=w)
        self.canvas.create_arc(x2 - 2*r, y1, x2, y1 + 2*r, start=0, extent=90,
                               style='arc', outline=color, width=w)
        self.canvas.create_arc(x2 - 2*r, y2 - 2*r, x2, y2, start=270, extent=90,
                               style='arc', outline=color, width=w)
        self.canvas.create_arc(x1, y2 - 2*r, x1 + 2*r, y2, start=180, extent=90,
                               style='arc', outline=color, width=w)

        # Four straight lines connecting the arcs
        self.canvas.create_line(x1 + r, y1, x2 - r, y1, fill=color, width=w)  # Top
        self.canvas.create_line(x1 + r, y2, x2 - r, y2, fill=color, width=w)  # Bottom
        self.canvas.create_line(x1, y1 + r, x1, y2 - r, fill=color, width=w)  # Left
        self.canvas.create_line(x2, y1 + r, x2, y2 - r, fill=color, width=w)  # Right

    def _read_stdin(self):
        """Read commands from stdin in background thread."""
        try:
            while True:
                char = sys.stdin.read(1)
                if char:
                    self.command_queue.put(char)
        except:
            pass

    def _check_commands(self):
        """Process commands from queue."""
        try:
            while True:
                cmd = self.command_queue.get_nowait()
                if cmd == 'q':
                    self.root.quit()
                    return
        except queue.Empty:
            pass
        self.root.after(50, self._check_commands)


if __name__ == "__main__":
    if len(sys.argv) >= 5:
        x1 = int(sys.argv[1])
        y1 = int(sys.argv[2])
        x2 = int(sys.argv[3])
        y2 = int(sys.argv[4])
        BorderOverlay(x1, y1, x2, y2).run()
    else:
        print("Usage: overlay_border.exe <x1> <y1> <x2> <y2>", file=sys.stderr)
        sys.exit(1)
