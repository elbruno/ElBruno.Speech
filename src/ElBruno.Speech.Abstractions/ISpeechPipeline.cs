namespace ElBruno.Speech;

/// <summary>
/// Orchestrates the full VAD → STT → LLM → TTS pipeline for a session.
/// </summary>
public interface ISpeechPipeline : IAsyncDisposable
{
    /// <summary>Starts the pipeline and begins processing audio from <paramref name="input"/>.</summary>
    Task RunAsync(IAudioInput input, IAudioOutput output, CancellationToken cancellationToken = default);

    /// <summary>Requests an immediate graceful stop of the pipeline.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
