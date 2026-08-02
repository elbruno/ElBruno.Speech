using System.Diagnostics;

namespace ElBruno.Speech.OpenTelemetry;

/// <summary>
/// OpenTelemetry activity source for the ElBruno.Speech pipeline.
/// Activity source name: <c>ElBruno.Speech</c>
/// </summary>
public static class SpeechPipelineActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string SourceName = "ElBruno.Speech";

    /// <summary>The shared <see cref="ActivitySource"/> for pipeline tracing.</summary>
    public static readonly ActivitySource Source = new(SourceName, "1.0.0");

    /// <summary>Starts an activity for a VAD processing frame.</summary>
    public static Activity? StartVadActivity() =>
        Source.StartActivity("speech.vad.process_frame");

    /// <summary>Starts an activity for an STT transcription call.</summary>
    public static Activity? StartSttActivity() =>
        Source.StartActivity("speech.stt.transcribe");

    /// <summary>Starts an activity for an LLM turn.</summary>
    public static Activity? StartLlmActivity() =>
        Source.StartActivity("speech.llm.respond");

    /// <summary>Starts an activity for a TTS synthesis call.</summary>
    public static Activity? StartTtsActivity() =>
        Source.StartActivity("speech.tts.synthesize");
}
