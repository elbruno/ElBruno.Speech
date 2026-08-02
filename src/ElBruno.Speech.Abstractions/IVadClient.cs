namespace ElBruno.Speech;

/// <summary>Detects voice activity in a stream of audio frames.</summary>
public interface IVadClient : IAsyncDisposable
{
    /// <summary>
    /// Processes a single audio frame and returns the current voice activity state.
    /// </summary>
    ValueTask<VoiceActivityResult> ProcessFrameAsync(AudioFrame frame, CancellationToken cancellationToken = default);

    /// <summary>Resets the internal VAD state (e.g. silence/speech counters).</summary>
    void Reset();
}

/// <summary>Result from a single VAD frame evaluation.</summary>
/// <param name="State">Whether the frame contains speech or silence.</param>
/// <param name="Confidence">Probability score from the underlying model (0–1).</param>
/// <param name="Frame">The frame that was evaluated.</param>
public readonly record struct VoiceActivityResult(
    VoiceActivityState State,
    float Confidence,
    AudioFrame Frame);

/// <summary>Voice activity classification.</summary>
public enum VoiceActivityState
{
    /// <summary>The frame contains silence.</summary>
    Silence = 0,

    /// <summary>The frame contains speech.</summary>
    Speech = 1,
}
