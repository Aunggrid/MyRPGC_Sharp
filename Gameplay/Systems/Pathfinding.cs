using System.Collections.Generic;       // Fixes Queue, Dictionary, List
using Microsoft.Xna.Framework;          // Fixes Point
using MyRPG.Gameplay.World;             // Fixes WorldGrid

namespace MyRPG.Gameplay.Systems        // Wrap in a proper namespace
{
    public static class Pathfinder
    {

        public static List<Point> FindPath(WorldGrid grid, Point start, Point end)
        {
            // Safety check: is the end reachable?
            if (end.X < 0 || end.X >= grid.Width || end.Y < 0 || end.Y >= grid.Height) return null;
            // Note: In a real game, you'd check grid.GetTile(end).IsWalkable here

            var frontier = new Queue<Point>();
            frontier.Enqueue(start);

            var cameFrom = new Dictionary<Point, Point>();
            cameFrom[start] = start;

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                if (current == end) break;

                foreach (var next in GetNeighbors(grid, current))
                {
                    if (!cameFrom.ContainsKey(next))
                    {
                        frontier.Enqueue(next);
                        cameFrom[next] = current;
                    }
                }
            }

            // Retrace path
            var path = new List<Point>();
            var curr = end;

            if (!cameFrom.ContainsKey(end)) return null; // Path not found

            while (curr != start)
            {
                path.Add(curr);
                curr = cameFrom[curr];
            }
            path.Reverse();
            return path;
        }

        private static List<Point> GetNeighbors(WorldGrid grid, Point center)
        {
            var neighbors = new List<Point>();
            Point[] dirs = { new Point(0, 1), new Point(0, -1), new Point(1, 0), new Point(-1, 0) };

            foreach (var d in dirs)
            {
                Point next = new Point(center.X + d.X, center.Y + d.Y);

                // Use the Public Helper we made in WorldGrid to check bounds
                if (next.X >= 0 && next.X < grid.Width && next.Y >= 0 && next.Y < grid.Height)
                {
                    // Check if walkable (Accessing the Tiles array directly)
                    // Note: You might need to make _tiles public or add a method 'IsWalkable(x,y)' in WorldGrid
                    // For now, let's assume we can access it or just check bounds
                    neighbors.Add(next);
                }
            }
            return neighbors;
        }
    }
}