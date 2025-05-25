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

> 📝 Note: This feature currently works **ONLY FOR FAVOURITED** avatars!  
*See [this related VRC feedback ticket](https://feedback.vrchat.com/avatar-30/p/1626-osc-avatar-change-is-not-working) – please upvote if it's relevant to you.*

![Features inside module settings](https://github.com/user-attachments/assets/bb105102-5336-45ff-ad27-8edc17b10269)

---

### 🔊 Squeak Meter

**Squeak Meter** listens to your system’s default audio output and analyzes sound in real time. It sends OSC parameters for overall volume, stereo balance, and the normalized amplitudes of bass, mid and treble frequency bands.

#### ✨ Features

- 🎧 **Real-time audio analysis** of your system’s default output
- 📊 **Customizable gain and smoothing**
- 📡 **Customizable OSC parameter output** for:
  - **Volume** – Overall loudness (`0.0 – 1.0`)
  - **Direction** – Stereo balance (`0.0 = left`, `0.5 = center`, `1.0 = right`)
  - **Bass** – Amplitude in the `0 – 250 Hz` range
  - **Mid** – Amplitude in the `250 – 4000 Hz` range
  - **Treble** – Amplitude in the `4000 – 20000 Hz` range

> 💡 Tip: Use the built-in sliders to adjust sensitivity and smoothing for each band, so you can tailor the response to your specific setup or creative goals.

![Window for both module settings and parameters](https://github.com/user-attachments/assets/43d919ba-de6f-4aa5-a3c0-5a3d09e92561)
