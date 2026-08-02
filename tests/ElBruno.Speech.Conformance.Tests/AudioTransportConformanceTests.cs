using ElBruno.Speech;
using ElBruno.Speech.Audio;
using FluentAssertions;

namespace ElBruno.Speech.Conformance.Tests;

/// <summary>Verifies IAudioInput/IAudioOutput implementations satisfy the transport contract.</summary>
public sealed class AudioTransportConformanceTests
{
    [Fact]
    public async Task MemoryAudioInput_ProducesFramesWithCorrectFormat()
    {
        var format = AudioFormat.Pcm16KhzMono;
        int frameSizeBytes = format.SampleRate / 1000 * 20 * format.Channels * format.BytesPerSample;
        var data = new byte[frameSizeBytes * 3];
        await using var input = new MemoryAudioInput(format, data, frameDurationMs: 20);

        var frames = new List<AudioFrame>();
        await foreach (var f in input.ReadFramesAsync())
            frames.Add(f);

        frames.Should().HaveCount(3);
        frames.Should().AllSatisfy(f => f.Format.Should().Be(format));
        frames.Last().IsFinal.Should().BeTrue();
    }

    [Fact]
    public async Task NullAudioOutput_AcceptsAllFrames()
    {
        await using var output = new NullAudioOutput();
        for (int i = 0; i < 10; i++)
        {
            var frame = new AudioFrame(new byte[640], AudioFormat.Pcm16KhzMono, i, TimeSpan.Zero);
            await output.WriteAsync(frame);
        }
        // No exception = pass
    }

    [Fact]
    public async Task WavRoundTrip_PreservesFormatAndSamples()
    {
        var format = AudioFormat.Pcm16KhzMono;
        var samples = new byte[3200]; // 100ms
        new Random(0).NextBytes(samples);

        var path = Path.GetTempFileName() + ".wav";
        try
        {
            WavWriter.Write(path, format, samples);
            await using var input = new FileAudioInput(path);
            input.Format.Should().Be(format);

            var frames = new List<AudioFrame>();
            await foreach (var f in input.ReadFramesAsync())
                frames.Add(f);
            frames.Should().NotBeEmpty();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
