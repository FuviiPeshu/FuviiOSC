using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace FuviiOSC.CamPanion;

[ModuleTitle("CamPanion")]
[ModuleDescription("Control VRChat camera via OSC.")]
[ModuleType(ModuleType.Generic)]
public class CamPanionModule : Module
{
    private const string CameraModePath = "/usercamera/Mode";
    private const string CameraPosePath = "/usercamera/Pose";
    private const string CameraClosePath = "/usercamera/Close";
    private const string CameraCapturePath = "/usercamera/Capture";
    private const string CameraCaptureDelayedPath = "/usercamera/CaptureDelayed";
    private const string ShowUIInCameraPath = "/usercamera/ShowUIInCamera";
    private const string LockPath = "/usercamera/Lock";
    private const string LocalPlayerPath = "/usercamera/LocalPlayer";
    private const string RemotePlayerPath = "/usercamera/RemotePlayer";
    private const string EnvironmentPath = "/usercamera/Environment";
    private const string GreenScreenPath = "/usercamera/GreenScreen";
    private const string SmoothMovementPath = "/usercamera/SmoothMovement";
    private const string LookAtMePath = "/usercamera/LookAtMe";
    private const string AutoLevelRollPath = "/usercamera/AutoLevelRoll";
    private const string AutoLevelPitchPath = "/usercamera/AutoLevelPitch";
    private const string FlyingPath = "/usercamera/Flying";
    private const string TriggerTakesPhotosPath = "/usercamera/TriggerTakesPhotos";
    private const string DollyPathsStayVisiblePath = "/usercamera/DollyPathsStayVisible";
    private const string CameraEarsPath = "/usercamera/CameraEars";
    private const string ShowFocusPath = "/usercamera/ShowFocus";
    private const string StreamingPath = "/usercamera/Streaming";
    private const string RollWhileFlyingPath = "/usercamera/RollWhileFlying";
    private const string OrientationIsLandscapePath = "/usercamera/OrientationIsLandscape";
    private const string ZoomPath = "/usercamera/Zoom";
    private const string ExposurePath = "/usercamera/Exposure";
    private const string FocalDistancePath = "/usercamera/FocalDistance";
    private const string AperturePath = "/usercamera/Aperture";
    private const string HuePath = "/usercamera/Hue";
    private const string SaturationPath = "/usercamera/Saturation";
    private const string LightnessPath = "/usercamera/Lightness";
    private const string LookAtMeXOffsetPath = "/usercamera/LookAtMeXOffset";
    private const string LookAtMeYOffsetPath = "/usercamera/LookAtMeYOffset";
    private const string FlySpeedPath = "/usercamera/FlySpeed";
    private const string TurnSpeedPath = "/usercamera/TurnSpeed";
    private const string SmoothingStrengthPath = "/usercamera/SmoothingStrength";
    private const string PhotoRatePath = "/usercamera/PhotoRate";
    private const string DurationPath = "/usercamera/Duration";

    [ModulePersistent("cameraTriggerPath")]
    public string CameraTriggerPath { get; set; } = "campanion/Trigger";

    protected override void OnPreLoad()
    {
        CreateTextBox(CamPanionSetting.CameraTriggerPath, "Camera Trigger Path", "OSC path to trigger camera spawn", CameraTriggerPath);
    }

