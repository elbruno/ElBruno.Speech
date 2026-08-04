using Microsoft.Extensions.DependencyInjection;

public class MicrophoneSelectorTests : BunitContext
{
    public MicrophoneSelectorTests()
    {
        Services.AddScoped<SpeechStateService>();
        Services.AddScoped<IAudioDeviceProvider, DefaultAudioDeviceProvider>();
    }

    [Fact]
    public void Renders_default_device_option()
    {
        var cut = Render<MicrophoneSelector>();
        cut.FindAll("option").Should().ContainSingle();
        cut.Find("option").TextContent.Should().Be("Default Microphone");
        cut.Instance.SelectedDeviceId.Should().Be("default");
    }

    [Fact]
    public void SelectedDeviceId_parameter_is_preserved()
    {
        var cut = Render<MicrophoneSelector>(p => p.Add(x => x.SelectedDeviceId, "default"));
        cut.Find("option").HasAttribute("selected").Should().BeTrue();
    }

    [Fact]
    public async Task Selection_change_fires_callback_with_new_device()
    {
        var provider = new TestAudioDeviceProvider(
            new AudioDeviceInfo("mic-1", "Microphone 1"),
            new AudioDeviceInfo("mic-2", "Microphone 2"));
        Services.AddSingleton<IAudioDeviceProvider>(provider);
        string? selected = null;
        var cut = Render<MicrophoneSelector>(p =>
            p.Add(x => x.OnDeviceChanged, (string id) => selected = id));

        cut.Find("select").Change("mic-2");
        await cut.InvokeAsync(() => Task.CompletedTask);

        cut.Instance.SelectedDeviceId.Should().Be("mic-2");
        selected.Should().Be("mic-2");
    }

    [Fact]
    public void Empty_provider_renders_an_empty_select()
    {
        Services.AddSingleton<IAudioDeviceProvider>(new TestAudioDeviceProvider());

        var cut = Render<MicrophoneSelector>();

        cut.FindAll("option").Should().BeEmpty();
        cut.Instance.SelectedDeviceId.Should().BeNull();
    }

    private sealed class TestAudioDeviceProvider(params AudioDeviceInfo[] devices) : IAudioDeviceProvider
    {
        public Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AudioDeviceInfo>>(devices);
    }
}
