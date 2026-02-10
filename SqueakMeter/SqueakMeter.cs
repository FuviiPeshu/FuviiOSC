using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace FuviiOSC.SqueakMeter;

[ModuleTitle("Squeak Meter")]
[ModuleDescription("Listens to the selected audio output device and provides OSC parameters for volume, frequencies and direction. To select audio device go to the 'Run' tab")]
[ModuleType(ModuleType.Generic)]
public class SqueakMeterModule : Module
{
    // State
    private bool _enabled = true;
    private bool _shouldUpdate;
    private int _bytesPerSample;

    // Raw values
    private float _bass;
    private float _mid;
    private float _treble;
    private float _leftEarVolume;
    private float _rightEarVolume;

    // Smoothed values
    private float _bassSmoothed;
    private float _bassSmoothedPrevious;
    private float _midSmoothed;
    private float _midSmoothedPrevious;
    private float _trebleSmoothed;
    private float _trebleSmoothedPrevious;
    private float _leftEarSmoothedVolume;
    private float _rightEarSmoothedVolume;
    private float _direction;
    private float _previousDirection;
    private float _volume;
    private float _previousVolume;

    // Audio capture
    private AudioDeviceNotificationClient? _notificationClient = new AudioDeviceNotificationClient();
    private readonly MMDeviceEnumerator _enumerator = new MMDeviceEnumerator();
    private MMDevice? _activeDevice;
    private WasapiLoopbackCapture? _capture;

    // Constants
    private const int VOLUME_BOOST_FACTOR = 8;
    private const float PARAMETER_CHANGE_THRESHOLD = 0.001f;

    public AudioDeviceNotificationClient? notificationClient = new AudioDeviceNotificationClient();
    public readonly MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
    public MMDevice? activeDevice;
    public WasapiLoopbackCapture? capture;

    [ModulePersistent("disabledDeviceList")]
    public List<AudioDeviceInfo> disabledDeviceList { get; set; } = [];
    [ModulePersistent("selectedDevice")]
    public AudioDeviceInfo? selectedDevice { get; set; } = null;

    protected override void OnPreLoad()
    {
        enumerator.RegisterEndpointNotificationCallback(notificationClient);

        CreateSlider(SqueakMeterSetting.SmoothScalar, "Smooth scalar", "Scalar for smoothing values (default: 0.16)", 0.16f, 0.0f, 0.99f, 0.01f);
        CreateSlider(SqueakMeterSetting.Gain, "Gain", "Scalar for volume (default: 2.0)", 2.0f, 0.1f, 10, 0.1f);
        CreateSlider(SqueakMeterSetting.BassBoost, "Bass boost", "Scalar for bass (default: 3.2)", 3.2f, 0.1f, 10, 0.1f);
        CreateSlider(SqueakMeterSetting.MidBoost, "Mid boost", "Scalar for mid (default: 2.2)", 2.2f, 0.1f, 10, 0.1f);
        CreateSlider(SqueakMeterSetting.TrebleBoost, "Treble boost", "Scalar for treble (default: 3.6)", 3.6f, 0.1f, 10, 0.1f);

        RegisterParameter<float>(SqueakMeterParameter.Volume, "VRCOSC/SqueakMeter/Volume", ParameterMode.Write, "Volume", "Sends value depending on the volume level\nRange: 0 - 1");
        RegisterParameter<float>(SqueakMeterParameter.Bass, "VRCOSC/SqueakMeter/Bass", ParameterMode.Write, "Bass", "Sends the normalized amplitude (volume) of the bass frequency band (0 – 250 Hz)\nRange: 0 - 1");
        RegisterParameter<float>(SqueakMeterParameter.Mid, "VRCOSC/SqueakMeter/Mid", ParameterMode.Write, "Mid", "Sends the normalized amplitude (volume) of the mid frequency band (250 – 4000 Hz)\nRange: 0 - 1");
        RegisterParameter<float>(SqueakMeterParameter.Treble, "VRCOSC/SqueakMeter/Treble", ParameterMode.Write, "Treble", "Sends the normalized amplitude (volume) of the treble frequency band (4000 – 20000 Hz)\nRange: 0 - 1");
        RegisterParameter<float>(SqueakMeterParameter.Direction, "VRCOSC/SqueakMeter/Direction", ParameterMode.Write, "Direction", "Sends value depending on the audio direction (stereo balance)\nRange: 0 - 1 (where 0 = left, 0.5 = center, 1 = right)");

        SetRuntimeView(typeof(AudioDeviceModuleRuntimeView));
    }