    protected override Task<bool> OnModuleStart()
    {
        // Register official endpoints
        RegisterParameter<int>(CamPanionParameter.CameraMode, CameraModePath, ParameterMode.ReadWrite, "Camera Mode", "Set/Get camera mode");
        RegisterParameter<bool>(CamPanionParameter.CameraClose, CameraClosePath, ParameterMode.Write, "Camera Close", "Close camera");
        RegisterParameter<bool>(CamPanionParameter.CameraCapture, CameraCapturePath, ParameterMode.Write, "Camera Capture", "Take photo");
        RegisterParameter<bool>(CamPanionParameter.CameraCaptureDelayed, CameraCaptureDelayedPath, ParameterMode.Write, "Camera Capture Delayed", "Take timed photo");
        RegisterParameter<bool>(CamPanionParameter.ShowUIInCamera, ShowUIInCameraPath, ParameterMode.ReadWrite, "Show UI In Camera", "Toggle UI mask");
        RegisterParameter<bool>(CamPanionParameter.Lock, LockPath, ParameterMode.ReadWrite, "Lock", "Toggle lock");
        RegisterParameter<bool>(CamPanionParameter.LocalPlayer, LocalPlayerPath, ParameterMode.ReadWrite, "Local Player", "Toggle Local Player mask");
        RegisterParameter<bool>(CamPanionParameter.RemotePlayer, RemotePlayerPath, ParameterMode.ReadWrite, "Remote Player", "Toggle Remote Players mask");
        RegisterParameter<bool>(CamPanionParameter.Environment, EnvironmentPath, ParameterMode.ReadWrite, "Environment", "Toggle Environment mask");
        RegisterParameter<bool>(CamPanionParameter.GreenScreen, GreenScreenPath, ParameterMode.ReadWrite, "Green Screen", "Toggle greenscreen");
        RegisterParameter<bool>(CamPanionParameter.SmoothMovement, SmoothMovementPath, ParameterMode.ReadWrite, "Smooth Movement", "Toggle Smoothed behavior");
        RegisterParameter<bool>(CamPanionParameter.LookAtMe, LookAtMePath, ParameterMode.ReadWrite, "Look At Me", "Toggle Look-At-Me behaviour");
        RegisterParameter<bool>(CamPanionParameter.AutoLevelRoll, AutoLevelRollPath, ParameterMode.ReadWrite, "Auto Level Roll", "Toggle auto-level roll behavior");
        RegisterParameter<bool>(CamPanionParameter.AutoLevelPitch, AutoLevelPitchPath, ParameterMode.ReadWrite, "Auto Level Pitch", "Toggle auto-level pitch behavior");
        RegisterParameter<bool>(CamPanionParameter.Flying, FlyingPath, ParameterMode.ReadWrite, "Flying", "Toggle Flying");
        RegisterParameter<bool>(CamPanionParameter.TriggerTakesPhotos, TriggerTakesPhotosPath, ParameterMode.ReadWrite, "Trigger Takes Photos", "Toggle Trigger takes photos");
        RegisterParameter<bool>(CamPanionParameter.DollyPathsStayVisible, DollyPathsStayVisiblePath, ParameterMode.ReadWrite, "Dolly Paths Stay Visible", "Toggle dolly path stays visible while animating");
        RegisterParameter<bool>(CamPanionParameter.CameraEars, CameraEarsPath, ParameterMode.ReadWrite, "Camera Ears", "Toggle audio from camera");
        RegisterParameter<bool>(CamPanionParameter.ShowFocus, ShowFocusPath, ParameterMode.ReadWrite, "Show Focus", "Toggle focus overlay");
        RegisterParameter<bool>(CamPanionParameter.Streaming, StreamingPath, ParameterMode.ReadWrite, "Streaming", "Toggle spout stream");
        RegisterParameter<bool>(CamPanionParameter.RollWhileFlying, RollWhileFlyingPath, ParameterMode.ReadWrite, "Roll While Flying", "Toggle roll while flying behavior");
        RegisterParameter<bool>(CamPanionParameter.OrientationIsLandscape, OrientationIsLandscapePath, ParameterMode.ReadWrite, "Orientation Is Landscape", "Toggle orientation");
        RegisterParameter<float>(CamPanionParameter.Zoom, ZoomPath, ParameterMode.ReadWrite, "Zoom", "Set/Get zoom slider");
        RegisterParameter<float>(CamPanionParameter.Exposure, ExposurePath, ParameterMode.ReadWrite, "Exposure", "Set/Get exposure slider");
        RegisterParameter<float>(CamPanionParameter.FocalDistance, FocalDistancePath, ParameterMode.ReadWrite, "Focal Distance", "Set/Get focal distance slider");
        RegisterParameter<float>(CamPanionParameter.Aperture, AperturePath, ParameterMode.ReadWrite, "Aperture", "Set/Get aperture slider");
        RegisterParameter<float>(CamPanionParameter.Hue, HuePath, ParameterMode.ReadWrite, "Hue", "Set/Get greenscreen hue slider");
        RegisterParameter<float>(CamPanionParameter.Saturation, SaturationPath, ParameterMode.ReadWrite, "Saturation", "Set/Get greenscreen saturation slider");
        RegisterParameter<float>(CamPanionParameter.Lightness, LightnessPath, ParameterMode.ReadWrite, "Lightness", "Set/Get greenscreen lightness slider");
        RegisterParameter<float>(CamPanionParameter.LookAtMeXOffset, LookAtMeXOffsetPath, ParameterMode.ReadWrite, "Look At Me X Offset", "Set/Get LAM X offset slider");
        RegisterParameter<float>(CamPanionParameter.LookAtMeYOffset, LookAtMeYOffsetPath, ParameterMode.ReadWrite, "Look At Me Y Offset", "Set/Get LAM Y offset slider");
        RegisterParameter<float>(CamPanionParameter.FlySpeed, FlySpeedPath, ParameterMode.ReadWrite, "Fly Speed", "Set/Get fly speed slider");
        RegisterParameter<float>(CamPanionParameter.TurnSpeed, TurnSpeedPath, ParameterMode.ReadWrite, "Turn Speed", "Set/Get turn speed slider");
        RegisterParameter<float>(CamPanionParameter.SmoothingStrength, SmoothingStrengthPath, ParameterMode.ReadWrite, "Smoothing Strength", "Set/Get smoothing strength slider");
        RegisterParameter<float>(CamPanionParameter.PhotoRate, PhotoRatePath, ParameterMode.ReadWrite, "Photo Rate", "Set/Get dolly photo capture rate slider");
        RegisterParameter<float>(CamPanionParameter.Duration, DurationPath, ParameterMode.ReadWrite, "Duration", "Set/Get dolly duration slider");
        // Register user-customizable trigger
        RegisterParameter<bool>(CamPanionParameter.CameraTrigger, CameraTriggerPath, ParameterMode.Read, "Camera Trigger", "Trigger camera spawn");
        return Task.FromResult(true);
    }

