using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.Speech.Stt.Whisper;

public static class ServiceCollectionExtensions
{
    /// <summary>Opt-in registration, with no model download or native loading until resolution.</summary>
    public static IServiceCollection AddWhisperLargeV3SpeechToText(this IServiceCollection services,
        WhisperOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var snapshot = (options ?? new WhisperOptions()).Snapshot();
        services.AddSingleton<ISpeechToTextClient>(_ => new WhisperSpeechToTextClient(snapshot));
        return services;
    }
}
