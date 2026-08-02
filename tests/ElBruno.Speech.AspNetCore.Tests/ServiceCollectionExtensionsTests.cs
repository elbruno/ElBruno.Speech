using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ElBruno.Speech;
using ElBruno.Speech.AspNetCore;
using ElBruno.Speech.TestUtils;
using FluentAssertions;

namespace ElBruno.Speech.AspNetCore.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSpeechPipelineAspNetCore_RegistersRegistryAndPipeline()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISpeechToTextClient>(new FakeSpeechToTextClient());
        services.AddSingleton<IChatClient>(new FakeChatClient());
        services.AddSingleton<ITextToSpeechClient>(new FakeTextToSpeechClient());
        services.AddLogging();
        services.AddSpeechPipelineAspNetCore();

        var provider = services.BuildServiceProvider();

        provider.GetService<SpeechSessionRegistry>().Should().NotBeNull();
        provider.GetService<ISpeechPipeline>().Should().NotBeNull();
    }

    [Fact]
    public void AddSpeechPipelineAspNetCore_WithOptions_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISpeechToTextClient>(new FakeSpeechToTextClient());
        services.AddSingleton<IChatClient>(new FakeChatClient());
        services.AddSingleton<ITextToSpeechClient>(new FakeTextToSpeechClient());
        services.AddLogging();

        var opts = new SpeechPipelineOptions { FrameDurationMs = 40 };
        services.AddSpeechPipelineAspNetCore(opts);

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<SpeechPipelineOptions>().FrameDurationMs.Should().Be(40);
    }
}
