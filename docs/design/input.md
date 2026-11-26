# Input & Controls — Design

Status: **draft**

## Settled decisions
- Full controller support is a first-class requirement (primary test pad: PS5 DualSense), alongside keyboard & mouse.

## Rules for every system
- All input goes through Godot's **Input Map actions** (`interact`, `pick_up`, `place`, `rotate`...). No hard-coded keys or buttons anywhere.
- Every menu and UI screen must be fully navigable with a controller (focus-based navigation, no mouse-only interactions).
- Button prompts on screen switch automatically between keyboard and PlayStation glyphs depending on the last device used.
- Machine placement aims from the player/camera and snaps to the grid, so it works with a stick as well as a mouse.

## Later / unconfirmed
- DualSense-specific features (adaptive triggers, lightbar, advanced haptics) likely need a plugin; basic rumble may work out of the box. To be checked in Godot when we get there.
- Control remapping menu.
