using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ElBruno.Speech.OpenTelemetry;

/// <summary>Extension methods for registering ElBruno.Speech telemetry.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ElBruno.Speech meter and activity source to the OpenTelemetry builder.
    /// Call after <c>builder.Services.AddOpenTelemetry()</c>.
    /// </summary>
    public static OpenTelemetryBuilder AddSpeechPipelineTelemetry(this OpenTelemetryBuilder builder)
    {
        builder
            .WithMetrics(metrics => metrics.AddMeter(SpeechPipelineMetrics.MeterName))
            .WithTracing(tracing => tracing.AddSource(SpeechPipelineActivitySource.SourceName));
        return builder;
    }
}
