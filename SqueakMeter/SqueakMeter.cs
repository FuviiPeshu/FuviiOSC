using System;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace FuviiOSC.SqueakMeter;

[ModuleTitle("Squeak Meter")]
[ModuleDescription("Listens to the default audio output device and provides OSC parameters for volume, frequencies and direction")]
[ModuleType(ModuleType.Generic)]
public class SqueakMeterModule : Module
{
    private bool _enabled = true;
    private float _bass;
    private float _bassSmoothedVolume;
    private float _bassSmoothedVolumePrevious;
    private float _mid;
    private float _midSmoothedVolume;
    private float _midSmoothedVolumePrevious;
    private float _treble;
    private float _trebleSmoothedVolume;
    private float _trebleSmoothedVolumePrevious;
    private float _leftEarVolume;
    private float _leftEarSmoothedVolume;
    private float _rightEarVolume;
    private float _rightEarSmoothedVolume;
    private float _direction = 0;
    private float _previousDirection = 0;
    private float _volume = 0;
    private float _previousVolume = 0;

    protected override void OnPreLoad()
    {
        CreateSlider(SqueakMeterSetting.SmoothScalar, "Smooth scalar", "Scalar for smoothing values (default: 0.16)", 0.16f, 0.0f, 0.99f, 0.01f);
        CreateSlider(SqueakMeterSetting.Gain, "Gain", "Scalar for volume (default: 2)", 2.0f, 0.1f, 10, 0.1f);
        CreateSlider(SqueakMeterSetting.BassBoost, "Bass boost", "Scalar for bass (default: 2)", 2.0f, 0.1f, 10, 0.1f);
        CreateSlider(SqueakMeterSetting.MidBoost, "Mid boost", "Scalar for mid (default: 4)", 4.0f, 0.1f, 10, 0.1f);
        CreateSlider(SqueakMeterSetting.TrebleBoost, "Treble boost", "Scalar for treble (default: 6)", 6.0f, 0.1f, 10, 0.1f);

        RegisterParameter<float>(SqueakMeterParameter.Volume, "VRCOSC/SqueakMeter/Volume", ParameterMode.Write, "Volume", "Sends value depending on the volume level\nRange: 0.0 - 1.0");
        RegisterParameter<float>(SqueakMeterParameter.Bass, "VRCOSC/SqueakMeter/Bass", ParameterMode.Write, "Bass", "Sends the normalized amplitude (volume) of the bass frequency band (0 – 250 Hz)\nRange: 0.0 - 1.0");
        RegisterParameter<float>(SqueakMeterParameter.Mid, "VRCOSC/SqueakMeter/Mid", ParameterMode.Write, "Mid", "Sends the normalized amplitude (volume) of the mid frequency band (250 – 4000 Hz)\nRange: 0.0 - 1.0");
        RegisterParameter<float>(SqueakMeterParameter.Treble, "VRCOSC/SqueakMeter/Treble", ParameterMode.Write, "Treble", "Sends the normalized amplitude (volume) of the treble frequency band (4000 – 20000 Hz)\nRange: 0.0 - 1.0");
        RegisterParameter<float>(SqueakMeterParameter.Direction, "VRCOSC/SqueakMeter/Direction", ParameterMode.Write, "Direction", "Sends value depending on the direction\n(Range: 0.0 - 1.0 where 0.0 means left, 0.5 center and 1.0 right)");
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 32)]
    private void ModuleUpdate()
    {
        if (!_enabled)
        {
            return;
        }

        try
        {
            // Get smoothed values
            float smoothScalar = GetSmoothScalar();
            float gain = GetGain();
            _leftEarSmoothedVolume = GetSmoothedValue(_leftEarSmoothedVolume, _leftEarVolume * gain, smoothScalar);
            _rightEarSmoothedVolume = GetSmoothedValue(_rightEarSmoothedVolume, _rightEarVolume * gain, smoothScalar);
            _bassSmoothedVolume = GetSmoothedValue(_bassSmoothedVolume, _bass * gain, smoothScalar);
            _midSmoothedVolume = GetSmoothedValue(_midSmoothedVolume, _mid * gain, smoothScalar);
            _trebleSmoothedVolume = GetSmoothedValue(_trebleSmoothedVolume, _treble * gain, smoothScalar);
            // Handle NaN or Infinity values by resetting them
            if (float.IsNaN(_leftEarSmoothedVolume) || float.IsNaN(_rightEarSmoothedVolume) ||
                float.IsInfinity(_leftEarSmoothedVolume) || float.IsInfinity(_rightEarSmoothedVolume))
            {
                _leftEarSmoothedVolume = 0;
                _rightEarSmoothedVolume = 0;
            }
            _direction = VRCClamp(-(_leftEarSmoothedVolume * 2) + (_rightEarSmoothedVolume * 2) + 0.5f);
            _volume = (_leftEarSmoothedVolume + _rightEarSmoothedVolume) / 2.0f;

            // Send parameters only if they have changed significantly
            if (Math.Abs(_direction - _previousDirection) > 0.001f)
            {
                SendParameterAndWait(SqueakMeterParameter.Direction, _direction);
                _previousDirection = _direction;
            }
            if (Math.Abs(_volume - _previousVolume) > 0.001f)
            {
                SendParameterAndWait(SqueakMeterParameter.Volume, _volume);
                _previousVolume = _volume;
            }
            if (Math.Abs(_bassSmoothedVolume - _bassSmoothedVolumePrevious) > 0.001f)
            {
                SendParameterAndWait(SqueakMeterParameter.Bass, _bassSmoothedVolume);
                _bassSmoothedVolumePrevious = _bassSmoothedVolume;
            }
            if (Math.Abs(_midSmoothedVolume - _midSmoothedVolumePrevious) > 0.001f)
            {
                SendParameterAndWait(SqueakMeterParameter.Mid, _midSmoothedVolume);
                _midSmoothedVolumePrevious = _midSmoothedVolume;
            }
            if (Math.Abs(_trebleSmoothedVolume - _trebleSmoothedVolumePrevious) > 0.001f)
            {
                SendParameterAndWait(SqueakMeterParameter.Treble, _trebleSmoothedVolume);
                _trebleSmoothedVolumePrevious = _trebleSmoothedVolume;
            }
        }
        catch (Exception error)
        {
            Log($"Audio module update failed: {error}");
            _enabled = false;
        }
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

    private enum SqueakMeterSetting
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
