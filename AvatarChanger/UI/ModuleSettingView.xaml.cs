using System.Windows;
using VRCOSC.App.UI.Core;

namespace FuviiOSC.AvatarChanger.UI;

public partial class AvatarChangerModuleSettingView
{
    private readonly AvatarChangerModuleSetting moduleSetting;
    private WindowManager windowManager = null!;

    public AvatarChangerModuleSettingView(AvatarChangerModule instance, AvatarChangerModuleSetting moduleSetting)
    {
        InitializeComponent();

        this.moduleSetting = moduleSetting;
        DataContext = moduleSetting;
    }

    private void AvatarChangerModuleSettingView_OnLoaded(object sender, RoutedEventArgs e)
    {
        windowManager = new WindowManager(this);
    }

    private void AddInstanceButton_OnClick(object sender, RoutedEventArgs e)
    {
        moduleSetting.Add();
    }

    private void RemoveInstanceButton_OnClick(object sender, RoutedEventArgs e)
    {
        FrameworkElement element = (FrameworkElement)sender;
        AvatarChangerTrigger triggerInstance = (AvatarChangerTrigger)element.Tag;

        MessageBoxResult result = MessageBox.Show("Warning. This will remove the \"" + triggerInstance.Name.Value + "\" trigger data. Are you sure?", "Delete avatar trigger?", MessageBoxButton.YesNo);
        if (result != MessageBoxResult.Yes) return;

        moduleSetting.Remove(triggerInstance);
    }

    private void EditInstanceButton_OnClick(object sender, RoutedEventArgs e)
    {
        FrameworkElement element = (FrameworkElement)sender;
        AvatarChangerTrigger triggerInstance = (AvatarChangerTrigger)element.Tag;

        windowManager.TrySpawnChild(new AvatarChangerTriggerInstanceEditWindow(triggerInstance.Name, triggerInstance.AvatarId, triggerInstance.TriggerParams));
    }
}
