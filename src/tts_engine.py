"""
Herald TTS Engine

Abstraction layer for text-to-speech engines.
MVP uses pyttsx3 (offline, simple).
Can swap to edge-tts later for better voices.
"""

import pyttsx3
import threading
from typing import Optional
from loguru import logger

from config import MIN_RATE, MAX_RATE, load_settings, set_setting


class TTSEngine:
    """
    Text-to-speech engine wrapper.

    Usage:
        engine = TTSEngine()
        engine.speak("Hello world")
        engine.stop()  # Interrupt speech
    """

    def __init__(self):
        self._engine: Optional[pyttsx3.Engine] = None
        self._speaking = False
        self._thread: Optional[threading.Thread] = None
        self._voice_id: Optional[str] = None

        # Load saved settings
        settings = load_settings()
        self._rate = settings.get("rate", 350)
        self._voice_name = settings.get("voice", "zira")

    def _get_engine(self) -> pyttsx3.Engine:
        """Get or create the TTS engine."""
        if self._engine is None:
            self._engine = pyttsx3.init()
            self._engine.setProperty('rate', self._rate)
            self._apply_voice()
        return self._engine

    def _apply_voice(self) -> None:
        """Apply the current voice setting."""
        if not self._engine:
            return

        voices = self._engine.getProperty('voices')
        for voice in voices:
            if self._voice_name.lower() in voice.name.lower():
                self._engine.setProperty('voice', voice.id)
                self._voice_id = voice.id
                logger.debug(f"Voice set to: {voice.name}")
                return

        logger.warning(f"Voice '{self._voice_name}' not found, using default")

    @property
    def rate(self) -> int:
        """Current speech rate (words per minute)."""
        return self._rate

    @rate.setter
    def rate(self, value: int):
        """Set speech rate, clamped to valid range, and save to settings."""
        self._rate = max(MIN_RATE, min(MAX_RATE, value))
        if self._engine:
            self._engine.setProperty('rate', self._rate)
        # Save to persistent settings
        set_setting("rate", self._rate)

    @property
    def voice_name(self) -> str:
        """Current voice name."""
        return self._voice_name

    @voice_name.setter
    def voice_name(self, name: str):
        """Set voice by name (e.g., 'zira', 'david')."""
        self._voice_name = name.lower()
        set_setting("voice", self._voice_name)
        if self._engine:
            self._apply_voice()

    @property
    def is_speaking(self) -> bool:
        """Whether TTS is currently speaking."""
        return self._speaking

    def speak(self, text: str) -> None:
        """
        Speak the given text.

        Runs in a background thread so it doesn't block.
        Call stop() to interrupt.
        """
        if not text or not text.strip():
            return

        # Stop any current speech first
        self.stop()

        def _speak_thread():
            self._speaking = True
            try:
                engine = self._get_engine()
                engine.say(text)
                engine.runAndWait()
            except Exception as e:
                logger.error(f"TTS error: {e}")
            finally:
                self._speaking = False

        self._thread = threading.Thread(target=_speak_thread, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        """Stop current speech immediately."""
        if self._engine and self._speaking:
            try:
                self._engine.stop()
            except Exception:
                pass
        self._speaking = False

        # Recreate engine for next use (pyttsx3 quirk)
        self._engine = None

    def get_voices(self) -> list:
        """Get available voices."""
        engine = self._get_engine()
        return engine.getProperty('voices')


# Singleton instance for the application
_engine_instance: Optional[TTSEngine] = None


def get_engine() -> TTSEngine:
    """Get the singleton TTS engine instance."""
    global _engine_instance
    if _engine_instance is None:
        _engine_instance = TTSEngine()
    return _engine_instance


# Self-test
if __name__ == "__main__":
    import time

    print("Testing TTS Engine...")
    engine = get_engine()

    print(f"Available voices:")
    for voice in engine.get_voices():
        print(f"  - {voice.name}")

    print(f"\nCurrent voice: {engine.voice_name}")
    print(f"Current rate: {engine.rate} wpm")

    print("\nSpeaking test...")
    engine.speak("Hello, this is a test of the Herald text to speech engine.")
    time.sleep(3)

    print("Done.")
