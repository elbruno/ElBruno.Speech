using System.Buffers.Binary;
using System.Net;
using System.Text.Json;
using ElBruno.Speech;
using ElBruno.Speech.Audio;
using ElBruno.Speech.Tts.Ollama;
using Microsoft.Extensions.AI;
using OllamaSpeechDemo;
using Xunit;

namespace ElBruno.Speech.Tts.Ollama.Tests;

public sealed class OllamaSpeechDemoTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavesCompleteAndStreamedAudioWithPlayableHeaders(bool unknownWavLength)
    {
        byte[] pcm = CreatePcm();
        byte[] wav = CreateWav(pcm);
        if (unknownWavLength)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(wav.AsSpan(4), uint.MaxValue);
            BinaryPrimitives.WriteUInt32LittleEndian(wav.AsSpan(40), uint.MaxValue);
        }
        var formats = new List<string>();
        using var http = new HttpClient(new Handler(async request =>
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("A synthetic audio test.", json.RootElement.GetProperty("input").GetString());
            string format = json.RootElement.GetProperty("response_format").GetString()!;
            formats.Add(format);
            return format == "wav" ? wav : pcm;
        }));
        using var speech = new OllamaSpeechClient(http);
        string output = Path.Combine(Path.GetTempPath(), "OllamaSpeechDemoTests-" + Guid.NewGuid());
        using var log = new StringWriter();
        try
        {
            await SpeechDemo.GenerateAsync(speech, "A synthetic audio test.", output, log);

            Assert.Equal(new[] { "wav", "pcm" }, formats);
            Assert.Equal(pcm, await File.ReadAllBytesAsync(Path.Combine(output, "streamed.pcm")));
            foreach (string name in new[] { "complete.wav", "streamed.wav" })
            {
                byte[] saved = await File.ReadAllBytesAsync(Path.Combine(output, name));
                Assert.Equal((uint)(saved.Length - 8), BinaryPrimitives.ReadUInt32LittleEndian(saved.AsSpan(4)));
                Assert.Equal((uint)pcm.Length, BinaryPrimitives.ReadUInt32LittleEndian(saved.AsSpan(40)));
                var (format, samples) = WavReader.ParseWav(saved);
                Assert.Equal(24000, format.SampleRate);
                Assert.Equal(1, format.Channels);
                Assert.Equal(AudioSampleFormat.Int16, format.SampleFormat);
                Assert.Equal(pcm, samples.ToArray());
            }
            Assert.Contains("First PCM chunk:", log.ToString());
            Assert.Contains("non-silent audio", log.ToString());
            Assert.Contains("do not verify speech accuracy", log.ToString());
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("silent")]
    [InlineData("partial-sample")]
    [InlineData("wrong-rate")]
    [InlineData("truncated")]
    [InlineData("not-wav")]
    public async Task RejectsInvalidCompleteAudioInsteadOfReportingSuccess(string failure)
    {
        byte[] wav = failure switch
        {
            "empty" => CreateWav([]),
            "silent" => CreateWav(new byte[100]),
            "partial-sample" => CreateWav([1]),
            "wrong-rate" => CreateWav(CreatePcm(), sampleRate: 16000),
            "truncated" => CreateWav(CreatePcm())[..^2],
            "not-wav" => [1, 2, 3],
            _ => throw new ArgumentException(nameof(failure))
        };
        using var http = new HttpClient(new Handler(_ => Task.FromResult(wav)));
        using var speech = new OllamaSpeechClient(http);
        string output = Path.Combine(Path.GetTempPath(), "OllamaSpeechDemoTests-" + Guid.NewGuid());
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                SpeechDemo.GenerateAsync(speech, "test", output, TextWriter.Null));
            Assert.False(File.Exists(Path.Combine(output, "complete.wav")));
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RejectsEmptyPartialOrSilentPcmStreaming(int invalidByteCount)
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            return json.RootElement.GetProperty("response_format").GetString() == "wav"
                ? CreateWav(CreatePcm()) : new byte[invalidByteCount];
        }));
        using var speech = new OllamaSpeechClient(http);
        string output = Path.Combine(Path.GetTempPath(), "OllamaSpeechDemoTests-" + Guid.NewGuid());
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                SpeechDemo.GenerateAsync(speech, "test", output, TextWriter.Null));
            Assert.False(File.Exists(Path.Combine(output, "streamed.wav")));
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    private static byte[] CreatePcm()
    {
        var pcm = new byte[48000];
        for (int i = 0; i < pcm.Length / 2; i++)
        {
            short sample = (short)(8000 * Math.Sin(2 * Math.PI * 440 * i / 24000));
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), sample);
        }
        return pcm;
    }

    private static byte[] CreateWav(byte[] pcm, int sampleRate = 24000)
    {
        using var stream = new MemoryStream();
        WavWriter.Write(stream, new AudioFormat(sampleRate, 1, AudioSampleFormat.Int16), pcm);
        return stream.ToArray();
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<byte[]>> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            new(HttpStatusCode.OK) { Content = new ByteArrayContent(await respond(request)) };
    }
}
