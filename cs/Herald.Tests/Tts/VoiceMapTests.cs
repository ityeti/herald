using Herald.Tts;

namespace Herald.Tests.Tts;

public class VoiceMapTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void EdgeVoiceMap_ContainsExpectedVoices()
    {
        var map = EdgeTtsProtocol.VoiceMap;

        Assert.True(map.ContainsKey("aria"));
        Assert.True(map.ContainsKey("guy"));
        Assert.True(map.ContainsKey("jenny"));
        Assert.True(map.ContainsKey("christopher"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EdgeVoiceMap_AllValuesAreNeuralVoiceIds()
    {
        foreach (var (_, voiceId) in EdgeTtsProtocol.VoiceMap)
        {
            Assert.StartsWith("en-US-", voiceId);
            Assert.EndsWith("Neural", voiceId);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EdgeVoiceMap_IsCaseInsensitive()
    {
        var map = EdgeTtsProtocol.VoiceMap;
        Assert.Equal(map["aria"], map["ARIA"]);
        Assert.Equal(map["guy"], map["Guy"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EdgeEngine_GetAvailableVoices_ReturnsAllMappedVoices()
    {
        using var engine = new EdgeTtsEngine();
        var voices = engine.GetAvailableVoices();

        Assert.Contains("aria", voices);
        Assert.Contains("guy", voices);
        Assert.Contains("jenny", voices);
        Assert.Contains("christopher", voices);
        Assert.Equal(4, voices.Count);
    }
}
