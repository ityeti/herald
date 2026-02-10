using Herald.Tts;

namespace Herald.Tests.Tts;

public class ErrorSpeakerTests
{
    [Fact]
    public void SpeakError_DoesNotThrow_WithValidMessage()
    {
        var ex = Record.Exception(() => ErrorSpeaker.SpeakError("Test error message"));
        Assert.Null(ex);
    }

    [Fact]
    public void SpeakError_DoesNotThrow_WithNullMessage()
    {
        var ex = Record.Exception(() => ErrorSpeaker.SpeakError(null));
        Assert.Null(ex);
    }

    [Fact]
    public void SpeakError_DoesNotThrow_WithEmptyMessage()
    {
        var ex = Record.Exception(() => ErrorSpeaker.SpeakError(""));
        Assert.Null(ex);
    }

    [Fact]
    public void SpeakError_DoesNotThrow_WithWhitespaceMessage()
    {
        var ex = Record.Exception(() => ErrorSpeaker.SpeakError("   "));
        Assert.Null(ex);
    }
}
