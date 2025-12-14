using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FuviiOSC.Haptickle.UI;
using Valve.VR;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.Parameters.Queryable;

namespace FuviiOSC.Haptickle;

[ModuleTitle("Haptickle")]
[ModuleDescription("Triggers Vive tracker haptics (if vibration motor is attached) or external haptic devices based on avatar parameters")]
[ModuleType(ModuleType.Generic)]
public class HaptickleModule : Module
{
    private readonly Dictionary<string, CancellationTokenSource> _pulseTokens = new();
    private readonly Dictionary<string, CancellationTokenSource> _externalPulseTokens = new();
    private readonly Dictionary<string, float> _lastFloatValues = new();
    private readonly Dictionary<string, float> _lastTriggerValues = new();
    private readonly Dictionary<string, float> _lastTriggerDeltas = new();
    private readonly Dictionary<string, DateTime> _triggerStartTimes = new();
    private readonly Dictionary<string, DateTime> _lastValueTimestamps = new();
    // Workaround: Track validity state for each trigger/parameter (bug with not keeping isValid when value conditions are met and maintained)
    private readonly Dictionary<string, bool> _parameterValidStates = new();

    private const ushort _MIN_HAPTIC_PULSE_DURATION = 20, _MAX_HAPTIC_PULSE_DURATION = 80, _DEFAULT_PULSE_INTERVAL = 32, _TRACKER_HAPTIC_AXIS_ID = 1, _DEFAULT_DELAY = 420;
    private const float _MIN_MARGIN = 0.02f, _VELOCITY_TIME_SCALAR = 0.1f;

    public CVRSystem? openVrSystem;

    [ModulePersistent("hapticTriggers")]
    public List<HapticTrigger> HapticTriggers { get; set; } = new();

    protected override void OnPreLoad()
    {
        CreateSlider(HaptickleSetting.Timeout, "Timeout (s)", "How many seconds until haptic loop breaks in case of error/disconnect", 4, 1, 10, 1);

        CreateCustomSetting(HaptickleSetting.ExternalDeviceList, new HaptickleModuleSetting());

        RegisterParameter<float>(HaptickleParameter.Enabled, "VRCOSC/Haptickle/Enabled", ParameterMode.Write, "Enabled", "True when at least one haptic device is configured/connected (either SteamVR or external one)");
        RegisterParameter<float>(HaptickleParameter.Triggered, "VRCOSC/Haptickle/Triggered", ParameterMode.Write, "Triggered", "True when when at least one device haptic conditions are met and feedback is being triggered");

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

        foreach (CancellationTokenSource pulseToken in _externalPulseTokens.Values)
            pulseToken.Cancel();
        _externalPulseTokens.Clear();

        try
        {
            List<DeviceMapping> externalDeviceMappings = GetExternalDevices();
            foreach (DeviceMapping mapping in externalDeviceMappings)
                HaptickleUtils.SendOscMessage(mapping.DeviceIp, mapping.DevicePort, mapping.DeviceOscPath, 0);
        }
        catch (Exception error)
        {
            LogDebug($"Error while stopping external devices: {error.Message}");
        }

        SendParameter(HaptickleParameter.Enabled, 0.0f);
        SendParameter(HaptickleParameter.Triggered, 0.0f);

        return Task.CompletedTask;
    }

    protected override void OnAnyParameterReceived(VRChatParameter receivedParameter)
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
                bool isValid = HaptickleUtils.EvaluateIsValid(trigger, result, receivedParameter, queryableParameter, wasValid);
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

