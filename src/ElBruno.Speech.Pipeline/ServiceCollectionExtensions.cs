using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using ElBruno.Speech;

namespace ElBruno.Speech.Pipeline;

/// <summary>Extension methods for registering the speech pipeline with DI.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DefaultSpeechPipeline"/> as a transient <see cref="ISpeechPipeline"/>.
    /// Requires <see cref="ISpeechToTextClient"/>, <see cref="IChatClient"/>, and
    /// <see cref="ITextToSpeechClient"/> to be registered separately.
    /// </summary>
    public static IServiceCollection AddSpeechPipeline(
        this IServiceCollection services,
        SpeechPipelineOptions? options = null)
    {
        if (options is not null)
            services.AddSingleton(options);
        else
            services.AddSingleton(new SpeechPipelineOptions());

        services.AddTransient<ISpeechPipeline>(sp => new DefaultSpeechPipeline(
            sp.GetRequiredService<ISpeechToTextClient>(),
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<ITextToSpeechClient>(),
            sp.GetRequiredService<SpeechPipelineOptions>(),
            sp.GetService<IVadClient>()));

        return services;
    }
}
