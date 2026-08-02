using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ElBruno.Speech;
using ElBruno.Speech.Audio;
using ElBruno.Speech.NAudio;
using ElBruno.Speech.Pipeline;
using ElBruno.Speech.Vad.Silero;

// LocalVoiceAgent — full local voice loop: mic → VAD → STT → LLM → TTS → speaker.
//
// Usage:
//   dotnet run                  (simulated mode — no hardware required)
//   dotnet run -- --real        (real microphone + speaker, requires hardware)
//   dotnet run -- --list-devices

bool realMode = args.Contains("--real");
bool listDevices = args.Contains("--list-devices");

if (listDevices)
{
    Console.WriteLine("=== Input Devices ===");
    foreach (var d in AudioDeviceEnumerator.GetInputDevices())
        Console.WriteLine($"  [{d.DeviceNumber}] {d.Name}");
    Console.WriteLine("=== Output Devices ===");
    foreach (var d in AudioDeviceEnumerator.GetOutputDevices())
        Console.WriteLine($"  [{d.DeviceNumber}] {d.Name}");
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(realMode ? LogLevel.Information : LogLevel.Debug);

// Register fake AI providers (replace with real providers for production use)
builder.Services.AddSingleton<ISpeechToTextClient>(_ =>
    new FakeSttProvider("I would like to know the weather today."));
builder.Services.AddSingleton<IChatClient>(_ =>
    new FakeLlmProvider("The weather today is sunny with a high of 72 degrees. A great day to go outside!"));
builder.Services.AddSingleton<ITextToSpeechClient>(_ =>
    new FakeTtsProvider());

// Register VAD (factory; client created per session)
builder.Services.AddSileroVad(new VadOptions { Threshold = 0.5f });

// Register pipeline
builder.Services.AddSpeechPipeline(new SpeechPipelineOptions
{
    FrameDurationMs = 20,
    ChannelCapacity = 128,
    PreRollMs = 200,
});

var host = builder.Build();

Console.WriteLine(realMode
    ? "LocalVoiceAgent running in REAL mode — speak into your microphone. Ctrl+C to stop."
    : "LocalVoiceAgent running in SIMULATED mode. Pass --real to use hardware.");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

IAudioInput audioInput;
IAudioOutput audioOutput;

if (realMode)
{
    Console.WriteLine("Opening microphone and speaker...");
    audioInput = new NAudioMicrophoneInput(deviceNumber: 0, frameDurationMs: 20);
    audioOutput = new NAudioSpeakerOutput(deviceNumber: 0);
}
else
{
    // 3 seconds of simulated speech (non-silent so VAD pass-through fires)
    var format = AudioFormat.Pcm16KhzMono;
    var pcm = GenerateTone(format, frequencyHz: 440, durationMs: 3000);
    audioInput = new MemoryAudioInput(format, pcm, frameDurationMs: 20);
    audioOutput = new NullAudioOutput();
}

try
{
    var pipeline = host.Services.GetRequiredService<ISpeechPipeline>();
    Console.WriteLine("Pipeline started.");
    await pipeline.RunAsync(audioInput, audioOutput, cts.Token);
    Console.WriteLine("Pipeline finished.");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Stopped.");
}
finally
{
    await audioInput.DisposeAsync();
    await audioOutput.DisposeAsync();
}

// ── Helpers ──────────────────────────────────────────────────────────────────

static ReadOnlyMemory<byte> GenerateTone(AudioFormat format, double frequencyHz, int durationMs)
{
    int sampleCount = format.SampleRate * durationMs / 1000;
    var bytes = new byte[sampleCount * format.BytesPerSample * format.Channels];
    for (int i = 0; i < sampleCount; i++)
    {
        double t = i / (double)format.SampleRate;
        short s = (short)(short.MaxValue * 0.3 * Math.Sin(2 * Math.PI * frequencyHz * t));
        bytes[i * 2] = (byte)(s & 0xFF);
        bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
    }
    return bytes;
}

// ── Inline fake providers ─────────────────────────────────────────────────────

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

sealed class FakeLlmProvider(string response) : IChatClient
{
    public ChatClientMetadata Metadata => new("fake-llm", null, null);
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> msgs, ChatOptions? o = null, CancellationToken ct = default)
        => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> msgs, ChatOptions? o = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    { yield return new ChatResponseUpdate(ChatRole.Assistant, response); await Task.CompletedTask; }
    public object? GetService(Type t, object? k = null) => null;
    public void Dispose() { }
}

sealed class FakeTtsProvider : ITextToSpeechClient
{
    public TextToSpeechClientMetadata Metadata => new("fake-tts", null, null);
    public Task<TextToSpeechResponse> GetAudioAsync(string text, TextToSpeechOptions? o = null, CancellationToken ct = default)
    {
        var bytes = new byte[AudioFormat.Pcm16KhzMono.BytesPerSecond / 10]; // 100ms silence
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
