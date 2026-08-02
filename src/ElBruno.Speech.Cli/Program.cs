// elbrunospeech — speech pipeline CLI tool
// Commands: devices | transcribe <path> | vad <path> | talk <text> | serve [--port N] | bench

using Microsoft.Extensions.AI;
using ElBruno.Speech;
using ElBruno.Speech.Audio;
using ElBruno.Speech.NAudio;
using ElBruno.Speech.Vad.Silero;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintHelp();
    return 0;
}

var command = args[0].ToLowerInvariant();
var rest = args.Skip(1).ToArray();

return command switch
{
    "devices"    => RunDevices(),
    "transcribe" => await RunTranscribeAsync(rest),
    "vad"        => await RunVadAsync(rest),
    "talk"       => await RunTalkAsync(rest),
    "serve"      => await RunServeAsync(rest),
    "bench"      => RunBench(),
    _            => PrintUnknown(command),
};

// ── devices ──────────────────────────────────────────────────────────────────

static int RunDevices()
{
    Console.WriteLine("=== Input Devices ===");
    var inputs = AudioDeviceEnumerator.GetInputDevices();
    if (inputs.Count == 0) Console.WriteLine("  (none found)");
    foreach (var d in inputs) Console.WriteLine($"  [{d.DeviceNumber}] {d.Name}");

    Console.WriteLine("=== Output Devices ===");
    var outputs = AudioDeviceEnumerator.GetOutputDevices();
    if (outputs.Count == 0) Console.WriteLine("  (none found)");
    foreach (var d in outputs) Console.WriteLine($"  [{d.DeviceNumber}] {d.Name}");
    return 0;
}

// ── transcribe ───────────────────────────────────────────────────────────────

static async Task<int> RunTranscribeAsync(string[] args)
{
    if (args.Length == 0) { Console.Error.WriteLine("Usage: elbrunospeech transcribe <path.wav>"); return 1; }
    var path = args[0];
    if (!File.Exists(path)) { Console.Error.WriteLine($"File not found: {path}"); return 1; }

    Console.WriteLine($"Transcribing: {path}");
    Console.WriteLine("(Using fake STT provider — replace with a real ISpeechToTextClient)");

    var (format, samples) = WavReader.Read(path);
    Console.WriteLine($"Format: {format.SampleRate} Hz, {format.Channels} ch, {format.SampleFormat}");
    Console.WriteLine($"Duration: {TimeSpan.FromSeconds((double)samples.Length / format.BytesPerSecond):g}");

    using var stt = new FakeSttProvider($"[Transcription of {Path.GetFileName(path)}]");
    using var stream = new MemoryStream(samples.ToArray());
    var result = await stt.GetTextAsync(stream);
    Console.WriteLine($"Transcript: {result.Text}");
    return 0;
}

// ── vad ──────────────────────────────────────────────────────────────────────

static async Task<int> RunVadAsync(string[] args)
{
    if (args.Length == 0) { Console.Error.WriteLine("Usage: elbrunospeech vad <path.wav>"); return 1; }
    var path = args[0];
    if (!File.Exists(path)) { Console.Error.WriteLine($"File not found: {path}"); return 1; }

    Console.WriteLine($"Running VAD on: {path}");

    // Replicate the default model path (ModelDownloader is internal to ElBruno.Speech.Vad.Silero)
    var modelPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".cache", "elbruno-speech", "models", "silero_vad_v4.onnx");

    if (!File.Exists(modelPath))
    {
        Console.WriteLine($"Silero VAD model not found at: {modelPath}");
        Console.WriteLine("The model will be downloaded automatically on first use via SileroVadClientFactory.");
        Console.WriteLine("Showing frame count only (no VAD classification).");
        var (fmt, samples) = WavReader.Read(path);
        var framer = new AudioFramer(fmt, frameDurationMs: 32);
        int count = framer.Frame(samples).Count();
        Console.WriteLine($"Frames (32ms): {count}");
        return 0;
    }

    await using var factory = new SileroVadClientFactory();
    await using var vad = await factory.CreateAsync();
    var (format, pcm) = WavReader.Read(path);
    var audioFramer = new AudioFramer(format, frameDurationMs: 32);

    int speechCount = 0, silenceCount = 0;
    foreach (var frame in audioFramer.Frame(pcm))
    {
        var result = await vad.ProcessFrameAsync(frame);
        if (result.State == VoiceActivityState.Speech) speechCount++;
        else silenceCount++;
    }
    Console.WriteLine($"Speech frames: {speechCount} | Silence frames: {silenceCount}");
    Console.WriteLine($"Speech ratio: {speechCount * 100.0 / (speechCount + silenceCount):F1}%");
    return 0;
}

