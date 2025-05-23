using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;

namespace FuviiOSC.AvatarChanger;

[ModuleTitle("Avatar Changer")]
[ModuleDescription("Handles avatar change via OSC")]
[ModuleType(ModuleType.Generic)]
public class AvatarChangerModule : Module
{
    protected override void OnPreLoad()
    {
        CreateCustomSetting(AvatarChangerSetting.AvatarChangerTriggerInstances, new AvatarChangerModuleSetting());

        CreateState(AvatarChangerState.Default, "Default");

        CreateGroup("AvatarChangerTriggers", AvatarChangerSetting.AvatarChangerTriggerInstances);
    }

    protected override async Task<bool> OnModuleStart()
    {
        ChangeState(AvatarChangerState.Default);
        foreach (TriggerQueryableParameter? queryableParameter in GetSettingValue<List<AvatarChangerTrigger>>(AvatarChangerSetting.AvatarChangerTriggerInstances).SelectMany(trigger => trigger.Triggers))
        {
            await queryableParameter.Init();
        }

        return true;
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
