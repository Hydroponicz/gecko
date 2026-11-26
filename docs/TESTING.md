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
- [ ] Hold RMB / L2 / LT: the **closest** shelf/machine lights up and a menu appears bottom-right.
- [ ] Each option shows its button (E/F/Q/G, Square/Triangle/Circle/Cross, X/Y/B/A).
- [ ] Options you can't do are greyed out with a reason (e.g. "Load Ink (none in hand)").
- [ ] **Tap** an option: exactly one item moves (you can take ink one at a time).
- [ ] **Hold** an option: a bar fills, then as many items as possible move.
- [ ] Stash shelf shows both "Take" and "Store", so you can top up what you're carrying.
- [ ] You can't jump with Cross/A while targeting.
- [ ] Choosing an option: your character walks to the object, turns, leans in, and the items move
      as they lean in. You can't move until it's done (camera still works).
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
- [ ] Drying Rack: load up to 5 Wet Sheets, all dry at once (30s each) -> Dry Sheets.
- [ ] Cutter: Dry Sheet -> Bill Bundle (5s).
- [ ] Packing Table: 5 Bill Bundles -> Counterfeit Brick (8s) which appears in the Stash by itself.
- [ ] Every machine holds max 5 of each input and 5 output; you carry max 5.
- [ ] Each machine's label shows inputs, progress and output; finished items sit on top of the machine.

Nothing is saved yet: the factory resets every run.

## Debug menu (any level, editor runs only)
- F1 / R3 (click right stick) opens it. Arrow keys / D-pad up-down select, left-right change, Enter / Cross / A use.
- Give 5 of any item, drop carried items, refill the Stash, game speed x1/x2/x5/x10,
  fast-forward machines 60s, test lightbar + rumble (moved here from T / Triangle).
- Controller info (name, lightbar/rumble/motion support) is shown in the menu.

## Step 3 checklist: money & supplies
**Money display**
- [ ] Start with $500. Press Z / D-pad down: balances show top-right for a few seconds.
- [ ] Balances also pop up whenever money changes, with +/- amount.

**Ordering supplies**
- [ ] Pay phones: one near spawn, one outside the factory door. Hold L2 / LT / RMB near one.
- [ ] Menu: Order 5 Paper ($100), 5 Ink ($100), 5 of each ($200). Greyed out if you can't afford it.
- [ ] Ordering walks you to the phone; money is taken; the phone shows "Delivery in 0:30".
- [ ] After ~30s (or debug fast-forward) the supplies appear on the Stash shelves.
- [ ] Build mode now has a **Landline** (cycle past the Packing Table). Placed landlines order supplies too.

**Physical cash** (debug menu: "Give dirty cash" to get money)
- [ ] Give $15,000: wallet fills to $5,000, the rest appears as a duffel bag on your back.
- [ ] Cash Table (next to the Stash shelves): Unpack duffel bags -> stacks of cash appear on the table.
- [ ] Deposit from wallet / Take to wallet: tap = $1,000, hold = all (wallet max $5,000).
- [ ] Pack duffel bag: tap = one bag (up to $10,000), hold = as many as possible (max 3 bags).
- [ ] Carrying 3 bags: "weighed down", no sprint, no jump.
- [ ] Can't pack bags while carrying production items (hands full), and vice versa.

## Step 4 checklist: workers
**Hiring** (place all 4 machines first; give yourself cash with the debug menu)
- [ ] Phone shows "Hire a worker ($200/day - 0/4)". Cap is 4 with all machines; fewer machines = lower cap.
- [ ] Hiring: an orange worker walks in through the door ~10s later. Debug menu can hire instantly.
- [ ] Clock appears at the top of the money pop-up (Z / D-pad down).

**Automatic work** (supplies in the Stash)
- [ ] 1 worker: runs the whole chain alone (Stash -> Press -> Rack -> Cutter -> Packing).
- [ ] 2 workers: split the work (one does Stash->Press + Rack->Cutter, the other the rest).
- [ ] 4 workers: one job each. Bricks appear in the Stash with no help from you.
- [ ] Labels above workers show what they're doing; they lean in to take/give like you do.

**Avoidance & fail-safes**
- [ ] Workers walk around machines, each other, and you. You bump into them (they're solid).
- [ ] Stand in a narrow gap: they wait ("Waiting for you to move") and continue when you move.
- [ ] Build mode: place a machine on a worker -> they pop out to a free spot and carry on.
- [ ] Wall a machine in completely -> "Can't reach the ..." until you open a path.
- [ ] Build machines all around a worker (not on them) -> they pop out to the open area within ~1s.
- [ ] Stand in the only gap for 2+ minutes -> the waiting worker is moved to a safe spot.
- [ ] Machines can't be placed in the doorway.

**Wages**
- [ ] Put cash on the cash table, debug "Skip to 5:59 AM": at 6:00 a payday message shows; $200 per worker leaves the table.
- [ ] Not enough cash: unpaid workers quit and walk out.
- [ ] Target a worker -> "Let go": they drop their items at the Stash and leave.

## Step 4 fixes (round 2)
- [ ] Workers taking Ink/Paper walk to that exact shelf (not the cash table).
- [ ] 1 worker: moves Bill Bundles to the Packing Table / unblocks full machines before restocking the press.
- [ ] Holding Target: the camera swings to the target and follows a moving worker; release to get control back.
- [ ] Let a worker go while standing right next to them: no walking over, they drop items at the Stash,
      step around you and walk out (still solid, nobody gets wedged).
