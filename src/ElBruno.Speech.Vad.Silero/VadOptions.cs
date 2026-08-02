namespace ElBruno.Speech.Vad.Silero;

/// <summary>Configuration for the Silero VAD client.</summary>
public sealed record VadOptions
{
    /// <summary>
    /// Speech probability threshold above which a frame is classified as speech.
    /// Default: 0.5.
    /// </summary>
    public float Threshold { get; init; } = 0.5f;

    /// <summary>
    /// Minimum number of consecutive speech frames before the state flips to Speech.
    /// Default: 3 frames (96 ms at 32 ms/frame).
    /// </summary>
    public int MinSpeechFrames { get; init; } = 3;

    /// <summary>
    /// Minimum number of consecutive silence frames before the state flips to Silence.
    /// Default: 6 frames (192 ms at 32 ms/frame).
    /// </summary>
    public int MinSilenceFrames { get; init; } = 6;

    /// <summary>
    /// Path to the Silero VAD ONNX model file.
    /// If null, the model is downloaded to the default cache location on first use.
    /// Default: null (auto-download).
    /// </summary>
    public string? ModelPath { get; init; }
}
