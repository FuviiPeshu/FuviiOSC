using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FuviiOSC.Common;

public partial class FuviiHeader
{
    public FuviiHeader()
    {
        InitializeComponent();
        Margin = new Thickness(-13, -42, -13, -13);
        VerticalAlignment = VerticalAlignment.Stretch;
        Grid.SetColumnSpan(this, 99);
        Panel.SetZIndex(this, -1);

        SizeChanged += OnSizeChanged;
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
