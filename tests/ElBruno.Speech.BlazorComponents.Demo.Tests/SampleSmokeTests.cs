using Bunit;
using BlazorSpeechDemo.Pages;
using ElBruno.Speech.BlazorComponents;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class SampleSmokeTests : BunitContext
{
    public SampleSmokeTests()
    {
        Services.AddSingleton<SpeechStateService>();
        Services.AddScoped<IAudioDeviceProvider, DefaultAudioDeviceProvider>();
    }

    [Fact]
    public void Home_renders_every_speech_component()
    {
        var cut = Render<Home>();

        cut.Markup.Should().Contain("Blazor Speech Demo");
        cut.FindComponent<SpeechPipelineStatus>().Should().NotBeNull();
        cut.FindComponent<SttTranscriptBox>().Should().NotBeNull();
        cut.FindComponent<TtsPlayer>().Should().NotBeNull();
        cut.FindComponent<MicrophoneSelector>().Should().NotBeNull();
        cut.FindComponent<VadVisualizer>().Should().NotBeNull();
        cut.FindComponent<PipelineBuilder>().Should().NotBeNull();
    }

    [Fact]
    public void Home_controls_update_component_state()
    {
        var cut = Render<Home>();

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Speaking").Click();
        cut.FindComponent<SpeechPipelineStatus>().Instance.CurrentState.Should().Be(PipelineState.Speaking);

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Add Sample").Click();
        cut.Markup.Should().Contain("Sample transcription");

        cut.FindComponent<PipelineBuilder>().Find("button").Click();
        cut.Markup.Should().Contain("Frame: 20 ms | Capacity: 64");

        cut.FindAll("input[type=range]")[1].Change("90");
        cut.FindComponent<VadVisualizer>().Instance.Probability.Should().Be(0.9f);
    }
}
