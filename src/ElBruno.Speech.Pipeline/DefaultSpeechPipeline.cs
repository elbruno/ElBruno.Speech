using System.Threading.Channels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ElBruno.Speech;
using ElBruno.Speech.Audio;

namespace ElBruno.Speech.Pipeline;

/// <summary>
/// Default implementation of <see cref="ISpeechPipeline"/>.
/// Orchestrates VAD → STT → LLM → TTS using bounded <see cref="Channel{T}"/> stages.
/// </summary>
public sealed class DefaultSpeechPipeline : ISpeechPipeline
{
    private readonly ISpeechToTextClient _stt;
    private readonly IChatClient _llm;
    private readonly ITextToSpeechClient _tts;
    private readonly IVadClient? _vad;
    private readonly SpeechPipelineOptions _options;
    private readonly ILogger<DefaultSpeechPipeline> _logger;

    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private bool _disposed;

    public DefaultSpeechPipeline(
        ISpeechToTextClient stt,
        IChatClient llm,
        ITextToSpeechClient tts,
        SpeechPipelineOptions? options = null,
        IVadClient? vad = null,
        ILogger<DefaultSpeechPipeline>? logger = null)
    {
        _stt = stt;
        _llm = llm;
        _tts = tts;
        _vad = vad;
        _options = options ?? new SpeechPipelineOptions();
        _logger = logger ?? NullLogger<DefaultSpeechPipeline>.Instance;
    }

    /// <inheritdoc/>
    public async Task RunAsync(IAudioInput input, IAudioOutput output, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _runCts.Token;

        // Audio → VAD channel (drop oldest on overflow)
        var audioChannel = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(_options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        });

