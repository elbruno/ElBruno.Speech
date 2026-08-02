using Microsoft.Extensions.AI;
using ElBruno.Speech;

namespace ElBruno.Speech.Pipeline;

/// <summary>
/// Wraps an <see cref="ISpeechPipeline"/> factory as an <see cref="IRealtimeClient"/>,
/// enabling the ElBruno.Speech pipeline to be used anywhere MEAI's realtime API is accepted.
/// </summary>
/// <remarks>
/// Each call to <see cref="CreateSessionAsync"/> creates an independent pipeline session
/// with its own audio channels and recurrent state.
/// </remarks>
public sealed class SpeechPipelineRealtimeAdapter : IRealtimeClient
{
    private readonly Func<ISpeechPipeline> _pipelineFactory;

    /// <param name="pipelineFactory">
    /// Factory that creates a new <see cref="ISpeechPipeline"/> instance per session.
    /// Must return a fresh instance each call — pipeline state is per-session.
    /// </param>
    public SpeechPipelineRealtimeAdapter(Func<ISpeechPipeline> pipelineFactory)
        => _pipelineFactory = pipelineFactory;

    /// <inheritdoc/>
    public Task<IRealtimeClientSession> CreateSessionAsync(
        RealtimeSessionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var pipeline = _pipelineFactory();
        var session = new SpeechPipelineRealtimeSession(options ?? new RealtimeSessionOptions(), pipeline);
        return Task.FromResult<IRealtimeClientSession>(session);
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? key = null) => null;

    /// <inheritdoc/>
    public void Dispose() { }
}
