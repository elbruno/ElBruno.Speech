using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ElBruno.Speech;
using ElBruno.Speech.Pipeline;
using ElBruno.Speech.TestUtils;
using FluentAssertions;

namespace ElBruno.Speech.Pipeline.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSpeechPipeline_RegistersISpeechPipeline()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISpeechToTextClient>(new FakeSpeechToTextClient());
        services.AddSingleton<IChatClient>(new FakeChatClient());
        services.AddSingleton<ITextToSpeechClient>(new FakeTextToSpeechClient());
        services.AddSpeechPipeline();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetService<ISpeechPipeline>();
        pipeline.Should().NotBeNull();
        pipeline.Should().BeOfType<DefaultSpeechPipeline>();
    }

    [Fact]
    public void AddSpeechPipeline_WithOptions_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISpeechToTextClient>(new FakeSpeechToTextClient());
        services.AddSingleton<IChatClient>(new FakeChatClient());
        services.AddSingleton<ITextToSpeechClient>(new FakeTextToSpeechClient());

        var opts = new SpeechPipelineOptions { FrameDurationMs = 40 };
        services.AddSpeechPipeline(opts);

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<SpeechPipelineOptions>();
        resolved.FrameDurationMs.Should().Be(40);
    }
}
