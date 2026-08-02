using System.Threading.Channels;
using ElBruno.Speech;

namespace ElBruno.Speech.AspNetCore;

/// <summary>
/// An <see cref="IAudioOutput"/> that forwards PCM frames to the WebSocket send channel.
/// </summary>
internal sealed class WebSocketAudioOutput : IAudioOutput
{
    private readonly ChannelWriter<(byte[]? Pcm, string? Json)> _writer;
    private bool _disposed;

    public WebSocketAudioOutput(ChannelWriter<(byte[]? Pcm, string? Json)> writer)
        => _writer = writer;

    public async ValueTask WriteAsync(AudioFrame frame, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var bytes = frame.Data.ToArray();
        await _writer.WriteAsync((bytes, null), cancellationToken);
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
