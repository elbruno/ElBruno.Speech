using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.Speech.BlazorComponents;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSpeechBlazorComponents(this IServiceCollection services)
    {
        services.AddSingleton<IAudioDeviceProvider, DefaultAudioDeviceProvider>();
        return services;
    }
}