        List<DeviceMapping> externalDeviceMappings = GetExternalDevices();
        foreach (DeviceMapping mapping in externalDeviceMappings)
        {
            if (receivedParameter.Name.Equals(mapping.Parameter))
            {
                DateTime now = DateTime.UtcNow;
                float value = receivedParameter.GetValue<float>();
                string key = $"{mapping.DeviceIp}:{mapping.Parameter}";
                // If the value wasn't updated for a timeout period, assume it's invalid
                bool wasValid = false, isValid = value > float.Epsilon;
                if (_lastValueTimestamps.TryGetValue(key, out DateTime lastTime))
                    wasValid = (DateTime.UtcNow - lastTime).TotalSeconds > GetTimeoutValue() ? false : true;

                switch (mapping.TriggerMode)
                {
                    case HapticTriggerMode.Off:
                        StopExternalPatternLoop(mapping);
                        HaptickleUtils.SendOscMessage(mapping.DeviceIp, mapping.DevicePort, mapping.DeviceOscPath, 0);
                        break;
                    case HapticTriggerMode.Constant:
                        if (value > float.Epsilon && !_externalPulseTokens.ContainsKey(mapping.DeviceIp))
                            StartExternalPatternLoop(mapping, key, mapping.TriggerMode);
                        else if (value <= float.Epsilon)
                        {
                            StopExternalPatternLoop(mapping);
                            HaptickleUtils.SendOscMessage(mapping.DeviceIp, mapping.DevicePort, mapping.DeviceOscPath, 0);
                        }
                        break;
                    case HapticTriggerMode.Proximity:
                        if (value > float.Epsilon && !_externalPulseTokens.ContainsKey(mapping.DeviceIp))
                            StartExternalPatternLoop(mapping, key, mapping.TriggerMode);
                        else if (value <= float.Epsilon)
                        {
                            StopExternalPatternLoop(mapping);
                            HaptickleUtils.SendOscMessage(mapping.DeviceIp, mapping.DevicePort, mapping.DeviceOscPath, 0);
                        }
                        break;
                    case HapticTriggerMode.OnChange:
                        if (isValid != wasValid)
                        {
                            StartExternalPatternLoop(mapping, key, mapping.TriggerMode);
                            Task.Run(async () =>
                            {
                                await Task.Delay(_DEFAULT_DELAY);
                                StopExternalPatternLoop(mapping);
                                HaptickleUtils.SendOscMessage(mapping.DeviceIp, mapping.DevicePort, mapping.DeviceOscPath, 0);
                            });
                        }
                        break;
                    case HapticTriggerMode.Velocity:
                        DateTime lastTimestamp = lastTime != null ? lastTime : now;
                        float lastValue = _lastTriggerValues.TryGetValue(key, out float v) ? v : value;
                        float deltaTime = (float)Math.Max((now - lastTimestamp).TotalSeconds, _VELOCITY_TIME_SCALAR);
                        float speed = mapping.PatternConfig?.Speed ?? 1.0f;
                        float velocity = Math.Clamp(Math.Abs(value - lastValue) / (deltaTime / speed), 0.0f, 1.0f);
                        bool hasMovedSignificantly = velocity > _MIN_MARGIN;

                        _lastTriggerDeltas[key] = velocity;
                        _triggerStartTimes[key] = now;

                        if (isValid && hasMovedSignificantly && !_externalPulseTokens.ContainsKey(mapping.DeviceIp))
                            StartExternalPatternLoop(mapping, key, mapping.TriggerMode);
                        else if (!hasMovedSignificantly)
                        {
                            StopExternalPatternLoop(mapping);
                            HaptickleUtils.SendOscMessage(mapping.DeviceIp, mapping.DevicePort, mapping.DeviceOscPath, 0);
                        }
                        break;
                }

                _lastTriggerValues[key] = value;
                _lastValueTimestamps[key] = now;
            }
        }
    }

    public void IdentifyDevice(HapticTrigger trigger) =>
        openVrSystem?.TriggerHapticPulse((uint)trigger.DeviceIndex, 0, 500); // 500ms triggers blinking on the tracker LED

    private void StartPulseLoop(HapticTrigger trigger, string? scalarKey = null)
    {
        string key = trigger.DeviceSerialNumber;
        StopPulseLoop(trigger); // ensure no duplicate tasks

        CancellationTokenSource pulseToken = new CancellationTokenSource();
        _pulseTokens[key] = pulseToken;

        UpdateActiveState();

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

                if (openVrSystem == null || patterned <= float.Epsilon)
                {
                    await Task.Delay(_DEFAULT_DELAY, pulseToken.Token);
                    continue;
                }

                ushort pulseDuration = (ushort)(_MIN_HAPTIC_PULSE_DURATION + (patterned * (_MAX_HAPTIC_PULSE_DURATION - _MIN_HAPTIC_PULSE_DURATION)));
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
        UpdateActiveState();
    }

    private void StartExternalPatternLoop(DeviceMapping mapping, string? key = null, HapticTriggerMode mode = HapticTriggerMode.Constant)
    {
        StopExternalPatternLoop(mapping);

        var tokenSource = new CancellationTokenSource();
        _externalPulseTokens[mapping.DeviceIp] = tokenSource;

        UpdateActiveState();

        Task.Run(async () =>
        {
            DateTime start = DateTime.UtcNow;
            float patternDuration = 1.0f;
            if (mapping.PatternConfig != null && mapping.PatternConfig.Speed > 0)
                patternDuration = Math.Max(0.1f, 1.0f / mapping.PatternConfig.Speed);

            while ((DateTime.UtcNow - start).TotalSeconds < patternDuration && !tokenSource.Token.IsCancellationRequested)
            {
                float value = 1.0f;
                float delta = 0.0f;
                float phase = 0.0f;

                if (key != null)
                {
                    _lastTriggerValues.TryGetValue(key, out value);
                    _lastTriggerDeltas.TryGetValue(key, out delta);

                    if (_triggerStartTimes.TryGetValue(key, out DateTime lol))
                        phase = (float)(DateTime.UtcNow - lol).TotalSeconds;

                    // Timeout to prevent infinite loops (e.g. if device is disconnected)
                    if (_lastValueTimestamps.TryGetValue(key, out DateTime lastTime) && (DateTime.UtcNow - lastTime).TotalSeconds > GetTimeoutValue())
                    {
                        HaptickleUtils.SendOscMessage(mapping.DeviceIp, mapping.DevicePort, mapping.DeviceOscPath, 0);
                        StopExternalPatternLoop(mapping);
                        break;
                    }
                }

                float patterned = 0.0f;
                switch (mode)
                {
                    case HapticTriggerMode.Off:
                        break;
                    case HapticTriggerMode.Constant:
                    case HapticTriggerMode.OnChange:
                        patterned = VibrationPattern.Apply(mapping.PatternConfig, 1.0f, 0.0f, phase);
                        break;
                    case HapticTriggerMode.Proximity:
                        patterned = VibrationPattern.Apply(mapping.PatternConfig, value, 0.0f, phase);
                        break;
                    case HapticTriggerMode.Velocity:
                        patterned = VibrationPattern.Apply(mapping.PatternConfig, delta, 0.0f, phase);
                        break;
                }

                int oscValue = (int)Math.Clamp(patterned * 255.0f, 0, 255);
                HaptickleUtils.SendOscMessage(mapping.DeviceIp, mapping.DevicePort, mapping.DeviceOscPath, oscValue);

                await Task.Delay(_DEFAULT_PULSE_INTERVAL, tokenSource.Token);
            }

            HaptickleUtils.SendOscMessage(mapping.DeviceIp, mapping.DevicePort, mapping.DeviceOscPath, 0);
            StopExternalPatternLoop(mapping);
        }, tokenSource.Token);
    }

    private void StopExternalPatternLoop(DeviceMapping mapping)
    {
        if (_externalPulseTokens.TryGetValue(mapping.DeviceIp, out var token))
        {
            token.Cancel();
            _externalPulseTokens.Remove(mapping.DeviceIp);
        }

        UpdateActiveState();
    }

    private void HandleConstant(HapticTrigger trigger, string key, bool isValid, bool wasValid, QueryResult result)
    {
        _lastTriggerValues[key] = 1.0f;
        _lastTriggerDeltas[key] = 0.0f;
        _triggerStartTimes.TryAdd(key, DateTime.UtcNow);

        if (result.JustBecameValid)
            StartPulseLoop(trigger, key);
        else if (!isValid && wasValid)
            StopPulseLoop(trigger);
    }

    private void HandleProximity(HapticTrigger trigger, string key, bool isValid, bool wasValid, VRChatParameter receivedParameter)
    {
        float proximityValue = receivedParameter.GetValue<float>();
        _lastTriggerValues[key] = proximityValue;
        _lastTriggerDeltas[key] = 0.0f;
        _triggerStartTimes.TryAdd(key, DateTime.UtcNow);

        if (isValid && receivedParameter.Type == ParameterType.Float && !_pulseTokens.ContainsKey(trigger.DeviceSerialNumber))
            StartPulseLoop(trigger, key);
        else if (!isValid && wasValid)
            StopPulseLoop(trigger);
    }

    private void HandleVelocity(HapticTrigger trigger, string key, bool isValid, bool wasValid, VRChatParameter receivedParameter)
    {
        float value = receivedParameter.GetValue<float>();
        float lastValue = _lastFloatValues.TryGetValue(key, out float v) ? v : value;
        DateTime now = DateTime.UtcNow;
        DateTime lastTimestamp = _lastValueTimestamps.TryGetValue(key, out DateTime t) ? t : now;
        float deltaTime = (float)Math.Max((now - lastTimestamp).TotalSeconds, _VELOCITY_TIME_SCALAR);
        float speed = trigger.PatternConfig?.Speed ?? 1.0f;
        float velocity = Math.Clamp(Math.Abs(value - lastValue) / (deltaTime / speed), 0.0f, 1.0f);
        bool hasMovedSignificantly = velocity > _MIN_MARGIN;

        _lastFloatValues[key] = value;
        _lastValueTimestamps[key] = now;
        _lastTriggerValues[key] = velocity;
        _lastTriggerDeltas[key] = velocity;
        _triggerStartTimes.TryAdd(key, DateTime.UtcNow);

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
        _lastTriggerValues[key] = 1.0f;
        _lastTriggerDeltas[key] = 0.0f;
        _triggerStartTimes.TryAdd(key, DateTime.UtcNow);

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

    public float GetTimeoutValue() => GetSettingValue<int>(HaptickleSetting.Timeout);
    public List<DeviceMapping> GetExternalDevices() => GetSettingValue<List<DeviceMapping>>(HaptickleSetting.ExternalDeviceList);

    private void UpdateActiveState()
    {
        bool triggered = _pulseTokens.Count > 0 || _externalPulseTokens.Count > 0;
        bool hasConfiguredDevice = (HapticTriggers != null && HapticTriggers.Count > 0);
        try
        {
            List<DeviceMapping> external = GetExternalDevices();
            if (external != null && external.Count > 0) hasConfiguredDevice = true;
        }
        catch (Exception error)
        {
            LogDebug($"Error while trying to read external devices state: {error.Message}");
        }

        SendParameter(HaptickleParameter.Enabled, hasConfiguredDevice ? 1.0f : 0.0f);
        SendParameter(HaptickleParameter.Triggered, triggered ? 1.0f : 0.0f);
    }

    public enum HaptickleSetting
    {
        Timeout,
        HapticTriggers,
        ExternalDeviceList,
    }

    public enum HaptickleParameter
    {
        Enabled = 0,
        Triggered = 1,
    }
}
