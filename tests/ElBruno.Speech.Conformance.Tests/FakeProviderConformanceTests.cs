using Microsoft.Extensions.AI;
using ElBruno.Speech;
using ElBruno.Speech.Audio;
using ElBruno.Speech.Pipeline;
using ElBruno.Speech.TestUtils;
using FluentAssertions;

namespace ElBruno.Speech.Conformance.Tests;

/// <summary>
/// Verifies that fake providers satisfy MEAI client contracts
/// and that the pipeline accepts them end-to-end.
/// </summary>
public sealed class FakeProviderConformanceTests
{
    [Fact]
    public async Task FakeSpeechToTextClient_ReturnsConfiguredTranscript()
    {
        using var stt = new FakeSpeechToTextClient("test transcript");
        using var stream = new MemoryStream(new byte[100]);
        var result = await stt.GetTextAsync(stream);
        result.Text.Should().Be("test transcript");
    }

    [Fact]
    public async Task FakeChatClient_ReturnsConfiguredResponse()
    {
        using var llm = new FakeChatClient("test response");
        var result = await llm.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);
        result.Text.Should().Be("test response");
    }

    [Fact]
    public async Task FakeTextToSpeechClient_ReturnsSilencePcm()
    {
        using var tts = new FakeTextToSpeechClient(100);
        var result = await tts.GetAudioAsync("hello");
        var bytes = result.RawRepresentation as byte[];
        bytes.Should().NotBeNull();
        bytes!.Length.Should().Be(AudioFormat.Pcm16KhzMono.BytesPerSecond * 100 / 1000);
    }

    [Fact]
    public async Task DefaultSpeechPipeline_WithFakeProviders_RunsEndToEnd()
    {
        await using var pipeline = new DefaultSpeechPipeline(
            stt: new FakeSpeechToTextClient("hello"),
            llm: new FakeChatClient("world"),
            tts: new FakeTextToSpeechClient(50));

        var format = AudioFormat.Pcm16KhzMono;
        var silence = new byte[format.BytesPerSecond / 4]; // 250ms
        await using var input = new MemoryAudioInput(format, silence, frameDurationMs: 20);
        await using var output = new NullAudioOutput();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await pipeline.RunAsync(input, output, cts.Token);
    }
}
