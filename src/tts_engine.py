"""
Herald TTS Engine

Abstraction layer supporting multiple TTS backends:
- pyttsx3: Offline, uses Windows SAPI voices (Zira, David)
- edge-tts: Online, uses Microsoft Azure neural voices (Aria, Guy, Jenny)
"""

import asyncio
import os
import threading
from abc import ABC, abstractmethod
from loguru import logger

from config import MIN_RATE, MAX_RATE, load_settings, set_setting, PROJECT_ROOT


class BaseTTSEngine(ABC):
    """Abstract base class for TTS engines."""

    @abstractmethod
    def speak(self, text: str) -> None:
        """Speak the given text (non-blocking)."""
        pass

    @abstractmethod
    def stop(self) -> None:
        """Stop current speech."""
        pass

    @abstractmethod
    def pause(self) -> None:
        """Pause current speech."""
        pass

    @abstractmethod
    def resume(self) -> None:
        """Resume paused speech."""
        pass

    @property
    @abstractmethod
    def is_speaking(self) -> bool:
        """Whether currently speaking."""
        pass

    @property
    @abstractmethod
    def is_paused(self) -> bool:
        """Whether currently paused."""
        pass

    @property
    def is_generating(self) -> bool:
        """Whether currently generating audio (edge-tts only)."""
        return False  # Default: no generation phase

    @property
    @abstractmethod
    def rate(self) -> int:
        """Current speech rate."""
        pass

    @rate.setter
    @abstractmethod
    def rate(self, value: int) -> None:
        """Set speech rate."""
        pass

    @property
    @abstractmethod
    def voice_name(self) -> str:
        """Current voice name."""
        pass

    @voice_name.setter
    @abstractmethod
    def voice_name(self, name: str) -> None:
        """Set voice by name."""
        pass

    @abstractmethod
    def get_available_voices(self) -> list[str]:
        """Get list of available voice names."""
        pass


class Pyttsx3Engine(BaseTTSEngine):
    """Offline TTS using Windows SAPI via pyttsx3."""

    # Available voices for this engine
    VOICES = ["zira", "david"]

    def __init__(self):
        import pyttsx3

        self._pyttsx3 = pyttsx3

        self._engine = None
        self._speaking = False
        self._paused = False
        self._thread: threading.Thread | None = None

        # Load saved settings
        settings = load_settings()
        self._rate = settings.get("rate", 500)
        self._voice_name = settings.get("voice", "zira")

        # Ensure voice is valid for this engine
        if self._voice_name not in self.VOICES:
            self._voice_name = "zira"

    def _get_engine(self):
        if self._engine is None:
            self._engine = self._pyttsx3.init()
            self._apply_voice()
            # Set rate AFTER voice - some SAPI voices reset rate when changed
            self._engine.setProperty("rate", self._rate)
        return self._engine

    def _apply_voice(self):
        if not self._engine:
            return
        voices = self._engine.getProperty("voices")
        for voice in voices:
            if self._voice_name.lower() in voice.name.lower():
                self._engine.setProperty("voice", voice.id)
                logger.debug(f"Voice set to: {voice.name}")
                return
        logger.warning(f"Voice '{self._voice_name}' not found")

    def speak(self, text: str) -> None:
        if not text or not text.strip():
            return
        self.stop()

        def _speak_thread():
            self._speaking = True
            self._paused = False
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
        if self._engine and self._speaking:
            try:
                self._engine.stop()
            except Exception:  # noqa: S110
                pass
        self._speaking = False
        self._paused = False
        self._engine = None

    def pause(self) -> None:
        # pyttsx3 doesn't support pause well, just stop
        logger.warning("Pause not fully supported with offline engine")
        self._paused = True

    def resume(self) -> None:
        self._paused = False

    @property
    def is_speaking(self) -> bool:
        return self._speaking

    @property
    def is_paused(self) -> bool:
        return self._paused

    @property
    def rate(self) -> int:
        return self._rate

    @rate.setter
    def rate(self, value: int):
        self._rate = max(MIN_RATE, min(MAX_RATE, value))
        if self._engine:
            self._engine.setProperty("rate", self._rate)
        set_setting("rate", self._rate)

    @property
    def voice_name(self) -> str:
        return self._voice_name

    @voice_name.setter
    def voice_name(self, name: str):
        self._voice_name = name.lower()
        set_setting("voice", self._voice_name)
        if self._engine:
            self._apply_voice()
            # Reapply rate after voice change - SAPI voices can reset rate
            self._engine.setProperty("rate", self._rate)

    def get_available_voices(self) -> list[str]:
        return self.VOICES.copy()


