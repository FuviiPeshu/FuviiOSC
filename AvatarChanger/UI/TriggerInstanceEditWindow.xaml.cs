using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FuviiOSC.Common;
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
    public Observable<string> IconKey { get; }

    public Visibility FixedHeightVisible => ScaleMode.Value == (int)AvatarScaleMode.FixedHeight
        ? Visibility.Visible
        : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AvatarChangerTriggerInstanceEditWindow(
        Observable<string> triggerName,
        Observable<string> avatarId,
        Observable<string> iconKey,
        Observable<int> scaleMode,
        Observable<float> fixedEyeHeight,
        ObservableCollection<TriggerQueryableParameter> triggers)
    {
        InitializeComponent();

        TriggerName = triggerName;
        AvatarId = avatarId;
        IconKey = iconKey;
        ScaleMode = scaleMode;
        FixedEyeHeight = fixedEyeHeight;
        QueryableParameters = triggers;

        DataContext = this;

        UpdateIconPreview();
    }

    private void UpdateIconPreview()
    {
        BitmapImage? icon = AvatarIconLoader.GetIcon(IconKey.Value);
        SelectedIconPreview.Source = icon;
        IconKeyLabel.Text = string.IsNullOrEmpty(IconKey.Value) ? "(none)" : IconKey.Value;
    }

    private void ChooseIcon_OnClick(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> keys = AvatarIconLoader.GetAvailableKeys();

        Popup popup = new()
        {
            StaysOpen = false,
            PlacementTarget = (UIElement)sender,
            Placement = PlacementMode.Bottom,
            AllowsTransparency = true,
        };

        Border popupBorder = new()
        {
            Background = FuviiStyles.DarkBgBrush,
            BorderBrush = FuviiStyles.PurpleBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            MaxWidth = 400,
        };

        StackPanel container = new() { Orientation = Orientation.Vertical };
        WrapPanel grid = new() { Orientation = Orientation.Horizontal };

        foreach (string key in keys)
        {
            BitmapImage? icon = AvatarIconLoader.GetIcon(key);
            if (icon == null) continue;

            Border cell = new()
            {
                Width = 52,
                Height = 52,
                Margin = new Thickness(2),
                Background = Brushes.Transparent,
                BorderBrush = IconKey.Value == key ? FuviiStyles.GoldBrush : FuviiStyles.InactiveBrush,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = key,
                Child = new Image { Source = icon, Width = 40, Height = 40, Margin = new Thickness(4) },
            };
            cell.MouseLeftButtonUp += (_, _) =>
            {
                IconKey.Value = key;
                UpdateIconPreview();
                popup.IsOpen = false;
            };
            cell.MouseEnter += (_, _) => cell.BorderBrush = FuviiStyles.PurpleBrush;
            cell.MouseLeave += (_, _) => cell.BorderBrush = IconKey.Value == key ? FuviiStyles.GoldBrush : FuviiStyles.InactiveBrush;
            grid.Children.Add(cell);
        }

        container.Children.Add(grid);

        Button importBtn = new()
        {
            Content = keys.Count == 0 ? "Import image..." : "+ Import image...",
            Margin = new Thickness(2, 6, 2, 2),
            Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            Foreground = FuviiStyles.WhiteSoftBrush,
            BorderBrush = FuviiStyles.InactiveBrush,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
        };
        importBtn.Click += (_, _) =>
        {
            popup.IsOpen = false;
            ImportIconFromFile();
        };
        container.Children.Add(importBtn);

        popupBorder.Child = container;
        popup.Child = popupBorder;
        popup.IsOpen = true;
    }

    private void ImportIconFromFile()
    {
        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            Title = "Select avatar icon",
            Filter = "PNG images (*.png)|*.png|All images (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp",
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            string? importedKey = AvatarIconLoader.ImportIcon(dialog.FileName);
            if (importedKey != null)
            {
                IconKey.Value = importedKey;
                UpdateIconPreview();
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Failed to import icon: {ex.Message}", "Import Error", MessageBoxButton.OK);
        }
    }

    private void ClearIcon_OnClick(object sender, RoutedEventArgs e)
    {
        IconKey.Value = string.Empty;
        UpdateIconPreview();
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
