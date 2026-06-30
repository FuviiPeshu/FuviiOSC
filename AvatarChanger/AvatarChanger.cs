using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace FuviiOSC.AvatarChanger;

[ModuleTitle("Avatar Changer")]
[ModuleDescription("Handles avatar change via OSC. Supports height change.")]
[ModuleType(ModuleType.Generic)]
public class AvatarChangerModule : Module
{
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

    private enum ScalePhase
    {
        WaitingForNewAvatar,
        WaitingForLoad,
        Applied,
    }

    protected override void OnPreLoad()
    {
        CreateCustomSetting(AvatarChangerSetting.AvatarChangerTriggerInstances, new AvatarChangerModuleSetting());

        CreateState(AvatarChangerState.Default, "Default");

        CreateGroup("Avatar Change Triggers", "", AvatarChangerSetting.AvatarChangerTriggerInstances);
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
                    AvatarScaleMode scaleMode = (AvatarScaleMode)trigger.ScaleMode.Value;
                    string avatarId = trigger.AvatarId.Value;
                    float fixedHeight = trigger.FixedEyeHeight.Value;
                    float currentHeight = 0f;
                    if (scaleMode == AvatarScaleMode.MatchPrevious && GetClient().IsInAvatar)
                    {
                        currentHeight = GetClient().Avatar!.EyeHeight;
                    }

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
                        LogDebug($"Scale queued: mode={scaleMode}, matchH={currentHeight:F3}m, fixedH={fixedHeight:F3}m, prevAvatar={_preChangeAvatarId}");
                    }

                    ChangeAvatar(avatarId);
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
                if (targetHeight < 0 || currentEyeHeight < 0.01f)
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
                return Math.Clamp(_pendingMatchHeight, 0.1f, 10000f);
            case AvatarScaleMode.FixedHeight:
                return Math.Clamp(_pendingFixedHeight, 0.1f, 10000f);
            default:
                return -1f;
        }
    }

    private enum AvatarChangerSetting
    {
        AvatarChangerTriggerInstances
    }

    private enum AvatarChangerState
    {
        Default
    }
}
