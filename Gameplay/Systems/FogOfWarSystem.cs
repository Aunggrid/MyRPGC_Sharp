// Gameplay/Systems/FogOfWarSystem.cs
// Fog of War system - tracks tile visibility and exploration state

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MyRPG.Gameplay.World;

namespace MyRPG.Gameplay.Systems
{
    // ============================================
    // VISIBILITY STATE
    // ============================================
    
    public enum TileVisibility
    {
        Unexplored,     // Never seen - completely black
        Explored,       // Previously seen - show terrain, hide units (fog)
        Visible         // Currently visible - show everything
    }
    
    // ============================================
    // FOG OF WAR SYSTEM
    // ============================================
    
    public class FogOfWarSystem
    {
        // Visibility grid - same size as world
        private TileVisibility[,] _visibility;
        private int _width;
        private int _height;
        
        // Currently visible tiles (recalculated each update)
        private HashSet<Point> _currentlyVisible = new HashSet<Point>();
        
        // Reference to world for LOS checks
        private WorldGrid _world;
        
        // Player position (tile coordinates)
        private Point _lastPlayerTile = new Point(-1, -1);
        
        // Is the system enabled?
        public bool IsEnabled { get; set; } = true;
        
        // Debug mode - show all tiles
        public bool DebugRevealAll { get; set; } = false;
        
        // ============================================
        // INITIALIZATION
        // ============================================
        
        public FogOfWarSystem()
        {
            // Will be initialized when world is set
        }
        
        /// <summary>
        /// Initialize fog of war for a new world/zone
        /// </summary>
        public void Initialize(WorldGrid world, bool keepExplored = false)
        {
            _world = world;
            _width = world.Width;
            _height = world.Height;
            
            // Create or reset visibility grid
            if (!keepExplored || _visibility == null || 
                _visibility.GetLength(0) != _width || 
                _visibility.GetLength(1) != _height)
            {
                _visibility = new TileVisibility[_width, _height];
                
                // All tiles start unexplored
                for (int x = 0; x < _width; x++)
                {
                    for (int y = 0; y < _height; y++)
                    {
                        _visibility[x, y] = TileVisibility.Unexplored;
                    }
                }
            }
            
            _currentlyVisible.Clear();
            _lastPlayerTile = new Point(-1, -1);
            
            System.Diagnostics.Debug.WriteLine($">>> FogOfWar: Initialized {_width}x{_height} grid <<<");
        }
        
        /// <summary>
        /// Load exploration data from saved game
        /// </summary>
        public void LoadExplorationData(bool[,] explored)
        {
            if (explored == null || _visibility == null) return;
            
            int maxX = Math.Min(explored.GetLength(0), _width);
            int maxY = Math.Min(explored.GetLength(1), _height);
            
            for (int x = 0; x < maxX; x++)
            {
                for (int y = 0; y < maxY; y++)
                {
                    if (explored[x, y])
                    {
                        _visibility[x, y] = TileVisibility.Explored;
                    }
                }
            }
        }
        
