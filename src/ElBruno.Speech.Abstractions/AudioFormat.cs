namespace ElBruno.Speech;

/// <summary>
/// Describes the format of a PCM audio stream.
/// </summary>
/// <param name="SampleRate">Samples per channel per second (e.g. 16000, 48000).</param>
/// <param name="Channels">Number of interleaved channels (1 = mono, 2 = stereo).</param>
/// <param name="SampleFormat">Integer or float sample encoding.</param>
public sealed record AudioFormat(int SampleRate, int Channels, AudioSampleFormat SampleFormat)
{
    /// <summary>16 kHz, mono, 16-bit PCM — the canonical VAD/STT input format.</summary>
    public static AudioFormat Pcm16KhzMono { get; } = new(16_000, 1, AudioSampleFormat.Int16);

    /// <summary>Number of bytes that represent a single sample in one channel.</summary>
    public int BytesPerSample => SampleFormat switch
    {
        AudioSampleFormat.Int16 => 2,
        AudioSampleFormat.Float32 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(SampleFormat)),
    };

    /// <summary>Number of bytes per second for all channels combined.</summary>
    public int BytesPerSecond => SampleRate * Channels * BytesPerSample;
}
