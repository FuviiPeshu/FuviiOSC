using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Valve.VR;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.Parameters.Queryable;

namespace FuviiOSC.Haptickle;

[ModuleTitle("Haptickle")]
[ModuleDescription("Triggers Vive tracker haptics (if vibration motor is attached) based on avatar parameters")]
[ModuleType(ModuleType.Generic)]
public class HaptickleModule : Module
{
    private readonly Dictionary<string, CancellationTokenSource> _pulseTokens = new();
    private readonly Dictionary<string, float> _lastFloatValues = new();
    private readonly Dictionary<string, DateTime> _lastValueTimestamps = new();
    private const ushort _MIN_HAPTIC_PULSE_DURATION = 20;
    private const ushort _MAX_HAPTIC_PULSE_DURATION = 80;
    private const ushort _DEFAULT_PULSE_INTERVAL = 32;
    private const ushort _TRACKER_HAPTIC_AXIS_ID = 1;
    private const ushort _DEFAULT_DELAY = 420;
    private const float _MIN_MARGIN = 0.02f;
    private const float _DEFAULT_HAPTIC_STRENGTH = 1.0f;
    private readonly Dictionary<string, float> _lastTriggerValues = new();
    private readonly Dictionary<string, float> _lastTriggerDeltas = new();
    private readonly Dictionary<string, DateTime> _triggerStartTimes = new();
    // Workaround: Track validity state for each trigger/parameter (bug with not keeping isValid when value conditions are met and maintained)
    private readonly Dictionary<string, bool> _parameterValidStates = new();

    public CVRSystem? openVrSystem;

    [ModulePersistent("hapticTriggers")]
    public List<HapticTrigger> HapticTriggers { get; set; } = [];

