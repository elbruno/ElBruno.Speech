export async function getInputDevices() {
    if (!navigator.mediaDevices?.enumerateDevices) {
        return [];
    }

    const devices = await navigator.mediaDevices.enumerateDevices();
    return devices
        .filter(device => device.kind === "audioinput")
        .map(device => ({
            id: device.deviceId,
            name: device.label || "Microphone"
        }));
}
