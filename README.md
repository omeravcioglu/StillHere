# Still, Here

**An atmospheric first-person experience made in Unity 6.** You lie in a bed on a hospital ward and cannot move. You can blink, and you can slow time down or speed it up, while the ward carries on around you.

---

## The experience

- **A fixed point of view:** the camera sits at the head of a hospital bed, about a metre off the floor and tilted up, looking across a ward with two rows of beds. There are no movement or look controls.
- **The ward carries on:**
  - Two women stand talking near the foot of your bed.
  - A man sleeps on a stretcher across the aisle.
  - A man in a suit walks slowly up and down the ward, pausing for about ten seconds at each end.
- **Blink** (Space, left mouse button or gamepad A): tap for a quick blink, hold to keep your eyes shut. Upper and lower eyelids close over the screen in an almond shape and open more slowly after a long close. You also blink on your own, roughly every 43 seconds.
- **Time flow** (hold Q to slow down, E to speed up; gamepad left / right trigger): the world slows to 0.3× or speeds up to 2.5×, then drifts back to normal when you let go. It is never a pause: the walker and the character animations keep moving, only the pace changes. Blinking runs on real time, so it feels the same at any speed.
- **Focus** (mouse or left stick): marks where your attention is. The audio focus system is built to make sounds near that point louder and clearer and to muffle the rest (see Status).
- **Sound:** three looping ambience layers, a respirator at full volume over two quiet hospital-ambience tracks.
- There is no HUD, objective or ending: it is one continuous scene.

