using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FuviiOSC.Common;

namespace FuviiOSC.AvatarChanger.UI;

public partial class AvatarChangerRuntimeView
{
    private readonly AvatarChangerModule _module;
    private const int CLICK_FEEDBACK_MS = 1024;

    public AvatarChangerRuntimeView(AvatarChangerModule module)
    {
        _module = module;
        DataContext = this;
        InitializeComponent();

        _module.TriggersChanged += () => Dispatcher.BeginInvoke(RefreshButtons);

        Loaded += (_, _) => RefreshButtons();
    }

    private void RefreshButtons()
    {
        List<AvatarChangerTrigger> triggers;
        try
        {
            triggers = _module.GetTriggers();
        }
        catch
        {
            _module.LogDebug("Failed to get avatar changer triggers. Please check your settings.");
            return;
        }

        if (triggers.Count == 0)
        {
            EmptyLabel.Visibility = Visibility.Visible;
            TriggerPanel.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyLabel.Visibility = Visibility.Collapsed;
        TriggerPanel.Visibility = Visibility.Visible;

        TriggerPanel.Items.Clear();

        foreach (AvatarChangerTrigger trigger in triggers)
        {
            Border border = new()
            {
                Tag = trigger,
                MinWidth = 100,
                MinHeight = 48,
                Margin = new Thickness(4),
                Padding = new Thickness(12, 8, 12, 8),
                Background = FuviiStyles.DarkBgBrush,
                BorderBrush = FuviiStyles.PurpleBrush,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Child = CreateButtonContent(trigger),
            };
            border.MouseEnter += (_, _) => border.Background = FuviiStyles.PurpleFillBrush;
            border.MouseLeave += (_, _) => border.Background = FuviiStyles.DarkBgBrush;
            border.MouseLeftButtonUp += OnTriggerClick;
            TriggerPanel.Items.Add(border);
        }
    }

    private static UIElement CreateButtonContent(AvatarChangerTrigger trigger)
    {
        TextBlock label = new()
        {
            Text = trigger.Name.Value,
            Foreground = FuviiStyles.WhiteSoftBrush,
            FontSize = 14,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        return label;
    }

    private void OnTriggerClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.Tag is not AvatarChangerTrigger trigger) return;

        _module.TriggerAvatarChange(trigger);

        border.BorderBrush = FuviiStyles.GoldBrush;
        DispatcherTimer feedbackReset = new() { Interval = System.TimeSpan.FromMilliseconds(CLICK_FEEDBACK_MS) };
        feedbackReset.Tick += (_, _) =>
        {
            border.BorderBrush = FuviiStyles.PurpleBrush;
            feedbackReset.Stop();
        };
        feedbackReset.Start();
    }
}
