using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.Speech.Tts.Ollama;

/// <summary>Dependency injection extensions for the Ollama speech provider.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the Ollama Phonon client as an <see cref="ITextToSpeechClient"/>.</summary>
    public static IServiceCollection AddOllamaSpeech(this IServiceCollection services,
        OllamaSpeechOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var snapshot = (options ?? new OllamaSpeechOptions()).Snapshot();
        services.AddSingleton<ITextToSpeechClient>(_ => new OllamaSpeechClient(snapshot));
        return services;
    }
}
