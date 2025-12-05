//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using VRCOSC.App.SDK.Modules;
//using VRCOSC.App.SDK.Parameters;

//namespace FuviiOSC.CamPanion;

//[ModuleTitle("CamPanion")]
//[ModuleDescription("Control VRChat camera via OSC.")]
//[ModuleType(ModuleType.Generic)]
//public class CamPanionModule : Module
//{
//    // Map parameter enum to VRChat OSC endpoint
//    private static readonly Dictionary<CamPanionParameter, string> VrcOscEndpoints = new()
//    {
//        { CamPanionParameter.CameraMode, "/usercamera/Mode" },
//        { CamPanionParameter.CameraClose, "/usercamera/Close" },
//        { CamPanionParameter.CameraCapture, "/usercamera/Capture" },
//        { CamPanionParameter.CameraCaptureDelayed, "/usercamera/CaptureDelayed" },
//        { CamPanionParameter.ShowUIInCamera, "/usercamera/ShowUIInCamera" },
//        { CamPanionParameter.Lock, "/usercamera/Lock" },
//        { CamPanionParameter.LocalPlayer, "/usercamera/LocalPlayer" },
//        { CamPanionParameter.RemotePlayer, "/usercamera/RemotePlayer" },
//        { CamPanionParameter.Environment, "/usercamera/Environment" },
//        { CamPanionParameter.GreenScreen, "/usercamera/GreenScreen" },
//        { CamPanionParameter.SmoothMovement, "/usercamera/SmoothMovement" },
//        { CamPanionParameter.LookAtMe, "/usercamera/LookAtMe" },
//        { CamPanionParameter.AutoLevelRoll, "/usercamera/AutoLevelRoll" },
//        { CamPanionParameter.AutoLevelPitch, "/usercamera/AutoLevelPitch" },
//        { CamPanionParameter.Flying, "/usercamera/Flying" },
//        { CamPanionParameter.TriggerTakesPhotos, "/usercamera/TriggerTakesPhotos" },
//        { CamPanionParameter.DollyPathsStayVisible, "/usercamera/DollyPathsStayVisible" },
//        { CamPanionParameter.CameraEars, "/usercamera/CameraEars" },
//        { CamPanionParameter.ShowFocus, "/usercamera/ShowFocus" },
//        { CamPanionParameter.Streaming, "/usercamera/Streaming" },
//        { CamPanionParameter.RollWhileFlying, "/usercamera/RollWhileFlying" },
//        { CamPanionParameter.OrientationIsLandscape, "/usercamera/OrientationIsLandscape" },
//        { CamPanionParameter.Zoom, "/usercamera/Zoom" },
//        { CamPanionParameter.Exposure, "/usercamera/Exposure" },
//        { CamPanionParameter.FocalDistance, "/usercamera/FocalDistance" },
//        { CamPanionParameter.Aperture, "/usercamera/Aperture" },
//        { CamPanionParameter.Hue, "/usercamera/Hue" },
//        { CamPanionParameter.Saturation, "/usercamera/Saturation" },
//        { CamPanionParameter.Lightness, "/usercamera/Lightness" },
//        { CamPanionParameter.LookAtMeXOffset, "/usercamera/LookAtMeXOffset" },
//        { CamPanionParameter.LookAtMeYOffset, "/usercamera/LookAtMeYOffset" },
//        { CamPanionParameter.FlySpeed, "/usercamera/FlySpeed" },
//        { CamPanionParameter.TurnSpeed, "/usercamera/TurnSpeed" },
//        { CamPanionParameter.SmoothingStrength, "/usercamera/SmoothingStrength" },
//        { CamPanionParameter.PhotoRate, "/usercamera/PhotoRate" },
//        { CamPanionParameter.Duration, "/usercamera/Duration" }
//    };

//    // Map registered parameter names to CamPanionParameter
//    private Dictionary<string, CamPanionParameter> _nameToParameter = new();

//    protected override void OnPreLoad()
//    {
//        // No settings needed
//    }