        /// <summary>
        /// Get exploration data for saving
        /// </summary>
        public bool[,] GetExplorationData()
        {
            if (_visibility == null) return null;
            
            var explored = new bool[_width, _height];
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    explored[x, y] = _visibility[x, y] != TileVisibility.Unexplored;
                }
            }
            return explored;
        }
        
        // ============================================
        // UPDATE
        // ============================================
        
        /// <summary>
        /// Update visibility based on player position
        /// </summary>
        public void Update(Vector2 playerPosition, float sightRange, int tileSize)
        {
            if (!IsEnabled || _visibility == null || _world == null) return;
            
            // Calculate player tile
            Point playerTile = new Point(
                (int)(playerPosition.X / tileSize),
                (int)(playerPosition.Y / tileSize)
            );
            
            // Only recalculate if player moved to a new tile
            if (playerTile == _lastPlayerTile) return;
            _lastPlayerTile = playerTile;
            
            // Mark all currently visible tiles as "explored" (no longer visible)
            foreach (var tile in _currentlyVisible)
            {
                if (IsInBounds(tile))
                {
                    _visibility[tile.X, tile.Y] = TileVisibility.Explored;
                }
            }
            _currentlyVisible.Clear();
            
            // Calculate newly visible tiles
            int range = (int)Math.Ceiling(sightRange);
            var visibleTiles = _world.GetVisibleTiles(playerTile, range);
            
            // Mark them as visible and add to current set
            foreach (var tile in visibleTiles)
            {
                if (IsInBounds(tile))
                {
                    _visibility[tile.X, tile.Y] = TileVisibility.Visible;
                    _currentlyVisible.Add(tile);
                }
            }
        }
        
        /// <summary>
        /// Reveal tiles around a position (for items that grant vision, explosions, etc.)
        /// </summary>
        public void RevealArea(Point center, int radius)
        {
            if (_visibility == null || _world == null) return;
            
            var visibleTiles = _world.GetVisibleTiles(center, radius);
            foreach (var tile in visibleTiles)
            {
                if (IsInBounds(tile))
                {
                    // Only mark as explored, not visible (visibility is player-based)
                    if (_visibility[tile.X, tile.Y] == TileVisibility.Unexplored)
                    {
                        _visibility[tile.X, tile.Y] = TileVisibility.Explored;
                    }
                }
            }
        }
        
        /// <summary>
        /// Reveal entire map (cheat/debug)
        /// </summary>
        public void RevealAll()
        {
            if (_visibility == null) return;
            
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    _visibility[x, y] = TileVisibility.Explored;
                }
            }
            
            System.Diagnostics.Debug.WriteLine(">>> FogOfWar: Revealed all tiles <<<");
        }
        
        /// <summary>
        /// Reset exploration (hide everything)
        /// </summary>
        public void ResetExploration()
        {
            if (_visibility == null) return;
            
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    _visibility[x, y] = TileVisibility.Unexplored;
                }
            }
            _currentlyVisible.Clear();
            
            System.Diagnostics.Debug.WriteLine(">>> FogOfWar: Reset exploration <<<");
        }
        
        // ============================================
        // VISIBILITY QUERIES
        // ============================================
        
        /// <summary>
        /// Get visibility state of a tile
        /// </summary>
        public TileVisibility GetVisibility(Point tile)
        {
            if (DebugRevealAll) return TileVisibility.Visible;
            if (!IsEnabled) return TileVisibility.Visible;
            if (!IsInBounds(tile)) return TileVisibility.Unexplored;
            
            return _visibility[tile.X, tile.Y];
        }
        
        /// <summary>
        /// Get visibility state of a tile by coordinates
        /// </summary>
        public TileVisibility GetVisibility(int x, int y)
        {
            return GetVisibility(new Point(x, y));
        }
        
        /// <summary>
        /// Is this tile currently visible? (can see units/items)
        /// </summary>
        public bool IsVisible(Point tile)
        {
            return GetVisibility(tile) == TileVisibility.Visible;
        }
        
        /// <summary>
        /// Is this tile currently visible? (world position)
        /// </summary>
        public bool IsVisible(Vector2 worldPos, int tileSize)
        {
            Point tile = new Point((int)(worldPos.X / tileSize), (int)(worldPos.Y / tileSize));
            return IsVisible(tile);
        }
        
        /// <summary>
        /// Has this tile been explored? (can see terrain)
        /// </summary>
        public bool IsExplored(Point tile)
        {
            var vis = GetVisibility(tile);
            return vis == TileVisibility.Visible || vis == TileVisibility.Explored;
        }
        
        /// <summary>
        /// Is this tile completely unexplored? (show black)
        /// </summary>
        public bool IsUnexplored(Point tile)
        {
            return GetVisibility(tile) == TileVisibility.Unexplored;
        }
        
        /// <summary>
        /// Check if an entity at a position should be drawn
        /// </summary>
        public bool CanSeeEntity(Vector2 entityPos, int tileSize)
        {
            Point tile = new Point((int)(entityPos.X / tileSize), (int)(entityPos.Y / tileSize));
            return IsVisible(tile);
        }
        
        /// <summary>
        /// Get the set of currently visible tiles
        /// </summary>
        public IReadOnlyCollection<Point> GetVisibleTiles()
        {
            return _currentlyVisible;
        }
        
        /// <summary>
        /// Get exploration percentage (0-100)
        /// </summary>
        public float GetExplorationPercentage()
        {
            if (_visibility == null) return 0f;
            
            int explored = 0;
            int total = _width * _height;
            
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    if (_visibility[x, y] != TileVisibility.Unexplored)
                    {
                        explored++;
                    }
                }
            }
            
            return (explored / (float)total) * 100f;
        }
        
        // ============================================
        // HELPERS
        // ============================================
        
        private bool IsInBounds(Point tile)
        {
            return tile.X >= 0 && tile.X < _width && tile.Y >= 0 && tile.Y < _height;
        }
        
        /// <summary>
        /// Get fog overlay opacity for a tile (0 = no fog, 1 = full fog/black)
        /// </summary>
        public float GetFogOpacity(Point tile)
        {
            if (DebugRevealAll) return 0f;
            if (!IsEnabled) return 0f;
            
            var vis = GetVisibility(tile);
            return vis switch
            {
                TileVisibility.Visible => 0f,       // No fog
                TileVisibility.Explored => 0.6f,    // 60% darkened (fog)
                TileVisibility.Unexplored => 1f,    // Completely black
                _ => 0f
            };
        }
        
        /// <summary>
        /// Get fog color for drawing overlay
        /// </summary>
        public Color GetFogColor(Point tile)
        {
            var vis = GetVisibility(tile);
            return vis switch
            {
                TileVisibility.Visible => Color.Transparent,
                TileVisibility.Explored => new Color(0, 0, 0, 150),  // Semi-transparent black
                TileVisibility.Unexplored => Color.Black,
                _ => Color.Transparent
            };
        }
    }
}
