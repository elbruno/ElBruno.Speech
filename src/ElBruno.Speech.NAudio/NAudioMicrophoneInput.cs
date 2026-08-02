using System.Threading.Channels;
using NAudio.Wave;
using ElBruno.Speech;
using ElBruno.Speech.Audio;

namespace ElBruno.Speech.NAudio;

/// <summary>
/// An <see cref="IAudioInput"/> that captures audio from a system microphone via NAudio.
/// Produces 16 kHz mono Int16 PCM frames.
/// </summary>
public sealed class NAudioMicrophoneInput : IAudioInput
{
    private readonly WaveInEvent _waveIn;
    private readonly AudioFramer _framer;
    private readonly AudioRingBuffer _ringBuffer;
    private readonly Channel<AudioFrame> _channel;
    private readonly Task _readerTask;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <param name="deviceNumber">NAudio device index (0 = system default).</param>
    /// <param name="frameDurationMs">Frame duration in milliseconds (default: 20).</param>
    public NAudioMicrophoneInput(int deviceNumber = 0, int frameDurationMs = 20)
    {
        Format = AudioFormat.Pcm16KhzMono;
        _framer = new AudioFramer(Format, frameDurationMs);

        // Ring buffer: 2 seconds of audio headroom
        _ringBuffer = new AudioRingBuffer(Format.BytesPerSecond * 2);

        _channel = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(200)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = false,
        });

        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(16_000, 16, 1),
            BufferMilliseconds = frameDurationMs * 2,
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();

        // Background task: drain ring buffer → frame → channel
        _readerTask = Task.Run(() => DrainLoopAsync(_cts.Token));
    }

    /// <inheritdoc/>
    public AudioFormat Format { get; }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0) return;
        // Best-effort write; ring buffer drops oldest if full
        _ringBuffer.TryWrite(e.Buffer.AsSpan(0, e.BytesRecorded));
    }

    private async Task DrainLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                int available = _ringBuffer.Available;
                if (available < _framer.FrameSizeBytes)
                {
                    await Task.Delay(5, token); // wait for more data
                    continue;
                }

                int toRead = (available / _framer.FrameSizeBytes) * _framer.FrameSizeBytes;
                var readBuf = new byte[toRead];
                int actualRead = _ringBuffer.TryRead(readBuf);

                foreach (var frame in _framer.Frame(readBuf.AsMemory(0, actualRead)))
                    await _channel.Writer.WriteAsync(frame, token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _channel.Writer.TryComplete();
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<AudioFrame> ReadFramesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _waveIn.StopRecording();
        await _cts.CancelAsync();
        try { await _readerTask; } catch (OperationCanceledException) { }
        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.Dispose();
        _ringBuffer.Dispose();
        _cts.Dispose();
    }
}
