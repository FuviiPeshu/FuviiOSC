using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
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
    private bool enabled = true;
    private bool shouldUpdate;
    private float bass;
    private float bassSmoothed;
    private float bassSmoothedPrevious;
    private float mid;
    private float midSmoothed;
    private float midSmoothedPrevious;
    private float treble;
    private float trebleSmoothed;
    private float trebleSmoothedPrevious;
    private float leftEarVolume;
    private float leftEarSmoothedVolume;
    private float rightEarVolume;
    private float rightEarSmoothedVolume;
    private float direction = 0;
    private float previousDirection = 0;
    private float volume = 0;
    private float previousVolume = 0;
    private int bytesPerSample;

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
            SetCaptureDevice(selectedDevice.ID);

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
            if (capture != null)
            {
                capture.DataAvailable -= OnDataAvailable;
                capture.StopRecording();
                capture.Dispose();
                capture = null;
            }
            activeDevice?.Dispose();
            activeDevice = null;
        } catch (Exception error)
        {
            LogDebug($"Error during module stop: {error.Message}");
        }

        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        // Only process 32-bit float stereo audio
        if (!shouldUpdate || bytesPerSample != 4 || args.BytesRecorded == 0)
            return;

        shouldUpdate = false;
        int frameSize = bytesPerSample * 2; // 2 channels (stereo)
        int frameCount = args.BytesRecorded / frameSize;
        float leftSum = 0.0f;
        float rightSum = 0.0f;
        for (int i = 0; i < frameCount; i++)
        {
            int offset = i * frameSize;
            leftSum += Math.Abs(BitConverter.ToSingle(args.Buffer, offset));
            rightSum += Math.Abs(BitConverter.ToSingle(args.Buffer, offset + bytesPerSample));
        }

        // Avoid division by zero
        if (frameCount == 0)
        {
            leftEarVolume = 0;
            rightEarVolume = 0;
        }
        else
        {
            int volumeBoostFactor = 8; // boost to get reasonable values for volume
            leftEarVolume = (leftSum / frameCount) * volumeBoostFactor;
            rightEarVolume = (rightSum / frameCount) * volumeBoostFactor;
        }

        // Calculate band volumes
        (
            float bassValue,
            float midValue,
            float trebleValue
        ) = SqueakMeterUtils.AnalyzeFrequencies(args.Buffer, args.BytesRecorded, GetBassBoost(), GetMidBoost(), GetTrebleBoost(), GetGain());
        bass = bassValue;
        mid = midValue;
        treble = trebleValue;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 32)]
    private void ModuleUpdate()
    {
        if (!enabled)
            return;

        try
        {
            // Get smoothed values
            float smoothScalar = GetSmoothScalar();
            float gain = GetGain();
            leftEarSmoothedVolume = SqueakMeterUtils.GetSmoothedValue(leftEarSmoothedVolume, leftEarVolume * gain, smoothScalar);
            rightEarSmoothedVolume = SqueakMeterUtils.GetSmoothedValue(rightEarSmoothedVolume, rightEarVolume * gain, smoothScalar);
            bassSmoothed = SqueakMeterUtils.GetSmoothedValue(bassSmoothed, bass * gain, smoothScalar);
            midSmoothed = SqueakMeterUtils.GetSmoothedValue(midSmoothed, mid * gain, smoothScalar);
            trebleSmoothed = SqueakMeterUtils.GetSmoothedValue(trebleSmoothed, treble * gain, smoothScalar);
            // Handle NaN or Infinity values by resetting them
            if (float.IsNaN(leftEarSmoothedVolume) || float.IsNaN(rightEarSmoothedVolume) ||
                float.IsInfinity(leftEarSmoothedVolume) || float.IsInfinity(rightEarSmoothedVolume))
            {
                leftEarSmoothedVolume = 0;
                rightEarSmoothedVolume = 0;
            }
            direction = SqueakMeterUtils.VRCClamp(-(leftEarSmoothedVolume * 2) + (rightEarSmoothedVolume * 2) + 0.5f);
            volume = (leftEarSmoothedVolume + rightEarSmoothedVolume) / 2.0f;

            // Send parameters only if they have changed significantly
            if (Math.Abs(direction - previousDirection) > 0.001f)
            {
                SendParameterAndWait(SqueakMeterParameter.Direction, direction);
                previousDirection = direction;
            }
            if (Math.Abs(volume - previousVolume) > 0.001f)
            {
                SendParameterAndWait(SqueakMeterParameter.Volume, volume);
                previousVolume = volume;
            }
            if (Math.Abs(bassSmoothed - bassSmoothedPrevious) > 0.001f)
            {
                SendParameterAndWait(SqueakMeterParameter.Bass, bassSmoothed);
                bassSmoothedPrevious = bassSmoothed;
            }
            if (Math.Abs(midSmoothed - midSmoothedPrevious) > 0.001f)
            {
                SendParameterAndWait(SqueakMeterParameter.Mid, midSmoothed);
                midSmoothedPrevious = midSmoothed;
            }
            if (Math.Abs(trebleSmoothed - trebleSmoothedPrevious) > 0.001f)
            {
                SendParameterAndWait(SqueakMeterParameter.Treble, trebleSmoothed);
                trebleSmoothedPrevious = trebleSmoothed;
            }
            // Reset the flag to allow next update
            shouldUpdate = true;
        }
        catch (Exception error)
        {
            LogDebug($"Audio module update failed: {error.Message}");
            enabled = false;
        }
    }

    public MMDeviceCollection GetAudioOutputDevices() => enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

    public void SetCaptureDevice(string? deviceId)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            MMDevice? device = GetAudioOutputDevices().FirstOrDefault(d => d.ID == deviceId);

            try
            {
                if (device == null)
                    throw new ArgumentNullException(nameof(device));

                // Clean up previous capture and device
                if (capture != null)
                {
                    capture.DataAvailable -= OnDataAvailable;
                    capture.StopRecording();
                    capture.Dispose();
                    capture = null;
                }

                activeDevice?.Dispose();
                activeDevice = device;

                if (activeDevice.AudioEndpointVolume.HardwareSupport == 0)
                    throw new NotSupportedException($"Selected device '{activeDevice.FriendlyName}' does not support capturing.");

                capture = new WasapiLoopbackCapture(activeDevice);
                capture.DataAvailable += OnDataAvailable;
                capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
                bytesPerSample = capture.WaveFormat.BitsPerSample / capture.WaveFormat.BlockAlign;
                capture.StartRecording();
                enabled = true;
            }
            catch (Exception error)
            {
                LogDebug($"Audio setup failed: {error.Message}");
                notificationClient?.OnSelectedDeviceError(deviceId ?? "unknown", $"Audio setup failed: {error.Message}");
                activeDevice = null;
                enabled = false;
            }
        });
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
