# How to test the current build

1. Open Godot 4.7.2 **.NET**, **Import** the repo folder (select `project.godot`), and press **F5** (Run Project). The C# project builds automatically on first run.
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

## Step 2 checklist: factory gray box
The warehouse is the brick-walled room behind the spawn (walk forward-right). The door is on the side facing spawn.

**Targeting & carrying** (outside build mode)
- [ ] Near a shelf or machine, the bottom prompt shows `[RMB / L2 / LT] Interact`.
- [ ] Hold RMB / L2 / LT: the object you're facing lights up and a menu appears bottom-right.
- [ ] Each option shows its button (E/F/Q/G, Square/Triangle/Circle/Cross, X/Y/B/A).
- [ ] Options you can't do are greyed out with a reason (e.g. "Load Ink (none in hand)").
- [ ] **Tap** an option: exactly one item moves (you can take ink one at a time).
- [ ] **Hold** an option: a bar fills, then as many items as possible move.
- [ ] Stash shelf shows both "Take" and "Store", so you can top up what you're carrying.
- [ ] You can't jump with Cross/A while targeting.
- [ ] Only one item type can be carried; carried items stack in front of you.
- [ ] Counterfeit bricks look like green banded bricks of banknotes.

**Build mode**
- [ ] The build prompt (Tab / Touchpad / View) only appears inside the warehouse.
- [ ] In build mode: grid appears, see-through preview follows where the camera aims, green = OK, red = blocked.
- [ ] Wheel / D-pad left-right changes machine; R / R1 / RB rotates; LMB / Cross / A places (no jumping).
- [ ] Aim at a placed machine: E / Square / X picks it up to move it; X / Circle / B removes it.
- [ ] Can't place on the Stash, outside the room, on other machines, or where you stand.
- [ ] Leaving the room (or Tab / Touchpad) exits build mode; a picked-up machine returns to its spot.

**The chain**
- [ ] Press: load Paper and Ink -> makes Wet Sheets (10s each).
- [ ] Drying Rack: load Wet Sheets -> up to 6 dry at once (30s each) -> Dry Sheets.
- [ ] Cutter: Dry Sheet -> 4 Loose Bills (5s).
- [ ] Packing Table: 10 Loose Bills -> Counterfeit Brick (8s) which appears in the Stash by itself.
- [ ] Each machine's label shows inputs, progress and output; finished items sit on top of the machine.

Nothing is saved yet: the factory resets every run.
