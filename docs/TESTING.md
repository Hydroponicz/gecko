# How to test the current build

1. Open Godot 4.7.2, **Import** the repo folder (select `project.godot`), and press **F5** (Run Project).
2. You spawn in a gray test area with crates and a ramp.

## Step 1 checklist: player + controller
- [ ] Keyboard/mouse: WASD moves relative to the camera; mouse looks; Shift sprints; Space jumps.
- [ ] DualSense: left stick moves (gentle tilt = slow walk), right stick looks, Cross jumps, click L3 to sprint.
- [ ] Top-left text switches between "Keyboard & Mouse" and "Gamepad" when you swap.
- [ ] Top-left text lists your DualSense and shows lightbar/rumble/motion as `true`.
- [ ] Lightbar turns amber on start; T / Triangle cycles its colour and gives a short rumble.
- [ ] Camera doesn't clip through crates (it pulls in when blocked).
- [ ] Jumping off the tall crate stack gives a small rumble on landing.
- [ ] Esc / Options releases the mouse; clicking recaptures it.

Report anything that feels off: speeds, camera distance, sensitivity, etc.
