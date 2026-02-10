using Herald.Tts;

namespace Herald.Tests.Tts;

public class KokoroEngineTests
{
    [Theory]
    [InlineData(100, 0.5f)]   // Clamped to min
    [InlineData(150, 0.75f)]
    [InlineData(200, 1.0f)]   // Normal speech
    [InlineData(260, 1.3f)]   // Kokoro recommended max
    [InlineData(400, 2.0f)]
    [InlineData(600, 3.0f)]   // Clamped to max
    [InlineData(1500, 3.0f)]  // Way above max, still clamped
    public void WpmToKokoroSpeed_ReturnsCorrectSpeed(int wpm, float expected)
    {
        Assert.Equal(expected, KokoroEngine.WpmToKokoroSpeed(wpm));
    }

    [Fact]
    public void WpmToKokoroSpeed_IsAlwaysClamped()
    {
        for (int wpm = 0; wpm <= 2000; wpm += 50)
        {
            var speed = KokoroEngine.WpmToKokoroSpeed(wpm);
            Assert.InRange(speed, 0.5f, 3.0f);
        }
    }

    [Theory]
    [InlineData("heart", "af_heart")]
    [InlineData("bella", "af_bella")]
    [InlineData("michael", "am_michael")]
    [InlineData("emma", "bf_emma")]
    [InlineData("daniel", "bm_daniel")]
    [InlineData("HEART", "af_heart")]    // Case insensitive
    [InlineData("Heart", "af_heart")]    // Case insensitive
    [InlineData("unknown", "af_heart")]  // Falls back to heart
    public void ResolveVoice_MapsCorrectly(string shortName, string expected)
    {
        Assert.Equal(expected, KokoroEngine.ResolveVoice(shortName));
    }

    [Fact]
    public void VoiceMap_HasExpectedVoiceCount()
    {
        // 11 American Female + 8 American Male + 4 British Female + 4 British Male = 27
        Assert.Equal(27, KokoroEngine.VoiceMap.Count);
    }

    [Fact]
    public void VoiceMap_AllValuesFollowNamingConvention()
    {
        foreach (var (shortName, voiceId) in KokoroEngine.VoiceMap)
        {
            // Voice IDs follow pattern: {lang}{gender}_{name}
            Assert.Matches(@"^[abefhijpz][fm]_\w+$", voiceId);
        }
    }

    [Fact]
    public void VoiceMap_IsCaseInsensitive()
    {
        Assert.Equal(
            KokoroEngine.VoiceMap["heart"],
            KokoroEngine.VoiceMap["HEART"]);
        Assert.Equal(
            KokoroEngine.VoiceMap["michael"],
            KokoroEngine.VoiceMap["Michael"]);
    }

    [Fact]
    public void GetAvailableVoices_ReturnsNonEmptyList()
    {
        using var engine = new KokoroEngine("heart", 200);
        var voices = engine.GetAvailableVoices();
        Assert.NotEmpty(voices);
        Assert.Contains("heart", voices);
        Assert.Contains("michael", voices);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        using var engine = new KokoroEngine("bella", 300);
        Assert.Equal("bella", engine.VoiceName);
        Assert.Equal(300, engine.Rate);
        Assert.False(engine.IsSpeaking);
        Assert.False(engine.IsPaused);
        Assert.False(engine.IsGenerating);
    }

    [Fact]
    public void Rate_CanBeChanged()
    {
        using var engine = new KokoroEngine("heart", 200);
        engine.Rate = 400;
        Assert.Equal(400, engine.Rate);
    }

    [Fact]
    public void PcmConstants_MatchKokoroSharpOutputFormat()
    {
        // KokoroSharp produces raw PCM: 16-bit signed, 24kHz, mono
        Assert.Equal(24000, KokoroEngine.PcmSampleRate);
        Assert.Equal(16, KokoroEngine.PcmBitsPerSample);
        Assert.Equal(1, KokoroEngine.PcmChannels);
    }

    [Theory]
    [InlineData(600)]
    [InlineData(900)]
    [InlineData(1500)]
    public void WpmToKokoroSpeed_HighWpmAllClampToSameSpeed(int wpm)
    {
        // All WPM values >= 600 produce 3.0x (the Kokoro max), documenting the plateau
        Assert.Equal(3.0f, KokoroEngine.WpmToKokoroSpeed(wpm));
    }
}
