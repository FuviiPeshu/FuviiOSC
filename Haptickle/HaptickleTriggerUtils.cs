using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FuviiOSC.Common;
using Newtonsoft.Json;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.Parameters.Queryable;

namespace FuviiOSC.Haptickle;

public enum HapticTriggerMode
{
    Off,
    Constant,
    Proximity,
    Velocity,
    OnChange
}

public static class HapticTriggerModeHelper
{
    public static IEnumerable<HapticTriggerMode> AllValues => FuviiCommonUtils.EnumValuesGetter<HapticTriggerMode>.AllValues;
}

public class HapticTrigger
{
    [JsonProperty("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("device_index")]
    public int DeviceIndex { get; set; } = 0;
    [JsonProperty("device_serial_number")]
    public string DeviceSerialNumber { get; set; } = "";
    [JsonProperty("haptic_trigger_params")]
    public ObservableCollection<HapticTriggerQueryableParameter> HapticTriggerParams { get; set; } = new();
    [JsonProperty("trigger_mode")]
    public HapticTriggerMode TriggerMode { get; set; } = HapticTriggerMode.Off;
    [JsonProperty("pattern_config")]
    public VibrationPatternConfig PatternConfig { get; set; } = new VibrationPatternConfig();
}

public class HapticTriggerQueryableParameter : QueryableParameter
{
}

public enum VibrationPatternType
{
    Linear,
    Sine,
    Throb
}

public static class VibrationPatternTypeHelper
{
    public static IEnumerable<VibrationPatternType> AllValues => FuviiCommonUtils.EnumValuesGetter<VibrationPatternType>.AllValues;
}

public class VibrationPatternConfig
{
    public VibrationPatternType Pattern { get; set; } = VibrationPatternType.Linear;
    public float MinStrength { get; set; } = 0.0f;
    public float MaxStrength { get; set; } = 1.0f;
    public float Speed { get; set; } = 1.0f;
}

public static class VibrationPattern
{
    public static float Apply(VibrationPatternConfig config, float value, float delta, float phase)
    {
        float result = 0.0f;
        switch (config.Pattern)
        {
            case VibrationPatternType.Linear:
                result = value;
                break;
            case VibrationPatternType.Sine:
                result = (float)(Math.Sin(phase * config.Speed * 2.0f * Math.PI) * 0.5f + 0.5f) * value;
                break;
            case VibrationPatternType.Throb:
                double period = 0.42 / Math.Max(0.01, config.Speed); // prevent divide by zero
                double dutyCycle = 0.64;
                double t = (phase % period) / period;
                result = (t < dutyCycle) ? value : 0.0f;
                break;
        }
        return Map(result, config.MinStrength, config.MaxStrength);
    }

    private static float Map(float value, float min, float max)
    {
        if (value <= 0) return 0.0f;
        return min + (value * (max - min));
    }
}

public static class HaptickleTriggerUtils
{
    public static bool EvaluateIsValid(
        HapticTrigger trigger,
        QueryResult result,
        ReceivedParameter receivedParameter,
        HapticTriggerQueryableParameter queryableParameter,
        bool wasValid
    )
    {
        if (result.JustBecameValid)
            return true;
        if (result.JustBecameInvalid || !result.IsValid)
        {
            if (trigger.TriggerMode == HapticTriggerMode.Proximity || trigger.TriggerMode == HapticTriggerMode.Velocity)
                return true;
            return FuviiCommonUtils.IsParameterActuallyValid(receivedParameter, queryableParameter);
        }
        return wasValid;
    }
}
