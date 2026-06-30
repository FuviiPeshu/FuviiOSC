using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using VRCOSC.App.UI.Core;
using VRCOSC.App.Utils;

namespace FuviiOSC.AvatarChanger.UI;

public partial class AvatarChangerTriggerInstanceEditWindow : IManagedWindow, INotifyPropertyChanged
{
    public ObservableCollection<TriggerQueryableParameter> QueryableParameters { get; }
    public Observable<string> TriggerName { get; }
    public Observable<string> AvatarId { get; }
    public Observable<int> ScaleMode { get; }
    public Observable<float> FixedEyeHeight { get; }

    public Visibility FixedHeightVisible => ScaleMode.Value == (int)AvatarScaleMode.FixedHeight
        ? Visibility.Visible
        : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AvatarChangerTriggerInstanceEditWindow(
        Observable<string> triggerName,
        Observable<string> avatarId,
        Observable<int> scaleMode,
        Observable<float> fixedEyeHeight,
        ObservableCollection<TriggerQueryableParameter> triggers)
    {
        InitializeComponent();

        TriggerName = triggerName;
        AvatarId = avatarId;
        ScaleMode = scaleMode;
        FixedEyeHeight = fixedEyeHeight;
        QueryableParameters = triggers;

        DataContext = this;
    }

    private void ScaleModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FixedHeightVisible)));
    }

    private void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
        QueryableParameters.Add(new TriggerQueryableParameter());
    }

    private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
        FrameworkElement element = (FrameworkElement)sender;
        TriggerQueryableParameter instance = (TriggerQueryableParameter)element.Tag;

        QueryableParameters.Remove(instance);
    }

    public object GetComparer() => QueryableParameters;
}
