using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace ElBruno.Speech.Tts.Ollama;

/// <summary>Generates speech with Ollama's OpenAI-compatible Phonon endpoint.</summary>
public sealed class OllamaSpeechClient : ITextToSpeechClient
{
    private const int MaxTextCharacters = 4096;
    private const int ErrorBodyLimit = 1024;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly OllamaSpeechOptions _options;
    private bool _disposed;

    /// <summary>Creates a client that owns its HTTP transport.</summary>
    public OllamaSpeechClient(OllamaSpeechOptions? options = null)
        : this(new HttpClient(), options, ownsHttpClient: true)
    {
    }

    /// <summary>Creates a client using a caller-owned HTTP transport.</summary>
    public OllamaSpeechClient(HttpClient httpClient, OllamaSpeechOptions? options = null)
        : this(httpClient, options, ownsHttpClient: false)
    {
    }

    private OllamaSpeechClient(HttpClient httpClient, OllamaSpeechOptions? options, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _options = (options ?? new OllamaSpeechOptions()).Snapshot();
        Metadata = new("Ollama", new Uri("https://ollama.com"), _options.Model);
    }

    /// <inheritdoc />
    public TextToSpeechClientMetadata Metadata { get; }

    /// <inheritdoc />
    public async Task<TextToSpeechResponse> GetAudioAsync(string text,
        TextToSpeechOptions? options = null, CancellationToken cancellationToken = default)
    {
        var requestOptions = ResolveRequest(text, options);
        using var response = await SendAsync(requestOptions, cancellationToken).ConfigureAwait(false);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var audio = new MemoryStream();
        await responseStream.CopyToAsync(audio, cancellationToken).ConfigureAwait(false);
        var bytes = audio.ToArray();
        var content = new DataContent(bytes, requestOptions.MediaType);
        return new TextToSpeechResponse([content])
        {
            ModelId = requestOptions.Model,
            RawRepresentation = bytes
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(string text,
        TextToSpeechOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestOptions = ResolveRequest(text, options);
        using var response = await SendAsync(requestOptions, cancellationToken).ConfigureAwait(false);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[8192];
        while (true)
        {
            var count = await responseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0) yield break;

            var chunk = new byte[count];
            Buffer.BlockCopy(buffer, 0, chunk, 0, count);
            yield return new TextToSpeechResponseUpdate([new DataContent(chunk, requestOptions.MediaType)])
            {
                Kind = TextToSpeechResponseUpdateKind.AudioUpdated,
                ModelId = requestOptions.Model,
                RawRepresentation = chunk
            };
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null) return null;
        if (serviceType == typeof(TextToSpeechClientMetadata)) return Metadata;
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private RequestOptions ResolveRequest(string text, TextToSpeechOptions? options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(text);
        var textLength = CountCharacters(text);
        if (textLength > MaxTextCharacters)
            throw new ArgumentOutOfRangeException(nameof(text), $"Ollama speech input cannot exceed {MaxTextCharacters} characters.");

        if (options is not null)
        {
            if (options.Language is not null || options.Speed is not null || options.Pitch is not null || options.Volume is not null)
                throw new NotSupportedException("Ollama Phonon does not support language, speed, pitch, or volume options.");
            if (options.AdditionalProperties is { Count: > 0 })
                throw new NotSupportedException("Ollama Phonon does not support additional text-to-speech options.");
            if (options.RawRepresentationFactory is not null)
                throw new NotSupportedException("Ollama Phonon does not support a raw request factory.");
        }

        string model = options?.ModelId ?? _options.Model;
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        string? voice = options?.VoiceId ?? _options.Voice;
        if (voice is not null) ArgumentException.ThrowIfNullOrWhiteSpace(voice);

        var format = options?.AudioFormat is { } audioFormat
            ? ParseAudioFormat(audioFormat)
            : _options.AudioFormat;
        return format switch
        {
            OllamaSpeechAudioFormat.Wav => new(_options.Endpoint, text, model, voice, "wav", "audio/wav"),
            OllamaSpeechAudioFormat.Pcm => new(_options.Endpoint, text, model, voice, "pcm", "audio/pcm"),
            _ => throw new ArgumentOutOfRangeException(nameof(options), "Unsupported Ollama audio format.")
        };
    }

    private async Task<HttpResponseMessage> SendAsync(RequestOptions options, CancellationToken cancellationToken)
    {
        var payload = new SpeechRequest(options.Model, options.Input, options.Voice, options.ResponseFormat);
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.IsSuccessStatusCode) return response;

        var statusCode = response.StatusCode;
        string details;
        using (response)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            byte[] buffer = new byte[ErrorBodyLimit + 1];
            int count = 0;
            while (count < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(count), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                count += read;
            }
            details = Encoding.UTF8.GetString(buffer, 0, Math.Min(count, ErrorBodyLimit));
            if (count > ErrorBodyLimit) details += "...";
        }
        var message = $"Ollama speech request failed with HTTP {(int)statusCode} ({statusCode})";
        if (!string.IsNullOrWhiteSpace(details)) message += $": {details}";
        throw new HttpRequestException(message, null, statusCode);
    }

    private static OllamaSpeechAudioFormat ParseAudioFormat(string audioFormat) =>
        audioFormat.Trim().ToLowerInvariant() switch
        {
            "wav" or "audio/wav" => OllamaSpeechAudioFormat.Wav,
            "pcm" or "audio/pcm" => OllamaSpeechAudioFormat.Pcm,
            _ => throw new NotSupportedException($"Ollama Phonon does not support audio format '{audioFormat}'. Use wav or pcm.")
        };

    private static int CountCharacters(string text)
    {
        int count = 0;
        foreach (var _ in text.EnumerateRunes())
        {
            if (++count > MaxTextCharacters) return count;
        }
        return count;
    }

    private sealed record RequestOptions(
        Uri Endpoint, string Input, string Model, string? Voice, string ResponseFormat, string MediaType);

    private sealed record SpeechRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("voice"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Voice,
        [property: JsonPropertyName("response_format")] string ResponseFormat);
}
