using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Newtonsoft.Json;
using Valve.VR;
using VRCOSC.App.SDK.Parameters.Queryable;

namespace FuviiOSC.Haptickle;

public partial class HaptickleModuleRuntimeView
{
    public HaptickleModule Module { get; }
    public ObservableCollection<HapticTrigger> Trackers { get; } = new();

    public HaptickleModuleRuntimeView(HaptickleModule module)
    {
        DataContext = this;
        Module = module;

        InitializeComponent();
        UpdateDeviceList();
    }

    public void UpdateDeviceList()
    {
        Trackers.Clear();
        lock (Module)
        {
            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (Module.openVrSystem?.GetTrackedDeviceClass(i) == ETrackedDeviceClass.GenericTracker)
                {
                    ETrackedPropertyError err = ETrackedPropertyError.TrackedProp_Success;
                    var sb = new System.Text.StringBuilder(64);
                    Module.openVrSystem.GetStringTrackedDeviceProperty(i, ETrackedDeviceProperty.Prop_SerialNumber_String, sb, (uint)sb.Capacity, ref err);

                    HapticTrigger? savedTrigger = Module.HapticTriggers.Find(trigger => trigger.DeviceSerialNumber == sb.ToString());
                    Module.LogDebug($"Found tracker {i} with serial number {savedTrigger.DeviceSerialNumber} and saved trigger: {savedTrigger.HapticTriggerParams[0].Name} {savedTrigger.HapticTriggerParams[0].Name.Value}");
                    if (savedTrigger != null)
                    {
                        Trackers.Add(savedTrigger);
                    }
                    else
                    {
                        Trackers.Add(new HapticTrigger{
                            DeviceIndex = (int)i,
                            DeviceSerialNumber = sb.ToString(),
                        });
                    }
                }
            }
        }
    }

    private void StrengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider && slider.DataContext is HapticTrigger tracker)
        {
            int savedTriggerIndex = Module.HapticTriggers.FindIndex(trigger => trigger.DeviceSerialNumber == tracker.DeviceSerialNumber);
            if (savedTriggerIndex >= 0)
                Module.HapticTriggers[savedTriggerIndex].HapticStrength = (float)slider.Value;
            else
                Module.HapticTriggers.Add(tracker);
        }
    }
}

public class HapticTrigger
{
    [JsonProperty("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("device_index")]
    public int DeviceIndex { get; set; } = 0;
    [JsonProperty("device_serial_number")]
    public string DeviceSerialNumber { get; set; } = "";
    [JsonProperty("haptic_strength")]
    public float HapticStrength { get; set; } = 0.5f;
    [JsonProperty("haptic_trigger_params")]
    public ObservableCollection<HapticTriggerQueryableParameter> HapticTriggerParams { get; set; } = [];

    public bool Equals(HapticTrigger? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return DeviceIndex.Equals(other.DeviceIndex) && HapticStrength.Equals(other.HapticStrength) && HapticTriggerParams.SequenceEqual(other.HapticTriggerParams);
    }
}

public class HapticTriggerQueryableParameter : QueryableParameter
{
}

public class AlternationIndexToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && index > 0)
            return Visibility.Visible;
        else
            return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
