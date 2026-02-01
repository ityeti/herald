"""
Herald Region Capture

Provides screen region selection for OCR.
Uses tkinter for overlay and PIL for screenshot.
"""

import tkinter as tk
from PIL import ImageGrab
from typing import Optional, Tuple
from loguru import logger


class RegionSelector:
    """Overlay window for selecting a screen region."""

    def __init__(self):
        self.start_x = 0
        self.start_y = 0
        self.end_x = 0
        self.end_y = 0
        self.rect_id = None
        self.selection_made = False

    def select_region(self) -> Optional[Tuple[int, int, int, int]]:
        """
        Show fullscreen overlay and let user draw a selection box.

        Returns:
            Tuple of (x1, y1, x2, y2) for the selected region, or None if cancelled.
        """
        self.selection_made = False

        # Create fullscreen transparent overlay
        self.root = tk.Tk()
        self.root.attributes('-fullscreen', True)
        self.root.attributes('-alpha', 0.3)
        self.root.attributes('-topmost', True)
        self.root.configure(bg='gray')
        self.root.config(cursor="crosshair")

        # Create canvas for drawing selection
        self.canvas = tk.Canvas(
            self.root,
            bg='gray',
            highlightthickness=0
        )
        self.canvas.pack(fill=tk.BOTH, expand=True)

        # Instructions label
        self.label = tk.Label(
            self.root,
            text="Click and drag to select region. Press Escape to cancel.",
            font=("Arial", 14),
            bg='black',
            fg='white',
            padx=10,
            pady=5
        )
        self.label.place(relx=0.5, y=30, anchor='center')

        # Bind events
        self.canvas.bind('<Button-1>', self._on_press)
        self.canvas.bind('<B1-Motion>', self._on_drag)
        self.canvas.bind('<ButtonRelease-1>', self._on_release)
        self.root.bind('<Escape>', self._on_escape)

        # Run the overlay
        self.root.mainloop()

        if self.selection_made:
            # Normalize coordinates (ensure x1 < x2, y1 < y2)
            x1 = min(self.start_x, self.end_x)
            y1 = min(self.start_y, self.end_y)
            x2 = max(self.start_x, self.end_x)
            y2 = max(self.start_y, self.end_y)

            # Minimum size check
            if (x2 - x1) < 10 or (y2 - y1) < 10:
                logger.warning("Selection too small")
                return None

            return (x1, y1, x2, y2)

        return None

    def _on_press(self, event):
        """Handle mouse press - start selection."""
        self.start_x = event.x
        self.start_y = event.y
        self.end_x = event.x
        self.end_y = event.y

        # Create initial rectangle
        if self.rect_id:
            self.canvas.delete(self.rect_id)
        self.rect_id = self.canvas.create_rectangle(
            self.start_x, self.start_y,
            self.end_x, self.end_y,
            outline='red',
            width=2,
            fill='white',
            stipple='gray50'
        )

    def _on_drag(self, event):
        """Handle mouse drag - update selection rectangle."""
        self.end_x = event.x
        self.end_y = event.y

        # Update rectangle
        if self.rect_id:
            self.canvas.coords(
                self.rect_id,
                self.start_x, self.start_y,
                self.end_x, self.end_y
            )

    def _on_release(self, event):
        """Handle mouse release - finalize selection."""
        self.end_x = event.x
        self.end_y = event.y
        self.selection_made = True
        self.root.quit()
        self.root.destroy()

    def _on_escape(self, event):
        """Handle Escape key - cancel selection."""
        self.selection_made = False
        self.root.quit()
        self.root.destroy()


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
    selector = RegionSelector()
    region = selector.select_region()

    if region is None:
        logger.info("Region selection cancelled")
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
        # Save for inspection
        image.save("test_capture.png")
        print("Saved to test_capture.png")
    else:
        print("No region selected")
