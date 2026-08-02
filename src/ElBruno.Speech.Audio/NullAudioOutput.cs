namespace ElBruno.Speech.Audio;

/// <summary>An <see cref="IAudioOutput"/> that discards all frames. Useful for testing.</summary>
public sealed class NullAudioOutput : IAudioOutput
{
    /// <inheritdoc/>
    public ValueTask WriteAsync(AudioFrame frame, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask ClearAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
