"""
Herald Configuration

Manages settings and user preferences.
"""

import json
from pathlib import Path

# Project paths
PROJECT_ROOT = Path(__file__).parent.parent
CONFIG_DIR = PROJECT_ROOT / "config"
SETTINGS_FILE = CONFIG_DIR / "settings.json"

# Hotkey defaults (all configurable via tray menu and settings.json)
# Changed from Alt+key to Ctrl+Shift+key to avoid Windows Terminal conflicts
DEFAULT_HOTKEYS = {
    "hotkey_speak": "ctrl+shift+s",
    "hotkey_pause": "ctrl+shift+p",
    "hotkey_stop": "escape",
    "hotkey_speed_up": "ctrl+shift+]",
    "hotkey_speed_down": "ctrl+shift+[",
    "hotkey_next": "ctrl+shift+n",
    "hotkey_prev": "ctrl+shift+b",
    "hotkey_ocr": "ctrl+shift+o",
    "hotkey_monitor": "ctrl+shift+m",
    "hotkey_quit": "ctrl+shift+q",
}

# Individual constants for backward compatibility
DEFAULT_SPEAK_HOTKEY = DEFAULT_HOTKEYS["hotkey_speak"]
DEFAULT_PAUSE_HOTKEY = DEFAULT_HOTKEYS["hotkey_pause"]
DEFAULT_STOP_HOTKEY = DEFAULT_HOTKEYS["hotkey_stop"]
DEFAULT_SPEED_UP_HOTKEY = DEFAULT_HOTKEYS["hotkey_speed_up"]
DEFAULT_SPEED_DOWN_HOTKEY = DEFAULT_HOTKEYS["hotkey_speed_down"]
DEFAULT_NEXT_LINE_HOTKEY = DEFAULT_HOTKEYS["hotkey_next"]
DEFAULT_PREV_LINE_HOTKEY = DEFAULT_HOTKEYS["hotkey_prev"]
DEFAULT_OCR_REGION_HOTKEY = DEFAULT_HOTKEYS["hotkey_ocr"]
DEFAULT_MONITOR_REGION_HOTKEY = DEFAULT_HOTKEYS["hotkey_monitor"]
DEFAULT_QUIT_HOTKEY = DEFAULT_HOTKEYS["hotkey_quit"]

# Which hotkeys suppress key passthrough (to prevent typing brackets)
HOTKEY_SUPPRESS = {
    "hotkey_speed_up": True,
    "hotkey_speed_down": True,
}

# TTS defaults
DEFAULT_RATE = 900  # Words per minute (max for online voices)
MIN_RATE = 150  # Minimum effective for online voices
MAX_RATE = 1500  # Maximum for offline voices
RATE_STEP = 25  # Amount to change per speed hotkey press

# Line navigation
DEFAULT_LINE_DELAY = 0  # Milliseconds between lines (0 = no delay)
DEFAULT_READ_MODE = "lines"  # "lines" or "continuous"

# Privacy
DEFAULT_LOG_PREVIEW = True  # Show text preview in console/logs

# Auto-copy
DEFAULT_AUTO_COPY = True  # Auto Ctrl+C before reading (disable for terminals)

# Text filtering
DEFAULT_FILTER_CODE = True  # Filter URLs, code, paths by default
DEFAULT_NORMALIZE_TEXT = True  # Normalize identifiers for speech (snake_case, camelCase)

# OCR settings
DEFAULT_OCR_TO_CLIPBOARD = True  # Copy OCR'd text to clipboard
DEFAULT_AUTO_READ = False  # Auto-read when persistent region text changes
DEFAULT_AUTO_READ_INTERVAL = 2.5  # Seconds between OCR polls
DEFAULT_AUTO_READ_THRESHOLD = 0.5  # Minimum change ratio to trigger read (0.5 = 50%)

# Voice defaults
# Edge voices: aria, guy, jenny, christopher (online, neural)
# Pyttsx3 voices: zira, david (offline, Windows SAPI)
DEFAULT_VOICE = "aria"

# Logging
LOG_DIR = "logs"
LOG_FILE = "herald.log"
LOG_ROTATION = "1 day"  # Daily rotation (was 10MB — too high, never triggered)
LOG_RETENTION = "7 days"

# Engine: "edge" (online, better quality) or "pyttsx3" (offline)
DEFAULT_ENGINE = "edge"

# Default settings structure
DEFAULT_SETTINGS = {
    "engine": DEFAULT_ENGINE,
    "voice": DEFAULT_VOICE,
    "rate": DEFAULT_RATE,
    "hotkey_speak": DEFAULT_SPEAK_HOTKEY,
    "hotkey_pause": DEFAULT_PAUSE_HOTKEY,
    "hotkey_stop": DEFAULT_STOP_HOTKEY,
    "hotkey_speed_up": DEFAULT_SPEED_UP_HOTKEY,
    "hotkey_speed_down": DEFAULT_SPEED_DOWN_HOTKEY,
    "hotkey_next": DEFAULT_NEXT_LINE_HOTKEY,
    "hotkey_prev": DEFAULT_PREV_LINE_HOTKEY,
    "hotkey_ocr": DEFAULT_OCR_REGION_HOTKEY,
    "hotkey_monitor": DEFAULT_MONITOR_REGION_HOTKEY,
    "hotkey_quit": DEFAULT_QUIT_HOTKEY,
    "line_delay": DEFAULT_LINE_DELAY,
    "read_mode": DEFAULT_READ_MODE,
    "log_preview": DEFAULT_LOG_PREVIEW,
    "auto_copy": DEFAULT_AUTO_COPY,
    "ocr_to_clipboard": DEFAULT_OCR_TO_CLIPBOARD,
    "auto_read": DEFAULT_AUTO_READ,
    "filter_code": DEFAULT_FILTER_CODE,
    "normalize_text": DEFAULT_NORMALIZE_TEXT,
}


def load_settings() -> dict:
    """Load settings from JSON file, creating defaults if needed."""
    CONFIG_DIR.mkdir(exist_ok=True)

    if SETTINGS_FILE.exists():
        try:
            with open(SETTINGS_FILE) as f:
                settings = json.load(f)
                # Merge with defaults for any missing keys
                for key, value in DEFAULT_SETTINGS.items():
                    if key not in settings:
                        settings[key] = value
                return settings
        except (OSError, json.JSONDecodeError):
            pass

    # Create default settings file
    save_settings(DEFAULT_SETTINGS)
    return DEFAULT_SETTINGS.copy()


def save_settings(settings: dict) -> None:
    """Save settings to JSON file."""
    CONFIG_DIR.mkdir(exist_ok=True)
    with open(SETTINGS_FILE, "w") as f:
        json.dump(settings, f, indent=2)


def get_setting(key: str, default=None):
    """Get a single setting value."""
    settings = load_settings()
    return settings.get(key, default)


def set_setting(key: str, value) -> None:
    """Set and save a single setting value."""
    settings = load_settings()
    settings[key] = value
    save_settings(settings)


# Self-test
if __name__ == "__main__":
    print(f"Config dir: {CONFIG_DIR}")
    print(f"Settings file: {SETTINGS_FILE}")
    print(f"Current settings: {load_settings()}")
