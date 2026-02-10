using System.Speech.Synthesis;

namespace Herald.Tests.Tts;

public class SapiIntegrationTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    [Fact]
    [Trait("Category", "Integration")]
    public void SapiVoices_AtLeastOneAvailable()
    {
        using var synth = new SpeechSynthesizer();
        var voices = synth.GetInstalledVoices();
        var enabled = voices.Where(v => v.Enabled).ToList();

        Assert.True(enabled.Count > 0, "At least one SAPI voice should be installed");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SynthesizeToWavFile_ProducesNonEmptyFile()
    {
        var wavPath = Path.Combine(Path.GetTempPath(), $"herald_sapi_test_{Guid.NewGuid():N}.wav");
        _tempFiles.Add(wavPath);

        using var synth = new SpeechSynthesizer();
        synth.SetOutputToWaveFile(wavPath);
        synth.Speak("Hello from SAPI integration test.");
        synth.SetOutputToNull(); // flush

        Assert.True(File.Exists(wavPath), "WAV file should exist");
        var info = new FileInfo(wavPath);
        Assert.True(info.Length > 44, $"WAV file should contain audio data, got {info.Length} bytes");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SynthesizeOnStaThread_Succeeds()
    {
        var wavPath = Path.Combine(Path.GetTempPath(), $"herald_sapi_sta_{Guid.NewGuid():N}.wav");
        _tempFiles.Add(wavPath);

        Exception? threadEx = null;
        var done = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            try
            {
                using var synth = new SpeechSynthesizer();
                synth.SetOutputToWaveFile(wavPath);
                synth.Speak("Hello from SAPI STA thread test.");
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
        Assert.True(new FileInfo(wavPath).Length > 44, "WAV should have audio data");
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }
}
