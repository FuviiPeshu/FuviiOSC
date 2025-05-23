using VRCOSC.App.UI.Core;

namespace FuviiOSC.AvatarChanger.UI;

public partial class AvatarChangerModuleSettingView
{
    private readonly AvatarChangerModuleSetting moduleSetting;
    private WindowManager windowManager = null!;
    private AvatarChangerModule instance;

    public AvatarChangerModuleSettingView(AvatarChangerModule instance, AvatarChangerModuleSetting moduleSetting)
    {
        this.instance = instance;
        this.moduleSetting = moduleSetting;
    }
}