//    protected override Task<bool> OnModuleStart()
//    {
//        // Register all parameters to listen for and build name-to-enum map
//        RegisterParameter<int>(CamPanionParameter.CameraMode, "VRCOSC/CamPanion/Mode", ParameterMode.Read, "Camera Mode", "Set camera mode");
//        _nameToParameter["VRCOSC/CamPanion/Mode"] = CamPanionParameter.CameraMode;
//        RegisterParameter<bool>(CamPanionParameter.CameraClose, "VRCOSC/CamPanion/Close", ParameterMode.Read, "Camera Close", "Close camera");
//        _nameToParameter["VRCOSC/CamPanion/Close"] = CamPanionParameter.CameraClose;
//        RegisterParameter<bool>(CamPanionParameter.CameraCapture, "VRCOSC/CamPanion/Capture", ParameterMode.Read, "Camera Capture", "Take photo");
//        _nameToParameter["VRCOSC/CamPanion/Capture"] = CamPanionParameter.CameraCapture;
//        RegisterParameter<bool>(CamPanionParameter.CameraCaptureDelayed, "VRCOSC/CamPanion/CaptureDelayed", ParameterMode.Read, "Camera Capture Delayed", "Take timed photo");
//        _nameToParameter["VRCOSC/CamPanion/CaptureDelayed"] = CamPanionParameter.CameraCaptureDelayed;
//        RegisterParameter<bool>(CamPanionParameter.ShowUIInCamera, "VRCOSC/CamPanion/ShowUIInCamera", ParameterMode.Read, "Show UI In Camera", "Toggle UI mask");
//        _nameToParameter["VRCOSC/CamPanion/ShowUIInCamera"] = CamPanionParameter.ShowUIInCamera;
//        RegisterParameter<bool>(CamPanionParameter.Lock, "VRCOSC/CamPanion/Lock", ParameterMode.Read, "Lock", "Toggle lock");
//        _nameToParameter["VRCOSC/CamPanion/Lock"] = CamPanionParameter.Lock;
//        RegisterParameter<bool>(CamPanionParameter.LocalPlayer, "VRCOSC/CamPanion/LocalPlayer", ParameterMode.Read, "Local Player", "Toggle Local Player mask");
//        _nameToParameter["VRCOSC/CamPanion/LocalPlayer"] = CamPanionParameter.LocalPlayer;
//        RegisterParameter<bool>(CamPanionParameter.RemotePlayer, "VRCOSC/CamPanion/RemotePlayer", ParameterMode.Read, "Remote Player", "Toggle Remote Players mask");
//        _nameToParameter["VRCOSC/CamPanion/RemotePlayer"] = CamPanionParameter.RemotePlayer;
//        RegisterParameter<bool>(CamPanionParameter.Environment, "VRCOSC/CamPanion/Environment", ParameterMode.Read, "Environment", "Toggle Environment mask");
//        _nameToParameter["VRCOSC/CamPanion/Environment"] = CamPanionParameter.Environment;
//        RegisterParameter<bool>(CamPanionParameter.GreenScreen, "VRCOSC/CamPanion/GreenScreen", ParameterMode.Read, "Green Screen", "Toggle greenscreen");
//        _nameToParameter["VRCOSC/CamPanion/GreenScreen"] = CamPanionParameter.GreenScreen;
//        RegisterParameter<bool>(CamPanionParameter.SmoothMovement, "VRCOSC/CamPanion/SmoothMovement", ParameterMode.Read, "Smooth Movement", "Toggle Smoothed behavior");
//        _nameToParameter["VRCOSC/CamPanion/SmoothMovement"] = CamPanionParameter.SmoothMovement;
//        RegisterParameter<bool>(CamPanionParameter.LookAtMe, "VRCOSC/CamPanion/LookAtMe", ParameterMode.Read, "Look At Me", "Toggle Look-At-Me behaviour");
//        _nameToParameter["VRCOSC/CamPanion/LookAtMe"] = CamPanionParameter.LookAtMe;
//        RegisterParameter<bool>(CamPanionParameter.AutoLevelRoll, "VRCOSC/CamPanion/AutoLevelRoll", ParameterMode.Read, "Auto Level Roll", "Toggle auto-level roll behavior");
//        _nameToParameter["VRCOSC/CamPanion/AutoLevelRoll"] = CamPanionParameter.AutoLevelRoll;
//        RegisterParameter<bool>(CamPanionParameter.AutoLevelPitch, "VRCOSC/CamPanion/AutoLevelPitch", ParameterMode.Read, "Auto Level Pitch", "Toggle auto-level pitch behavior");
//        _nameToParameter["VRCOSC/CamPanion/AutoLevelPitch"] = CamPanionParameter.AutoLevelPitch;
//        RegisterParameter<bool>(CamPanionParameter.Flying, "VRCOSC/CamPanion/Flying", ParameterMode.Read, "Flying", "Toggle Flying");
//        _nameToParameter["VRCOSC/CamPanion/Flying"] = CamPanionParameter.Flying;
//        RegisterParameter<bool>(CamPanionParameter.TriggerTakesPhotos, "VRCOSC/CamPanion/TriggerTakesPhotos", ParameterMode.Read, "Trigger Takes Photos", "Toggle Trigger takes photos");
//        _nameToParameter["VRCOSC/CamPanion/TriggerTakesPhotos"] = CamPanionParameter.TriggerTakesPhotos;
//        RegisterParameter<bool>(CamPanionParameter.DollyPathsStayVisible, "VRCOSC/CamPanion/DollyPathsStayVisible", ParameterMode.Read, "Dolly Paths Stay Visible", "Toggle dolly path stays visible while animating");
//        _nameToParameter["VRCOSC/CamPanion/DollyPathsStayVisible"] = CamPanionParameter.DollyPathsStayVisible;
//        RegisterParameter<bool>(CamPanionParameter.CameraEars, "VRCOSC/CamPanion/CameraEars", ParameterMode.Read, "Camera Ears", "Toggle audio from camera");
//        _nameToParameter["VRCOSC/CamPanion/CameraEars"] = CamPanionParameter.CameraEars;
//        RegisterParameter<bool>(CamPanionParameter.ShowFocus, "VRCOSC/CamPanion/ShowFocus", ParameterMode.Read, "Show Focus", "Toggle focus overlay");
//        _nameToParameter["VRCOSC/CamPanion/ShowFocus"] = CamPanionParameter.ShowFocus;
//        RegisterParameter<bool>(CamPanionParameter.Streaming, "VRCOSC/CamPanion/Streaming", ParameterMode.Read, "Streaming", "Toggle spout stream");
//        _nameToParameter["VRCOSC/CamPanion/Streaming"] = CamPanionParameter.Streaming;
//        RegisterParameter<bool>(CamPanionParameter.RollWhileFlying, "VRCOSC/CamPanion/RollWhileFlying", ParameterMode.Read, "Roll While Flying", "Toggle roll while flying behavior");
//        _nameToParameter["VRCOSC/CamPanion/RollWhileFlying"] = CamPanionParameter.RollWhileFlying;
//        RegisterParameter<bool>(CamPanionParameter.OrientationIsLandscape, "VRCOSC/CamPanion/OrientationIsLandscape", ParameterMode.Read, "Orientation Is Landscape", "Toggle orientation");
//        _nameToParameter["VRCOSC/CamPanion/OrientationIsLandscape"] = CamPanionParameter.OrientationIsLandscape;
//        RegisterParameter<float>(CamPanionParameter.Zoom, "VRCOSC/CamPanion/Zoom", ParameterMode.Read, "Zoom", "Set/Get zoom slider");
//        _nameToParameter["VRCOSC/CamPanion/Zoom"] = CamPanionParameter.Zoom;
//        RegisterParameter<float>(CamPanionParameter.Exposure, "VRCOSC/CamPanion/Exposure", ParameterMode.Read, "Exposure", "Set/Get exposure slider");
//        _nameToParameter["VRCOSC/CamPanion/Exposure"] = CamPanionParameter.Exposure;
//        RegisterParameter<float>(CamPanionParameter.FocalDistance, "VRCOSC/CamPanion/FocalDistance", ParameterMode.Read, "Focal Distance", "Set/Get focal distance slider");
//        _nameToParameter["VRCOSC/CamPanion/FocalDistance"] = CamPanionParameter.FocalDistance;
//        RegisterParameter<float>(CamPanionParameter.Aperture, "VRCOSC/CamPanion/Aperture", ParameterMode.Read, "Aperture", "Set/Get aperture slider");
//        _nameToParameter["VRCOSC/CamPanion/Aperture"] = CamPanionParameter.Aperture;
//        RegisterParameter<float>(CamPanionParameter.Hue, "VRCOSC/CamPanion/Hue", ParameterMode.Read, "Hue", "Set/Get greenscreen hue slider");
//        _nameToParameter["VRCOSC/CamPanion/Hue"] = CamPanionParameter.Hue;
//        RegisterParameter<float>(CamPanionParameter.Saturation, "VRCOSC/CamPanion/Saturation", ParameterMode.Read, "Saturation", "Set/Get greenscreen saturation slider");
//        _nameToParameter["VRCOSC/CamPanion/Saturation"] = CamPanionParameter.Saturation;
//        RegisterParameter<float>(CamPanionParameter.Lightness, "VRCOSC/CamPanion/Lightness", ParameterMode.Read, "Lightness", "Set/Get greenscreen lightness slider");
//        _nameToParameter["VRCOSC/CamPanion/Lightness"] = CamPanionParameter.Lightness;
//        RegisterParameter<float>(CamPanionParameter.LookAtMeXOffset, "VRCOSC/CamPanion/LookAtMeXOffset", ParameterMode.Read, "Look At Me X Offset", "Set/Get LAM X offset slider");
//        _nameToParameter["VRCOSC/CamPanion/LookAtMeXOffset"] = CamPanionParameter.LookAtMeXOffset;
//        RegisterParameter<float>(CamPanionParameter.LookAtMeYOffset, "VRCOSC/CamPanion/LookAtMeYOffset", ParameterMode.Read, "Look At Me Y Offset", "Set/Get LAM Y offset slider");
//        _nameToParameter["VRCOSC/CamPanion/LookAtMeYOffset"] = CamPanionParameter.LookAtMeYOffset;
//        RegisterParameter<float>(CamPanionParameter.FlySpeed, "VRCOSC/CamPanion/FlySpeed", ParameterMode.Read, "Fly Speed", "Set/Get fly speed slider");
//        _nameToParameter["VRCOSC/CamPanion/FlySpeed"] = CamPanionParameter.FlySpeed;
//        RegisterParameter<float>(CamPanionParameter.TurnSpeed, "VRCOSC/CamPanion/TurnSpeed", ParameterMode.Read, "Turn Speed", "Set/Get turn speed slider");
//        _nameToParameter["VRCOSC/CamPanion/TurnSpeed"] = CamPanionParameter.TurnSpeed;
//        RegisterParameter<float>(CamPanionParameter.SmoothingStrength, "VRCOSC/CamPanion/SmoothingStrength", ParameterMode.Read, "Smoothing Strength", "Set/Get smoothing strength slider");
//        _nameToParameter["VRCOSC/CamPanion/SmoothingStrength"] = CamPanionParameter.SmoothingStrength;
//        RegisterParameter<float>(CamPanionParameter.PhotoRate, "VRCOSC/CamPanion/PhotoRate", ParameterMode.Read, "Photo Rate", "Set/Get dolly photo capture rate slider");
//        _nameToParameter["VRCOSC/CamPanion/PhotoRate"] = CamPanionParameter.PhotoRate;
//        RegisterParameter<float>(CamPanionParameter.Duration, "VRCOSC/CamPanion/Duration", ParameterMode.Read, "Duration", "Set/Get dolly duration slider");
//        _nameToParameter["VRCOSC/CamPanion/Duration"] = CamPanionParameter.Duration;

