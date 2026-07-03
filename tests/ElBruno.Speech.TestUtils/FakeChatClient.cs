using Microsoft.Extensions.AI;

namespace ElBruno.Speech.TestUtils;

/// <summary>
/// Deterministic fake <see cref="IChatClient"/> for unit tests.
/// Returns a configurable response without performing any real inference.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private readonly string _response;

    public FakeChatClient(string response = "I am a fake assistant.") => _response = response;

    public ChatClientMetadata Metadata => new("fake-chat", null, null);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _response)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var word in _response.Split(' '))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, word + " ");
            await Task.Yield();
        }
    }

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose() { }
}