        // VAD output → STT (backpressure: writer waits when full)
        var utteranceChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(4)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });

        // STT → LLM
        var transcriptChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(4)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });

        // LLM → TTS
        var responseChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(8)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });

        try
        {
            _runTask = Task.WhenAll(
                ProduceAudioAsync(input, audioChannel.Writer, token),
                RunVadStageAsync(audioChannel.Reader, utteranceChannel.Writer, token),
                RunSttStageAsync(utteranceChannel.Reader, transcriptChannel.Writer, token),
                RunLlmStageAsync(transcriptChannel.Reader, responseChannel.Writer, token),
                RunTtsStageAsync(responseChannel.Reader, output, token)
            );
            await _runTask;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _logger.LogDebug("Pipeline stopped via cancellation.");
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_runCts is null) return;
        await _runCts.CancelAsync();
        if (_runTask is not null)
        {
            try { await _runTask.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { }
            catch (AggregateException) { }
        }
    }

    // ── Stage 1: Read frames from IAudioInput → audio channel ──────────────

    private static async Task ProduceAudioAsync(
        IAudioInput input,
        ChannelWriter<AudioFrame> writer,
        CancellationToken token)
    {
        try
        {
            await foreach (var frame in input.ReadFramesAsync(token))
                await writer.WriteAsync(frame, token);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    // ── Stage 2: VAD — accumulate frames, emit utterance bytes ─────────────

    private async Task RunVadStageAsync(
        ChannelReader<AudioFrame> reader,
        ChannelWriter<byte[]> writer,
        CancellationToken token)
    {
        var utteranceBytes = new List<byte[]>();
        bool inSpeech = false;

        try
        {
            await foreach (var frame in reader.ReadAllAsync(token))
            {
                bool isSpeech;
                if (_vad is not null)
                {
                    var result = await _vad.ProcessFrameAsync(frame, token);
                    isSpeech = result.State == VoiceActivityState.Speech;
                }
                else
                {
                    // No VAD: treat all audio as speech, flush on IsFinal
                    isSpeech = true;
                }

                if (isSpeech)
                {
                    inSpeech = true;
                    utteranceBytes.Add(frame.Data.ToArray());
                }
                else if (inSpeech)
                {
                    // Silence after speech → emit utterance
                    inSpeech = false;
                    var pcm = MergeChunks(utteranceBytes);
                    utteranceBytes.Clear();
                    _logger.LogDebug("VAD: emitting utterance of {Bytes} bytes.", pcm.Length);
                    await writer.WriteAsync(pcm, token);
                }

                // No-VAD passthrough: flush on IsFinal
                if (_vad is null && frame.IsFinal && utteranceBytes.Count > 0)
                {
                    var pcm = MergeChunks(utteranceBytes);
                    utteranceBytes.Clear();
                    await writer.WriteAsync(pcm, token);
                }
            }

            // Flush any remaining speech at end-of-stream
            if (utteranceBytes.Count > 0)
            {
                var pcm = MergeChunks(utteranceBytes);
                await writer.WriteAsync(pcm, token);
            }
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static byte[] MergeChunks(List<byte[]> chunks)
    {
        int total = chunks.Sum(c => c.Length);
        var result = new byte[total];
        int offset = 0;
        foreach (var c in chunks) { c.CopyTo(result, offset); offset += c.Length; }
        return result;
    }

    // ── Stage 3: STT ───────────────────────────────────────────────────────

    private static readonly TimeSpan SttTimeout = TimeSpan.FromSeconds(30);

    private async Task RunSttStageAsync(
        ChannelReader<byte[]> reader,
        ChannelWriter<string> writer,
        CancellationToken token)
    {
        try
        {
            await foreach (var pcm in reader.ReadAllAsync(token))
            {
                string? transcript = null;
                try
                {
                    using var stream = new MemoryStream(pcm, writable: false);
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeoutCts.CancelAfter(SttTimeout);

                    var sttTask = _stt.GetTextAsync(stream, cancellationToken: timeoutCts.Token);
                    var completed = await Task.WhenAny(sttTask, Task.Delay(SttTimeout, token));
                    if (completed != sttTask)
                    {
                        _logger.LogWarning("STT timed out after {Timeout}s — skipping utterance.", SttTimeout.TotalSeconds);
                        continue;
                    }
                    var response = await sttTask;
                    transcript = response.Text?.Trim();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "STT provider threw an exception — skipping utterance.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(transcript))
                {
                    _logger.LogDebug("STT: \"{Transcript}\"", transcript);
                    await writer.WriteAsync(transcript, token);
                }
            }
        }
        finally
        {
            writer.TryComplete();
        }
    }

    // ── Stage 4: LLM ───────────────────────────────────────────────────────

    private async Task RunLlmStageAsync(
        ChannelReader<string> reader,
        ChannelWriter<string> writer,
        CancellationToken token)
    {
        try
        {
            await foreach (var transcript in reader.ReadAllAsync(token))
            {
                string? text = null;
                try
                {
                    var messages = new[] { new ChatMessage(ChatRole.User, transcript) };
                    var response = await _llm.GetResponseAsync(messages, cancellationToken: token);
                    text = response.Text?.Trim();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LLM provider threw an exception — skipping transcript.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogDebug("LLM: \"{Response}\"", text);
                    await writer.WriteAsync(text, token);
                }
            }
        }
        finally
        {
            writer.TryComplete();
        }
    }

    // ── Stage 5: TTS ───────────────────────────────────────────────────────

    private int _generationId;

    private async Task RunTtsStageAsync(
        ChannelReader<string> reader,
        IAudioOutput output,
        CancellationToken token)
    {
        long seqNum = 0;

        await foreach (var text in reader.ReadAllAsync(token))
        {
            int genId = Interlocked.Increment(ref _generationId);
            _logger.LogDebug("TTS gen {GenId}: \"{Text}\"", genId, text);

            foreach (var segment in TextSegmenter.Segment(text))
            {
                if (token.IsCancellationRequested) break;
                if (genId != _generationId) break; // stale — barge-in occurred

                byte[]? audioBytes = null;
                try
                {
                    var ttsResponse = await _tts.GetAudioAsync(segment, cancellationToken: token);
                    audioBytes = ttsResponse.RawRepresentation as byte[];
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "TTS provider threw an exception — skipping segment.");
                    continue;
                }

                if (audioBytes is { Length: > 0 })
                {
                    var frame = new AudioFrame(
                        Data: audioBytes,
                        Format: _options.TargetFormat,
                        SequenceNumber: seqNum++,
                        Timestamp: TimeSpan.FromSeconds((double)seqNum * audioBytes.Length / _options.TargetFormat.BytesPerSecond));
                    await output.WriteAsync(frame, token);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_runCts is not null)
        {
            await _runCts.CancelAsync();
            _runCts.Dispose();
        }
    }
}
