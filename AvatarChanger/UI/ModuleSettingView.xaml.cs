using System;
using System.Windows;
using VRCOSC.App.Utils;
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

        windowManager.TrySpawnChild(new AvatarChangerTriggerInstanceEditWindow(triggerInstance.Name, triggerInstance.AvatarId, triggerInstance.IconKey, triggerInstance.ScaleMode, triggerInstance.FixedEyeHeight, triggerInstance.TriggerParams));
    }

    private void DuplicateInstanceButton_OnClick(object sender, RoutedEventArgs e)
    {
        FrameworkElement element = (FrameworkElement)sender;
        AvatarChangerTrigger triggerInstance = (AvatarChangerTrigger)element.Tag;

        AvatarChangerTrigger duplicate = new()
        {
            ID = Guid.NewGuid().ToString(),
            Name = new Observable<string>($"{triggerInstance.Name.Value} Copy"),
            AvatarId = new Observable<string>(triggerInstance.AvatarId.Value),
            IconKey = new Observable<string>(triggerInstance.IconKey.Value),
            ScaleMode = new Observable<int>(triggerInstance.ScaleMode.Value),
            FixedEyeHeight = new Observable<float>(triggerInstance.FixedEyeHeight.Value),
        };

        foreach (TriggerQueryableParameter sourceParameter in triggerInstance.TriggerParams)
        {
            TriggerQueryableParameter duplicateParameter = new();
            duplicateParameter.Name.Value = sourceParameter.Name.Value;
            duplicateParameter.Type.Value = sourceParameter.Type.Value;
            duplicateParameter.Comparison.Value = sourceParameter.Comparison.Value;
            duplicateParameter.BoolValue.Value = sourceParameter.BoolValue.Value;
            duplicateParameter.IntValue.Value = sourceParameter.IntValue.Value;
            duplicateParameter.FloatValue.Value = sourceParameter.FloatValue.Value;
            duplicate.TriggerParams.Add(duplicateParameter);
        }

        moduleSetting.Attribute.Add(duplicate);
    }
}
