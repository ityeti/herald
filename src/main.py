"""
Herald - Text-to-Speech Utility

Press a hotkey to hear clipboard/selected text read aloud.
The inverse of whisper (voice-to-text).

Usage:
    python main.py

Hotkeys:
    Alt+S   - Speak clipboard text
    Alt+]   - Speed up
    Alt+[   - Slow down
    Escape  - Stop speaking
    Alt+Q   - Quit application

Requires admin privileges on Windows for global hotkeys.
"""

import sys
import time
import keyboard
from loguru import logger

from config import (
    DEFAULT_HOTKEY, STOP_HOTKEY, SPEED_UP_HOTKEY,
    SPEED_DOWN_HOTKEY, QUIT_HOTKEY, RATE_STEP, MIN_RATE, MAX_RATE
)
from tts_engine import get_engine
from text_grab import get_text_to_speak
from utils import setup_logging

# Flag to signal main loop to exit
_quit_requested = False


def on_speak_hotkey():
    """Called when the speak hotkey is pressed."""
    text = get_text_to_speak()

    if text:
        preview = f"{text[:50]}..." if len(text) > 50 else text
        logger.info(f"Speaking: {preview}")
        engine = get_engine()
        engine.speak(text)
    else:
        logger.warning("No text to speak (clipboard empty)")


def on_stop_hotkey():
    """Called when the stop hotkey is pressed."""
    engine = get_engine()
    if engine.is_speaking:
        logger.info("Stopped")
        engine.stop()


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


def on_quit():
    """Signal main loop to exit."""
    global _quit_requested
    _quit_requested = True
    logger.info("Quit requested")
    engine = get_engine()
    engine.stop()


def main():
    """Main entry point."""
    setup_logging()

    logger.info("Herald started")
    logger.info(f"  Speak:     {DEFAULT_HOTKEY}")
    logger.info(f"  Speed up:  {SPEED_UP_HOTKEY}")
    logger.info(f"  Slow down: {SPEED_DOWN_HOTKEY}")
    logger.info(f"  Stop:      {STOP_HOTKEY}")
    logger.info(f"  Quit:      {QUIT_HOTKEY}")
    print()

    # Register hotkeys
    keyboard.add_hotkey(DEFAULT_HOTKEY, on_speak_hotkey, suppress=True)
    keyboard.add_hotkey(STOP_HOTKEY, on_stop_hotkey, suppress=False)
    keyboard.add_hotkey(SPEED_UP_HOTKEY, on_speed_up, suppress=True)
    keyboard.add_hotkey(SPEED_DOWN_HOTKEY, on_speed_down, suppress=True)
    keyboard.add_hotkey(QUIT_HOTKEY, on_quit, suppress=True)

    engine = get_engine()
    logger.info(f"Current speed: {engine.rate} wpm")

    try:
        # Poll for quit flag (keyboard.wait() blocks forever)
        while not _quit_requested:
            time.sleep(0.1)
    except KeyboardInterrupt:
        logger.info("Ctrl+C - exiting")

    # Clean shutdown
    engine.stop()
    keyboard.unhook_all()
    logger.info("Herald stopped")
    sys.exit(0)


if __name__ == "__main__":
    main()
