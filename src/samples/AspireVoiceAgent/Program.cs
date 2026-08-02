// AspireVoiceAgent — production-ready speech service with OpenTelemetry, WebSocket, and Aspire compatibility.
// Replace the fake AI providers with real ISpeechToTextClient, IChatClient, ITextToSpeechClient implementations.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using ElBruno.Speech;
using ElBruno.Speech.AspNetCore;
using ElBruno.Speech.OpenTelemetry;
using ElBruno.Speech.Pipeline;
using ElBruno.Speech.Vad.Silero;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Information);

// OpenTelemetry — Aspire-compatible: exports to OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set.
// Aspire automatically injects OTLP settings for the dashboard.
builder.Services.AddOpenTelemetry()
    .AddSpeechPipelineTelemetry();

// Health checks (built into Microsoft.NET.Sdk.Web — no extra package needed)
builder.Services.AddHealthChecks()
    .AddCheck("speech-pipeline", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Pipeline ready."));

// AI providers — replace with real implementations
builder.Services.AddSingleton<ISpeechToTextClient>(_ =>
    new FakeSttProvider("Hello, I'm using the Aspire voice agent."));
builder.Services.AddSingleton<IChatClient>(_ =>
    new FakeLlmProvider("The Aspire voice agent is running with full OpenTelemetry integration."));
builder.Services.AddSingleton<ITextToSpeechClient>(_ =>
    new FakeTtsProvider());

// VAD (optional — remove if no model available)
builder.Services.AddSileroVad(new VadOptions { Threshold = 0.5f });

// Speech pipeline
builder.Services.AddSpeechPipelineAspNetCore(new SpeechPipelineOptions
{
    FrameDurationMs = 20,
    ChannelCapacity = 128,
});

var app = builder.Build();

// Endpoints
app.MapSpeechWebSocket("/speech");
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new
{
    service = "ElBruno.Speech.AspireVoiceAgent",
    version = "1.0.0",
    endpoints = new { speech = "ws://host/speech", health = "/health" },
}));
app.MapGet("/sessions", (SpeechSessionRegistry registry) => Results.Ok(new
{
    activeSessionCount = registry.ActiveSessionCount,
    sessionIds = registry.GetSessionIds(),
}));

Console.WriteLine("AspireVoiceAgent started.");
Console.WriteLine("  WebSocket: ws://localhost:{port}/speech");
Console.WriteLine("  Health:    http://localhost:{port}/health");

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
