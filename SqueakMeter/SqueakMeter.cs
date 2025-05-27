using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace FuviiOSC.SqueakMeter;

[ModuleTitle("Squeak Meter")]
[ModuleDescription("Listens to the default audio output device and provides OSC parameters for volume, frequencies and direction")]
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

    protected override void OnPreLoad()
    {
        enumerator.RegisterEndpointNotificationCallback(notificationClient);

        CreateSlider(SqueakMeterSetting.SmoothScalar, "Smooth scalar", "Scalar for smoothing values (default: 0.16)", 0.16f, 0.0f, 0.99f, 0.01f);
        CreateSlider(SqueakMeterSetting.Gain, "Gain", "Scalar for volume (default: 2)", 2.0f, 0.1f, 10, 0.1f);
        CreateSlider(SqueakMeterSetting.BassBoost, "Bass boost", "Scalar for bass (default: 32)", 32, 1, 100, 1);
        CreateSlider(SqueakMeterSetting.MidBoost, "Mid boost", "Scalar for mid (default: 220)", 220, 10, 1000, 10);
        CreateSlider(SqueakMeterSetting.TrebleBoost, "Treble boost", "Scalar for treble (default: 360)", 360, 10, 1000, 10);

        RegisterParameter<float>(SqueakMeterParameter.Volume, "VRCOSC/SqueakMeter/Volume", ParameterMode.Write, "Volume", "Sends value depending on the volume level\nRange: 0 - 1");
        RegisterParameter<float>(SqueakMeterParameter.Bass, "VRCOSC/SqueakMeter/Bass", ParameterMode.Write, "Bass", "Sends the normalized amplitude (volume) of the bass frequency band (0 – 250 Hz)\nRange: 0 - 1");
        RegisterParameter<float>(SqueakMeterParameter.Mid, "VRCOSC/SqueakMeter/Mid", ParameterMode.Write, "Mid", "Sends the normalized amplitude (volume) of the mid frequency band (250 – 4000 Hz)\nRange: 0 - 1");
        RegisterParameter<float>(SqueakMeterParameter.Treble, "VRCOSC/SqueakMeter/Treble", ParameterMode.Write, "Treble", "Sends the normalized amplitude (volume) of the treble frequency band (4000 – 20000 Hz)\nRange: 0 - 1");
        RegisterParameter<float>(SqueakMeterParameter.Direction, "VRCOSC/SqueakMeter/Direction", ParameterMode.Write, "Direction", "Sends value depending on the audio direction (stereo balance)\nRange: 0 - 1 (where 0 = left, 0.5 = center, 1 = right)");

        SetRuntimeView(typeof(AudioDeviceModuleRuntimeView));
    }

    protected override Task OnModuleStop()
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

        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        // Only process 32-bit float stereo audio
        if (!shouldUpdate || bytesPerSample != 4 || args.BytesRecorded == 0)
        {
            return;
        }

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
        AnalyzeFrequencies(args.Buffer, args.BytesRecorded);
    }

    private void AnalyzeFrequencies(byte[] buffer, int bytesRecorded)
    {
        int fftLength = 1024; // must be a power of 2
        int bytesPerSample = 4; // 32-bit float
        int channels = 2;
        int samples = Math.Min(bytesRecorded / bytesPerSample, fftLength * channels);
        int bassBoost = GetBassBoost();
        int midBoost = GetMidBoost();
        int trebleBoost = GetTrebleBoost();
        float gain = GetGain();

        // Prepare FFT buffer (average left and right channels)
        Complex[] fftBuffer = new Complex[fftLength];
        for (int i = 0, sample = 0; i < samples && sample < fftLength; i += channels)
        {
            float left = BitConverter.ToSingle(buffer, i * bytesPerSample);
            float right = BitConverter.ToSingle(buffer, (i + 1) * bytesPerSample);
            float sampleValue = (left + right) * 0.5f;
            fftBuffer[sample].X = sampleValue;
            fftBuffer[sample].Y = 0;
            sample++;
        }

        FastFourierTransform.FFT(true, (int)Math.Log(fftLength, 2), fftBuffer);

        // Frequency bins: 0 = DC, N/2 = Nyquist (half sample rate)
        float sampleRate = 48000f;
        float binSize = sampleRate / fftLength;
        float bassRaw = 0, midRaw = 0, trebleRaw = 0;
        int bassBins = 0, midBins = 0, trebleBins = 0;

        for (int i = 0; i < fftLength / 2; i++)
        {
            float freq = i * binSize;
            float magnitude = (float)Math.Sqrt(fftBuffer[i].X * fftBuffer[i].X + fftBuffer[i].Y * fftBuffer[i].Y);

            if (freq < 250) // bass: 0-250Hz
            {
                bassRaw += magnitude;
                bassBins++;
            }
            else if (freq < 4000) // mid: 250-4000Hz
            {
                midRaw += magnitude;
                midBins++;
            }
            else if (freq < 20000) // treble: 4k-20kHz
            {
                trebleRaw += magnitude;
                trebleBins++;
            }
        }

        bass = bassBins > 0 ? (bassRaw / bassBins) * bassBoost : 0;
        mid = midBins > 0 ? (midRaw / midBins) * midBoost : 0;
        treble = trebleBins > 0 ? (trebleRaw / trebleBins) * trebleBoost : 0;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 32)]
    private void ModuleUpdate()
    {
        if (!enabled)
        {
            return;
        }

        try
        {
            // Get smoothed values
            float smoothScalar = GetSmoothScalar();
            float gain = GetGain();
            leftEarSmoothedVolume = GetSmoothedValue(leftEarSmoothedVolume, leftEarVolume * gain, smoothScalar);
            rightEarSmoothedVolume = GetSmoothedValue(rightEarSmoothedVolume, rightEarVolume * gain, smoothScalar);
            bassSmoothed = GetSmoothedValue(bassSmoothed, bass * gain, smoothScalar);
            midSmoothed = GetSmoothedValue(midSmoothed, mid * gain, smoothScalar);
            trebleSmoothed = GetSmoothedValue(trebleSmoothed, treble * gain, smoothScalar);
            // Handle NaN or Infinity values by resetting them
            if (float.IsNaN(leftEarSmoothedVolume) || float.IsNaN(rightEarSmoothedVolume) ||
                float.IsInfinity(leftEarSmoothedVolume) || float.IsInfinity(rightEarSmoothedVolume))
            {
                leftEarSmoothedVolume = 0;
                rightEarSmoothedVolume = 0;
            }
            direction = VRCClamp(-(leftEarSmoothedVolume * 2) + (rightEarSmoothedVolume * 2) + 0.5f);
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
            LogDebug($"Audio module update failed: {error}");
            enabled = false;
        }
    }

    public void SetCaptureDevice(string deviceId)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            MMDevice? device = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).First(d => d.ID == deviceId);

            try
            {
                LogDebug($"Setting capture device: {device.FriendlyName} ({device.ID})");
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
                activeDevice = null;
                enabled = false;
            }
        });
    }

    // Limit value to the range [0.0, 1.0] with a minimum threshold of 0.01 to avoid flickering
    private static float VRCClamp(float value)
    {
        float clmapedValue = Math.Clamp(value, 0.0f, 1.0f);
        return clmapedValue < 0.01f ? 0.0f : clmapedValue;
    }

    private static float GetSmoothedValue(float firstFloat, float secondFloat, float smooth)
    {
        return VRCClamp(firstFloat * smooth + secondFloat * (1 - smooth));
    }

    private float GetSmoothScalar() => GetSettingValue<float>(SqueakMeterSetting.SmoothScalar);
    private float GetGain() => GetSettingValue<float>(SqueakMeterSetting.Gain);
    private int GetBassBoost() => GetSettingValue<int>(SqueakMeterSetting.BassBoost);
    private int GetMidBoost() => GetSettingValue<int>(SqueakMeterSetting.MidBoost);
    private int GetTrebleBoost() => GetSettingValue<int>(SqueakMeterSetting.TrebleBoost);

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
