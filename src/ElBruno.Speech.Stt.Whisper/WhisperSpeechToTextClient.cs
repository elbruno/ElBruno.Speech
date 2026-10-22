using System.Runtime.CompilerServices;
using ElBruno.Speech.Audio;
using Microsoft.Extensions.AI;

namespace ElBruno.Speech.Stt.Whisper;

/// <summary>Explicit opt-in local Whisper large-v3 CPU client. Streaming emits final-only updates.
/// Cancellation is cooperative at encoder boundaries; native work is joined before return/disposal.</summary>
public sealed class WhisperSpeechToTextClient : ISpeechToTextClient
{
    private readonly WhisperOptions _options;
    private readonly Func<float[], CancellationToken, IReadOnlyList<WhisperSegment>> _recognize;
    private readonly IDisposable? _backend;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public WhisperSpeechToTextClient(WhisperOptions? options = null)
    {
        _options = (options ?? new WhisperOptions()).Snapshot();
        var backend = new NativeWhisper(_options);
        _backend = backend;
        _recognize = backend.Recognize;
    }

    internal WhisperSpeechToTextClient(WhisperOptions options,
        Func<float[], CancellationToken, IReadOnlyList<WhisperSegment>> recognize, IDisposable? backend = null)
    {
        _options = options.Snapshot();
        _recognize = recognize;
        _backend = backend;
    }

    public SpeechToTextClientMetadata Metadata { get; } = new(
        "ElBruno.Speech.Stt.Whisper", new Uri("https://huggingface.co/" + WhisperModel.Repository),
        WhisperModel.ModelId);

    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioStream,
        SpeechToTextOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioStream);
        ValidateRequest(options);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var pcm = await Pcm16AudioInput.ReadAsync(audioStream, _options.InputFormat,
                _options.MaxInputBytes, cancellationToken).ConfigureAwait(false);
            var samples = new float[pcm.Length / 2];
            PcmConverter.Int16ToFloat32(pcm, samples);
            var native = samples.Length == 0 ? [] :
                await Task.Run(() => _recognize(samples, cancellationToken), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var segments = native.Select(segment =>
            {
                if (segment.Start < TimeSpan.Zero || segment.End < segment.Start)
                    throw new InvalidDataException("Whisper returned invalid segment intervals.");
                return segment with { ExtendsPastInput = segment.End.TotalSeconds > samples.Length / 16000.0 };
            }).ToList().AsReadOnly();
            string text = string.Concat(segments.Select(s => s.Text)).Trim();
            return new SpeechToTextResponse(text)
            {
                ModelId = WhisperModel.ModelId, RawRepresentation = new WhisperTranscript(text, segments)
            };
        }
        finally { _gate.Release(); }
    }

    private static void ValidateRequest(SpeechToTextOptions? options)
    {
        if (options?.ModelId is { } model && model != WhisperModel.ModelId)
            throw new NotSupportedException($"This client only supports {WhisperModel.ModelId}.");
        if (options?.SpeechSampleRate is { } rate && rate != 16000)
            throw new NotSupportedException("Resample audio to 16 kHz before transcription.");
        if (options?.SpeechLanguage is not null || options?.TextLanguage is not null)
            throw new NotSupportedException("This provider uses automatic language detection; hints and translation are not exposed.");
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(Stream audioStream,
        SpeechToTextOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetTextAsync(audioStream, options, cancellationToken).ConfigureAwait(false);
        foreach (var update in response.ToSpeechToTextResponseUpdates()) yield return update;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null) return null;
        if (serviceType == typeof(SpeechToTextClientMetadata)) return Metadata;
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        _gate.Wait();
        try
        {
            if (_disposed) return;
            _disposed = true;
            _backend?.Dispose();
        }
        finally { _gate.Release(); }
    }
}
