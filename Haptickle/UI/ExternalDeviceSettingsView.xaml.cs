using System.Collections.ObjectModel;
using System.Windows;
using VRCOSC.App.SDK.Modules.Attributes.Settings;

namespace FuviiOSC.Haptickle.UI;

public class HaptickleModuleSetting : ListModuleSetting<DeviceMapping>
{
    public HaptickleModuleSetting()
        : base("", "", typeof(ExternalDeviceSettingsView), new ObservableCollection<DeviceMapping>())
    {
    }

    protected override DeviceMapping CreateItem() => new DeviceMapping();
}

public partial class ExternalDeviceSettingsView
{
    private readonly HaptickleModuleSetting moduleSetting;

    public ExternalDeviceSettingsView(HaptickleModule module, HaptickleModuleSetting moduleSetting)
    {
        InitializeComponent();

        this.moduleSetting = moduleSetting;
        DataContext = moduleSetting;
    }

    private void AddDeviceButton_OnClick(object sender, RoutedEventArgs e)
    {
        moduleSetting.Add();
    }

    private void RemoveDeviceButton_OnClick(object sender, RoutedEventArgs e)
    {
        FrameworkElement element = (FrameworkElement)sender;
        DeviceMapping device = (DeviceMapping)element.Tag;

        MessageBoxResult result = MessageBox.Show($"Are you sure you want to remove the device with IP: {device.DeviceIp}?", "Delete Device", MessageBoxButton.YesNo);
        if (result == MessageBoxResult.Yes)
        {
            moduleSetting.Remove(device);
        }
    }
}
