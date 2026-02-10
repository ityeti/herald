using Herald.Tts;

namespace Herald.Tests.Audio;

public class EdgeTtsAudioTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    [Fact]
    [Trait("Category", "Audio")]
    public async Task EdgeTts_DefaultVoice_ProducesAudibleAudio()
    {
        using var engine = new EdgeTtsEngine("aria", 300);
        var file = await engine.SynthesizeToFileAsync("This is an audible content test for Edge TTS.");

        Assert.NotNull(file);
        _tempFiles.Add(file);

        Assert.True(AudioVerifier.Mp3HasAudibleContent(file),
            "EdgeTTS MP3 should contain audible content (non-silent)");
    }

    [Fact]
    [Trait("Category", "Audio")]
    public async Task EdgeTts_DefaultVoice_HasReasonableDuration()
    {
        using var engine = new EdgeTtsEngine("aria", 300);
        var file = await engine.SynthesizeToFileAsync("Testing audio duration for a short sentence.");

        Assert.NotNull(file);
        _tempFiles.Add(file);

        var duration = AudioVerifier.GetDuration(file);
        Assert.True(duration.TotalMilliseconds > 500,
            $"Audio should be at least 500ms, got {duration.TotalMilliseconds}ms");
        Assert.True(duration.TotalSeconds < 30,
            $"Audio should be less than 30s, got {duration.TotalSeconds}s");
    }

    [Theory]
    [Trait("Category", "Audio")]
    [InlineData("aria")]
    [InlineData("guy")]
    public async Task EdgeTts_EachVoice_ProducesAudibleAudio(string voice)
    {
        using var engine = new EdgeTtsEngine(voice, 300);
        var file = await engine.SynthesizeToFileAsync($"Audio test for {voice} voice.");

        Assert.NotNull(file);
        _tempFiles.Add(file);

        Assert.True(AudioVerifier.Mp3HasAudibleContent(file),
            $"EdgeTTS voice '{voice}' should produce audible audio");
    }

    [Theory]
    [Trait("Category", "Audio")]
    [InlineData(200)]   // slow
    [InlineData(600)]   // fast
    public async Task EdgeTts_DifferentRates_ProduceAudibleAudio(int rate)
    {
        using var engine = new EdgeTtsEngine("aria", rate);
        var file = await engine.SynthesizeToFileAsync("Testing rate variation.");

        Assert.NotNull(file);
        _tempFiles.Add(file);

        Assert.True(AudioVerifier.Mp3HasAudibleContent(file),
            $"EdgeTTS at rate {rate} should produce audible audio");
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }
}
