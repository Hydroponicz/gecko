using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gecko.Production;

/// <summary>
/// Workers: hired by phone, paid daily, and fully automatic. The layout's jobs (<see cref="JobLinks"/>)
/// are split evenly between workers: 1 worker does every job, at the cap each worker has one job.
/// Movement avoids other workers and the player, with fail-safes so nobody stays stuck.
/// </summary>
public sealed partial class FacilityState
{
    public const int WorkerWage = 200;
    public const int SquareMetresPerWorker = 35;
    public const float HireArrivalSeconds = 10f;

    // Tunables
    private const float WalkSpeed = 2.5f;          // cells per second
    private const float ActionSeconds = 0.8f;      // take / give
    private const float ReplanAfter = 0.6f;        // blocked this long: plan around people
    private const float SidestepAfter = 2.5f;      // blocked this long: lower priority steps aside
    private const float TeleportAfter = 8f;        // no progress this long: teleport to safety
    private const float RetryAfterUnreachable = 3f;
    private const float GiveUpWaitingAfter = 15f;  // destination full this long: return items to Stash
    private const int MaxIdleWalk = 6;             // idle workers wait close to where they finished

    private readonly List<WorkerState> _workers = [];
    private int _nextWorkerId = 1;
    private Vector2? _playerPosition; // grid units
    private readonly Random _random = new(1234);

    public IReadOnlyList<WorkerState> Workers => _workers;
    /// <summary>Where workers arrive and leave (inside the doorway).</summary>
    public Vector2I DoorCell => DoorArea.Position;

    public event Action<WorkerState>? WorkerAdded;
    public event Action<WorkerState>? WorkerRemoved;

    /// <summary>Most workers this property supports: one per job in the layout, limited by floor area.</summary>
    public int WorkerCap => Math.Min(JobLinks().Count, Math.Max(1, Size.X * Size.Y / SquareMetresPerWorker));

    /// <summary>Workers keep at least this far from the player (centre to centre, in cells) so bodies never overlap.</summary>
    private const float PlayerClearance = 0.9f;

    /// <summary>Player radius + worker radius + a little margin, in cells (1 cell = 1 m).</summary>
    private const float PlayerBodyGap = 0.72f;

    /// <summary>Where the player stands, in grid units (or null when outside). Workers keep clear of them.</summary>
    public void SetPlayerPosition(Vector2? position) => _playerPosition = position;

    // ---- Hiring and wages ----

    public WorkerState? HireWorker(float arrivalDelay = HireArrivalSeconds)
    {
        if (_workers.Count(w => w.Phase != WorkerPhase.Leaving) >= WorkerCap)
            return null;
        var worker = new WorkerState(_nextWorkerId++, CellCenter(DoorCell), arrivalDelay);
        _workers.Add(worker);
        WorkerAdded?.Invoke(worker);
        return worker;
    }

    /// <summary>Lets a worker go: they drop what they carry at the Stash and walk out.</summary>
    public void FireWorker(WorkerState worker, string reason = "Let go")
    {
        if (worker.Phase == WorkerPhase.Leaving)
            return;
        if (worker.Carry.Type is { } type)
            worker.Carry.Remove(Stash.Add(type, worker.Carry.Count));
        worker.Carry.Remove(worker.Carry.Count);
        worker.Phase = WorkerPhase.Leaving;
        worker.IdleSpot = null;
        worker.Activity = IdleActivity.None;
        worker.Status = reason;
        worker.Path.Clear();
        if (!worker.IsActive)
            RemoveWorker(worker);
    }

    /// <summary>Called at 6 AM. Pays each worker from the Stash cash; anyone unpaid quits. Returns total paid.</summary>
    public int PayWages()
    {
        int paid = 0;
        foreach (var worker in _workers.ToList())
        {
            if (worker.Phase == WorkerPhase.Leaving)
                continue;
            if (Stash.DirtyCash >= WorkerWage)
            {
                Stash.DirtyCash -= WorkerWage;
                paid += WorkerWage;
            }
            else
            {
                FireWorker(worker, "Quit: wasn't paid");
            }
        }
        return paid;
    }

    private void RemoveWorker(WorkerState worker)
    {
        if (_workers.Remove(worker))
            WorkerRemoved?.Invoke(worker);
    }

    // ---- Jobs from the layout ----

