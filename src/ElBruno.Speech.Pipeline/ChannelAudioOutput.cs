using System.Threading.Channels;
using ElBruno.Speech;

namespace ElBruno.Speech.Pipeline;

/// <summary>
/// An <see cref="IAudioOutput"/> that writes frames to a <see cref="ChannelWriter{T}"/>
/// for consumption by <see cref="SpeechPipelineRealtimeSession.GetStreamingResponseAsync"/>.
/// </summary>
internal sealed class ChannelAudioOutput : IAudioOutput
{
    private readonly ChannelWriter<AudioFrame> _writer;
    private bool _disposed;

    public ChannelAudioOutput(ChannelWriter<AudioFrame> writer) => _writer = writer;

    public async ValueTask WriteAsync(AudioFrame frame, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _writer.WriteAsync(frame, cancellationToken);
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() { _disposed = true; return ValueTask.CompletedTask; }
}
