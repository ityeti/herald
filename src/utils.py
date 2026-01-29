"""
Herald Utilities

Logging setup and helper functions.
"""

import sys
from pathlib import Path
from loguru import logger

from config import LOG_DIR, LOG_FILE, LOG_ROTATION, LOG_RETENTION


def setup_logging():
    """Configure loguru for file and console logging."""
    # Get project root (parent of src/)
    src_dir = Path(__file__).parent
    project_root = src_dir.parent
    log_dir = project_root / LOG_DIR
    log_dir.mkdir(exist_ok=True)

    log_path = log_dir / LOG_FILE

    # Remove default handler
    logger.remove()

    # Console handler (INFO and above)
    logger.add(
        sys.stderr,
        format="<level>{message}</level>",
        level="INFO",
        colorize=True
    )

    # File handler (DEBUG and above, with rotation)
    logger.add(
        log_path,
        format="{time:YYYY-MM-DD HH:mm:ss} | {level: <8} | {message}",
        level="DEBUG",
        rotation=LOG_ROTATION,
        retention=LOG_RETENTION,
        encoding="utf-8"
    )

    logger.info(f"Logging to {log_path}")
    return log_path


# Self-test
if __name__ == "__main__":
    log_path = setup_logging()
    logger.debug("Debug message (file only)")
    logger.info("Info message (console + file)")
    logger.warning("Warning message")
    logger.error("Error message")
    print(f"\nLog file: {log_path}")
