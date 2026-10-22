namespace ElBruno.Speech.Stt.Parakeet;

/// <summary>A decoded SentencePiece token with its native 80 ms encoder-frame start.
/// The runtime does not expose token end times.</summary>
public sealed record ParakeetToken(string Text, TimeSpan Start, bool IsInPadding = false);

/// <summary>Available in SpeechToTextResponse.RawRepresentation. No inferred word/segment
/// intervals or language labels are manufactured from token starts.</summary>
public sealed record ParakeetTranscript(string Text, IReadOnlyList<ParakeetToken> Tokens)
{
    public string? DetectedLanguage => null;
    public bool HasWordIntervals => false;
    public bool HasSegmentIntervals => false;
    public string TimingProvenance => "Native TDT token starts, 80 ms resolution; no end times. IsInPadding flags starts on chunk padding.";
}