    protected override Task<bool> OnModuleStart()
    {
        if (selectedDevice != null)
        {
            SetCaptureDevice(selectedDevice.ID);
        }

        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        try
        {
            // Unregister audio device notification callback
            if (notificationClient != null)
            {
                enumerator.UnregisterEndpointNotificationCallback(notificationClient);
                notificationClient = null;
            }

            // Clean up audio capture
            CleanupCapture();
        }
        catch (Exception error)
        {
            LogDebug($"Error during module stop: {error.Message}");
        }

        return Task.CompletedTask;
    }

    private void CleanupCapture()
    {
        if (capture != null)
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.StopRecording();
            capture.Dispose();
            capture = null;
        }

        activeDevice?.Dispose();
        activeDevice = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        // Only process 32-bit float stereo audio
        if (!_shouldUpdate || _bytesPerSample != 4 || args.BytesRecorded == 0)
            return;

        _shouldUpdate = false;

        int frameSize = _bytesPerSample * 2; // 2 channels (stereo)
        int frameCount = args.BytesRecorded / frameSize;

        if (frameCount == 0)
        {
            _leftEarVolume = 0;
            _rightEarVolume = 0;
            return;
        }

        float leftSum = 0.0f;
        float rightSum = 0.0f;

        for (int i = 0; i < frameCount; i++)
        {
            int offset = i * frameSize;
            leftSum += Math.Abs(BitConverter.ToSingle(args.Buffer, offset));
            rightSum += Math.Abs(BitConverter.ToSingle(args.Buffer, offset + _bytesPerSample));
        }

        _leftEarVolume = (leftSum / frameCount) * VOLUME_BOOST_FACTOR;
        _rightEarVolume = (rightSum / frameCount) * VOLUME_BOOST_FACTOR;

