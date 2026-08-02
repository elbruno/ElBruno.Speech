using ElBruno.Speech.Audio;
using FluentAssertions;
using System.Buffers;
using System.Runtime.InteropServices;

namespace ElBruno.Speech.Audio.Tests;

public sealed class MonoConverterTests
{
    [Fact]
    public void StereoToMono_AveragesChannels()
    {
        // Two stereo samples: [1000, 3000], [2000, 4000]
        short[] stereo = [1000, 3000, 2000, 4000];
        var bytes = new byte[stereo.Length * 2];
        MemoryMarshal.Cast<short, byte>(stereo).CopyTo(bytes);

        var (buf, byteCount) = MonoConverter.StereoToMono(bytes, channels: 2);
        try
        {
            var monoSamples = MemoryMarshal.Cast<byte, short>(buf.AsSpan(0, byteCount));
            monoSamples.Length.Should().Be(2);
            monoSamples[0].Should().Be(2000); // (1000+3000)/2
            monoSamples[1].Should().Be(3000); // (2000+4000)/2
        }
        finally { ArrayPool<byte>.Shared.Return(buf); }
    }

    [Fact]
    public void StereoToMono_AlreadyMono_ThrowsArgumentException()
    {
        var act = () => MonoConverter.StereoToMono(new byte[4], channels: 1);
        act.Should().Throw<ArgumentException>();
    }
}
