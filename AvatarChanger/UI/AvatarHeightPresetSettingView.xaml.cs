using System.Windows;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Modules.Attributes.Settings;

namespace FuviiOSC.AvatarChanger.UI;

public partial class AvatarHeightPresetSettingView
{
    public ModuleSetting ModuleSetting { get; }

    private readonly ListModuleSetting listModuleSetting;

    public AvatarHeightPresetSettingView(Module _, ListModuleSetting moduleSetting)
    {
        listModuleSetting = moduleSetting;
        ModuleSetting = moduleSetting;

        InitializeComponent();

        DataContext = this;
    }

    private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
        FrameworkElement element = (FrameworkElement)sender;
        listModuleSetting.Remove(element.Tag);
    }

    private void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
        listModuleSetting.Add();
    }
}
