"""
Herald - Text-to-Speech Utility

Press a hotkey to hear clipboard/selected text read aloud.
The inverse of whisper (voice-to-text).

Usage:
    python main.py

Hotkeys:
    Alt+S   - Speak clipboard text (splits into lines)
    Alt+P   - Pause/resume
    Alt+N   - Skip to next line
    Alt+B   - Go back to previous line
    Alt+]   - Speed up
    Alt+[   - Slow down
    Escape  - Stop speaking (clears line queue)
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
    STOP_HOTKEY, SPEED_UP_HOTKEY, SPEED_DOWN_HOTKEY, QUIT_HOTKEY,
    NEXT_LINE_HOTKEY, PREV_LINE_HOTKEY,
    RATE_STEP, MIN_RATE, MAX_RATE, load_settings, set_setting,
    DEFAULT_SPEAK_HOTKEY, DEFAULT_PAUSE_HOTKEY, DEFAULT_LINE_DELAY
)
from tts_engine import get_engine, switch_engine, EdgeTTSEngine, Pyttsx3Engine
from text_grab import get_text_to_speak
from tray_app import TrayApp
from utils import setup_logging

# Global state
_quit_requested = False
_tray_app: Optional[TrayApp] = None
_console_visible = True
_current_speak_hotkey = DEFAULT_SPEAK_HOTKEY
_current_pause_hotkey = DEFAULT_PAUSE_HOTKEY

# Line queue for skip functionality
_line_queue: list[str] = []
_current_line_index = 0
_was_speaking = False  # Track state for auto-advance
_line_delay = DEFAULT_LINE_DELAY  # Delay in ms between lines


def on_speak_hotkey():
    """Called when the speak hotkey is pressed."""
    global _line_queue, _current_line_index

    text = get_text_to_speak()

    if text:
        # Split text into lines, filtering out empty lines
        lines = [line.strip() for line in text.split('\n') if line.strip()]

        if not lines:
            logger.warning("No text to speak (only whitespace)")
            return

        # Store the queue and start from the beginning
        _line_queue = lines
        _current_line_index = 0

        # Speak the first line
        _speak_current_line()
    else:
        logger.warning("No text to speak (clipboard empty)")


def _speak_current_line():
    """Speak the current line from the queue."""
    global _current_line_index

    if not _line_queue or _current_line_index >= len(_line_queue):
        logger.info("Finished all lines")
        _clear_queue()
        return

    line = _line_queue[_current_line_index]
    total_lines = len(_line_queue)
    line_num = _current_line_index + 1

    # Show line number and preview
    preview = f"{line[:40]}..." if len(line) > 40 else line
    engine = get_engine()

    if isinstance(engine, EdgeTTSEngine):
        logger.info(f"[{line_num}/{total_lines}] Generating: {preview}")
        if _tray_app:
            _tray_app.set_generating(True)

        # Prefetch next line while this one plays
        if _current_line_index + 1 < len(_line_queue):
            next_line = _line_queue[_current_line_index + 1]
            engine.prefetch(next_line)
    else:
        logger.info(f"[{line_num}/{total_lines}] Speaking: {preview}")
        if _tray_app:
            _tray_app.set_speaking(True)

    engine.speak(line)


def _clear_queue():
    """Clear the line queue and prefetch cache."""
    global _line_queue, _current_line_index
    _line_queue = []
    _current_line_index = 0

    # Clear prefetch cache if using edge-tts
    engine = get_engine()
    if isinstance(engine, EdgeTTSEngine) and hasattr(engine, 'clear_prefetch_cache'):
        engine.clear_prefetch_cache()


def on_next_line():
    """Skip to the next line in the queue."""
    global _current_line_index

    if not _line_queue:
        return

    engine = get_engine()
    engine.stop()

    if _current_line_index < len(_line_queue) - 1:
        _current_line_index += 1
        logger.info(f"Skipping to line {_current_line_index + 1}/{len(_line_queue)}")
        _speak_current_line()
    else:
        logger.info("Already at last line")
        _clear_queue()
        if _tray_app:
            _tray_app.set_speaking(False)


def on_prev_line():
    """Go back to the previous line in the queue."""
    global _current_line_index

    if not _line_queue:
        return

    engine = get_engine()
    engine.stop()

    if _current_line_index > 0:
        _current_line_index -= 1
        logger.info(f"Going back to line {_current_line_index + 1}/{len(_line_queue)}")
        _speak_current_line()
    else:
        logger.info("Already at first line - restarting")
        _speak_current_line()


def on_stop_hotkey():
    """Called when the stop hotkey is pressed."""
    engine = get_engine()
    if engine.is_speaking or _line_queue:
        logger.info("Stopped")
        engine.stop()
        _clear_queue()
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

    # Stop any current speech first
    current_engine = get_engine()
    current_engine.stop()

    # Save voice to settings FIRST (before engine switch loads settings)
    set_setting("voice", voice)

    # Determine which engine to use and switch if needed
    if voice in EdgeTTSEngine.VOICES:
        if not isinstance(current_engine, EdgeTTSEngine):
            logger.info("Switching to Edge TTS engine...")
            switch_engine("edge")
        engine = get_engine()
        engine.voice_name = voice
    elif voice in Pyttsx3Engine.VOICES:
        if not isinstance(current_engine, Pyttsx3Engine):
            logger.info("Switching to offline engine...")
            switch_engine("pyttsx3")
        engine = get_engine()
        engine.voice_name = voice
    else:
        logger.warning(f"Unknown voice: {voice}")
        return

    # Announce the change
    engine = get_engine()
    engine.speak(f"Voice changed to {voice}")


def on_speed_change(speed: int):
    """Handle speed change from tray menu."""
    logger.info(f"Changing speed to: {speed}")
    engine = get_engine()
    engine.rate = speed
    engine.speak(f"Speed set to {speed} words per minute")


def on_line_delay_change(delay: int):
    """Handle line delay change from tray menu."""
    global _line_delay
    logger.info(f"Changing line delay to: {delay}ms")
    _line_delay = delay
    set_setting("line_delay", delay)
    engine = get_engine()
    if delay == 0:
        engine.speak("Line delay disabled")
    else:
        engine.speak(f"Line delay set to {delay} milliseconds")


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


def on_speak_hotkey_change(new_hotkey: str):
    """Handle speak hotkey change from tray menu."""
    global _current_speak_hotkey

    old_hotkey = _current_speak_hotkey
    logger.info(f"Changing speak hotkey: {old_hotkey} -> {new_hotkey}")

    # Unregister old hotkey
    try:
        keyboard.remove_hotkey(old_hotkey)
    except (KeyError, ValueError):
        pass  # Hotkey wasn't registered

    # Register new hotkey
    keyboard.add_hotkey(new_hotkey, on_speak_hotkey, suppress=True)
    _current_speak_hotkey = new_hotkey

    # Save to settings
    set_setting("hotkey_speak", new_hotkey)

    # Announce the change
    engine = get_engine()
    engine.speak(f"Speak hotkey changed to {new_hotkey.replace('+', ' ')}")


def on_pause_hotkey_change(new_hotkey: str):
    """Handle pause hotkey change from tray menu."""
    global _current_pause_hotkey

    old_hotkey = _current_pause_hotkey
    logger.info(f"Changing pause hotkey: {old_hotkey} -> {new_hotkey}")

    # Unregister old hotkey
    try:
        keyboard.remove_hotkey(old_hotkey)
    except (KeyError, ValueError):
        pass  # Hotkey wasn't registered

    # Register new hotkey
    keyboard.add_hotkey(new_hotkey, on_pause_resume, suppress=True)
    _current_pause_hotkey = new_hotkey

    # Save to settings
    set_setting("hotkey_pause", new_hotkey)

    # Announce the change
    engine = get_engine()
    engine.speak(f"Pause hotkey changed to {new_hotkey.replace('+', ' ')}")


def update_tray_state():
    """Update tray icon state based on engine state and handle auto-advance."""
    global _was_speaking, _current_line_index

    engine = get_engine()

    # Check if we need to auto-advance to next line
    is_active = engine.is_speaking or engine.is_paused or (hasattr(engine, 'is_generating') and engine.is_generating)

    if _was_speaking and not is_active and _line_queue:
        # Just finished a line, advance to next
        _current_line_index += 1
        if _current_line_index < len(_line_queue):
            # Apply delay if configured
            if _line_delay > 0:
                time.sleep(_line_delay / 1000.0)
            _speak_current_line()
        else:
            logger.info("Finished all lines")
            _clear_queue()

    _was_speaking = is_active

    # Update tray icon
    if not _tray_app:
        return

    # Check generating first (edge-tts only)
    if hasattr(engine, 'is_generating') and engine.is_generating:
        _tray_app.set_generating(True)
    elif engine.is_speaking:
        _tray_app.set_speaking(True)
    elif engine.is_paused:
        _tray_app.set_paused(True)
    else:
        _tray_app.set_speaking(False)
        _tray_app.set_paused(False)
        _tray_app.set_generating(False)


def main():
    """Main entry point."""
    global _tray_app, _current_speak_hotkey, _current_pause_hotkey, _line_delay

    setup_logging()

    # Load settings
    settings = load_settings()
    _current_speak_hotkey = settings.get("hotkey_speak", DEFAULT_SPEAK_HOTKEY)
    _current_pause_hotkey = settings.get("hotkey_pause", DEFAULT_PAUSE_HOTKEY)
    _line_delay = settings.get("line_delay", DEFAULT_LINE_DELAY)

    logger.info("Herald started")
    logger.info(f"  Speak:      {_current_speak_hotkey}")
    logger.info(f"  Pause:      {_current_pause_hotkey}")
    logger.info(f"  Speed up:   {SPEED_UP_HOTKEY}")
    logger.info(f"  Slow down:  {SPEED_DOWN_HOTKEY}")
    logger.info(f"  Next line:  {NEXT_LINE_HOTKEY}")
    logger.info(f"  Prev line:  {PREV_LINE_HOTKEY}")
    logger.info(f"  Stop:       {STOP_HOTKEY}")
    logger.info(f"  Quit:       {QUIT_HOTKEY}")
    print()

    # Initialize engine
    engine = get_engine()
    logger.info(f"Voice: {engine.voice_name}, Speed: {engine.rate} wpm")

    # Start tray app
    _tray_app = TrayApp(
        on_voice_change=on_voice_change,
        on_speed_change=on_speed_change,
        on_line_delay_change=on_line_delay_change,
        on_pause_toggle=on_pause_resume,
        on_console_toggle=on_console_toggle,
        on_speak_hotkey_change=on_speak_hotkey_change,
        on_pause_hotkey_change=on_pause_hotkey_change,
        on_quit=on_quit,
        current_voice=engine.voice_name,
        current_speed=engine.rate,
        current_line_delay=_line_delay,
        current_speak_hotkey=_current_speak_hotkey,
        current_pause_hotkey=_current_pause_hotkey,
        console_visible=True,
    )
    _tray_app.start_async()

    # Register hotkeys
    keyboard.add_hotkey(_current_speak_hotkey, on_speak_hotkey, suppress=True)
    keyboard.add_hotkey(_current_pause_hotkey, on_pause_resume, suppress=True)
    keyboard.add_hotkey(STOP_HOTKEY, on_stop_hotkey, suppress=False)
    keyboard.add_hotkey(SPEED_UP_HOTKEY, on_speed_up, suppress=True)
    keyboard.add_hotkey(SPEED_DOWN_HOTKEY, on_speed_down, suppress=True)
    keyboard.add_hotkey(NEXT_LINE_HOTKEY, on_next_line, suppress=True)
    keyboard.add_hotkey(PREV_LINE_HOTKEY, on_prev_line, suppress=True)
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
