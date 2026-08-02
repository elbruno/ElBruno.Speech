using ElBruno.Speech.Audio;
using FluentAssertions;

namespace ElBruno.Speech.Audio.Tests;

public sealed class AudioRingBufferTests : IDisposable
{
    private readonly AudioRingBuffer _ring = new(1024);

    [Fact]
    public void TryWrite_ThenRead_ReturnsCorrectData()
    {
        var data = new byte[] { 1, 2, 3, 4 };
        _ring.TryWrite(data).Should().BeTrue();
        _ring.Available.Should().Be(4);

        var dest = new byte[4];
        int read = _ring.TryRead(dest);
        read.Should().Be(4);
        dest.Should().Equal(data);
        _ring.Available.Should().Be(0);
    }

    [Fact]
    public void TryWrite_WhenFull_ReturnsFalse()
    {
        var big = new byte[1025];
        _ring.TryWrite(big).Should().BeFalse();
    }

    [Fact]
    public void WrapAround_WorksCorrectly()
    {
        // Fill 800 bytes, read 700, write 700 more → should wrap
        var write800 = new byte[800];
        _ring.TryWrite(write800).Should().BeTrue();
        var read700 = new byte[700];
        _ring.TryRead(read700).Should().Be(700);
        var write700 = new byte[700];
        _ring.TryWrite(write700).Should().BeTrue();
        _ring.Available.Should().Be(800);
    }

    public void Dispose() => _ring.Dispose();
}
