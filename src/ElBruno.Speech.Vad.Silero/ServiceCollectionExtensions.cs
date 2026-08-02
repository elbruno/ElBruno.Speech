using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.Speech.Vad.Silero;

/// <summary>Extension methods for registering Silero VAD with DI.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SileroVadClientFactory"/> as a singleton and provides
    /// a scoped factory method for creating <see cref="SileroVadClient"/> instances.
    /// </summary>
    public static IServiceCollection AddSileroVad(
        this IServiceCollection services,
        VadOptions? options = null)
    {
        services.AddSingleton(new SileroVadClientFactory(options));
        return services;
    }
}
