using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Valve.VR;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.Parameters.Queryable;

namespace FuviiOSC.Haptickle;

[ModuleTitle("Haptickle")]
[ModuleDescription("Triggers Vive tracker haptics based on avatar parameters")]
[ModuleType(ModuleType.Generic)]
public class HaptickleModule : Module
{
    private readonly Dictionary<string, CancellationTokenSource> _pulseTokens = [];
    private const ushort _MIN_HAPTIC_PULSE_DURATION = 16;
    private const ushort _MAX_HAPTIC_PULSE_DURATION = 64;
    private const ushort _DEFAULT_PULSE_INTERVAL = 16;
    private const ushort _TRACKER_HAPTIC_AXIS_ID = 1;

    public CVRSystem? openVrSystem;

    [ModulePersistent("hapticTriggers")]
    public List<HapticTrigger> HapticTriggers { get; set; } = [];

    protected override void OnPreLoad()
    {
        SetRuntimeView(typeof(HaptickleModuleRuntimeView));
    }

    protected override Task<bool> OnModuleStart()
    {
        EVRInitError evrError = EVRInitError.None;
        try
        {
            openVrSystem = OpenVR.Init(ref evrError, EVRApplicationType.VRApplication_Overlay);
            if (evrError != EVRInitError.None || openVrSystem == null)
            {
                throw new Exception($"OpenVR initialization failed with error: {evrError}");
            }
        }
        catch (Exception error)
        {
            LogDebug($"Error during module start: {error.Message}");
            openVrSystem = null;
            return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        if (openVrSystem != null)
        {
            OpenVR.Shutdown();
            openVrSystem = null;
        }

        foreach (CancellationTokenSource pulseToken in _pulseTokens.Values)
            pulseToken.Cancel();
        _pulseTokens.Clear();

        return Task.CompletedTask;
    }

    protected override void OnAnyParameterReceived(ReceivedParameter receivedParameter)
    {
        if (openVrSystem == null || receivedParameter == null) return;

        foreach (HapticTrigger trigger in HapticTriggers)
        {
            foreach (HapticTriggerQueryableParameter queryableParameter in trigger.HapticTriggerParams.Where(param => param.Name.Value == receivedParameter.Name))
            {
                QueryResult result = queryableParameter.Evaluate(receivedParameter);
                if (result != null && result.JustBecameValid)
                {
                    LogDebug($"Triggering haptic on device {trigger.DeviceSerialNumber} with strength set to {trigger.HapticStrength}");
                    StartPulseLoop(trigger);
                }
                // Stop pulsing if just became invalid
                else if (result != null && result.JustBecameInvalid)
                {
                    StopPulseLoop(trigger);
                }
            }
        }
    }

    private void StartPulseLoop(HapticTrigger trigger)
    {
        string key = trigger.DeviceSerialNumber;
        StopPulseLoop(trigger); // ensure no duplicate tasks

        CancellationTokenSource pulseToken = new CancellationTokenSource();
        _pulseTokens[key] = pulseToken;

        Task.Run(async () =>
        {
            while (!pulseToken.Token.IsCancellationRequested)
            {
                float strength = trigger.HapticStrength;
                if (strength <= 0.0f)
                {
                    await Task.Delay(_DEFAULT_PULSE_INTERVAL, pulseToken.Token);
                    continue;
                }

                ushort pulseDuration = (ushort)(_MIN_HAPTIC_PULSE_DURATION + (strength * (_MAX_HAPTIC_PULSE_DURATION - _MIN_HAPTIC_PULSE_DURATION)));
                openVrSystem?.TriggerHapticPulse((uint)trigger.DeviceIndex, _TRACKER_HAPTIC_AXIS_ID, pulseDuration);

                await Task.Delay(pulseDuration + _DEFAULT_PULSE_INTERVAL, pulseToken.Token);
            }
        }, pulseToken.Token);
    }

    private void StopPulseLoop(HapticTrigger trigger)
    {
        string key = trigger.DeviceSerialNumber;
        if (_pulseTokens.TryGetValue(key, out CancellationTokenSource? pulseToken))
        {
            pulseToken.Cancel();
            _pulseTokens.Remove(key);
        }
    }

    public enum HaptickleSetting
    {
        HapticTriggers
    }

    public enum HaptickleParameter
    {
    }
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

    public bool Equals(HapticTrigger? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return DeviceIndex.Equals(other.DeviceIndex) && HapticStrength.Equals(other.HapticStrength) && HapticTriggerParams.SequenceEqual(other.HapticTriggerParams);
    }
}

public class HapticTriggerQueryableParameter : QueryableParameter
{
}
