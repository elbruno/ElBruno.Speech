using System.Buffers.Binary;
using System.Diagnostics;
using ElBruno.Speech;
using ElBruno.Speech.Audio;
using Microsoft.Extensions.AI;

namespace OllamaSpeechDemo;

/// <summary>Demonstrates complete WAV generation and incremental PCM streaming using MEAI.</summary>
public static class SpeechDemo
{
    /// <summary>Generates, checks, and saves both audio formats. Does not play audio automatically.</summary>
    public static async Task GenerateAsync(ITextToSpeechClient speech, string text, string outputDirectory,
        TextWriter log, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        string completePath = Path.Combine(outputDirectory, "complete.wav");
        string pcmPath = Path.Combine(outputDirectory, "streamed.pcm");
        string streamedPath = Path.Combine(outputDirectory, "streamed.wav");

        var timer = Stopwatch.StartNew();
        var response = await speech.GetAudioAsync(text, new() { AudioFormat = "wav" }, cancellationToken);
        var wav = GetAudio(response.Contents, "audio/wav");
        var completePcm = ReadOllamaWav(wav.Data);
        SavePlayableWav(completePath, completePcm.Span);
        log.WriteLine($"Complete: {completePath}, {completePcm.Length / 48000.0:F2}s of audio in {timer.Elapsed.TotalSeconds:F2}s.");

        timer.Restart();
        int chunks = 0;
        long bytes = 0;
        await using (var file = File.Create(pcmPath))
        {
            await foreach (var update in speech.GetStreamingAudioAsync(text,
                new() { AudioFormat = "pcm" }, cancellationToken))
            {
                foreach (var content in update.Contents.OfType<DataContent>())
                {
                    if (content.MediaType != "audio/pcm")
                        throw new InvalidDataException($"Expected audio/pcm, received {content.MediaType}.");
                    if (content.Data.IsEmpty) continue;
                    await file.WriteAsync(content.Data, cancellationToken);
                    if (++chunks == 1)
                        log.WriteLine($"First PCM chunk: {timer.Elapsed.TotalMilliseconds:F0}ms.");
                    bytes += content.Data.Length;
                }
            }
        }

        byte[] streamedPcm = await File.ReadAllBytesAsync(pcmPath, cancellationToken);
        SavePlayableWav(streamedPath, streamedPcm);
        log.WriteLine($"Streaming: {chunks} chunks, {bytes} PCM bytes, {bytes / 48000.0:F2}s of audio in {timer.Elapsed.TotalSeconds:F2}s.");
        log.WriteLine($"Saved: {streamedPath}");
        log.WriteLine("Format checked: 24 kHz, mono, signed 16-bit PCM, non-silent audio.");
        log.WriteLine("File checks do not verify speech accuracy. Listen to both WAV files before recording a demo.");
    }

    private static DataContent GetAudio(IList<AIContent> contents, string mediaType)
    {
        var audio = contents.OfType<DataContent>().ToArray();
        if (audio.Length != 1 || audio[0].MediaType != mediaType)
            throw new InvalidDataException($"Expected one {mediaType} audio content item.");
        return audio[0];
    }

    private static ReadOnlyMemory<byte> ReadOllamaWav(ReadOnlyMemory<byte> audio)
    {
        var data = audio.Span;
        if (data.Length < 44 || !data[..4].SequenceEqual("RIFF"u8) || !data.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Ollama did not return a PCM WAV file.");

        bool validFormat = false;
        int offset = 12;
        while (offset <= data.Length - 8)
        {
            var id = data.Slice(offset, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4));
            offset += 8;
            if (id.SequenceEqual("data"u8))
            {
                if (!validFormat)
                    throw new InvalidDataException("Expected mono PCM16 WAV audio at 24 kHz.");
                // Ollama streams WAV before its length is known, using uint.MaxValue for data size.
                if (size != uint.MaxValue && size > data.Length - offset)
                    throw new InvalidDataException("Ollama returned truncated WAV samples.");
                int length = size == uint.MaxValue ? data.Length - offset : (int)size;
                return audio.Slice(offset, length);
            }
            if (size > data.Length - offset)
                throw new InvalidDataException("Ollama returned a truncated WAV chunk.");
            if (id.SequenceEqual("fmt "u8))
            {
                var format = data.Slice(offset, (int)size);
                validFormat = format.Length >= 16
                    && BinaryPrimitives.ReadUInt16LittleEndian(format) == 1
                    && BinaryPrimitives.ReadUInt16LittleEndian(format[2..]) == 1
                    && BinaryPrimitives.ReadUInt32LittleEndian(format[4..]) == 24000
                    && BinaryPrimitives.ReadUInt32LittleEndian(format[8..]) == 48000
                    && BinaryPrimitives.ReadUInt16LittleEndian(format[12..]) == 2
                    && BinaryPrimitives.ReadUInt16LittleEndian(format[14..]) == 16;
            }
            offset += (int)size + (int)(size & 1);
        }
        throw new InvalidDataException("Ollama WAV response has no audio data chunk.");
    }

    private static void SavePlayableWav(string path, ReadOnlySpan<byte> pcm)
    {
        if (pcm.IsEmpty || pcm.Length % 2 != 0)
            throw new InvalidDataException("Expected non-empty audio with complete 16-bit PCM samples.");
        bool nonSilent = false;
        for (int i = 0; i < pcm.Length; i += 2)
            nonSilent |= BinaryPrimitives.ReadInt16LittleEndian(pcm[i..]) != 0;
        if (!nonSilent) throw new InvalidDataException("Ollama returned entirely silent audio.");

        // Rewrite the WAV sizes once all samples are known, for compatibility with file players.
        WavWriter.Write(path, new AudioFormat(24000, 1, AudioSampleFormat.Int16), pcm);
    }
}
