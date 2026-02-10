using Herald.Tts;

namespace Herald.Tests.Tts;

public class SapiRateTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(200, 0)]     // baseline: 200 WPM = rate 0
    [InlineData(230, 1)]     // one step up
    [InlineData(170, -1)]    // one step down
    [InlineData(500, 10)]    // max clamp
    [InlineData(1000, 10)]   // beyond max, clamped
    [InlineData(0, -6)]      // (0-200)/30 = -6, within clamp range
    [InlineData(50, -5)]     // low value
    public void WpmToSapiRate_ReturnsCorrectRate(int wpm, int expected)
    {
        Assert.Equal(expected, SapiEngine.WpmToSapiRate(wpm));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void WpmToSapiRate_IsAlwaysClamped()
    {
        for (int wpm = 0; wpm <= 2000; wpm += 50)
        {
            var rate = SapiEngine.WpmToSapiRate(wpm);
            Assert.InRange(rate, -10, 10);
        }
    }
}
