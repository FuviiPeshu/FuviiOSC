using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace FuviiOSC.AvatarChanger;

[ModuleTitle("Avatar Changer")]
[ModuleDescription("Handles avatar change via OSC (IMPORTANT NOTE: Works only for favourited avatars until VRChat fixes the bug)")]
[ModuleType(ModuleType.Generic)]
public class AvatarChangerModule : Module
{
    protected override void OnPreLoad()
    {
        CreateCustomSetting(AvatarChangerSetting.AvatarChangerTriggerInstances, new AvatarChangerModuleSetting());

        CreateState(AvatarChangerState.Default, "Default");

        CreateGroup("Avatar Change Triggers", AvatarChangerSetting.AvatarChangerTriggerInstances);
    }

    protected override Task<bool> OnModuleStart()
    {
        ChangeState(AvatarChangerState.Default);
        return Task.FromResult(true);
    }

    protected override void OnAnyParameterReceived(VRChatParameter receivedParameter)
    {
        List<AvatarChangerTrigger> triggers = GetSettingValue<List<AvatarChangerTrigger>>(AvatarChangerSetting.AvatarChangerTriggerInstances);
        foreach (AvatarChangerTrigger trigger in triggers)
        {
            foreach (TriggerQueryableParameter queryableParameter in trigger.TriggerParams.Where(param => param.Name.Value == receivedParameter.Name))
            {
                VRCOSC.App.SDK.Parameters.Queryable.QueryResult result = queryableParameter.Evaluate(receivedParameter);
                if (result != null && result.JustBecameValid)
                {
                    string avatarId = trigger.AvatarId.Value;
                    ChangeAvatar(avatarId);
                }
            }
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
