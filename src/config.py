"""
Herald Configuration

Manages settings for the TTS utility.
"""

# Hotkeys
# Options considered for speak: alt+r, ctrl+shift+r, win+alt+r, f9, ctrl+., alt+s
DEFAULT_HOTKEY = "alt+s"      # Speak clipboard
STOP_HOTKEY = "escape"        # Stop speaking
SPEED_UP_HOTKEY = "alt+]"     # Increase rate
SPEED_DOWN_HOTKEY = "alt+["   # Decrease rate
QUIT_HOTKEY = "alt+q"         # Exit application

# TTS Settings
DEFAULT_RATE = 200  # Words per minute (pyttsx3 default is ~200)
MIN_RATE = 50
MAX_RATE = 400
RATE_STEP = 25      # Amount to change per speed hotkey press

# Logging
LOG_DIR = "logs"
LOG_FILE = "herald.log"
LOG_ROTATION = "10 MB"
LOG_RETENTION = "7 days"
