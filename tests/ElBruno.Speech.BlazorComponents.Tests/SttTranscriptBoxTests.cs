public class SttTranscriptBoxTests : BunitContext
{
    public SttTranscriptBoxTests()
    {
        Services.AddScoped<SpeechStateService>();
    }

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
        cut.Find(".segment.final .text").TextContent.Should().Be("Hello world");
    }

    [Fact]
    public async Task AddSegment_marks_interim_text_and_can_hide_timestamps()
    {
        var cut = Render<SttTranscriptBox>(p => p.Add(x => x.ShowTimestamps, false));
        await cut.InvokeAsync(() => cut.Instance.AddSegment("still speaking", isFinal: false));

        cut.Find(".segment").ClassList.Should().Contain("interim");
        cut.FindAll(".timestamp").Should().BeEmpty();
    }

    [Fact]
    public async Task MaxSegments_keeps_only_the_most_recent_segments()
    {
        var cut = Render<SttTranscriptBox>(p => p.Add(x => x.MaxSegments, 2));
        await cut.InvokeAsync(() => cut.Instance.AddSegment("first"));
        await cut.InvokeAsync(() => cut.Instance.AddSegment("second"));
        await cut.InvokeAsync(() => cut.Instance.AddSegment("third"));

        cut.FindAll(".segment").Should().HaveCount(2);
        cut.Markup.Should().NotContain("first");
        cut.Markup.Should().Contain("second").And.Contain("third");
    }

    [Fact]
    public async Task MaxSegments_zero_renders_no_segments()
    {
        var cut = Render<SttTranscriptBox>(p => p.Add(x => x.MaxSegments, 0));
        await cut.InvokeAsync(() => cut.Instance.AddSegment("ignored"));

        cut.FindAll(".segment").Should().BeEmpty();
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
