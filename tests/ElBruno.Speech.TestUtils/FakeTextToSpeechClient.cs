using Microsoft.Extensions.AI;

namespace ElBruno.Speech.TestUtils;

/// <summary>
/// Deterministic fake <see cref="ITextToSpeechClient"/> for unit tests.
/// Returns silent PCM audio without performing any real synthesis.
/// </summary>
public sealed class FakeTextToSpeechClient : ITextToSpeechClient
{
    private readonly int _silenceDurationMs;

    public FakeTextToSpeechClient(int silenceDurationMs = 100) => _silenceDurationMs = silenceDurationMs;

    public TextToSpeechClientMetadata Metadata => new("fake-tts", null, null);

    public Task<TextToSpeechResponse> GetAudioAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = AudioFormat.Pcm16KhzMono.BytesPerSecond * _silenceDurationMs / 1000;
        return Task.FromResult(new TextToSpeechResponse { RawRepresentation = new byte[bytes] });
    }

    public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text,
        TextToSpeechOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chunkSize = AudioFormat.Pcm16KhzMono.BytesPerSecond * 20 / 1000; // 20ms chunks
        var total = AudioFormat.Pcm16KhzMono.BytesPerSecond * _silenceDurationMs / 1000;
        var sent = 0;
        while (sent < total)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var take = Math.Min(chunkSize, total - sent);
            yield return new TextToSpeechResponseUpdate { RawRepresentation = new byte[take] };
            sent += take;
            await Task.Yield();
        }
    }

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose() { }
}
