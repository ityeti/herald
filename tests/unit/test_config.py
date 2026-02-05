"""
Unit tests for Herald configuration module.

Tests config loading, saving, and defaults without touching the real config file.
"""

import json
import pytest
from unittest.mock import patch


@pytest.mark.unit
class TestConfigDefaults:
    """Test configuration default values."""

    def test_default_settings_structure(self):
        """Verify DEFAULT_SETTINGS has all required keys."""
        from config import DEFAULT_SETTINGS

        required_keys = [
            "engine",
            "voice",
            "rate",
            "hotkey_speak",
            "hotkey_pause",
            "line_delay",
            "read_mode",
            "log_preview",
            "auto_copy",
            "ocr_to_clipboard",
            "auto_read",
        ]

        for key in required_keys:
            assert key in DEFAULT_SETTINGS, f"Missing default key: {key}"

    def test_default_engine_is_edge(self):
        """Default engine should be 'edge' for better quality."""
        from config import DEFAULT_ENGINE

        assert DEFAULT_ENGINE == "edge"

    def test_default_voice_is_aria(self):
        """Default voice should be 'aria' (Edge TTS)."""
        from config import DEFAULT_VOICE

        assert DEFAULT_VOICE == "aria"

    def test_rate_bounds(self):
        """Verify rate min/max constants are sensible."""
        from config import MIN_RATE, MAX_RATE, DEFAULT_RATE

        assert MIN_RATE > 0, "MIN_RATE must be positive"
        assert MAX_RATE > MIN_RATE, "MAX_RATE must exceed MIN_RATE"
        assert MIN_RATE <= DEFAULT_RATE <= MAX_RATE, "DEFAULT_RATE out of bounds"


@pytest.mark.unit
class TestConfigLoadSave:
    """Test configuration file operations."""

    def test_load_creates_default_if_missing(self, temp_config_dir):
        """load_settings should create defaults if file doesn't exist."""
        from config import DEFAULT_SETTINGS

        # Patch the settings file location
        settings_file = temp_config_dir / "settings.json"

        with patch("config.SETTINGS_FILE", settings_file), patch("config.CONFIG_DIR", temp_config_dir):
            from config import load_settings

            settings = load_settings()

        # Should return defaults
        for key in DEFAULT_SETTINGS:
            assert key in settings

        # File should now exist
        assert settings_file.exists()

    def test_load_merges_with_defaults(self, temp_config_dir):
        """load_settings should add missing keys from defaults."""
        from config import DEFAULT_SETTINGS

        # Create partial settings file
        settings_file = temp_config_dir / "settings.json"
        partial_settings = {"engine": "pyttsx3", "voice": "david"}
        with open(settings_file, "w") as f:
            json.dump(partial_settings, f)

        with patch("config.SETTINGS_FILE", settings_file), patch("config.CONFIG_DIR", temp_config_dir):
            from config import load_settings

            settings = load_settings()

        # Should have custom values
        assert settings["engine"] == "pyttsx3"
        assert settings["voice"] == "david"

        # Should have defaults for missing keys
        assert "rate" in settings
        assert settings["rate"] == DEFAULT_SETTINGS["rate"]

    def test_save_settings_creates_file(self, temp_config_dir):
        """save_settings should create the config file."""
        settings_file = temp_config_dir / "settings.json"
        test_settings = {"engine": "edge", "voice": "guy", "rate": 400}

        with patch("config.SETTINGS_FILE", settings_file), patch("config.CONFIG_DIR", temp_config_dir):
            from config import save_settings

            save_settings(test_settings)

        assert settings_file.exists()

        with open(settings_file) as f:
            saved = json.load(f)

        assert saved["voice"] == "guy"
        assert saved["rate"] == 400

    def test_get_setting_returns_value(self, temp_settings_file, sample_settings):
        """get_setting should return the correct value."""
        with patch("config.SETTINGS_FILE", temp_settings_file), patch("config.CONFIG_DIR", temp_settings_file.parent):
            from config import get_setting

            assert get_setting("voice") == sample_settings["voice"]
            assert get_setting("rate") == sample_settings["rate"]

    def test_get_setting_returns_default_for_missing(self, temp_settings_file):
        """get_setting should return default for missing keys."""
        with patch("config.SETTINGS_FILE", temp_settings_file), patch("config.CONFIG_DIR", temp_settings_file.parent):
            from config import get_setting

            assert get_setting("nonexistent", "default_value") == "default_value"

    def test_set_setting_updates_value(self, temp_settings_file):
        """set_setting should update and save the value."""
        with patch("config.SETTINGS_FILE", temp_settings_file), patch("config.CONFIG_DIR", temp_settings_file.parent):
            from config import set_setting, get_setting

            set_setting("rate", 750)
            assert get_setting("rate") == 750

            # Verify it was persisted
            with open(temp_settings_file) as f:
                saved = json.load(f)
            assert saved["rate"] == 750


@pytest.mark.unit
class TestConfigConstants:
    """Test configuration constants."""

    def test_hotkey_defaults_are_valid_format(self):
        """Hotkey defaults should be valid keyboard module format."""
        from config import DEFAULT_HOTKEYS

        assert len(DEFAULT_HOTKEYS) == 10

        for key, hotkey in DEFAULT_HOTKEYS.items():
            assert isinstance(hotkey, str), f"{key} is not a string"
            assert len(hotkey) > 0, f"{key} is empty"
            # Basic format check - should contain letters/symbols
            assert any(c.isalpha() or c in "[]+-" for c in hotkey), f"{key} has invalid format"

    def test_rate_step_is_positive(self):
        """RATE_STEP should be a positive integer."""
        from config import RATE_STEP

        assert isinstance(RATE_STEP, int)
        assert RATE_STEP > 0
