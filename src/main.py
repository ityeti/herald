"""
Herald - Text-to-Speech Utility

Press a hotkey to hear clipboard/selected text read aloud.
The inverse of whisper (voice-to-text).

Usage:
    python main.py

Hotkeys:
    Alt+S - Read clipboard text (speak)
    Escape - Stop reading

Requires admin privileges on Windows for global hotkeys.
"""

import sys
import keyboard

from config import DEFAULT_HOTKEY, STOP_HOTKEY
from tts_engine import get_engine
from text_grab import get_text_to_speak


def on_speak_hotkey():
    """Called when the speak hotkey is pressed."""
    text = get_text_to_speak()

    if text:
        print(f"Speaking: {text[:50]}..." if len(text) > 50 else f"Speaking: {text}")
        engine = get_engine()
        engine.speak(text)
    else:
        print("No text to speak (clipboard empty)")


def on_stop_hotkey():
    """Called when the stop hotkey is pressed."""
    engine = get_engine()
    if engine.is_speaking:
        print("Stopping...")
        engine.stop()


def main():
    """Main entry point."""
    # Minimal output - launcher batch file shows the banner
    print("Ready. Copy text to clipboard, press Alt+S to speak.")
    print()

    # Register hotkeys
    keyboard.add_hotkey(DEFAULT_HOTKEY, on_speak_hotkey, suppress=True)
    keyboard.add_hotkey(STOP_HOTKEY, on_stop_hotkey, suppress=False)

    try:
        # Keep running until Ctrl+C
        keyboard.wait()
    except KeyboardInterrupt:
        print("\nExiting Herald...")
        engine = get_engine()
        engine.stop()
        sys.exit(0)


if __name__ == "__main__":
    main()
