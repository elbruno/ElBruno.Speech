using NAudio.Wave;
using ElBruno.Speech;

namespace ElBruno.Speech.NAudio;

/// <summary>
/// An <see cref="IAudioOutput"/> that plays PCM audio through a system speaker via NAudio.
/// Accepts 16 kHz mono Int16 frames.
/// </summary>
public sealed class NAudioSpeakerOutput : IAudioOutput
{
    private readonly WaveOutEvent _waveOut;
    private readonly BufferedWaveProvider _buffer;
    private bool _disposed;

    /// <param name="deviceNumber">NAudio device index (0 = system default).</param>
    /// <param name="bufferDurationMs">Size of the internal playback buffer in milliseconds (default: 500).</param>
    public NAudioSpeakerOutput(int deviceNumber = 0, int bufferDurationMs = 500)
    {
        var waveFormat = new WaveFormat(16_000, 16, 1);
        _buffer = new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromMilliseconds(bufferDurationMs),
            DiscardOnBufferOverflow = true,
        };

        _waveOut = new WaveOutEvent { DeviceNumber = deviceNumber };
        _waveOut.Init(_buffer);
        _waveOut.Play();
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(AudioFrame frame, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (frame.Format.SampleFormat != AudioSampleFormat.Int16 ||
            frame.Format.SampleRate != 16_000 ||
            frame.Format.Channels != 1)
            throw new SpeechPipelineException(
                "NAudioSpeakerOutput requires 16 kHz mono Int16 frames. " +
                $"Got: {frame.Format.SampleRate} Hz, {frame.Format.Channels} ch, {frame.Format.SampleFormat}.");

        // NAudio BufferedWaveProvider.AddSamples requires a byte[] — copy to avoid unsafe pinning
        var bytes = frame.Data.ToArray();
        _buffer.AddSamples(bytes, 0, bytes.Length);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _buffer.ClearBuffer();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _waveOut.Stop();
        _waveOut.Dispose();
        return ValueTask.CompletedTask;
    }
}
