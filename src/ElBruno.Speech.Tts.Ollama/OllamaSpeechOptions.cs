namespace ElBruno.Speech.Tts.Ollama;

/// <summary>The audio formats supported by Ollama's speech endpoint.</summary>
public enum OllamaSpeechAudioFormat
{
    /// <summary>16-bit mono PCM audio in a WAV container at 24 kHz.</summary>
    Wav,

    /// <summary>Raw 16-bit little-endian mono PCM audio at 24 kHz.</summary>
    Pcm
}

/// <summary>Configuration for the Ollama Phonon text-to-speech client.</summary>
public sealed class OllamaSpeechOptions
{
    /// <summary>Gets or sets the Ollama server URL, OpenAI-compatible API base, or complete speech endpoint.</summary>
    /// <remarks>Host roots, paths ending in /v1, and paths ending in /v1/audio are completed automatically.
    /// Complete speech endpoints and other custom endpoint paths are supported without adding a route.</remarks>
    public Uri Endpoint { get; set; } = new("http://localhost:11434/");

    /// <summary>Gets or sets the model identifier. Defaults to <c>phonon</c>.</summary>
    public string Model { get; set; } = "phonon";

    /// <summary>Gets or sets the voice, or null to use the model's default voice.</summary>
    public string? Voice { get; set; }

    /// <summary>Gets or sets the response audio format.</summary>
    public OllamaSpeechAudioFormat AudioFormat { get; set; } = OllamaSpeechAudioFormat.Wav;

    internal OllamaSpeechOptions Snapshot()
    {
        ArgumentNullException.ThrowIfNull(Endpoint);
        if (!Endpoint.IsAbsoluteUri || (Endpoint.Scheme != Uri.UriSchemeHttp && Endpoint.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Endpoint must be an absolute HTTP or HTTPS URI.", nameof(Endpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(Model);
        if (Voice is not null) ArgumentException.ThrowIfNullOrWhiteSpace(Voice);
        if (!Enum.IsDefined(AudioFormat)) throw new ArgumentOutOfRangeException(nameof(AudioFormat));

        return new OllamaSpeechOptions
        {
            Endpoint = ResolveSpeechEndpoint(Endpoint),
            Model = Model,
            Voice = Voice,
            AudioFormat = AudioFormat
        };
    }

    private static Uri ResolveSpeechEndpoint(Uri endpoint)
    {
        string path = endpoint.AbsolutePath.TrimEnd('/');
        string? completedPath = path switch
        {
            "" => "/v1/audio/speech",
            _ when path.EndsWith("/v1", StringComparison.Ordinal) => path + "/audio/speech",
            _ when path.EndsWith("/v1/audio", StringComparison.Ordinal) => path + "/speech",
            _ when path.EndsWith("/v1/audio/speech", StringComparison.Ordinal) => path,
            _ => null
        };

        return completedPath is null ? endpoint : new UriBuilder(endpoint) { Path = completedPath }.Uri;
    }
}
