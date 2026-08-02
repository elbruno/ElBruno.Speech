using ElBruno.Speech;
using ElBruno.Speech.Audio;
using FluentAssertions;

namespace ElBruno.Speech.Audio.Tests;

public sealed class AudioFramerTests
{
    private readonly AudioFormat _format = AudioFormat.Pcm16KhzMono;

    [Fact]
    public void Frame_ExactMultiple_NoTailPadding()
    {
        // 20 ms frame at 16 kHz mono Int16 = 320 samples = 640 bytes
        var framer = new AudioFramer(_format, frameDurationMs: 20);
        int frameSizeBytes = framer.FrameSizeBytes;
        var data = new byte[frameSizeBytes * 3]; // exactly 3 frames

        var frames = framer.Frame(data).ToList();
        frames.Should().HaveCount(3);
        frames.All(f => f.Data.Length == frameSizeBytes).Should().BeTrue();
        frames.Last().IsFinal.Should().BeTrue();
    }

    [Fact]
    public void Frame_NonMultiple_LastFramePadded()
    {
        var framer = new AudioFramer(_format, frameDurationMs: 20);
        int frameSizeBytes = framer.FrameSizeBytes;
        // 2.5 frames worth of data
        var data = new byte[frameSizeBytes * 2 + frameSizeBytes / 2];

        var frames = framer.Frame(data).ToList();
        frames.Should().HaveCount(3);
        frames.Last().Data.Length.Should().Be(frameSizeBytes);
        frames.Last().IsFinal.Should().BeTrue();
    }

    [Fact]
    public void Frame_SequenceNumbersIncrementMonotonically()
    {
        var framer = new AudioFramer(_format);
        var data = new byte[framer.FrameSizeBytes * 5];
        var frames = framer.Frame(data).ToList();

        for (int i = 0; i < frames.Count; i++)
            frames[i].SequenceNumber.Should().Be(i);
    }

    [Fact]
    public void Reset_ResetsSequenceAndTimestamp()
    {
        var framer = new AudioFramer(_format);
        var data = new byte[framer.FrameSizeBytes * 2];
        _ = framer.Frame(data).ToList();
        framer.Reset();

        var frames = framer.Frame(data).ToList();
        frames[0].SequenceNumber.Should().Be(0);
        frames[0].Timestamp.Should().Be(TimeSpan.Zero);
    }
}
