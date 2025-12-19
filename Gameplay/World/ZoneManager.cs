// Gameplay/World/ZoneManager.cs
// Handles multiple zones/maps and transitions between them

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MyRPG.Data;
using MyRPG.Gameplay.Entities;

namespace MyRPG.Gameplay.World
{
    // ============================================
    // ZONE EXIT
    // ============================================
    
    public class ZoneExit
    {
        public ZoneExitDirection Direction { get; set; }
        public string TargetZoneId { get; set; }
        public Point EntryPoint { get; set; }  // Where player appears in target zone
        
        public ZoneExit(ZoneExitDirection dir, string targetId, Point entry)
        {
            Direction = dir;
            TargetZoneId = targetId;
            EntryPoint = entry;
        }
    }
    
    public class ZoneData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ZoneType Type { get; set; }
        public int Width { get; set; } = 50;
        public int Height { get; set; } = 50;
        
        // Connections to other zones
        public Dictionary<ZoneExitDirection, ZoneExit> Exits { get; set; } = new Dictionary<ZoneExitDirection, ZoneExit>();
        
        // Zone properties
        public float DangerLevel { get; set; } = 1.0f;  // Enemy difficulty multiplier
        public float LootMultiplier { get; set; } = 1.0f;
        public int EnemyCount { get; set; } = 4;
        public bool HasMerchant { get; set; } = false;
        
        // Seed for procedural generation
        public int Seed { get; set; }
        
        // State persistence
        public bool HasBeenVisited { get; set; } = false;
        public List<Point> ClearedEnemyPositions { get; set; } = new List<Point>();
        
        // Entity persistence - save state when leaving zone
        public List<SavedEnemyState> SavedEnemies { get; set; } = new List<SavedEnemyState>();
        public List<SavedNPCState> SavedNPCs { get; set; } = new List<SavedNPCState>();
        
        public ZoneData(string id, string name, ZoneType type)
        {
            Id = id;
            Name = name;
            Type = type;
            Seed = id.GetHashCode();
        }
        
        public void AddExit(ZoneExitDirection dir, string targetId, Point entryPoint)
        {
            Exits[dir] = new ZoneExit(dir, targetId, entryPoint);
        }
        
        /// <summary>
        /// Save current enemy states for persistence
        /// </summary>
        public void SaveEnemyStates(List<EnemyEntity> enemies, int tileSize)
        {
            SavedEnemies.Clear();
            foreach (var enemy in enemies)
            {
                if (!enemy.IsAlive) continue;
                
                SavedEnemies.Add(new SavedEnemyState
                {
                    Type = enemy.Type,
                    Position = enemy.Position,
                    CurrentHealth = enemy.CurrentHealth,
                    IsProvoked = enemy.IsProvoked,
                    State = enemy.State
                });
            }
        }
        
