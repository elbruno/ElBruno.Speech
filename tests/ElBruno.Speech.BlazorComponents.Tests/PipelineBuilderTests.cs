using ElBruno.Speech;

public class PipelineBuilderTests : BunitContext
{
    [Fact]
    public void Renders_apply_button()
    {
        var cut = Render<PipelineBuilder>();
        cut.Markup.Should().Contain("Apply");
    }

    [Fact]
    public async Task Configure_fires_OnConfigured_with_defaults()
    {
        SpeechPipelineOptions? received = null;
        var cut = Render<PipelineBuilder>(p =>
            p.Add(x => x.OnConfigured, (SpeechPipelineOptions o) => { received = o; }));
        await cut.InvokeAsync(() => cut.Instance.Configure());
        received.Should().NotBeNull();
        received!.FrameDurationMs.Should().Be(20);
    }
}
