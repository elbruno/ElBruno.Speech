namespace ElBruno.Speech.Audio;

/// <summary>
/// An <see cref="IAudioInput"/> backed by an in-memory PCM byte array.
/// Useful for testing and offline processing.
/// </summary>
public sealed class MemoryAudioInput : IAudioInput
{
    private readonly ReadOnlyMemory<byte> _data;
    private readonly AudioFramer _framer;
    private bool _disposed;

    /// <param name="format">Audio format of the PCM data.</param>
    /// <param name="pcmData">Raw PCM samples (caller retains ownership).</param>
    /// <param name="frameDurationMs">Frame duration in milliseconds (default: 20).</param>
    public MemoryAudioInput(AudioFormat format, ReadOnlyMemory<byte> pcmData, int frameDurationMs = 20)
    {
        Format = format;
        _data = pcmData;
        _framer = new AudioFramer(format, frameDurationMs);
    }

    /// <inheritdoc/>
    public AudioFormat Format { get; }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AudioFrame> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (var frame in _framer.Frame(_data))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return frame;
            await Task.Yield();
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
