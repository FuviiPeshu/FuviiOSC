using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace FuviiOSC.SqueakMeter;

public record AudioDeviceInfo(string ID, string FriendlyName);

public class AudioDeviceNotificationClient : IMMNotificationClient
{
    public event Action? DevicesChanged;

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => DevicesChanged?.Invoke();
    public void OnDeviceAdded(string deviceId) => DevicesChanged?.Invoke();
    public void OnDeviceRemoved(string deviceId) => DevicesChanged?.Invoke();
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => DevicesChanged?.Invoke();
    public void OnPropertyValueChanged(string deviceId, PropertyKey key) => DevicesChanged?.Invoke();
}

public partial class AudioDeviceModuleRuntimeView
{
    public SqueakMeterModule Module { get; }
    public ObservableCollection<AudioDeviceInfo> AudioDevices { get; } = new();

    public AudioDeviceModuleRuntimeView(SqueakMeterModule module)
    {
        InitializeComponent();
        DataContext = this;

        Module = module;
        if (Module.notificationClient != null)
        {
            Module.notificationClient.DevicesChanged += HandleUpdateDeviceList;
        }
        else
        {
            Module.notificationClient = new AudioDeviceNotificationClient();
            Module.notificationClient.DevicesChanged += HandleUpdateDeviceList;
        }
        HandleUpdateDeviceList();
    }

    public MMDeviceCollection GetAudioOutputDevices() => Module.enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

    public void HandleUpdateDeviceList()
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                MMDeviceCollection audioDevices = GetAudioOutputDevices();
                if (audioDevices.Count == 0)
                {
                    Module.LogDebug("No audio devices found.");
                    return;
                }

                // Update UI with the new list of devices
                AudioDevices.Clear();
                foreach (MMDevice device in audioDevices)
                    AudioDevices.Add(new AudioDeviceInfo(device.ID, device.FriendlyName));

                // Reset audio device to the first device if audio device is no longer available
                if (Module.activeDevice == null || !audioDevices.Any(d => d.ID == Module.activeDevice.ID))
                    Module.activeDevice = audioDevices[0];
            });
        }
        catch (Exception error)
        {
            Module.LogDebug($"Error updating audio devices: {error.Message}");
        }
    }

    private void DeviceSelection_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
    }

    private void DeviceSelection_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            ComboBox comboBox = (ComboBox)sender;
            string? selectedId = (string)comboBox.SelectedValue;
            if (selectedId != null)
                Module.SetCaptureDevice(selectedId);
        });
    }
}
