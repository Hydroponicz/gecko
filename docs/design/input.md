# Input & Controls — Design

Status: **draft**

## Settled decisions
- Full controller support is a first-class requirement (primary test pad: PS5 DualSense), alongside keyboard & mouse.

## Rules for every system
- All input goes through Godot's **Input Map actions** (`interact`, `pick_up`, `place`, `rotate`...). No hard-coded keys or buttons anywhere.
- Every menu and UI screen must be fully navigable with a controller (focus-based navigation, no mouse-only interactions).
- Button prompts on screen switch automatically between keyboard and PlayStation glyphs depending on the last device used.
- Machine placement aims from the player/camera and snaps to the grid, so it works with a stick as well as a mouse.

## Targeting (RDR2-style)
Hold **Target** (L2 / LT / right mouse) to lock onto an object or (later) an NPC, then choose an
option with the face buttons / E F Q G. Tap = one item, hold = all. In combat the same button aims.

## Supported controllers
Godot reads buttons and sticks for **all** controllers (Xbox, PlayStation, Steam Input, generic),
using its built-in SDL controller database. We do not write our own button reading:
it would add a lot of work for no new features.

## Button prompts
On-screen prompts always match the device the player last touched.

| Glyph set | Used for |
|---|---|
| Keyboard & mouse | Any key/mouse input |
| Xbox | Xbox controllers, and the default for unknown pads |
| PlayStation | DualSense / DualShock 4 (detected by name/vendor ID) |
| Steam | Steam Controller / Steam Deck, via Steamworks Steam Input glyphs once we ship on Steam |

Notes:
- Steam controllers use the Xbox A/B/X/Y layout, so Xbox glyphs are a correct fallback.
  Steam Input presents controllers to the game as a virtual Xbox pad; real Steam glyphs need the
  Steamworks Steam Input API (later, with Steam integration).
- Steam Input can also wrap a DualSense and hide its extra features; revisit when we add Steam.
- Prompts are built from action names, never hard-coded button names.

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

## Custom DualSense layer (planned)
Decision: write our **own** DualSense code instead of third-party plugins, and have it be the
**only** thing that sends commands to the controller (lightbar, rumble, triggers, haptics), so two
programs never fight over it.

- Language: **C#** inside the game project, talking to the controller over raw HID
  (e.g. via the managed HidSharp library). No C++ needed.
- Platform: **Windows** first. Must support **USB and Bluetooth** (Bluetooth output reports need a CRC32).
- Features: adaptive triggers, touchpad, lightbar, player LEDs, rumble, mic-mute LED, gyro.
- HD haptics: **USB only** (the controller is a 4-channel USB audio device; channels 3-4 drive the
  haptic motors). Bluetooth HD haptics is not feasible. Fallback over Bluetooth: classic rumble.
- Godot keeps reading buttons/sticks (keeps Input Map, remapping, other controller brands working).
- All of it sits behind `Gamepad` (src/Core/Gamepad.cs), so gameplay code never changes.
- Consoles (far future): platform SDKs provide these features natively; the HID layer is PC-only.
- Risk to test: Godot's own controller driver initialising the lightbar when the pad connects.

Lightbar ideas: flash red/blue while wanted by police, show heat level, crew colour, low health.

## Later
- Control remapping menu.