def _speak_error(message: str):
    """Speak an error message using offline TTS (fallback when edge-tts fails)."""
    try:
        import pyttsx3

        engine = pyttsx3.init()
        engine.setProperty("rate", 200)
        engine.say(message)
        engine.runAndWait()
        engine.stop()
    except Exception as e:
        logger.error(f"Failed to speak error message: {e}")
        # Last resort: Windows beep (silently ignore if this also fails)
        try:
            import winsound

            winsound.Beep(800, 300)  # Error tone
        except Exception:  # noqa: S110
            pass


class EdgeTTSEngine(BaseTTSEngine):
    """Online TTS using Microsoft Edge neural voices."""

    # Voice mapping: friendly name -> edge-tts voice ID
    VOICES = {
        "aria": "en-US-AriaNeural",
        "guy": "en-US-GuyNeural",
        "jenny": "en-US-JennyNeural",
        "christopher": "en-US-ChristopherNeural",
    }

    # Rate mapping: our wpm -> edge-tts rate modifier
    # edge-tts uses percentage like "+50%" or "-25%"
    # Default is about 150 wpm, we want 50-600 range

    def __init__(self):
        import pygame

        self._pygame = pygame

        # Initialize pygame mixer with explicit settings
        pygame.mixer.init(frequency=44100, size=-16, channels=2, buffer=4096)
        mixer_info = pygame.mixer.get_init()
        if mixer_info:
            logger.info(
                f"Pygame mixer initialized: freq={mixer_info[0]}, format={mixer_info[1]}, channels={mixer_info[2]}"
            )
        else:
            logger.error("Pygame mixer failed to initialize!")
            _speak_error("Audio system failed to initialize.")

        self._generating = False  # True while fetching audio from API
        self._speaking = False  # True while audio is playing
        self._paused = False
        self._audio_file: str | None = None
        self._thread: threading.Thread | None = None
        self._file_counter = 0  # Unique filename counter to avoid race conditions
        self._stop_requested = False  # Signal to stop current generation

        # Thread lock for pygame mixer operations (pygame is not thread-safe)
        self._mixer_lock = threading.Lock()

        # Prefetch cache: text_hash -> audio_file_path (ordered dict for LRU eviction)
        self._prefetch_cache: dict[str, str] = {}
        self._prefetch_cache_max_size = 10  # Max cached audio files
        self._prefetch_thread: threading.Thread | None = None

        # Temp directory for audio files
        self._temp_dir = PROJECT_ROOT / "temp"
        self._temp_dir.mkdir(exist_ok=True)
        self._cleanup_old_files()  # Clean up any leftover files from previous runs

        # Load saved settings
        settings = load_settings()
        self._rate = settings.get("rate", 500)
        voice = settings.get("voice", "aria")

        # Ensure voice is valid for this engine
        if voice not in self.VOICES:
            voice = "aria"
        self._voice_name = voice

    def _cleanup_old_files(self):
        """Clean up any leftover audio files from previous runs."""
        try:
            for f in self._temp_dir.glob("herald_*.mp3"):
                try:
                    f.unlink()
                except Exception:  # noqa: S110
                    pass
        except Exception:  # noqa: S110
            pass

    def _get_text_hash(self, text: str) -> str:
        """Get a short hash for text to use as cache key."""
        import hashlib

        return hashlib.md5(text.encode()).hexdigest()[:12]

    def _rate_to_edge_modifier(self) -> str:
        """Convert our rate (wpm) to edge-tts rate modifier."""
        # Edge-tts rate modifier has practical limits (about -50% to +200%)
        # Map our wpm range to edge-tts percentage:
        #   100 wpm  -> -50%
        #   300 wpm  ->   0% (baseline)
        #   600 wpm  -> +100%
        #   1200 wpm -> +200% (capped)

        # Linear interpolation from wpm to percentage
        # At 300 wpm = 0%, every 300 wpm = 100% change
        baseline_wpm = 300
        percent = int((self._rate - baseline_wpm) / 3)

        # Clamp to edge-tts practical limits
        percent = max(-50, min(200, percent))

        logger.debug(f"Rate {self._rate} wpm -> {percent}%")

        if percent >= 0:
            return f"+{percent}%"
        return f"{percent}%"

    def speak(self, text: str) -> None:
        if not text or not text.strip():
            return
        self.stop()

        text_hash = self._get_text_hash(text)

        def _speak_thread():
            self._generating = True
            self._paused = False
            self._stop_requested = False
            audio_file = None

            try:
                # Check if we have a prefetched file for this text
                if text_hash in self._prefetch_cache:
                    audio_file = self._prefetch_cache.pop(text_hash)
                    if os.path.exists(audio_file):
                        logger.debug(f"Using prefetched audio for: {text[:30]}...")
                    else:
                        audio_file = None  # File was cleaned up, regenerate

                # Generate if not prefetched
                if audio_file is None:
                    import edge_tts

                    voice_id = self.VOICES.get(self._voice_name, "en-US-AriaNeural")
                    rate = self._rate_to_edge_modifier()

                    # Create unique temp file
                    self._file_counter += 1
                    audio_file = str(self._temp_dir / f"herald_{self._file_counter}.mp3")

                    # Run async edge-tts (this is the slow part)
                    async def generate():
                        communicate = edge_tts.Communicate(text, voice_id, rate=rate)
                        await communicate.save(audio_file)

                    asyncio.run(generate())

                    # Verify file was generated successfully
                    if os.path.exists(audio_file):
                        file_size = os.path.getsize(audio_file)
                        if file_size == 0:
                            logger.error(f"Edge TTS generated empty file: {audio_file}")
                            _speak_error("Audio generation failed. Check your internet connection.")
                            return
                        logger.debug(f"Generated audio: {file_size} bytes")
                    else:
                        logger.error(f"Edge TTS failed to create file: {audio_file}")
                        _speak_error("Audio generation failed. Check your internet connection.")
                        return

                # Check if stop was requested during generation
                if self._stop_requested:
                    self._cleanup_file(audio_file)
                    return

                # Store for cleanup
                self._audio_file = audio_file

                # Done generating, now playing
                self._generating = False
                self._speaking = True

                # Play the audio (with lock for thread safety)
                with self._mixer_lock:
                    if self._stop_requested:
                        return
                    try:
                        self._pygame.mixer.music.load(self._audio_file)
                        self._pygame.mixer.music.set_volume(1.0)  # Ensure volume is max
                        self._pygame.mixer.music.play()
                        logger.debug(f"Started playback: {self._audio_file}")
                    except Exception as e:
                        logger.error(f"Failed to play audio: {e}")
                        _speak_error("Audio playback failed.")
                        return

                # Wait for playback to complete
                while True:
                    with self._mixer_lock:
                        try:
                            busy = self._pygame.mixer.music.get_busy()
                        except Exception:
                            busy = False
                    if not busy and not self._paused:
                        break
                    if not self._speaking:
                        break
                    self._pygame.time.wait(100)

            except Exception as e:
                logger.error(f"Edge TTS error: {e}")
                _speak_error(f"Text to speech error: {str(e)[:50]}")
            finally:
                self._generating = False
                self._speaking = False
                self._cleanup_audio()

        self._thread = threading.Thread(target=_speak_thread, daemon=True)
        self._thread.start()

    def prefetch(self, text: str) -> None:
        """Pre-generate audio for text (for next line prefetching)."""
        if not text or not text.strip():
            return

        text_hash = self._get_text_hash(text)

        # Don't prefetch if already cached
        if text_hash in self._prefetch_cache:
            return

        def _prefetch_thread():
            try:
                import edge_tts

                voice_id = self.VOICES.get(self._voice_name, "en-US-AriaNeural")
                rate = self._rate_to_edge_modifier()

                # Create unique temp file
                self._file_counter += 1
                audio_file = str(self._temp_dir / f"herald_prefetch_{self._file_counter}.mp3")

                async def generate():
                    communicate = edge_tts.Communicate(text, voice_id, rate=rate)
                    await communicate.save(audio_file)

                asyncio.run(generate())

                # Verify file was generated successfully
                if os.path.exists(audio_file):
                    file_size = os.path.getsize(audio_file)
                    if file_size == 0:
                        logger.error(f"Prefetch generated empty file: {audio_file}")
                        return
                else:
                    logger.error(f"Prefetch failed to create file: {audio_file}")
                    return

                # Store in cache and evict old entries if needed
                self._prefetch_cache[text_hash] = audio_file
                self._evict_prefetch_cache()
                logger.debug(f"Prefetched: {text[:30]}...")

            except Exception as e:
                logger.debug(f"Prefetch failed: {e}")

        self._prefetch_thread = threading.Thread(target=_prefetch_thread, daemon=True)
        self._prefetch_thread.start()

    def clear_prefetch_cache(self):
        """Clear all prefetched audio files."""
        for audio_file in self._prefetch_cache.values():
            self._cleanup_file(audio_file)
        self._prefetch_cache.clear()

    def _evict_prefetch_cache(self):
        """Remove oldest entries if cache exceeds max size (LRU eviction)."""
        while len(self._prefetch_cache) > self._prefetch_cache_max_size:
            # dict maintains insertion order in Python 3.7+, pop first item
            oldest_key = next(iter(self._prefetch_cache))
            old_file = self._prefetch_cache.pop(oldest_key)
            self._cleanup_file(old_file)
            logger.debug(f"Evicted prefetch cache entry: {oldest_key[:8]}...")

    def _cleanup_file(self, filepath: str):
        """Clean up a specific audio file."""
        try:
            if filepath and os.path.exists(filepath):
                os.remove(filepath)
        except Exception:  # noqa: S110
            pass

    def _cleanup_audio(self):
        """Clean up current audio file."""
        with self._mixer_lock:
            try:
                if self._audio_file and os.path.exists(self._audio_file):
                    try:
                        self._pygame.mixer.music.stop()
                    except Exception:  # noqa: S110
                        pass
                    try:
                        self._pygame.mixer.music.unload()
                    except Exception:  # noqa: S110
                        pass
                    # Small delay to ensure file handle is released
                    self._pygame.time.wait(50)
                    try:
                        os.remove(self._audio_file)
                    except Exception:  # noqa: S110
                        pass
                    self._audio_file = None
            except Exception as e:
                logger.debug(f"Cleanup error (safe to ignore): {e}")

    @property
    def is_generating(self) -> bool:
        """Whether currently generating audio (before playback)."""
        return self._generating

    def stop(self) -> None:
        self._stop_requested = True
        self._generating = False
        self._speaking = False
        self._paused = False
        with self._mixer_lock:
            try:
                self._pygame.mixer.music.stop()
            except Exception:  # noqa: S110
                pass
        self._cleanup_audio()
        self.clear_prefetch_cache()

    def pause(self) -> None:
        if self._speaking and not self._paused:
            with self._mixer_lock:
                try:
                    self._pygame.mixer.music.pause()
                except Exception:  # noqa: S110
                    pass
            self._paused = True
            logger.debug("Paused")

    def resume(self) -> None:
        if self._paused:
            with self._mixer_lock:
                try:
                    self._pygame.mixer.music.unpause()
                except Exception:  # noqa: S110
                    pass
            self._paused = False
            logger.debug("Resumed")

    @property
    def is_speaking(self) -> bool:
        return self._speaking

    @property
    def is_paused(self) -> bool:
        return self._paused

    @property
    def rate(self) -> int:
        return self._rate

    @rate.setter
    def rate(self, value: int):
        self._rate = max(MIN_RATE, min(MAX_RATE, value))
        set_setting("rate", self._rate)

    @property
    def voice_name(self) -> str:
        return self._voice_name

    @voice_name.setter
    def voice_name(self, name: str):
        name = name.lower()
        if name in self.VOICES:
            self._voice_name = name
            set_setting("voice", self._voice_name)

    def get_available_voices(self) -> list[str]:
        return list(self.VOICES.keys())


