"""
Herald - Text-to-Speech Utility

Press a hotkey to hear clipboard/selected text read aloud.
The inverse of whisper (voice-to-text).

Usage:
    python main.py

Hotkeys:
    Alt+S   - Speak clipboard/selection (auto-copies, supports OCR for images)
    Alt+O   - OCR region capture (draw box on screen to read)
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
import os
import subprocess
import keyboard
from loguru import logger
from typing import Optional

from config import (
    STOP_HOTKEY, SPEED_UP_HOTKEY, SPEED_DOWN_HOTKEY, QUIT_HOTKEY,
    NEXT_LINE_HOTKEY, PREV_LINE_HOTKEY, OCR_REGION_HOTKEY,
    RATE_STEP, MIN_RATE, MAX_RATE, load_settings, set_setting,
    DEFAULT_SPEAK_HOTKEY, DEFAULT_PAUSE_HOTKEY, DEFAULT_LINE_DELAY,
    DEFAULT_READ_MODE, DEFAULT_LOG_PREVIEW, DEFAULT_AUTO_COPY
)
from tts_engine import get_engine, switch_engine, EdgeTTSEngine, Pyttsx3Engine
from text_grab import get_content_to_speak, ocr_image
from region_capture import select_and_capture
from tray_app import TrayApp
from utils import setup_logging


def ensure_single_instance():
    """Ensure only one instance of Herald is running.

    Returns True if this is the only instance, False if another exists.
    """
    MUTEX_NAME = "Global\\HeraldSingleInstance"

    # Try to create a named mutex
    kernel32 = ctypes.windll.kernel32
    mutex = kernel32.CreateMutexW(None, True, MUTEX_NAME)
    last_error = kernel32.GetLastError()

    # ERROR_ALREADY_EXISTS = 183 means another instance has the mutex
    if last_error == 183:
        print("Herald is already running. Check your system tray.")
        print("To start a new instance, close the existing one first.")
        input("\nPress Enter to exit...")
        sys.exit(0)

    return True


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
_read_mode = DEFAULT_READ_MODE  # "lines" or "continuous"
_log_preview = DEFAULT_LOG_PREVIEW  # Show text in console/logs
_auto_copy = DEFAULT_AUTO_COPY  # Auto Ctrl+C before reading


def on_speak_hotkey():
    """Called when the speak hotkey is pressed."""
    global _line_queue, _current_line_index

    # Auto-copy selection and get content (text or OCR'd image)
    text, source = get_content_to_speak(auto_copy=_auto_copy)

    if not text:
        logger.warning("No text to speak (clipboard empty or OCR failed)")
        return

    if source == "ocr":
        logger.info("Reading text from image (OCR)")

    if _read_mode == "continuous":
        # Original behavior: speak all text as one block
        _speak_continuous(text)
    else:
        # Line-by-line mode (default)
        lines = [line.strip() for line in text.split('\n') if line.strip()]

        if not lines:
            logger.warning("No text to speak (only whitespace)")
            return

        _line_queue = lines
        _current_line_index = 0
        _speak_current_line()


def on_ocr_region():
    """Called when the OCR region capture hotkey is pressed."""
    global _line_queue, _current_line_index

    logger.info("OCR region capture - select area with mouse...")

    # Capture screen region
    image = select_and_capture()

    if image is None:
        logger.info("OCR cancelled")
        return

    # Run OCR on the captured region
    text = ocr_image(image)

    if not text:
        logger.warning("OCR found no text in selection")
        engine = get_engine()
        engine.speak("No text found")
        return

    logger.info(f"OCR extracted {len(text)} characters")

    if _read_mode == "continuous":
        _speak_continuous(text)
    else:
        lines = [line.strip() for line in text.split('\n') if line.strip()]

        if not lines:
            logger.warning("OCR result was only whitespace")
            return

        _line_queue = lines
        _current_line_index = 0
        _speak_current_line()


def _speak_continuous(text: str):
    """Speak all text as one continuous block (original behavior)."""
    engine = get_engine()

    if _log_preview:
        preview = f"{text[:50]}..." if len(text) > 50 else text
        if isinstance(engine, EdgeTTSEngine):
            logger.info(f"Generating: {preview}")
        else:
            logger.info(f"Speaking: {preview}")
    else:
        if isinstance(engine, EdgeTTSEngine):
            logger.info("Generating audio...")
        else:
            logger.info("Speaking...")

    if isinstance(engine, EdgeTTSEngine):
        if _tray_app:
            _tray_app.set_generating(True)
    else:
        if _tray_app:
            _tray_app.set_speaking(True)

    engine.speak(text)


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
    engine = get_engine()

    # Log with or without text preview based on privacy setting
    if _log_preview:
        preview = f"{line[:40]}..." if len(line) > 40 else line
        if isinstance(engine, EdgeTTSEngine):
            logger.info(f"[{line_num}/{total_lines}] Generating: {preview}")
        else:
            logger.info(f"[{line_num}/{total_lines}] Speaking: {preview}")
    else:
        if isinstance(engine, EdgeTTSEngine):
            logger.info(f"[{line_num}/{total_lines}] Generating...")
        else:
            logger.info(f"[{line_num}/{total_lines}] Speaking...")

    if isinstance(engine, EdgeTTSEngine):
        if _tray_app:
            _tray_app.set_generating(True)

        # Prefetch next line while this one plays
        if _current_line_index + 1 < len(_line_queue):
            next_line = _line_queue[_current_line_index + 1]
            engine.prefetch(next_line)
    else:
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


def on_read_mode_change(mode: str):
    """Handle read mode change from tray menu."""
    global _read_mode
    logger.info(f"Changing read mode to: {mode}")
    _read_mode = mode
    set_setting("read_mode", mode)
    engine = get_engine()
    if mode == "continuous":
        engine.speak("Continuous mode enabled")
    else:
        engine.speak("Line by line mode enabled")


def on_log_preview_change(enabled: bool):
    """Handle log preview toggle from tray menu."""
    global _log_preview
    logger.info(f"Log preview: {'enabled' if enabled else 'disabled'}")
    _log_preview = enabled
    set_setting("log_preview", enabled)
    engine = get_engine()
    if enabled:
        engine.speak("Text preview enabled")
    else:
        engine.speak("Text preview disabled")


def on_auto_copy_change(enabled: bool):
    """Handle auto-copy toggle from tray menu."""
    global _auto_copy
    logger.info(f"Auto-copy: {'enabled' if enabled else 'disabled'}")
    _auto_copy = enabled
    set_setting("auto_copy", enabled)
    engine = get_engine()
    if enabled:
        engine.speak("Auto copy enabled")
    else:
        engine.speak("Auto copy disabled")


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
    global _tray_app, _current_speak_hotkey, _current_pause_hotkey
    global _line_delay, _read_mode, _log_preview, _auto_copy

    # Ensure only one instance runs
    ensure_single_instance()

    setup_logging()

    # Load settings
    settings = load_settings()
    _current_speak_hotkey = settings.get("hotkey_speak", DEFAULT_SPEAK_HOTKEY)
    _current_pause_hotkey = settings.get("hotkey_pause", DEFAULT_PAUSE_HOTKEY)
    _line_delay = settings.get("line_delay", DEFAULT_LINE_DELAY)
    _read_mode = settings.get("read_mode", DEFAULT_READ_MODE)
    _log_preview = settings.get("log_preview", DEFAULT_LOG_PREVIEW)
    _auto_copy = settings.get("auto_copy", DEFAULT_AUTO_COPY)

    logger.info("Herald started")
    logger.info(f"  Speak:      {_current_speak_hotkey}")
    logger.info(f"  Pause:      {_current_pause_hotkey}")
    logger.info(f"  Speed up:   {SPEED_UP_HOTKEY}")
    logger.info(f"  Slow down:  {SPEED_DOWN_HOTKEY}")
    logger.info(f"  Next line:  {NEXT_LINE_HOTKEY}")
    logger.info(f"  Prev line:  {PREV_LINE_HOTKEY}")
    logger.info(f"  OCR region: {OCR_REGION_HOTKEY}")
    logger.info(f"  Stop:       {STOP_HOTKEY}")
    logger.info(f"  Quit:       {QUIT_HOTKEY}")
    print()
    print("Tip: Right-click the tray icon to hide this console or change settings.")
    print()

    # Initialize engine
    engine = get_engine()
    logger.info(f"Voice: {engine.voice_name}, Speed: {engine.rate} wpm")

    # Start tray app
    _tray_app = TrayApp(
        on_voice_change=on_voice_change,
        on_speed_change=on_speed_change,
        on_line_delay_change=on_line_delay_change,
        on_read_mode_change=on_read_mode_change,
        on_log_preview_change=on_log_preview_change,
        on_auto_copy_change=on_auto_copy_change,
        on_pause_toggle=on_pause_resume,
        on_console_toggle=on_console_toggle,
        on_speak_hotkey_change=on_speak_hotkey_change,
        on_pause_hotkey_change=on_pause_hotkey_change,
        on_quit=on_quit,
        current_voice=engine.voice_name,
        current_speed=engine.rate,
        current_line_delay=_line_delay,
        current_read_mode=_read_mode,
        current_log_preview=_log_preview,
        current_auto_copy=_auto_copy,
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
    keyboard.add_hotkey(OCR_REGION_HOTKEY, on_ocr_region, suppress=True)
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
