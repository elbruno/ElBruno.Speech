namespace ElBruno.Speech.Audio;

/// <summary>
/// An <see cref="IAudioInput"/> that reads PCM frames from a WAV file on disk.
/// The file is read entirely on construction; frames are yielded on demand.
/// </summary>
public sealed class FileAudioInput : IAudioInput
{
    private readonly ReadOnlyMemory<byte> _samples;
    private readonly AudioFramer _framer;
    private bool _disposed;

    /// <param name="path">Path to a PCM WAV file.</param>
    /// <param name="frameDurationMs">Frame duration in milliseconds (default: 20).</param>
    public FileAudioInput(string path, int frameDurationMs = 20)
    {
        (Format, _samples) = WavReader.Read(path);
        _framer = new AudioFramer(Format, frameDurationMs);
    }

    /// <inheritdoc/>
    public AudioFormat Format { get; }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AudioFrame> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (var frame in _framer.Frame(_samples))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return frame;
            await Task.Yield(); // allow cancellation and cooperative scheduling
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
