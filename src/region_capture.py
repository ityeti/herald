"""
Herald Region Capture

Provides screen region selection for OCR.
Uses subprocess to run a separate Python script for tkinter (avoids thread issues).
"""

import subprocess
import sys
import json
import tempfile
import os
from PIL import ImageGrab
from typing import Optional, Tuple
from loguru import logger
from pathlib import Path


# Standalone script that runs in a separate process
SELECTOR_SCRIPT = '''
import tkinter as tk
import json
import sys

class RegionSelector:
    def __init__(self):
        self.start_x = 0
        self.start_y = 0
        self.end_x = 0
        self.end_y = 0
        self.rect_id = None
        self.selection_made = False

    def run(self):
        self.root = tk.Tk()
        self.root.attributes('-fullscreen', True)
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
            x1 = min(self.start_x, self.end_x)
            y1 = min(self.start_y, self.end_y)
            x2 = max(self.start_x, self.end_x)
            y2 = max(self.start_y, self.end_y)
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
    RegionSelector().run()
'''


def select_region() -> Optional[Tuple[int, int, int, int]]:
    """
    Show fullscreen overlay and let user draw a selection box.
    Runs in a separate process to avoid tkinter threading issues.

    Returns:
        Tuple of (x1, y1, x2, y2) for the selected region, or None if cancelled.
    """
    try:
        # Write the selector script to a temp file
        script_path = Path(tempfile.gettempdir()) / "herald_region_selector.py"
        script_path.write_text(SELECTOR_SCRIPT)

        # Run in subprocess using the same Python interpreter
        result = subprocess.run(
            [sys.executable, str(script_path)],
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
        region: Tuple of (x1, y1, x2, y2) coordinates.

    Returns:
        PIL Image of the captured region.
    """
    try:
        # ImageGrab.grab takes bbox as (left, top, right, bottom)
        image = ImageGrab.grab(bbox=region)
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
    print("Testing region capture...")
    print("Select a region with your mouse. Press Escape to cancel.")

    image = select_and_capture()

    if image:
        print(f"Captured {image.size[0]}x{image.size[1]} image")
        image.save("test_capture.png")
        print("Saved to test_capture.png")
    else:
        print("No region selected")
