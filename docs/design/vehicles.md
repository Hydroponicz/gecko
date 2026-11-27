# Vehicles & Selling — Design

Status: **5a built (awaiting test)**, 5b agreed in principle.

## 5a: Vehicles
- Arcade, GTA-like handling (Godot VehicleBody3D tuned for arcade): speed-sensitive steering,
  handbrake slides, downforce, top speed limit. All numbers exported on the car for tuning.
- Cars never right themselves. A flipped car can't be driven: crawl out (Triangle / Y / F), then target it
  and choose "Push it back over" (walk to its side, ~2s shove, a real physics push).
- Getting in: Triangle / Y / F finds the nearest free car within ~6 m; your character walks to the
  driver's door and gets in (moving the stick cancels), walking around the front or back if you're on
  the other side. If the driver's door is blocked (or you get stuck on the way), you get in through the
  passenger door and slide across (~1s, can't drive until seated). Behind a car, the same button opens/closes the trunk.
- Engine force is per driven wheel (Godot applies it to each wheel): 1,900 per wheel x 4, car mass 1,500 kg.
- Stability lessons: wheel roll influence 1.0 = resists roll, 0.0 = rolls over (we use 0.75);
  suspension damping is scaled by mass in Godot, so ~2.5 compression / 3.2 rebound for a settled ride.
  All suspension values are exported under "Suspension" on the car for tuning.
- Temporary speedometer (km/h) on screen while driving, for tuning.
- Controls: get in/out Triangle/Y/F; gas R2/W; brake/reverse L2/S; steer left stick/A-D;
  handbrake Square/X/Space; camera right stick/mouse (re-centres behind the car after 1.5s).
- Starter car: a beat-up sedan parked on the avenue by the warehouse loading bay.
- Can only get out below ~10 km/h (or when flipped). Exit by the driver's door, else other side, else behind.

### Trunk (general container, weapon-ready)
- Space-based: most items 1, duffel bag 6. Sedan: 20 space (20 bricks, or 3 bags + 2 bricks).
- Stand behind the car and press Triangle / Y / F to **open the trunk** (lid swings up); only an open
  trunk can be targeted. Then: Load what you carry / Take each stored kind (tap 1, hold all).
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
  exist (may not show, haggle, or rob you). Each buyer only buys so much, then goes on a long cooldown.
- Distributors: bulk (10+), lower price (~$700/brick), one trip, reliable, **never on cooldown**;
  bulk needs a bigger vehicle.

## Long-term vehicle goals
- Weighty, GTA IV-like handling (mass, body roll, weight transfer).
- Strong damage, planned as a **crash cage**: driving stays rigid; a coarse soft-body cage (50-100 nodes)
  simulates only during impacts and deforms the visible mesh; plus detachable parts and mechanical damage.
  Full BeamNG-style node-beam for every car was judged too costly (months; few cars at once).
- Keep car code split into driving / visuals / damage so the cage can be added later without rewrites.
- Asset rule: cars from Meshy must come as separate parts (body, doors, hood, trunk lid, bumpers, wheels, windows).
- Paid in dirty cash: wallet first, overflow into carried duffel bags.
- Per-buyer daily demand limits. Heat and automated drivers/guards come after police.
