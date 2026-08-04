public class TtsPlayerTests : BunitContext
{
    [Fact]
    public void Play_button_is_enabled_by_default()
    {
        var cut = Render<TtsPlayer>();
        cut.Find("button").HasAttribute("disabled").Should().BeFalse();
        cut.FindAll("button")[1].HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("button")[2].HasAttribute("disabled").Should().BeTrue();
        cut.Instance.Volume.Should().Be(80);
        cut.Find("span").TextContent.Should().Be("80%");
    }

    [Fact]
    public async Task Play_enables_pause_and_disables_play()
    {
        var cut = Render<TtsPlayer>();
        await cut.InvokeAsync(() => cut.Instance.Play());

        cut.Instance.IsPlaying.Should().BeTrue();
        cut.Instance.IsPaused.Should().BeFalse();
        cut.FindAll("button")[0].HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("button")[1].HasAttribute("disabled").Should().BeFalse();
        cut.FindAll("button")[2].HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task Pause_sets_paused_state_and_stop_resets_it()
    {
        var cut = Render<TtsPlayer>();
        await cut.InvokeAsync(() => cut.Instance.Play());
        await cut.InvokeAsync(() => cut.Instance.Pause());

        cut.Instance.IsPlaying.Should().BeFalse();
        cut.Instance.IsPaused.Should().BeTrue();
        cut.FindAll("button")[2].HasAttribute("disabled").Should().BeFalse();

        await cut.InvokeAsync(() => cut.Instance.Stop());
        cut.Instance.IsPlaying.Should().BeFalse();
        cut.Instance.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void AutoPlay_starts_playback_after_first_render()
    {
        var cut = Render<TtsPlayer>(p => p.Add(x => x.AutoPlay, true));

        cut.WaitForAssertion(() => cut.Instance.IsPlaying.Should().BeTrue());
    }

    [Fact]
    public async Task Playback_commands_raise_the_matching_callbacks()
    {
        var events = new List<string>();
        var cut = Render<TtsPlayer>(p =>
        {
            p.Add(x => x.OnPlayRequested, () => events.Add("play"));
            p.Add(x => x.OnPauseRequested, () => events.Add("pause"));
            p.Add(x => x.OnStopRequested, () => events.Add("stop"));
        });

        await cut.InvokeAsync(() => cut.Instance.Play());
        await cut.InvokeAsync(() => cut.Instance.Pause());
        await cut.InvokeAsync(() => cut.Instance.Stop());

        events.Should().Equal("play", "pause", "stop");
    }

    [Fact]
    public void Hides_volume_label_and_updates_volume_from_input()
    {
        var cut = Render<TtsPlayer>(p => p.Add(x => x.ShowVolumeLabel, false));

        cut.FindAll("span").Should().BeEmpty();
        cut.Find("input[type=range]").Change("35");

        cut.Instance.Volume.Should().Be(35);
    }

    [Fact]
    public async Task Stop_fires_OnPlaybackCompleted()
    {
        var completed = false;
        var cut = Render<TtsPlayer>(p => p.Add(x => x.OnPlaybackCompleted, () => { completed = true; }));
        await cut.InvokeAsync(() => cut.Instance.Play());
        await cut.InvokeAsync(() => cut.Instance.Stop());
        completed.Should().BeTrue();
    }
}
