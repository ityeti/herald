"""
Herald Text Grabbing

Handles getting text from clipboard or selection.
Supports auto-copy (Ctrl+C simulation) and OCR for images.
"""

import time
import pyperclip
import keyboard
from typing import Optional, Tuple
from loguru import logger


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
        logger.error(f"Clipboard error: {e}")
        return None


def get_clipboard_image():
    """
    Get image from the system clipboard.

    Returns:
        PIL Image if clipboard contains an image, None otherwise.
    """
    try:
        from PIL import ImageGrab
        image = ImageGrab.grabclipboard()
        if image is not None:
            # ImageGrab returns PIL Image for images, list of file paths for files
            from PIL import Image
            if isinstance(image, Image.Image):
                return image
        return None
    except Exception as e:
        logger.debug(f"No image in clipboard: {e}")
        return None


def auto_copy_selection() -> bool:
    """
    Simulate Ctrl+C to copy any current selection.

    Returns:
        True if copy was attempted, False on error.
    """
    try:
        # Small delay to ensure hotkey is released
        time.sleep(0.05)
        # Send Ctrl+C
        keyboard.send('ctrl+c')
        # Wait for clipboard to update
        time.sleep(0.1)
        return True
    except Exception as e:
        logger.error(f"Auto-copy failed: {e}")
        return False


def get_text_to_speak(auto_copy: bool = True) -> Optional[str]:
    """
    Get the text that should be spoken.

    If auto_copy is True, first simulates Ctrl+C to copy any selection.
    Then checks clipboard for text.

    Args:
        auto_copy: If True, simulate Ctrl+C first to copy selection.

    Returns:
        Text to speak, or None if nothing available.
    """
    if auto_copy:
        auto_copy_selection()

    return get_clipboard_text()


def get_content_to_speak(auto_copy: bool = True) -> Tuple[Optional[str], str]:
    """
    Get content to speak - either text or OCR'd image.

    Args:
        auto_copy: If True, simulate Ctrl+C first to copy selection.

    Returns:
        Tuple of (text, source) where source is "text", "ocr", or "none".
    """
    if auto_copy:
        auto_copy_selection()

    # Check for text first
    text = get_clipboard_text()
    if text:
        return (text, "text")

    # Check for image and OCR it
    image = get_clipboard_image()
    if image:
        ocr_text = ocr_image(image)
        if ocr_text:
            return (ocr_text, "ocr")

    return (None, "none")


def ocr_image(image) -> Optional[str]:
    """
    Run OCR on a PIL Image using Windows OCR.

    Args:
        image: PIL Image to OCR.

    Returns:
        Extracted text, or None if OCR failed.
    """
    try:
        import winocr
        import asyncio

        # Convert to bytes for winocr
        import io
        buffer = io.BytesIO()
        image.save(buffer, format='PNG')
        image_bytes = buffer.getvalue()

        # Run Windows OCR
        async def run_ocr():
            result = await winocr.recognize_bytes(image_bytes, lang='en')
            return result.text if result else None

        text = asyncio.run(run_ocr())

        if text and text.strip():
            logger.info(f"OCR extracted {len(text)} characters")
            return text.strip()
        return None

    except ImportError:
        logger.warning("winocr not installed - OCR disabled")
        return None
    except Exception as e:
        logger.error(f"OCR failed: {e}")
        return None
