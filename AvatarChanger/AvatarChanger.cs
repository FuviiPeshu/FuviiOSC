using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FuviiOSC.AvatarChanger.UI;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;

namespace FuviiOSC.AvatarChanger;

[ModuleTitle("Avatar Changer")]
[ModuleDescription("Handles avatar change via OSC. Supports height change.")]
[ModuleType(ModuleType.Generic)]
public class AvatarChangerModule : Module
{
    private const float OSC_MIN_EYE_HEIGHT = 0.01f;
    private const float OSC_MAX_EYE_HEIGHT = 10000f;

    private bool _pendingScale;
    private AvatarScaleMode _pendingScaleMode;
    private float _pendingFixedHeight;
    private float _pendingMatchHeight;
    private DateTime _pendingScaleSince;
    private string _preChangeAvatarId = string.Empty;
    private float _lastSeenEyeHeight;
    private DateTime _eyeHeightStableSince;
    private ScalePhase _phase;
    private DateTime _appliedAt;
    private float _appliedHeight;
    private int _applyCount;

    internal bool IsAvatarHeightControlAvailable => GetClient().IsInAvatar && GetClient().Avatar?.EyeHeightScalingAllowed == true;
    internal float CurrentAvatarEyeHeight => GetClient().IsInAvatar ? GetClient().Avatar?.EyeHeight ?? 0f : 0f;
    internal float AvatarSliderMinimum => GetRuntimeHeightRange().Minimum;
    internal float AvatarSliderMaximum => GetRuntimeHeightRange().Maximum;
    internal float AvatarEyeHeightMin => OSC_MIN_EYE_HEIGHT;
    internal float AvatarEyeHeightMax => OSC_MAX_EYE_HEIGHT;
    internal bool UseLogarithmicHeightSlider => GetSettingValue<bool>(AvatarChangerSetting.LogarithmicHeightSlider);

    internal event Action? TriggersChanged;
    internal event Action? HeightPresetsChanged;

    protected override void OnPreLoad()
    {
        CreateCustomSetting(AvatarChangerSetting.AvatarChangerTriggerInstances, new AvatarChangerModuleSetting());
        CreateCustomSetting(AvatarChangerSetting.HeightPresets, new AvatarHeightPresetSetting(
            "Height presets", "Optional avatar height presets (in meters, VRC can freeze/crash if scaled above 1000m)", [0.32f, 1f, 10f]));

        CreateState(AvatarChangerState.Default, "Default");

        CreateTextBox(
            AvatarChangerSetting.HeightSliderMinimum, "Height slider minimum", "Min height slider bound (in meters, VRC can freeze/crash if scaled above 1000m)", 0.2f);
        CreateTextBox(
            AvatarChangerSetting.HeightSliderMaximum, "Height slider maximum", "Max height slider bound (in meters, VRC can freeze/crash if scaled above 1000m)", 5f);
        CreateToggle(
            AvatarChangerSetting.LogarithmicHeightSlider, "Logarithmic height slider", "Use logarithmic scale for the runtime view height slider", false);

        CreateGroup("Avatar Change Triggers", "", AvatarChangerSetting.AvatarChangerTriggerInstances);
        CreateGroup("Runtime Height Controls", "",
            AvatarChangerSetting.HeightSliderMinimum,
            AvatarChangerSetting.HeightSliderMaximum,
            AvatarChangerSetting.LogarithmicHeightSlider,
            AvatarChangerSetting.HeightPresets);

        SetRuntimeView(typeof(AvatarChangerRuntimeView));
    }

    protected override void OnPostLoad()
    {
        GetSetting(AvatarChangerSetting.AvatarChangerTriggerInstances).OnSettingChange += () => TriggersChanged?.Invoke();
        GetSetting(AvatarChangerSetting.HeightPresets).OnSettingChange += () => HeightPresetsChanged?.Invoke();
    }

    protected override Task<bool> OnModuleStart()
    {
        _pendingScale = false;

        ChangeState(AvatarChangerState.Default);
        return Task.FromResult(true);
    }

