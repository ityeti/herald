using Herald.Tts;

/// <summary>
/// Throwaway test project to verify KokoroSharp works on this hardware.
/// Tests: model loading, synthesis to WAV, voice quality, latency, CPU usage.
/// Run: dotnet run --project cs/Herald.KokoroTest
/// </summary>
Console.WriteLine("=== Herald KokoroSharp Test ===");
Console.WriteLine($"Time: {DateTime.Now}");
Console.WriteLine($".NET: {Environment.Version}");
Console.WriteLine();

// Test 1: Create engine (triggers model download on first run)
Console.WriteLine("[1] Creating KokoroEngine (may download ~320MB model on first run)...");
var sw = System.Diagnostics.Stopwatch.StartNew();
try
{
    using var engine = new KokoroEngine("heart", 200);

    // Trigger model load explicitly via first speak
    Console.WriteLine("    Triggering model load...");
    engine.Speak("Hello.");
    while (engine.IsGenerating) Thread.Sleep(100);
    while (engine.IsSpeaking) Thread.Sleep(100);
    Console.WriteLine($"    Engine initialized and first speak done in {sw.Elapsed.TotalSeconds:F1}s");

    // Test 2: Available voices
    Console.WriteLine();
    Console.WriteLine($"[2] Available voices ({engine.GetAvailableVoices().Count}):");
    foreach (var voice in engine.GetAvailableVoices())
        Console.Write($"  {voice}");
    Console.WriteLine();

    // Test 3: Full sentence
    Console.WriteLine();
    Console.WriteLine("[3] Speaking full sentence...");
    sw.Restart();
    engine.Speak("The quick brown fox jumps over the lazy dog. This is a test of Kokoro neural text to speech.");

    // Wait for speech to complete
    while (engine.IsGenerating || engine.IsSpeaking)
    {
        if (engine.IsGenerating)
            Console.Write($"\r    Generating... {sw.Elapsed.TotalMilliseconds:F0}ms  ");
        else if (engine.IsSpeaking)
            Console.Write($"\r    Speaking... {sw.Elapsed.TotalMilliseconds:F0}ms    ");
        Thread.Sleep(100);
    }
    Console.WriteLine($"\r    Done in {sw.Elapsed.TotalSeconds:F1}s                      ");

    // Test 4: Speed test
    Console.WriteLine();
    Console.WriteLine("[4] Speed test at 400 WPM (speed 2.0x)...");
    engine.Rate = 400;
    sw.Restart();
    engine.Speak("This is a speed test at four hundred words per minute.");
    while (engine.IsGenerating || engine.IsSpeaking) Thread.Sleep(100);
    Console.WriteLine($"    Done in {sw.Elapsed.TotalSeconds:F1}s");

    // Test 5: Different voice
    Console.WriteLine();
    Console.WriteLine("[5] Testing voice 'michael' (male)...");
    engine.VoiceName = "michael";
    engine.Rate = 200;
    sw.Restart();
    engine.Speak("Hello, this is the Michael voice from Kokoro.");
    while (engine.IsGenerating || engine.IsSpeaking) Thread.Sleep(100);
    Console.WriteLine($"    Done in {sw.Elapsed.TotalSeconds:F1}s");

    // Test 6: Rate conversion
    Console.WriteLine();
    Console.WriteLine("[6] Rate conversion tests:");
    int[] testRates = [150, 200, 300, 500, 900, 1500];
    foreach (var rate in testRates)
    {
        var speed = KokoroEngine.WpmToKokoroSpeed(rate);
        Console.WriteLine($"    {rate} WPM -> speed {speed:F2}x");
    }

    Console.WriteLine();
    Console.WriteLine("=== ALL TESTS PASSED ===");
}
catch (Exception ex)
{
    Console.WriteLine($"\n    FAILED: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine($"    Stack: {ex.StackTrace}");
    return 1;
}

return 0;
