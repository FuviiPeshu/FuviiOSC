using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using VRCOSC.App.Utils;

namespace FuviiOSC.SqueakMeter;

public record AudioDeviceInfo(string ID, string FriendlyName, bool IsEnabled = true);

public class AudioDeviceNotificationClient : IMMNotificationClient
{
    public event Action? DevicesChanged;
    public event Action<string, string>? DeviceError;

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => DevicesChanged?.Invoke();
    public void OnDeviceAdded(string deviceId) => DevicesChanged?.Invoke();
    public void OnDeviceRemoved(string deviceId) => DevicesChanged?.Invoke();
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => DevicesChanged?.Invoke();
    public void OnPropertyValueChanged(string deviceId, PropertyKey key) => DevicesChanged?.Invoke();
    public void OnSelectedDeviceError(string deviceId, string errorMessage) => DeviceError?.Invoke(deviceId, errorMessage);
}

public partial class AudioDeviceModuleRuntimeView : INotifyPropertyChanged
{
    public SqueakMeterModule Module { get; }
    public ObservableCollection<AudioDeviceInfo> AudioDevices { get; } = new();
    public Observable<string> SelectedDeviceId { get; set; } = new();

    private bool suppressSelectionChanged = false;
    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
            }
        }
    }

    public ObservableCollection<AudioDeviceInfo> DisabledDevices { get; } = new();

    public AudioDeviceModuleRuntimeView(SqueakMeterModule module)
    {
        InitializeComponent();
        DataContext = this;
        Module = module;
        Module.disabledDeviceList.ForEach(device => DisabledDevices.Add(device));

        if (Module.notificationClient == null)
            Module.notificationClient = new AudioDeviceNotificationClient();

        Module.notificationClient.DevicesChanged += UpdateDeviceList;
        Module.notificationClient.DeviceError += DeviceErrorOccurred;
        UpdateDeviceList();
    }

    public void DeviceErrorOccurred(string deviceId, string errorMessage)
    {
        AudioDeviceInfo? device = AudioDevices.First(d => d.ID == deviceId);
        if (device != null && !DisabledDevices.Any(device => device.ID == deviceId))
        {
            int index = AudioDevices.IndexOf(device);
            AudioDevices[index] = device with { IsEnabled = false };
            DisabledDevices.Add(AudioDevices[index]);
            Module.disabledDeviceList.Add(AudioDevices[index]);
        }
        Module.selectedDevice = null;
        SelectedDeviceId.Value = String.Empty;
        ErrorMessage = errorMessage;
    }

    public void RemoveDisabledDevice(object sender, RoutedEventArgs e)
    {
        FrameworkElement element = (FrameworkElement)sender;
        AudioDeviceInfo device = (AudioDeviceInfo)element.Tag;

        if (device != null)
        {
            int index = AudioDevices.IndexOf(device);
            AudioDevices[index] = device with { IsEnabled = true };
            DisabledDevices.Remove(device);
            Module.disabledDeviceList.RemoveIf(el => el.ID == device.ID);
        }
    }

    public void UpdateDeviceList()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(UpdateDeviceList));
            return;
        }

        try
        {
            ErrorMessage = null;

            MMDeviceCollection audioDevices = Module.GetAudioOutputDevices();
            if (audioDevices.Count == 0)
            {
                Module.LogDebug("No audio devices found.");
                ErrorMessage = "No audio devices found.";
                return;
            }

            // Save the current selection
            string previousSelectedId = Module.selectedDevice?.ID ?? SelectedDeviceId.Value;

            // Update UI with the new list of devices
            suppressSelectionChanged = true;
            SelectedDeviceId.Value = String.Empty;
            AudioDevices.Clear();
            foreach (MMDevice device in audioDevices)
            {
                bool isEnabled = !DisabledDevices.Any(d => d.ID == device.ID);
                AudioDevices.Add(new AudioDeviceInfo(device.ID, device.FriendlyName, isEnabled));
            }

            // Reset audio device to the first device if selected audio device is no longer available
            if (Module.activeDevice == null || !audioDevices.Any(d => d.ID == previousSelectedId))
            {
                Module.SetCaptureDevice(audioDevices[0].ID);
            }
            else
            {
                SelectedDeviceId.Value = previousSelectedId;
            }
            suppressSelectionChanged = false;
        }
        catch (Exception error)
        {
            suppressSelectionChanged = false;
            ErrorMessage = $"Error updating audio devices: {error.Message}";
            Module.LogDebug($"Error updating audio devices: {error.Message}");
        }
    }

    private void DeviceSelection_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionChanged)
            return;

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => DeviceSelection_OnSelectionChanged(sender, e)));
            return;
        }

        ErrorMessage = null;
        if (sender is not ComboBox comboBox)
        {
            ErrorMessage = "Device selection sender is not a ComboBox.";
            return;
        }

        if (comboBox.SelectedItem is AudioDeviceInfo selectedItem)
        {
            SelectedDeviceId.Value = selectedItem.ID;
            Module.selectedDevice = selectedItem;
            Module.SetCaptureDevice(selectedItem.ID);
            Module.LogDebug($"Selected audio device: {selectedItem.FriendlyName} ({selectedItem.ID})");
        }
        else
        {
            SelectedDeviceId.Value = string.Empty;
            Module.selectedDevice = null;
            ErrorMessage = "No audio device selected.";
            Module.LogDebug("No audio device selected.");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
