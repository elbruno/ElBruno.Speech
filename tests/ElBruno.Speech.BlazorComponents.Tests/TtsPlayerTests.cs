public class TtsPlayerTests : BunitContext
{
    [Fact]
    public void Play_button_is_enabled_by_default()
    {
        var cut = Render<TtsPlayer>();
        cut.Find("button").TextContent.Should().Contain("Play");
    }

    [Fact]
    public async Task Play_sets_IsPlaying_true()
    {
        var cut = Render<TtsPlayer>();
        await cut.InvokeAsync(() => cut.Instance.Play());
        cut.Instance.IsPlaying.Should().BeTrue();
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
