namespace ElBruno.Speech.Audio;

/// <summary>
/// An <see cref="IAudioOutput"/> that accumulates PCM frames and writes a WAV file on disposal.
/// </summary>
public sealed class WavAudioOutput : IAudioOutput
{
    private readonly string _path;
    private readonly List<byte[]> _chunks = [];
    private AudioFormat? _format;
    private bool _disposed;

    /// <param name="path">Output path for the WAV file.</param>
    public WavAudioOutput(string path) => _path = path;

    /// <inheritdoc/>
    public ValueTask WriteAsync(AudioFrame frame, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _format ??= frame.Format;
        _chunks.Add(frame.Data.ToArray());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        _chunks.Clear();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        if (_format is not null && _chunks.Count > 0)
        {
            int total = _chunks.Sum(c => c.Length);
            var pcm = new byte[total];
            int offset = 0;
            foreach (var chunk in _chunks)
            {
                chunk.CopyTo(pcm, offset);
                offset += chunk.Length;
            }
            WavWriter.Write(_path, _format, pcm);
        }

        return ValueTask.CompletedTask;
    }
}
