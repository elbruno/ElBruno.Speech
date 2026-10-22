using System.Globalization;
using System.Text;

namespace ElBruno.Speech.TestUtils;

public sealed record ErrorCounts(int Substitutions, int Deletions, int Insertions, int ReferenceCount, int HypothesisCount)
{
    public int Errors => Substitutions + Deletions + Insertions;
    public double? Rate => ReferenceCount == 0 ? null : (double)Errors / ReferenceCount;

    public static ErrorCounts Sum(IEnumerable<ErrorCounts> scores) =>
        scores.Aggregate(new ErrorCounts(0, 0, 0, 0, 0), (sum, item) =>
            new(sum.Substitutions + item.Substitutions, sum.Deletions + item.Deletions,
                sum.Insertions + item.Insertions, sum.ReferenceCount + item.ReferenceCount,
                sum.HypothesisCount + item.HypothesisCount));
}

public sealed record TranscriptionScore(ErrorCounts Words, ErrorCounts Characters);

public static class TranscriptionScorer
{
    public const string Policy = "unicode-nfc-lower-punctuation-space-v1";

    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var output = new StringBuilder();
        bool separator = false;
        foreach (var rune in text.Normalize(NormalizationForm.FormC).ToLowerInvariant().EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (Rune.IsLetterOrDigit(rune) || category is UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                if (separator && output.Length > 0) output.Append(' ');
                output.Append(rune);
                separator = false;
            }
            else
            {
                separator = true;
            }
        }
        return output.ToString();
    }

    public static TranscriptionScore Score(string reference, string hypothesis)
    {
        string normalizedReference = Normalize(reference);
        string normalizedHypothesis = Normalize(hypothesis);
        return new(
            Distance(normalizedReference.Split(' ', StringSplitOptions.RemoveEmptyEntries),
                normalizedHypothesis.Split(' ', StringSplitOptions.RemoveEmptyEntries)),
            Distance(normalizedReference.Replace(" ", "").EnumerateRunes().ToArray(),
                normalizedHypothesis.Replace(" ", "").EnumerateRunes().ToArray()));
    }

    private static ErrorCounts Distance<T>(T[] reference, T[] hypothesis)
    {
        var previous = new (int S, int D, int I)[hypothesis.Length + 1];
        for (int j = 1; j <= hypothesis.Length; j++) previous[j] = (0, 0, j);
        for (int i = 1; i <= reference.Length; i++)
        {
            var current = new (int S, int D, int I)[hypothesis.Length + 1];
            current[0] = (0, i, 0);
            for (int j = 1; j <= hypothesis.Length; j++)
            {
                var diagonal = previous[j - 1];
                if (EqualityComparer<T>.Default.Equals(reference[i - 1], hypothesis[j - 1]))
                {
                    current[j] = diagonal;
                    continue;
                }
                (int S, int D, int I) substitute = (diagonal.S + 1, diagonal.D, diagonal.I);
                var above = previous[j];
                (int S, int D, int I) delete = (above.S, above.D + 1, above.I);
                var left = current[j - 1];
                (int S, int D, int I) insert = (left.S, left.D, left.I + 1);
                // Stable ties: substitution, then deletion, then insertion.
                current[j] = new[] { substitute, delete, insert }.MinBy(c => c.S + c.D + c.I);
            }
            previous = current;
        }
        var result = previous[^1];
        return new(result.S, result.D, result.I, reference.Length, hypothesis.Length);
    }
}
