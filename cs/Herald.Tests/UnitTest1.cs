using Herald.Config;
using Herald.Text;

namespace Herald.Tests;

public class SettingsTests
{
    [Fact]
    public void DefaultSettings_HaveExpectedValues()
    {
        var settings = new Settings();
        Assert.Equal("edge", settings.Engine);
        Assert.Equal("aria", settings.Voice);
        Assert.Equal(900, settings.Rate);
        Assert.Equal("ctrl+shift+s", settings.HotkeySpeak);
        Assert.True(settings.AutoCopy);
        Assert.True(settings.FilterCode);
    }

    [Fact]
    public void GetHotkey_ReturnsCorrectValue()
    {
        var settings = new Settings { HotkeySpeak = "ctrl+alt+x" };
        Assert.Equal("ctrl+alt+x", settings.GetHotkey("hotkey_speak"));
    }

    [Fact]
    public void SetHotkey_UpdatesValue()
    {
        var settings = new Settings();
        settings.SetHotkey("hotkey_pause", "ctrl+p");
        Assert.Equal("ctrl+p", settings.HotkeyPause);
    }
}

public class TextFilterTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("12345", true)]
    [InlineData("───────", true)]
    [InlineData("Hello world", false)]
    public void IsUnspeakable_IdentifiesCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, TextFilter.IsUnspeakable(input));
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("import os", true)]
    [InlineData("git clone repo", true)]
    [InlineData("C:\\Users\\test", true)]
    [InlineData("Hello, this is a normal sentence.", false)]
    public void IsCodeLike_IdentifiesCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, TextFilter.IsCodeLike(input));
    }

    [Fact]
    public void NormalizeForSpeech_RemovesMarkdown()
    {
        var result = TextFilter.NormalizeForSpeech("This is **bold** text");
        Assert.Equal("This is bold text", result);
    }

    [Fact]
    public void NormalizeForSpeech_ConvertsCamelCase()
    {
        var result = TextFilter.NormalizeForSpeech("the methodName works");
        Assert.Equal("the method Name works", result);
    }

    [Fact]
    public void NormalizeForSpeech_ConvertsSnakeCase()
    {
        var result = TextFilter.NormalizeForSpeech("the filter_code flag");
        Assert.Equal("the filter code flag", result);
    }

    [Fact]
    public void FilterAndSplit_RemovesCodeLines()
    {
        var text = "Hello world\nimport os\nGoodbye world";
        var result = TextFilter.FilterAndSplit(text, filterCode: true, normalizeText: false);
        Assert.Equal(2, result.Count);
        Assert.Equal("Hello world", result[0]);
        Assert.Equal("Goodbye world", result[1]);
    }
}