        // Calculate band volumes
        (_bass, _mid, _treble) = SqueakMeterUtils.AnalyzeFrequencies(
            args.Buffer,
            args.BytesRecorded,
            GetBassBoost(),
            GetMidBoost(),
            GetTrebleBoost(),
            GetGain());
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 32)]
    private void ModuleUpdate()
    {
        if (!_enabled)
            return;

        try
        {
            UpdateSmoothedValues();
            SendChangedParameters();
            _shouldUpdate = true;
        }
        catch (Exception error)
        {
            LogDebug($"Audio module update failed: {error.Message}");
            _enabled = false;
        }
    }

    private void UpdateSmoothedValues()
    {
        float smoothScalar = GetSmoothScalar();
        float gain = GetGain();

        _leftEarSmoothedVolume = SqueakMeterUtils.GetSmoothedValue(_leftEarSmoothedVolume, _leftEarVolume * gain, smoothScalar);
        _rightEarSmoothedVolume = SqueakMeterUtils.GetSmoothedValue(_rightEarSmoothedVolume, _rightEarVolume * gain, smoothScalar);
        _bassSmoothed = SqueakMeterUtils.GetSmoothedValue(_bassSmoothed, _bass * gain, smoothScalar);
        _midSmoothed = SqueakMeterUtils.GetSmoothedValue(_midSmoothed, _mid * gain, smoothScalar);
        _trebleSmoothed = SqueakMeterUtils.GetSmoothedValue(_trebleSmoothed, _treble * gain, smoothScalar);

        // Handle NaN or Infinity values by resetting them
        if (float.IsNaN(_leftEarSmoothedVolume) || float.IsInfinity(_leftEarSmoothedVolume))
            _leftEarSmoothedVolume = 0;
        if (float.IsNaN(_rightEarSmoothedVolume) || float.IsInfinity(_rightEarSmoothedVolume))
            _rightEarSmoothedVolume = 0;

        _direction = SqueakMeterUtils.VRCClamp(-(_leftEarSmoothedVolume * 2) + (_rightEarSmoothedVolume * 2) + 0.5f);
        _volume = (_leftEarSmoothedVolume + _rightEarSmoothedVolume) / 2.0f;
    }

    private void SendChangedParameters()
    {
        if (Math.Abs(_direction - _previousDirection) > PARAMETER_CHANGE_THRESHOLD)
        {
            SendParameter(SqueakMeterParameter.Direction, _direction);
            _previousDirection = _direction;
        }

        if (Math.Abs(_volume - _previousVolume) > PARAMETER_CHANGE_THRESHOLD)
        {
            SendParameter(SqueakMeterParameter.Volume, _volume);
            _previousVolume = _volume;
        }

        if (Math.Abs(_bassSmoothed - _bassSmoothedPrevious) > PARAMETER_CHANGE_THRESHOLD)
        {
            SendParameter(SqueakMeterParameter.Bass, _bassSmoothed);
            _bassSmoothedPrevious = _bassSmoothed;
        }

        if (Math.Abs(_midSmoothed - _midSmoothedPrevious) > PARAMETER_CHANGE_THRESHOLD)
        {
            SendParameter(SqueakMeterParameter.Mid, _midSmoothed);
            _midSmoothedPrevious = _midSmoothed;
        }

        if (Math.Abs(_trebleSmoothed - _trebleSmoothedPrevious) > PARAMETER_CHANGE_THRESHOLD)
        {
            SendParameter(SqueakMeterParameter.Treble, _trebleSmoothed);
            _trebleSmoothedPrevious = _trebleSmoothed;
        }
    }

    public MMDeviceCollection GetAudioOutputDevices()
    {
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
    }

    public void SetCaptureDevice(string? deviceId)
    {
        MMDevice? device = GetAudioOutputDevices().FirstOrDefault(d => d.ID == deviceId);

        try
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device), "Device not found");

            CleanupCapture();
            activeDevice = device;

            if (activeDevice.AudioEndpointVolume.HardwareSupport == 0)
                throw new NotSupportedException($"Selected device '{activeDevice.FriendlyName}' does not support capturing.");

            capture = new WasapiLoopbackCapture(activeDevice);
            capture.DataAvailable += OnDataAvailable;
            capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
            _bytesPerSample = capture.WaveFormat.BitsPerSample / capture.WaveFormat.BlockAlign;
            capture.StartRecording();
            _enabled = true;
        }
        catch (Exception error)
        {
            LogDebug($"Audio setup failed: {error.Message}");
            notificationClient?.OnSelectedDeviceError(deviceId ?? "unknown", $"Audio setup failed: {error.Message}");
            activeDevice = null;
            _enabled = false;
        }
    }

    private float GetSmoothScalar() => GetSettingValue<float>(SqueakMeterSetting.SmoothScalar);
    private float GetGain() => GetSettingValue<float>(SqueakMeterSetting.Gain);
    private float GetBassBoost() => SqueakMeterUtils.ScaleSliderValue(GetSettingValue<float>(SqueakMeterSetting.BassBoost), 10, 100);
    private float GetMidBoost() => SqueakMeterUtils.ScaleSliderValue(GetSettingValue<float>(SqueakMeterSetting.MidBoost), 10, 1000);
    private float GetTrebleBoost() => SqueakMeterUtils.ScaleSliderValue(GetSettingValue<float>(SqueakMeterSetting.TrebleBoost), 10, 1000);

    public enum SqueakMeterSetting
    {
        SmoothScalar,
        Gain,
        BassBoost,
        MidBoost,
        TrebleBoost
    }

    public enum SqueakMeterParameter
    {
        Enabled,
        Volume,
        Bass,
        Mid,
        Treble,
        Direction
    }
}
