namespace ElBruno.Speech;

/// <summary>Configuration for the speech pipeline.</summary>
public sealed record SpeechPipelineOptions
{
    /// <summary>Target audio format for VAD and STT (default: 16 kHz, mono, Int16).</summary>
    public AudioFormat TargetFormat { get; init; } = AudioFormat.Pcm16KhzMono;

    /// <summary>Frame duration in milliseconds (default: 20 ms).</summary>
    public int FrameDurationMs { get; init; } = 20;

    /// <summary>Pre-roll before speech start to avoid clipping (default: 200 ms).</summary>
    public int PreRollMs { get; init; } = 200;

    /// <summary>Maximum bounded-channel capacity per pipeline stage (default: 64 frames).</summary>
    public int ChannelCapacity { get; init; } = 64;
}