    protected override void OnAnyParameterReceived(VRChatParameter receivedParameter)
    {
        List<AvatarChangerTrigger> triggers = GetSettingValue<List<AvatarChangerTrigger>>(AvatarChangerSetting.AvatarChangerTriggerInstances);
        string paramName = receivedParameter.Name;

        foreach (AvatarChangerTrigger trigger in triggers)
        {
            foreach (TriggerQueryableParameter queryableParameter in trigger.TriggerParams)
            {
                if (queryableParameter.Name.Value != paramName) continue;

                VRCOSC.App.SDK.Parameters.Queryable.QueryResult result = queryableParameter.Evaluate(receivedParameter);
                if (result != null && result.JustBecameValid)
                {
                    TriggerAvatarChange(trigger);
                }
            }
        }
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, false, 256)]
    private void CheckPendingScale()
    {
        if (!_pendingScale) return;
        if (!GetClient().IsInAvatar) return;

        double elapsed = (DateTime.UtcNow - _pendingScaleSince).TotalSeconds;
        if (elapsed > 60)
        {
            _pendingScale = false;
            LogDebug($"Scale timeout after {elapsed:F1}s in phase {_phase}");
            return;
        }

        string currentAvatarId = GetClient().Avatar!.Id;
        float currentEyeHeight = GetClient().Avatar!.EyeHeight;
        bool scalingAllowed = GetClient().Avatar!.EyeHeightScalingAllowed;

        switch (_phase)
        {
            case ScalePhase.WaitingForNewAvatar:
                if (!string.IsNullOrEmpty(_preChangeAvatarId) && currentAvatarId == _preChangeAvatarId) return;

                _lastSeenEyeHeight = currentEyeHeight;
                _eyeHeightStableSince = DateTime.UtcNow;
                _phase = ScalePhase.WaitingForLoad;

                return;

            case ScalePhase.WaitingForLoad:
                if (Math.Abs(currentEyeHeight - _lastSeenEyeHeight) > 0.001f)
                {
                    _lastSeenEyeHeight = currentEyeHeight;
                    _eyeHeightStableSince = DateTime.UtcNow;
                    return;
                }

                if (!scalingAllowed)
                {
                    _pendingScale = false;
                    LogDebug("Scale skipped - world does not allow scaling");
                    return;
                }

                float targetHeight = ComputeTargetHeight();
                if (targetHeight < 0 || currentEyeHeight < OSC_MIN_EYE_HEIGHT)
                {
                    _pendingScale = false;
                    return;
                }

                GetClient().Avatar!.SetEyeHeight(targetHeight);
                _appliedAt = DateTime.UtcNow;
                _appliedHeight = targetHeight;
                _applyCount++;
                _phase = ScalePhase.Applied;

                return;

            case ScalePhase.Applied:
                double sinceSend = (DateTime.UtcNow - _appliedAt).TotalSeconds;
                if (sinceSend < 2) return;

                if (Math.Abs(currentEyeHeight - _appliedHeight) < 0.05f)
                {
                    _pendingScale = false;
                    LogDebug($"Scale success: eye height is {currentEyeHeight:F3}m (target was {_appliedHeight:F3}m, attempt #{_applyCount})");
                    return;
                }

                if (_applyCount >= 3)
                {
                    _pendingScale = false;
                    LogDebug($"Scale gave up: eye height is {currentEyeHeight:F3}m but wanted {_appliedHeight:F3}m after {_applyCount} attempts");
                    return;
                }

                _lastSeenEyeHeight = currentEyeHeight;
                _eyeHeightStableSince = DateTime.UtcNow;
                _phase = ScalePhase.WaitingForLoad;

                return;
        }
    }

    private float ComputeTargetHeight()
    {
        switch (_pendingScaleMode)
        {
            case AvatarScaleMode.MatchPrevious:
                if (_pendingMatchHeight < 0.01f)
                {
                    LogDebug("Scale skipped - no previous eye height known");
                    return -1f;
                }
                return Math.Clamp(_pendingMatchHeight, OSC_MIN_EYE_HEIGHT, OSC_MAX_EYE_HEIGHT);
            case AvatarScaleMode.FixedHeight:
                return Math.Clamp(_pendingFixedHeight, OSC_MIN_EYE_HEIGHT, OSC_MAX_EYE_HEIGHT);
            default:
                return -1f;
        }
    }

    internal List<AvatarChangerTrigger> GetTriggers()
    {
        return GetSettingValue<List<AvatarChangerTrigger>>(AvatarChangerSetting.AvatarChangerTriggerInstances);
    }

    internal List<float> AvatarHeightPresets => ParseHeightPresets(GetSettingValue<List<string>>(AvatarChangerSetting.HeightPresets));

    private static List<float> ParseHeightPresets(IEnumerable<string> values)
    {
        List<float> heights = [];

        foreach (string value in values)
        {
            if (!AvatarHeightPresetSetting.TryParseHeight(value, out float height)) continue;

            heights.Add(Math.Clamp(height, OSC_MIN_EYE_HEIGHT, OSC_MAX_EYE_HEIGHT));
        }

        return heights;
    }

    internal bool TrySetAvatarEyeHeight(float requestedHeight)
    {
        if (!GetClient().IsInAvatar) return false;

        Avatar? avatar = GetClient().Avatar;
        if (avatar is null || !avatar.EyeHeightScalingAllowed) return false;

        float minimum = OSC_MIN_EYE_HEIGHT;
        float maximum = OSC_MAX_EYE_HEIGHT;
        avatar.SetEyeHeight(Math.Clamp(requestedHeight, minimum, maximum));

        return true;
    }

    private HeightRange GetRuntimeHeightRange()
    {
        float minimum = Math.Clamp(GetSettingValue<float>(AvatarChangerSetting.HeightSliderMinimum), OSC_MIN_EYE_HEIGHT, OSC_MAX_EYE_HEIGHT);
        float maximum = Math.Clamp(GetSettingValue<float>(AvatarChangerSetting.HeightSliderMaximum), OSC_MIN_EYE_HEIGHT, OSC_MAX_EYE_HEIGHT);

        return (minimum <= maximum)
            ? new HeightRange(minimum, maximum)
            : new HeightRange(OSC_MIN_EYE_HEIGHT, OSC_MAX_EYE_HEIGHT);
    }

    internal void TriggerAvatarChange(AvatarChangerTrigger trigger)
    {
        AvatarScaleMode scaleMode = (AvatarScaleMode)trigger.ScaleMode.Value;
        string avatarId = trigger.AvatarId.Value;
        float fixedHeight = trigger.FixedEyeHeight.Value;
        float currentHeight = 0f;
        if (scaleMode == AvatarScaleMode.MatchPrevious && GetClient().IsInAvatar)
            currentHeight = GetClient().Avatar!.EyeHeight;

        if (scaleMode != AvatarScaleMode.None)
        {
            _preChangeAvatarId = GetClient().IsInAvatar ? GetClient().Avatar!.Id : string.Empty;
            _pendingScale = true;
            _pendingScaleMode = scaleMode;
            _pendingFixedHeight = fixedHeight;
            _pendingMatchHeight = currentHeight;
            _pendingScaleSince = DateTime.UtcNow;
            _lastSeenEyeHeight = 0f;
            _eyeHeightStableSince = DateTime.MinValue;
            _phase = ScalePhase.WaitingForNewAvatar;
            _applyCount = 0;
            LogDebug($"Scale queued (manual): mode={scaleMode}, matchH={currentHeight:F3}m, fixedH={fixedHeight:F3}m");
        }

        ChangeAvatar(avatarId);
    }

    private enum AvatarChangerSetting
    {
        AvatarChangerTriggerInstances,
        HeightSliderMinimum,
        HeightSliderMaximum,
        LogarithmicHeightSlider,
        HeightPresets
    }

    private enum AvatarChangerState
    {
        Default
    }

    private enum ScalePhase
    {
        WaitingForNewAvatar,
        WaitingForLoad,
        Applied,
    }

    private record struct HeightRange(float Minimum, float Maximum);
}
