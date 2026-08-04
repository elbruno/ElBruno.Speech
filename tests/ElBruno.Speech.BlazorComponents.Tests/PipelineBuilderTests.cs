using ElBruno.Speech;

public class PipelineBuilderTests : BunitContext
{
    [Fact]
    public void Renders_apply_button()
    {
        var cut = Render<PipelineBuilder>();
        cut.Find("button").TextContent.Should().Be("Apply");
        cut.FindAll("input").Select(input => input.GetAttribute("value"))
            .Should().Equal("20", "64", "200");
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
        received.ChannelCapacity.Should().Be(64);
        received.PreRollMs.Should().Be(200);
    }

    [Fact]
    public async Task Input_changes_are_sent_when_apply_is_clicked()
    {
        SpeechPipelineOptions? received = null;
        var cut = Render<PipelineBuilder>(p =>
            p.Add(x => x.OnConfigured, (SpeechPipelineOptions o) => received = o));
        cut.FindAll("input")[0].Change("40");
        cut.FindAll("input")[1].Change("128");
        cut.FindAll("input")[2].Change("500");

        cut.Find("button").Click();
        await cut.InvokeAsync(() => Task.CompletedTask);

        received.Should().NotBeNull();
        received!.FrameDurationMs.Should().Be(40);
        received.ChannelCapacity.Should().Be(128);
        received.PreRollMs.Should().Be(500);
    }

    [Fact]
    public async Task Invalid_number_input_keeps_the_previous_value()
    {
        SpeechPipelineOptions? received = null;
        var cut = Render<PipelineBuilder>(p =>
            p.Add(x => x.OnConfigured, (SpeechPipelineOptions o) => received = o));

        cut.FindAll("input")[0].Change("not-a-number");
        cut.Find("button").Click();
        await cut.InvokeAsync(() => Task.CompletedTask);

        received.Should().NotBeNull();
        received!.FrameDurationMs.Should().Be(20);
    }
}
