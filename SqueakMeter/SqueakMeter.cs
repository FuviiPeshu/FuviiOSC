using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace FuviiOSC.SqueakMeter;

[ModuleTitle("Squeak Meter")]
[ModuleDescription("Listens to the default audio output device and provides OSC parameters for volume, frequencies and direction")]
[ModuleType(ModuleType.Generic)]
public class SqueakMeterModule : Module
{
    private float _bass;
    private float _mid;
    private float _treble;
    private float _leftEarVolume;
    private float _rightEarVolume;
    private float _direction = 0;
    private float _volume = 0;

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
