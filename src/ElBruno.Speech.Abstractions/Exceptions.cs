namespace ElBruno.Speech;

/// <summary>Base exception for ElBruno.Speech pipeline errors.</summary>
public class SpeechPipelineException : Exception
{
    /// <summary>Short machine-readable error code (e.g. "AUDIO_FORMAT_UNSUPPORTED").</summary>
    public string ErrorCode { get; }

    /// <summary>Indicates whether the error is transient and the operation can be retried.</summary>
    public bool IsTransient { get; }

    /// <summary>Session identifier, if the error is scoped to a session.</summary>
    public string? SessionId { get; }

    /// <summary>Turn identifier, if the error is scoped to a turn.</summary>
    public string? TurnId { get; }

    /// <inheritdoc/>
    public SpeechPipelineException(
        string errorCode,
        string message,
        bool isTransient = false,
        string? sessionId = null,
        string? turnId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        IsTransient = isTransient;
        SessionId = sessionId;
        TurnId = turnId;
    }
}

/// <summary>The audio format is not supported by this component.</summary>
public sealed class UnsupportedAudioFormatException(AudioFormat format, string? sessionId = null)
    : SpeechPipelineException(
        "AUDIO_FORMAT_UNSUPPORTED",
        $"Audio format {format.SampleRate} Hz {format.Channels}ch {format.SampleFormat} is not supported.",
        isTransient: false,
        sessionId: sessionId);

/// <summary>An audio buffer overflowed its bounded capacity.</summary>
public sealed class AudioBufferOverflowException(string? sessionId = null)
    : SpeechPipelineException(
        "AUDIO_BUFFER_OVERFLOW",
        "The audio buffer overflowed. The client is sending audio faster than the pipeline can process it.",
        isTransient: true,
        sessionId: sessionId);

/// <summary>The VAD model inference encountered an error.</summary>
public sealed class VadInferenceException(string message, Exception? inner = null, string? sessionId = null)
    : SpeechPipelineException("VAD_INFERENCE_ERROR", message, isTransient: true, sessionId: sessionId, innerException: inner);

/// <summary>The speech-to-text provider returned an error.</summary>
public sealed class SpeechToTextProviderException(string message, Exception? inner = null, string? sessionId = null, string? turnId = null)
    : SpeechPipelineException("STT_PROVIDER_ERROR", message, isTransient: true, sessionId: sessionId, turnId: turnId, innerException: inner);

/// <summary>The chat provider returned an error.</summary>
public sealed class ChatProviderException(string message, Exception? inner = null, string? sessionId = null, string? turnId = null)
    : SpeechPipelineException("CHAT_PROVIDER_ERROR", message, isTransient: true, sessionId: sessionId, turnId: turnId, innerException: inner);

/// <summary>The text-to-speech provider returned an error.</summary>
public sealed class TextToSpeechProviderException(string message, Exception? inner = null, string? sessionId = null, string? turnId = null)
    : SpeechPipelineException("TTS_PROVIDER_ERROR", message, isTransient: true, sessionId: sessionId, turnId: turnId, innerException: inner);

/// <summary>The session registry has reached its maximum capacity.</summary>
public sealed class SpeechSessionCapacityException(int maxSessions)
    : SpeechPipelineException(
        "SESSION_CAPACITY_EXCEEDED",
        $"Cannot create a new session: the maximum of {maxSessions} concurrent sessions has been reached.",
        isTransient: true);

/// <summary>The session has been disposed and can no longer accept input.</summary>
public sealed class SpeechSessionDisposedException(string sessionId)
    : SpeechPipelineException("SESSION_DISPOSED", "The session has been disposed.", sessionId: sessionId);

/// <summary>A provider does not support the requested capability.</summary>
public sealed class SpeechProviderCapabilityException(string capability, string? sessionId = null)
    : SpeechPipelineException(
        "PROVIDER_CAPABILITY_UNSUPPORTED",
        $"The provider does not support '{capability}'.",
        sessionId: sessionId);

/// <summary>A WebSocket protocol message was malformed or violated the session protocol.</summary>
public sealed class SpeechProtocolException(string message, string? sessionId = null)
    : SpeechPipelineException("PROTOCOL_ERROR", message, sessionId: sessionId);
