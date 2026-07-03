namespace ElBruno.Speech;

/// <summary>Provides a stream of <see cref="AudioFrame"/> objects from a source.</summary>
public interface IAudioInput : IAsyncDisposable
{
    /// <summary>Format of the frames produced by this input.</summary>
    AudioFormat Format { get; }

    /// <summary>Reads frames until the source is exhausted or cancellation is requested.</summary>
    IAsyncEnumerable<AudioFrame> ReadFramesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Accepts <see cref="AudioFrame"/> objects for playback or storage.</summary>
public interface IAudioOutput : IAsyncDisposable
{
    /// <summary>Writes a single frame to the output.</summary>
    ValueTask WriteAsync(AudioFrame frame, CancellationToken cancellationToken = default);

    /// <summary>Discards all buffered frames immediately.</summary>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}
