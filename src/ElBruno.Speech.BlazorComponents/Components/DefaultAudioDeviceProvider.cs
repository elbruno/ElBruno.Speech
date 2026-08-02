namespace ElBruno.Speech.BlazorComponents;

public sealed class DefaultAudioDeviceProvider : IAudioDeviceProvider
{
    public Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AudioDeviceInfo>>([new AudioDeviceInfo("default", "Default Microphone")]);
}
