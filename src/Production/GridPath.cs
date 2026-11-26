using System;
using System.Collections.Generic;
using Godot;

namespace Gecko.Production;

/// <summary>A* and flood-fill helpers on a facility's cell grid (4-directional).</summary>
public static class GridPath
{
    private static readonly Vector2I[] Directions = [Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up];

    public static IEnumerable<Vector2I> Neighbors(Vector2I cell)
    {
        foreach (var dir in Directions)
            yield return cell + dir;
    }

    /// <summary>
    /// Shortest path from <paramref name="start"/> to any of <paramref name="goals"/>.
    /// Returns the cells to walk through (excluding start), an empty list if already at a goal,
    /// or null if no goal is reachable.
    /// </summary>
    /// <param name="walkable">Cells that can be entered.</param>
    /// <param name="extraCost">Extra cost to enter a cell (e.g. someone standing there).</param>
    public static List<Vector2I>? Find(
        Vector2I start, IReadOnlyCollection<Vector2I> goals, Func<Vector2I, bool> walkable, Func<Vector2I, int> extraCost)
    {
        if (goals.Count == 0)
            return null;
        var goalSet = new HashSet<Vector2I>(goals);
        if (goalSet.Contains(start))
            return [];

        int Heuristic(Vector2I cell)
        {
            int best = int.MaxValue;
            foreach (var goal in goalSet)
                best = Math.Min(best, Math.Abs(goal.X - cell.X) + Math.Abs(goal.Y - cell.Y));
            return best;
        }

        var open = new PriorityQueue<Vector2I, int>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var cost = new Dictionary<Vector2I, int> { [start] = 0 };
        open.Enqueue(start, Heuristic(start));

        while (open.TryDequeue(out var current, out _))
        {
            if (goalSet.Contains(current))
            {
                var path = new List<Vector2I>();
                for (var cell = current; cell != start; cell = cameFrom[cell])
                    path.Add(cell);
                path.Reverse();
                return path;
            }
            foreach (var next in Neighbors(current))
            {
                if (!walkable(next))
                    continue;
                int newCost = cost[current] + 10 + extraCost(next);
                if (cost.TryGetValue(next, out int known) && known <= newCost)
                    continue;
                cost[next] = newCost;
                cameFrom[next] = current;
                open.Enqueue(next, newCost + Heuristic(next) * 10);
            }
        }
        return null;
    }

    /// <summary>Nearest cell (by steps, passing through anything inside the grid) that satisfies <paramref name="accept"/>.</summary>
    public static Vector2I? Nearest(Vector2I start, Vector2I gridSize, Func<Vector2I, bool> accept)
    {
        var seen = new HashSet<Vector2I> { start };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (accept(cell))
                return cell;
            foreach (var next in Neighbors(cell))
            {
                if (next.X < 0 || next.Y < 0 || next.X >= gridSize.X || next.Y >= gridSize.Y || !seen.Add(next))
                    continue;
                queue.Enqueue(next);
            }
        }
        return null;
    }
}
