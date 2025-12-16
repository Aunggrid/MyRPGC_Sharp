using System.Collections.Generic;
using Microsoft.Xna.Framework;

public static class Pathfinder {
    
    // Returns a list of points (The path) from Start -> End
    public static List<Point> FindPath(WorldGrid grid, Point start, Point end) {
        // If the end is a wall, we can't go there.
        if (!grid.IsInBounds(end) || !grid.Tiles[end.X, end.Y].IsWalkable) 
            return null;

        // "Frontier" = Tiles we need to check
        var frontier = new Queue<Point>();
        frontier.Enqueue(start);

        // "CameFrom" = How we got to each tile (used to retrace steps)
        var cameFrom = new Dictionary<Point, Point>();
        cameFrom[start] = start; // Start came from itself

        while (frontier.Count > 0) {
            var current = frontier.Dequeue();

            if (current == end) break; // Found it!

            // Check 4 neighbors (Up, Down, Left, Right)
            foreach (var next in GetNeighbors(grid, current)) {
                if (!cameFrom.ContainsKey(next)) {
                    frontier.Enqueue(next);
                    cameFrom[next] = current; // Record the path
                }
            }
        }

        // Retrace the path backward from End -> Start
        var path = new List<Point>();
        var curr = end;
        while (curr != start) {
            if (!cameFrom.ContainsKey(curr)) return null; // No path found
            path.Add(curr);
            curr = cameFrom[curr];
        }
        path.Reverse(); // Flip it so it's Start -> End
        return path;
    }

    private static List<Point> GetNeighbors(WorldGrid grid, Point center) {
        var neighbors = new List<Point>();
        Point[] dirs = { new Point(0, 1), new Point(0, -1), new Point(1, 0), new Point(-1, 0) };

        foreach (var d in dirs) {
            Point next = new Point(center.X + d.X, center.Y + d.Y);
            if (grid.IsInBounds(next) && grid.Tiles[next.X, next.Y].IsWalkable) {
                neighbors.Add(next);
            }
        }
        return neighbors;
    }
}