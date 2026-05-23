using NAudio.Wave;
using StreamMusicPlayer.Models;

namespace StreamMusicPlayer.Services;

public sealed class AudioOutputDeviceService
{
    public IReadOnlyList<AudioOutputDevice> GetOutputDevices()
    {
        var devices = new List<AudioOutputDevice>
        {
            new()
            {
                Id = AudioOutputDevice.DefaultId,
                DisplayName = "System default",
                DeviceNumber = -1,
                IsDefault = true
            }
        };

        for (var deviceNumber = 0; deviceNumber < WaveOut.DeviceCount; deviceNumber++)
        {
            var capabilities = WaveOut.GetCapabilities(deviceNumber);
            devices.Add(new AudioOutputDevice
            {
                Id = BuildDeviceId(deviceNumber, capabilities.ProductName),
                DisplayName = capabilities.ProductName,
                DeviceNumber = deviceNumber
            });
        }

        return devices;
    }

    public AudioOutputDevice ResolveDevice(string? storedDeviceId)
    {
        var devices = GetOutputDevices();
        return devices.FirstOrDefault(device => device.Id == storedDeviceId)
            ?? devices.First(device => device.IsDefault);
    }

    private static string BuildDeviceId(int deviceNumber, string productName)
    {
        return $"{deviceNumber}:{productName}";
    }
}
