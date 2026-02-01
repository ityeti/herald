"""
Herald Persistent Region

Provides a persistent screen region with visible border for continuous OCR.
- Alt+M: Toggle region on/off
- When active, shows a visible border around the selected area
- Alt+S reads from the persistent region instead of clipboard
"""

import subprocess
import sys
import json
import tempfile
import threading
import time
import ctypes
from PIL import ImageGrab
from typing import Optional, Tuple, Callable
from loguru import logger
from pathlib import Path
from difflib import SequenceMatcher

from region_capture import get_virtual_screen_bounds, select_region
from text_grab import ocr_image


# Persistent overlay script - runs in separate process
# Shows a thin border around the selected region
OVERLAY_SCRIPT = '''
import tkinter as tk
import sys
import ctypes

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
        self.x1 = x1
        self.y1 = y1
        self.x2 = x2
        self.y2 = y2
        self.border_width = 3

    def run(self):
        self.root = tk.Tk()
        self.root.title("Herald OCR Region")

        # Calculate dimensions
        width = self.x2 - self.x1
        height = self.y2 - self.y1

        # Create a transparent window with just a border
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

        # Draw border rectangle
        self.canvas.create_rectangle(
            self.border_width // 2,
            self.border_width // 2,
            width - self.border_width // 2,
            height - self.border_width // 2,
            outline='#00FF00',  # Bright green
            width=self.border_width
        )

        # Add small label in corner
        self.canvas.create_text(
            self.border_width + 5,
            self.border_width + 5,
            text="OCR",
            fill='#00FF00',
            font=('Arial', 9, 'bold'),
            anchor='nw'
        )

        # Close on Escape
        self.root.bind('<Escape>', lambda e: self.root.quit())

        # Check for quit signal via stdin
        self.root.after(100, self._check_stdin)

        self.root.mainloop()

    def _check_stdin(self):
        """Check if parent process sent quit signal."""
        try:
            import select
            # On Windows, use msvcrt for non-blocking stdin check
            import msvcrt
            if msvcrt.kbhit():
                char = msvcrt.getch()
                if char == b'q':
                    self.root.quit()
                    return
        except:
            pass
        self.root.after(100, self._check_stdin)

if __name__ == "__main__":
    if len(sys.argv) >= 5:
        x1 = int(sys.argv[1])
        y1 = int(sys.argv[2])
        x2 = int(sys.argv[3])
        y2 = int(sys.argv[4])
        BorderOverlay(x1, y1, x2, y2).run()
'''


