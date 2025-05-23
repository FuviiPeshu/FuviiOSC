using VRCOSC.App.SDK.Modules;

namespace FuviiOSC.AvatarChanger;

[ModuleTitle("Avatar Changer")]
[ModuleDescription("Handles avatar change via OSC")]
[ModuleType(ModuleType.Generic)]
public class AvatarChangerModule : Module
{
    protected override void OnPreLoad()
    {
        CreateState(AvatarChangerState.Default, "Default");
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
