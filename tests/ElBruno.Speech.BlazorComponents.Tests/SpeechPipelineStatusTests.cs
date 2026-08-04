public class SpeechPipelineStatusTests : BunitContext
{
    public SpeechPipelineStatusTests()
    {
        Services.AddScoped<SpeechStateService>();
    }

    [Fact]
    public void Renders_idle_badge_and_label_by_default()
    {
        var cut = Render<SpeechPipelineStatus>();
        cut.Find(".badge").ClassList.Should().Contain("badge-secondary");
        cut.Find(".state-label").TextContent.Should().Be("Idle");
    }

    [Fact]
    public void Hides_state_label_when_disabled()
    {
        var cut = Render<SpeechPipelineStatus>(p => p.Add(x => x.ShowStateLabel, false));
        cut.FindAll(".state-label").Should().BeEmpty();
    }

    [Theory]
    [InlineData(PipelineState.Idle, "badge-secondary")]
    [InlineData(PipelineState.Listening, "badge-success")]
    [InlineData(PipelineState.Transcribing, "badge-warning")]
    [InlineData(PipelineState.Responding, "badge-info")]
    [InlineData(PipelineState.Speaking, "badge-primary")]
    public void Uses_expected_badge_class_for_each_state(PipelineState state, string expectedClass)
    {
        var cut = Render<SpeechPipelineStatus>(p => p.Add(x => x.CurrentState, state));
        cut.Find(".badge").ClassList.Should().Contain(expectedClass);
        cut.Find(".badge").TextContent.Should().Be(state.ToString());
    }

    [Fact]
    public void State_changes_raise_OnStateChanged()
    {
        PipelineState? received = null;
        var cut = Render<SpeechPipelineStatus>(p =>
            p.Add(x => x.OnStateChanged, (PipelineState state) => received = state));

        Services.GetRequiredService<SpeechStateService>().SetState(PipelineState.Speaking);
        cut.WaitForAssertion(() => received.Should().Be(PipelineState.Speaking));
    }
}
