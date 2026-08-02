using ElBruno.Speech;
using FluentAssertions;

namespace ElBruno.Speech.Conformance.Tests;

/// <summary>Verifies ISpeechPipeline and SpeechPipelineOptions public contracts.</summary>
public sealed class PipelineContractConformanceTests
{
    [Fact]
    public void ISpeechPipeline_IsAsyncDisposable()
    {
        typeof(ISpeechPipeline).IsAssignableTo(typeof(IAsyncDisposable)).Should().BeTrue();
    }

    [Fact]
    public void ISpeechPipeline_HasRunAsync()
    {
        var method = typeof(ISpeechPipeline).GetMethod("RunAsync");
        method.Should().NotBeNull();
    }

    [Fact]
    public void ISpeechPipeline_HasStopAsync()
    {
        var method = typeof(ISpeechPipeline).GetMethod("StopAsync");
        method.Should().NotBeNull();
    }

    [Fact]
    public void SpeechPipelineOptions_Defaults_MatchPrd()
    {
        var opts = new SpeechPipelineOptions();
        opts.TargetFormat.Should().Be(AudioFormat.Pcm16KhzMono);
        opts.FrameDurationMs.Should().Be(20);
        opts.PreRollMs.Should().Be(200);
        opts.ChannelCapacity.Should().Be(64);
    }

    [Fact]
    public void IAudioInput_IsAsyncDisposable()
    {
        typeof(IAudioInput).IsAssignableTo(typeof(IAsyncDisposable)).Should().BeTrue();
    }

    [Fact]
    public void IAudioOutput_IsAsyncDisposable()
    {
        typeof(IAudioOutput).IsAssignableTo(typeof(IAsyncDisposable)).Should().BeTrue();
    }
}
