using System.Buffers.Binary;

namespace ElBruno.Speech.Audio;

public enum Pcm16InputFormat
{
    RawPcm16,
    Wav
}

/// <summary>Bounded, strict mono 16 kHz PCM16 input shared by local STT providers.</summary>
public static class Pcm16AudioInput
{
    /// <summary>Reads without closing the caller's stream. Does not resample or downmix.</summary>
    public static async Task<byte[]> ReadAsync(Stream input, Pcm16InputFormat format, int maxInputBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (maxInputBytes < 44) throw new ArgumentOutOfRangeException(nameof(maxInputBytes));
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        using var data = new MemoryStream();
        byte[] buffer = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (data.Length + read > maxInputBytes)
                throw new InvalidDataException($"Audio exceeds MaxInputBytes ({maxInputBytes}).");
            data.Write(buffer, 0, read);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Parse(data.ToArray(), format);
    }

    public static byte[] Parse(byte[] data, Pcm16InputFormat format)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        ReadOnlyMemory<byte> pcm = data;
        if (format == Pcm16InputFormat.Wav)
        {
            if (data.Length < 44 || !data.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
                !data.AsSpan(8, 4).SequenceEqual("WAVE"u8) ||
                BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4)) != data.Length - 8)
                throw new InvalidDataException("Expected a complete RIFF/WAVE PCM16 file.");
            bool foundFormat = false, foundData = false;
            int offset = 12;
            while (offset <= data.Length - 8)
            {
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 4));
                if (length > data.Length - offset - 8)
                    throw new InvalidDataException("Truncated WAV chunk.");
                var chunk = data.AsSpan(offset, 4);
                if (chunk.SequenceEqual("fmt "u8))
                {
                    if (foundFormat || length < 16 ||
                        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 8)) != 1 ||
                        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 10)) != 1 ||
                        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 12)) != 16000 ||
                        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 16)) != 32000 ||
                        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 20)) != 2 ||
                        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 22)) != 16)
                        throw new InvalidDataException("WAV input must be mono 16 kHz PCM16.");
                    foundFormat = true;
                }
                if (chunk.SequenceEqual("data"u8))
                {
                    if (!foundFormat || foundData)
                        throw new InvalidDataException("Expected one data chunk after fmt.");
                    foundData = true;
                }
                long next = offset + 8L + length + (length & 1);
                if (next > data.Length)
                    throw new InvalidDataException("Missing WAV padding byte.");
                offset = checked((int)next);
            }
            if (!foundFormat || !foundData || offset != data.Length)
                throw new InvalidDataException("Incomplete WAV structure.");
            pcm = WavReader.ParseWav(data).Samples;
        }
        else if (data.AsSpan().StartsWith("RIFF"u8))
        {
            throw new InvalidDataException("Select WAV input format for WAV-wrapped input.");
        }
        if ((pcm.Length & 1) != 0)
            throw new InvalidDataException("PCM16 input contains an incomplete sample.");
        return pcm.ToArray();
    }
}
