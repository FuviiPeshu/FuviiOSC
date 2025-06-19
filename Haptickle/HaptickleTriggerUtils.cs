using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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

public class HapticTrigger
{
    [JsonProperty("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("device_index")]
    public int DeviceIndex { get; set; } = 0;
    [JsonProperty("device_serial_number")]
    public string DeviceSerialNumber { get; set; } = "";
    [JsonProperty("haptic_strength")]
    public float HapticStrength { get; set; } = 0.5f;
    [JsonProperty("haptic_trigger_params")]
    public ObservableCollection<HapticTriggerQueryableParameter> HapticTriggerParams { get; set; } = new();
    [JsonProperty("trigger_mode")]
    public HapticTriggerMode TriggerMode { get; set; } = HapticTriggerMode.Off;
}

public class HapticTriggerQueryableParameter : QueryableParameter
{
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
