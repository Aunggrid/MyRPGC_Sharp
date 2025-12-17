// Gameplay/Systems/Pathfinding.cs
// A* pathfinding with 8-directional diagonal movement

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MyRPG.Gameplay.World;
using MyRPG;  // For GameServices

namespace MyRPG.Gameplay.Systems
{
    public static class Pathfinder
    {
        // 8-directional movement (including diagonals)
        private static readonly Point[] Directions = new Point[]
        {
            new Point(0, -1),   // North
            new Point(1, -1),   // NE (diagonal)
            new Point(1, 0),    // East
            new Point(1, 1),    // SE (diagonal)
            new Point(0, 1),    // South
            new Point(-1, 1),   // SW (diagonal)
            new Point(-1, 0),   // West
            new Point(-1, -1)   // NW (diagonal)
        };
        
        // Diagonal movement costs more (√2 ≈ 1.414)
        private const float DIAGONAL_COST = 1.414f;
        private const float CARDINAL_COST = 1.0f;

        /// <summary>
        /// A* pathfinding with diagonal movement support
        /// </summary>
        public static List<Point> FindPath(WorldGrid grid, Point start, Point end)
        {
            // Safety check
            if (!IsValidTile(grid, end)) return null;
            if (!IsTileWalkable(grid, end)) return null;
            
            // A* algorithm
            var openSet = new PriorityQueue<Point, float>();
            var cameFrom = new Dictionary<Point, Point>();
            var gScore = new Dictionary<Point, float>();
            var fScore = new Dictionary<Point, float>();
            
            gScore[start] = 0;
            fScore[start] = Heuristic(start, end);
            openSet.Enqueue(start, fScore[start]);
            
            while (openSet.Count > 0)
            {
                var current = openSet.Dequeue();
                
                if (current == end)
                {
                    return ReconstructPath(cameFrom, current, start);
                }
                
                foreach (var (neighbor, moveCost) in GetNeighborsWithCost(grid, current))
                {
                    float tentativeG = gScore[current] + moveCost;
                    
                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Heuristic(neighbor, end);
                        
                        // Add to open set if not already there
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                    }
                }
            }
            
            return null; // No path found
        }
        
        /// <summary>
        /// Get valid adjacent tiles for combat movement (1 tile, including diagonals)
        /// </summary>
        public static List<Point> GetAdjacentTiles(WorldGrid grid, Point center, bool includeCenter = false)
        {
            var adjacent = new List<Point>();
            
            if (includeCenter) adjacent.Add(center);
            
            foreach (var dir in Directions)
            {
                Point next = new Point(center.X + dir.X, center.Y + dir.Y);
                
                if (IsValidTile(grid, next) && IsTileWalkable(grid, next))
                {
                    // For diagonals, check that we can actually move there (not blocked by corners)
                    if (IsDiagonal(dir))
                    {
                        if (CanMoveDiagonally(grid, center, dir))
                        {
                            adjacent.Add(next);
                        }
                    }
                    else
                    {
                        adjacent.Add(next);
                    }
                }
            }
            
            return adjacent;
        }
        
        /// <summary>
        /// Check if two points are adjacent (including diagonals)
        /// </summary>
        public static bool IsAdjacent(Point a, Point b)
        {
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            
            // Adjacent means max 1 tile away in both X and Y (Chebyshev distance)
            return dx <= 1 && dy <= 1 && !(dx == 0 && dy == 0);
        }
        
        /// <summary>
        /// Get distance in tiles (Chebyshev - allows diagonal)
        /// </summary>
        public static int GetDistance(Point a, Point b)
        {
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }
        
        /// <summary>
        /// Get Manhattan distance (no diagonal)
        /// </summary>
        public static int GetManhattanDistance(Point a, Point b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }
        
        // ============================================
        // PRIVATE HELPERS
        // ============================================
        
        private static float Heuristic(Point a, Point b)
        {
            // Octile distance heuristic (accounts for diagonal movement)
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            return CARDINAL_COST * (dx + dy) + (DIAGONAL_COST - 2 * CARDINAL_COST) * Math.Min(dx, dy);
        }
        
