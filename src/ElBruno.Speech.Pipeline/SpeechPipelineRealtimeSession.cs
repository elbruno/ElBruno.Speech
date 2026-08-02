using System.Threading.Channels;
using Microsoft.Extensions.AI;
using ElBruno.Speech;
using ElBruno.Speech.Audio;

namespace ElBruno.Speech.Pipeline;

/// <summary>
/// An <see cref="IRealtimeClientSession"/> that routes audio through the
/// <see cref="ISpeechPipeline"/> and streams TTS output as base64-encoded
/// <see cref="OutputTextAudioRealtimeServerMessage"/> instances.
/// </summary>
public sealed class SpeechPipelineRealtimeSession : IRealtimeClientSession
{
    private const string PcmMediaType = "audio/pcm;rate=16000";

    private readonly ISpeechPipeline _pipeline;
    private readonly Channel<byte[]> _audioChannel;
    private readonly Channel<AudioFrame> _outputChannel;
    private CancellationTokenSource? _cts;
    private bool _inputCommitted;
    private bool _disposed;

    internal SpeechPipelineRealtimeSession(RealtimeSessionOptions options, ISpeechPipeline pipeline)
    {
        Options = options;
        _pipeline = pipeline;

        _audioChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(128)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true,
        });

        _outputChannel = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = true,
        });
    }

    /// <inheritdoc/>
    public RealtimeSessionOptions Options { get; }

    /// <inheritdoc/>
    public async Task SendAsync(RealtimeClientMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        switch (message)
        {
            case InputAudioBufferAppendRealtimeClientMessage append:
                var bytes = append.Content.Data.ToArray();
                if (bytes.Length > 0)
                    await _audioChannel.Writer.WriteAsync(bytes, cancellationToken);
                break;

            case InputAudioBufferCommitRealtimeClientMessage:
                if (!_inputCommitted)
                {
                    _inputCommitted = true;
                    _audioChannel.Writer.TryComplete();
                }
                break;

            default:
                // Unsupported message type — silently ignore (forward-compat)
                break;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<RealtimeServerMessage> GetStreamingResponseAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        await using var audioInput = new ChannelAudioInput(_audioChannel.Reader, AudioFormat.Pcm16KhzMono);
        await using var audioOutput = new ChannelAudioOutput(_outputChannel.Writer);

        // Run the pipeline in the background; output frames are written to _outputChannel
        var pipelineTask = _pipeline.RunAsync(audioInput, audioOutput, token)
            .ContinueWith(_ => _outputChannel.Writer.TryComplete(), TaskScheduler.Default);

        await foreach (var frame in _outputChannel.Reader.ReadAllAsync(token))
        {
            var base64Audio = Convert.ToBase64String(frame.Data.Span);
            yield return new OutputTextAudioRealtimeServerMessage(RealtimeServerMessageType.OutputAudioDelta)
            {
                Audio = base64Audio,
                Text = string.Empty,
            };
        }

        await pipelineTask;
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? key = null) => null;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _audioChannel.Writer.TryComplete();
        _outputChannel.Writer.TryComplete();
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }
        await _pipeline.DisposeAsync();
    }
}
