# Vehicles & Selling — Design

Status: **5a built (awaiting test)**, 5b agreed in principle.

## 5a: Vehicles
- Arcade, GTA-like handling (Godot VehicleBody3D tuned for arcade): speed-sensitive steering,
  handbrake slides, downforce, top speed limit. All numbers exported on the car for tuning.
- Fail-safe: a car stuck on its side/roof for 3s is set back on its wheels.
- Controls: get in/out Triangle/Y/F; gas R2/W; brake/reverse L2/S; steer left stick/A-D;
  handbrake Square/X/Space; camera right stick/mouse (re-centres behind the car after 1.5s).
- Starter car: a beat-up sedan parked on the avenue by the warehouse loading bay.
- Can only get out below ~10 km/h. Exit by the driver's door, else other side, else behind.

### Trunk (general container, weapon-ready)
- Space-based: most items 1, duffel bag 6. Sedan: 20 space (20 bricks, or 3 bags + 2 bricks).
- Target the back of the car on foot: Load what you carry / Take each stored kind (tap 1, hold all).
- Cargo is visible on the rear deck. Weapons will be storable here once combat exists.

### Targeting from a vehicle
- In a vehicle L2 is the brake, so targeting is **D-pad up** (right mouse on keyboard).
- Same menu and camera lock-on as on foot. Only options that don't need walking over (people)
  work from a vehicle; machines, shelves, phones need you on foot.

### Drive-in loading bays (warehouses)
- Roll-up opening in the warehouse's right wall onto a marked LOADING BAY (6 x 4 cells).
- No machines can be built in the bay; idle workers don't stand there.
- A vehicle inside the building blocks worker paths; workers walk around it (fail-safes apply).

## 5b: Selling (agreed, not built)
- Arrange sales by phone; the NPC randomly picks the meeting place and time window.
- Buyers: small amounts (1-5), high price (~$1,000/brick), many trips, riskier once police/gangs
  exist (may not show, haggle, or rob you).
- Distributors: bulk (10+), lower price (~$700/brick), one trip, reliable; bulk needs a bigger vehicle.
- Paid in dirty cash: wallet first, overflow into carried duffel bags.
- Per-buyer daily demand limits. Heat and automated drivers/guards come after police.