    /// <summary>
    /// Every "move items from A to B" job the current layout needs, in chain order.
    /// Inputs come from machines that make them, or from the Stash for raw supplies.
    /// </summary>
    public List<JobLink> JobLinks()
    {
        var links = new List<JobLink>();
        var production = _machines.Where(m => m.Def.Kind == MachineKind.Production).ToList();
        var ordered = production.OrderBy(m => IndexOf(m.Def)).ThenBy(m => _machines.IndexOf(m)).ToList();

        foreach (var dest in ordered)
        {
            var fromStash = new List<ItemType>();
            foreach (var type in dest.Def.Inputs.Keys)
            {
                var producers = production.Where(m => !m.Def.OutputToStash && m.Def.Output == type).ToList();
                if (producers.Count > 0)
                    links.Add(new JobLink(producers, new Endpoint(dest), [type]));
                else
                    fromStash.Add(type);
            }
            if (fromStash.Count > 0)
                links.Add(new JobLink(null, new Endpoint(dest), fromStash));
        }

        // Outputs that no machine uses (and that don't go to the Stash by themselves) are brought to the Stash.
        foreach (var group in ordered.Where(m => !m.Def.OutputToStash).GroupBy(m => m.Def))
        {
            bool used = production.Any(m => m.Def.Inputs.ContainsKey(group.Key.Output));
            if (!used)
                links.Add(new JobLink(group.ToList(), Endpoint.Stash, [group.Key.Output]));
        }
        return links;

        static int IndexOf(MachineDef def)
        {
            for (int i = 0; i < Production.Machines.All.Count; i++)
            {
                if (Production.Machines.All[i] == def)
                    return i;
            }
            return int.MaxValue;
        }
    }

    /// <summary>The jobs given to one worker: job j goes to worker j % workerCount.</summary>
    public List<JobLink> JobsFor(WorkerState worker)
    {
        var active = _workers.Where(w => w.IsActive && w.Phase != WorkerPhase.Leaving).OrderBy(w => w.Id).ToList();
        int index = active.IndexOf(worker);
        if (index < 0)
            return [];
        var links = JobLinks();
        return links.Where((_, j) => j % active.Count == index).ToList();
    }

    // ---- Grid helpers ----

    public bool IsWalkable(Vector2I cell) =>
        cell.X >= 0 && cell.Y >= 0 && cell.X < Size.X && cell.Y < Size.Y
        && !StashArea.HasPoint(cell) && MachineAt(cell) == null;

    public static Vector2 CellCenter(Vector2I cell) => new(cell.X + 0.5f, cell.Y + 0.5f);

    /// <summary>Walkable cells next to an endpoint, where a character stands to use it.</summary>
    public List<Vector2I> AccessCells(Endpoint endpoint)
    {
        var rect = endpoint.Machine?.Rect
                   ?? (endpoint.StashSlot >= 0
                       ? new Rect2I(StashArea.Position + new Vector2I(endpoint.StashSlot, 0), Vector2I.One)
                       : StashArea);
        var cells = new List<Vector2I>();
        for (int x = rect.Position.X; x < rect.End.X; x++)
        {
            AddIfWalkable(new Vector2I(x, rect.Position.Y - 1));
            AddIfWalkable(new Vector2I(x, rect.End.Y));
        }
        for (int y = rect.Position.Y; y < rect.End.Y; y++)
        {
            AddIfWalkable(new Vector2I(rect.Position.X - 1, y));
            AddIfWalkable(new Vector2I(rect.End.X, y));
        }
        return cells;

        void AddIfWalkable(Vector2I cell)
        {
            if (IsWalkable(cell))
                cells.Add(cell);
        }
    }

    // ---- Simulation ----

    private void TickWorkers(float delta)
    {
        foreach (var worker in _workers.ToList())
        {
            if (worker.Phase == WorkerPhase.Arriving)
            {
                worker.ArriveTimer -= delta;
                if (worker.ArriveTimer <= 0f)
                {
                    // Step in onto the nearest free tile by the door (never on top of someone).
                    var taken = AgentCells(worker);
                    var entry = GridPath.Nearest(DoorCell, Size, c => IsWalkable(c) && !taken.ContainsKey(c));
                    if (entry == null)
                        continue; // doorway packed: wait outside a moment longer
                    worker.Position = CellCenter(entry.Value);
                    worker.Phase = WorkerPhase.Idle;
                    worker.Status = "Starting work";
                }
                continue;
            }

            // Fail-safe: standing inside something (e.g. a machine was placed on them).
            if (!IsWalkable(worker.Cell))
            {
                Teleport(worker, "was inside a machine");
                continue;
            }

            // Universal fail-safe: no meaningful progress for 2 minutes.
            if (worker.Watchdog.Tick(delta))
            {
                Teleport(worker, "no progress for 2 minutes");
                continue;
            }

            switch (worker.Phase)
            {
                case WorkerPhase.Idle:
                    ChooseTask(worker, delta);
                    break;
                case WorkerPhase.ToSource:
                    if (!EndpointExists(worker.Source) || !EndpointExists(worker.Dest))
                        ResetTask(worker, "machine removed");
                    else if (MoveAlongPath(worker, delta))
                        StartAction(worker, WorkerPhase.Taking);
                    break;
                case WorkerPhase.ToDest:
                    if (!EndpointExists(worker.Dest))
                        ResetTask(worker, "machine removed");
                    else if (MoveAlongPath(worker, delta))
                        StartAction(worker, WorkerPhase.Giving);
                    break;
                case WorkerPhase.Taking:
                case WorkerPhase.Giving:
                    TickAction(worker, delta);
                    break;
                case WorkerPhase.Waiting:
                    TickWaiting(worker, delta);
                    break;
                case WorkerPhase.Leaving:
                    // Walks out like anyone else: around people, with the normal fail-safes.
                    if (MoveAlongPath(worker, delta, DoorCell))
                        RemoveWorker(worker);
                    break;
            }
        }
    }

