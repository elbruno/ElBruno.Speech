using System.Buffers;

namespace ElBruno.Speech.Audio;

/// <summary>
/// Chops a contiguous PCM buffer into fixed-duration <see cref="AudioFrame"/> objects.
/// Default frame size is 20 ms at 16 kHz mono Int16 = 320 samples = 640 bytes.
/// </summary>
public sealed class AudioFramer
{
    private readonly AudioFormat _format;
    private readonly int _frameSizeBytes;
    private long _sequenceNumber;
    private TimeSpan _currentTimestamp;

    /// <param name="format">Audio format of the frames to produce.</param>
    /// <param name="frameDurationMs">Frame duration in milliseconds (default: 20).</param>
    public AudioFramer(AudioFormat format, int frameDurationMs = 20)
    {
        if (frameDurationMs <= 0) throw new ArgumentOutOfRangeException(nameof(frameDurationMs));
        _format = format;
        int samplesPerFrame = format.SampleRate / 1000 * frameDurationMs;
        _frameSizeBytes = samplesPerFrame * format.Channels * format.BytesPerSample;
    }

    /// <summary>Frame size in bytes.</summary>
    public int FrameSizeBytes => _frameSizeBytes;

    /// <summary>
    /// Partitions <paramref name="data"/> into frames. The last frame is padded with silence
    /// (zero bytes) if data length is not a multiple of <see cref="FrameSizeBytes"/>.
    /// Each frame's <see cref="AudioFrame.Data"/> owns its own copy of the PCM bytes.
    /// </summary>
    public IEnumerable<AudioFrame> Frame(ReadOnlyMemory<byte> data)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            int remaining = data.Length - offset;
            bool isFinal = remaining <= _frameSizeBytes;
            int chunkSize = Math.Min(_frameSizeBytes, remaining);

            var frameMem = new byte[_frameSizeBytes]; // owned copy
            data.Slice(offset, chunkSize).Span.CopyTo(frameMem);
            // tail padding is already zero from array initialization

            yield return new AudioFrame(
                Data: frameMem,
                Format: _format,
                SequenceNumber: _sequenceNumber++,
                Timestamp: _currentTimestamp,
                IsFinal: isFinal);

            _currentTimestamp += TimeSpan.FromSeconds((double)_frameSizeBytes / _format.BytesPerSecond);
            offset += chunkSize;
        }
    }

    /// <summary>Resets sequence numbers and timestamps to zero.</summary>
    public void Reset()
    {
        _sequenceNumber = 0;
        _currentTimestamp = TimeSpan.Zero;
    }
}
