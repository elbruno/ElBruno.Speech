using ElBruno.Speech;
using ElBruno.Speech.Audio;
using ElBruno.Speech.Pipeline;
using ElBruno.Speech.TestUtils;
using FluentAssertions;

namespace ElBruno.Speech.Pipeline.Tests;

public sealed class DefaultSpeechPipelineTests
{
    private static MemoryAudioInput MakeSilenceInput(int durationMs = 500)
    {
        var format = AudioFormat.Pcm16KhzMono;
        var bytes = new byte[format.BytesPerSecond * durationMs / 1000];
        return new MemoryAudioInput(format, bytes, frameDurationMs: 20);
    }

    [Fact]
    public async Task RunAsync_WithFakeProviders_CompletesSuccessfully()
    {
        await using var pipeline = new DefaultSpeechPipeline(
            stt: new FakeSpeechToTextClient("hello"),
            llm: new FakeChatClient("world"),
            tts: new FakeTextToSpeechClient(100));

        await using var input = MakeSilenceInput(200);
        await using var output = new NullAudioOutput();

        var act = () => pipeline.RunAsync(input, output, CancellationToken.None);
        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RunAsync_CancellationRequested_StopsGracefully()
    {
        await using var pipeline = new DefaultSpeechPipeline(
            stt: new FakeSpeechToTextClient("hello"),
            llm: new FakeChatClient("world"),
            tts: new FakeTextToSpeechClient(100));

        await using var input = MakeSilenceInput(5000); // long input
        await using var output = new NullAudioOutput();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var act = () => pipeline.RunAsync(input, output, cts.Token);
        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StopAsync_BeforeRun_DoesNotThrow()
    {
        await using var pipeline = new DefaultSpeechPipeline(
            stt: new FakeSpeechToTextClient(),
            llm: new FakeChatClient(),
            tts: new FakeTextToSpeechClient());

        var act = () => pipeline.StopAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RunAsync_NoVad_PassthroughMode_WritesAudioOutput()
    {
        // Without VAD, all audio is treated as speech and flushed at IsFinal.
        // The pipeline should complete and the output should have received at least one frame.
        var outputFrames = new List<AudioFrame>();
        await using var capturingOutput = new CapturingOutput(outputFrames);

        await using var pipeline = new DefaultSpeechPipeline(
            stt: new FakeSpeechToTextClient("test transcript"),
            llm: new FakeChatClient("test response"),
            tts: new FakeTextToSpeechClient(50));

        await using var input = MakeSilenceInput(100);

        await pipeline.RunAsync(input, capturingOutput);
        outputFrames.Should().NotBeEmpty();
    }
}

// Minimal output that captures frames for assertions
file sealed class CapturingOutput(List<AudioFrame> frames) : IAudioOutput
{
    public ValueTask WriteAsync(AudioFrame frame, CancellationToken cancellationToken = default)
    {
        frames.Add(frame);
        return ValueTask.CompletedTask;
    }
    public ValueTask ClearAsync(CancellationToken cancellationToken = default) { frames.Clear(); return ValueTask.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