class PersistentRegion:
    """Manages a persistent OCR region with visible overlay."""

    def __init__(
        self,
        on_text_detected: Optional[Callable[[str], None]] = None,
        poll_interval: float = 2.5,
        change_threshold: float = 0.5
    ):
        """
        Initialize persistent region manager.

        Args:
            on_text_detected: Callback when new text is detected (for auto-read)
            poll_interval: Seconds between OCR polls in auto-read mode
            change_threshold: Minimum change ratio to trigger read (0.5 = 50%)
        """
        self.region: Optional[Tuple[int, int, int, int]] = None
        self.overlay_process: Optional[subprocess.Popen] = None
        self.on_text_detected = on_text_detected
        self.poll_interval = poll_interval
        self.change_threshold = change_threshold

        self._auto_read_enabled = False
        self._auto_read_thread: Optional[threading.Thread] = None
        self._last_text = ""
        self._stop_event = threading.Event()

    @property
    def is_active(self) -> bool:
        """Check if a region is currently active."""
        return self.region is not None and self.overlay_process is not None

    @property
    def auto_read_enabled(self) -> bool:
        """Check if auto-read is enabled."""
        return self._auto_read_enabled

    def toggle(self) -> bool:
        """
        Toggle persistent region on/off.

        Returns:
            True if region is now active, False if deactivated.
        """
        if self.is_active:
            self.deactivate()
            return False
        else:
            return self.activate()

    def activate(self) -> bool:
        """
        Show region selector and activate persistent overlay.

        Returns:
            True if region was selected and activated.
        """
        # First, select the region
        logger.info("Select region for continuous OCR...")
        region = select_region()

        if region is None:
            logger.info("Region selection cancelled")
            return False

        self.region = region
        logger.info(f"Persistent region set: {region}")

        # Start the overlay process
        self._start_overlay()

        return True

    def deactivate(self):
        """Stop the overlay and clear the region."""
        self._stop_auto_read()
        self._stop_overlay()
        self.region = None
        self._last_text = ""
        logger.info("Persistent region deactivated")

    def _start_overlay(self):
        """Start the border overlay process."""
        if not self.region:
            return

        try:
            # Write overlay script to temp file
            script_path = Path(tempfile.gettempdir()) / "herald_overlay.py"
            script_path.write_text(OVERLAY_SCRIPT)

            # Start overlay process
            x1, y1, x2, y2 = self.region
            self.overlay_process = subprocess.Popen(
                [sys.executable, str(script_path), str(x1), str(y1), str(x2), str(y2)],
                stdin=subprocess.PIPE,
                creationflags=subprocess.CREATE_NO_WINDOW if sys.platform == 'win32' else 0
            )
            logger.debug("Overlay process started")

        except Exception as e:
            logger.error(f"Failed to start overlay: {e}")
            self.overlay_process = None

    def _stop_overlay(self):
        """Stop the border overlay process."""
        if self.overlay_process:
            try:
                # Try to send quit signal
                self.overlay_process.stdin.write(b'q')
                self.overlay_process.stdin.flush()
            except:
                pass

            try:
                self.overlay_process.terminate()
                self.overlay_process.wait(timeout=1)
            except:
                try:
                    self.overlay_process.kill()
                except:
                    pass

            self.overlay_process = None
            logger.debug("Overlay process stopped")

    def capture(self):
        """
        Capture the current region as an image.

        Returns:
            PIL Image of the region, or None if no region active.
        """
        if not self.region:
            return None

        try:
            image = ImageGrab.grab(bbox=self.region, all_screens=True)
            return image
        except Exception as e:
            logger.error(f"Failed to capture region: {e}")
            return None

    def read_now(self) -> Optional[str]:
        """
        Capture and OCR the current region immediately.

        Returns:
            OCR'd text, or None if failed.
        """
        if not self.region:
            logger.warning("No persistent region active")
            return None

        image = self.capture()
        if not image:
            return None

        text = ocr_image(image)
        if text:
            self._last_text = text
        return text

    def set_auto_read(self, enabled: bool):
        """Enable or disable auto-read mode."""
        if enabled and not self._auto_read_enabled:
            self._start_auto_read()
        elif not enabled and self._auto_read_enabled:
            self._stop_auto_read()

    def _start_auto_read(self):
        """Start the auto-read polling thread."""
        if self._auto_read_thread and self._auto_read_thread.is_alive():
            return

        self._stop_event.clear()
        self._auto_read_enabled = True
        self._auto_read_thread = threading.Thread(target=self._auto_read_loop, daemon=True)
        self._auto_read_thread.start()
        logger.info(f"Auto-read started (poll: {self.poll_interval}s, threshold: {self.change_threshold*100}%)")

    def _stop_auto_read(self):
        """Stop the auto-read polling thread."""
        self._auto_read_enabled = False
        self._stop_event.set()
        if self._auto_read_thread:
            self._auto_read_thread.join(timeout=1)
            self._auto_read_thread = None
        logger.info("Auto-read stopped")

    def _auto_read_loop(self):
        """Background loop that polls for text changes."""
        while not self._stop_event.is_set() and self.is_active:
            try:
                # Capture and OCR
                image = self.capture()
                if image:
                    text = ocr_image(image)

                    if text and self._has_significant_change(text):
                        logger.info(f"Text changed ({len(text)} chars), triggering read")
                        self._last_text = text

                        if self.on_text_detected:
                            self.on_text_detected(text)

            except Exception as e:
                logger.error(f"Auto-read error: {e}")

            # Wait for next poll
            self._stop_event.wait(self.poll_interval)

    def _has_significant_change(self, new_text: str) -> bool:
        """
        Check if text has changed significantly from last read.

        Args:
            new_text: The newly OCR'd text.

        Returns:
            True if change exceeds threshold.
        """
        if not self._last_text:
            return True

        # Calculate similarity ratio
        similarity = SequenceMatcher(None, self._last_text, new_text).ratio()
        change = 1 - similarity

        logger.debug(f"Text similarity: {similarity:.2f}, change: {change:.2f}")

        return change >= self.change_threshold


# Module-level instance for global access
_persistent_region: Optional[PersistentRegion] = None


def get_persistent_region() -> PersistentRegion:
    """Get or create the global persistent region instance."""
    global _persistent_region
    if _persistent_region is None:
        _persistent_region = PersistentRegion()
    return _persistent_region


def set_persistent_region(instance: PersistentRegion):
    """Set the global persistent region instance."""
    global _persistent_region
    _persistent_region = instance
