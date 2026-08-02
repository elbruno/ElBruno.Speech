using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ElBruno.Speech;
using ElBruno.Speech.Audio;
using ElBruno.Speech.Pipeline;
using ElBruno.Speech.TestUtils;
using FluentAssertions;

namespace ElBruno.Speech.Pipeline.Tests;

public sealed class SpeechPipelineRealtimeAdapterTests
{
    private static DefaultSpeechPipeline MakePipeline() => new(
        stt: new FakeSpeechToTextClient("hello world"),
        llm: new FakeChatClient("test response"),
        tts: new FakeTextToSpeechClient(50));

    [Fact]
    public async Task CreateSessionAsync_ReturnsSession()
    {
        var adapter = new SpeechPipelineRealtimeAdapter(MakePipeline);
        var session = await adapter.CreateSessionAsync(new RealtimeSessionOptions());
        session.Should().NotBeNull();
        session.Should().BeAssignableTo<IRealtimeClientSession>();
        await session!.DisposeAsync();
    }

    [Fact]
    public async Task CreateSessionAsync_WithOptions_SessionHasOptions()
    {
        var adapter = new SpeechPipelineRealtimeAdapter(MakePipeline);
        var opts = new RealtimeSessionOptions { Model = "test-model" };
        var session = await adapter.CreateSessionAsync(opts);
        session!.Options!.Model.Should().Be("test-model");
        await session.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_AudioAppend_DoesNotThrow()
    {
        var adapter = new SpeechPipelineRealtimeAdapter(MakePipeline);
        await using var session = await adapter.CreateSessionAsync(new RealtimeSessionOptions());

        var pcm = new byte[640]; // one 20ms frame
        var content = new DataContent(pcm.AsMemory(), "audio/pcm;rate=16000");
        var msg = new InputAudioBufferAppendRealtimeClientMessage(content);

        Func<Task> act = async () => await session.SendAsync(msg);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetStreamingResponseAsync_WithSilenceAndCommit_YieldsAudioMessages()
    {
        var adapter = new SpeechPipelineRealtimeAdapter(MakePipeline);
        await using var session = await adapter.CreateSessionAsync(new RealtimeSessionOptions());

        // Send 200ms of silence (3200 bytes) then commit
        var silence = new byte[AudioFormat.Pcm16KhzMono.BytesPerSecond / 5]; // 200ms
        var silenceContent = new DataContent(silence.AsMemory(), "audio/pcm;rate=16000");
        await session.SendAsync(new InputAudioBufferAppendRealtimeClientMessage(silenceContent));
        await session.SendAsync(new InputAudioBufferCommitRealtimeClientMessage());

        var messages = new List<RealtimeServerMessage>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await foreach (var msg in session.GetStreamingResponseAsync(cts.Token))
            messages.Add(msg);

        messages.Should().NotBeEmpty();
        messages.Should().AllBeOfType<OutputTextAudioRealtimeServerMessage>();

        // All non-empty audio messages should have valid base64 content
        foreach (var msg in messages.Cast<OutputTextAudioRealtimeServerMessage>())
        {
            if (string.IsNullOrEmpty(msg.Audio)) continue;
            var buffer = new byte[msg.Audio.Length];
            Convert.TryFromBase64String(msg.Audio, buffer, out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task RealtimeServiceCollectionExtensions_RegistersAdapter()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISpeechToTextClient>(new FakeSpeechToTextClient());
        services.AddSingleton<IChatClient>(new FakeChatClient());
        services.AddSingleton<ITextToSpeechClient>(new FakeTextToSpeechClient());
        services.AddSpeechPipeline();
        services.AddSpeechPipelineRealtimeClient();

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<IRealtimeClient>();
        client.Should().NotBeNull();
        client.Should().BeOfType<SpeechPipelineRealtimeAdapter>();
    }
}

