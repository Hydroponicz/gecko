# Production System — Design

Status: **draft** (paper design, nothing implemented)

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
| Carry capacity | 5 items per trip (player carries 5 too) |
| Blocked        | Pickup empty or drop-off full → worker waits (shows the bottleneck) |
| Wages          | Dirty money, paid daily from the Stash; unpaid → they quit |
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
| Drying Rack    | 1 Wet Sheet      | 1 Dry Sheet         | 30s   | Holds 6 sheets at once (placeholder), each dries on its own timer — a capacity puzzle, not a speed one |
| Cutter         | 1 Dry Sheet      | 4 Loose Bills       | 5s    | |
| Packing Table  | 10 Loose Bills   | 1 Counterfeit Brick | 8s    | |

All numbers are placeholders to be tuned in play.

### Rules
- Each machine has a small input buffer and output buffer; it stops when output is full.
- Raw inputs (Paper, Ink) are bought from a supplier and delivered to the Stash.
- Counterfeit Bricks are product, not money: sold to a fence for dirty money (selling system TBD).

## Open questions
- Buffer sizes per machine.
- Offline progress: does production continue while the game is closed?
