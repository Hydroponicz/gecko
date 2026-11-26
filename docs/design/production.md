# Production System — Design

Status: **draft** (paper design, nothing implemented)

## Core concept
Businesses work like the classic open-world crime-game business loop
(buy a property → get supplies → produce stock → sell it), **but production is a
hands-on factory**: the property's interior is an empty shell the player lays out
and optimizes Satisfactory-style.

| Business-loop piece | How it works here |
|---|---|
| Buy the property    | Fixed location on the city map; interior starts empty |
| Supplies            | Buy them (delivered to the Stash) or steal them in supply jobs |
| Production          | Player-built machine layout + hand-carry / worker routes |
| Stock               | Finished product accumulates in the Stash |
| Selling             | Product is sold for dirty money (sell system TBD) |
| Upgrades            | Better machines, more/better workers, security (TBD) |

## Settled decisions
- The player physically walks inside production facilities and places machines.
- Machines are always powered (no power/electricity system for now).
- Transport between machines:
  1. **Early game:** the player hand-carries items between machines.
  2. **Later:** the player hires criminals to work the facility (automation -> passive income).
     Workers are **automatic** (see Workers): they split the layout's jobs between them.
  4. **No worker crowds:** a full basic chain should run with roughly 4 workers, not dozens.
     Scaling up comes from better carrying (e.g. hand truck / dolly unlocks), not from piling on more workers.

## Placement
- A bought property's interior is an empty floor with a grid.
- Machines can be placed **anywhere** on the grid (free layout); walking distance makes layout matter.
- Placed machines can be picked up and moved at any time for a **full refund**.

## The Stash (facility entrance)
Every production building has one **Stash** at its entrance. It holds:
- **Raw materials** — supplier deliveries land here (Paper, Ink).
- **Dirty money for wages** — workers are paid from it automatically each in-game day; if it runs dry, they walk off.
- **Finished product** — Counterfeit Bricks from the Packing Table are moved into the Stash automatically.

The Stash is also a valid worker route point (e.g. "Stash → Printing Press").

## Workers (automatic)
No manual routes. The game works out the **jobs** from the layout: every "move items from A to B"
connection (e.g. Stash -> Printing Press, Printing Press -> Drying Rack, ...). Jobs are split evenly:
job *j* goes to worker *j mod (number of workers)*. 1 worker does everything; at the cap each worker
has one job. Each worker always does their most urgent job (destination with the most room).

| Topic | Rule |
|---|---|
| Worker cap | min(jobs in the layout, floor area / 35 m^2). Current warehouse: 4 |
| Hiring | Phone (pay phone or landline): "Hire a worker", $200/day, arrives ~10s later |
| Firing | Target the worker: "Let go" (they drop items at the Stash and walk out) |
| Wages | 6:00 AM every game day from the property's cash table; unpaid workers quit |
| Clock | 1 game day = 48 real minutes; shown in the money pop-up |
| Carrying | Same rules as the player: one type, up to 5, same take/give action |
| Stats | None yet; all workers identical |

### Pathfinding and fail-safes
Workers path on the building grid (A*), avoid each other and the player, and reserve the next cell.
1. Blocked 0.6s: re-plan around people.
2. Blocked 2.5s: the lower-priority worker steps aside (newer hires give way; everyone gives way to the player).
3. No progress 8s: teleport to the nearest free safe cell and restart the task (keeps carried items).
4. Inside a machine (placed on them): teleport out immediately.
5. Destination unreachable: status "Can't reach X", retry every 3s.
6. Destination full for 15s: take the items back to the Stash.
7. Blocked by the player: wait politely ("Waiting for you to move"); not counted as quickly stuck.
8. Boxed in by machines (around, not on top): teleport into the area connected to the door.
9. Universal: no progress toward the goal for 2 minutes -> teleport to safety (see npc.md).
The doorway is kept clear (no machines) so workers can always get in and out.

## Chain 1: Counterfeit Cash

```
[Paper] ─┐
         ├─► PRINTING PRESS ─► [Wet Sheet] ─► DRYING RACK ─► [Dry Sheet] ─► CUTTER ─► [Bill Bundle] ─► PACKING TABLE ─► [Counterfeit Brick]
[Ink] ───┘
```

**Design rule (changed 2026-11-26): everything is 5.** Every machine holds up to 5 of each input
(counting what it's working on) and 5 of its output, and you carry up to 5 of any item. The game is
about immersion and fun, not ratio optimisation.

| Machine        | Input            | Output              | Time  | Notes |
|----------------|------------------|---------------------|-------|-------|
| Printing Press | 1 Paper + 1 Ink  | 1 Wet Sheet         | 10s   | |
| Drying Rack    | 1 Wet Sheet      | 1 Dry Sheet         | 30s   | Hangs up to 5 sheets, each drying on its own timer |
| Cutter         | 1 Dry Sheet      | 1 Bill Bundle       | 5s    | |
| Packing Table  | 5 Bill Bundles   | 1 Counterfeit Brick | 8s    | Brick = banded block of banknotes; goes to the Stash by itself |

So 5 Paper + 5 Ink = 1 brick. Durations are placeholders. Live values: `src/Production/Machines.cs`.

### Carry limits
Up to 5 of one item type at a time.

### Build mode
- Only available while standing inside a facility (frees the button elsewhere).
- Keyboard: Tab toggle, wheel = change machine, R rotate, LMB place, E pick up/move, X remove.
- Build mode keeps direct aim controls (no targeting menu).
- Controller: Touchpad/View toggle, D-pad left/right change, R1/RB rotate, Cross/A place,
  Square/X pick up/move, Circle/B remove.
- Removing a machine puts its contents in the Stash.

### Interaction (RDR2-style targeting)
- Hold **Target** (L2 / LT / right mouse) to lock onto the object the camera faces; it highlights.
- A menu lists up to 4 options, each on its own button
  (Square/Triangle/Circle/Cross, X/Y/B/A, E/F/Q/G).
- Unavailable options stay visible, greyed out, with the reason ("none in hand", "machine full"),
  so players learn what each machine needs.
- Target picks the **closest** object to the player (nearest edge), not where the camera points.
- **Tap** an option = move 1 item. **Hold** it (~0.45s, a bar fills) = move as many as possible.
- Choosing an option **locks the player into an action**: the character walks to the object,
  turns to it and performs the action (placeholder lean until real animations); items transfer at
  the moment of the put-down. Movement input is ignored until it finishes. If the way is blocked
  for 3s, it's done from where they stand (if in reach) or cancelled.
- The same Target button will aim weapons in combat and give NPC options later.
- The Stash is a row of 4 shelves inside the door; each shelf holds one item type (50 max).

### Rules
- Each machine has a small input buffer and output buffer; it stops when output is full.
- Raw inputs (Paper, Ink) are bought from a supplier and delivered to the Stash.
- Counterfeit Bricks are product, not money: sold to a fence for dirty money (selling system TBD).

## Future vision (not in scope yet)
Many property types, from small to huge, e.g.:
- RVs converted into mobile meth labs
- Small basements / garages
- Massive warehouses
- Private mansions

Architecture note: a property's interior and its production state must not depend on
being at a fixed map location, so a vehicle (RV) can host one later.

## Open questions
- Buffer sizes per machine.
- Offline progress: does production continue while the game is closed?
