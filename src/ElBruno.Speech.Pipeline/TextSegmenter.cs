namespace ElBruno.Speech.Pipeline;

/// <summary>
/// Splits text into speakable segments at sentence boundaries for low-latency TTS dispatch.
/// </summary>
internal static class TextSegmenter
{
    private static readonly char[] SentenceEnds = ['.', '?', '!', '\n'];
    private const int MaxChunkLength = 200;
    private const int MinLengthForSplit = 50;

    public static IEnumerable<string> Segment(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        if (text.Length < MinLengthForSplit) { yield return text.Trim(); yield break; }

        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            bool isBoundary = Array.IndexOf(SentenceEnds, text[i]) >= 0;
            bool isMaxChunk = i - start >= MaxChunkLength;

            if ((isBoundary || isMaxChunk) && i > start)
            {
                var chunk = text.Substring(start, i - start + (isBoundary ? 1 : 0)).Trim();
                if (!string.IsNullOrWhiteSpace(chunk)) yield return chunk;
                start = i + (isBoundary ? 1 : 0);
            }
        }

        if (start < text.Length)
        {
            var tail = text.Substring(start).Trim();
            if (!string.IsNullOrWhiteSpace(tail)) yield return tail;
        }
    }
}
