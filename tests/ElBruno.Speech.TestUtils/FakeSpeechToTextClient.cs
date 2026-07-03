using Microsoft.Extensions.AI;

namespace ElBruno.Speech.TestUtils;

/// <summary>
/// Deterministic fake <see cref="ISpeechToTextClient"/> for unit tests.
/// Returns a configurable transcript without performing any real inference.
/// </summary>
public sealed class FakeSpeechToTextClient : ISpeechToTextClient
{
    private readonly string _transcript;

    public FakeSpeechToTextClient(string transcript = "hello world") => _transcript = transcript;

    public SpeechToTextClientMetadata Metadata => new("fake-stt", null, null);

    public Task<SpeechToTextResponse> GetTextAsync(
        Stream audioSteam,
        SpeechToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SpeechToTextResponse(_transcript));
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioStream,
        SpeechToTextOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new SpeechToTextResponseUpdate(_transcript);
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose() { }
}
