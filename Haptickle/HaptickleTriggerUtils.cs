using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
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
    private const float TWO_PI = 2.0f * MathF.PI;
    private const float DEFAULT_PERIOD_FACTOR = 0.42f;
    private const float DEFAULT_DUTY_CYCLE = 0.64f;
    private const float MIN_SPEED = 0.01f;

    public static float Apply(VibrationPatternConfig config, float value, float delta, float phase)
    {
        float result = config.Pattern switch
        {
            VibrationPatternType.Linear => value,
            VibrationPatternType.Sine => ApplySinePattern(value, phase, config.Speed),
            VibrationPatternType.Throb => ApplyThrobPattern(value, phase, config.Speed),
            _ => value
        };

        return MapToRange(result, config.MinStrength, config.MaxStrength);
    }

    private static float ApplySinePattern(float value, float phase, float speed)
    {
        float sineValue = MathF.Sin(phase * speed * TWO_PI) * 0.5f + 0.5f;
        return sineValue * value;
    }

    private static float ApplyThrobPattern(float value, float phase, float speed)
    {
        float period = DEFAULT_PERIOD_FACTOR / Math.Max(MIN_SPEED, speed);
        float t = (phase % period) / period;
        return t < DEFAULT_DUTY_CYCLE ? value : 0.0f;
    }

    private static float MapToRange(float value, float min, float max)
    {
        if (value <= 0) return 0.0f;
        return min + (value * (max - min));
    }
}

[Serializable]
public class DeviceMapping : IEquatable<DeviceMapping>
{
    public string ID { get; set; } = Guid.NewGuid().ToString();
    public string Parameter { get; set; } = "Parameter";
    public string DeviceIp { get; set; } = "192.168.";
    public int DevicePort { get; set; } = 8888;
    public string DeviceOscPath { get; set; } = "/motor";
    public HapticTriggerMode TriggerMode { get; set; } = HapticTriggerMode.Proximity;
    public VibrationPatternConfig PatternConfig { get; set; } = new VibrationPatternConfig();

    public DeviceMapping()
    {
    }

    public DeviceMapping(string parameter, string deviceIp, int devicePort, string deviceOscPath)
    {
        Parameter = parameter;
        DeviceIp = deviceIp;
        DevicePort = devicePort;
        DeviceOscPath = deviceOscPath;
    }

    public bool Equals(DeviceMapping? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ID == other.ID;
    }

    public override bool Equals(object? obj) => Equals(obj as DeviceMapping);

    public override int GetHashCode() => ID.GetHashCode();
}

public static class HaptickleUtils
{
    public static bool EvaluateIsValid(
        HapticTrigger trigger,
        QueryResult result,
        VRChatParameter receivedParameter,
        HapticTriggerQueryableParameter queryableParameter,
        bool wasValid)
    {
        if (result.JustBecameValid)
            return true;

        if (result.JustBecameInvalid || !result.IsValid)
        {
            // For continuous modes, keep checking actual value
            if (trigger.TriggerMode is HapticTriggerMode.Proximity or HapticTriggerMode.Velocity)
                return true;

            return FuviiCommonUtils.IsParameterActuallyValid(receivedParameter, queryableParameter);
        }

        return wasValid;
    }

    public static void SendOscMessage(string ip, int port, string address, int value)
    {
        var msg = new List<byte>();

        // OSC Address (null-padded to 4-byte boundary)
        byte[] addrBytes = Encoding.ASCII.GetBytes(address);
        msg.AddRange(addrBytes);
        msg.Add(0);
        while (msg.Count % 4 != 0) msg.Add(0);

        // Type tag ",i" for integer
        msg.AddRange([(byte)',', (byte)'i', 0, 0]);

        // Integer value (big-endian)
        byte[] intBytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(intBytes);
        msg.AddRange(intBytes);

        using var udp = new UdpClient();
        udp.Connect(ip, port);
        udp.Send(msg.ToArray(), msg.Count);
    }
}
