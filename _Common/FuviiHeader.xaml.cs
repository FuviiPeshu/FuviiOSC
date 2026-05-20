using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FuviiOSC.Common;

public partial class FuviiHeader
{
    private FrameworkElement? _hoverTarget;

    public FuviiHeader()
    {
        InitializeComponent();
        Margin = new Thickness(-13, -42, -13, -13);
        VerticalAlignment = VerticalAlignment.Stretch;
        Grid.SetColumnSpan(this, 99);
        Panel.SetZIndex(this, -1);

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hoverTarget = FindParentCard();
        if (_hoverTarget != null)
        {
            _hoverTarget.MouseEnter += (_, _) => AnimateLogo(1.0);
            _hoverTarget.MouseLeave += (_, _) => AnimateLogo(0.0);
        }
    }

    private void AnimateLogo(double targetOpacity)
    {
        DoubleAnimation anim = new()
        {
            To = targetOpacity,
            Duration = TimeSpan.FromMilliseconds(256),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        LogoImage.BeginAnimation(OpacityProperty, anim);
    }

    private Border? FindParentCard()
    {
        // Structure: Border (Card) > SpacedStackPanel > ContentControl > UserControl > ... > FuviiHeader
        DependencyObject? current = this;
        for (int i = 0; i < 16 && current != null; i++)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current?.GetType().Name == "SpacedStackPanel")
                return VisualTreeHelper.GetParent(current) as Border;
        }
        return null;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        WatermarkImage.Width = h;
        WatermarkImage.Height = h;
        Canvas.SetLeft(WatermarkImage, w - h);
        Canvas.SetTop(WatermarkImage, 0);
    }
}
