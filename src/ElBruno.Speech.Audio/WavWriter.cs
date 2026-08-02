using System.Buffers.Binary;

namespace ElBruno.Speech.Audio;

/// <summary>Writes PCM audio data to WAV files.</summary>
public static class WavWriter
{
    /// <summary>Writes PCM samples with the given format to a WAV file.</summary>
    public static void Write(string path, AudioFormat format, ReadOnlySpan<byte> samples)
    {
        using var fs = File.Create(path);
        Write(fs, format, samples);
    }

    /// <summary>Writes PCM samples with the given format to a stream.</summary>
    public static void Write(Stream stream, AudioFormat format, ReadOnlySpan<byte> samples)
    {
        int byteRate = format.BytesPerSecond;
        int blockAlign = format.Channels * format.BytesPerSample;
        int dataSize = samples.Length;
        int riffSize = 36 + dataSize;

        Span<byte> header = stackalloc byte[44];

        // RIFF
        header[0] = (byte)'R'; header[1] = (byte)'I'; header[2] = (byte)'F'; header[3] = (byte)'F';
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4), riffSize);
        header[8] = (byte)'W'; header[9] = (byte)'A'; header[10] = (byte)'V'; header[11] = (byte)'E';

        // fmt
        header[12] = (byte)'f'; header[13] = (byte)'m'; header[14] = (byte)'t'; header[15] = (byte)' ';
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(header.Slice(20), 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(header.Slice(22), (short)format.Channels);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(24), format.SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(28), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(header.Slice(32), (short)blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(header.Slice(34), (short)(format.BytesPerSample * 8));

        // data
        header[36] = (byte)'d'; header[37] = (byte)'a'; header[38] = (byte)'t'; header[39] = (byte)'a';
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(40), dataSize);

        stream.Write(header);
        stream.Write(samples);
    }
}
