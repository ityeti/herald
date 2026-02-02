"""
Herald Test Configuration

Shared fixtures and test utilities.
"""

import os
import sys
import json
import tempfile
import pytest
from pathlib import Path

# Add src to path for imports
PROJECT_ROOT = Path(__file__).parent.parent
sys.path.insert(0, str(PROJECT_ROOT / "src"))
sys.path.insert(0, str(PROJECT_ROOT))

# Suppress pygame welcome message
os.environ['PYGAME_HIDE_SUPPORT_PROMPT'] = "1"


@pytest.fixture
def temp_config_dir(tmp_path):
    """Create a temporary config directory for testing."""
    config_dir = tmp_path / "config"
    config_dir.mkdir()
    return config_dir


@pytest.fixture
def sample_settings():
    """Return sample settings for testing."""
    return {
        "engine": "edge",
        "voice": "aria",
        "rate": 500,
        "hotkey_speak": "alt+s",
        "hotkey_pause": "alt+p",
        "line_delay": 0,
        "read_mode": "lines",
        "log_preview": True,
        "auto_copy": True,
        "ocr_to_clipboard": True,
        "auto_read": False,
    }


@pytest.fixture
def temp_settings_file(temp_config_dir, sample_settings):
    """Create a temporary settings.json file."""
    settings_file = temp_config_dir / "settings.json"
    with open(settings_file, "w") as f:
        json.dump(sample_settings, f)
    return settings_file


@pytest.fixture
def temp_audio_dir(tmp_path):
    """Create a temporary directory for audio files."""
    audio_dir = tmp_path / "audio"
    audio_dir.mkdir()
    return audio_dir


@pytest.fixture(scope="session")
def project_root():
    """Return the project root directory."""
    return PROJECT_ROOT


# Test data for TTS validation
SAMPLE_TEXTS = {
    "short": "Hello world",
    "medium": "The quick brown fox jumps over the lazy dog.",
    "with_numbers": "I have 3 apples and 2 oranges.",
    "with_punctuation": "Hello! How are you? I'm fine, thanks.",
}


@pytest.fixture
def sample_texts():
    """Return sample texts for TTS testing."""
    return SAMPLE_TEXTS.copy()
