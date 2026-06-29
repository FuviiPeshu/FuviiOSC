using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Valve.VR;
using VRCOSC.App.OpenVR.Device;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using FuviiOSC.PawTracking.UI;

namespace FuviiOSC.PawTracking;

[ModuleTitle("Paw Tracking")]
[ModuleDescription(
    "\t\t --- Note: trackers MUST be assigned with a corresponding 'role' in SteamVR Input settings; controllers must support SteamVR skeleton input.\n" +
    "Detects hand gestures, tracker button presses, steamVR controller skeleton data for finger tracking. Provides configurable thresholds and options for gesture detection and input forwarding."
)]
[ModuleType(ModuleType.SteamVR)]
public class PawTrackingModule : Module
{
    private GestureResult _lastLeftGesture = new(VRCGesture.Neutral, 0f, VRCGesture.Neutral);
    private GestureResult _lastRightGesture = new(VRCGesture.Neutral, 0f, VRCGesture.Neutral);
    private List<TrackerButtonState> _trackerButtonStates = new();
    private bool _trackerActionsReady;
    private float _leftGripForce;
    private float _rightGripForce;
    private int _consecutiveOpenVRFailures;

    public GestureResult LastLeftGesture => _lastLeftGesture;
    public GestureResult LastRightGesture => _lastRightGesture;
    public IReadOnlyList<TrackerButtonState> TrackerButtonStates => _trackerButtonStates;
    public float LeftGripForce => _leftGripForce;
    public float RightGripForce => _rightGripForce;
    public bool ControllerInputsEnabled { get; private set; } = true;
    public bool TrackerButtonsEnabled { get; private set; } = true;
    public IReadOnlyDictionary<string, float> TrackerBatteryLevels => _trackerBatteryLevels;
    public int ConnectedTrackerCount => _knownTrackerIndexes.Count;

    private static readonly Dictionary<DeviceRole, PawTrackingParam> TrackerParamMap = new()
    {
        [DeviceRole.Chest] = PawTrackingParam.TrackerChestButton,
        [DeviceRole.Waist] = PawTrackingParam.TrackerWaistButton,
        [DeviceRole.LeftFoot] = PawTrackingParam.TrackerLeftFootButton,
        [DeviceRole.RightFoot] = PawTrackingParam.TrackerRightFootButton,
        [DeviceRole.LeftKnee] = PawTrackingParam.TrackerLeftKneeButton,
        [DeviceRole.RightKnee] = PawTrackingParam.TrackerRightKneeButton,
        [DeviceRole.LeftElbow] = PawTrackingParam.TrackerLeftElbowButton,
        [DeviceRole.RightElbow] = PawTrackingParam.TrackerRightElbowButton,
    };

    private readonly Dictionary<PawTrackingParam, ulong> _trackerActionHandles = new();

    private static readonly uint inputdigitalactiondata_t_size = (uint)Unsafe.SizeOf<InputDigitalActionData_t>();
    private static readonly uint inputanalogactiondata_t_size = (uint)Unsafe.SizeOf<InputAnalogActionData_t>();

    private ulong _leftGripForceHandle;
    private ulong _rightGripForceHandle;
    private bool _gripForceReady;

    private HashSet<uint> _knownTrackerIndexes = new();
    private DateTime _lastDeviceScan = DateTime.MinValue;
    private Dictionary<string, float> _trackerBatteryLevels = new();

    private static readonly TrackerDef[] TrackerDefs =
    [
        new("/actions/main/in/tracker_chest_button", "vive_tracker_chest", "/user/chest/input/power", PawTrackingParam.TrackerChestButton, "Chest"),
        new("/actions/main/in/tracker_waist_button", "vive_tracker_waist", "/user/waist/input/power", PawTrackingParam.TrackerWaistButton, "Waist"),
        new("/actions/main/in/tracker_leftfoot_button", "vive_tracker_left_foot", "/user/foot/left/input/power", PawTrackingParam.TrackerLeftFootButton, "LeftFoot"),
        new("/actions/main/in/tracker_rightfoot_button", "vive_tracker_right_foot", "/user/foot/right/input/power", PawTrackingParam.TrackerRightFootButton, "RightFoot"),
        new("/actions/main/in/tracker_leftknee_button", "vive_tracker_left_knee", "/user/knee/left/input/power", PawTrackingParam.TrackerLeftKneeButton, "LeftKnee"),
        new("/actions/main/in/tracker_rightknee_button", "vive_tracker_right_knee", "/user/knee/right/input/power", PawTrackingParam.TrackerRightKneeButton, "RightKnee"),
        new("/actions/main/in/tracker_leftelbow_button", "vive_tracker_left_elbow", "/user/elbow/left/input/power", PawTrackingParam.TrackerLeftElbowButton, "LeftElbow"),
        new("/actions/main/in/tracker_rightelbow_button", "vive_tracker_right_elbow", "/user/elbow/right/input/power", PawTrackingParam.TrackerRightElbowButton, "RightElbow"),
        new("/actions/main/in/tracker_leftshoulder_button", "vive_tracker_left_shoulder", "/user/shoulder/left/input/power", PawTrackingParam.TrackerLeftShoulderButton, "LeftShoulder"),
        new("/actions/main/in/tracker_rightshoulder_button", "vive_tracker_right_shoulder", "/user/shoulder/right/input/power", PawTrackingParam.TrackerRightShoulderButton, "RightShoulder"),
        new("/actions/main/in/tracker_leftwrist_button", "vive_tracker_left_wrist", "/user/wrist/left/input/power", PawTrackingParam.TrackerLeftWristButton, "LeftWrist"),
        new("/actions/main/in/tracker_rightwrist_button", "vive_tracker_right_wrist", "/user/wrist/right/input/power", PawTrackingParam.TrackerRightWristButton, "RightWrist"),
        new("/actions/main/in/tracker_leftankle_button", "vive_tracker_left_ankle", "/user/ankle/left/input/power", PawTrackingParam.TrackerLeftAnkleButton, "LeftAnkle"),
        new("/actions/main/in/tracker_rightankle_button", "vive_tracker_right_ankle", "/user/ankle/right/input/power", PawTrackingParam.TrackerRightAnkleButton, "RightAnkle"),
        new("/actions/main/in/tracker_camera_button", "vive_tracker_camera", "/user/camera/input/power", PawTrackingParam.TrackerCameraButton, "Camera"),
    ];

