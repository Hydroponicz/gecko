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
- Raw inputs (Paper, Ink) are bought from a supplier and dropped at the facility door.
- Counterfeit Bricks are product, not money: sold to a fence for dirty money (selling system TBD).

## Open questions
- Buffer sizes per machine.
- Worker details: carry capacity, waiting behavior, wages, hiring.
- Offline progress: does production continue while the game is closed?
