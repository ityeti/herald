"""
Herald Text Grabbing

Handles getting text from clipboard or selection.
Supports auto-copy (Ctrl+C simulation) and OCR for images.
"""

import time
import pyperclip
import keyboard
from loguru import logger


def get_clipboard_text() -> str | None:
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
        from PIL import ImageGrab, Image
        image = ImageGrab.grabclipboard()

        if image is None:
            return None

        if isinstance(image, Image.Image):
            logger.debug(f"Found image in clipboard: {image.size[0]}x{image.size[1]}")
            return image

        if isinstance(image, list):
            logger.debug(f"Clipboard contains file paths: {image}")
            return None

        return None
    except Exception as e:
        logger.error(f"Error checking clipboard for image: {e}")
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


def get_text_to_speak(auto_copy: bool = True) -> str | None:
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


def get_content_to_speak(auto_copy: bool = True) -> tuple[str | None, str]:
    """
    Get content to speak - either text or OCR'd image.

    Args:
        auto_copy: If True, simulate Ctrl+C first to copy selection.

    Returns:
        Tuple of (text, source) where source is "text", "ocr", or "none".
    """
    # Check for image FIRST - before auto-copy which would overwrite it
    image = get_clipboard_image()
    if image:
        logger.debug("Found image in clipboard, running OCR...")
        ocr_text = ocr_image(image)
        if ocr_text:
            return (ocr_text, "ocr")
        else:
            logger.warning("OCR returned no text from image")

    # No image - try auto-copy to get selected text
    if auto_copy:
        auto_copy_selection()

    # Check for text
    text = get_clipboard_text()
    if text:
        return (text, "text")

    return (None, "none")


def ocr_image(image) -> str | None:
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

        width, height = image.size
        logger.debug(f"OCR: Processing image {width}x{height}")

        # Convert to raw bytes (RGBA) for winocr
        # winocr.recognize_bytes expects raw pixel data, not PNG
        if image.mode != 'RGBA':
            image = image.convert('RGBA')
        image_bytes = image.tobytes()
        logger.debug(f"OCR: Image converted to {len(image_bytes)} bytes (RGBA)")

        # Run Windows OCR
        async def run_ocr():
            result = await winocr.recognize_bytes(image_bytes, width, height, lang='en')
            return result.text if result else None

        # Handle case where event loop might already exist
        try:
            loop = asyncio.get_running_loop()
        except RuntimeError:
            loop = None

        if loop is not None:
            # Already in async context - create new loop in thread
            import concurrent.futures
            with concurrent.futures.ThreadPoolExecutor() as executor:
                future = executor.submit(asyncio.run, run_ocr())
                text = future.result(timeout=10)
        else:
            text = asyncio.run(run_ocr())

        if text and text.strip():
            logger.info(f"OCR extracted {len(text)} characters")
            return text.strip()

        logger.debug("OCR: No text found in image")
        return None

    except ImportError as e:
        logger.warning(f"winocr not installed - OCR disabled: {e}")
        return None
    except Exception as e:
        logger.error(f"OCR failed: {e}", exc_info=True)
        return None