        private static List<(Point neighbor, float cost)> GetNeighborsWithCost(WorldGrid grid, Point center)
        {
            var neighbors = new List<(Point, float)>();
            
            for (int i = 0; i < Directions.Length; i++)
            {
                var dir = Directions[i];
                Point next = new Point(center.X + dir.X, center.Y + dir.Y);
                
                if (!IsValidTile(grid, next)) continue;
                if (!IsTileWalkable(grid, next)) continue;
                
                bool isDiag = IsDiagonal(dir);
                
                // For diagonals, check corner cutting
                if (isDiag && !CanMoveDiagonally(grid, center, dir))
                {
                    continue;
                }
                
                // Calculate movement cost
                float baseCost = isDiag ? DIAGONAL_COST : CARDINAL_COST;
                float tileCost = grid.Tiles[next.X, next.Y].MovementCost;
                
                neighbors.Add((next, baseCost * tileCost));
            }
            
            return neighbors;
        }
        
        private static bool IsDiagonal(Point dir)
        {
            return dir.X != 0 && dir.Y != 0;
        }
        
        private static bool CanMoveDiagonally(WorldGrid grid, Point from, Point dir)
        {
            // Check that both adjacent cardinal tiles are walkable (no corner cutting)
            Point cardinalX = new Point(from.X + dir.X, from.Y);
            Point cardinalY = new Point(from.X, from.Y + dir.Y);
            
            bool xWalkable = IsValidTile(grid, cardinalX) && IsTileWalkable(grid, cardinalX);
            bool yWalkable = IsValidTile(grid, cardinalY) && IsTileWalkable(grid, cardinalY);
            
            // Both adjacent tiles must be walkable to move diagonally (prevents corner cutting)
            return xWalkable && yWalkable;
        }
        
        private static bool IsValidTile(WorldGrid grid, Point p)
        {
            return p.X >= 0 && p.X < grid.Width && p.Y >= 0 && p.Y < grid.Height;
        }
        
        /// <summary>
        /// Check if a tile is walkable (both terrain and structures)
        /// </summary>
        private static bool IsTileWalkable(WorldGrid grid, Point p)
        {
            // Check terrain
            if (!grid.Tiles[p.X, p.Y].IsWalkable) return false;
            
            // Check structures (if BuildingSystem is available)
            if (GameServices.IsInitialized && GameServices.Building != null)
            {
                if (GameServices.Building.IsBlockedByStructure(p)) return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Public method to check if a tile is walkable (for external use)
        /// </summary>
        public static bool IsWalkable(WorldGrid grid, Point p)
        {
            if (!IsValidTile(grid, p)) return false;
            return IsTileWalkable(grid, p);
        }
        
        private static List<Point> ReconstructPath(Dictionary<Point, Point> cameFrom, Point current, Point start)
        {
            var path = new List<Point>();
            
            while (current != start)
            {
                path.Add(current);
                current = cameFrom[current];
            }
            
            path.Reverse();
            return path;
        }
    }
    
    // ============================================
    // SIMPLE PRIORITY QUEUE (for A*)
    // ============================================
    
    public class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        private List<(TElement Element, TPriority Priority)> _heap = new List<(TElement, TPriority)>();
        
        public int Count => _heap.Count;
        
        public void Enqueue(TElement element, TPriority priority)
        {
            _heap.Add((element, priority));
            HeapifyUp(_heap.Count - 1);
        }
        
        public TElement Dequeue()
        {
            if (_heap.Count == 0) throw new InvalidOperationException("Queue is empty");
            
            var result = _heap[0].Element;
            _heap[0] = _heap[_heap.Count - 1];
            _heap.RemoveAt(_heap.Count - 1);
            
            if (_heap.Count > 0) HeapifyDown(0);
            
            return result;
        }
        
        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_heap[index].Priority.CompareTo(_heap[parent].Priority) >= 0) break;
                
                (_heap[index], _heap[parent]) = (_heap[parent], _heap[index]);
                index = parent;
            }
        }
        
        private void HeapifyDown(int index)
        {
            while (true)
            {
                int smallest = index;
                int left = 2 * index + 1;
                int right = 2 * index + 2;
                
                if (left < _heap.Count && _heap[left].Priority.CompareTo(_heap[smallest].Priority) < 0)
                    smallest = left;
                if (right < _heap.Count && _heap[right].Priority.CompareTo(_heap[smallest].Priority) < 0)
                    smallest = right;
                
                if (smallest == index) break;
                
                (_heap[index], _heap[smallest]) = (_heap[smallest], _heap[index]);
                index = smallest;
            }
        }
    }
}
