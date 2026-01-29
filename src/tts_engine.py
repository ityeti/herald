"""
Herald TTS Engine

Abstraction layer for text-to-speech engines.
MVP uses pyttsx3 (offline, simple).
Can swap to edge-tts later for better voices.
"""

import pyttsx3
import threading
from typing import Optional

from config import DEFAULT_RATE, MIN_RATE, MAX_RATE


class TTSEngine:
    """
    Text-to-speech engine wrapper.

    Usage:
        engine = TTSEngine()
        engine.speak("Hello world")
        engine.stop()  # Interrupt speech
    """

    def __init__(self, rate: int = DEFAULT_RATE):
        self._engine: Optional[pyttsx3.Engine] = None
        self._rate = rate
        self._speaking = False
        self._thread: Optional[threading.Thread] = None

    def _get_engine(self) -> pyttsx3.Engine:
        """Get or create the TTS engine."""
        if self._engine is None:
            self._engine = pyttsx3.init()
            self._engine.setProperty('rate', self._rate)
        return self._engine

    @property
    def rate(self) -> int:
        """Current speech rate (words per minute)."""
        return self._rate

    @rate.setter
    def rate(self, value: int):
        """Set speech rate, clamped to valid range."""
        self._rate = max(MIN_RATE, min(MAX_RATE, value))
        if self._engine:
            self._engine.setProperty('rate', self._rate)

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
                print(f"TTS error: {e}")
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

    def set_voice(self, voice_id: str) -> None:
        """Set the voice by ID."""
        engine = self._get_engine()
        engine.setProperty('voice', voice_id)


# Singleton instance for the application
_engine_instance: Optional[TTSEngine] = None


def get_engine() -> TTSEngine:
    """Get the singleton TTS engine instance."""
    global _engine_instance
    if _engine_instance is None:
        _engine_instance = TTSEngine()
    return _engine_instance
