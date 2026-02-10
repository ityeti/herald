using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Serilog;

namespace Herald.Ocr;

/// <summary>
/// Windows Runtime OCR via Windows.Media.Ocr.
/// Uses the COM-based WinRT API directly to avoid WinRT projection package dependencies.
/// Falls back to a simple Tesseract-free approach using the built-in Windows OCR engine.
/// </summary>
public static class WinOcr
{
    /// <summary>
    /// Run OCR on a Bitmap image using Windows.Media.Ocr via PowerShell interop.
    /// This avoids the complexity of WinRT COM interop in .NET 8 while still using
    /// the same Windows OCR engine.
    /// </summary>
    public static async Task<string?> RecognizeAsync(Bitmap image, int timeoutMs = 10000)
    {
        // Save image to temp file as PNG
        var tempFile = Path.Combine(Path.GetTempPath(), $"herald_ocr_{Guid.NewGuid():N}.png");
        try
        {
            image.Save(tempFile, ImageFormat.Png);

            // Use PowerShell to invoke WinRT OCR (cleanest approach without WinRT packages)
            var script = $@"
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType=WindowsRuntime]
$null = [Windows.Storage.StorageFile, Windows.Foundation, ContentType=WindowsRuntime]

function Await($WinRtTask, $ResultType) {{
    $asTask = [System.WindowsRuntimeSystemExtensions].GetMethod('AsTask', [Type[]]@($WinRtTask.GetType()))
    if (-not $asTask) {{
        $asTaskGeneric = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {{
            $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.IsGenericMethod
        }} | Select-Object -First 1
        $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    }}
    $task = $asTask.Invoke($null, @($WinRtTask))
    $task.Wait()
    return $task.Result
}}

$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync('{tempFile.Replace("'", "''")}')) ([Windows.Storage.StorageFile])
$stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
$decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
$bitmap = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])

$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage([Windows.Globalization.Language]::new('en'))
if ($engine) {{
    $result = Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
    Write-Output $result.Text
}}

$stream.Dispose()
";

            using var cts = new CancellationTokenSource(timeoutMs);
            var result = await RunPowerShellAsync(script, cts.Token);
            var text = result?.Trim();

            if (!string.IsNullOrEmpty(text))
            {
                Log.Debug("OCR extracted {Length} characters", text.Length);
                return text;
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            Log.Warning("OCR timed out after {Timeout}ms", timeoutMs);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OCR failed");
            return null;
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    /// <summary>
    /// Grab an image from the clipboard. Returns null if no image present.
    /// Must be called from STA thread.
    /// </summary>
    public static Bitmap? GetClipboardImage()
    {
        try
        {
            if (Clipboard.ContainsImage())
            {
                var img = Clipboard.GetImage();
                if (img != null)
                    return new Bitmap(img);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to get clipboard image");
        }
        return null;
    }

    /// <summary>Capture a screen region as a bitmap.</summary>
    public static Bitmap? CaptureRegion(Rectangle region)
    {
        try
        {
            var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(region.Left, region.Top, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Screen capture failed for region {Region}", region);
            return null;
        }
    }

    private static async Task<string?> RunPowerShellAsync(string script, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command -",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc == null) return null;

        await proc.StandardInput.WriteAsync(script);
        proc.StandardInput.Close();

        var output = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(ct);
            Log.Debug("PowerShell OCR stderr: {Error}", err);
        }

        return output;
    }
}
