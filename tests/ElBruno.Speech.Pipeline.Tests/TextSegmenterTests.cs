using ElBruno.Speech.Pipeline;
using FluentAssertions;

namespace ElBruno.Speech.Pipeline.Tests;

public sealed class TextSegmenterTests
{
    [Fact]
    public void Segment_ShortText_ReturnsSingleChunk()
    {
        var result = TextSegmenter.Segment("Hello world.").ToList();
        result.Should().HaveCount(1);
        result[0].Should().Be("Hello world.");
    }

    [Fact]
    public void Segment_EmptyText_ReturnsEmpty()
    {
        TextSegmenter.Segment("").Should().BeEmpty();
        TextSegmenter.Segment("   ").Should().BeEmpty();
    }

    [Fact]
    public void Segment_MultipleSentences_SplitsCorrectly()
    {
        var text = "Hello wonderful world, how are you doing today? I am fine, thank you very much! That is great to hear.";
        var result = TextSegmenter.Segment(text).ToList();
        result.Should().HaveCountGreaterThan(1);
        string.Join("", result.Select(s => s.TrimEnd())).Should().NotBeEmpty();
    }

    [Fact]
    public void Segment_NullText_ReturnsEmpty()
    {
        TextSegmenter.Segment(null!).Should().BeEmpty();
    }
}