    protected override void OnPreLoad()
    {
        CreateSlider(HaptickleSetting.Timeout, "Timeout (s)", "How many seconds until haptic loop breaks in case of error/disconnect", 4, 1, 10, 1);

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
                if (result == null) continue;

                // --- WORKAROUND: Maintain our own "isValid" state due to VRCOSC bug ---
                string key = $"{trigger.DeviceSerialNumber}:{queryableParameter.Name.Value}";
                bool wasValid = _parameterValidStates.TryGetValue(key, out bool prevValid) && prevValid;
                bool isValid = HaptickleTriggerUtils.EvaluateIsValid(trigger, result, receivedParameter, queryableParameter, wasValid);
                _parameterValidStates[key] = isValid;
                // --- END WORKAROUND ---
                _lastValueTimestamps[key] = DateTime.UtcNow;

                switch (trigger.TriggerMode)
                {
                    case HapticTriggerMode.Off:
                        StopPulseLoop(trigger);
                        break;
                    case HapticTriggerMode.Constant:
                        HandleConstant(trigger, key, isValid, wasValid, result);
                        break;
                    case HapticTriggerMode.Proximity:
                        HandleProximity(trigger, key, isValid, wasValid, receivedParameter);
                        break;
                    case HapticTriggerMode.Velocity:
                        HandleVelocity(trigger, key, isValid, wasValid, receivedParameter);
                        break;
                    case HapticTriggerMode.OnChange:
                        HandleOnChange(trigger, key, isValid, wasValid);
                        break;
                }
            }
        }
    }

    public void IdentifyDevice(HapticTrigger trigger)
    {
        openVrSystem?.TriggerHapticPulse((uint)trigger.DeviceIndex, 0, 500); // 500ms triggers blinking on the tracker LED
    }

    private void StartPulseLoop(HapticTrigger trigger, string? scalarKey = null)
    {
        string key = trigger.DeviceSerialNumber;
        StopPulseLoop(trigger); // ensure no duplicate tasks

        CancellationTokenSource pulseToken = new CancellationTokenSource();
        _pulseTokens[key] = pulseToken;

        Task.Run(async () =>
        {
            int index = 0;
            while (!pulseToken.Token.IsCancellationRequested)
            {
                float value = 1.0f;
                float delta = 0.0f;
                float phase = 0.0f;

                if (scalarKey != null)
                {
                    _lastTriggerValues.TryGetValue(scalarKey, out value);
                    _lastTriggerDeltas.TryGetValue(scalarKey, out delta);

                    // Timeout to prevent infinite loops (e.g. if tracker is disconnected)
                    if (_lastValueTimestamps.TryGetValue(scalarKey, out DateTime lastTime) && (DateTime.UtcNow - lastTime).TotalSeconds > GetTimeoutValue())
                        break;

                    if (_triggerStartTimes.TryGetValue(scalarKey, out DateTime start))
                        phase = (float)(DateTime.UtcNow - start).TotalSeconds; 
                }

                float patterned = VibrationPattern.Apply(trigger.PatternConfig, value, delta, phase);
                float strength = Math.Clamp(_DEFAULT_HAPTIC_STRENGTH * patterned, 0.0f, 1.0f);

                if (openVrSystem == null || strength <= 0.0f)
                {
                    await Task.Delay(_DEFAULT_DELAY, pulseToken.Token);
                    continue;
                }

                ushort pulseDuration = (ushort)(_MIN_HAPTIC_PULSE_DURATION + (strength * (_MAX_HAPTIC_PULSE_DURATION - _MIN_HAPTIC_PULSE_DURATION)));
                openVrSystem.TriggerHapticPulse((uint)trigger.DeviceIndex, _TRACKER_HAPTIC_AXIS_ID, pulseDuration);
                index += 1;

                await Task.Delay(pulseDuration + _DEFAULT_PULSE_INTERVAL, pulseToken.Token);
            }
            StopPulseLoop(trigger);
        }, pulseToken.Token);
    }

    public void StopPulseLoop(HapticTrigger trigger)
    {
        string key = trigger.DeviceSerialNumber;
        if (_pulseTokens.TryGetValue(key, out CancellationTokenSource? pulseToken))
        {
            pulseToken.Cancel();
            _pulseTokens.Remove(key);
        }
    }

    private void HandleConstant(HapticTrigger trigger, string key, bool isValid, bool wasValid, QueryResult result)
    {
        _lastTriggerValues[key] = _DEFAULT_HAPTIC_STRENGTH;
        _lastTriggerDeltas[key] = 0;

        if (!_triggerStartTimes.ContainsKey(key))
            _triggerStartTimes[key] = DateTime.UtcNow;

        if (result.JustBecameValid)
            StartPulseLoop(trigger, key);
        else if (!isValid && wasValid)
            StopPulseLoop(trigger);
    }

    private void HandleProximity(HapticTrigger trigger, string key, bool isValid, bool wasValid, ReceivedParameter receivedParameter)
    {
        float proximityValue = receivedParameter.GetValue<float>();
        _lastTriggerValues[key] = proximityValue;
        _lastTriggerDeltas[key] = 0;

        if (!_triggerStartTimes.ContainsKey(key))
            _triggerStartTimes[key] = DateTime.UtcNow;

        if (isValid && receivedParameter.Type == ParameterType.Float && !_pulseTokens.ContainsKey(trigger.DeviceSerialNumber))
            StartPulseLoop(trigger, key);
        else if (!isValid && wasValid)
            StopPulseLoop(trigger);
    }

    private void HandleVelocity(HapticTrigger trigger, string key, bool isValid, bool wasValid, ReceivedParameter receivedParameter)
    {
        float value = receivedParameter.GetValue<float>();
        float lastValue = _lastFloatValues.TryGetValue(key, out float v) ? v : value;
        DateTime now = DateTime.UtcNow;
        DateTime lastTimestamp = _lastValueTimestamps.TryGetValue(key, out DateTime t) ? t : now;
        float timePassed = (float)Math.Max((now - lastTimestamp).TotalSeconds, 0.01f);
        float velocity = Math.Abs(value - lastValue) / timePassed;
        bool hasMovedSignificantly = velocity > _MIN_MARGIN;

        _lastFloatValues[key] = value;
        _lastValueTimestamps[key] = now;
        _lastTriggerValues[key] = value;
        _lastTriggerDeltas[key] = velocity;

        if (!_triggerStartTimes.ContainsKey(key))
            _triggerStartTimes[key] = DateTime.UtcNow;

        if (isValid && receivedParameter.Type == ParameterType.Float)
        {
            if (hasMovedSignificantly && !_pulseTokens.ContainsKey(trigger.DeviceSerialNumber))
                StartPulseLoop(trigger, key);
            else if (!hasMovedSignificantly)
                StopPulseLoop(trigger);
        }
        else if (!isValid && wasValid)
        {
            _lastFloatValues[key] = 0.0f;
            _lastTriggerValues[key] = 0.0f;
            _lastTriggerDeltas[key] = 0.0f;
            StopPulseLoop(trigger);
        }
    }

    private void HandleOnChange(HapticTrigger trigger, string key, bool isValid, bool wasValid)
    {
        _lastTriggerValues[key] = _DEFAULT_HAPTIC_STRENGTH;
        _lastTriggerDeltas[key] = 0;

        if (!_triggerStartTimes.ContainsKey(key))
            _triggerStartTimes[key] = DateTime.UtcNow;

        if (isValid != wasValid)
        {
            StartPulseLoop(trigger, key);
            Task.Run(async () =>
            {
                await Task.Delay(_DEFAULT_DELAY);
                StopPulseLoop(trigger);
            });
        }
    }

    private float GetTimeoutValue() => GetSettingValue<int>(HaptickleSetting.Timeout);

    public enum HaptickleSetting
    {
        HapticTriggers,
        Timeout,
    }

    public enum HaptickleParameter
    {
    }
}
