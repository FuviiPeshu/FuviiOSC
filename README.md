# Fuvii's Modules 🦦

A small collection of useful VRCOSC modules!

## 🛠 How to install

This repository contains files designed to be used with the [VolcanicArts/VRCOSC](https://github.com/VolcanicArts/VRCOSC) app.
Follow the instructions there and after launching the program the modules should appear in the first tab (Package Download) as **"Fuvii's Modules"**. 
From there, simply download the latest version and enjoy!

![VRCOSC package installation](https://github.com/user-attachments/assets/477559b6-706f-4e2e-bba9-e5776bb8695f)

## 📦 Available modules

### 🔁 Avatar Changer

**Avatar Changer** listens for specific OSC parameters and compares them against user-defined conditions. When a match is found, it automatically switches to the associated avatar ID.
This is made possible thanks to the [VRChat 2025.1.2 update](https://docs.vrchat.com/docs/vrchat-202512#changes--fixes).

#### ✨ Features

- 🧩 **Flexible Triggers:** Define multiple triggers based on any OSC parameter with conditions such as equals, greater than, less than, or not equals
- 🗂 **Multiple Avatars:** Assign different avatars to different trigger conditions

#### ❓ How to use

1. **Open the module settings** for Avatar Changer in VRCOSC
2. **Add a new trigger:**
   - Click the **Add (green '+' sign)** button
   - Click the **Edit (blue 'pencil' icon)** button next to the new trigger
   - Enter a name for the trigger (just for display purposes)
   - Enter the avatar ID to the avatar you want to switch to when the condition is met
   - In 'OSC parameters' section click the **Add (green '+' sign)** button
   - Enter your OSC parameter, choose the condition (equals, greater than, etc.) and set the value you expect to match
3. Repeat for as many triggers/avatars as you like

> 📝 Note: This feature currently works **ONLY FOR FAVOURITED** avatars!  
*See [this related VRC feedback ticket](https://feedback.vrchat.com/avatar-30/p/1626-osc-avatar-change-is-not-working) – please upvote if it's relevant to you.*

![Features inside module settings](https://github.com/user-attachments/assets/bb105102-5336-45ff-ad27-8edc17b10269)

---

### 🔊 Squeak Meter

**Squeak Meter** listens to selected audio output and analyzes sound in real time. It sends OSC parameters for overall volume, stereo balance, and the normalized amplitudes of bass, mid and treble frequency bands.

#### ✨ Features

- 🎧 **Real-time audio analysis** of selected audio output
- 📊 **Customizable gain and smoothing**
- 📡 **Customizable OSC parameter output** for:
  - **Volume** – Overall loudness (`0.0 – 1.0`)
  - **Direction** – Stereo balance (`0.0 = left`, `0.5 = center`, `1.0 = right`)
  - **Bass** – Amplitude in the `0 – 250 Hz` range
  - **Mid** – Amplitude in the `250 – 4000 Hz` range
  - **Treble** – Amplitude in the `4000 – 20000 Hz` range

#### ❓ How to use

1. **Open the Run tab** inside VRCOSC
2. **Select your audio device** in SqueakMeter section inside **Runtime** view:
   - Use the **Audio Device** dropdown to choose which output device you want to analyze
   - If selected device is not supported, it will be grayed out and added to the disabled device list under select box and you won't be able to select it anymore (unless removed from the disabled list)
3. **(Optional) Adjust analysis settings:**
   - Use the sliders in the module settings to fine-tune **Gain**, **Smoothing**, **Bass Boost**, **Mid Boost**, and **Treble Boost** to match your preferences

![Window for both module settings and parameters](https://github.com/user-attachments/assets/43d919ba-de6f-4aa5-a3c0-5a3d09e92561)
![Run tab with audio device selection](https://github.com/user-attachments/assets/0f749660-2c17-4639-a49f-ac987283750c)
