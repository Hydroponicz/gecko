# NPC Rules — Design

Status: **standing rules** for every NPC that moves (workers now; civilians, police, gangs, crew later).

## Never stuck (fail-safe standard)
Every moving NPC must use `StuckWatchdog` (`src/AI/StuckWatchdog.cs`):
- The NPC reports **progress** only when it does something meaningful: gets **closer** to its goal,
  finishes an action, or is legitimately idle/waiting. Wandering back and forth does not count.
- **2 minutes** without progress: teleport to the **nearest pathfinding-safe spot** nearby and reset
  its current task.
- **Immediate** rescue when the NPC is somewhere it can't legally be:
  - inside geometry (e.g. a machine placed on it, a car parked into it), or
  - boxed in: cut off from the safe area (e.g. machines built around it).
- "Safe area" = navigable space connected to the main walkable area (inside a property: everything
  connected to the doorway; in the city: the main navmesh region / sidewalk network).
- Teleports should prefer spots out of the player's view once camera checks exist (civilians in the city).

## Layered fail-safes (workers, as implemented)
1. Blocked 0.6s: re-plan around people.
2. Blocked 2.5s: lower-priority NPC steps aside (newer gives way; everyone gives way to the player).
3. No movement 8s while walking: teleport to safety and restart the task.
4. Inside geometry or boxed in: teleport to the safe area immediately.
5. Target unreachable but the NPC itself is fine: "Can't reach X", retry every 3s (not a teleport).
6. Destination full 15s: give up and return items to the Stash.
7. Blocked by the player: wait politely, keep a body-width gap, step away and route around;
   only the 2-minute rule teleports. NPCs never lose collision (immersion).
8. Anything else: 2-minute watchdog.

## City NPCs (later)
Civilians etc. will use Godot's navigation mesh instead of the building grid, but follow the same rules:
the watchdog, "closer to goal" progress, and teleport to the nearest safe navmesh point.
