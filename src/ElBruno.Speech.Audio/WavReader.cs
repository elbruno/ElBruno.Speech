using System.Buffers;
using System.Buffers.Binary;

namespace ElBruno.Speech.Audio;

/// <summary>Reads PCM WAV files and exposes raw sample data.</summary>
public static class WavReader
{
    /// <summary>Reads a WAV file and returns the audio format and PCM data.</summary>
    /// <exception cref="SpeechPipelineException">Thrown for malformed or unsupported WAV files.</exception>
    public static (AudioFormat Format, ReadOnlyMemory<byte> Samples) Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return ParseWav(bytes);
    }

    /// <summary>Reads a WAV from a byte array and returns the audio format and PCM data.</summary>
    public static (AudioFormat Format, ReadOnlyMemory<byte> Samples) ParseWav(ReadOnlySpan<byte> data)
    {
        if (data.Length < 44)
            throw new SpeechPipelineException("WAV data too short.");

        // RIFF header
        if (data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F')
            throw new SpeechPipelineException("Not a RIFF file.");
        if (data[8] != 'W' || data[9] != 'A' || data[10] != 'V' || data[11] != 'E')
            throw new SpeechPipelineException("Not a WAVE file.");

        int offset = 12;
        int sampleRate = 0, channels = 0, bitsPerSample = 0;
        int audioDataOffset = 0, audioDataLength = 0;

        while (offset + 8 <= data.Length)
        {
            var chunkId = data.Slice(offset, 4);
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + 4, 4));

            if (chunkId[0] == 'f' && chunkId[1] == 'm' && chunkId[2] == 't' && chunkId[3] == ' ')
            {
                int audioFormat = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset + 8, 2));
                if (audioFormat != 1)
                    throw new SpeechPipelineException($"Only PCM (format 1) is supported; got {audioFormat}.");
                channels = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset + 10, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + 12, 4));
                bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset + 22, 2));
            }
            else if (chunkId[0] == 'd' && chunkId[1] == 'a' && chunkId[2] == 't' && chunkId[3] == 'a')
            {
                audioDataOffset = offset + 8;
                audioDataLength = Math.Min(chunkSize, data.Length - audioDataOffset);
                break;
            }

            offset += 8 + chunkSize;
            if ((chunkSize & 1) == 1) offset++; // RIFF padding byte
        }

        if (audioDataOffset == 0)
            throw new SpeechPipelineException("No data chunk found in WAV file.");
        if (sampleRate == 0)
            throw new SpeechPipelineException("No fmt chunk found in WAV file.");

        var sampleFormat = bitsPerSample switch
        {
            16 => AudioSampleFormat.Int16,
            32 => AudioSampleFormat.Float32,
            _ => throw new SpeechPipelineException($"Unsupported bit depth: {bitsPerSample}."),
        };

        var format = new AudioFormat(sampleRate, channels, sampleFormat);
        var samples = data.Slice(audioDataOffset, audioDataLength).ToArray().AsMemory();
        return (format, samples);
    }
}
