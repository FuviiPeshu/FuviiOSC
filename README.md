# Fuvii's Modules 🦦

A small collection of useful VRCOSC modules!

- [How to install](#how-to-install)
- [Available modules](#available-modules)
  - [Avatar Changer](#avatar-changer)
  - [Squeak Meter](#squeak-meter)
  - [Haptickle](#haptickle)
  - [Paw Tracking](#paw-tracking)

<br>

## How to install

This repository contains modules for the [VolcanicArts/VRCOSC](https://github.com/VolcanicArts/VRCOSC) app.
After launching VRCOSC, the modules should appear in the first tab (Package Download) as **"Fuvii's Modules"**.
Download the latest version, enable the modules you want and enjoy!

> Feel free to suggest new features or modules! And don't forget to report any issues you will encounter :)

![VRCOSC package installation](https://github.com/user-attachments/assets/fd67f861-84ff-4727-b3fb-94a4b5942cd8)

<br>

## Available modules

### Avatar Changer

**Avatar Changer** listens for specific OSC parameters and compares them against user-defined conditions. When a match is found, it automatically switches to the associated avatar ID.
This is made possible thanks to the [VRChat 2025.1.2 update](https://docs.vrchat.com/docs/vrchat-202512#changes--fixes).

#### Features

- **Flexible Triggers:** Define multiple triggers based on any OSC parameter with conditions such as equals, greater than, less than, or not equals
- **Multiple Avatars:** Assign different avatars to different trigger conditions

#### How to use

1. **Open the module settings** for Avatar Changer in VRCOSC
2. **Add a new trigger:**
   - Click the **Add** button
   - Click the **Edit** button next to the new trigger
   - Enter a name for the trigger (just for display purposes)
   - Enter the avatar ID to the avatar you want to switch to when the condition is met
   - In 'OSC parameters' section click the **Add** button
   - Enter your OSC parameter, choose the condition (equals, greater than, etc.) and set the value you expect to match
3. Repeat for as many triggers/avatars as you like


![Features inside module settings](https://github.com/user-attachments/assets/bb105102-5336-45ff-ad27-8edc17b10269)

---

### Squeak Meter

**Squeak Meter** listens to a selected audio output and analyzes sound in real time. It sends OSC parameters for overall volume, stereo balance, and the normalized amplitudes of bass, mid and treble frequency bands.

#### Features

- **Real-time audio analysis** of selected audio output
- **Customizable gain and smoothing**
- **OSC parameter output** for:
  - **Volume** - Overall loudness (`0.0 - 1.0`)
  - **Direction** - Stereo balance (`0.0 = left`, `0.5 = center`, `1.0 = right`)
  - **Bass** - Amplitude in the `0 - 250 Hz` range
  - **Mid** - Amplitude in the `250 - 4000 Hz` range
  - **Treble** - Amplitude in the `4000 - 20000 Hz` range

#### How to use

1. **Open the Run tab** inside VRCOSC
2. **Select your audio device** in the SqueakMeter section inside **Runtime** view:
   - Use the **Audio Device** dropdown to choose which output device you want to analyze
   - If a device is not supported, it will be grayed out and added to the disabled device list
3. **(Optional) Adjust analysis settings:**
   - Use the sliders in the module settings to fine-tune **Gain**, **Smoothing**, **Bass Boost**, **Mid Boost**, and **Treble Boost**

![Window for both module settings and parameters](https://github.com/user-attachments/assets/43d919ba-de6f-4aa5-a3c0-5a3d09e92561)
![Run tab with audio device selection](https://github.com/user-attachments/assets/0f749660-2c17-4639-a49f-ac987283750c)

---

### Haptickle

**Haptickle** triggers haptic feedback on Vive trackers (with a vibration motor attached) and external devices via IP, based on avatar parameters received through OSC.

> Note for Vive trackers: Motor needs to be attached to the proper pogo pins (1 - general purpose output pin + 2 - ground).
> Manual: https://dl.vive.com/Tracker/Guideline/HTC_Vive_Tracker_Developer_Guidelines_v1.4.pdf

#### Features

- **Per-device configuration:** Assign haptic triggers to individual devices (Vive trackers by serial number; external devices by IP address, port, and OSC control path)
- **Customizable haptic strength:** Adjust the intensity for each device
- **Automatic SteamVR device detection:** Trackers are automatically detected and updated as they are connected or disconnected
- **External device support:** Set up external devices manually via settings menu (OSC UDP)

#### How to use

1. **Open the Run tab** inside VRCOSC
2. **Connect your Vive trackers** and ensure they appear in the device list inside **Runtime** view
3. **Add or edit haptic triggers:**
   - Set the haptic strength and define the OSC parameter(s) that will activate the haptic pulse
4. **(Optional) Add external devices:**
   - In the module settings, add a new external device entry
   - Specify the OSC parameter to listen for, the device's IP address, port and OSC path
   - Choose the vibration pattern, trigger mode and adjust strength for each device

![Settings view with timeout and external devices](https://github.com/user-attachments/assets/d39e67c1-33bf-4ace-b6ff-50c541219d85)
![Run tab with tracker haptic settings](https://github.com/user-attachments/assets/598affe0-b0d6-447f-b224-eaa079d7feea)

---

### Paw Tracking

**Paw Tracking** reads SteamVR controller skeleton data and tracker button presses, providing gesture detection with configurable thresholds and full input forwarding via OSC.

#### Features

- **Gesture detection** - Detects VRChat hand gestures (Idle, Fist, Open, Point, Peace, Rock, Gun, Thumbs Up) from SteamVR skeleton data with per-gesture confidence values
- **Per-finger curl values** - Individual curl values (0-1) for index, middle, ring, and pinky on both hands
- **Full controller input forwarding** - Trigger (pull/touch/click), grip (pull/force/click), A/B buttons (touch/click), thumbstick (XY/touch/click), trackpad (XY/touch/click)
- **Tracker button presses** - Detects button presses on Vive trackers for all devices that have their roles selected in SteamVR (chest, waist, left/right foot, left/right knee, left/right elbow, left/right shoulder, left/right wrist, left/right ankle, camera)
- **Configureable thresholds** - Separate finger-up and finger-down thresholds prevent gesture flickering
- **Grip force** - Index controller grip force support (use squeeze value rather than hold)

#### Notes

- Trackers must be assigned roles in SteamVR Input settings
- Controllers must support skeleton input (didn't test for others, you can reach out to me for adding support for your devices)

---
