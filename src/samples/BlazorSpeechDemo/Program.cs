using Microsoft.Extensions.AI;
using ElBruno.Speech;
using ElBruno.Speech.BlazorComponents;
using ElBruno.Speech.Pipeline;
using BlazorSpeechDemo;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Register fake AI providers for demo purposes
builder.Services.AddSingleton<ISpeechToTextClient>(_ =>
    new DemoSttClient("Transcribed speech text goes here."));
builder.Services.AddSingleton<IChatClient>(_ =>
    new DemoChatClient("This is a demo response from the language model."));
builder.Services.AddSingleton<ITextToSpeechClient>(_ =>
    new DemoTtsClient());

builder.Services.AddSpeechPipeline();
builder.Services.AddSpeechBlazorComponents();

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

// Demo providers
sealed class DemoSttClient(string text) : ISpeechToTextClient
{
    public SpeechToTextClientMetadata Metadata => new("demo-stt", null, null);
    public Task<SpeechToTextResponse> GetTextAsync(Stream s, SpeechToTextOptions? o = null, CancellationToken ct = default)
        => Task.FromResult(new SpeechToTextResponse(text));
    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream s, SpeechToTextOptions? o = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    { yield return new SpeechToTextResponseUpdate(text); await Task.CompletedTask; }
    public object? GetService(Type t, object? k = null) => null;
    public void Dispose() { }
}

sealed class DemoChatClient(string response) : IChatClient
{
    public ChatClientMetadata Metadata => new("demo-chat", null, null);
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> msgs, ChatOptions? o = null, CancellationToken ct = default)
        => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> msgs, ChatOptions? o = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    { yield return new ChatResponseUpdate(ChatRole.Assistant, response); await Task.CompletedTask; }
    public object? GetService(Type t, object? k = null) => null;
    public void Dispose() { }
}

sealed class DemoTtsClient : ITextToSpeechClient
{
    public TextToSpeechClientMetadata Metadata => new("demo-tts", null, null);
    public Task<TextToSpeechResponse> GetAudioAsync(string text, TextToSpeechOptions? o = null, CancellationToken ct = default)
        => Task.FromResult(new TextToSpeechResponse { RawRepresentation = new byte[100] });
    public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text, TextToSpeechOptions? o = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    { yield return new TextToSpeechResponseUpdate { RawRepresentation = new byte[100] }; await Task.CompletedTask; }
    public object? GetService(Type t, object? k = null) => null;
    public void Dispose() { }
}
