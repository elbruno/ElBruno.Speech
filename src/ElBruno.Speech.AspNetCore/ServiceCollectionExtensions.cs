using Microsoft.Extensions.DependencyInjection;
using ElBruno.Speech;
using ElBruno.Speech.Pipeline;

namespace ElBruno.Speech.AspNetCore;

/// <summary>Extension methods for registering ASP.NET Core speech services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the speech pipeline and session registry for use with
    /// <see cref="WebApplicationExtensions.MapSpeechWebSocket"/>.
    /// </summary>
    /// <remarks>
    /// Callers must also register <see cref="Microsoft.Extensions.AI.ISpeechToTextClient"/>,
    /// <see cref="Microsoft.Extensions.AI.IChatClient"/>, and
    /// <see cref="Microsoft.Extensions.AI.ITextToSpeechClient"/> separately.
    /// </remarks>
    public static IServiceCollection AddSpeechPipelineAspNetCore(
        this IServiceCollection services,
        SpeechPipelineOptions? options = null)
    {
        services.AddSingleton<SpeechSessionRegistry>();
        services.AddSpeechPipeline(options);
        return services;
    }
}