        /// <summary>
        /// Save current NPC states for persistence
        /// </summary>
        public void SaveNPCStates(List<NPCEntity> npcs)
        {
            SavedNPCs.Clear();
            foreach (var npc in npcs)
            {
                SavedNPCs.Add(new SavedNPCState
                {
                    Type = npc.Type,
                    Name = npc.Name,
                    Position = npc.Position
                });
            }
        }
    }
    
    /// <summary>
    /// Saved state for enemy persistence between zone transitions
    /// </summary>
    public class SavedEnemyState
    {
        public EnemyType Type { get; set; }
        public Vector2 Position { get; set; }
        public float CurrentHealth { get; set; }
        public bool IsProvoked { get; set; }
        public EnemyState State { get; set; }
    }
    
    /// <summary>
    /// Saved state for NPC persistence
    /// </summary>
    public class SavedNPCState
    {
        public NPCType Type { get; set; }
        public string Name { get; set; }
        public Vector2 Position { get; set; }
    }
    
    // ============================================
    // ZONE MANAGER
    // ============================================
    
    public class ZoneManager
    {
        private Dictionary<string, ZoneData> _zones = new Dictionary<string, ZoneData>();
        private string _currentZoneId;
        private int _tileSize;
        
        // Events
        public event Action<string, string> OnZoneTransition;  // fromId, toId
        public event Action<ZoneData> OnZoneLoaded;
        
        public ZoneData CurrentZone => _zones.GetValueOrDefault(_currentZoneId);
        public string CurrentZoneId => _currentZoneId;
        
        public ZoneManager(int tileSize)
        {
            _tileSize = tileSize;
            InitializeZones();
        }
        
        // ============================================
        // ZONE INITIALIZATION
        // ============================================
        
        private void InitializeZones()
        {
            // ==================
            // STARTING AREA
            // ==================
            var startZone = new ZoneData("start", "Abandoned Camp", ZoneType.Wasteland)
            {
                Width = 50,
                Height = 50,
                DangerLevel = 1.0f,
                EnemyCount = 4,
                HasMerchant = true
            };
            startZone.AddExit(ZoneExitDirection.North, "ruins_south", new Point(25, 48));  // Spawn at south edge of ruins
            startZone.AddExit(ZoneExitDirection.East, "forest_west", new Point(1, 25));   // Spawn at west edge of forest
            startZone.AddExit(ZoneExitDirection.South, "wasteland_north", new Point(25, 1)); // Spawn at north edge of wasteland
            AddZone(startZone);
            
            // ==================
            // RUINED CITY (North of start)
            // ==================
            var ruinsSouth = new ZoneData("ruins_south", "Ruined Outskirts", ZoneType.Ruins)
            {
                Width = 50,
                Height = 50,
                DangerLevel = 1.5f,
                EnemyCount = 6,
                HasMerchant = false
            };
            ruinsSouth.AddExit(ZoneExitDirection.South, "start", new Point(25, 1));         // Spawn at north edge of start
            ruinsSouth.AddExit(ZoneExitDirection.North, "ruins_center", new Point(30, 58));  // Spawn at south edge of center (60x60)
            AddZone(ruinsSouth);
            
            var ruinsCenter = new ZoneData("ruins_center", "City Center", ZoneType.Ruins)
            {
                Width = 60,
                Height = 60,
                DangerLevel = 2.0f,
                EnemyCount = 8,
                HasMerchant = true,
                LootMultiplier = 1.5f
            };
            ruinsCenter.AddExit(ZoneExitDirection.South, "ruins_south", new Point(25, 1));   // Spawn at north edge of ruins_south
            ruinsCenter.AddExit(ZoneExitDirection.East, "laboratory", new Point(1, 30));    // Spawn at west edge of lab (60x60)
            AddZone(ruinsCenter);
            
            // ==================
            // MUTANT FOREST (East of start)
            // ==================
            var forestWest = new ZoneData("forest_west", "Twisted Woods", ZoneType.Forest)
            {
                Width = 50,
                Height = 50,
                DangerLevel = 1.3f,
                EnemyCount = 5,
                HasMerchant = false
            };
            forestWest.AddExit(ZoneExitDirection.West, "start", new Point(48, 25));          // Spawn at east edge of start
            forestWest.AddExit(ZoneExitDirection.East, "forest_deep", new Point(1, 25));    // Spawn at west edge of deep forest
            AddZone(forestWest);
            
            var forestDeep = new ZoneData("forest_deep", "Deep Forest", ZoneType.Forest)
            {
                Width = 50,
                Height = 50,
                DangerLevel = 2.0f,
                EnemyCount = 7,
                HasMerchant = false,
                LootMultiplier = 1.3f
            };
            forestDeep.AddExit(ZoneExitDirection.West, "forest_west", new Point(48, 25));    // Spawn at east edge of forest_west
            forestDeep.AddExit(ZoneExitDirection.North, "cave_entrance", new Point(20, 38)); // Spawn at south edge of cave (40x40)
            AddZone(forestDeep);
            
            // ==================
            // SOUTHERN WASTELAND
            // ==================
            var wastelandNorth = new ZoneData("wasteland_north", "Scorched Plains", ZoneType.Wasteland)
            {
                Width = 50,
                Height = 50,
                DangerLevel = 1.2f,
                EnemyCount = 4,
                HasMerchant = false
            };
            wastelandNorth.AddExit(ZoneExitDirection.North, "start", new Point(25, 48));     // Spawn at south edge of start
            wastelandNorth.AddExit(ZoneExitDirection.South, "settlement", new Point(20, 1)); // Spawn at north edge of settlement (40x40)
            AddZone(wastelandNorth);
            
            // ==================
            // FRIENDLY SETTLEMENT
            // ==================
            var settlement = new ZoneData("settlement", "Haven", ZoneType.Settlement)
            {
                Width = 40,
                Height = 40,
                DangerLevel = 0.5f,
                EnemyCount = 1,  // Just guards maybe
                HasMerchant = true,
                LootMultiplier = 0.5f
            };
            settlement.AddExit(ZoneExitDirection.North, "wasteland_north", new Point(25, 48)); // Spawn at south edge of wasteland
            AddZone(settlement);
            
            // ==================
            // CAVE SYSTEM
            // ==================
            var caveEntrance = new ZoneData("cave_entrance", "Cave Mouth", ZoneType.Cave)
            {
                Width = 40,
                Height = 40,
                DangerLevel = 1.8f,
                EnemyCount = 5,
                HasMerchant = false
            };
            caveEntrance.AddExit(ZoneExitDirection.South, "forest_deep", new Point(25, 1));   // Spawn at north edge of forest
            caveEntrance.AddExit(ZoneExitDirection.North, "cave_depths", new Point(25, 48)); // Spawn at south edge of depths
            AddZone(caveEntrance);
            
            var caveDepths = new ZoneData("cave_depths", "Abyssal Depths", ZoneType.Cave)
            {
                Width = 50,
                Height = 50,
                DangerLevel = 2.5f,
                EnemyCount = 8,
                HasMerchant = false,
                LootMultiplier = 2.0f
            };
            caveDepths.AddExit(ZoneExitDirection.South, "cave_entrance", new Point(20, 1));   // Spawn at north edge of entrance (40x40)
            AddZone(caveDepths);
            
            // ==================
            // LABORATORY (End game area)
            // ==================
            var laboratory = new ZoneData("laboratory", "Research Facility", ZoneType.Laboratory)
            {
                Width = 60,
                Height = 60,
                DangerLevel = 3.0f,
                EnemyCount = 10,
                HasMerchant = false,
                LootMultiplier = 2.5f
            };
            laboratory.AddExit(ZoneExitDirection.West, "ruins_center", new Point(58, 30));    // Spawn at east edge of ruins_center (60x60)
            AddZone(laboratory);
            
            // Set starting zone
            _currentZoneId = "start";
            
            System.Diagnostics.Debug.WriteLine($">>> ZoneManager: Initialized {_zones.Count} zones <<<");
        }
        
        private void AddZone(ZoneData zone)
        {
            _zones[zone.Id] = zone;
        }
        
        // ============================================
        // ZONE ACCESS
        // ============================================
        
        public ZoneData GetZone(string id)
        {
            return _zones.GetValueOrDefault(id);
        }
        
        public List<ZoneData> GetAllZones()
        {
            return new List<ZoneData>(_zones.Values);
        }
        
        public void SetCurrentZone(string id)
        {
            if (_zones.ContainsKey(id))
            {
                string oldId = _currentZoneId;
                _currentZoneId = id;
                _zones[id].HasBeenVisited = true;
                
                OnZoneTransition?.Invoke(oldId, id);
                OnZoneLoaded?.Invoke(_zones[id]);
            }
        }
        
        // ============================================
        // ZONE TRANSITIONS
        // ============================================
        
        /// <summary>
        /// Check if player is at a zone exit and return the exit info
        /// </summary>
        public ZoneExit CheckForExit(Point playerTile, int zoneWidth, int zoneHeight)
        {
            var zone = CurrentZone;
            if (zone == null) return null;
            
            // Check each edge
            if (playerTile.Y <= 0 && zone.Exits.ContainsKey(ZoneExitDirection.North))
            {
                return zone.Exits[ZoneExitDirection.North];
            }
            if (playerTile.Y >= zoneHeight - 1 && zone.Exits.ContainsKey(ZoneExitDirection.South))
            {
                return zone.Exits[ZoneExitDirection.South];
            }
            if (playerTile.X <= 0 && zone.Exits.ContainsKey(ZoneExitDirection.West))
            {
                return zone.Exits[ZoneExitDirection.West];
            }
            if (playerTile.X >= zoneWidth - 1 && zone.Exits.ContainsKey(ZoneExitDirection.East))
            {
                return zone.Exits[ZoneExitDirection.East];
            }
            
            return null;
        }
        
        /// <summary>
        /// Get display info for zone exits (for UI hints)
        /// </summary>
        public string GetExitHint(ZoneExitDirection dir)
        {
            var zone = CurrentZone;
            if (zone == null || !zone.Exits.ContainsKey(dir)) return null;
            
            var exit = zone.Exits[dir];
            var targetZone = GetZone(exit.TargetZoneId);
            if (targetZone == null) return null;
            
            return $"{dir}: {targetZone.Name}";
        }
        
        // ============================================
        // WORLD GENERATION
        // ============================================
        
        /// <summary>
        /// Generate or regenerate world grid for a zone
        /// </summary>
        public void GenerateZoneWorld(WorldGrid world, ZoneData zone)
        {
            Random rand = new Random(zone.Seed);
            
            // Clear to base terrain
            TileType baseTerrain = zone.Type switch
            {
                ZoneType.Wasteland => TileType.Dirt,
                ZoneType.Ruins => TileType.Stone,
                ZoneType.Settlement => TileType.Grass,
                ZoneType.Cave => TileType.Stone,
                ZoneType.Laboratory => TileType.Stone,
                ZoneType.Forest => TileType.Grass,
                _ => TileType.Grass
            };
            
            // Fill with base terrain
            for (int x = 0; x < world.Width; x++)
            {
                for (int y = 0; y < world.Height; y++)
                {
                    world.SetTile(x, y, baseTerrain);
                }
            }
            
            // Add terrain variation
            switch (zone.Type)
            {
                case ZoneType.Wasteland:
                    GenerateWasteland(world, rand);
                    break;
                case ZoneType.Ruins:
                    GenerateRuins(world, rand);
                    break;
                case ZoneType.Forest:
                    GenerateForest(world, rand);
                    break;
                case ZoneType.Cave:
                    GenerateCave(world, rand);
                    break;
                case ZoneType.Settlement:
                    GenerateSettlement(world, rand);
                    break;
                case ZoneType.Laboratory:
                    GenerateLaboratory(world, rand);
                    break;
            }
            
            // Ensure exits are walkable
            foreach (var exit in zone.Exits.Values)
            {
                Point exitTile = GetExitTile(exit.Direction, world.Width, world.Height);
                // Clear a path at exit
                for (int i = -1; i <= 1; i++)
                {
                    int x = exitTile.X + (exit.Direction == ZoneExitDirection.North || exit.Direction == ZoneExitDirection.South ? i : 0);
                    int y = exitTile.Y + (exit.Direction == ZoneExitDirection.East || exit.Direction == ZoneExitDirection.West ? i : 0);
                    if (x >= 0 && x < world.Width && y >= 0 && y < world.Height)
                    {
                        world.SetTile(x, y, baseTerrain);
                    }
                }
            }
        }
        
        private Point GetExitTile(ZoneExitDirection dir, int width, int height)
        {
            return dir switch
            {
                ZoneExitDirection.North => new Point(width / 2, 0),
                ZoneExitDirection.South => new Point(width / 2, height - 1),
                ZoneExitDirection.East => new Point(width - 1, height / 2),
                ZoneExitDirection.West => new Point(0, height / 2),
                _ => new Point(width / 2, height / 2)
            };
        }
        
        private void GenerateWasteland(WorldGrid world, Random rand)
        {
            // Scatter rocks and sand
            for (int i = 0; i < 100; i++)
            {
                int x = rand.Next(world.Width);
                int y = rand.Next(world.Height);
                world.SetTile(x, y, rand.NextDouble() > 0.5 ? TileType.Sand : TileType.Stone);
            }
            
            // Add some water pools
            for (int i = 0; i < 3; i++)
            {
                int cx = rand.Next(5, world.Width - 5);
                int cy = rand.Next(5, world.Height - 5);
                int r = rand.Next(2, 4);
                
                for (int x = cx - r; x <= cx + r; x++)
                {
                    for (int y = cy - r; y <= cy + r; y++)
                    {
                        if (x >= 0 && x < world.Width && y >= 0 && y < world.Height)
                        {
                            if (Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) < r)
                            {
                                world.SetTile(x, y, TileType.Water);
                            }
                        }
                    }
                }
            }
        }
        
        private void GenerateRuins(WorldGrid world, Random rand)
        {
            // Add ruined building outlines (stone walls)
            for (int b = 0; b < 8; b++)
            {
                int bx = rand.Next(5, world.Width - 10);
                int by = rand.Next(5, world.Height - 10);
                int bw = rand.Next(4, 8);
                int bh = rand.Next(4, 8);
                
                // Draw walls with gaps
                for (int x = bx; x < bx + bw; x++)
                {
                    if (rand.NextDouble() > 0.3) world.SetTile(x, by, TileType.StoneWall);
                    if (rand.NextDouble() > 0.3) world.SetTile(x, by + bh - 1, TileType.StoneWall);
                }
                for (int y = by; y < by + bh; y++)
                {
                    if (rand.NextDouble() > 0.3) world.SetTile(bx, y, TileType.StoneWall);
                    if (rand.NextDouble() > 0.3) world.SetTile(bx + bw - 1, y, TileType.StoneWall);
                }
            }
            
            // Scatter rubble
            for (int i = 0; i < 50; i++)
            {
                int x = rand.Next(world.Width);
                int y = rand.Next(world.Height);
                if (world.GetTile(x, y).Type != TileType.StoneWall)
                {
                    world.SetTile(x, y, TileType.Dirt);
                }
            }
        }
        
        private void GenerateForest(WorldGrid world, Random rand)
        {
            // Dense trees (stone walls as trees)
            for (int i = 0; i < 150; i++)
            {
                int x = rand.Next(2, world.Width - 2);
                int y = rand.Next(2, world.Height - 2);
                world.SetTile(x, y, TileType.StoneWall);  // Tree
            }
            
            // Clearings
            for (int c = 0; c < 5; c++)
            {
                int cx = rand.Next(8, world.Width - 8);
                int cy = rand.Next(8, world.Height - 8);
                int r = rand.Next(3, 6);
                
                for (int x = cx - r; x <= cx + r; x++)
                {
                    for (int y = cy - r; y <= cy + r; y++)
                    {
                        if (x >= 0 && x < world.Width && y >= 0 && y < world.Height)
                        {
                            if (Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) < r)
                            {
                                world.SetTile(x, y, TileType.Grass);
                            }
                        }
                    }
                }
            }
        }
        
        private void GenerateCave(WorldGrid world, Random rand)
        {
            // Fill with walls first
            for (int x = 0; x < world.Width; x++)
            {
                for (int y = 0; y < world.Height; y++)
                {
                    world.SetTile(x, y, TileType.StoneWall);
                }
            }
            
            // Carve out passages using cellular automata-ish approach
            // Start with random open spaces
            for (int x = 2; x < world.Width - 2; x++)
            {
                for (int y = 2; y < world.Height - 2; y++)
                {
                    if (rand.NextDouble() > 0.55)
                    {
                        world.SetTile(x, y, TileType.Stone);
                    }
                }
            }
            
            // Smooth passes
            for (int pass = 0; pass < 3; pass++)
            {
                for (int x = 2; x < world.Width - 2; x++)
                {
                    for (int y = 2; y < world.Height - 2; y++)
                    {
                        int walls = CountNeighborWalls(world, x, y);
                        if (walls > 4)
                            world.SetTile(x, y, TileType.StoneWall);
                        else if (walls < 4)
                            world.SetTile(x, y, TileType.Stone);
                    }
                }
            }
            
            // Ensure center is open
            int centerX = world.Width / 2;
            int centerY = world.Height / 2;
            for (int x = centerX - 5; x <= centerX + 5; x++)
            {
                for (int y = centerY - 5; y <= centerY + 5; y++)
                {
                    if (x >= 0 && x < world.Width && y >= 0 && y < world.Height)
                    {
                        world.SetTile(x, y, TileType.Stone);
                    }
                }
            }
        }
        
        private int CountNeighborWalls(WorldGrid world, int cx, int cy)
        {
            int count = 0;
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                for (int y = cy - 1; y <= cy + 1; y++)
                {
                    if (x == cx && y == cy) continue;
                    if (x < 0 || x >= world.Width || y < 0 || y >= world.Height)
                    {
                        count++;
                    }
                    else if (world.GetTile(x, y).Type == TileType.StoneWall)
                    {
                        count++;
                    }
                }
            }
            return count;
        }
        
        private void GenerateSettlement(WorldGrid world, Random rand)
        {
            // Nice grass with paths
            // Create dirt paths
            int pathY = world.Height / 2;
            for (int x = 0; x < world.Width; x++)
            {
                world.SetTile(x, pathY, TileType.Dirt);
                world.SetTile(x, pathY + 1, TileType.Dirt);
            }
            
            int pathX = world.Width / 2;
            for (int y = 0; y < world.Height; y++)
            {
                world.SetTile(pathX, y, TileType.Dirt);
                world.SetTile(pathX + 1, y, TileType.Dirt);
            }
            
            // Some decorative stones
            for (int i = 0; i < 20; i++)
            {
                int x = rand.Next(world.Width);
                int y = rand.Next(world.Height);
                world.SetTile(x, y, TileType.Stone);
            }
        }
        
        private void GenerateLaboratory(WorldGrid world, Random rand)
        {
            // Grid-like corridors
            for (int x = 0; x < world.Width; x++)
            {
                for (int y = 0; y < world.Height; y++)
                {
                    bool isCorridorX = (x % 8 < 3);
                    bool isCorridorY = (y % 8 < 3);
                    
                    if (isCorridorX || isCorridorY)
                    {
                        world.SetTile(x, y, TileType.Stone);
                    }
                    else
                    {
                        world.SetTile(x, y, TileType.StoneWall);
                    }
                }
            }
            
            // Random openings in walls
            for (int i = 0; i < 30; i++)
            {
                int x = rand.Next(world.Width);
                int y = rand.Next(world.Height);
                world.SetTile(x, y, TileType.Stone);
            }
        }
        
        // ============================================
        // ENEMY GENERATION FOR ZONES
        // ============================================
        
        public List<EnemyEntity> GenerateZoneEnemies(ZoneData zone, int tileSize)
        {
            var enemies = new List<EnemyEntity>();
            
            // If zone has saved enemy states, restore them instead of generating new
            if (zone.SavedEnemies.Count > 0)
            {
                int index = 1;
                foreach (var saved in zone.SavedEnemies)
                {
                    var enemy = EnemyEntity.Create(saved.Type, saved.Position, index++);
                    enemy.CurrentHealth = saved.CurrentHealth;
                    enemy.IsProvoked = saved.IsProvoked;
                    enemy.State = saved.State;
                    enemies.Add(enemy);
                }
                System.Diagnostics.Debug.WriteLine($">>> Restored {enemies.Count} enemies from saved state <<<");
                return enemies;
            }
            
            // First visit - generate new enemies
            var occupiedTiles = new HashSet<Point>();
            Random rand = new Random(zone.Seed + 1000);
            
            // Spawn hostile enemies
            for (int i = 0; i < zone.EnemyCount; i++)
            {
                EnemyType type = PickHostileForZone(zone.Type, zone.DangerLevel, rand);
                
                // Find unoccupied position
                Point pos = FindUnoccupiedSpawn(zone, rand, occupiedTiles, 5, zone.Width - 5, 5, zone.Height - 5);
                
                // Skip if already cleared or couldn't find spot
                if (zone.ClearedEnemyPositions.Contains(pos)) continue;
                if (occupiedTiles.Contains(pos)) continue;
                
                occupiedTiles.Add(pos);
                var enemy = EnemyEntity.Create(type, new Vector2(pos.X * tileSize, pos.Y * tileSize), i + 1);
                enemies.Add(enemy);
            }
            
            // Spawn passive creatures (separate pool)
            int passiveCount = GetPassiveCount(zone.Type, rand);
            for (int i = 0; i < passiveCount; i++)
            {
                EnemyType type = PickPassiveForZone(zone.Type, rand);
                
                // Find unoccupied position
                Point pos = FindUnoccupiedSpawn(zone, rand, occupiedTiles, 3, zone.Width - 3, 3, zone.Height - 3);
                
                if (occupiedTiles.Contains(pos)) continue;
                
                occupiedTiles.Add(pos);
                var creature = EnemyEntity.Create(type, new Vector2(pos.X * tileSize, pos.Y * tileSize), 100 + i);
                enemies.Add(creature);
            }
            
            return enemies;
        }
        
        /// <summary>
        /// Find an unoccupied spawn position
        /// </summary>
        private Point FindUnoccupiedSpawn(ZoneData zone, Random rand, HashSet<Point> occupied, int minX, int maxX, int minY, int maxY)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                int x = rand.Next(minX, maxX);
                int y = rand.Next(minY, maxY);
                Point pos = new Point(x, y);
                
                if (!occupied.Contains(pos) && !zone.ClearedEnemyPositions.Contains(pos))
                {
                    return pos;
                }
            }
            
            // Fallback: spiral search from center
            int cx = (minX + maxX) / 2;
            int cy = (minY + maxY) / 2;
            for (int radius = 1; radius < 20; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        Point pos = new Point(cx + dx, cy + dy);
                        if (pos.X >= minX && pos.X < maxX && pos.Y >= minY && pos.Y < maxY)
                        {
                            if (!occupied.Contains(pos) && !zone.ClearedEnemyPositions.Contains(pos))
                            {
                                return pos;
                            }
                        }
                    }
                }
            }
            
            return new Point(minX, minY);  // Last resort
        }
        
        private int GetPassiveCount(ZoneType zoneType, Random rand)
        {
            return zoneType switch
            {
                ZoneType.Wasteland => rand.Next(2, 5),    // Some scavengers
                ZoneType.Ruins => rand.Next(1, 3),        // Few creatures
                ZoneType.Forest => rand.Next(4, 8),       // Many animals
                ZoneType.Cave => rand.Next(2, 4),         // Insects and slugs
                ZoneType.Settlement => rand.Next(0, 2),   // Almost none
                ZoneType.Laboratory => rand.Next(1, 3),   // Escaped experiments
                _ => rand.Next(1, 4)
            };
        }
        
        private EnemyType PickHostileForZone(ZoneType zoneType, float dangerLevel, Random rand)
        {
            // Small chance for special ability enemies based on danger level
            double specialChance = dangerLevel * 0.15;  // Up to 15% at max danger
            double roll = rand.NextDouble();
            
            if (roll < specialChance)
            {
                // Pick a special enemy type
                return PickSpecialEnemy(zoneType, rand);
            }
            
            // Regular weighted selection based on zone type
            return zoneType switch
            {
                ZoneType.Wasteland => rand.NextDouble() > 0.7 ? EnemyType.MutantBeast : EnemyType.Raider,
                ZoneType.Ruins => rand.NextDouble() > 0.5 ? EnemyType.Raider : EnemyType.Hunter,
                ZoneType.Forest => rand.NextDouble() > 0.6 ? EnemyType.MutantBeast : EnemyType.Raider,
                ZoneType.Cave => rand.NextDouble() > 0.4 ? EnemyType.Abomination : EnemyType.MutantBeast,
                ZoneType.Settlement => EnemyType.Raider,  // Rare encounters
                ZoneType.Laboratory => rand.NextDouble() > 0.3 ? EnemyType.Abomination : EnemyType.Hunter,
                _ => EnemyType.Raider
            };
        }
        
        private EnemyType PickSpecialEnemy(ZoneType zoneType, Random rand)
        {
            // Zone-appropriate special enemies
            return zoneType switch
            {
                ZoneType.Wasteland => rand.NextDouble() > 0.5 ? EnemyType.Stalker : EnemyType.Brute,
                ZoneType.Ruins => rand.NextDouble() > 0.5 ? EnemyType.Stalker : EnemyType.Spitter,
                ZoneType.Forest => rand.NextDouble() > 0.5 ? EnemyType.HiveMother : EnemyType.Stalker,
                ZoneType.Cave => rand.NextDouble() > 0.6 ? EnemyType.Brute : 
                                 rand.NextDouble() > 0.3 ? EnemyType.Spitter : EnemyType.HiveMother,
                ZoneType.Settlement => EnemyType.Stalker,  // Assassin types
                ZoneType.Laboratory => rand.NextDouble() > 0.5 ? EnemyType.Psionic : EnemyType.Spitter,
                _ => EnemyType.Brute
            };
        }
        
        private EnemyType PickPassiveForZone(ZoneType zoneType, Random rand)
        {
            double roll = rand.NextDouble();
            
            return zoneType switch
            {
                ZoneType.Wasteland => roll > 0.6 ? EnemyType.Scavenger : EnemyType.WildBoar,
                ZoneType.Ruins => roll > 0.5 ? EnemyType.Scavenger : EnemyType.GiantInsect,
                ZoneType.Forest => roll > 0.7 ? EnemyType.WildBoar : 
                                   roll > 0.4 ? EnemyType.MutantDeer : EnemyType.Scavenger,
                ZoneType.Cave => roll > 0.5 ? EnemyType.CaveSlug : EnemyType.GiantInsect,
                ZoneType.Settlement => EnemyType.Scavenger,
                ZoneType.Laboratory => roll > 0.5 ? EnemyType.GiantInsect : EnemyType.CaveSlug,
                _ => EnemyType.Scavenger
            };
        }
    }
}
