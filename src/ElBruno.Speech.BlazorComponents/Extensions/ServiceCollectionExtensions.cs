using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.Speech.BlazorComponents;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSpeechBlazorComponents(this IServiceCollection services)
    {
        services.AddScoped<SpeechStateService>();
        services.AddScoped<IAudioDeviceProvider, BrowserAudioDeviceProvider>();
        return services;
    }
}
