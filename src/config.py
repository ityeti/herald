"""
Herald Configuration

Manages settings for the TTS utility.
"""

# Default hotkey (can be changed in settings later)
# Options considered: alt+r, ctrl+shift+r, win+alt+r, f9, ctrl+., alt+s
DEFAULT_HOTKEY = "alt+s"

# Stop hotkey
STOP_HOTKEY = "escape"

# TTS Settings
DEFAULT_RATE = 200  # Words per minute (pyttsx3 default is ~200)
MIN_RATE = 50
MAX_RATE = 400

# Rate adjustment step (for speed up/down hotkeys in Phase 2)
RATE_STEP = 25
