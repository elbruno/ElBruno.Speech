using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ElBruno.Speech;
using ElBruno.Speech.AspNetCore;
using ElBruno.Speech.Pipeline;

// WebSocketVoiceAgent — ASP.NET Core server with WebSocket speech endpoint.
// Connect a browser (or any WebSocket client) to /speech to test the pipeline.
// Send binary PCM (16 kHz mono Int16) and receive TTS audio + JSON events.

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Register fake AI providers (replace with real providers for production use)
builder.Services.AddSingleton<ISpeechToTextClient>(_ =>
    new FakeSttProvider("Hello, what can you help me with today?"));
builder.Services.AddSingleton<IChatClient>(_ =>
    new FakeLlmProvider("I can help you with many things! This is a demonstration of the ElBruno Speech WebSocket pipeline."));
builder.Services.AddSingleton<ITextToSpeechClient>(_ =>
    new FakeTtsProvider());

builder.Services.AddSpeechPipelineAspNetCore(new SpeechPipelineOptions
{
    FrameDurationMs = 20,
    ChannelCapacity = 128,
});

var app = builder.Build();

// Map the speech WebSocket endpoint at /speech
app.MapSpeechWebSocket("/speech");

// Simple status endpoint
app.MapGet("/", () => Results.Ok(new
{
    status = "running",
    endpoint = "ws://localhost:5000/speech",
    protocol = "binary PCM (16kHz mono Int16) in, binary PCM + JSON events out",
}));

app.MapGet("/sessions", (SpeechSessionRegistry registry) => Results.Ok(new
{
    activeSessionCount = registry.ActiveSessionCount,
    sessionIds = registry.GetSessionIds(),
}));

Console.WriteLine("WebSocketVoiceAgent running.");
Console.WriteLine("Speech endpoint: ws://localhost:{port}/speech");
Console.WriteLine("Status: http://localhost:{port}/");
Console.WriteLine("Connect a WebSocket client and send 16 kHz mono Int16 PCM audio.");

await app.RunAsync();

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

