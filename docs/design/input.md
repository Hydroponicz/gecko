# Input & Controls — Design

Status: **draft**

## Settled decisions
- Full controller support is a first-class requirement (primary test pad: PS5 DualSense), alongside keyboard & mouse.

## Rules for every system
- All input goes through Godot's **Input Map actions** (`interact`, `pick_up`, `place`, `rotate`...). No hard-coded keys or buttons anywhere.
- Every menu and UI screen must be fully navigable with a controller (focus-based navigation, no mouse-only interactions).
- Button prompts on screen switch automatically between keyboard and PlayStation glyphs depending on the last device used.
- Machine placement aims from the player/camera and snaps to the grid, so it works with a stick as well as a mouse.

## DualSense feature support (researched for Godot 4.7.2)
| Feature | Status | How |
|---|---|---|
| Buttons, sticks, analog triggers | Native | Input Map actions |
| Lightbar color | **Native** | `Input.set_joy_light(device, color)` / `has_joy_light()` |
| Rumble | **Native** (classic two-motor rumble) | `Input.start_joy_vibration(device, weak, strong, duration)` |
| Gyro + accelerometer | **Native** (new in 4.7) | `set_joy_motion_sensors_enabled()`, `get_joy_gyroscope()`, calibration API |
| Adaptive triggers | Not native | Needs a GDExtension/plugin (e.g. DualSenseX-Support, Godot-Dualsense prototype). Evaluate when we add guns. |
| HD audio haptics | Not native | Needs a plugin (e.g. "Audio Haptics" GDExtension) |
| Touchpad | Not native | Needs a plugin; may get a basic button press natively |
| Speaker / mic | Not native | Low priority |

Plan: use native features now (lightbar, rumble, gyro aiming later). Evaluate a plugin for adaptive
triggers when the gun system is built: their state is unknown and adding one is a commitment.

Lightbar ideas: flash red/blue while wanted by police, show heat level, crew colour, low health.

## Later
- Control remapping menu.
