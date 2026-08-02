using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ElBruno.Speech;

namespace ElBruno.Speech.Pipeline;

/// <summary>
/// Extension methods for registering <see cref="SpeechPipelineRealtimeAdapter"/> with DI.
/// </summary>
public static class RealtimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SpeechPipelineRealtimeAdapter"/> as <see cref="IRealtimeClient"/>.
    /// Requires <see cref="ISpeechPipeline"/> to be registered (call
    /// <see cref="ServiceCollectionExtensions.AddSpeechPipeline"/> first).
    /// </summary>
    public static IServiceCollection AddSpeechPipelineRealtimeClient(
        this IServiceCollection services)
    {
        services.AddSingleton<IRealtimeClient>(sp =>
            new SpeechPipelineRealtimeAdapter(
                () => sp.GetRequiredService<ISpeechPipeline>()));
        return services;
    }
}
