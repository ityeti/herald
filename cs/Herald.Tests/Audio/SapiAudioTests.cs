using System.Speech.Synthesis;

namespace Herald.Tests.Audio;

public class SapiAudioTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    [Fact]
    [Trait("Category", "Audio")]
    public void Sapi_DefaultVoice_ProducesAudibleAudio()
    {
        var wavPath = TempWav();

        using var synth = new SpeechSynthesizer();
        synth.SetOutputToWaveFile(wavPath);
        synth.Speak("This is an audible content test for SAPI.");
        synth.SetOutputToNull();

        Assert.True(AudioVerifier.HasAudibleContent(wavPath),
            "SAPI WAV should contain audible content (non-silent)");
    }

    [Fact]
    [Trait("Category", "Audio")]
    public void Sapi_DefaultVoice_HasReasonableDuration()
    {
        var wavPath = TempWav();

        using var synth = new SpeechSynthesizer();
        synth.SetOutputToWaveFile(wavPath);
        synth.Speak("Testing audio duration for a short sentence.");
        synth.SetOutputToNull();

        var duration = AudioVerifier.GetDuration(wavPath);
        Assert.True(duration.TotalMilliseconds > 500,
            $"Audio should be at least 500ms, got {duration.TotalMilliseconds}ms");
        Assert.True(duration.TotalSeconds < 30,
            $"Audio should be less than 30s, got {duration.TotalSeconds}s");
    }

    [Fact]
    [Trait("Category", "Audio")]
    public void Sapi_StaThread_ProducesAudibleAudio()
    {
        var wavPath = TempWav();
        Exception? threadEx = null;
        var done = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            try
            {
                using var synth = new SpeechSynthesizer();
                synth.SetOutputToWaveFile(wavPath);
                synth.Speak("Testing SAPI on STA thread for audible output.");
                synth.SetOutputToNull();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
            finally
            {
                done.Set();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        Assert.True(done.Wait(TimeSpan.FromSeconds(15)), "STA thread should complete within 15s");
        Assert.Null(threadEx);
        Assert.True(AudioVerifier.HasAudibleContent(wavPath),
            "SAPI on STA thread should produce audible audio");
    }

    private string TempWav()
    {
        var path = Path.Combine(Path.GetTempPath(), $"herald_sapi_audio_{Guid.NewGuid():N}.wav");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }
}
