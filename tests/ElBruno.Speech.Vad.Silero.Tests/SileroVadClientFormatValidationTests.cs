using ElBruno.Speech;
using ElBruno.Speech.Vad.Silero;
using FluentAssertions;
using Xunit;

namespace ElBruno.Speech.Vad.Silero.Tests;

public sealed class SileroVadClientFormatValidationTests
{
    private static SileroVadClient CreateTestClient() => new(new VadOptions());

    [Fact]
    public async Task ProcessFrameAsync_WrongSampleRate_ThrowsSpeechPipelineException()
    {
        await using var client = CreateTestClient();
        var frame = new AudioFrame(
            Data: new byte[1024],
            Format: new AudioFormat(44100, 1, AudioSampleFormat.Int16),
            SequenceNumber: 0,
            Timestamp: TimeSpan.Zero);

        var act = () => client.ProcessFrameAsync(frame).AsTask();
        await act.Should().ThrowAsync<SpeechPipelineException>()
            .WithMessage("*16 kHz*");
    }

    [Fact]
    public async Task ProcessFrameAsync_WrongChannels_ThrowsSpeechPipelineException()
    {
        await using var client = CreateTestClient();
        var frame = new AudioFrame(
            Data: new byte[1024],
            Format: new AudioFormat(16000, 2, AudioSampleFormat.Int16),
            SequenceNumber: 0,
            Timestamp: TimeSpan.Zero);

        var act = () => client.ProcessFrameAsync(frame).AsTask();
        await act.Should().ThrowAsync<SpeechPipelineException>()
            .WithMessage("*mono*");
    }

    [Fact]
    public async Task ProcessFrameAsync_WrongSampleFormat_ThrowsSpeechPipelineException()
    {
        await using var client = CreateTestClient();
        var frame = new AudioFrame(
            Data: new byte[1024],
            Format: new AudioFormat(16000, 1, AudioSampleFormat.Float32),
            SequenceNumber: 0,
            Timestamp: TimeSpan.Zero);

        var act = () => client.ProcessFrameAsync(frame).AsTask();
        await act.Should().ThrowAsync<SpeechPipelineException>()
            .WithMessage("*Int16*");
    }

    [Fact]
    public async Task Reset_DoesNotThrow()
    {
        await using var client = CreateTestClient();
        var act = () => { client.Reset(); return Task.CompletedTask; };
        await act.Should().NotThrowAsync();
    }
}
