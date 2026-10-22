namespace ElBruno.Speech.Stt.Whisper;

/// <summary>Unreviewed native segment predictions, not forced alignment or reviewed word timestamps.</summary>
public sealed record WhisperSegment(string Text, TimeSpan Start, TimeSpan End,
    string? DetectedLanguage, bool ExtendsPastInput = false);

public sealed record WhisperTranscript(string Text, IReadOnlyList<WhisperSegment> Segments)
{
    public bool HasWordIntervals => false;
    public bool HasSegmentIntervals => true;
    public string TimingProvenance => "native-whisper.cpp-segment-predictions-unreviewed";
    public string? DetectedLanguage
    {
        get
        {
            var languages = Segments.Select(s => s.DetectedLanguage).Distinct().ToArray();
            return languages.Length == 1 ? languages[0] : null;
        }
    }
}