    protected override void OnPreLoad()
    {
        CreateSlider(PawTrackingSetting.FingerDownThreshold, "Finger Down Threshold",
            "Global threshold for a finger to be considered curled/down. 0=fully open, 1=fully closed.", 0.55f, 0.1f, 0.95f, 0.01f);
        CreateSlider(PawTrackingSetting.FingerUpThreshold, "Finger Up Threshold",
            "Global threshold for a finger to be considered extended/up. Below this = open.", 0.35f, 0.05f, 0.9f, 0.01f);
        CreateSlider(PawTrackingSetting.GestureChangeThreshold, "Gesture Change Threshold",
            "Minimum confidence improvement required to switch to a new gesture. Prevents flickering.", 0.08f, 0.0f, 0.5f, 0.01f);
        CreateSlider(PawTrackingSetting.MinGestureWeight, "Minimum Gesture Weight",
            "Minimum weight (confidence) required to output a gesture. Below this, Neutral is sent.", 0.3f, 0.0f, 0.9f, 0.01f);
        CreateSlider(PawTrackingSetting.FistGripThreshold, "Fist Grip Threshold",
            "Minimum grip squeeze (0-1) required to register Fist or ThumbsUp. Prevents accidental fist when just resting fingers.", 0.4f, 0.0f, 1.0f, 0.01f);
        CreateToggle(PawTrackingSetting.EnableControllerInputs, "Enable Controller Inputs",
            "Forward trigger, grip, buttons, stick, and trackpad data as OSC parameters.", true);
        CreateToggle(PawTrackingSetting.EnableTrackerButtons, "Enable Tracker Buttons",
            "Read tracker button presses via legacy OpenVR API. Sends bool per tracker role (Chest, Waist, Feet, Knees, Elbows).", true);
        CreateToggle(PawTrackingSetting.UseGripForce, "Use Grip Force for Gesture Weight",
            "When enabled, uses grip force (hard squeeze, Index controllers only) instead of grip pull for gesture weight.", false);
        CreateToggle(PawTrackingSetting.DebugLogging, "Debug Logging",
            "Log gesture changes and finger values for troubleshooting.", false);

        RegisterParameter<int>(PawTrackingParam.GestureLeft, "GestureLeft", ParameterMode.Write,
            "Gesture Left", "VRChat gesture ID for the left hand (0-7)");
        RegisterParameter<int>(PawTrackingParam.GestureRight, "GestureRight", ParameterMode.Write,
            "Gesture Right", "VRChat gesture ID for the right hand (0-7)");
        RegisterParameter<float>(PawTrackingParam.GestureLeftWeight, "GestureLeftWeight", ParameterMode.Write,
            "Gesture Left Weight", "Grip strength for the left gesture weight (0-1)");
        RegisterParameter<float>(PawTrackingParam.GestureRightWeight, "GestureRightWeight", ParameterMode.Write,
            "Gesture Right Weight", "Grip strength for the right gesture weight (0-1)");
        RegisterParameter<float>(PawTrackingParam.GestureLeftConfidence, "VRCOSC/PawTracking/GestureLeftConfidence", ParameterMode.Write,
            "Gesture Left Confidence", "How well the left hand matches the detected gesture (0-1)");
        RegisterParameter<float>(PawTrackingParam.GestureRightConfidence, "VRCOSC/PawTracking/GestureRightConfidence", ParameterMode.Write,
            "Gesture Right Confidence", "How well the right hand matches the detected gesture (0-1)");

        RegisterParameter<float>(PawTrackingParam.LeftIndex, "VRCOSC/PawTracking/LeftIndex", ParameterMode.Write,
            "Left Index Curl", "Raw index finger curl (0=open, 1=closed)");
        RegisterParameter<float>(PawTrackingParam.LeftMiddle, "VRCOSC/PawTracking/LeftMiddle", ParameterMode.Write,
            "Left Middle Curl", "Raw middle finger curl");
        RegisterParameter<float>(PawTrackingParam.LeftRing, "VRCOSC/PawTracking/LeftRing", ParameterMode.Write,
            "Left Ring Curl", "Raw ring finger curl");
        RegisterParameter<float>(PawTrackingParam.LeftPinky, "VRCOSC/PawTracking/LeftPinky", ParameterMode.Write,
            "Left Pinky Curl", "Raw pinky finger curl");
        RegisterParameter<float>(PawTrackingParam.RightIndex, "VRCOSC/PawTracking/RightIndex", ParameterMode.Write,
            "Right Index Curl", "Raw index finger curl");
        RegisterParameter<float>(PawTrackingParam.RightMiddle, "VRCOSC/PawTracking/RightMiddle", ParameterMode.Write,
            "Right Middle Curl", "Raw middle finger curl");
        RegisterParameter<float>(PawTrackingParam.RightRing, "VRCOSC/PawTracking/RightRing", ParameterMode.Write,
            "Right Ring Curl", "Raw ring finger curl");
        RegisterParameter<float>(PawTrackingParam.RightPinky, "VRCOSC/PawTracking/RightPinky", ParameterMode.Write,
            "Right Pinky Curl", "Raw pinky finger curl");

        RegisterParameter<float>(PawTrackingParam.LeftTriggerPull, "VRCOSC/PawTracking/LeftTrigger", ParameterMode.Write,
            "Left Trigger Pull", "Left trigger analog value (0-1)");
        RegisterParameter<bool>(PawTrackingParam.LeftTriggerTouch, "VRCOSC/PawTracking/LeftTriggerTouch", ParameterMode.Write,
            "Left Trigger Touch", "Left trigger capacitive touch");
        RegisterParameter<bool>(PawTrackingParam.LeftTriggerClick, "VRCOSC/PawTracking/LeftTriggerClick", ParameterMode.Write,
            "Left Trigger Click", "Left trigger fully pressed");
        RegisterParameter<float>(PawTrackingParam.RightTriggerPull, "VRCOSC/PawTracking/RightTrigger", ParameterMode.Write,
            "Right Trigger Pull", "Right trigger analog value (0-1)");
        RegisterParameter<bool>(PawTrackingParam.RightTriggerTouch, "VRCOSC/PawTracking/RightTriggerTouch", ParameterMode.Write,
            "Right Trigger Touch", "Right trigger capacitive touch");
        RegisterParameter<bool>(PawTrackingParam.RightTriggerClick, "VRCOSC/PawTracking/RightTriggerClick", ParameterMode.Write,
            "Right Trigger Click", "Right trigger fully pressed");

        RegisterParameter<float>(PawTrackingParam.LeftGripPull, "VRCOSC/PawTracking/LeftGrip", ParameterMode.Write,
            "Left Grip Pull", "Left grip squeeze amount (0-1, light touch fills easily)");
        RegisterParameter<float>(PawTrackingParam.LeftGripForce, "VRCOSC/PawTracking/LeftGripForce", ParameterMode.Write,
            "Left Grip Force", "Left grip hard-squeeze force (0-1, requires strong squeeze on Index)");
        RegisterParameter<bool>(PawTrackingParam.LeftGripClick, "VRCOSC/PawTracking/LeftGripClick", ParameterMode.Write,
            "Left Grip Click", "Left grip full squeeze");
        RegisterParameter<float>(PawTrackingParam.RightGripPull, "VRCOSC/PawTracking/RightGrip", ParameterMode.Write,
            "Right Grip Pull", "Right grip squeeze amount (0-1, light touch fills easily)");
        RegisterParameter<float>(PawTrackingParam.RightGripForce, "VRCOSC/PawTracking/RightGripForce", ParameterMode.Write,
            "Right Grip Force", "Right grip hard-squeeze force (0-1, requires strong squeeze on Index)");
        RegisterParameter<bool>(PawTrackingParam.RightGripClick, "VRCOSC/PawTracking/RightGripClick", ParameterMode.Write,
            "Right Grip Click", "Right grip full squeeze");

        RegisterParameter<bool>(PawTrackingParam.LeftATouch, "VRCOSC/PawTracking/LeftATouch", ParameterMode.Write,
            "Left A Touch", "Left A button capacitive touch");
        RegisterParameter<bool>(PawTrackingParam.LeftAClick, "VRCOSC/PawTracking/LeftAClick", ParameterMode.Write,
            "Left A Click", "Left A button pressed");
        RegisterParameter<bool>(PawTrackingParam.LeftBTouch, "VRCOSC/PawTracking/LeftBTouch", ParameterMode.Write,
            "Left B Touch", "Left B button capacitive touch");
        RegisterParameter<bool>(PawTrackingParam.LeftBClick, "VRCOSC/PawTracking/LeftBClick", ParameterMode.Write,
            "Left B Click", "Left B button pressed");
        RegisterParameter<bool>(PawTrackingParam.RightATouch, "VRCOSC/PawTracking/RightATouch", ParameterMode.Write,
            "Right A Touch", "Right A button capacitive touch");
        RegisterParameter<bool>(PawTrackingParam.RightAClick, "VRCOSC/PawTracking/RightAClick", ParameterMode.Write,
            "Right A Click", "Right A button pressed");
        RegisterParameter<bool>(PawTrackingParam.RightBTouch, "VRCOSC/PawTracking/RightBTouch", ParameterMode.Write,
            "Right B Touch", "Right B button capacitive touch");
        RegisterParameter<bool>(PawTrackingParam.RightBClick, "VRCOSC/PawTracking/RightBClick", ParameterMode.Write,
            "Right B Click", "Right B button pressed");

        RegisterParameter<float>(PawTrackingParam.LeftStickX, "VRCOSC/PawTracking/LeftStickX", ParameterMode.Write,
            "Left Stick X", "Left thumbstick horizontal position (-1 to 1)");
        RegisterParameter<float>(PawTrackingParam.LeftStickY, "VRCOSC/PawTracking/LeftStickY", ParameterMode.Write,
            "Left Stick Y", "Left thumbstick vertical position (-1 to 1)");
        RegisterParameter<bool>(PawTrackingParam.LeftStickTouch, "VRCOSC/PawTracking/LeftStickTouch", ParameterMode.Write,
            "Left Stick Touch", "Left thumbstick capacitive touch");
        RegisterParameter<bool>(PawTrackingParam.LeftStickClick, "VRCOSC/PawTracking/LeftStickClick", ParameterMode.Write,
            "Left Stick Click", "Left thumbstick pressed down");
        RegisterParameter<float>(PawTrackingParam.RightStickX, "VRCOSC/PawTracking/RightStickX", ParameterMode.Write,
            "Right Stick X", "Right thumbstick horizontal position (-1 to 1)");
        RegisterParameter<float>(PawTrackingParam.RightStickY, "VRCOSC/PawTracking/RightStickY", ParameterMode.Write,
            "Right Stick Y", "Right thumbstick vertical position (-1 to 1)");
        RegisterParameter<bool>(PawTrackingParam.RightStickTouch, "VRCOSC/PawTracking/RightStickTouch", ParameterMode.Write,
            "Right Stick Touch", "Right thumbstick capacitive touch");
        RegisterParameter<bool>(PawTrackingParam.RightStickClick, "VRCOSC/PawTracking/RightStickClick", ParameterMode.Write,
            "Right Stick Click", "Right thumbstick pressed down");

        RegisterParameter<float>(PawTrackingParam.LeftPadX, "VRCOSC/PawTracking/LeftPadX", ParameterMode.Write,
            "Left Pad X", "Left trackpad horizontal position (-1 to 1)");
        RegisterParameter<float>(PawTrackingParam.LeftPadY, "VRCOSC/PawTracking/LeftPadY", ParameterMode.Write,
            "Left Pad Y", "Left trackpad vertical position (-1 to 1)");
        RegisterParameter<bool>(PawTrackingParam.LeftPadTouch, "VRCOSC/PawTracking/LeftPadTouch", ParameterMode.Write,
            "Left Pad Touch", "Left trackpad capacitive touch");
        RegisterParameter<bool>(PawTrackingParam.LeftPadClick, "VRCOSC/PawTracking/LeftPadClick", ParameterMode.Write,
            "Left Pad Click", "Left trackpad pressed");
        RegisterParameter<float>(PawTrackingParam.RightPadX, "VRCOSC/PawTracking/RightPadX", ParameterMode.Write,
            "Right Pad X", "Right trackpad horizontal position (-1 to 1)");
        RegisterParameter<float>(PawTrackingParam.RightPadY, "VRCOSC/PawTracking/RightPadY", ParameterMode.Write,
            "Right Pad Y", "Right trackpad vertical position (-1 to 1)");
        RegisterParameter<bool>(PawTrackingParam.RightPadTouch, "VRCOSC/PawTracking/RightPadTouch", ParameterMode.Write,
            "Right Pad Touch", "Right trackpad capacitive touch");
        RegisterParameter<bool>(PawTrackingParam.RightPadClick, "VRCOSC/PawTracking/RightPadClick", ParameterMode.Write,
            "Right Pad Click", "Right trackpad pressed");

        RegisterParameter<bool>(PawTrackingParam.TrackerChestButton, "VRCOSC/PawTracking/TrackerChestButton", ParameterMode.Write,
            "Chest Tracker Button", "Chest tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerWaistButton, "VRCOSC/PawTracking/TrackerWaistButton", ParameterMode.Write,
            "Waist Tracker Button", "Waist/Hip tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerLeftFootButton, "VRCOSC/PawTracking/TrackerLeftFootButton", ParameterMode.Write,
            "Left Foot Tracker Button", "Left foot tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerRightFootButton, "VRCOSC/PawTracking/TrackerRightFootButton", ParameterMode.Write,
            "Right Foot Tracker Button", "Right foot tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerLeftKneeButton, "VRCOSC/PawTracking/TrackerLeftKneeButton", ParameterMode.Write,
            "Left Knee Tracker Button", "Left knee tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerRightKneeButton, "VRCOSC/PawTracking/TrackerRightKneeButton", ParameterMode.Write,
            "Right Knee Tracker Button", "Right knee tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerLeftElbowButton, "VRCOSC/PawTracking/TrackerLeftElbowButton", ParameterMode.Write,
            "Left Elbow Tracker Button", "Left elbow tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerRightElbowButton, "VRCOSC/PawTracking/TrackerRightElbowButton", ParameterMode.Write,
            "Right Elbow Tracker Button", "Right elbow tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerLeftShoulderButton, "VRCOSC/PawTracking/TrackerLeftShoulderButton", ParameterMode.Write,
            "Left Shoulder Tracker Button", "Left shoulder tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerRightShoulderButton, "VRCOSC/PawTracking/TrackerRightShoulderButton", ParameterMode.Write,
            "Right Shoulder Tracker Button", "Right shoulder tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerLeftWristButton, "VRCOSC/PawTracking/TrackerLeftWristButton", ParameterMode.Write,
            "Left Wrist Tracker Button", "Left wrist tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerRightWristButton, "VRCOSC/PawTracking/TrackerRightWristButton", ParameterMode.Write,
            "Right Wrist Tracker Button", "Right wrist tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerLeftAnkleButton, "VRCOSC/PawTracking/TrackerLeftAnkleButton", ParameterMode.Write,
            "Left Ankle Tracker Button", "Left ankle tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerRightAnkleButton, "VRCOSC/PawTracking/TrackerRightAnkleButton", ParameterMode.Write,
            "Right Ankle Tracker Button", "Right ankle tracker button pressed");
        RegisterParameter<bool>(PawTrackingParam.TrackerCameraButton, "VRCOSC/PawTracking/TrackerCameraButton", ParameterMode.Write,
            "Camera Tracker Button", "Camera tracker button pressed");

        CreateGroup("Thresholds", "Finger detection thresholds. Index controllers have noisy capacitive sensors - adjust to your grip.",
            PawTrackingSetting.FingerDownThreshold,
            PawTrackingSetting.FingerUpThreshold,
            PawTrackingSetting.GestureChangeThreshold,
            PawTrackingSetting.MinGestureWeight,
            PawTrackingSetting.FistGripThreshold);

        CreateGroup("Features", "Toggle optional features",
            PawTrackingSetting.UseGripForce,
            PawTrackingSetting.EnableControllerInputs,
            PawTrackingSetting.EnableTrackerButtons,
            PawTrackingSetting.DebugLogging);

        SetRuntimeView(typeof(PawTrackingRuntimeView));
    }

