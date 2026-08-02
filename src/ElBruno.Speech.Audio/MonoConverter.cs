using System.Buffers;
using System.Runtime.InteropServices;

namespace ElBruno.Speech.Audio;

/// <summary>Downmixes multi-channel PCM to mono by averaging channels.</summary>
public static class MonoConverter
{
    /// <summary>
    /// Downmixes Int16 stereo (or N-channel) PCM to mono.
    /// Returns a pooled buffer; caller must return it to <see cref="ArrayPool{T}.Shared"/> when done.
    /// </summary>
    public static (byte[] Buffer, int ByteCount) StereoToMono(ReadOnlySpan<byte> input, int channels)
    {
        if (channels == 1) throw new ArgumentException("Input is already mono.", nameof(channels));

        var inputSamples = MemoryMarshal.Cast<byte, short>(input);
        int monoSampleCount = inputSamples.Length / channels;
        int byteCount = monoSampleCount * 2;
        var output = ArrayPool<byte>.Shared.Rent(byteCount);
        var outputSamples = MemoryMarshal.Cast<byte, short>(output.AsSpan(0, byteCount));

        for (int i = 0; i < monoSampleCount; i++)
        {
            int sum = 0;
            for (int ch = 0; ch < channels; ch++)
                sum += inputSamples[i * channels + ch];
            outputSamples[i] = (short)(sum / channels);
        }

        return (output, byteCount);
    }
}
