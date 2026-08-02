using ElBruno.Speech;
using ElBruno.Speech.Audio;
using FluentAssertions;

namespace ElBruno.Speech.Audio.Tests;

public sealed class MemoryAudioInputTests
{
    [Fact]
    public async Task ReadFramesAsync_ProducesExpectedFrameCount()
    {
        var format = AudioFormat.Pcm16KhzMono;
        int frameSizeBytes = format.SampleRate / 1000 * 20 * format.Channels * format.BytesPerSample; // 640
        // 5 frames worth of audio
        var data = new byte[frameSizeBytes * 5];
        await using var input = new MemoryAudioInput(format, data, frameDurationMs: 20);

        var frames = new List<AudioFrame>();
        await foreach (var f in input.ReadFramesAsync())
            frames.Add(f);

        frames.Should().HaveCount(5);
        frames.Last().IsFinal.Should().BeTrue();
    }
}
