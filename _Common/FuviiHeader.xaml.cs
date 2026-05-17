using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FuviiOSC.Common;

public partial class FuviiHeader
{
    private const double CornerRadius = 5;
    private const double NotchDepth = 14;
    private const double NotchPadding = 14;
    private const double DiagonalWidth = 14;
    private const double DefaultNotchHalfWidth = 60;
    private const double ShapeHeight = 42;

    private TextBlock? _titleTextBlock;

    public FuviiHeader()
    {
        InitializeComponent();
        Margin = new Thickness(-13, -42, -13, 0);
        VerticalAlignment = VerticalAlignment.Stretch;
        Grid.SetColumnSpan(this, 99);
        Panel.SetZIndex(this, -1);

        BackgroundPath.Fill = CreateHeaderGradient();

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private static LinearGradientBrush CreateHeaderGradient()
    {
        Color top = FuviiStyles.Purple;
        top.A = 0x50;
        Color bottom = FuviiStyles.Purple;
        bottom.A = 0x00;

        LinearGradientBrush brush = new()
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = { new GradientStop(top, 0), new GradientStop(bottom, 1) }
        };
        brush.Freeze();
        return brush;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _titleTextBlock = FindTitleTextBlock();
        RebuildGeometry();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RebuildGeometry();
    }

    private void RebuildGeometry()
    {
        double w = ActualWidth;
        if (w <= 0) return;

        double h = ShapeHeight;
        double notchHalfWidth = DefaultNotchHalfWidth;
        if (_titleTextBlock is { ActualWidth: > 0 })
            notchHalfWidth = _titleTextBlock.ActualWidth / 2 + NotchPadding;

        double centerX = w / 2;
        double sideBottom = h - NotchDepth;
        double notchLeft = centerX - notchHalfWidth - DiagonalWidth;
        double notchRight = centerX + notchHalfWidth + DiagonalWidth;

        // Clamp to avoid overlapping sides
        notchLeft = Math.Max(CornerRadius + 10, notchLeft);
        notchRight = Math.Min(w - CornerRadius - 10, notchRight);

        PathFigure fig = new() { StartPoint = new Point(CornerRadius, 0), IsClosed = true, IsFilled = true };
        fig.Segments.Add(new LineSegment(new Point(w - CornerRadius, 0), false));
        fig.Segments.Add(new ArcSegment(new Point(w, CornerRadius), new Size(CornerRadius, CornerRadius), 0, false, SweepDirection.Clockwise, false));
        fig.Segments.Add(new LineSegment(new Point(w, sideBottom), false));
        fig.Segments.Add(new LineSegment(new Point(notchRight, h), false));
        fig.Segments.Add(new LineSegment(new Point(notchLeft, h), false));
        fig.Segments.Add(new LineSegment(new Point(0, sideBottom), false));
        fig.Segments.Add(new LineSegment(new Point(0, CornerRadius), false));
        fig.Segments.Add(new ArcSegment(new Point(CornerRadius, 0), new Size(CornerRadius, CornerRadius), 0, false, SweepDirection.Clockwise, false));

        PathGeometry geometry = new();
        geometry.Figures.Add(fig);
        geometry.Freeze();

        BackgroundPath.Data = geometry;

        double containerH = ActualHeight;
        if (containerH > 0)
        {
            WatermarkImage.Width = containerH;
            WatermarkImage.Height = containerH;
            Canvas.SetLeft(WatermarkImage, w - containerH);
            Canvas.SetTop(WatermarkImage, 0);
        }
    }

    private TextBlock? FindTitleTextBlock()
    {
        DependencyObject? ancestor = this;
        for (int i = 0; i < 25 && ancestor != null; i++)
            ancestor = VisualTreeHelper.GetParent(ancestor);

        return ancestor != null ? FindTextBlockInTree(ancestor) : null;
    }

    private static TextBlock? FindTextBlockInTree(DependencyObject root)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock tb && !string.IsNullOrEmpty(tb.Text) && tb.Text.Length < 30)
            {
                if (tb.HorizontalAlignment == HorizontalAlignment.Center ||
                    tb.FontSize >= 14 ||
                    tb.FontWeight == FontWeights.Bold ||
                    tb.FontWeight == FontWeights.SemiBold)
                    return tb;
            }

            TextBlock? found = FindTextBlockInTree(child);
            if (found != null) return found;
        }
        return null;
    }
}
