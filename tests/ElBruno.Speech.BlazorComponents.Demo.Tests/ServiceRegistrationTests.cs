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
        var descriptor = services.Single(x => x.ServiceType == typeof(IAudioDeviceProvider));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
        descriptor.ImplementationType.Should().Be(typeof(BrowserAudioDeviceProvider));
    }

    [Fact]
    public void AddSpeechBlazorComponents_registers_scoped_state()
    {
        var services = new ServiceCollection();
        services.AddSpeechBlazorComponents();
        var descriptor = services.Single(x => x.ServiceType == typeof(SpeechStateService));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void SpeechStateService_is_scoped_per_service_provider_scope()
    {
        var services = new ServiceCollection();
        services.AddSpeechBlazorComponents();
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<SpeechStateService>();
        var sameScope = firstScope.ServiceProvider.GetRequiredService<SpeechStateService>();
        var second = secondScope.ServiceProvider.GetRequiredService<SpeechStateService>();

        sameScope.Should().BeSameAs(first);
        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public async Task DefaultAudioDeviceProvider_returns_at_least_one_device()
    {
        var p = new DefaultAudioDeviceProvider();
        var devices = await p.GetInputDevicesAsync();
        devices.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DefaultAudioDeviceProvider_honors_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => new DefaultAudioDeviceProvider().GetInputDevicesAsync(cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
