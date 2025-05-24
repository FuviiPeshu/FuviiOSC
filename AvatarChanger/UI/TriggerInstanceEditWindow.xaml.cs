using System.Collections.ObjectModel;
using System.Windows;
using VRCOSC.App.UI.Core;
using VRCOSC.App.Utils;

namespace FuviiOSC.AvatarChanger.UI;

public partial class AvatarChangerTriggerInstanceEditWindow : IManagedWindow
{
    public ObservableCollection<TriggerQueryableParameter> QueryableParameters { get; }
    public Observable<string> TriggerName { get; }
    public Observable<string> AvatarId { get; }

    public AvatarChangerTriggerInstanceEditWindow(Observable<string> triggerName, Observable<string> avatarId, ObservableCollection<TriggerQueryableParameter> triggers)
    {
        InitializeComponent();

        TriggerName = triggerName;
        AvatarId = avatarId;
        QueryableParameters = triggers;

        DataContext = this;
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