    private void ChooseTask(WorkerState worker, float delta)
    {
        worker.ActionProgress = 0f;

        // Boxed in by machines (not on top of them, but no way out): move to the safe area.
        worker.TrapCheckTimer -= delta;
        if (worker.TrapCheckTimer <= 0f)
        {
            worker.TrapCheckTimer = 1f;
            if (!SafeArea().Contains(worker.Cell))
            {
                Teleport(worker, "boxed in");
                return;
            }
        }
        if (worker.RetryTimer > 0f)
        {
            worker.RetryTimer -= delta;
            return;
        }

        var jobs = JobsFor(worker);

        // Already carrying something (after a reset): deliver it where it's needed, else to the Stash.
        if (worker.Carry.Type is { } carried)
        {
            var dest = jobs.Select(j => j.Dest).FirstOrDefault(d => d.Machine != null && Room(d, carried) > 0);
            if (dest.Machine == null)
                dest = _machines.Where(m => m.InputRoom(carried) > 0).Select(m => new Endpoint(m)).FirstOrDefault();
            if (dest.Machine == null)
                dest = StashShelfFor(carried);
            worker.Dest = dest;
            worker.TaskType = carried;
            BeginMove(worker, WorkerPhase.ToDest, dest);
            return;
        }

        // Pick the job that best keeps the operation producing (see ScoreJob).
        float bestScore = 0f;
        foreach (var job in jobs)
        {
            foreach (var type in job.Types)
            {
                int room = Room(job.Dest, type);
                if (room <= 0)
                    continue;
                var source = BestSource(job, type, out int available);
                if (available <= 0)
                    continue;
                float score = ScoreJob(job, source, type, room, available);
                if (score <= bestScore)
                    continue;
                bestScore = score;
                worker.Source = source;
                worker.Dest = job.Dest.IsStash ? StashShelfFor(type) : job.Dest;
                worker.TaskType = type;
                worker.TaskAmount = Math.Min(Math.Min(room, available), Items.Get(type).MaxCarry);
            }
        }

        if (bestScore <= 0f)
        {
            worker.Watchdog.Progress(); // legitimately idle, not stuck
            WaitOutOfTheWay(worker, delta);
            return;
        }
        worker.IdleSpot = null;
        worker.Activity = IdleActivity.None;
        BeginMove(worker, WorkerPhase.ToSource, worker.Source);
    }

