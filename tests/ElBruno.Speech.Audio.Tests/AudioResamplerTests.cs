using ElBruno.Speech.Audio;
using FluentAssertions;
using System.Buffers;
using System.Runtime.InteropServices;

namespace ElBruno.Speech.Audio.Tests;

public sealed class AudioResamplerTests
{
    [Fact]
    public void ResampleTo16Khz_16KhzPassthrough_ReturnsOriginalSpan()
    {
        var data = new byte[640]; // already 16 kHz
        var (buf, result) = AudioResampler.ResampleTo16Khz(data, 16_000);
        buf.Should().BeNull(); // no allocation
        result.Length.Should().Be(640);
    }

    [Fact]
    public void ResampleTo16Khz_48Khz_ProducesCorrectSampleCount()
    {
        // 1 second of 48 kHz mono Int16 = 48000 * 2 = 96000 bytes
        var data = new byte[96_000];
        var (buf, result) = AudioResampler.ResampleTo16Khz(data, 48_000);
        try
        {
            // Expect ~16000 samples = 32000 bytes
            int resultSamples = result.Length / 2;
            resultSamples.Should().BeInRange(15_990, 16_010);
        }
        finally
        {
            if (buf is not null) ArrayPool<byte>.Shared.Return(buf);
        }
    }

    [Fact]
    public void ResampleTo16Khz_UnsupportedRate_ThrowsSpeechPipelineException()
    {
        Assert.Throws<SpeechPipelineException>(() => { AudioResampler.ResampleTo16Khz(new byte[100], 96_000); });
    }
}
