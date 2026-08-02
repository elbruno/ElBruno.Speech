using ElBruno.Speech;
using ElBruno.Speech.Audio;
using FluentAssertions;

namespace ElBruno.Speech.Audio.Tests;

public sealed class NullAudioOutputTests
{
    [Fact]
    public async Task WriteAsync_DoesNotThrow()
    {
        await using var output = new NullAudioOutput();
        var frame = new AudioFrame(new byte[640], AudioFormat.Pcm16KhzMono, 0, TimeSpan.Zero);
        var act = () => output.WriteAsync(frame).AsTask();
        await act.Should().NotThrowAsync();
    }
}