    /// <summary>
    /// Idle workers don't loiter in doorways or in front of machines/shelves: they walk to a free spot
    /// in open floor out of the way (each idle worker their own) and wait there. Work still interrupts this any time.
    /// </summary>
    private void WaitOutOfTheWay(WorkerState worker, float delta)
    {
        worker.NudgeTimer = Mathf.Max(0f, worker.NudgeTimer - delta);
        var keepClear = KeepClearCells();
        bool spotStillGood = worker.IdleSpot is { } spot && IsWalkable(spot) && !keepClear.Contains(spot)
                             && !_workers.Any(w => w != worker && w.IdleSpot == spot)
                             && (worker.PlannedActivity != IdleActivity.LeanOnWall || WallNormal(spot) != null);
        if (!spotStillGood)
        {
            worker.Activity = IdleActivity.None;
            worker.PlannedActivity = (IdleActivity)Random.Shared.Next(1, 5);
            var reachable = SafeArea();
            worker.IdleSpot = null;
            if (worker.PlannedActivity == IdleActivity.LeanOnWall)
            {
                worker.IdleSpot = PickIdleSpot(worker, keepClear, againstWall: true);
                if (worker.IdleSpot == null) // no wall close by: do something else instead of wandering off
                    worker.PlannedActivity = (IdleActivity)Random.Shared.Next(2, 5);
            }
            // Already standing somewhere fine? Stay put (less walking = less traffic in tight layouts).
            if (worker.IdleSpot == null && IsGoodIdleCell(worker, worker.Cell, keepClear, reachable))
                worker.IdleSpot = worker.Cell;
            worker.IdleSpot ??= PickIdleSpot(worker, keepClear, againstWall: false);
            worker.Path.Clear();
            if (worker.IdleSpot is { } target && target != worker.Cell)
                worker.Path = PlanPath(worker, [target], hardBlockAgents: false) ?? [];
        }

        if (worker.Path.Count > 0)
        {
            worker.Status = "Stepping out of the way";
            MoveAlongPath(worker, delta);
            return;
        }

        // At the spot: settle into the activity.
        if (worker.Activity == IdleActivity.None)
        {
            worker.Activity = worker.PlannedActivity;
            if (worker.Activity == IdleActivity.LeanOnWall && WallNormal(worker.Cell) is { } away)
                worker.Facing = away; // back to the wall
            else
                worker.Facing = (CellCenter(new Vector2I(Size.X / 2, Size.Y / 2)) - worker.Position).Normalized();
        }
        worker.Status = worker.Activity switch
        {
            IdleActivity.LeanOnWall => "Leaning on the wall",
            IdleActivity.Smoking => "Smoking a cigarette",
            IdleActivity.DrinkingBeer => "Having a beer",
            _ => "Taking a break",
        };
    }

    /// <summary>Direction pointing away from the outer wall next to this cell, or null if it isn't against one.</summary>
    private Vector2? WallNormal(Vector2I cell)
    {
        if (cell.X == 0) return Vector2.Right;
        if (cell.X == Size.X - 1) return Vector2.Left;
        if (cell.Y == 0) return Vector2.Down;
        if (cell.Y == Size.Y - 1) return Vector2.Up;
        return null;
    }

    /// <summary>Cells idle workers shouldn't stand on: the doorway area and every machine/shelf working spot.</summary>
    private HashSet<Vector2I> KeepClearCells()
    {
        var cells = new HashSet<Vector2I>();
        // Doorway, plus a couple of cells into the room in front of it.
        for (int x = DoorArea.Position.X - 1; x <= DoorArea.End.X; x++)
        {
            for (int y = DoorArea.Position.Y - 2; y <= DoorArea.Position.Y; y++)
                cells.Add(new Vector2I(x, y));
        }
        cells.UnionWith(AccessCells(Endpoint.Stash));
        foreach (var machine in _machines)
            cells.UnionWith(AccessCells(new Endpoint(machine)));
        return cells;
    }

    /// <param name="againstWall">Only spots right against an outer wall (for leaning). Never the strip
    /// behind machines: those are machine working spots and stay clear.</param>
    private Vector2I? PickIdleSpot(WorkerState worker, HashSet<Vector2I> keepClear, bool againstWall)
    {
        var reachable = SafeArea();
        var taken = _workers.Where(w => w != worker && w.IdleSpot != null).Select(w => w.IdleSpot!.Value).ToHashSet();
        var ranked = new List<(Vector2I Cell, float Score)>();
        foreach (var cell in reachable)
        {
            if (keepClear.Contains(cell) || taken.Contains(cell))
                continue;
            if (againstWall && WallNormal(cell) == null)
                continue;
            // Stay close (max 6 tiles away), prefer open floor away from anything that must stay clear,
            // and don't stand right next to another idle worker.
            int travel = Math.Abs(cell.X - worker.Cell.X) + Math.Abs(cell.Y - worker.Cell.Y);
            if (travel > MaxIdleWalk || (worker.NudgeTimer > 0f && travel <= 1))
                continue;
            float clearance = keepClear.Count == 0 ? 3f
                : Math.Min(3f, keepClear.Min(k => (float)(Math.Abs(k.X - cell.X) + Math.Abs(k.Y - cell.Y))));
            float score = clearance * 2f
                          - travel * 0.8f
                          - taken.Count(t => Math.Abs(t.X - cell.X) + Math.Abs(t.Y - cell.Y) <= 1) * 4f;
            ranked.Add((cell, score));
        }
        // Never wait on or right next to a chokepoint (a tile that would cut the room in two if blocked).
        foreach (var (cell, _) in ranked.OrderByDescending(r => r.Score))
        {
            if (IsChokepoint(cell, reachable))
                continue;
            if (GridPath.Neighbors(cell).Any(n => reachable.Contains(n) && IsChokepoint(n, reachable)))
                continue;
            return cell;
        }
        return null;
    }

