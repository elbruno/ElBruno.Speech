public class SttTranscriptBoxTests : BunitContext
{
    [Fact]
    public void Renders_empty_by_default()
    {
        var cut = Render<SttTranscriptBox>();
        cut.FindAll(".segment").Should().BeEmpty();
    }

    [Fact]
    public async Task AddSegment_shows_text()
    {
        var cut = Render<SttTranscriptBox>();
        await cut.InvokeAsync(() => cut.Instance.AddSegment("Hello world", isFinal: true));
        cut.Markup.Should().Contain("Hello world");
    }

    [Fact]
    public async Task Clear_removes_all_segments()
    {
        var cut = Render<SttTranscriptBox>();
        await cut.InvokeAsync(() => cut.Instance.AddSegment("First"));
        await cut.InvokeAsync(() => cut.Instance.Clear());
        cut.FindAll(".segment").Should().BeEmpty();
    }
}
