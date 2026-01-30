"""
Herald - Text-to-Speech Utility

Press a hotkey to hear clipboard/selected text read aloud.
The inverse of whisper (voice-to-text).

Usage:
    python main.py

Hotkeys:
    Alt+S   - Speak clipboard text
    Alt+P   - Pause/resume
    Alt+]   - Speed up
    Alt+[   - Slow down
    Escape  - Stop speaking
    Alt+Q   - Quit application

Requires admin privileges on Windows for global hotkeys.
"""

import sys
import time
import ctypes
import keyboard
from loguru import logger
from typing import Optional

from config import (
    DEFAULT_HOTKEY, STOP_HOTKEY, PAUSE_HOTKEY, SPEED_UP_HOTKEY,
    SPEED_DOWN_HOTKEY, QUIT_HOTKEY, RATE_STEP, MIN_RATE, MAX_RATE,
    load_settings
)
from tts_engine import get_engine, switch_engine, EdgeTTSEngine, Pyttsx3Engine
from text_grab import get_text_to_speak
from tray_app import TrayApp
from utils import setup_logging

# Global state
_quit_requested = False
_tray_app: Optional[TrayApp] = None
_console_visible = True


def on_speak_hotkey():
    """Called when the speak hotkey is pressed."""
    text = get_text_to_speak()

    if text:
        preview = f"{text[:50]}..." if len(text) > 50 else text
        logger.info(f"Speaking: {preview}")
        engine = get_engine()
        engine.speak(text)
        if _tray_app:
            _tray_app.set_speaking(True)
    else:
        logger.warning("No text to speak (clipboard empty)")


def on_stop_hotkey():
    """Called when the stop hotkey is pressed."""
    engine = get_engine()
    if engine.is_speaking:
        logger.info("Stopped")
        engine.stop()
        if _tray_app:
            _tray_app.set_speaking(False)
            _tray_app.set_paused(False)


def on_pause_resume():
    """Toggle pause/resume."""
    engine = get_engine()
    if engine.is_paused:
        logger.info("Resumed")
        engine.resume()
        if _tray_app:
            _tray_app.set_paused(False)
    elif engine.is_speaking:
        logger.info("Paused")
        engine.pause()
        if _tray_app:
            _tray_app.set_paused(True)


def on_speed_up():
    """Increase speech rate."""
    engine = get_engine()
    old_rate = engine.rate
    engine.rate = old_rate + RATE_STEP

    if engine.rate >= MAX_RATE:
        logger.info(f"Speed: {engine.rate} wpm (maximum)")
        engine.speak("Maximum speed")
    else:
        logger.info(f"Speed: {engine.rate} wpm (faster)")
        engine.speak("Faster")

    if _tray_app:
        _tray_app.set_speed(engine.rate)


def on_speed_down():
    """Decrease speech rate."""
    engine = get_engine()
    old_rate = engine.rate
    engine.rate = old_rate - RATE_STEP

    if engine.rate <= MIN_RATE:
        logger.info(f"Speed: {engine.rate} wpm (minimum)")
        engine.speak("Minimum speed")
    else:
        logger.info(f"Speed: {engine.rate} wpm (slower)")
        engine.speak("Slower")

    if _tray_app:
        _tray_app.set_speed(engine.rate)


def on_quit():
    """Signal main loop to exit."""
    global _quit_requested
    _quit_requested = True
    logger.info("Quit requested")
    engine = get_engine()
    engine.stop()


# Tray app callbacks
def on_voice_change(voice: str):
    """Handle voice change from tray menu."""
    logger.info(f"Changing voice to: {voice}")

    # Determine which engine to use
    if voice in EdgeTTSEngine.VOICES:
        engine = get_engine()
        if not isinstance(engine, EdgeTTSEngine):
            switch_engine("edge")
            engine = get_engine()
        engine.voice_name = voice
    elif voice in Pyttsx3Engine.VOICES:
        engine = get_engine()
        if not isinstance(engine, Pyttsx3Engine):
            switch_engine("pyttsx3")
            engine = get_engine()
        engine.voice_name = voice

    # Announce the change
    engine = get_engine()
    engine.speak(f"Voice changed to {voice}")


def on_speed_change(speed: int):
    """Handle speed change from tray menu."""
    logger.info(f"Changing speed to: {speed}")
    engine = get_engine()
    engine.rate = speed
    engine.speak(f"Speed set to {speed} words per minute")


def on_console_toggle(visible: bool):
    """Handle console visibility toggle from tray menu."""
    global _console_visible
    _console_visible = visible

    # Get console window handle
    kernel32 = ctypes.windll.kernel32
    user32 = ctypes.windll.user32
    hwnd = kernel32.GetConsoleWindow()

    if hwnd:
        if visible:
            user32.ShowWindow(hwnd, 5)  # SW_SHOW
            logger.info("Console shown")
        else:
            user32.ShowWindow(hwnd, 0)  # SW_HIDE
            logger.info("Console hidden")


def update_tray_state():
    """Update tray icon state based on engine state."""
    if not _tray_app:
        return

    engine = get_engine()
    _tray_app.set_speaking(engine.is_speaking)
    _tray_app.set_paused(engine.is_paused)


def main():
    """Main entry point."""
    global _tray_app

    setup_logging()

    logger.info("Herald started")
    logger.info(f"  Speak:     {DEFAULT_HOTKEY}")
    logger.info(f"  Pause:     {PAUSE_HOTKEY}")
    logger.info(f"  Speed up:  {SPEED_UP_HOTKEY}")
    logger.info(f"  Slow down: {SPEED_DOWN_HOTKEY}")
    logger.info(f"  Stop:      {STOP_HOTKEY}")
    logger.info(f"  Quit:      {QUIT_HOTKEY}")
    print()

    # Initialize engine
    engine = get_engine()
    logger.info(f"Voice: {engine.voice_name}, Speed: {engine.rate} wpm")

    # Start tray app
    settings = load_settings()
    _tray_app = TrayApp(
        on_voice_change=on_voice_change,
        on_speed_change=on_speed_change,
        on_pause_toggle=on_pause_resume,
        on_console_toggle=on_console_toggle,
        on_quit=on_quit,
        current_voice=engine.voice_name,
        current_speed=engine.rate,
        console_visible=True,
    )
    _tray_app.start_async()

    # Register hotkeys
    keyboard.add_hotkey(DEFAULT_HOTKEY, on_speak_hotkey, suppress=True)
    keyboard.add_hotkey(PAUSE_HOTKEY, on_pause_resume, suppress=True)
    keyboard.add_hotkey(STOP_HOTKEY, on_stop_hotkey, suppress=False)
    keyboard.add_hotkey(SPEED_UP_HOTKEY, on_speed_up, suppress=True)
    keyboard.add_hotkey(SPEED_DOWN_HOTKEY, on_speed_down, suppress=True)
    keyboard.add_hotkey(QUIT_HOTKEY, on_quit, suppress=True)

    try:
        # Main loop - poll for quit and update tray state
        while not _quit_requested:
            update_tray_state()
            time.sleep(0.1)
    except KeyboardInterrupt:
        logger.info("Ctrl+C - exiting")

    # Clean shutdown
    engine.stop()
    if _tray_app:
        _tray_app.stop()
    keyboard.unhook_all()
    logger.info("Herald stopped")
    sys.exit(0)


if __name__ == "__main__":
    main()
