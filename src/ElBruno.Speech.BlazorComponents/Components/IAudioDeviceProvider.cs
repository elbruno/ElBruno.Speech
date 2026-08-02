namespace ElBruno.Speech.BlazorComponents;

public interface IAudioDeviceProvider
{
    Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(CancellationToken ct = default);
}