**Status:** prototype, last worked on in February 2026. The ward scene is set up with blinking, time control, the four characters and the ambience. Three pieces are written but not in use yet: audio focus (no sound in the scene is registered with it: `FocusableAudioSource` is declared inside `AudioFocusSystem.cs`, so Unity can't attach it from the Inspector, and no code adds it), `BreathingCamera` (a breathing sway for the camera) and `LightFlicker` (six flicker styles, from a faint fluorescent hum to a dying bulb).

## Tech stack

| Area | What it uses |
|---|---|
| Engine | **Unity 6** (6000.0.32f1), **Universal Render Pipeline** 17 |
| Input | Unity **Input System** 1.11: one `Presence` action map (focus, blink, time) with keyboard & mouse and gamepad schemes, also set as the project-wide actions |
| Rendering | URP, a hand-written HLSL eyelid shader, baked reflection probes in the ward scene |
| UI | uGUI `RawImage` overlay for the eyelids; IMGUI for the debug panel |
| Audio | Unity audio: looping `AudioSource` layers, a per-source `AudioLowPassFilter` for focus, output set to 48 kHz stereo |
| Animation | Mecanim `Animator` controllers with one animation state per character; the walker is moved by script with root motion off |

> AI Navigation, Timeline, Visual Scripting and Multiplayer Center are installed but not used by the scene or the code.

## What I built

All of my code lives in **`Assets/Scripts/`**: 11 C# scripts, about 2.8k lines. The eyelid shader (`Assets/Shaders/EyelidBlink.shader`) and the input actions (`Assets/StillHereInputActions.inputactions`) are mine too.

| System | Key scripts |
|---|---|
| Blink (hold-to-close eyelids, natural timing, automatic blinks, eyelid overlay shader) | `BlinkController`, `EyelidBlink.shader` |
| Time flow (slow down or speed up the world, easing back to normal) | `TimeFlowController` |
| Input (focus point and ray, blink hold and events, time value; switches between mouse and gamepad) | `StillHereInput`, `StillHereInputActions` |
| Audio focus (sounds near your focus get louder and clearer, the rest are muffled; voices get a boost) | `AudioFocusSystem`, `FocusableAudioSource` |
| Audio setup and ambience (stereo output for headphones; looping, interval and trigger-zone sounds with fades and variation) | `SpatialAudioSetup`, `EnvironmentSound` |
| Bootstrap (finds or adds the core systems) and hooks for scripted moments | `StillHereManager` |
| People moving through the ward (looping, ping-pong or one-way paths) | `WaypointMover` |
| Atmosphere, written but not placed in the scene yet (camera breathing, flickering lights) | `BreathingCamera`, `LightFlicker` |
| Debug panel for input, eyelid darkness and time scale (disabled in the scene) | `InputDebugger` |

### Code highlights

- **`Assets/Scripts/Core/BlinkController.cs`** + **`Assets/Shaders/EyelidBlink.shader`:** the blink is a four-phase state machine on unscaled time: an 80 ms ease-in close, then a 120 ms open, or 400 ms if the eyes were held shut for more than half a second. The shader draws aspect-corrected, almond-shaped upper and lower lids with a soft edge and a darker lash line, and the overlay is switched off while the eyes are fully open.
- **`Assets/Scripts/Core/TimeFlowController.cs`:** maps the time input through an `AnimationCurve` to a target between 0.3× and 2.5×, eases `Time.timeScale` toward it on unscaled time, and scales `Time.fixedDeltaTime` with it to keep physics steady. It also exposes overrides and timed transitions for scripted moments.
- **`Assets/Scripts/Audio/AudioFocusSystem.cs`:** casts a ray from the camera through the focus point and scores each registered sound by its distance from that ray, scaled by depth, so the focus area widens like a cone. Each source then blends its volume and a low-pass cutoff between 800 Hz (unfocused) and 22 kHz (focused).
- **`Assets/Scripts/Input/StillHereInput.cs`:** one action map feeds every system. It follows whichever of the mouse and the gamepad stick moved last, turns the focus point into a world-space ray, and exposes the blink hold time and events plus a single -1 to 1 time value.
- **`Assets/Scripts/Core/StillHereManager.cs`:** finds or adds the systems on one GameObject and provides `TransitionWithBlink` (run a callback, such as a scene change, while the eyes are shut) and `EmotionalMoment` (ease time down to half speed). Nothing calls these hooks yet.

## Scenes (build order)

| # | Scene | Purpose |
|---|---|---|
| 0 | `Assets/HospitalPack/Hospital/DemoScene/Hospital.unity` | The whole experience: the hospital pack's demo map with my systems, characters and sounds added and the camera placed at the head of a bed |

It is the only scene in the build settings and uses 9 of the 11 scripts (all but `BreathingCamera` and `LightFlicker`). The core systems sit on the `StillHere` object, `Sounds`, `Sounds (1)` and `Sounds (2)` are the ambience layers, and `Suit_Man (1)` walks between `Waypoint1` and `Waypoint1 (1)`.

## Integrated third-party assets

The experience is built on these assets. Only the files the scene actually uses are included.

| Asset | Used for |
|---|---|
| Hospital pack (`Assets/HospitalPack`) | The hospital environment and props (beds, stretchers, IV drips, medical machines, MRI and CT scanners, furniture, walls). The scene started from its `Hospital` demo map. Prop materials use URP Lit. |
| Reallusion Character Creator characters (`Assets/Characters`) | The four people on the ward: `Woman_Talking_1`, `Woman_Talking_2`, `Man_Sleeping` and `Suit_Man`, with their talking, sleeping and walking animations. The suit man's walk cycle comes from the `Man Walking` model, which is included for that clip only. Materials use URP Lit. |
| Ambient sounds (`Assets/Sounds`) | The three ambience loops: hospital ambience, busy hospital ambience and a respirator |
| URP project template settings (`Assets/Settings`) | Render pipeline assets for the PC and Mobile quality levels, renderers and volume profiles |

Reallusion's CC/iC Unity tools were part of the working project but are not needed here (see below).

## About this repository

This public repository is a **showcase**. It contains the documentation and the **12 source files I wrote** for this project. The complete project, including licensed third-party assets that cannot be redistributed, is kept in a private repository.

Copyright © Omer Avcioglu (McHunter Studio). **All rights reserved.** Viewing only; see LICENSE.
