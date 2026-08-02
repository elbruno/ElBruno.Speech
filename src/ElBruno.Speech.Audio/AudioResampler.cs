using System.Buffers;
using System.Runtime.InteropServices;

namespace ElBruno.Speech.Audio;

/// <summary>Result of a resampling operation. Holds an optional pooled buffer and a span over valid bytes.</summary>
public readonly ref struct ResampleResult
{
    internal ResampleResult(byte[]? pooledBuffer, ReadOnlySpan<byte> result)
    {
        PooledBuffer = pooledBuffer;
        Result = result;
    }

    /// <summary>The rented pooled buffer, or <c>null</c> for 16 kHz passthrough. Must be returned to <see cref="ArrayPool{T}.Shared"/> when done.</summary>
    public byte[]? PooledBuffer { get; }

    /// <summary>The valid resampled bytes.</summary>
    public ReadOnlySpan<byte> Result { get; }

    /// <summary>Supports positional deconstruction: <c>var (buf, span) = result;</c></summary>
    public void Deconstruct(out byte[]? pooledBuffer, out ReadOnlySpan<byte> result)
    {
        pooledBuffer = PooledBuffer;
        result = Result;
    }
}

/// <summary>Resamples Int16 mono PCM from any supported rate to 16 kHz using linear interpolation.</summary>
/// <remarks>
/// Supported input rates: 8000, 16000, 22050, 24000, 44100, 48000 Hz.
/// 16 kHz passthrough is a no-op (returns the original data view).
/// </remarks>
public static class AudioResampler
{
    private static readonly int[] SupportedRates = [8000, 16000, 22050, 24000, 44100, 48000];
    private const int TargetRate = 16_000;

    /// <summary>
    /// Resamples <paramref name="input"/> from <paramref name="inputRate"/> to 16 kHz.
    /// Returns a pooled buffer; caller must return it to <see cref="ArrayPool{T}.Shared"/> when done.
    /// For 16 kHz input, <see cref="ResampleResult.PooledBuffer"/> is <c>null</c> and no allocation is made.
    /// </summary>
    public static ResampleResult ResampleTo16Khz(ReadOnlySpan<byte> input, int inputRate)
    {
        if (!Array.Exists(SupportedRates, r => r == inputRate))
            throw new SpeechPipelineException($"Unsupported input sample rate: {inputRate}. Supported: {string.Join(", ", SupportedRates)}.");

        if (inputRate == TargetRate)
            return new ResampleResult(null, input); // passthrough

        var inputSamples = MemoryMarshal.Cast<byte, short>(input);
        int outputSampleCount = (int)Math.Ceiling((double)inputSamples.Length * TargetRate / inputRate);
        int byteCount = outputSampleCount * 2;
        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        var outputSamples = MemoryMarshal.Cast<byte, short>(buffer.AsSpan(0, byteCount));

        double ratio = (double)inputRate / TargetRate;
        for (int i = 0; i < outputSampleCount; i++)
        {
            double srcPos = i * ratio;
            int srcIdx = (int)srcPos;
            double frac = srcPos - srcIdx;

            short s0 = srcIdx < inputSamples.Length ? inputSamples[srcIdx] : (short)0;
            short s1 = srcIdx + 1 < inputSamples.Length ? inputSamples[srcIdx + 1] : (short)0;
            outputSamples[i] = (short)(s0 + frac * (s1 - s0));
        }

        return new ResampleResult(buffer, buffer.AsSpan(0, byteCount));
    }
}
