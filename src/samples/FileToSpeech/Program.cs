using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ElBruno.Speech;
using ElBruno.Speech.Audio;
using ElBruno.Speech.Pipeline;

// FileToSpeech — demonstrates the VAD → STT → LLM → TTS pipeline.
// Uses in-memory fake providers so the sample runs without external models.
// Replace the fake registrations with real provider packages to connect live models.

var inputPath = args.Length > 0 ? args[0] : null;
var outputPath = args.Length > 1 ? args[1] : "output.wav";

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Register fake providers (swap for real providers in production)
builder.Services.AddSingleton<ISpeechToTextClient>(_ =>
    new FakeSpeechToTextProvider("Hello, how can I help you today?"));
builder.Services.AddSingleton<IChatClient>(_ =>
    new FakeChatProvider("I am here to help! This is a demonstration of the ElBruno Speech pipeline."));
builder.Services.AddSingleton<ITextToSpeechClient>(_ =>
    new FakeTtsProvider());

builder.Services.AddSpeechPipeline(new SpeechPipelineOptions
{
    FrameDurationMs = 20,
    ChannelCapacity = 64,
});

var host = builder.Build();

// Build audio input
IAudioInput audioInput;
if (inputPath is not null && File.Exists(inputPath))
{
    Console.WriteLine($"Reading input: {inputPath}");
    audioInput = new FileAudioInput(inputPath);
}
else
{
    Console.WriteLine("No input file provided — using 2 seconds of generated silence.");
    var format = AudioFormat.Pcm16KhzMono;
    var silence = new byte[format.BytesPerSecond * 2]; // 2 seconds
    audioInput = new MemoryAudioInput(format, silence);
}

await using var audioOutput = new WavAudioOutput(outputPath);

var pipeline = host.Services.GetRequiredService<ISpeechPipeline>();

Console.WriteLine("Running pipeline...");
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await pipeline.RunAsync(audioInput, audioOutput, cts.Token);
await audioInput.DisposeAsync();

Console.WriteLine($"Done. Output written to: {outputPath}");

// ── Inline fake providers (self-contained sample) ──────────────────────────

sealed class FakeSpeechToTextProvider(string transcript) : ISpeechToTextClient
{
    public SpeechToTextClientMetadata Metadata => new("fake-stt", null, null);

    public Task<SpeechToTextResponse> GetTextAsync(
        Stream audioStream, SpeechToTextOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SpeechToTextResponse(transcript));

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioStream, SpeechToTextOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new SpeechToTextResponseUpdate(transcript);
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? key = null) => null;
    public void Dispose() { }
}

sealed class FakeChatProvider(string response) : IChatClient
{
    public ChatClientMetadata Metadata => new("fake-chat", null, null);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, response);
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? key = null) => null;
    public void Dispose() { }
}

sealed class FakeTtsProvider : ITextToSpeechClient
{
    public TextToSpeechClientMetadata Metadata => new("fake-tts", null, null);

    public Task<TextToSpeechResponse> GetAudioAsync(
        string text, TextToSpeechOptions? options = null, CancellationToken cancellationToken = default)
    {
        // 100ms of silence per segment
        var bytes = new byte[AudioFormat.Pcm16KhzMono.BytesPerSecond / 10];
        return Task.FromResult(new TextToSpeechResponse { RawRepresentation = bytes });
    }

    public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text, TextToSpeechOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var bytes = new byte[AudioFormat.Pcm16KhzMono.BytesPerSecond / 10];
        yield return new TextToSpeechResponseUpdate { RawRepresentation = bytes };
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? key = null) => null;
    public void Dispose() { }
}
