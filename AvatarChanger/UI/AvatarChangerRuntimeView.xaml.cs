using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FuviiOSC.Common;

namespace FuviiOSC.AvatarChanger.UI;

public partial class AvatarChangerRuntimeView
{
    private const double SLIDER_POSITION_MINIMUM = 0d;
    private const double SLIDER_POSITION_MAXIMUM = 100d;
    private readonly AvatarChangerModule _module;
    private const int CLICK_FEEDBACK_MS = 1024;
    private readonly DispatcherTimer _heightTimer;
    private bool _updatingHeightSlider;

    public AvatarChangerRuntimeView(AvatarChangerModule module)
    {
        _module = module;
        DataContext = this;
        InitializeComponent();

        _module.TriggersChanged += () => Dispatcher.BeginInvoke(RefreshButtons);
        _module.HeightPresetsChanged += () => Dispatcher.BeginInvoke(RefreshHeightPresets);

        _heightTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _heightTimer.Tick += (_, _) => RefreshHeightControls();

        Loaded += (_, _) =>
        {
            RefreshButtons();
            RefreshHeightPresets();
            RefreshHeightControls();
            _heightTimer.Start();
        };
        Unloaded += (_, _) => _heightTimer.Stop();
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
        StackPanel panel = new()
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        BitmapImage? icon = AvatarIconLoader.GetIcon(trigger.IconKey.Value);
        if (icon != null)
        {
            panel.Children.Add(new Image
            {
                Source = icon,
                Width = 36,
                Height = 36,
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = trigger.Name.Value,
            Foreground = FuviiStyles.WhiteSoftBrush,
            FontSize = 14,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        });

        return panel;
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

    private void RefreshHeightControls()
    {
        bool isAvailable = _module.IsAvatarHeightControlAvailable;
        float minimum = _module.AvatarSliderMinimum;
        float maximum = _module.AvatarSliderMaximum;
        float current = _module.CurrentAvatarEyeHeight;
        bool useLogarithmicScale = _module.UseLogarithmicHeightSlider;

        _updatingHeightSlider = true;
        HeightSlider.Minimum = useLogarithmicScale ? SLIDER_POSITION_MINIMUM : minimum;
        HeightSlider.Maximum = useLogarithmicScale ? SLIDER_POSITION_MAXIMUM : maximum;
        HeightSlider.IsSnapToTickEnabled = false;
        HeightSlider.Value = useLogarithmicScale
            ? HeightToSliderPosition(current, minimum, maximum)
            : Math.Clamp(current, minimum, maximum);
        LogarithmicMidpointOverlay.Visibility = useLogarithmicScale ? Visibility.Visible : Visibility.Collapsed;
        LogarithmicMidpointLabel.Text = useLogarithmicScale
            ? FormatHeight(CalculateLogarithmicMidpoint(minimum, maximum))
            : string.Empty;
        HeightSlider.IsEnabled = isAvailable;
        HeightPresetPanel.IsEnabled = isAvailable;
        CurrentHeightLabel.Text = isAvailable ? FormatHeight(current) : "--";
        HeightRangeLabel.Text = $"{FormatHeight(minimum)} - {FormatHeight(maximum)}";
        _updatingHeightSlider = false;
    }

    private void RefreshHeightPresets()
    {
        HeightPresetItems.Children.Clear();

        foreach (float preset in _module.AvatarHeightPresets)
        {
            Border button = new()
            {
                Tag = preset,
                ToolTip = $"Set avatar height to {FormatHeight(preset)}",
                Style = (Style)FindResource("HeightPresetButton"),
                Child = new TextBlock
                {
                    Text = FormatHeight(preset),
                    Foreground = FuviiStyles.WhiteSoftBrush,
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            button.MouseLeftButtonUp += HeightPreset_OnClick;
            HeightPresetItems.Children.Add(button);
        }
    }

    private void HeightSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingHeightSlider || !IsLoaded) return;

        float minimum = _module.AvatarSliderMinimum;
        float maximum = _module.AvatarSliderMaximum;
        float requestedHeight = _module.UseLogarithmicHeightSlider
            ? SliderPositionToHeight(e.NewValue, minimum, maximum)
            : (float)e.NewValue;

        _module.TrySetAvatarEyeHeight(requestedHeight);
    }

    private static double HeightToSliderPosition(float height, float minimum, float maximum)
    {
        if (maximum <= minimum) return SLIDER_POSITION_MINIMUM;

        double logarithmicMinimum = Math.Log(minimum);
        double logarithmicMaximum = Math.Log(maximum);
        double logarithmicHeight = Math.Log(Math.Clamp(height, minimum, maximum));

        return Math.Clamp(
            (logarithmicHeight - logarithmicMinimum) /
            (logarithmicMaximum - logarithmicMinimum) * SLIDER_POSITION_MAXIMUM,
            SLIDER_POSITION_MINIMUM,
            SLIDER_POSITION_MAXIMUM);
    }

    private static float SliderPositionToHeight(double position, float minimum, float maximum)
    {
        if (maximum <= minimum) return minimum;

        double normalizedPosition = Math.Clamp(position, SLIDER_POSITION_MINIMUM, SLIDER_POSITION_MAXIMUM) /
                         SLIDER_POSITION_MAXIMUM;
        double height = Math.Exp(Math.Log(minimum) + normalizedPosition * (Math.Log(maximum) - Math.Log(minimum)));

        return (float)height;
    }

    private static float CalculateLogarithmicMidpoint(float minimum, float maximum)
    {
        return (float)Math.Sqrt(minimum * maximum);
    }

    private void HeightPreset_OnClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement button) return;

        float height = button.Tag is float presetHeight
            ? presetHeight
            : button.Tag?.ToString() switch
            {
                "min" => _module.AvatarEyeHeightMin,
                "max" => _module.AvatarEyeHeightMax,
                _ => -1f,
            };

        if (height < 0f) return;

        _module.TrySetAvatarEyeHeight(height);
        RefreshHeightControls();
        e.Handled = true;
    }

    private static string FormatHeight(float height)
    {
        return $"{height.ToString("0.##", CultureInfo.InvariantCulture)}m";
    }
}
