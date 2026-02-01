"""
Herald Region Capture

Provides screen region selection for OCR.
Uses subprocess to run a separate Python script for tkinter (avoids thread issues).
Supports multi-monitor setups.
"""

import subprocess
import sys
import json
import tempfile
import ctypes
from PIL import ImageGrab
from typing import Optional, Tuple
from loguru import logger
from pathlib import Path


def get_virtual_screen_bounds() -> Tuple[int, int, int, int]:
    """
    Get the bounding box of the entire virtual screen (all monitors).

    Returns:
        Tuple of (left, top, width, height) for the virtual screen.
    """
    try:
        # Enable DPI awareness for accurate coordinates
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(2)
        except Exception:
            try:
                ctypes.windll.user32.SetProcessDPIAware()
            except Exception:
                pass

        user32 = ctypes.windll.user32
        # SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77
        # SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79
        left = user32.GetSystemMetrics(76)
        top = user32.GetSystemMetrics(77)
        width = user32.GetSystemMetrics(78)
        height = user32.GetSystemMetrics(79)
        return (left, top, width, height)
    except Exception as e:
        logger.error(f"Failed to get virtual screen bounds: {e}")
        # Fallback to primary monitor
        user32 = ctypes.windll.user32
        return (0, 0, user32.GetSystemMetrics(0), user32.GetSystemMetrics(1))


# Standalone script that runs in a separate process
# Takes virtual screen bounds as command line arguments
SELECTOR_SCRIPT = '''
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
        import ctypes
        user32 = ctypes.windll.user32
        left, top = 0, 0
        width = user32.GetSystemMetrics(0)
        height = user32.GetSystemMetrics(1)

    RegionSelector(left, top, width, height).run()
'''


def select_region() -> Optional[Tuple[int, int, int, int]]:
    """
    Show fullscreen overlay and let user draw a selection box.
    Runs in a separate process to avoid tkinter threading issues.
    Supports multi-monitor setups.

    Returns:
        Tuple of (x1, y1, x2, y2) for the selected region, or None if cancelled.
    """
    try:
        # Get virtual screen bounds (all monitors)
        vscreen = get_virtual_screen_bounds()
        logger.debug(f"Virtual screen: left={vscreen[0]}, top={vscreen[1]}, "
                     f"width={vscreen[2]}, height={vscreen[3]}")

        # Write the selector script to a temp file
        script_path = Path(tempfile.gettempdir()) / "herald_region_selector.py"
        script_path.write_text(SELECTOR_SCRIPT)

        # Run in subprocess with virtual screen bounds as arguments
        result = subprocess.run(
            [sys.executable, str(script_path),
             str(vscreen[0]), str(vscreen[1]), str(vscreen[2]), str(vscreen[3])],
            capture_output=True,
            text=True,
            timeout=120,  # 2 minute timeout
            creationflags=subprocess.CREATE_NO_WINDOW if sys.platform == 'win32' else 0
        )

        # Parse output
        if result.stdout.strip():
            data = json.loads(result.stdout.strip())
            region = data.get("region")
            if region:
                return tuple(region)

        if result.returncode != 0:
            logger.debug(f"Region selector exited with code {result.returncode}")
            if result.stderr:
                logger.debug(f"Stderr: {result.stderr}")

        return None

    except subprocess.TimeoutExpired:
        logger.warning("Region selection timed out")
        return None
    except json.JSONDecodeError as e:
        logger.error(f"Failed to parse region selector output: {e}")
        return None
    except Exception as e:
        logger.error(f"Region selection failed: {e}", exc_info=True)
        return None


def capture_region(region: Tuple[int, int, int, int]):
    """
    Capture a screenshot of the specified region.

    Args:
        region: Tuple of (x1, y1, x2, y2) screen coordinates.

    Returns:
        PIL Image of the captured region.
    """
    try:
        # ImageGrab.grab takes bbox as (left, top, right, bottom)
        # It handles multi-monitor setups and negative coordinates
        image = ImageGrab.grab(bbox=region, all_screens=True)
        return image
    except Exception as e:
        logger.error(f"Screenshot failed: {e}")
        return None


def select_and_capture():
    """
    Show region selector and capture the selected area.

    Returns:
        PIL Image of the captured region, or None if cancelled.
    """
    logger.debug("Starting region selection...")
    region = select_region()

    if region is None:
        logger.info("Region selection cancelled or failed")
        return None

    logger.debug(f"Capturing region: {region}")
    return capture_region(region)


# Self-test
if __name__ == "__main__":
    print("Testing region capture (multi-monitor)...")
    print(f"Virtual screen bounds: {get_virtual_screen_bounds()}")
    print("Select a region with your mouse. Press Escape to cancel.")

    image = select_and_capture()

    if image:
        print(f"Captured {image.size[0]}x{image.size[1]} image")
        image.save("test_capture.png")
        print("Saved to test_capture.png")
    else:
        print("No region selected")