    protected override Task<bool> OnModuleStart()
    {
        _lastLeftGesture = new GestureResult(VRCGesture.Neutral, 0f, VRCGesture.Neutral);
        _lastRightGesture = new GestureResult(VRCGesture.Neutral, 0f, VRCGesture.Neutral);
        _trackerButtonStates = new List<TrackerButtonState>();
        _trackerActionsReady = false;
        _gripForceReady = false;
        _leftGripForce = 0f;
        _rightGripForce = 0f;
        _consecutiveOpenVRFailures = 0;
        _knownTrackerIndexes = new HashSet<uint>();
        _lastDeviceScan = DateTime.UtcNow;

        try { PatchOpenVRActions(); }
        catch (Exception ex) { LogDebug($"OpenVR action patching failed: {ex.Message}"); }

        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        _trackerActionsReady = false;
        _gripForceReady = false;
        _trackerActionHandles.Clear();
        _trackerButtonStates.Clear();
        _leftGripForceHandle = 0;
        _rightGripForceHandle = 0;
        _leftGripForce = 0f;
        _rightGripForce = 0f;

        SendParameter(PawTrackingParam.GestureLeft, 0);
        SendParameter(PawTrackingParam.GestureRight, 0);
        SendParameter(PawTrackingParam.GestureLeftWeight, 0f);
        SendParameter(PawTrackingParam.GestureRightWeight, 0f);
        SendParameter(PawTrackingParam.GestureLeftConfidence, 0f);
        SendParameter(PawTrackingParam.GestureRightConfidence, 0f);
        return Task.CompletedTask;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 33)]
    private void OnUpdate()
    {
        // Guard against OpenVR being torn down during shutdown
        if (OpenVR.Input == null)
        {
            _consecutiveOpenVRFailures++;
            return;
        }

        // Back off if OpenVR has been failing repeatedly
        if (_consecutiveOpenVRFailures > 10)
        {
            _consecutiveOpenVRFailures--;
            return;
        }

        try
        {
            OnUpdateCore();
            _consecutiveOpenVRFailures = 0;
        }
        catch (Exception ex)
        {
            _consecutiveOpenVRFailures++;
            if (_consecutiveOpenVRFailures <= 3)
                LogDebug($"OpenVR update failed: {ex.Message}");
        }
    }

    private void OnUpdateCore()
    {
        Controller? lc = GetOpenVRManager().GetLeftController();
        Controller? rc = GetOpenVRManager().GetRightController();

        float downThreshold = GetSettingValue<float>(PawTrackingSetting.FingerDownThreshold);
        float upThreshold = GetSettingValue<float>(PawTrackingSetting.FingerUpThreshold);
        float changeThreshold = GetSettingValue<float>(PawTrackingSetting.GestureChangeThreshold);
        float minWeight = GetSettingValue<float>(PawTrackingSetting.MinGestureWeight);
        float fistGrip = GetSettingValue<float>(PawTrackingSetting.FistGripThreshold);
        bool enableInputs = GetSettingValue<bool>(PawTrackingSetting.EnableControllerInputs);
        bool enableTrackers = GetSettingValue<bool>(PawTrackingSetting.EnableTrackerButtons);
        bool useGripForce = GetSettingValue<bool>(PawTrackingSetting.UseGripForce);
        bool debug = GetSettingValue<bool>(PawTrackingSetting.DebugLogging);

        GestureConfig config = new(downThreshold, upThreshold, changeThreshold, minWeight, fistGrip);

        ControllerInputsEnabled = enableInputs;
        TrackerButtonsEnabled = enableTrackers;

        if (!_gripForceReady) RetryGripForceHandles();
        if (_gripForceReady && OpenVR.Input != null)
        {
            InputAnalogActionData_t ld = new();
            OpenVR.Input.GetAnalogActionData(_leftGripForceHandle, ref ld, inputanalogactiondata_t_size, OpenVR.k_ulInvalidInputValueHandle);
            _leftGripForce = ld.x;

            InputAnalogActionData_t rd = new();
            OpenVR.Input.GetAnalogActionData(_rightGripForceHandle, ref rd, inputanalogactiondata_t_size, OpenVR.k_ulInvalidInputValueHandle);
            _rightGripForce = rd.x;
        }

        if (lc is not null)
        {
            ProcessHand(lc, true, config, enableInputs, useGripForce, debug);
        }

        if (rc is not null)
        {
            ProcessHand(rc, false, config, enableInputs, useGripForce, debug);
        }

        // Tracker buttons via legacy OpenVR GetControllerState
        if (enableTrackers)
        {
            PollTrackerButtons(debug);
        }
        else
        {
            _trackerButtonStates.Clear();
        }
    }

    private void ProcessHand(Controller controller, bool isLeft, GestureConfig config, bool enableInputs, bool useGripForce, bool debug)
    {
        InputState input = controller.Input;
        Skeleton fingers = input.Skeleton;
        ref GestureResult lastGesture = ref isLeft ? ref _lastLeftGesture : ref _lastRightGesture;

        GestureResult result = DetectGesture(fingers, input, lastGesture, config);

        if (debug && result.Gesture != lastGesture.Gesture)
        {
            string side = isLeft ? "L" : "R";
            LogDebug($"[{side}] {lastGesture.Gesture} -> {result.Gesture} (w={result.Weight:F2}, closest={result.ClosestGesture})");
        }

        lastGesture = result;

        // Compute effective output weight: grip pull by default, grip force when UseGripForce is on
        float outputWeight;
        if (result.Gesture == VRCGesture.Neutral)
        {
            outputWeight = 0f;
        }
        else if (useGripForce && _gripForceReady)
        {
            outputWeight = isLeft ? _leftGripForce : _rightGripForce;
        }
        else
        {
            outputWeight = input.Grip.Pull;
        }

        // Store effective weight so runtime view shows the correct value
        lastGesture = result with { Weight = outputWeight };

        SendParameter(isLeft ? PawTrackingParam.GestureLeft : PawTrackingParam.GestureRight, (int)result.Gesture);
        SendParameter(isLeft ? PawTrackingParam.GestureLeftWeight : PawTrackingParam.GestureRightWeight, outputWeight);
        SendParameter(isLeft ? PawTrackingParam.GestureLeftConfidence : PawTrackingParam.GestureRightConfidence, result.ClosestWeight);

        SendParameter(isLeft ? PawTrackingParam.LeftIndex : PawTrackingParam.RightIndex, fingers.Index);
        SendParameter(isLeft ? PawTrackingParam.LeftMiddle : PawTrackingParam.RightMiddle, fingers.Middle);
        SendParameter(isLeft ? PawTrackingParam.LeftRing : PawTrackingParam.RightRing, fingers.Ring);
        SendParameter(isLeft ? PawTrackingParam.LeftPinky : PawTrackingParam.RightPinky, fingers.Pinky);

        if (enableInputs)
        {
            SendControllerInputs(input, isLeft);
        }
    }

    private void SendControllerInputs(InputState input, bool isLeft)
    {
        SendParameter(isLeft ? PawTrackingParam.LeftTriggerPull : PawTrackingParam.RightTriggerPull, input.Trigger.Pull);
        SendParameter(isLeft ? PawTrackingParam.LeftTriggerTouch : PawTrackingParam.RightTriggerTouch, input.Trigger.Touch);
        SendParameter(isLeft ? PawTrackingParam.LeftTriggerClick : PawTrackingParam.RightTriggerClick, input.Trigger.Click);

        SendParameter(isLeft ? PawTrackingParam.LeftGripPull : PawTrackingParam.RightGripPull, input.Grip.Pull);
        SendParameter(isLeft ? PawTrackingParam.LeftGripForce : PawTrackingParam.RightGripForce, isLeft ? _leftGripForce : _rightGripForce);
        SendParameter(isLeft ? PawTrackingParam.LeftGripClick : PawTrackingParam.RightGripClick, input.Grip.Click);

        SendParameter(isLeft ? PawTrackingParam.LeftATouch : PawTrackingParam.RightATouch, input.Primary.Touch);
        SendParameter(isLeft ? PawTrackingParam.LeftAClick : PawTrackingParam.RightAClick, input.Primary.Click);

        SendParameter(isLeft ? PawTrackingParam.LeftBTouch : PawTrackingParam.RightBTouch, input.Secondary.Touch);
        SendParameter(isLeft ? PawTrackingParam.LeftBClick : PawTrackingParam.RightBClick, input.Secondary.Click);

        SendParameter(isLeft ? PawTrackingParam.LeftStickX : PawTrackingParam.RightStickX, input.Stick.Position.X);
        SendParameter(isLeft ? PawTrackingParam.LeftStickY : PawTrackingParam.RightStickY, input.Stick.Position.Y);
        SendParameter(isLeft ? PawTrackingParam.LeftStickTouch : PawTrackingParam.RightStickTouch, input.Stick.Touch);
        SendParameter(isLeft ? PawTrackingParam.LeftStickClick : PawTrackingParam.RightStickClick, input.Stick.Click);

        SendParameter(isLeft ? PawTrackingParam.LeftPadX : PawTrackingParam.RightPadX, input.Pad.Position.X);
        SendParameter(isLeft ? PawTrackingParam.LeftPadY : PawTrackingParam.RightPadY, input.Pad.Position.Y);
        SendParameter(isLeft ? PawTrackingParam.LeftPadTouch : PawTrackingParam.RightPadTouch, input.Pad.Touch);
        SendParameter(isLeft ? PawTrackingParam.LeftPadClick : PawTrackingParam.RightPadClick, input.Pad.Click);
    }

    private void PatchOpenVRActions()
    {
        string manifestPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VRCOSC", "runtime", "openvr", "action_manifest.json");

        if (!File.Exists(manifestPath))
        {
            LogDebug("OpenVR actions: manifest not found, skipping");
            return;
        }

        string manifestDir = Path.GetDirectoryName(manifestPath)!;
        string json = File.ReadAllText(manifestPath);
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        JsonArray actions = root["actions"]!.AsArray();
        HashSet<string> existingNames = new();
        foreach (JsonNode? a in actions)
            if (a?["name"]?.GetValue<string>() is { } n)
                existingNames.Add(n);

        bool modified = false;

        string[] gripForceActions =
        [
            "/actions/main/in/left_grip_force",
            "/actions/main/in/right_grip_force"
        ];
        foreach (string name in gripForceActions)
        {
            if (existingNames.Contains(name)) continue;
            actions.Add(new JsonObject { ["name"] = name, ["type"] = "vector1" });
            modified = true;
        }

        bool enableTrackers = GetSettingValue<bool>(PawTrackingSetting.EnableTrackerButtons);
        if (enableTrackers)
        {
            foreach (TrackerDef def in TrackerDefs)
            {
                if (existingNames.Contains(def.ActionName)) continue;
                actions.Add(new JsonObject { ["name"] = def.ActionName, ["type"] = "boolean" });
                modified = true;
            }
        }

        JsonArray bindings = root["default_bindings"]!.AsArray();
        HashSet<string> existingTypes = new();
        foreach (JsonNode? b in bindings)
            if (b?["controller_type"]?.GetValue<string>() is { } ct)
                existingTypes.Add(ct);

        if (enableTrackers)
        {
            foreach (TrackerDef def in TrackerDefs)
            {
                string bindingFileName = $"pawtracking_{def.ControllerType}_bindings.json";
                string bindingJson = $$"""
                {
                    "action_manifest_version": 0,
                    "alias_info": {},
                    "bindings": {
                        "/actions/main": {
                            "sources": [{
                                "inputs": { "click": { "output": "{{def.ActionName}}" } },
                                "mode": "button",
                                "path": "{{def.InputPath}}"
                            }]
                        }
                    },
                    "category": "steamvr_input",
                    "controller_type": "{{def.ControllerType}}",
                    "name": "PawTracking {{def.RoleLabel}} tracker binding"
                }
                """;
                File.WriteAllText(Path.Combine(manifestDir, bindingFileName), bindingJson);

                if (existingTypes.Contains(def.ControllerType)) continue;
                bindings.Add(new JsonObject
                {
                    ["controller_type"] = def.ControllerType,
                    ["binding_url"] = bindingFileName
                });
                modified = true;
            }
        }

        PatchKnucklesGripBindings(manifestDir);

        if (modified)
        {
            JsonSerializerOptions opts = new() { WriteIndented = true };
            File.WriteAllText(manifestPath, root.ToJsonString(opts));
            LogDebug("OpenVR actions: patched manifest");
        }

        // Always reload (bindings files may have changed even if manifest JSON didn't)
        EVRInputError err = OpenVR.Input.SetActionManifestPath(manifestPath);
        if (err != EVRInputError.None)
        {
            LogDebug($"OpenVR actions: SetActionManifestPath error: {err}");
            return;
        }

        ulong lh = 0, rh = 0;
        EVRInputError le = OpenVR.Input.GetActionHandle("/actions/main/in/left_grip_force", ref lh);
        EVRInputError re = OpenVR.Input.GetActionHandle("/actions/main/in/right_grip_force", ref rh);
        if (le == EVRInputError.None && re == EVRInputError.None && lh != 0 && rh != 0)
        {
            _leftGripForceHandle = lh;
            _rightGripForceHandle = rh;
            _gripForceReady = true;
        }
        else
        {
            LogDebug($"Grip force: handle errors L={le} R={re}");
        }

        if (enableTrackers)
        {
            _trackerActionHandles.Clear();
            foreach (TrackerDef def in TrackerDefs)
            {
                ulong handle = 0;
                err = OpenVR.Input.GetActionHandle(def.ActionName, ref handle);
                if (err == EVRInputError.None && handle != 0)
                    _trackerActionHandles[def.Param] = handle;
                else
                    LogDebug($"Tracker: GetActionHandle({def.ActionName}) failed: {err}");
            }
            _trackerActionsReady = _trackerActionHandles.Count > 0;
        }
    }

    private static void PatchKnucklesGripBindings(string manifestDir)
    {
        string knucklesPath = Path.Combine(manifestDir, "knuckles_bindings.json");
        if (!File.Exists(knucklesPath)) return;

        string json = File.ReadAllText(knucklesPath);
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        JsonArray? mainBindings = root["bindings"]?["/actions/main"]?["sources"]?.AsArray();
        if (mainBindings is null) return;

        bool patched = false;

        foreach (JsonNode? source in mainBindings)
        {
            string? path = source?["path"]?.GetValue<string>();
            if (path is null || !path.Contains("/input/grip")) continue;
            if (source?["mode"]?.GetValue<string>() != "force_sensor") continue;

            JsonObject? inputs = source?["inputs"]?.AsObject();
            if (inputs is null || !inputs.ContainsKey("value")) continue;

            bool isLeft = path.Contains("left");
            string pullAction = isLeft ? "/actions/main/in/left_grip_pull" : "/actions/main/in/right_grip_pull";
            string clickAction = isLeft ? "/actions/main/in/left_grip_click" : "/actions/main/in/right_grip_click";

            source!["mode"] = "trigger";
            source["inputs"] = new JsonObject
            {
                ["pull"] = new JsonObject { ["output"] = pullAction },
                ["click"] = new JsonObject { ["output"] = clickAction }
            };
            patched = true;
        }

        (string gripPath, string forceAction)[] sides =
        [
            ("/user/hand/left/input/grip", "/actions/main/in/left_grip_force"),
            ("/user/hand/right/input/grip", "/actions/main/in/right_grip_force")
        ];

        foreach (var (gripPath, forceAction) in sides)
        {
            bool hasForceEntry = false;
            foreach (JsonNode? source in mainBindings)
            {
                string? p = source?["path"]?.GetValue<string>();
                string? m = source?["mode"]?.GetValue<string>();
                if (p == gripPath && m == "force_sensor")
                {
                    hasForceEntry = true;
                    break;
                }
            }

            if (!hasForceEntry)
            {
                mainBindings.Add(new JsonObject
                {
                    ["inputs"] = new JsonObject
                    {
                        ["force"] = new JsonObject { ["output"] = forceAction }
                    },
                    ["mode"] = "force_sensor",
                    ["path"] = gripPath
                });
                patched = true;
            }
        }

        if (patched)
        {
            File.WriteAllText(knucklesPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private void PollTrackerButtons(bool debug)
    {
        CVRInput? input = OpenVR.Input;
        if (input == null) return;

        if (!_trackerActionsReady)
        {
            RetryTrackerHandles();
            if (!_trackerActionsReady)
            {
                _trackerButtonStates.Clear();
                return;
            }
        }

        if ((DateTime.UtcNow - _lastDeviceScan).TotalSeconds > 5)
        {
            CheckForNewTrackers();
        }

        CVRSystem? system = OpenVR.System;
        List<TrackerButtonState> newStates = new();

        foreach (TrackerDef def in TrackerDefs)
        {
            if (!_trackerActionHandles.TryGetValue(def.Param, out ulong handle)) continue;

            InputDigitalActionData_t data = new();
            input.GetDigitalActionData(handle, ref data, inputdigitalactiondata_t_size, OpenVR.k_ulInvalidInputValueHandle);

            if (!data.bActive)
            {
                SendParameter(def.Param, false);
                continue;
            }

            // bActive can remain true for disconnected devices (SteamVR remembers bindings).
            // Verify the device is actually connected via the action's origin.
            if (system != null && data.activeOrigin != 0)
            {
                InputOriginInfo_t originInfo = new();
                input.GetOriginTrackedDeviceInfo(data.activeOrigin, ref originInfo,
                    (uint)Unsafe.SizeOf<InputOriginInfo_t>());
                if (originInfo.trackedDeviceIndex != OpenVR.k_unTrackedDeviceIndexInvalid &&
                    !system.IsTrackedDeviceConnected(originInfo.trackedDeviceIndex))
                {
                    SendParameter(def.Param, false);
                    continue;
                }
            }

            bool pressed = data.bState;
            newStates.Add(new TrackerButtonState(def.RoleLabel, 0, pressed));
            SendParameter(def.Param, pressed);
        }

        _trackerButtonStates = newStates;
    }

    private void CheckForNewTrackers()
    {
        _lastDeviceScan = DateTime.UtcNow;

        CVRSystem? system = OpenVR.System;
        if (system == null) return;

        HashSet<uint> currentIndexes = new();
        for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
        {
            if (system.GetTrackedDeviceClass(i) == ETrackedDeviceClass.GenericTracker &&
                system.IsTrackedDeviceConnected(i))
            {
                currentIndexes.Add(i);
            }
        }

        // Detect connects
        bool hasNewTracker = !currentIndexes.IsSubsetOf(_knownTrackerIndexes);

        _knownTrackerIndexes = currentIndexes;

        if (hasNewTracker)
            ReloadActionManifest();

        // Update battery levels for role-resolved trackers (only if actually connected)
        Dictionary<string, float> batteries = new();
        foreach (TrackerDef def in TrackerDefs)
        {
            ulong sourceHandle = 0;
            OpenVR.Input.GetInputSourceHandle(def.InputPath.Replace("/input/power", ""), ref sourceHandle);
            if (sourceHandle == OpenVR.k_ulInvalidActionHandle) continue;

            InputOriginInfo_t info = new();
            OpenVR.Input.GetOriginTrackedDeviceInfo(sourceHandle, ref info,
                (uint)Unsafe.SizeOf<InputOriginInfo_t>());
            if (info.trackedDeviceIndex == OpenVR.k_unTrackedDeviceIndexInvalid) continue;
            if (!system.IsTrackedDeviceConnected(info.trackedDeviceIndex)) continue;

            ETrackedPropertyError propErr = ETrackedPropertyError.TrackedProp_Success;
            float battery = system.GetFloatTrackedDeviceProperty(info.trackedDeviceIndex,
                ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref propErr);
            if (propErr == ETrackedPropertyError.TrackedProp_Success)
                batteries[def.RoleLabel] = battery;
        }
        _trackerBatteryLevels = batteries;
    }

    private void ReloadActionManifest()
    {
        string manifestPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VRCOSC", "runtime", "openvr", "action_manifest.json");

        if (!File.Exists(manifestPath)) return;

        OpenVR.Input.SetActionManifestPath(manifestPath);
    }

    private void RetryTrackerHandles()
    {
        try
        {
            CVRInput? input = OpenVR.Input;
            if (input == null)
            {
                _trackerActionsReady = false;
                return;
            }

            _trackerActionHandles.Clear();
            foreach (TrackerDef def in TrackerDefs)
            {
                ulong handle = 0;
                EVRInputError err = input.GetActionHandle(def.ActionName, ref handle);
                if (err == EVRInputError.None && handle != 0)
                    _trackerActionHandles[def.Param] = handle;
            }
            _trackerActionsReady = _trackerActionHandles.Count > 0;
        }
        catch (Exception ex)
        {
            LogDebug($"RetryTrackerHandles failed: {ex.Message}");
        }
    }

    private void RetryGripForceHandles()
    {
        try
        {
            CVRInput? input = OpenVR.Input;
            if (input == null)
            {
                _gripForceReady = false;
                return;
            }

            ulong lh = 0, rh = 0;
            EVRInputError le = input.GetActionHandle("/actions/main/in/left_grip_force", ref lh);
            EVRInputError re = input.GetActionHandle("/actions/main/in/right_grip_force", ref rh);
            if (le == EVRInputError.None && re == EVRInputError.None && lh != 0 && rh != 0)
            {
                _leftGripForceHandle = lh;
                _rightGripForceHandle = rh;
                _gripForceReady = true;
            }
        }
        catch (Exception ex)
        {
            LogDebug($"RetryGripForceHandles failed: {ex.Message}");
        }
    }

    #region Gesture Detection

    // Each gesture is defined by target finger curl values (0=open, 1=closed)
    // Index, Middle, Ring, Pinky - no thumb data available from VRCOSC SDK
    private static readonly Dictionary<VRCGesture, float[]> GesturePatterns = new()
    {
        [VRCGesture.Neutral] = [0f, 0f, 0f, 0f],
        [VRCGesture.Fist] = [1f, 1f, 1f, 1f],
        [VRCGesture.HandOpen] = [0f, 0f, 0f, 0f],
        [VRCGesture.FingerPoint] = [0f, 1f, 1f, 1f],
        [VRCGesture.Victory] = [0f, 0f, 1f, 1f],
        [VRCGesture.RockNRoll] = [0f, 1f, 1f, 0f],
        [VRCGesture.HandGun] = [0f, 0f, 1f, 1f],
        [VRCGesture.ThumbsUp] = [1f, 1f, 1f, 1f],
    };

    private static bool IsThumbLifted(InputState input)
    {
        return !input.Primary.Touch   // A button
            && !input.Secondary.Touch  // B button
            && !input.Stick.Touch      // Thumbstick
            && !input.Pad.Touch;       // Trackpad
    }

    private static GestureResult DetectGesture(Skeleton fingers, InputState input, GestureResult previous, GestureConfig cfg)
    {
        float[] currentFingers = [fingers.Index, fingers.Middle, fingers.Ring, fingers.Pinky];

        List<GestureCandidate> candidates = new();

        foreach (var (gesture, pattern) in GesturePatterns)
        {
            // ThumbsUp is handled by post-processing using capacitive touch data
            if (gesture == VRCGesture.ThumbsUp)
                continue;

            // HandGun and Victory have identical 4-finger patterns. 
            // VRChat distinguishes them by thumb: gun=thumb up, victory=thumb down.
            // Without thumb data, we skip HandGun to avoid ambiguity.
            if (gesture == VRCGesture.HandGun)
                continue;

            // HandOpen and Neutral are identical without thumb data.
            // Use Neutral for "all fingers open" to avoid confusion.
            if (gesture == VRCGesture.HandOpen)
                continue;

            float score = ComputeGestureScore(currentFingers, pattern, cfg);
            candidates.Add(new GestureCandidate(score, gesture));
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

        GestureCandidate best = candidates[0];
        VRCGesture closestGesture = best.Gesture;
        float closestWeight = best.Score;

        // Apply hysteresis: only switch gesture if the new best is significantly better
        VRCGesture outputGesture;
        float outputWeight;

        if (best.Gesture == previous.Gesture)
        {
            outputGesture = best.Gesture;
            outputWeight = best.Score;
        }
        else
        {
            float previousScore = candidates
                .Where(c => c.Gesture == previous.Gesture)
                .Select(c => c.Score)
                .FirstOrDefault();

            if (best.Score - previousScore > cfg.ChangeThreshold)
            {
                outputGesture = best.Gesture;
                outputWeight = best.Score;
            }
            else
            {
                outputGesture = previous.Gesture;
                outputWeight = previousScore;
            }
        }

        // Enforce minimum weight - below this we report Neutral
        if (outputWeight < cfg.MinWeight && outputGesture != VRCGesture.Neutral)
        {
            outputGesture = VRCGesture.Neutral;
            outputWeight = 1f - closestWeight;
        }

        if (outputGesture == VRCGesture.Fist)
        {
            if (input.Grip.Pull < cfg.FistGripThreshold)
            {
                outputGesture = VRCGesture.Neutral;
                outputWeight = 1f - closestWeight;
            }
            else if (IsThumbLifted(input))
            {
                // All fingers curled + grip squeezed + thumb not touching anything = ThumbsUp
                outputGesture = VRCGesture.ThumbsUp;
            }
        }

        // Also update closest gesture with grip/thumb awareness
        if (closestGesture == VRCGesture.Fist && IsThumbLifted(input))
        {
            closestGesture = VRCGesture.ThumbsUp;
        }

        return new GestureResult(outputGesture, Math.Clamp(outputWeight, 0f, 1f), closestGesture, Math.Clamp(closestWeight, 0f, 1f));
    }

    private static float ComputeGestureScore(float[] current, float[] target, GestureConfig cfg)
    {
        // For each finger, compute how well it matches the target.
        // Target 0 = finger should be open (below upThreshold is perfect)
        // Target 1 = finger should be closed (above downThreshold is perfect)
        float totalScore = 0f;

        for (int i = 0; i < 4; i++)
        {
            float fingerVal = current[i];
            float targetVal = target[i];

            float fingerScore;
            if (targetVal > 0.5f)
            {
                if (fingerVal >= cfg.DownThreshold)
                    fingerScore = 1f;
                else if (fingerVal <= cfg.UpThreshold)
                    fingerScore = 0f;
                else
                    fingerScore = (fingerVal - cfg.UpThreshold) / (cfg.DownThreshold - cfg.UpThreshold);
            }
            else
            {
                if (fingerVal <= cfg.UpThreshold)
                    fingerScore = 1f;
                else if (fingerVal >= cfg.DownThreshold)
                    fingerScore = 0f;
                else
                    fingerScore = 1f - (fingerVal - cfg.UpThreshold) / (cfg.DownThreshold - cfg.UpThreshold);
            }

            totalScore += fingerScore;
        }

        return totalScore / 4f;
    }

    private record struct GestureCandidate(float Score, VRCGesture Gesture);

    #endregion
}
