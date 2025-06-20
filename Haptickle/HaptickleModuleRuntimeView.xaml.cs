using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Valve.VR;
using VRCOSC.App.Utils;
using WindowsInput.Events;

namespace FuviiOSC.Haptickle;

public partial class HaptickleModuleRuntimeView
{
    public HaptickleModule Module { get; }
    public ObservableCollection<HapticTrigger> Trackers { get; } = [];

    private readonly DispatcherTimer _vrEventTimer = new DispatcherTimer();

    public HaptickleModuleRuntimeView(HaptickleModule module)
    {
        DataContext = this;
        Module = module;

        InitializeComponent();
        UpdateDeviceList(GetConnectedTrackerIndexes());

        // Set up a timer to check for VR device list updates every 5 seconds
        _vrEventTimer.Interval = TimeSpan.FromMilliseconds(5000);
        _vrEventTimer.Tick += CheckForVRDeviceListUpdate;
        _vrEventTimer.Start();
    }

    private void CheckForVRDeviceListUpdate(object? sender, EventArgs e)
    {
        IEnumerable<uint> currentlyConnectedTrackerIndexes = GetConnectedTrackerIndexes().ToList();
        IEnumerable<uint> trackerIndexes = Trackers.Select(t => (uint)t.DeviceIndex).ToList();

        if (!currentlyConnectedTrackerIndexes.ToHashSet().SetEquals(trackerIndexes))
            UpdateDeviceList(currentlyConnectedTrackerIndexes);
    }

    public void UpdateDeviceList(IEnumerable<uint>? connectedIndexes = null)
    {
        Trackers.Clear();
        lock (Module)
        {
            HashSet<uint>? indexes = connectedIndexes?.ToHashSet();
            indexes?.ForEach(i =>
            {
                if (Module.openVrSystem?.GetTrackedDeviceClass(i) == ETrackedDeviceClass.GenericTracker)
                {
                    ETrackedPropertyError trackedError = ETrackedPropertyError.TrackedProp_Success;
                    StringBuilder strBuilder = new StringBuilder(64);
                    Module.openVrSystem.GetStringTrackedDeviceProperty(i, ETrackedDeviceProperty.Prop_SerialNumber_String, strBuilder, (uint)strBuilder.Capacity, ref trackedError);

                    string serialNumber = strBuilder.ToString();
                    HapticTrigger? savedTrigger = Module.HapticTriggers.Find(trigger => trigger.DeviceSerialNumber == serialNumber);
                    if (savedTrigger != null)
                    {
                        savedTrigger.DeviceIndex = (int)i;
                        Trackers.Add(savedTrigger);
                    }
                    else
                        Trackers.Add(new HapticTrigger
                        {
                            DeviceIndex = (int)i,
                            DeviceSerialNumber = serialNumber,
                            HapticTriggerParams = [new HapticTriggerQueryableParameter()],
                        });
                }
            });
        }
    }

    private Collection<uint> GetConnectedTrackerIndexes()
    {
        Collection<uint> indexes = [];
        if (Module.openVrSystem == null)
            return [];

        for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
        {
            if (Module.openVrSystem.GetTrackedDeviceClass(i) == ETrackedDeviceClass.GenericTracker)
            {
                bool isConnected = Module.openVrSystem.IsTrackedDeviceConnected(i);
                if (isConnected)
                    indexes.Add(i);
            }
        }
        return indexes;
    }

    private async void Button_Identify(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is HapticTrigger trigger)
        {
            Module.IdentifyDevice(trigger);
            await BlinkAndDisableAsync(button, Colors.LimeGreen);
        }
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.DataContext is HapticTrigger trigger)
        {
            Module.StopPulseLoop(trigger);
        }
    }

    public async Task BlinkAndDisableAsync(Button button, Color blinkColor, int durationMs = 400, int blinkCount = 4)
    {
        try
        {
            if (button == null) return;

            TextBlock identifyIcon = (TextBlock)button.Template.FindName("IdentifyIcon", button);
            if (identifyIcon == null) return;

            Brush origBrush = identifyIcon.Foreground;

            button.IsEnabled = false;
            for (int i = 0; i < blinkCount; i++)
            {
                Brush tempBrush = new SolidColorBrush(blinkColor);
                identifyIcon.Foreground = tempBrush;
                await Task.Delay(durationMs / blinkCount);
                identifyIcon.Foreground = origBrush;
                // Pause between blinks, except after the last one
                if (i < blinkCount - 1)
                    await Task.Delay(durationMs / blinkCount);
            }
            await Task.Delay(durationMs);
            button.IsEnabled = true;
        }
        catch (Exception error)
        {
            Module.LogDebug($"Error in BlinkAndDisableAsync: {error}");
        }
    }
}
