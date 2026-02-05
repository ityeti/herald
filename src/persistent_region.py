"""
Herald Persistent Region

Provides a persistent screen region with visible border for continuous OCR.
- Alt+M: Toggle region on/off
- When active, shows a visible border around the selected area
- Alt+S reads from the persistent region instead of clipboard
"""

import subprocess
import sys
import tempfile
import threading
from PIL import ImageGrab
from collections.abc import Callable
from loguru import logger
from pathlib import Path
from difflib import SequenceMatcher

from region_capture import select_region, get_helper_path
from text_grab import ocr_image


# Persistent overlay script - runs in separate process
# Shows a rounded border OUTSIDE the capture region (so it doesn't appear in screenshots)
OVERLAY_SCRIPT = '''
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
        label_bg_color = '#2E7D32'
        label_text_color = 'white'

        # Draw rounded rectangle border
        self._draw_rounded_rect(
            self.padding - self.border_width // 2,
            self.padding - self.border_width // 2,
            width - self.padding + self.border_width // 2,
            height - self.padding + self.border_width // 2,
            self.corner_radius,
            border_color
        )

        # Draw "OCR" label at top-left corner
        label_text = "OCR"
        label_font = ('Segoe UI', 9, 'bold')
        label_padding_x = 6
        label_padding_y = 2

        # Create label background rectangle
        label_x = self.padding + 4
        label_y = self.padding - self.border_width - 1
        self.canvas.create_rectangle(
            label_x, label_y,
            label_x + 32, label_y + 16,
            fill=label_bg_color, outline=label_bg_color
        )

        # Create label text
        self.canvas.create_text(
            label_x + label_padding_x, label_y + label_padding_y,
            text=label_text, font=label_font, fill=label_text_color, anchor='nw'
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
'''


class PersistentRegion:
    """Manages a persistent OCR region with visible overlay."""

    def __init__(
        self,
        on_text_detected: Callable[[str], None] | None = None,
        poll_interval: float = 2.5,
        change_threshold: float = 0.5,
    ):
        """
        Initialize persistent region manager.

        Args:
            on_text_detected: Callback when new text is detected (for auto-read)
            poll_interval: Seconds between OCR polls in auto-read mode
            change_threshold: Minimum change ratio to trigger read (0.5 = 50%)
        """
        self.region: tuple[int, int, int, int] | None = None
        self.overlay_process: subprocess.Popen | None = None
        self.on_text_detected = on_text_detected
        self.poll_interval = poll_interval
        self.change_threshold = change_threshold

        self._auto_read_enabled = False
        self._auto_read_thread: threading.Thread | None = None
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
            x1, y1, x2, y2 = self.region

            # Determine how to run the overlay
            helper_exe = get_helper_path("overlay_border")

            if helper_exe:
                # Packaged mode - use bundled helper exe
                logger.debug(f"Using helper exe: {helper_exe}")
                cmd = [str(helper_exe), str(x1), str(y1), str(x2), str(y2)]
            else:
                # Development mode - use Python script
                script_path = Path(tempfile.gettempdir()) / "herald_overlay.py"
                script_path.write_text(OVERLAY_SCRIPT)
                cmd = [sys.executable, str(script_path), str(x1), str(y1), str(x2), str(y2)]

            # Start overlay process
            self.overlay_process = subprocess.Popen(
                cmd, stdin=subprocess.PIPE, creationflags=subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0
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
                self.overlay_process.stdin.write(b"q")
                self.overlay_process.stdin.flush()
            except Exception:  # noqa: S110
                pass

            try:
                self.overlay_process.terminate()
                self.overlay_process.wait(timeout=1)
            except Exception:
                try:
                    self.overlay_process.kill()
                except Exception:  # noqa: S110
                    pass

            self.overlay_process = None
            logger.debug("Overlay process stopped")

    def capture(self):
        """
        Capture the current region as an image.
        Border is outside capture area so no need to hide it.

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

    def read_now(self) -> str | None:
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
        self._last_text = ""  # Clear so first scan always triggers
        self._auto_read_thread = threading.Thread(target=self._auto_read_loop, daemon=True)
        self._auto_read_thread.start()
        logger.info(f"Auto-read started (poll: {self.poll_interval}s, threshold: {self.change_threshold * 100:.0f}%)")

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
        first_run = True
        while not self._stop_event.is_set() and self.is_active:
            try:
                # Capture and OCR
                image = self.capture()
                if image:
                    text = ocr_image(image)

                    # Skip very short text (likely OCR artifacts or noise)
                    if text and len(text.strip()) >= 10:
                        # First run always reads, or when text changes significantly
                        should_read = first_run or self._has_significant_change(text)

                        if should_read:
                            logger.info(
                                f"Auto-read: {'First scan' if first_run else 'Text changed'} ({len(text)} chars)"
                            )
                            self._last_text = text
                            first_run = False

                            if self.on_text_detected:
                                self.on_text_detected(text)
                            else:
                                logger.warning("Auto-read: No callback set!")
                        else:
                            logger.debug(f"Auto-read: No significant change ({len(text)} chars)")

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
_persistent_region: PersistentRegion | None = None


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
