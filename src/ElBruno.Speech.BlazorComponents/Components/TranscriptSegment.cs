namespace ElBruno.Speech.BlazorComponents;

public sealed record TranscriptSegment(string Text, DateTime Timestamp, bool IsFinal);
