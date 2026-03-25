"""
Synthetic tests for Herald TTS generation.

Tests that TTS engines produce valid audio files.
These tests require network access for Edge TTS.
"""

import asyncio
import pytest


@pytest.mark.synthetic
@pytest.mark.slow
class TestEdgeTTSGeneration:
    """Test Edge TTS audio generation."""

    @pytest.fixture
    def edge_engine(self, temp_audio_dir):
        """Create an EdgeTTSEngine with custom temp directory."""
        from tts_engine import EdgeTTSEngine

        engine = EdgeTTSEngine()
        # Override temp directory
        engine._temp_dir = temp_audio_dir
        return engine

    def test_engine_initialization(self, edge_engine):
        """Edge TTS engine should initialize without errors."""
        assert edge_engine is not None
        assert edge_engine.voice_name in ["aria", "guy", "jenny", "christopher"]

    def test_available_voices(self, edge_engine):
        """Should return list of available voices."""
        voices = edge_engine.get_available_voices()
        assert isinstance(voices, list)
        assert len(voices) > 0
        assert "aria" in voices

    def test_rate_bounds(self, edge_engine):
        """Rate should be clamped to valid range."""
        from config import MIN_RATE, MAX_RATE

        # Test minimum
        edge_engine.rate = 1
        assert edge_engine.rate >= MIN_RATE

        # Test maximum
        edge_engine.rate = 10000
        assert edge_engine.rate <= MAX_RATE

    @pytest.mark.slow
    def test_generate_audio_file(self, edge_engine, temp_audio_dir):
        """Edge TTS should generate a valid MP3 file."""
        import edge_tts

        test_text = "Hello, this is a test."
        voice_id = "en-US-AriaNeural"
        output_file = temp_audio_dir / "test_output.mp3"

        # Generate audio directly (bypassing the async speak method)
        async def generate():
            communicate = edge_tts.Communicate(test_text, voice_id)
            await communicate.save(str(output_file))

        asyncio.run(generate())

        # Verify file exists and has content
        assert output_file.exists(), "Audio file was not created"
        assert output_file.stat().st_size > 0, "Audio file is empty"

        # Basic MP3 header check (ID3 or sync word)
        with open(output_file, "rb") as f:
            header = f.read(3)
            # MP3 files start with ID3 tag or sync word (0xFF 0xFB/0xFA/0xF3)
            is_id3 = header == b"ID3"
            is_sync = header[0] == 0xFF and (header[1] & 0xE0) == 0xE0
            assert is_id3 or is_sync, "File does not appear to be valid MP3"

    @pytest.mark.slow
    def test_generate_multiple_voices(self, temp_audio_dir):
        """Test generating audio with different voices."""
        import edge_tts

        test_text = "Testing voice."
        voices = ["en-US-AriaNeural", "en-US-GuyNeural"]

        for voice_id in voices:
            output_file = temp_audio_dir / f"test_{voice_id}.mp3"

            async def generate(vid=voice_id, out=output_file):
                communicate = edge_tts.Communicate(test_text, vid)
                await communicate.save(str(out))

            asyncio.run(generate())

            assert output_file.exists(), f"Failed to generate audio for {voice_id}"
            assert output_file.stat().st_size > 1000, f"Audio too small for {voice_id}"


@pytest.mark.synthetic
@pytest.mark.slow
class TestPyttsx3Generation:
    """Test offline TTS (pyttsx3) generation."""

    @pytest.fixture
    def pyttsx3_engine(self):
        """Create a Pyttsx3Engine instance."""
        from tts_engine import Pyttsx3Engine

        return Pyttsx3Engine()

    def test_engine_initialization(self, pyttsx3_engine):
        """Pyttsx3 engine should initialize without errors."""
        assert pyttsx3_engine is not None

    def test_available_voices(self, pyttsx3_engine):
        """Should return list of available offline voices."""
        voices = pyttsx3_engine.get_available_voices()
        assert isinstance(voices, list)
        assert len(voices) > 0
        # Windows SAPI voices
        assert "zira" in voices or "david" in voices

    def test_rate_property(self, pyttsx3_engine):
        """Rate property should work correctly."""

        original_rate = pyttsx3_engine.rate

        pyttsx3_engine.rate = 400
        assert pyttsx3_engine.rate == 400

        # Restore
        pyttsx3_engine.rate = original_rate


@pytest.mark.synthetic
@pytest.mark.slow
class TestTTSEngineFactory:
    """Test TTS engine factory functions."""

    def test_get_engine_returns_instance(self):
        """get_engine should return a TTS engine instance."""
        from tts_engine import get_engine, BaseTTSEngine

        engine = get_engine()
        assert isinstance(engine, BaseTTSEngine)

    def test_get_all_voices(self):
        """get_all_voices should return dict of all voices."""
        from tts_engine import get_all_voices

        all_voices = get_all_voices()

        assert "edge" in all_voices
        assert "pyttsx3" in all_voices
        assert len(all_voices["edge"]) > 0
        assert len(all_voices["pyttsx3"]) > 0
