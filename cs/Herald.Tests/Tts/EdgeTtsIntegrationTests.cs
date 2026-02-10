using Herald.Tts;
using NAudio.Wave;

namespace Herald.Tests.Tts;

public class EdgeTtsIntegrationTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SynthesizeToFile_ProducesNonEmptyMp3()
    {
        using var engine = new EdgeTtsEngine("aria", 300);
        var file = await engine.SynthesizeToFileAsync("Hello from integration test.");

        Assert.NotNull(file);
        _tempFiles.Add(file);
        Assert.True(File.Exists(file), "Audio file should exist");

        var info = new FileInfo(file);
        Assert.True(info.Length > 100, $"Audio file should be non-trivial size, got {info.Length} bytes");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SynthesizeToFile_IsDecodableByNAudio()
    {
        using var engine = new EdgeTtsEngine("aria", 300);
        var file = await engine.SynthesizeToFileAsync("Testing NAudio decode capability.");

        Assert.NotNull(file);
        _tempFiles.Add(file);

        // Verify NAudio can open and read the MP3
        using var reader = new Mp3FileReader(file);
        Assert.True(reader.TotalTime.TotalMilliseconds > 100,
            $"Audio should be longer than 100ms, got {reader.TotalTime.TotalMilliseconds}ms");
        Assert.True(reader.WaveFormat.SampleRate > 0, "Sample rate should be positive");
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("aria")]
    [InlineData("guy")]
    public async Task SynthesizeToFile_WorksWithDifferentVoices(string voice)
    {
        using var engine = new EdgeTtsEngine(voice, 300);
        var file = await engine.SynthesizeToFileAsync($"Testing {voice} voice.");

        Assert.NotNull(file);
        _tempFiles.Add(file);
        Assert.True(new FileInfo(file).Length > 100);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }
}