    /// <summary>A fine place to wait: reachable, not a spot that must stay clear, not taken, not by a chokepoint.</summary>
    private bool IsGoodIdleCell(WorkerState worker, Vector2I cell, HashSet<Vector2I> keepClear, HashSet<Vector2I> reachable)
    {
        if (worker.NudgeTimer > 0f && cell == worker.Cell)
            return false; // just got asked to move from here
        if (!reachable.Contains(cell) || keepClear.Contains(cell))
            return false;
        if (_workers.Any(w => w != worker && (w.IdleSpot == cell || (w.IsActive && w.Cell == cell))))
            return false;
        if (IsChokepoint(cell, reachable))
            return false;
        return !GridPath.Neighbors(cell).Any(n => reachable.Contains(n) && IsChokepoint(n, reachable));
    }

    /// <summary>True if blocking this cell would cut some of the reachable area off from the door.</summary>
    private bool IsChokepoint(Vector2I cell, HashSet<Vector2I> reachable)
    {
        if (cell == DoorCell)
            return true;
        var seen = new HashSet<Vector2I> { DoorCell };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(DoorCell);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in GridPath.Neighbors(current))
            {
                if (next != cell && reachable.Contains(next) && seen.Add(next))
                    queue.Enqueue(next);
            }
        }
        return seen.Count < reachable.Count - 1;
    }

    /// <summary>
    /// How much a job helps keep product coming out (higher = do it first):
    /// finishing work beats starting work, unblocking a full machine and feeding an idle one are urgent,
    /// and tiny trips are avoided while a machine is still making more.
    /// </summary>
    private float ScoreJob(JobLink job, Endpoint source, ItemType type, int room, int available)
    {
        float score = 1f;

        // Downstream first: the closer to the finished product, the more it matters.
        score += 10f * ChainDepth(job.Dest);

        // Unblock: the source machine's output is full, so it has stopped working.
        if (source.Machine is { } from && from.OutputCount >= from.Def.OutputCapacity)
            score += 25f;

        // Feed: the destination is idle and this lets it start.
        if (job.Dest.Machine is { } to && to.ActiveCrafts == 0
            && to.InputCount(type) + Math.Min(available, room) >= to.Def.Inputs.GetValueOrDefault(type))
            score += 12f;

        // Batch: don't run back and forth with 1 item while the source is still making more.
        int fullLoad = Math.Min(Items.Get(type).MaxCarry, room);
        if (available < fullLoad && source.Machine is { ActiveCrafts: > 0 })
            score -= 20f;

        score += room * 0.5f + available * 0.2f;
        return score;
    }

    /// <summary>Position of a destination in the chain: later machines (closer to the product) score higher.</summary>
    private int ChainDepth(Endpoint dest)
    {
        if (dest.Machine == null)
            return Production.Machines.All.Count; // bringing finished goods to the Stash: last step
        for (int i = 0; i < Production.Machines.All.Count; i++)
        {
            if (Production.Machines.All[i] == dest.Machine.Def)
                return i;
        }
        return 0;
    }

    /// <summary>The shelf to store an item on: the one already holding it, else the first empty one.</summary>
    private Endpoint StashShelfFor(ItemType type)
    {
        for (int i = 0; i < Stash.Slots.Count; i++)
        {
            if (Stash.Slots[i].Type == type && Stash.Slots[i].Count < StashState.SlotCapacity)
                return new Endpoint(null, i);
        }
        for (int i = 0; i < Stash.Slots.Count; i++)
        {
            if (Stash.Slots[i].Type == null)
                return new Endpoint(null, i);
        }
        return Endpoint.Stash;
    }

    private Endpoint BestSource(JobLink job, ItemType type, out int available)
    {
        if (job.Sources == null)
        {
            // The exact shelf holding this item (the fullest one).
            int bestSlot = -1;
            available = 0;
            for (int i = 0; i < Stash.Slots.Count; i++)
            {
                if (Stash.Slots[i].Type == type && Stash.Slots[i].Count > available)
                {
                    available = Stash.Slots[i].Count;
                    bestSlot = i;
                }
            }
            return new Endpoint(null, bestSlot);
        }
        var best = job.Sources.Where(m => _machines.Contains(m) && m.Def.Output == type)
            .OrderByDescending(m => m.OutputCount).FirstOrDefault();
        available = best?.OutputCount ?? 0;
        return new Endpoint(best);
    }

    private int Room(Endpoint endpoint, ItemType type) =>
        endpoint.Machine?.InputRoom(type) ?? Stash.RoomFor(type);

    private bool EndpointExists(Endpoint endpoint) => endpoint.IsStash || _machines.Contains(endpoint.Machine!);

    private void BeginMove(WorkerState worker, WorkerPhase phase, Endpoint target)
    {
        worker.Phase = phase;
        worker.Activity = IdleActivity.None;
        worker.BestDistanceToGoal = int.MaxValue;
        worker.Path.Clear();
        worker.NoProgressTimer = 0f;
        worker.BlockedTimer = 0f;
        worker.ReplannedAroundAgents = false;
        worker.Status = phase == WorkerPhase.ToSource
            ? $"Getting {Items.Get(worker.TaskType).Name} from {target.Name}"
            : $"Bringing {Items.Get(worker.TaskType).Name} to {target.Name}";

        var path = PlanPath(worker, AccessCells(target), hardBlockAgents: false);
        if (path == null)
        {
            if (!SafeArea().Contains(worker.Cell))
            {
                Teleport(worker, "boxed in"); // the worker is trapped, not the target
                return;
            }
            // The target is walled in; the worker is fine. Keep retrying until the layout changes.
            worker.Phase = WorkerPhase.Idle;
            worker.Status = $"Can't reach the {target.Name}";
            worker.RetryTimer = RetryAfterUnreachable;
            worker.Watchdog.Progress();
            return;
        }
        worker.Path = path;
    }

    private void StartAction(WorkerState worker, WorkerPhase phase)
    {
        worker.Phase = phase;
        worker.ActionTimer = 0f;
        var target = phase == WorkerPhase.Taking ? worker.Source : worker.Dest;
        var center = target.Machine != null
            ? new Vector2(target.Machine.Rect.Position.X + target.Machine.Rect.Size.X / 2f,
                target.Machine.Rect.Position.Y + target.Machine.Rect.Size.Y / 2f)
            : target.StashSlot >= 0
                ? new Vector2(StashArea.Position.X + target.StashSlot + 0.5f, StashArea.Position.Y + 0.5f)
                : new Vector2(StashArea.Position.X + StashArea.Size.X / 2f, StashArea.Position.Y + 0.5f);
        var toTarget = center - worker.Position;
        if (toTarget.LengthSquared() > 0.0001f)
            worker.Facing = toTarget.Normalized();
    }

    private void TickAction(WorkerState worker, float delta)
    {
        worker.ActionTimer += delta;
        worker.ActionProgress = Mathf.Clamp(worker.ActionTimer / ActionSeconds, 0f, 1f);
        if (worker.ActionTimer < ActionSeconds)
            return;
        worker.ActionProgress = 0f;
        worker.Watchdog.Progress();

        if (worker.Phase == WorkerPhase.Taking)
        {
            int taken = TakeFor(worker);
            if (taken <= 0)
            {
                ResetTask(worker, null); // someone got there first
                return;
            }
            BeginMove(worker, WorkerPhase.ToDest, worker.Dest);
            return;
        }

        GiveFrom(worker);
        if (worker.Carry.IsEmpty)
        {
            worker.Phase = WorkerPhase.Idle;
            return;
        }
        worker.Phase = WorkerPhase.Waiting; // destination filled up: wait for room
        worker.WaitTimer = 0f;
        worker.Status = $"Waiting: {worker.Dest.Name} is full";
    }

    private void TickWaiting(WorkerState worker, float delta)
    {
        worker.WaitTimer += delta;
        worker.Watchdog.Progress(); // waiting at a full machine is normal (and gives up after 15s)
        if (Room(worker.Dest, worker.TaskType) > 0)
        {
            StartAction(worker, WorkerPhase.Giving);
            return;
        }
        if (worker.WaitTimer > GiveUpWaitingAfter && !worker.Dest.IsStash)
        {
            worker.Dest = StashShelfFor(worker.TaskType); // stop blocking: put it back in the Stash
            BeginMove(worker, WorkerPhase.ToDest, worker.Dest);
        }
    }

    private int TakeFor(WorkerState worker)
    {
        int amount = Math.Min(worker.TaskAmount, Room(worker.Dest, worker.TaskType));
        if (worker.Source.Machine is { } machine)
            return TakeFromMachine(machine, worker.Carry, amount);
        int slot = worker.Source.StashSlot;
        if (slot >= 0 && Stash.Slots[slot].Type == worker.TaskType)
            return TakeFromStash(slot, worker.Carry, amount);
        for (int i = 0; i < Stash.Slots.Count; i++)
        {
            if (Stash.Slots[i].Type == worker.TaskType)
                return TakeFromStash(i, worker.Carry, amount);
        }
        return 0;
    }

    private void GiveFrom(WorkerState worker)
    {
        if (worker.Dest.Machine is { } machine)
            LoadMachine(machine, worker.Carry, worker.Carry.Count);
        else
            StoreInStash(worker.Carry, worker.Carry.Count);
    }

    /// <summary>Stops the current task and picks a new one (keeps anything carried).</summary>
    private void ResetTask(WorkerState worker, string? reason)
    {
        worker.Phase = WorkerPhase.Idle;
        worker.Activity = IdleActivity.None;
        worker.IdleSpot = null;
        worker.Path.Clear();
        worker.ActionProgress = 0f;
        if (reason != null)
            worker.Status = $"Reset: {reason}";
    }

    /// <summary>
    /// Last-resort fail-safe: jump to the nearest free cell in the safe area (connected to the door,
    /// so it can reach the rest of the building) and restart the task. Keeps carried items.
    /// </summary>
    private void Teleport(WorkerState worker, string reason)
    {
        var occupied = AgentCells(worker);
        var safeArea = SafeArea();
        var safe = GridPath.Nearest(worker.Cell, Size,
            c => c != worker.Cell && safeArea.Contains(c) && !occupied.ContainsKey(c)) ?? DoorCell;
        worker.Position = CellCenter(safe);
        worker.Teleports++;
        worker.Watchdog.Progress();
        worker.NoProgressTimer = 0f;
        worker.BestDistanceToGoal = int.MaxValue;
        bool leaving = worker.Phase == WorkerPhase.Leaving;
        ResetTask(worker, $"got stuck ({reason})");
        if (leaving)
            worker.Phase = WorkerPhase.Leaving;
    }

    /// <summary>
    /// The pathfinding-safe area: every walkable cell connected to the doorway.
    /// Anyone outside it is boxed in and gets moved back into it.
    /// </summary>
    public HashSet<Vector2I> SafeArea()
    {
        var area = new HashSet<Vector2I> { DoorCell };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(DoorCell);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var next in GridPath.Neighbors(cell))
            {
                if (IsWalkable(next) && area.Add(next))
                    queue.Enqueue(next);
            }
        }
        return area;
    }

    /// <summary>Cells taken by other people: their current cell and the next cell they're stepping into.
    /// Value = who (worker id, or 0 for the player, who has right of way).</summary>
    private Dictionary<Vector2I, int> AgentCells(WorkerState except)
    {
        var cells = new Dictionary<Vector2I, int>();
        if (_playerPosition is { } player)
        {
            // Every cell whose centre is too close to the player's body counts as taken by the player.
            var center = new Vector2I(Mathf.FloorToInt(player.X), Mathf.FloorToInt(player.Y));
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    var cell = center + new Vector2I(dx, dy);
                    if (CellCenter(cell).DistanceTo(player) < PlayerClearance)
                        cells[cell] = 0;
                }
            }
        }
        foreach (var other in _workers)
        {
            if (other == except || !other.IsActive)
                continue;
            cells.TryAdd(other.Cell, other.Id);
            if (other.Path.Count > 0)
                cells.TryAdd(other.Path[0], other.Id);
        }
        return cells;
    }

    private List<Vector2I>? PlanPath(WorkerState worker, List<Vector2I> goals, bool hardBlockAgents)
    {
        var agents = AgentCells(worker);
        return GridPath.Find(
            worker.Cell,
            goals,
            cell => IsWalkable(cell) && (!hardBlockAgents || !agents.ContainsKey(cell)),
            cell => agents.ContainsKey(cell) ? (hardBlockAgents ? 1000 : 40) : 0);
    }

    /// <summary>
    /// Walks along the path, avoiding people. Returns true on arrival.
    /// Fail-safes: re-plan around people, step aside, then teleport if no progress.
    /// </summary>
    private bool MoveAlongPath(WorkerState worker, float delta, Vector2I? finalCell = null)
    {
        if (worker.Path.Count == 0)
        {
            // Leaving needs a path to the door; plan it lazily.
            if (finalCell is { } goal && worker.Cell != goal)
            {
                var path = PlanPath(worker, [goal], hardBlockAgents: false);
                if (path == null)
                    return true; // can't get out: just leave
                worker.Path = path;
            }
            else
            {
                return true;
            }
        }

        worker.NoProgressTimer += delta;
        if (worker.NoProgressTimer > TeleportAfter)
        {
            Teleport(worker, "no progress");
            return false;
        }

        var next = worker.Path[0];
        var agents = AgentCells(worker);
        if (agents.TryGetValue(next, out int blocker))
        {
            worker.BlockedTimer += delta;
            // "Excuse me": an idle coworker standing in the way of someone working moves along.
            if (worker.Phase != WorkerPhase.Idle
                && _workers.FirstOrDefault(w => w.Id == blocker) is { Phase: WorkerPhase.Idle, Path.Count: 0 } idler)
            {
                idler.IdleSpot = null;
                idler.Activity = IdleActivity.None;
                idler.NudgeTimer = 6f;
            }
            if (blocker == 0)
            {
                // The player is in the way: wait politely instead of counting it as stuck.
                worker.NoProgressTimer = Mathf.Max(0f, worker.NoProgressTimer - delta);
                worker.Status = "Waiting for you to move";
            }
            if (worker.BlockedTimer > ReplanAfter && !worker.ReplannedAroundAgents)
            {
                worker.ReplannedAroundAgents = true;
                var goals = new List<Vector2I> { worker.Path[^1] };
                var around = PlanPath(worker, goals, hardBlockAgents: true);
                if (around != null && (around.Count == 0 || !agents.ContainsKey(around[0])))
                    worker.Path = around;
            }
            // The lower-priority person steps aside (newer hires give way; everyone gives way to the player).
            bool iGiveWay = blocker == 0 || blocker < worker.Id;
            if (worker.BlockedTimer > SidestepAfter && iGiveWay)
            {
                var sides = GridPath.Neighbors(worker.Cell)
                    .Where(c => IsWalkable(c) && !agents.ContainsKey(c) && c != next).ToList();
                if (sides.Count > 0)
                {
                    var side = sides[_random.Next(sides.Count)];
                    var goal = worker.Path[^1];
                    worker.Path = [side];
                    var rest = GridPath.Find(side, [goal], IsWalkable, _ => 0);
                    if (rest != null)
                        worker.Path.AddRange(rest);
                }
                worker.BlockedTimer = 0f;
                worker.ReplannedAroundAgents = false;
            }
            return false;
        }

        var target = CellCenter(next);
        var offset = target - worker.Position;
        float step = WalkSpeed * delta;

        // Never step closer to the player than their body allows (bodies would overlap).
        if (_playerPosition is { } player)
        {
            var after = worker.Position + offset.LimitLength(step);
            if (after.DistanceTo(player) < PlayerBodyGap && after.DistanceTo(player) < worker.Position.DistanceTo(player))
            {
                worker.BlockedTimer += delta;
                worker.Status = worker.Phase == WorkerPhase.Leaving ? worker.Status : "Waiting for you to move";
                worker.NoProgressTimer = Mathf.Max(0f, worker.NoProgressTimer - delta);
                if (worker.BlockedTimer > ReplanAfter)
                {
                    // Step away from the player first, then route around them.
                    var goal = worker.Path[^1];
                    var people = AgentCells(worker);
                    var away = GridPath.Neighbors(worker.Cell)
                        .Where(c => IsWalkable(c) && !people.ContainsKey(c)
                                    && CellCenter(c).DistanceTo(player) > worker.Position.DistanceTo(player))
                        .OrderByDescending(c => CellCenter(c).DistanceTo(player))
                        .Cast<Vector2I?>()
                        .FirstOrDefault();
                    if (away is { } side)
                    {
                        worker.Path = [side];
                        var rest = GridPath.Find(side, [goal], c => IsWalkable(c) && !people.ContainsKey(c), _ => 0)
                                   ?? GridPath.Find(side, [goal], IsWalkable, _ => 0);
                        if (rest != null)
                            worker.Path.AddRange(rest);
                    }
                    worker.BlockedTimer = 0f;
                }
                return false;
            }
        }

        worker.BlockedTimer = 0f;
        worker.ReplannedAroundAgents = false;
        if (offset.Length() <= step)
        {
            worker.Position = target;
            worker.Path.RemoveAt(0);
            worker.NoProgressTimer = 0f;
            // Only getting closer to the goal counts (stepping aside back and forth doesn't).
            if (worker.Path.Count > 0)
            {
                var goal = worker.Path[^1];
                int distance = Math.Abs(goal.X - next.X) + Math.Abs(goal.Y - next.Y);
                if (distance < worker.BestDistanceToGoal)
                {
                    worker.BestDistanceToGoal = distance;
                    worker.Watchdog.Progress();
                }
            }
            else
            {
                worker.Watchdog.Progress(); // arrived
            }
            if (!IsWalkable(next))
            {
                // Layout changed under the path: plan again.
                worker.Path.Clear();
                ResetTask(worker, "path blocked");
                return false;
            }
            return worker.Path.Count == 0;
        }
        worker.Facing = offset.Normalized();
        worker.Position += worker.Facing * step;
        // The cell ahead became a machine since planning: re-plan.
        if (!IsWalkable(next))
        {
            var goal = worker.Path[^1];
            var replanned = PlanPath(worker, [goal], hardBlockAgents: false);
            if (replanned == null)
                ResetTask(worker, "path blocked");
            else
                worker.Path = replanned;
        }
        return false;
    }
}
