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
  2. **Later:** the player hires criminals to work the facility (automation → passive income).
     Workers use the **assigned routes** model: each worker is given a route
     ("take from machine A, put into machine B") and physically walks it, acting as a living conveyor.
     Layout and walking distance therefore matter.
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

## Workers
| Topic          | Rule |
|----------------|------|
| Route          | One pickup point → one drop-off point |
| What they carry| Whatever the drop-off point accepts (e.g. on "Stash → Press" a worker brings Paper *and* Ink) |
| Blocked        | Pickup empty or drop-off full → worker waits (shows the bottleneck) |
| Wages          | Dirty money, paid daily from the Stash; unpaid → they quit |
| Carry limit    | Same per-item limits as the player (see Carry limits) |
| Hiring         | Contact menu for now; later the crew/hierarchy system |
| Stats          | None yet — all workers identical |
  3. **Conveyors:** not planned unless they turn out to make sense.
- Passive ("idle") income comes from automated facilities.

## Chain 1: Counterfeit Cash

```
[Paper] ─┐
         ├─► PRINTING PRESS ─► [Wet Sheet] ─► DRYING RACK ─► [Dry Sheet] ─► CUTTER ─► [Loose Bills] ─► PACKING TABLE ─► [Counterfeit Brick]
[Ink] ───┘
```

| Machine        | Input            | Output              | Time  | Notes |
|----------------|------------------|---------------------|-------|-------|
| Printing Press | 1 Paper + 1 Ink  | 1 Wet Sheet         | 10s   | |
| Drying Rack    | 1 Wet Sheet      | 1 Dry Sheet         | 30s   | Holds 6 sheets at once, each dries on its own timer — a capacity puzzle, not a speed one |
| Cutter         | 1 Dry Sheet      | 4 Loose Bills       | 5s    | |
| Packing Table  | 10 Loose Bills   | 1 Counterfeit Brick | 8s    | |

All numbers are placeholders to be tuned in play. Live values: `src/Production/Machines.cs`.

### Carry limits (per item type, one type at a time)
| Item | Max carried |
|---|---|
| Paper, Ink, Wet Sheet, Dry Sheet | 5 |
| Loose Bills | 20 (small bundles) |
| Counterfeit Brick | 4 (boxes) |

### Build mode
- Only available while standing inside a facility (frees the button elsewhere).
- Keyboard: Tab toggle, wheel = change machine, R rotate, LMB place, E pick up/move, X remove.
- Controller: Touchpad/View toggle, D-pad left/right change, R1/RB rotate, Cross/A place,
  Square/X pick up/move, Circle/B remove.
- Removing a machine puts its contents in the Stash.

### Interaction
- Tap interact = move one item; hold = keep moving them. Direction (give/take) is locked for the hold.
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
