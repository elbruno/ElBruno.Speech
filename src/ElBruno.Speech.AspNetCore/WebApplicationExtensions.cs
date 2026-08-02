using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ElBruno.Speech;

namespace ElBruno.Speech.AspNetCore;

/// <summary>Extension methods for mapping the speech WebSocket endpoint.</summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Maps a WebSocket endpoint at <paramref name="path"/> that accepts audio from clients,
    /// runs it through the speech pipeline, and streams TTS audio back.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="path">The endpoint path (default: "/speech").</param>
    public static WebApplication MapSpeechWebSocket(this WebApplication app, string path = "/speech")
    {
        app.UseWebSockets();

        app.Map(path, async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("WebSocket connection required.");
                return;
            }

            var ws = await context.WebSockets.AcceptWebSocketAsync();
            var pipeline = context.RequestServices.GetRequiredService<ISpeechPipeline>();
            var registry = context.RequestServices.GetRequiredService<SpeechSessionRegistry>();
            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger<SpeechWebSocketSession>();

            await using var session = new SpeechWebSocketSession(ws, pipeline, logger);
            registry.Register(session);
            try
            {
                await session.RunAsync(context.RequestAborted);
            }
            finally
            {
                registry.Unregister(session);
            }
        });

        return app;
    }
}
