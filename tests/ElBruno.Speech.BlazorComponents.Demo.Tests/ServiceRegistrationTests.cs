using Microsoft.Extensions.DependencyInjection;
using ElBruno.Speech.BlazorComponents;
using FluentAssertions;
using Xunit;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddSpeechBlazorComponents_registers_IAudioDeviceProvider()
    {
        var services = new ServiceCollection();
        services.AddSpeechBlazorComponents();
        var provider = services.BuildServiceProvider();
        var svc = provider.GetService<IAudioDeviceProvider>();
        svc.Should().NotBeNull().And.BeOfType<DefaultAudioDeviceProvider>();
    }

    [Fact]
    public async Task DefaultAudioDeviceProvider_returns_at_least_one_device()
    {
        var p = new DefaultAudioDeviceProvider();
        var devices = await p.GetInputDevicesAsync();
        devices.Should().NotBeEmpty();
    }
}
