"""
Herald Configuration

Manages settings and user preferences.
"""

import json
from pathlib import Path
from typing import Optional

# Project paths
PROJECT_ROOT = Path(__file__).parent.parent
CONFIG_DIR = PROJECT_ROOT / "config"
SETTINGS_FILE = CONFIG_DIR / "settings.json"

# Hotkeys (not user-configurable yet)
DEFAULT_HOTKEY = "alt+s"      # Speak clipboard
STOP_HOTKEY = "escape"        # Stop speaking
PAUSE_HOTKEY = "alt+p"        # Pause/resume
SPEED_UP_HOTKEY = "alt+]"     # Increase rate
SPEED_DOWN_HOTKEY = "alt+["   # Decrease rate
QUIT_HOTKEY = "alt+q"         # Exit application

# TTS defaults
DEFAULT_RATE = 500   # Words per minute
MIN_RATE = 50
MAX_RATE = 600
RATE_STEP = 25       # Amount to change per speed hotkey press

# Voice defaults
# Edge voices: aria, guy, jenny, christopher (online, neural)
# Pyttsx3 voices: zira, david (offline, Windows SAPI)
DEFAULT_VOICE = "aria"

# Logging
LOG_DIR = "logs"
LOG_FILE = "herald.log"
LOG_ROTATION = "10 MB"
LOG_RETENTION = "7 days"

# Engine: "edge" (online, better quality) or "pyttsx3" (offline)
DEFAULT_ENGINE = "edge"

# Default settings structure
DEFAULT_SETTINGS = {
    "engine": DEFAULT_ENGINE,
    "voice": DEFAULT_VOICE,
    "rate": DEFAULT_RATE,
}


def load_settings() -> dict:
    """Load settings from JSON file, creating defaults if needed."""
    CONFIG_DIR.mkdir(exist_ok=True)

    if SETTINGS_FILE.exists():
        try:
            with open(SETTINGS_FILE, "r") as f:
                settings = json.load(f)
                # Merge with defaults for any missing keys
                for key, value in DEFAULT_SETTINGS.items():
                    if key not in settings:
                        settings[key] = value
                return settings
        except (json.JSONDecodeError, IOError):
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
