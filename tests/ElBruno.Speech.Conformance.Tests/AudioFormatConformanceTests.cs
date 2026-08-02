using ElBruno.Speech;
using FluentAssertions;

namespace ElBruno.Speech.Conformance.Tests;

/// <summary>Verifies AudioFormat and AudioFrame public API contracts.</summary>
public sealed class AudioFormatConformanceTests
{
    [Fact]
    public void Pcm16KhzMono_HasCorrectProperties()
    {
        var fmt = AudioFormat.Pcm16KhzMono;
        fmt.SampleRate.Should().Be(16_000);
        fmt.Channels.Should().Be(1);
        fmt.SampleFormat.Should().Be(AudioSampleFormat.Int16);
        fmt.BytesPerSample.Should().Be(2);
        fmt.BytesPerSecond.Should().Be(32_000);
    }

    [Fact]
    public void AudioFrame_Duration_ComputedCorrectly()
    {
        var fmt = AudioFormat.Pcm16KhzMono;
        // 640 bytes = 320 samples = 20ms at 16kHz mono Int16
        var frame = new AudioFrame(new byte[640], fmt, 0, TimeSpan.Zero);
        frame.Duration.Should().BeCloseTo(TimeSpan.FromMilliseconds(20), precision: TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void AudioFrame_IsFinal_DefaultFalse()
    {
        var frame = new AudioFrame(new byte[640], AudioFormat.Pcm16KhzMono, 0, TimeSpan.Zero);
        frame.IsFinal.Should().BeFalse();
    }

    [Fact]
    public void AudioFormat_Equality_WorksCorrectly()
    {
        var a = new AudioFormat(16_000, 1, AudioSampleFormat.Int16);
        var b = new AudioFormat(16_000, 1, AudioSampleFormat.Int16);
        a.Should().Be(b);
        a.Should().Be(AudioFormat.Pcm16KhzMono);
    }
}
