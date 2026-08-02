using System.Threading.Channels;
using ElBruno.Speech;
using ElBruno.Speech.Audio;

namespace ElBruno.Speech.Pipeline;

/// <summary>
/// An <see cref="IAudioInput"/> that reads raw PCM byte chunks from a channel
/// and produces framed <see cref="AudioFrame"/> objects.
/// </summary>
internal sealed class ChannelAudioInput : IAudioInput
{
    private readonly ChannelReader<byte[]> _reader;
    private readonly AudioFramer _framer;
    private bool _disposed;

    public ChannelAudioInput(ChannelReader<byte[]> reader, AudioFormat format, int frameDurationMs = 20)
    {
        Format = format;
        _reader = reader;
        _framer = new AudioFramer(format, frameDurationMs);
    }

    public AudioFormat Format { get; }

    public async IAsyncEnumerable<AudioFrame> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await foreach (var chunk in _reader.ReadAllAsync(cancellationToken))
            foreach (var frame in _framer.Frame(chunk))
                yield return frame;
    }

    public ValueTask DisposeAsync() { _disposed = true; return ValueTask.CompletedTask; }
}
