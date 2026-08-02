public class VadVisualizerTests : BunitContext
{
    [Fact]
    public void Renders_silence_class_when_probability_is_zero()
    {
        var cut = Render<VadVisualizer>(p => p.Add(x => x.Probability, 0f));
        cut.Markup.Should().Contain("silence");
    }

    [Fact]
    public void Renders_speech_class_when_probability_is_high()
    {
        var cut = Render<VadVisualizer>(p => p.Add(x => x.Probability, 0.9f));
        cut.Markup.Should().Contain("speech");
    }

    [Fact]
    public void UpdateProbability_updates_bar()
    {
        var cut = Render<VadVisualizer>();
        cut.InvokeAsync(() => cut.Instance.UpdateProbability(0.75f));
        cut.Instance.Probability.Should().Be(0.75f);
    }
}
