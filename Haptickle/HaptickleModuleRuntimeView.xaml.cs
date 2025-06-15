using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Valve.VR;

namespace FuviiOSC.Haptickle;

public partial class HaptickleModuleRuntimeView
{
    public HaptickleModule Module { get; }
    public ObservableCollection<HapticTrigger> Trackers { get; } = [];

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
                    ETrackedPropertyError trackedError = ETrackedPropertyError.TrackedProp_Success;
                    StringBuilder strBuilder = new StringBuilder(64);
                    Module.openVrSystem.GetStringTrackedDeviceProperty(i, ETrackedDeviceProperty.Prop_SerialNumber_String, strBuilder, (uint)strBuilder.Capacity, ref trackedError);

                    string serialNumber = strBuilder.ToString();
                    HapticTrigger? savedTrigger = Module.HapticTriggers.Find(trigger => trigger.DeviceSerialNumber == serialNumber);
                    if (savedTrigger != null)
                    {
                        Trackers.Add(savedTrigger);
                    }
                    else
                    {
                        Trackers.Add(new HapticTrigger
                        {
                            DeviceIndex = (int)i,
                            DeviceSerialNumber = serialNumber,
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
