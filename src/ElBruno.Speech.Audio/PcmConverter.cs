using System.Buffers;
using System.Runtime.InteropServices;

namespace ElBruno.Speech.Audio;

/// <summary>Converts between Int16 PCM and Float32 PCM sample encodings.</summary>
public static class PcmConverter
{
    private const float Int16ToFloat = 1f / 32768f;
    private const float FloatToInt16 = 32767f;

    /// <summary>Converts Int16 PCM bytes to Float32 PCM bytes.
    /// Caller owns the returned array and must return it to the pool when done.</summary>
    public static (float[] Buffer, int SampleCount) Int16ToFloat32(ReadOnlySpan<byte> int16Bytes)
    {
        int sampleCount = int16Bytes.Length / 2;
        var floatBuffer = ArrayPool<float>.Shared.Rent(sampleCount);

        var int16Samples = MemoryMarshal.Cast<byte, short>(int16Bytes);
        for (int i = 0; i < sampleCount; i++)
            floatBuffer[i] = int16Samples[i] * Int16ToFloat;

        return (floatBuffer, sampleCount);
    }

    /// <summary>Converts Float32 PCM samples to Int16 PCM bytes.
    /// Caller owns the returned array and must return it to the pool when done.</summary>
    public static (byte[] Buffer, int ByteCount) Float32ToInt16(ReadOnlySpan<float> floatSamples)
    {
        int byteCount = floatSamples.Length * 2;
        var byteBuffer = ArrayPool<byte>.Shared.Rent(byteCount);
        var int16View = MemoryMarshal.Cast<byte, short>(byteBuffer.AsSpan(0, byteCount));

        for (int i = 0; i < floatSamples.Length; i++)
        {
            float clamped = Math.Clamp(floatSamples[i], -1f, 1f);
            int16View[i] = (short)(clamped * FloatToInt16);
        }

        return (byteBuffer, byteCount);
    }

    /// <summary>Converts Int16 PCM bytes to a Float32 span, writing into <paramref name="destination"/>.</summary>
    public static void Int16ToFloat32(ReadOnlySpan<byte> int16Bytes, Span<float> destination)
    {
        var int16Samples = MemoryMarshal.Cast<byte, short>(int16Bytes);
        int count = Math.Min(int16Samples.Length, destination.Length);
        for (int i = 0; i < count; i++)
            destination[i] = int16Samples[i] * Int16ToFloat;
    }

    /// <summary>Converts Float32 samples to Int16 PCM bytes, writing into <paramref name="destination"/>.</summary>
    public static void Float32ToInt16(ReadOnlySpan<float> floatSamples, Span<byte> destination)
    {
        var int16View = MemoryMarshal.Cast<byte, short>(destination);
        int count = Math.Min(floatSamples.Length, int16View.Length);
        for (int i = 0; i < count; i++)
        {
            float clamped = Math.Clamp(floatSamples[i], -1f, 1f);
            int16View[i] = (short)(clamped * FloatToInt16);
        }
    }
}
