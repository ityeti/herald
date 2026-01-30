"""
Herald System Tray Application

Provides system tray icon with menu for controlling TTS settings.
"""

import pystray
from PIL import Image, ImageDraw
from typing import Callable, Optional
from loguru import logger
import threading


class TrayApp:
    """System tray application for Herald TTS."""

    # Voice options grouped by engine
    EDGE_VOICES = [
        ("aria", "Aria (Female)"),
        ("jenny", "Jenny (Female)"),
        ("guy", "Guy (Male)"),
        ("christopher", "Christopher (Male)"),
    ]

    OFFLINE_VOICES = [
        ("zira", "Zira (Female, Offline)"),
        ("david", "David (Male, Offline)"),
    ]

    # Speed presets
    SPEED_PRESETS = [
        (200, "Slow (200 wpm)"),
        (350, "Normal (350 wpm)"),
        (500, "Fast (500 wpm)"),
        (600, "Maximum (600 wpm)"),
    ]

    def __init__(
        self,
        on_voice_change: Optional[Callable[[str], None]] = None,
        on_speed_change: Optional[Callable[[int], None]] = None,
        on_pause_toggle: Optional[Callable[[], None]] = None,
        on_console_toggle: Optional[Callable[[bool], None]] = None,
        on_quit: Optional[Callable[[], None]] = None,
        current_voice: str = "aria",
        current_speed: int = 500,
        console_visible: bool = True,
    ):
        self.on_voice_change = on_voice_change
        self.on_speed_change = on_speed_change
        self.on_pause_toggle = on_pause_toggle
        self.on_console_toggle = on_console_toggle
        self.on_quit_callback = on_quit

        self.current_voice = current_voice
        self.current_speed = current_speed
        self.console_visible = console_visible
        self.is_paused = False
        self.is_speaking = False

        self.icon: Optional[pystray.Icon] = None

        # Create icons for different states
        self.icons = {
            "idle": self._create_icon("gray"),
            "speaking": self._create_icon("green"),
            "paused": self._create_icon("yellow"),
        }

        logger.debug("Tray app initialized")

    def _create_icon(self, color: str) -> Image.Image:
        """Create a simple speaker icon with given color."""
        size = 64
        image = Image.new('RGBA', (size, size), (0, 0, 0, 0))
        draw = ImageDraw.Draw(image)

        color_map = {
            "gray": (128, 128, 128),
            "green": (50, 180, 50),
            "yellow": (255, 200, 0),
        }

        fill = color_map.get(color, (128, 128, 128))

        # Draw speaker shape (simplified)
        # Speaker body
        draw.rectangle([16, 20, 28, 44], fill=fill, outline=(0, 0, 0))
        # Speaker cone
        draw.polygon([(28, 16), (44, 8), (44, 56), (28, 48)], fill=fill, outline=(0, 0, 0))

        # Sound waves for speaking state
        if color == "green":
            draw.arc([46, 20, 56, 44], -60, 60, fill=(50, 180, 50), width=2)
            draw.arc([50, 12, 64, 52], -60, 60, fill=(50, 180, 50), width=2)
        elif color == "yellow":
            # Pause bars
            draw.rectangle([48, 24, 52, 40], fill=(255, 200, 0))
            draw.rectangle([56, 24, 60, 40], fill=(255, 200, 0))

        return image

    def _create_menu(self) -> pystray.Menu:
        """Create the tray menu."""

        # Voice submenu - Edge voices
        edge_voice_items = []
        for voice_id, label in self.EDGE_VOICES:
            edge_voice_items.append(
                pystray.MenuItem(
                    label,
                    self._make_voice_callback(voice_id),
                    checked=lambda item, v=voice_id: self.current_voice == v
                )
            )

        # Voice submenu - Offline voices
        offline_voice_items = []
        for voice_id, label in self.OFFLINE_VOICES:
            offline_voice_items.append(
                pystray.MenuItem(
                    label,
                    self._make_voice_callback(voice_id),
                    checked=lambda item, v=voice_id: self.current_voice == v
                )
            )

        # Speed submenu
        speed_items = []
        for speed, label in self.SPEED_PRESETS:
            speed_items.append(
                pystray.MenuItem(
                    label,
                    self._make_speed_callback(speed),
                    checked=lambda item, s=speed: self.current_speed == s
                )
            )

        # Console submenu
        console_items = [
            pystray.MenuItem(
                "Visible",
                self._make_console_callback(True),
                checked=lambda item: self.console_visible
            ),
            pystray.MenuItem(
                "Hidden",
                self._make_console_callback(False),
                checked=lambda item: not self.console_visible
            ),
        ]

        return pystray.Menu(
            pystray.MenuItem(
                "Voice (Online)",
                pystray.Menu(*edge_voice_items)
            ),
            pystray.MenuItem(
                "Voice (Offline)",
                pystray.Menu(*offline_voice_items)
            ),
            pystray.MenuItem(
                "Speed",
                pystray.Menu(*speed_items)
            ),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem(
                "Pause" if not self.is_paused else "Resume",
                self._on_pause_toggle,
                enabled=self.is_speaking or self.is_paused
            ),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem(
                "Console",
                pystray.Menu(*console_items)
            ),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("Quit", self._on_quit)
        )

    def _make_voice_callback(self, voice_id: str):
        """Create callback for voice selection."""
        def callback():
            if voice_id != self.current_voice:
                logger.info(f"Voice change: {voice_id}")
                self.current_voice = voice_id
                if self.on_voice_change:
                    self.on_voice_change(voice_id)
                self._refresh_menu()
        return callback

    def _make_speed_callback(self, speed: int):
        """Create callback for speed selection."""
        def callback():
            if speed != self.current_speed:
                logger.info(f"Speed change: {speed}")
                self.current_speed = speed
                if self.on_speed_change:
                    self.on_speed_change(speed)
                self._refresh_menu()
        return callback

    def _make_console_callback(self, visible: bool):
        """Create callback for console visibility."""
        def callback():
            if visible != self.console_visible:
                logger.info(f"Console visibility: {visible}")
                self.console_visible = visible
                if self.on_console_toggle:
                    self.on_console_toggle(visible)
                self._refresh_menu()
        return callback

    def _on_pause_toggle(self):
        """Handle pause/resume toggle."""
        if self.on_pause_toggle:
            self.on_pause_toggle()

    def _on_quit(self):
        """Handle quit."""
        logger.info("Quit from tray")
        if self.icon:
            self.icon.stop()
        if self.on_quit_callback:
            self.on_quit_callback()

    def _refresh_menu(self):
        """Refresh the menu to update checkmarks."""
        if self.icon:
            self.icon.menu = self._create_menu()

    def _update_icon(self):
        """Update icon based on current state."""
        if self.icon:
            if self.is_paused:
                self.icon.icon = self.icons["paused"]
            elif self.is_speaking:
                self.icon.icon = self.icons["speaking"]
            else:
                self.icon.icon = self.icons["idle"]

    def set_speaking(self, speaking: bool):
        """Update speaking state."""
        self.is_speaking = speaking
        self._update_icon()
        self._refresh_menu()

    def set_paused(self, paused: bool):
        """Update paused state."""
        self.is_paused = paused
        self._update_icon()
        self._refresh_menu()

    def set_voice(self, voice: str):
        """Update current voice (for menu checkmark)."""
        self.current_voice = voice
        self._refresh_menu()

    def set_speed(self, speed: int):
        """Update current speed (for menu checkmark)."""
        self.current_speed = speed
        self._refresh_menu()

    def start_async(self):
        """Start tray application in background thread."""
        logger.info("Starting tray app")

        def run():
            try:
                self.icon = pystray.Icon(
                    "Herald",
                    self.icons["idle"],
                    "Herald TTS",
                    self._create_menu()
                )
                self.icon.run()
            except Exception as e:
                logger.error(f"Tray app error: {e}")

        thread = threading.Thread(target=run, daemon=True)
        thread.start()

        # Wait for icon to be ready
        import time
        max_wait = 5
        waited = 0
        while self.icon is None and waited < max_wait:
            time.sleep(0.1)
            waited += 0.1

        if self.icon:
            logger.info("Tray app started")
        else:
            logger.warning("Tray app may not have started")

    def stop(self):
        """Stop tray application."""
        if self.icon:
            self.icon.stop()
            self.icon = None


# Self-test
if __name__ == "__main__":
    import time
    import sys

    print("Testing Herald Tray App...")
    print("Look for speaker icon in system tray")
    print("Right-click for menu")
    print()

    def on_voice(v):
        print(f"Voice changed to: {v}")

    def on_speed(s):
        print(f"Speed changed to: {s}")

    def on_quit():
        print("Quit requested")
        sys.exit(0)

    app = TrayApp(
        on_voice_change=on_voice,
        on_speed_change=on_speed,
        on_quit=on_quit
    )

    app.start_async()

    # Test state changes
    try:
        print("Idle state...")
        time.sleep(3)

        print("Speaking state...")
        app.set_speaking(True)
        time.sleep(3)

        print("Paused state...")
        app.set_paused(True)
        time.sleep(3)

        print("Back to idle...")
        app.set_speaking(False)
        app.set_paused(False)

        print("\nTest complete. Use tray menu to quit.")
        while True:
            time.sleep(1)

    except KeyboardInterrupt:
        print("\nExiting...")
        app.stop()
