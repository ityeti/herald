"""
Herald Text Grabbing

Handles getting text from clipboard or selection.
MVP: clipboard only. Phase 2+ may add selection via accessibility APIs.
"""

import pyperclip
from typing import Optional


def get_clipboard_text() -> Optional[str]:
    """
    Get text from the system clipboard.

    Returns:
        The clipboard text, or None if clipboard is empty or not text.
    """
    try:
        text = pyperclip.paste()
        if text and text.strip():
            return text.strip()
        return None
    except Exception as e:
        print(f"Clipboard error: {e}")
        return None


def get_selected_text() -> Optional[str]:
    """
    Get currently selected text.

    MVP: Falls back to clipboard (user can Ctrl+C first).
    Phase 2: Could use Windows accessibility APIs to get actual selection.

    Returns:
        The selected text, or None if nothing selected.
    """
    # MVP: Just use clipboard
    # User workflow: Select text → Ctrl+C → Hotkey
    # Or: Select text → Hotkey (if we add Ctrl+C simulation later)
    return get_clipboard_text()


def get_text_to_speak() -> Optional[str]:
    """
    Get the text that should be spoken.

    Tries selection first, falls back to clipboard.

    Returns:
        Text to speak, or None if nothing available.
    """
    # Try selected text first
    text = get_selected_text()
    if text:
        return text

    # Fall back to clipboard
    return get_clipboard_text()