// ── talk ─────────────────────────────────────────────────────────────────────

static async Task<int> RunTalkAsync(string[] args)
{
    if (args.Length == 0) { Console.Error.WriteLine("Usage: elbrunospeech talk <text> [output.wav]"); return 1; }
    var text = args[0];
    var outPath = args.Length > 1 ? args[1] : "output.wav";

    Console.WriteLine($"Synthesizing: \"{text}\"");
    Console.WriteLine("(Using fake TTS provider — replace with a real ITextToSpeechClient)");

    using var tts = new FakeTtsProvider();
    var result = await tts.GetAudioAsync(text);
    var bytes = result.RawRepresentation as byte[] ?? [];
    WavWriter.Write(outPath, AudioFormat.Pcm16KhzMono, bytes);
    Console.WriteLine($"Written to: {outPath} ({bytes.Length} bytes PCM)");
    return 0;
}

// ── serve ────────────────────────────────────────────────────────────────────

static async Task<int> RunServeAsync(string[] args)
{
    int port = 5150;
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i] is "--port" or "-p" && int.TryParse(args[i + 1], out var p))
            port = p;

    Console.WriteLine($"Starting WebSocket speech server on port {port}...");
    Console.WriteLine("(Use elbrunospeech serve with real providers for production)");
    Console.WriteLine("Press Ctrl+C to stop.");
    Console.WriteLine("For the full WebSocket server, run the WebSocketVoiceAgent sample:");
    Console.WriteLine("  dotnet run --project src/samples/WebSocketVoiceAgent");
    return await Task.FromResult(0);
}

// ── bench ────────────────────────────────────────────────────────────────────

static int RunBench()
{
    Console.WriteLine("Benchmarks are in the benchmarks/ project.");
    Console.WriteLine("Run: dotnet run --project benchmarks/ElBruno.Speech.Benchmarks -c Release");
    return 0;
}

// ── help ─────────────────────────────────────────────────────────────────────

static int PrintHelp()
{
    Console.WriteLine("""
        elbrunospeech — ElBruno.Speech CLI

        Usage: elbrunospeech <command> [options]

        Commands:
          devices                    List audio input/output devices
          transcribe <path.wav>      Transcribe a WAV file to text
          vad <path.wav>             Run voice activity detection on a WAV file
          talk <text> [output.wav]   Synthesize text to a WAV file
          serve [--port N]           Start WebSocket voice server (see note)
          bench                      Show benchmark instructions

        Examples:
          elbrunospeech devices
          elbrunospeech transcribe recording.wav
          elbrunospeech vad recording.wav
          elbrunospeech talk "Hello world" hello.wav
        """);
    return 0;
}

static int PrintUnknown(string cmd)
{
    Console.Error.WriteLine($"Unknown command: {cmd}. Run 'elbrunospeech help' for usage.");
    return 1;
}

// ── Inline fake providers (used when no real providers are configured) ────────

sealed class FakeSttProvider(string transcript) : ISpeechToTextClient
{
    public SpeechToTextClientMetadata Metadata => new("fake-stt", null, null);
    public Task<SpeechToTextResponse> GetTextAsync(Stream s, SpeechToTextOptions? o = null, CancellationToken ct = default)
        => Task.FromResult(new SpeechToTextResponse(transcript));
    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream s, SpeechToTextOptions? o = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    { yield return new SpeechToTextResponseUpdate(transcript); await Task.CompletedTask; }
    public object? GetService(Type t, object? k = null) => null;
    public void Dispose() { }
}

sealed class FakeTtsProvider : ITextToSpeechClient
{
    public TextToSpeechClientMetadata Metadata => new("fake-tts", null, null);
    public Task<TextToSpeechResponse> GetAudioAsync(string text, TextToSpeechOptions? o = null, CancellationToken ct = default)
    {
        var bytes = new byte[AudioFormat.Pcm16KhzMono.BytesPerSecond / 10];
        return Task.FromResult(new TextToSpeechResponse { RawRepresentation = bytes });
    }
    public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text, TextToSpeechOptions? o = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var bytes = new byte[AudioFormat.Pcm16KhzMono.BytesPerSecond / 10];
        yield return new TextToSpeechResponseUpdate { RawRepresentation = bytes };
        await Task.CompletedTask;
    }
    public object? GetService(Type t, object? k = null) => null;
    public void Dispose() { }
}
