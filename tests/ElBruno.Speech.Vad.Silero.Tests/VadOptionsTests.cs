using ElBruno.Speech.Vad.Silero;
using FluentAssertions;
using Xunit;

namespace ElBruno.Speech.Vad.Silero.Tests;

public sealed class VadOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new VadOptions();
        opts.Threshold.Should().Be(0.5f);
        opts.MinSpeechFrames.Should().Be(3);
        opts.MinSilenceFrames.Should().Be(6);
        opts.ModelPath.Should().BeNull();
    }

    [Fact]
    public void Record_WithProperties_Works()
    {
        var opts = new VadOptions { Threshold = 0.7f, MinSpeechFrames = 5, MinSilenceFrames = 10 };
        opts.Threshold.Should().Be(0.7f);
        opts.MinSpeechFrames.Should().Be(5);
        opts.MinSilenceFrames.Should().Be(10);
    }

    [Fact]
    public void Record_Equality_Works()
    {
        var a = new VadOptions { Threshold = 0.6f };
        var b = new VadOptions { Threshold = 0.6f };
        a.Should().Be(b);
    }
}
