namespace ElBruno.Speech;

/// <summary>
/// An immutable view of a PCM audio frame.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Data"/> is a view into caller-owned memory and is only valid for the lifetime
/// of the frame object. Long-lived consumers must copy <see cref="Data"/> before the frame
/// is reused or returned to a pool.
/// </para>
/// <para>
/// Sequence numbers are monotonically increasing per session and must not be reused.
/// </para>
/// </remarks>
/// <param name="Data">Raw sample bytes in the encoding described by <see cref="Format"/>.</param>
/// <param name="Format">Sample rate, channel count, and encoding.</param>
/// <param name="SequenceNumber">Monotonically increasing frame index within a session.</param>
/// <param name="Timestamp">Position of the first sample relative to the session start.</param>
/// <param name="IsFinal">
/// <see langword="true"/> if this is the last frame in the current stream or turn.
/// </param>
public readonly record struct AudioFrame(
    ReadOnlyMemory<byte> Data,
    AudioFormat Format,
    long SequenceNumber,
    TimeSpan Timestamp,
    bool IsFinal = false)
{
    /// <summary>Duration of the audio represented by this frame.</summary>
    public TimeSpan Duration =>
        TimeSpan.FromSeconds((double)Data.Length / Format.BytesPerSecond);
}
