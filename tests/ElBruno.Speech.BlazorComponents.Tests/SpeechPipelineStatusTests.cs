public class SpeechPipelineStatusTests : BunitContext
{
    [Fact]
    public void Renders_idle_badge_by_default()
    {
        var cut = Render<SpeechPipelineStatus>();
        cut.Markup.Should().Contain("Idle");
    }

    [Fact]
    public void Shows_label_when_ShowStateLabel_is_true()
    {
        var cut = Render<SpeechPipelineStatus>(p => p.Add(x => x.ShowStateLabel, true));
        cut.Markup.Should().Contain("state-label");
    }

    [Fact]
    public void Updates_badge_class_for_listening_state()
    {
        var cut = Render<SpeechPipelineStatus>(p => p.Add(x => x.CurrentState, PipelineState.Listening));
        cut.Markup.Should().Contain("badge-success");
    }
}
