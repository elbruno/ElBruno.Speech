using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.Speech.Stt.Parakeet;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers Parakeet as the STT provider. Does not download model assets.</summary>
    public static IServiceCollection AddParakeetSpeechToText(this IServiceCollection services,
        ParakeetOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var snapshot = (options ?? new ParakeetOptions()).Snapshot();
        services.AddSingleton<ISpeechToTextClient>(_ => new ParakeetSpeechToTextClient(snapshot));
        return services;
    }
}
