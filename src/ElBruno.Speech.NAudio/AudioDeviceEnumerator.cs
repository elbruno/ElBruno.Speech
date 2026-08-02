using System.Runtime.InteropServices;
using NAudio.Wave;

namespace ElBruno.Speech.NAudio;

/// <summary>Enumerates available audio input and output devices via NAudio.</summary>
public static class AudioDeviceEnumerator
{
    /// <summary>Returns all available microphone (WaveIn) devices.</summary>
    public static IReadOnlyList<AudioDeviceInfo> GetInputDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            devices.Add(new AudioDeviceInfo(i, caps.ProductName, AudioDeviceKind.Input));
        }
        return devices;
    }

    /// <summary>Returns all available speaker (WaveOut) devices.</summary>
    public static IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        int count = WaveInterop.waveOutGetNumDevs();
        for (int i = 0; i < count; i++)
        {
            WaveInterop.waveOutGetDevCaps(
                new IntPtr(i),
                out var caps,
                Marshal.SizeOf<WaveOutCapabilities>());
            devices.Add(new AudioDeviceInfo(i, caps.ProductName, AudioDeviceKind.Output));
        }
        return devices;
    }
}

/// <summary>Describes a system audio device.</summary>
/// <param name="DeviceNumber">NAudio device index.</param>
/// <param name="Name">Human-readable device name.</param>
/// <param name="Kind">Input (microphone) or output (speaker).</param>
public sealed record AudioDeviceInfo(int DeviceNumber, string Name, AudioDeviceKind Kind);

/// <summary>Whether a device is for audio input or output.</summary>
public enum AudioDeviceKind { Input, Output }
