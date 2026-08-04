using Microsoft.JSInterop;

namespace ElBruno.Speech.BlazorComponents;

/// <summary>
/// Enumerates browser microphone devices through the Web Audio device API.
/// </summary>
public sealed class BrowserAudioDeviceProvider(IJSRuntime jsRuntime) : IAudioDeviceProvider, IAsyncDisposable
{
    private IJSObjectReference? _module;

    /// <inheritdoc />
    public async Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                ct,
                "./_content/ElBruno.Speech.BlazorComponents/js/audioDevices.js");

            var devices = await _module.InvokeAsync<AudioDeviceInfo[]>("getInputDevices", ct);
            return devices ?? [];
        }
        catch (JSException) when (!ct.IsCancellationRequested)
        {
            // Device enumeration is unavailable during prerendering or when the
            // browser does not expose mediaDevices. The host may provide a
            // platform-specific IAudioDeviceProvider instead.
            return [];
        }
        catch (InvalidOperationException) when (!ct.IsCancellationRequested)
        {
            return [];
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The browser circuit may already be gone during disposal.
            }
        }
    }
}
