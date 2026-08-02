using System.Diagnostics.Metrics;

namespace ElBruno.Speech.OpenTelemetry;

/// <summary>
/// OpenTelemetry metrics for the ElBruno.Speech pipeline.
/// Meter name: <c>ElBruno.Speech</c>
/// </summary>
public static class SpeechPipelineMetrics
{
    /// <summary>OpenTelemetry meter name.</summary>
    public const string MeterName = "ElBruno.Speech";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    /// <summary>Number of VAD frames classified as speech.</summary>
    public static readonly Counter<long> SpeechFrames =
        _meter.CreateCounter<long>("speech.vad.speech_frames", "frames", "VAD frames classified as speech.");

    /// <summary>Number of VAD frames classified as silence.</summary>
    public static readonly Counter<long> SilenceFrames =
        _meter.CreateCounter<long>("speech.vad.silence_frames", "frames", "VAD frames classified as silence.");

    /// <summary>Number of utterances sent to STT.</summary>
    public static readonly Counter<long> UtterancesTranscribed =
        _meter.CreateCounter<long>("speech.stt.utterances", "utterances", "Utterances sent to ISpeechToTextClient.");

    /// <summary>Number of LLM turns completed.</summary>
    public static readonly Counter<long> LlmTurns =
        _meter.CreateCounter<long>("speech.llm.turns", "turns", "Turns sent to IChatClient.");

    /// <summary>Number of TTS segments synthesized.</summary>
    public static readonly Counter<long> TtsSegments =
        _meter.CreateCounter<long>("speech.tts.segments", "segments", "Segments sent to ITextToSpeechClient.");

    /// <summary>Histogram of STT latency in milliseconds.</summary>
    public static readonly Histogram<double> SttLatencyMs =
        _meter.CreateHistogram<double>("speech.stt.latency_ms", "ms", "ISpeechToTextClient call duration.");

    /// <summary>Histogram of TTS latency in milliseconds.</summary>
    public static readonly Histogram<double> TtsLatencyMs =
        _meter.CreateHistogram<double>("speech.tts.latency_ms", "ms", "ITextToSpeechClient call duration.");
}
