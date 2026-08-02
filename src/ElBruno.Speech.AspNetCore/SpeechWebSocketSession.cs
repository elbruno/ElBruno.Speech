using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ElBruno.Speech;
using ElBruno.Speech.Pipeline;

namespace ElBruno.Speech.AspNetCore;

/// <summary>
/// Manages the lifecycle of a single WebSocket speech session.
/// Receives binary PCM audio from the client, runs it through the pipeline,
/// and sends TTS audio frames and JSON event messages back.
/// </summary>
public sealed class SpeechWebSocketSession : IAsyncDisposable
{
    private readonly WebSocket _ws;
    private readonly ISpeechPipeline _pipeline;
    private readonly ILogger _logger;
    private readonly string _sessionId;
    private bool _disposed;

    public SpeechWebSocketSession(
        WebSocket ws,
        ISpeechPipeline pipeline,
        ILogger logger)
    {
        _ws = ws;
        _pipeline = pipeline;
        _logger = logger;
        _sessionId = Guid.NewGuid().ToString("N")[..8];
    }

    public string SessionId => _sessionId;

    /// <summary>
    /// Runs the WebSocket session to completion.
    /// Returns when the client disconnects or cancellation is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("WebSocket session {SessionId} started.", _sessionId);

        var receiveChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true,
        });

        var sendChannel = Channel.CreateBounded<(byte[]? Pcm, string? Json)>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = true,
        });

        await using var audioInput = new WebSocketAudioInput(receiveChannel.Reader, AudioFormat.Pcm16KhzMono);
        await using var audioOutput = new WebSocketAudioOutput(sendChannel.Writer);

        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = sessionCts.Token;

        var receiveTask = ReceiveLoopAsync(receiveChannel.Writer, sessionCts, token);
        var sendTask = SendLoopAsync(sendChannel.Reader, token);
        var pipelineTask = _pipeline.RunAsync(audioInput, audioOutput, token);

        try
        {
            await receiveTask;
            await sessionCts.CancelAsync();
            await Task.WhenAll(pipelineTask, sendTask).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogDebug("Session {SessionId}: client disconnected abruptly.", _sessionId);
        }
        finally
        {
            await SendJsonAsync(sendChannel.Writer, new { type = "done" }, CancellationToken.None)
                .ConfigureAwait(false);
            sendChannel.Writer.TryComplete();
            _logger.LogInformation("WebSocket session {SessionId} ended.", _sessionId);
        }
    }

    private async Task ReceiveLoopAsync(
        ChannelWriter<byte[]> writer,
        CancellationTokenSource sessionCts,
        CancellationToken token)
    {
        var buffer = new byte[65536];
        try
        {
            while (_ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(buffer.AsMemory(), token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
                {
                    var chunk = buffer[..result.Count].ToArray();
                    writer.TryWrite(chunk);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            writer.TryComplete();
            await sessionCts.CancelAsync();
        }
    }

    private async Task SendLoopAsync(
        ChannelReader<(byte[]? Pcm, string? Json)> reader,
        CancellationToken token)
    {
        try
        {
            await foreach (var (pcm, json) in reader.ReadAllAsync(token))
            {
                if (_ws.State != WebSocketState.Open) break;

                if (pcm is not null)
                {
                    await _ws.SendAsync(
                        pcm.AsMemory(),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        token);
                }
                else if (json is not null)
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await _ws.SendAsync(
                        bytes.AsMemory(),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        token);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    private static async Task SendJsonAsync(
        ChannelWriter<(byte[]?, string?)> writer,
        object payload,
        CancellationToken token)
    {
        var json = JsonSerializer.Serialize(payload);
        await writer.WriteAsync((null, json), token).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