# Singleton instance
_engine_instance: BaseTTSEngine | None = None


def get_engine() -> BaseTTSEngine:
    """Get the TTS engine based on settings."""
    global _engine_instance
    if _engine_instance is None:
        settings = load_settings()
        engine_type = settings.get("engine", "edge")  # Default to edge for better quality
        voice = settings.get("voice", "aria")

        # Auto-select engine based on voice if not specified
        if voice in EdgeTTSEngine.VOICES:
            engine_type = "edge"
        elif voice in Pyttsx3Engine.VOICES:
            engine_type = "pyttsx3"

        if engine_type == "edge":
            logger.info("Using Edge TTS (online, neural voices)")
            _engine_instance = EdgeTTSEngine()
        else:
            logger.info("Using pyttsx3 (offline, Windows SAPI)")
            _engine_instance = Pyttsx3Engine()

    return _engine_instance


def switch_engine(engine_type: str) -> BaseTTSEngine:
    """Switch to a different TTS engine."""
    global _engine_instance
    if _engine_instance:
        _engine_instance.stop()
    _engine_instance = None
    set_setting("engine", engine_type)
    return get_engine()


def get_all_voices() -> dict[str, list[str]]:
    """Get all available voices grouped by engine."""
    return {
        "edge": list(EdgeTTSEngine.VOICES.keys()),
        "pyttsx3": Pyttsx3Engine.VOICES.copy(),
    }


# Self-test
if __name__ == "__main__":
    import time

    print("Testing Edge TTS Engine...")
    engine = EdgeTTSEngine()

    print(f"Available voices: {engine.get_available_voices()}")
    print(f"Current voice: {engine.voice_name}")
    print(f"Current rate: {engine.rate} wpm")

    print("\nSpeaking test with Aria...")
    engine.speak("Hello! This is Aria, testing the Herald text to speech engine with edge T T S.")

    time.sleep(2)
    print("Pausing...")
    engine.pause()
    time.sleep(2)
    print("Resuming...")
    engine.resume()

    time.sleep(5)
    engine.stop()
    print("Done.")
