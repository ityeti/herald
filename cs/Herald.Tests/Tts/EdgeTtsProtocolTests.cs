using Herald.Tts;

namespace Herald.Tests.Tts;

public class EdgeTtsProtocolTests
{
    // --- WpmToEdgeRate ---

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(300, "+0%")]     // baseline
    [InlineData(600, "+100%")]   // double speed
    [InlineData(900, "+200%")]   // max clamp
    [InlineData(1500, "+200%")]  // beyond max, clamped
    [InlineData(150, "-50%")]    // min clamp
    [InlineData(0, "-50%")]      // below min, clamped
    [InlineData(450, "+50%")]    // midpoint
    public void WpmToEdgeRate_ReturnsCorrectPercent(int wpm, string expected)
    {
        Assert.Equal(expected, EdgeTtsProtocol.WpmToEdgeRate(wpm));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void WpmToEdgeRate_PositiveRatesHavePlusPrefix()
    {
        var result = EdgeTtsProtocol.WpmToEdgeRate(400);
        Assert.StartsWith("+", result);
        Assert.EndsWith("%", result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void WpmToEdgeRate_NegativeRatesHaveMinusPrefix()
    {
        var result = EdgeTtsProtocol.WpmToEdgeRate(200);
        Assert.StartsWith("-", result);
        Assert.EndsWith("%", result);
    }

    // --- BuildSsml ---

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildSsml_ContainsVoiceAndRate()
    {
        var ssml = EdgeTtsProtocol.BuildSsml("Hello world", "en-US-AriaNeural", "+0%");

        Assert.Contains("en-US-AriaNeural", ssml);
        Assert.Contains("+0%", ssml);
        Assert.Contains("Hello world", ssml);
        Assert.StartsWith("<speak", ssml);
        Assert.EndsWith("</speak>", ssml);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Tom & Jerry", "&amp;")]
    [InlineData("a < b", "&lt;")]
    [InlineData("a > b", "&gt;")]
    [InlineData("say \"hello\"", "&quot;")]
    [InlineData("it's fine", "&apos;")]
    public void BuildSsml_EscapesXmlSpecialChars(string input, string expectedEscape)
    {
        var ssml = EdgeTtsProtocol.BuildSsml(input, "en-US-AriaNeural", "+0%");
        Assert.Contains(expectedEscape, ssml);
        // Ensure raw special chars are not present (except in XML tags)
        Assert.DoesNotContain($">{input}<", ssml);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildSsml_EmptyTextProducesValidSsml()
    {
        var ssml = EdgeTtsProtocol.BuildSsml("", "en-US-AriaNeural", "+0%");
        Assert.Contains("<prosody rate='+0%'></prosody>", ssml);
    }

    // --- GenerateSecMsGec ---

    [Fact]
    [Trait("Category", "Unit")]
    public void GenerateSecMsGec_Returns64CharUppercaseHex()
    {
        var token = EdgeTtsProtocol.GenerateSecMsGec();

        Assert.Equal(64, token.Length);
        Assert.Matches("^[0-9A-F]+$", token);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GenerateSecMsGec_IsStableWithin5Minutes()
    {
        // Two calls within the same 5-min window should produce the same token
        var token1 = EdgeTtsProtocol.GenerateSecMsGec();
        var token2 = EdgeTtsProtocol.GenerateSecMsGec();
        Assert.Equal(token1, token2);
    }

    // --- GenerateMuid ---

    [Fact]
    [Trait("Category", "Unit")]
    public void GenerateMuid_Returns32CharUppercaseHex()
    {
        var muid = EdgeTtsProtocol.GenerateMuid();

        Assert.Equal(32, muid.Length);
        Assert.Matches("^[0-9A-F]+$", muid);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GenerateMuid_IsRandom()
    {
        var muid1 = EdgeTtsProtocol.GenerateMuid();
        var muid2 = EdgeTtsProtocol.GenerateMuid();
        Assert.NotEqual(muid1, muid2);
    }

    // --- TextHash ---

    [Fact]
    [Trait("Category", "Unit")]
    public void TextHash_Returns12CharLowercaseHex()
    {
        var hash = EdgeTtsProtocol.TextHash("Hello world");

        Assert.Equal(12, hash.Length);
        Assert.Matches("^[0-9a-f]+$", hash);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TextHash_IsDeterministic()
    {
        var hash1 = EdgeTtsProtocol.TextHash("test input");
        var hash2 = EdgeTtsProtocol.TextHash("test input");
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TextHash_DiffersForDifferentText()
    {
        var hash1 = EdgeTtsProtocol.TextHash("Hello");
        var hash2 = EdgeTtsProtocol.TextHash("World");
        Assert.NotEqual(hash1, hash2);
    }

    // --- ResolveVoice ---

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("aria", "en-US-AriaNeural")]
    [InlineData("guy", "en-US-GuyNeural")]
    [InlineData("jenny", "en-US-JennyNeural")]
    [InlineData("christopher", "en-US-ChristopherNeural")]
    [InlineData("ARIA", "en-US-AriaNeural")]  // case insensitive
    public void ResolveVoice_MapsKnownVoices(string shortName, string expected)
    {
        Assert.Equal(expected, EdgeTtsProtocol.ResolveVoice(shortName));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveVoice_FallsBackToAria()
    {
        Assert.Equal("en-US-AriaNeural", EdgeTtsProtocol.ResolveVoice("unknown_voice"));
    }

    // --- TrustedClientToken ---

    [Fact]
    [Trait("Category", "Unit")]
    public void TrustedClientToken_Is32CharHex()
    {
        Assert.Equal(32, EdgeTtsProtocol.TrustedClientToken.Length);
        Assert.Matches("^[0-9A-F]+$", EdgeTtsProtocol.TrustedClientToken);
    }
}
