using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ElBruno.Speech.Audio;
using Microsoft.Extensions.AI;

namespace ElBruno.Speech.Stt.Parakeet;

/// <summary>Offline Parakeet v3 transcription. Streaming returns a final result only.
/// Model downloads are explicit, never triggered by construction or inference.</summary>
public sealed class ParakeetSpeechToTextClient : ISpeechToTextClient
{
    private readonly ParakeetOptions _options;
    private readonly Func<float[], CancellationToken, IReadOnlyList<ParakeetToken>> _recognize;
    private readonly IDisposable? _backend;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public ParakeetSpeechToTextClient(ParakeetOptions? options = null)
    {
        _options = (options ?? new ParakeetOptions()).Snapshot();
        var backend = new OnnxParakeet(_options.ModelDirectory);
        _backend = backend;
        _recognize = backend.Recognize;
    }

    internal ParakeetSpeechToTextClient(ParakeetOptions options,
        Func<float[], CancellationToken, IReadOnlyList<ParakeetToken>> recognize)
    {
        _options = options.Snapshot();
        _recognize = recognize;
    }

    public SpeechToTextClientMetadata Metadata { get; } = new(
        "ElBruno.Speech.Stt.Parakeet", new Uri("https://huggingface.co/" + ParakeetModel.Repository),
        ParakeetModel.ModelId);

    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioStream,
        SpeechToTextOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioStream);
        ValidateRequest(options);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var data = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = await audioStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (data.Length + read > _options.MaxInputBytes)
                    throw new InvalidDataException($"Audio exceeds MaxInputBytes ({_options.MaxInputBytes}).");
                data.Write(buffer, 0, read);
            }
            var pcm = ReadPcm(data.ToArray(), _options.InputFormat);
            var tokens = await Task.Run(() => Transcribe(pcm, cancellationToken), cancellationToken).ConfigureAwait(false);
            string text = DecodeText(tokens);
            var result = new ParakeetTranscript(text, tokens.AsReadOnly());
            return new SpeechToTextResponse(text)
            {
                ModelId = ParakeetModel.ModelId,
                RawRepresentation = result
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    private List<ParakeetToken> Transcribe(byte[] pcm, CancellationToken cancellationToken)
    {
        var result = new List<ParakeetToken>();
        int chunkBytes = _options.ChunkSeconds * 32000;
        for (int offset = 0; offset < pcm.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = Math.Min(chunkBytes, pcm.Length - offset);
            // The STFT preprocessor needs at least one analysis window.
            float[] samples = new float[Math.Max(512, count / 2)];
            PcmConverter.Int16ToFloat32(pcm.AsSpan(offset, count), samples);
            TimeSpan start = TimeSpan.FromSeconds(offset / 32000.0);
            TimeSpan nativeLimit = TimeSpan.FromSeconds(Math.Ceiling(samples.Length / 1280.0) * 0.08);
            foreach (var token in _recognize(samples, cancellationToken))
            {
                if (token.Start < TimeSpan.Zero || token.Start > nativeLimit)
                    throw new InvalidDataException("A native token start falls outside the padded encoder frames.");
                result.Add(token with
                {
                    Start = token.Start + start,
                    IsInPadding = token.Start.TotalSeconds >= count / 32000.0
                });
            }
            offset += count;
        }
        return result;
    }

    internal static string DecodeText(IEnumerable<ParakeetToken> tokens)
    {
        string text = string.Concat(tokens.Select(token => token.Text)).Replace('\u2581', ' ');
        return Regex.Replace(text, @"\A\s|\s\B|(\s)\b", match => match.Groups[1].Success ? " " : "").Trim();
    }

    private static void ValidateRequest(SpeechToTextOptions? options)
    {
        if (options?.ModelId is { } model && model != ParakeetModel.ModelId)
            throw new NotSupportedException($"This client only supports {ParakeetModel.ModelId}.");
        if (options?.SpeechSampleRate is { } rate && rate != 16000)
            throw new NotSupportedException("Resample audio to 16 kHz before transcription.");
        if (options?.SpeechLanguage is not null || options?.TextLanguage is not null)
            throw new NotSupportedException("Parakeet automatically selects its language; language hints and translation are not supported.");
    }

    internal static byte[] ReadPcm(byte[] data, ParakeetInputFormat format)
    {
        ReadOnlyMemory<byte> pcm = data;
        if (format == ParakeetInputFormat.Wav)
        {
            if (data.Length < 44 || !data.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
                !data.AsSpan(8, 4).SequenceEqual("WAVE"u8) ||
                BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4)) != data.Length - 8)
                throw new InvalidDataException("Expected a complete RIFF/WAVE PCM16 file.");
            bool foundFormat = false, foundData = false;
            int offset = 12;
            while (offset <= data.Length - 8)
            {
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 4));
                if (length > data.Length - offset - 8)
                    throw new InvalidDataException("Truncated WAV chunk.");
                var chunk = data.AsSpan(offset, 4);
                if (chunk.SequenceEqual("fmt "u8))
                {
                    if (foundFormat || length < 16 ||
                        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 8)) != 1 ||
                        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 10)) != 1 ||
                        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 12)) != 16000 ||
                        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 16)) != 32000 ||
                        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 20)) != 2 ||
                        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 22)) != 16)
                        throw new InvalidDataException("WAV input must be mono 16 kHz PCM16.");
                    foundFormat = true;
                }
                if (chunk.SequenceEqual("data"u8))
                {
                    if (!foundFormat || foundData)
                        throw new InvalidDataException("Expected one data chunk after fmt.");
                    foundData = true;
                }
                long next = offset + 8L + length + (length & 1);
                if (next > data.Length)
                    throw new InvalidDataException("Missing WAV padding byte.");
                offset = checked((int)next);
            }
            if (!foundFormat || !foundData || offset != data.Length)
                throw new InvalidDataException("Incomplete WAV structure.");
            pcm = WavReader.ParseWav(data).Samples;
        }
        else if (data.AsSpan().StartsWith("RIFF"u8))
        {
            throw new InvalidDataException("Select ParakeetInputFormat.Wav for WAV-wrapped input.");
        }
        if ((pcm.Length & 1) != 0)
            throw new InvalidDataException("PCM16 input contains an incomplete sample.");
        return pcm.ToArray();
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(Stream audioStream,
        SpeechToTextOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetTextAsync(audioStream, options, cancellationToken).ConfigureAwait(false);
        foreach (var update in response.ToSpeechToTextResponseUpdates())
            yield return update;
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
