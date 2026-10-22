using ElBruno.Speech.TestUtils;
using Xunit;

namespace ElBruno.Speech.Fixtures.Tests;

public sealed class TranscriptionScorerTests
{
    [Theory]
    [InlineData("¡ÁRBOL, niño!  café.", "árbol niño café")]
    [InlineData("cafe\u0301", "café")]
    [InlineData("one\t two\nthree", "one two three")]
    [InlineData("don't re-enter", "don t re enter")]
    public void NormalizationIsExplicitAndPreservesAccents(string input, string expected) =>
        Assert.Equal(expected, TranscriptionScorer.Normalize(input));

    [Theory]
    [InlineData("one two", "one three", 1, 0, 0)]
    [InlineData("one two", "one", 0, 1, 0)]
    [InlineData("one", "one two", 0, 0, 1)]
    [InlineData("", "extra", 0, 0, 1)]
    [InlineData("missing", "", 0, 1, 0)]
    [InlineData("", "", 0, 0, 0)]
    public void CountsEditTypes(string reference, string hypothesis, int substitutions, int deletions, int insertions)
    {
        var words = TranscriptionScorer.Score(reference, hypothesis).Words;
        Assert.Equal(substitutions, words.Substitutions);
        Assert.Equal(deletions, words.Deletions);
        Assert.Equal(insertions, words.Insertions);
        Assert.Equal(reference.Length == 0 ? null : (double?)words.Errors / words.ReferenceCount, words.Rate);
    }

    [Fact]
    public void CerUsesUnicodeScalarsAndExcludesSpaces()
    {
        var score = TranscriptionScorer.Score("niño café", "nino cafe");
        Assert.Equal(2, score.Characters.Substitutions);
        Assert.Equal(8, score.Characters.ReferenceCount);
        Assert.Equal(0.25, score.Characters.Rate);
        Assert.Equal(1, TranscriptionScorer.Score("\U00010428", "\U00010428").Characters.ReferenceCount);
    }

    [Fact]
    public void CorpusRatesUseSummedCountsNotMeanPercentages()
    {
        var sum = ErrorCounts.Sum([
            TranscriptionScorer.Score("one", "two").Words,
            TranscriptionScorer.Score("one two three", "one two three").Words
        ]);
        Assert.Equal(4, sum.ReferenceCount);
        Assert.Equal(0.25, sum.Rate);
    }
}
