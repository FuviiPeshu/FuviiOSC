using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using FuviiOSC.Common;
using VRCOSC.App.Utils;

namespace FuviiOSC.SqueakMeter;

public record AudioDeviceInfo(string ID, string FriendlyName, bool IsEnabled = true);

public class AudioDeviceNotificationClient : IMMNotificationClient
{
    public event Action? DevicesChanged;
    public event Action<string, string>? DeviceError;

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => DevicesChanged?.Invoke();
    public void OnDeviceAdded(string deviceId) => DevicesChanged?.Invoke();
    public void OnDeviceRemoved(string deviceId) => DevicesChanged?.Invoke();
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => DevicesChanged?.Invoke();
    public void OnPropertyValueChanged(string deviceId, PropertyKey key) => DevicesChanged?.Invoke();
    public void OnSelectedDeviceError(string deviceId, string errorMessage) => DeviceError?.Invoke(deviceId, errorMessage);
}

public partial class AudioDeviceModuleRuntimeView : INotifyPropertyChanged
{
    public SqueakMeterModule Module { get; }
    public ObservableCollection<AudioDeviceInfo> AudioDevices { get; } = new();
    public Observable<string> SelectedDeviceId { get; set; } = new();

    private bool suppressSelectionChanged = false;
    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
            }
        }
    }

    public ObservableCollection<AudioDeviceInfo> DisabledDevices { get; } = new();

    private const int ChartIntervalMs = 50;
    private const double SamplesPerSecond = 1000.0 / ChartIntervalMs;
    private const int DefaultHistorySeconds = 5;
    private const int MaxHistorySeconds = 60;
    private const int DefaultHistorySize = (int)(DefaultHistorySeconds * SamplesPerSecond);
    private const int MaxHistorySize = (int)(MaxHistorySeconds * SamplesPerSecond) + 1;
    private int _historySize = DefaultHistorySize;
    private float[] _volumeHistory = new float[MaxHistorySize];
    private float[] _bassHistory = new float[MaxHistorySize];
    private float[] _midHistory = new float[MaxHistorySize];
    private float[] _trebleHistory = new float[MaxHistorySize];
    private float[] _directionHistory = new float[MaxHistorySize];
    private int _historyIndex;
    private int _historyFilled;
    private readonly DispatcherTimer _chartTimer;
    // Smoothed values for EQ bars (to make them less jittery)
    private float _softBass;
    private float _softMid;
    private float _softTreble;
    private float _softVol;
    // Session peak value tracking
    private float _peakVol;
    private float _peakBass;
    private float _peakMid;
    private float _peakTreble;
    // Toggle state (true = Frequencies, false = History)
    private bool _showFrequencies = true;

    public AudioDeviceModuleRuntimeView(SqueakMeterModule module)
    {
        InitializeComponent();
        DataContext = this;
        Module = module;
        Module.disabledDeviceList.ForEach(device => DisabledDevices.Add(device));

        if (Module.notificationClient == null)
            Module.notificationClient = new AudioDeviceNotificationClient();

        Module.notificationClient.DevicesChanged += UpdateDeviceList;
        Module.notificationClient.DeviceError += DeviceErrorOccurred;
        UpdateDeviceList();

        _chartTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ChartIntervalMs) };
        _chartTimer.Tick += (_, _) => UpdateCharts();
        _chartTimer.Start();
    }

    public void DeviceErrorOccurred(string deviceId, string errorMessage)
    {
        AudioDeviceInfo? device = AudioDevices.First(d => d.ID == deviceId);
        if (device != null && !DisabledDevices.Any(device => device.ID == deviceId))
        {
            int index = AudioDevices.IndexOf(device);
            AudioDevices[index] = device with { IsEnabled = false };
            DisabledDevices.Add(AudioDevices[index]);
            Module.disabledDeviceList.Add(AudioDevices[index]);
        }
        Module.selectedDevice = null;
        SelectedDeviceId.Value = String.Empty;
        ErrorMessage = errorMessage;
    }

    public void RemoveDisabledDevice(object sender, RoutedEventArgs e)
    {
        FrameworkElement element = (FrameworkElement)sender;
        AudioDeviceInfo device = (AudioDeviceInfo)element.Tag;

        if (device != null)
        {
            int index = AudioDevices.IndexOf(device);
            AudioDevices[index] = device with { IsEnabled = true };
            DisabledDevices.Remove(device);
            Module.disabledDeviceList.RemoveIf(el => el.ID == device.ID);
        }
    }

    public void UpdateDeviceList()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(UpdateDeviceList));
            return;
        }

        try
        {
            ErrorMessage = null;

            MMDeviceCollection audioDevices = Module.GetAudioOutputDevices();
            if (audioDevices.Count == 0)
            {
                Module.LogDebug("No audio devices found.");
                ErrorMessage = "No audio devices found.";
                return;
            }

            // Save the current selection
            string previousSelectedId = Module.selectedDevice?.ID ?? SelectedDeviceId.Value;

            // Update UI with the new list of devices
            suppressSelectionChanged = true;
            SelectedDeviceId.Value = String.Empty;
            AudioDevices.Clear();
            foreach (MMDevice device in audioDevices)
            {
                bool isEnabled = !DisabledDevices.Any(d => d.ID == device.ID);
                AudioDevices.Add(new AudioDeviceInfo(device.ID, device.FriendlyName, isEnabled));
            }

            // Reset audio device to the first device if selected audio device is no longer available
            if (Module.activeDevice == null || !audioDevices.Any(d => d.ID == previousSelectedId))
            {
                Module.SetCaptureDevice(audioDevices[0].ID);
            }
            else
            {
                SelectedDeviceId.Value = previousSelectedId;
            }
            suppressSelectionChanged = false;
        }
        catch (Exception error)
        {
            suppressSelectionChanged = false;
            ErrorMessage = $"Error updating audio devices: {error.Message}";
            Module.LogDebug($"Error updating audio devices: {error.Message}");
        }
    }

    private void DeviceSelection_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionChanged)
            return;

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => DeviceSelection_OnSelectionChanged(sender, e)));
            return;
        }

        ErrorMessage = null;
        if (sender is not ComboBox comboBox)
        {
            ErrorMessage = "Device selection sender is not a ComboBox.";
            return;
        }

        if (comboBox.SelectedItem is AudioDeviceInfo selectedItem)
        {
            SelectedDeviceId.Value = selectedItem.ID;
            Module.selectedDevice = selectedItem;
            Module.SetCaptureDevice(selectedItem.ID);
            Module.LogDebug($"Selected audio device: {selectedItem.FriendlyName} ({selectedItem.ID})");
        }
        else
        {
            SelectedDeviceId.Value = string.Empty;
            Module.selectedDevice = null;
            ErrorMessage = "No audio device selected.";
            Module.LogDebug("No audio device selected.");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void UpdateCharts()
    {
        try
        {
            float vol = Module.CurrentVolume;
            float bass = Module.CurrentBass;
            float mid = Module.CurrentMid;
            float treble = Module.CurrentTreble;
            float dir = Module.CurrentDirection;

            _volumeHistory[_historyIndex] = vol;
            _bassHistory[_historyIndex] = bass;
            _midHistory[_historyIndex] = mid;
            _trebleHistory[_historyIndex] = treble;
            _directionHistory[_historyIndex] = dir;
            _historyIndex = (_historyIndex + 1) % _historySize;
            if (_historyFilled < _historySize)
            {
                _historyFilled++;
            }

            const float softenValues = 0.5f; // additional smoothing to make the charts less jittery
            _softBass = _softBass + (Math.Clamp(bass, 0f, 1f) - _softBass) * softenValues;
            _softMid = _softMid + (Math.Clamp(mid, 0f, 1f) - _softMid) * softenValues;
            _softTreble = _softTreble + (Math.Clamp(treble, 0f, 1f) - _softTreble) * softenValues;
            _softVol = _softVol + (Math.Clamp(vol, 0f, 1f) - _softVol) * softenValues;

            if (_softVol > _peakVol) _peakVol = _softVol;
            if (_softBass > _peakBass) _peakBass = _softBass;
            if (_softMid > _peakMid) _peakMid = _softMid;
            if (_softTreble > _peakTreble) _peakTreble = _softTreble;

            UpdateEqBars();
            DrawDirectionDot();

            VolumeValue.Text = vol.ToString("F2");
            BassValue.Text = bass.ToString("F2");
            MidValue.Text = mid.ToString("F2");
            TrebleValue.Text = treble.ToString("F2");
            DirectionValue.Text = dir.ToString("F2");

            DrawSparkline(VolumeChart, _volumeHistory, _historyIndex, _historySize, FuviiStyles.GoldBrush);
            DrawSparkline(BassChart, _bassHistory, _historyIndex, _historySize, FuviiStyles.PurpleBrush);
            DrawSparkline(MidChart, _midHistory, _historyIndex, _historySize, FuviiStyles.CyanBrush);
            DrawSparkline(TrebleChart, _trebleHistory, _historyIndex, _historySize, FuviiStyles.GreenBrush);
            DrawSparkline(DirectionChart, _directionHistory, _historyIndex, _historySize, FuviiStyles.OrangeBrush, centerLine: true);
        }
        catch (Exception error)
        {
            Module.LogDebug($"Error updating charts: {error.Message}");
        }
    }

    private void UpdateEqBars()
    {
        double maxH = EqBarGrid.ActualHeight;
        if (maxH < 1) return;

        EqVolBar.Height = _softVol * maxH;
        EqBassBar.Height = _softBass * maxH;
        EqMidBar.Height = _softMid * maxH;
        EqTrebleBar.Height = _softTreble * maxH;

        EqVolPeak.Margin = new Thickness(0, 0, 0, _peakVol * maxH);
        EqBassPeak.Margin = new Thickness(0, 0, 0, _peakBass * maxH);
        EqMidPeak.Margin = new Thickness(0, 0, 0, _peakMid * maxH);
        EqTreblePeak.Margin = new Thickness(0, 0, 0, _peakTreble * maxH);
    }

    private void DrawDirectionDot()
    {
        DirectionDotCanvas.Children.Clear();
        double w = DirectionDotCanvas.ActualWidth;
        double h = DirectionDotCanvas.ActualHeight;
        if (w < 4 || h < 4) return;

        Line? track = new Line
        {
            X1 = 0,
            X2 = w,
            Y1 = h / 2,
            Y2 = h / 2,
            Stroke = FuviiStyles.GridLineBrush,
            StrokeThickness = 1
        };
        DirectionDotCanvas.Children.Add(track);

        Line? center = new Line
        {
            X1 = w / 2,
            X2 = w / 2,
            Y1 = 2,
            Y2 = h - 2,
            Stroke = FuviiStyles.WhiteSubtleBrush,
            StrokeThickness = 1
        };
        DirectionDotCanvas.Children.Add(center);

        float dir = Math.Clamp(Module.CurrentDirection, 0f, 1f);
        double dotX = dir * w;
        const double dotR = 5;
        Ellipse? dot = new Ellipse { Width = dotR * 2, Height = dotR * 2, Fill = FuviiStyles.OrangeBrush };
        Canvas.SetLeft(dot, dotX - dotR);
        Canvas.SetTop(dot, h / 2 - dotR);
        DirectionDotCanvas.Children.Add(dot);
    }

    private void Toggle_Frequencies(object sender, MouseButtonEventArgs e)
    {
        if (_showFrequencies) return;
        _showFrequencies = true;

        FrequenciesPanel.Visibility = Visibility.Visible;
        HistoryPanel.Visibility = Visibility.Collapsed;
        ResetPeakBtn.Visibility = Visibility.Visible;
        HistorySelector.Visibility = Visibility.Collapsed;

        ToggleFreqLabel.Foreground = Brushes.White;
        ToggleFreqLabel.FontWeight = FontWeights.SemiBold;
        ToggleHistLabel.Foreground = FuviiStyles.WhiteSoftBrush;
        ToggleHistLabel.FontWeight = FontWeights.Normal;

        DoubleAnimation? anim = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        ToggleTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void Toggle_History(object sender, MouseButtonEventArgs e)
    {
        if (!_showFrequencies) return;
        _showFrequencies = false;

        FrequenciesPanel.Visibility = Visibility.Collapsed;
        HistoryPanel.Visibility = Visibility.Visible;
        ResetPeakBtn.Visibility = Visibility.Collapsed;
        HistorySelector.Visibility = Visibility.Visible;

        ToggleFreqLabel.Foreground = FuviiStyles.WhiteSoftBrush;
        ToggleFreqLabel.FontWeight = FontWeights.Normal;
        ToggleHistLabel.Foreground = Brushes.White;
        ToggleHistLabel.FontWeight = FontWeights.SemiBold;

        double halfWidth = ToggleGrid.ActualWidth / 2;
        DoubleAnimation? anim = new DoubleAnimation
        {
            To = halfWidth,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        ToggleTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void ResetPeak_Click(object sender, RoutedEventArgs e)
    {
        _peakVol = 0;
        _peakBass = 0;
        _peakMid = 0;
        _peakTreble = 0;
    }

    private void HistorySelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not ComboBoxItem item)
            return;

        string? tag = item.Content?.ToString();
        int seconds = tag switch
        {
            "2s" => 2,
            "5s" => 5,
            "10s" => 10,
            "30s" => 30,
            "60s" => 60,
            _ => 5
        };

        int newSize = Math.Min((int)(seconds * SamplesPerSecond), MaxHistorySize);
        if (newSize == _historySize) return;

        _historySize = newSize;
        _historyIndex = 0;
        _historyFilled = 0;
        Array.Clear(_volumeHistory);
        Array.Clear(_bassHistory);
        Array.Clear(_midHistory);
        Array.Clear(_trebleHistory);
        Array.Clear(_directionHistory);
    }

    private void DrawSparkline(Canvas canvas, float[] history, int currentIndex, int historySize, Brush stroke, bool centerLine = false)
    {
        canvas.Children.Clear();
        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w < 2 || h < 2 || historySize < 2) return;

        double baseline = centerLine ? h / 2 : h;

        if (centerLine)
        {
            Line line = new Line
            {
                X1 = 0,
                X2 = w,
                Y1 = h / 2,
                Y2 = h / 2,
                Stroke = FuviiStyles.GridLineBrush,
                StrokeThickness = 1
            };
            canvas.Children.Add(line);
        }

        Polyline polyline = new Polyline
        {
            Stroke = stroke,
            StrokeThickness = 1.2,
            StrokeLineJoin = PenLineJoin.Round
        };
        // Always draw full historySize points across the full width.
        int filled = _historyFilled;
        int startSlot = historySize - filled;
        int dataStart = (currentIndex - filled + historySize) % historySize;
        double stepX = w / (historySize - 1);

        for (int i = 0; i < historySize; i++)
        {
            double x = i * stepX;
            double y;
            if (i < startSlot)
            {
                y = baseline;
            }
            else
            {
                int idx = (dataStart + (i - startSlot)) % historySize;
                float val = Math.Clamp(history[idx], 0f, 1f);
                y = h - (val * h);
            }
            polyline.Points.Add(new Point(x, y));
        }

        canvas.Children.Add(polyline);
    }
}
