using System.Threading.Channels;
using ElBruno.Speech;
using ElBruno.Speech.AspNetCore;
using FluentAssertions;

namespace ElBruno.Speech.AspNetCore.Tests;

public sealed class WebSocketAudioInputTests
{
    [Fact]
    public async Task ReadFramesAsync_ProducesFramesFromChannel()
    {
        var channel = Channel.CreateUnbounded<byte[]>();
        var format = AudioFormat.Pcm16KhzMono;
        // Write 2 frames worth of PCM (640 bytes each) then complete
        int frameSizeBytes = format.SampleRate / 1000 * 20 * format.Channels * format.BytesPerSample;
        await channel.Writer.WriteAsync(new byte[frameSizeBytes * 2]);
        channel.Writer.Complete();

        await using var input = new WebSocketAudioInput(channel.Reader, format, frameDurationMs: 20);

        var frames = new List<AudioFrame>();
        await foreach (var f in input.ReadFramesAsync())
            frames.Add(f);

        frames.Should().HaveCount(2);
    }
}
