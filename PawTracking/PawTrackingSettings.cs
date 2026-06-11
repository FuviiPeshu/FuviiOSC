namespace FuviiOSC.PawTracking;

public enum VRCGesture
{
    Neutral = 0,
    Fist = 1,
    HandOpen = 2,
    FingerPoint = 3,
    Victory = 4,
    RockNRoll = 5,
    HandGun = 6,
    ThumbsUp = 7
}

public enum PawTrackingSetting
{
    FingerDownThreshold,
    FingerUpThreshold,
    GestureChangeThreshold,
    MinGestureWeight,
    FistGripThreshold,
    EnableControllerInputs,
    EnableTrackerButtons,
    DebugLogging,
    UseGripForce
}

public enum PawTrackingParam
{
    // Gestures
    GestureLeft,
    GestureRight,
    GestureLeftWeight,
    GestureRightWeight,
    GestureLeftConfidence,
    GestureRightConfidence,
    // Finger curls
    LeftIndex,
    LeftMiddle,
    LeftRing,
    LeftPinky,
    RightIndex,
    RightMiddle,
    RightRing,
    RightPinky,
    // Triggers
    LeftTriggerPull,
    LeftTriggerTouch,
    LeftTriggerClick,
    RightTriggerPull,
    RightTriggerTouch,
    RightTriggerClick,
    // Grips
    LeftGripPull,
    LeftGripForce,
    LeftGripClick,
    RightGripPull,
    RightGripForce,
    RightGripClick,
    // Controller Buttons
    LeftATouch,
    LeftAClick,
    LeftBTouch,
    LeftBClick,
    RightATouch,
    RightAClick,
    RightBTouch,
    RightBClick,
    // Thumbsticks
    LeftStickX,
    LeftStickY,
    LeftStickTouch,
    LeftStickClick,
    RightStickX,
    RightStickY,
    RightStickTouch,
    RightStickClick,
    // Trackpads
    LeftPadX,
    LeftPadY,
    LeftPadTouch,
    LeftPadClick,
    RightPadX,
    RightPadY,
    RightPadTouch,
    RightPadClick,
    // Tracker buttons
    TrackerChestButton,
    TrackerWaistButton,
    TrackerLeftFootButton,
    TrackerRightFootButton,
    TrackerLeftKneeButton,
    TrackerRightKneeButton,
    TrackerLeftElbowButton,
    TrackerRightElbowButton,
    TrackerLeftShoulderButton,
    TrackerRightShoulderButton,
    TrackerLeftWristButton,
    TrackerRightWristButton,
    TrackerLeftAnkleButton,
    TrackerRightAnkleButton,
    TrackerCameraButton
}

public readonly record struct GestureConfig(
    float DownThreshold,
    float UpThreshold,
    float ChangeThreshold,
    float MinWeight,
    float FistGripThreshold
);

public readonly record struct GestureResult(
    VRCGesture Gesture,
    float Weight,
    VRCGesture ClosestGesture,
    float ClosestWeight = 0f
);

public record TrackerButtonState(
    string Role,
    uint DeviceIndex,
    bool ButtonPressed
);

public readonly record struct TrackerDef(
    string ActionName,
    string ControllerType,
    string InputPath,
    PawTrackingParam Param,
    string RoleLabel
);
