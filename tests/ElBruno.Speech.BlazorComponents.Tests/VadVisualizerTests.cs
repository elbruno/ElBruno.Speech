using System.Globalization;

public class VadVisualizerTests : BunitContext
{
    public VadVisualizerTests()
    {
        Services.AddScoped<SpeechStateService>();
    }

    [Fact]
    public void Renders_silence_class_when_probability_is_zero()
    {
        var cut = Render<VadVisualizer>(p => p.Add(x => x.Probability, 0f));
        cut.Find(".vad-bar-fill").ClassList.Should().Contain("silence");
        cut.Find(".vad-bar-fill").GetAttribute("style").Should().Be("width: 0%");
        cut.Find(".vad-prob").TextContent.Should().Be("0%");
    }

    [Fact]
    public void Probability_label_is_culture_independent()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var cut = Render<VadVisualizer>(p => p.Add(x => x.Probability, 0f));

            cut.Find(".vad-prob").TextContent.Should().Be("0%");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void Renders_speech_class_when_probability_is_high()
    {
        var cut = Render<VadVisualizer>(p => p.Add(x => x.Probability, 0.9f));
        cut.Find(".vad-bar-fill").ClassList.Should().Contain("speech");
        cut.Find(".vad-bar-fill").GetAttribute("style").Should().Be("width: 90%");
    }

    [Fact]
    public void Probability_at_threshold_is_speech()
    {
        var cut = Render<VadVisualizer>(p => p.Add(x => x.Probability, 0.5f));
        cut.Instance.IsSpeech.Should().BeTrue();
    }

    [Fact]
    public void Hides_probability_label_when_disabled()
    {
        var cut = Render<VadVisualizer>(p => p.Add(x => x.ShowProbabilityLabel, false));
        cut.FindAll(".vad-prob").Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateProbability_updates_bar()
    {
        var cut = Render<VadVisualizer>();
        await cut.InvokeAsync(() => cut.Instance.UpdateProbability(0.75f));
        cut.Instance.Probability.Should().Be(0.75f);
        cut.Find(".vad-bar-fill").GetAttribute("style").Should().Be("width: 75%");
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(2f, 1f)]
    public async Task UpdateProbability_clamps_invalid_values(float input, float expected)
    {
        var cut = Render<VadVisualizer>();
        await cut.InvokeAsync(() => cut.Instance.UpdateProbability(input));

        cut.Instance.Probability.Should().Be(expected);
    }
}
