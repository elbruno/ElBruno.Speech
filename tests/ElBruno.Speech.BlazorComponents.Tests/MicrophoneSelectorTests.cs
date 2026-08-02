using Microsoft.Extensions.DependencyInjection;

public class MicrophoneSelectorTests : BunitContext
{
    public MicrophoneSelectorTests()
    {
        Services.AddSingleton<IAudioDeviceProvider, DefaultAudioDeviceProvider>();
    }

    [Fact]
    public void Renders_default_device_option()
    {
        var cut = Render<MicrophoneSelector>();
        cut.Markup.Should().Contain("Default Microphone");
    }

    [Fact]
    public async Task OnDeviceChanged_fires_when_selection_changes()
    {
        string? selected = null;
        var cut = Render<MicrophoneSelector>(p =>
            p.Add(x => x.OnDeviceChanged, (string id) => { selected = id; }));
        await cut.Instance.OnDeviceChanged.InvokeAsync("default");
        selected.Should().Be("default");
    }
}
