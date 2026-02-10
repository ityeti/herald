using NAudio.Wave;

namespace Herald.Tests.Audio;

/// <summary>
/// Shared utility for verifying audio files contain audible content.
/// Decodes to PCM and checks peak amplitude against a threshold.
/// </summary>
internal static class AudioVerifier
{
    /// <summary>
    /// Check if a WAV file contains audible content (peak amplitude above threshold).
    /// </summary>
    internal static bool HasAudibleContent(string wavPath, float threshold = 0.01f)
    {
        using var reader = new WaveFileReader(wavPath);
        return CheckPeakAmplitude(reader, threshold);
    }

    /// <summary>
    /// Check if an MP3 file contains audible content (peak amplitude above threshold).
    /// </summary>
    internal static bool Mp3HasAudibleContent(string mp3Path, float threshold = 0.01f)
    {
        using var reader = new Mp3FileReader(mp3Path);
        return CheckPeakAmplitude(reader, threshold);
    }

    /// <summary>
    /// Get the duration of an audio file (WAV or MP3).
    /// </summary>
    internal static TimeSpan GetDuration(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".mp3")
        {
            using var reader = new Mp3FileReader(filePath);
            return reader.TotalTime;
        }
        else
        {
            using var reader = new WaveFileReader(filePath);
            return reader.TotalTime;
        }
    }

    private static bool CheckPeakAmplitude(WaveStream reader, float threshold)
    {
        // Convert to 32-bit float for easy amplitude analysis
        var sampleProvider = reader.ToSampleProvider();
        var buffer = new float[4096];
        float peak = 0;

        int samplesRead;
        while ((samplesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < samplesRead; i++)
            {
                var abs = Math.Abs(buffer[i]);
                if (abs > peak) peak = abs;
            }

            // Early exit if we already found audible content
            if (peak > threshold) return true;
        }

        return peak > threshold;
    }
}