    protected override void OnAnyParameterReceived(ReceivedParameter receivedParameter)
    {
        if (receivedParameter.Name == CameraTriggerPath && receivedParameter.GetValue<bool>())
        {
            SendParameterAndWait(CamPanionParameter.CameraMode, 1); // Example: set mode to Photo
        }
    }

    public enum CamPanionSetting
    {
        CameraTriggerPath = 0
    }

    public enum CamPanionParameter
    {
        CameraMode = 0,
        CameraClose = 1,
        CameraCapture = 2,
        CameraCaptureDelayed = 3,
        ShowUIInCamera = 4,
        Lock = 5,
        LocalPlayer = 6,
        RemotePlayer = 7,
        Environment = 8,
        GreenScreen = 9,
        SmoothMovement = 10,
        LookAtMe = 11,
        AutoLevelRoll = 12,
        AutoLevelPitch = 13,
        Flying = 14,
        TriggerTakesPhotos = 15,
        DollyPathsStayVisible = 16,
        CameraEars = 17,
        ShowFocus = 18,
        Streaming = 19,
        RollWhileFlying = 20,
        OrientationIsLandscape = 21,
        Zoom = 22,
        Exposure = 23,
        FocalDistance = 24,
        Aperture = 25,
        Hue = 26,
        Saturation = 27,
        Lightness = 28,
        LookAtMeXOffset = 29,
        LookAtMeYOffset = 30,
        FlySpeed = 31,
        TurnSpeed = 32,
        SmoothingStrength = 33,
        PhotoRate = 34,
        Duration = 35,
        CameraTrigger = 36
    }
}
