using ElBruno.Speech.Audio;
using FluentAssertions;
using System.Buffers;
using System.Runtime.InteropServices;

namespace ElBruno.Speech.Audio.Tests;

public sealed class PcmConverterTests
{
    [Fact]
    public void Int16ToFloat32_Silence_YieldsZeroFloats()
    {
        var bytes = new byte[8]; // 4 zero-value Int16 samples
        var (buf, count) = PcmConverter.Int16ToFloat32(bytes);
        try
        {
            count.Should().Be(4);
            for (int i = 0; i < count; i++)
                buf[i].Should().Be(0f);
        }
        finally { ArrayPool<float>.Shared.Return(buf); }
    }

    [Fact]
    public void Float32ToInt16_RoundTrip_PreservesValues()
    {
        // Int16 max positive → float → Int16 should be close (within 1 due to rounding)
        short[] shorts = [16384, -16384, 0, 32767];
        int byteLen = shorts.Length * 2;
        var inputBytes = new byte[byteLen];
        MemoryMarshal.Cast<short, byte>(shorts).CopyTo(inputBytes);

        var (floatBuf, floatCount) = PcmConverter.Int16ToFloat32(inputBytes);
        try
        {
            var (byteBuf, byteCount) = PcmConverter.Float32ToInt16(floatBuf.AsSpan(0, floatCount));
            try
            {
                var resultShorts = MemoryMarshal.Cast<byte, short>(byteBuf.AsSpan(0, byteCount));
                for (int i = 0; i < shorts.Length; i++)
                    Math.Abs(resultShorts[i] - shorts[i]).Should().BeLessThanOrEqualTo(1);
            }
            finally { ArrayPool<byte>.Shared.Return(byteBuf); }
        }
        finally { ArrayPool<float>.Shared.Return(floatBuf); }
    }
}