//        return Task.FromResult(true);
//    }

//    protected override void OnAnyParameterReceived(VRChatParameter receivedParameter)
//    {
//        // Lookup the parameter by its actual registered name
//        if (_nameToParameter.TryGetValue(receivedParameter.Name, out var param) && VrcOscEndpoints.TryGetValue(receivedParameter.Name, out var endpoint))
//        {
//            switch (receivedParameter.Type)
//            {
//                case ParameterType.Bool:
//                    SendParameterAndWait(param, receivedParameter.GetValue<bool>());
//                    break;
//                case ParameterType.Int:
//                    SendParameterAndWait(param, receivedParameter.GetValue<int>());
//                    break;
//                case ParameterType.Float:
//                    SendParameterAndWait(param, receivedParameter.GetValue<float>());
//                    break;
//            }
//        }
//    }

//    public enum CamPanionParameter
//    {
//        CameraMode,
//        CameraClose,
//        CameraCapture,
//        CameraCaptureDelayed,
//        ShowUIInCamera,
//        Lock,
//        LocalPlayer,
//        RemotePlayer,
//        Environment,
//        GreenScreen,
//        SmoothMovement,
//        LookAtMe,
//        AutoLevelRoll,
//        AutoLevelPitch,
//        Flying,
//        TriggerTakesPhotos,
//        DollyPathsStayVisible,
//        CameraEars,
//        ShowFocus,
//        Streaming,
//        RollWhileFlying,
//        OrientationIsLandscape,
//        Zoom,
//        Exposure,
//        FocalDistance,
//        Aperture,
//        Hue,
//        Saturation,
//        Lightness,
//        LookAtMeXOffset,
//        LookAtMeYOffset,
//        FlySpeed,
//        TurnSpeed,
//        SmoothingStrength,
//        PhotoRate,
//        Duration
//    }
//}
