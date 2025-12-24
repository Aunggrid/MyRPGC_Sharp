// Gameplay/World/ZoneManager.cs
// Handles multiple zones/maps and transitions between them
// Based on the World of Orodia lore - The Exclusion Zone (ruins of Aethelgard)

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
    
    // ============================================
    // ZONE DATA
    // ============================================
    
    public class ZoneData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }  // Lore description
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
        
        // Faction control (from Orodia lore)
        public FactionType ControllingFaction { get; set; } = FactionType.TheChanged;
        
        // FREE ZONE PROPERTIES
        public bool IsFreeZone { get; set; } = false;           // Is this a free exploration zone?
        public bool AllowBaseBuilding { get; set; } = false;    // Can player build base here?
        public float ResourceMultiplier { get; set; } = 1.0f;   // Material gathering bonus
        public ZoneType BiomeType { get; set; }                 // For free zones: what biome spawned
        
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
            BiomeType = type;
            Seed = id.GetHashCode();
            Description = "";
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
        // ZONE INITIALIZATION - THE WORLD OF ORODIA
        // ============================================
        // The Exclusion Zone: Ruins of Aethelgard
        // 400 years after The Severance opened a door to The Void
        // ============================================
        
        private void InitializeZones()
        {
            // ==========================================
            // RUSTHOLLOW - Player Starting Hub
            // Major Changed trading settlement in Outer Ruins
            // ==========================================
            var rusthollow = new ZoneData("rusthollow", "Rusthollow", ZoneType.Settlement)
            {
                Description = "A ramshackle trading hub built from scavenged ruins. Home to the Changed - your people.",
                Width = 45,
                Height = 45,
                DangerLevel = 0.5f,
                EnemyCount = 2,  // Just guards
                HasMerchant = true,
                LootMultiplier = 0.5f,
                ControllingFaction = FactionType.TheChanged
            };
            rusthollow.AddExit(ZoneExitDirection.North, "outer_ruins_north", new Point(25, 48));
            rusthollow.AddExit(ZoneExitDirection.East, "outer_ruins_east", new Point(1, 25));
            rusthollow.AddExit(ZoneExitDirection.South, "scavenger_plains", new Point(25, 1));
            rusthollow.AddExit(ZoneExitDirection.West, "twisted_woods", new Point(48, 25));
            AddZone(rusthollow);
            
            // ==========================================
            // OUTER RUINS - Edge of the Exclusion Zone
            // Least corruption, scavengers frequent here
            // ==========================================
            
            var outerRuinsNorth = new ZoneData("outer_ruins_north", "Outer Ruins - North", ZoneType.OuterRuins)
            {
                Description = "Crumbling buildings at the edge of the Zone. The violet shimmer is faint here.",
                Width = 50,
                Height = 50,
                DangerLevel = 1.2f,
                EnemyCount = 5,
                HasMerchant = false,
                LootMultiplier = 1.0f,
                ControllingFaction = FactionType.TheChanged
            };
            outerRuinsNorth.AddExit(ZoneExitDirection.South, "rusthollow", new Point(22, 1));
            outerRuinsNorth.AddExit(ZoneExitDirection.North, "inner_ruins_south", new Point(25, 48));
            outerRuinsNorth.AddExit(ZoneExitDirection.East, "syndicate_post", new Point(1, 25));
            AddZone(outerRuinsNorth);
            
            var outerRuinsEast = new ZoneData("outer_ruins_east", "Outer Ruins - East", ZoneType.OuterRuins)
            {
                Description = "Collapsed warehouses and factories. Raiders sometimes camp here.",
                Width = 50,
                Height = 50,
                DangerLevel = 1.3f,
                EnemyCount = 6,
                HasMerchant = false,
                LootMultiplier = 1.1f,
                ControllingFaction = FactionType.TheChanged
            };
            outerRuinsEast.AddExit(ZoneExitDirection.West, "rusthollow", new Point(43, 22));
            outerRuinsEast.AddExit(ZoneExitDirection.North, "syndicate_post", new Point(25, 48));
            outerRuinsEast.AddExit(ZoneExitDirection.East, "forward_base_purity", new Point(1, 25));
            AddZone(outerRuinsEast);
            
            // ==========================================
            // SYNDICATE POST SEVEN - Iron Syndicate Outpost
            // Trade with anyone, enslaves mutants
            // ==========================================
            var syndicatePost = new ZoneData("syndicate_post", "Syndicate Post Seven", ZoneType.TradingPost)
            {
                Description = "Iron Syndicate trade outpost. They'll deal with mutants... for the right price.",
                Width = 40,
                Height = 40,
                DangerLevel = 1.0f,
                EnemyCount = 3,  // Syndicate guards
                HasMerchant = true,
                LootMultiplier = 0.8f,
                ControllingFaction = FactionType.IronSyndicate
            };
            syndicatePost.AddExit(ZoneExitDirection.West, "outer_ruins_north", new Point(48, 25));
            syndicatePost.AddExit(ZoneExitDirection.South, "outer_ruins_east", new Point(25, 1));
            syndicatePost.AddExit(ZoneExitDirection.North, "inner_ruins_east", new Point(25, 48));
            AddZone(syndicatePost);
            
            // ==========================================
            // FORWARD BASE PURITY - United Sanctum Military
            // Hostile to mutants - "Biological hazards"
            // ==========================================
            var forwardBase = new ZoneData("forward_base_purity", "Forward Base Purity", ZoneType.SanctumOutpost)
            {
                Description = "Sanctum military installation. DANGER: They shoot mutants on sight.",
                Width = 45,
                Height = 45,
                DangerLevel = 2.5f,
                EnemyCount = 8,  // Sanctum troopers
                HasMerchant = false,
                LootMultiplier = 1.8f,  // Good tech loot
                ControllingFaction = FactionType.UnitedSanctum
            };
            forwardBase.AddExit(ZoneExitDirection.West, "outer_ruins_east", new Point(48, 25));
            forwardBase.AddExit(ZoneExitDirection.North, "the_nursery", new Point(22, 43));
            AddZone(forwardBase);
            
            // ==========================================
            // THE NURSERY - Verdant Order Research Facility
            // They want to "study" mutants
            // ==========================================
            var theNursery = new ZoneData("the_nursery", "The Nursery", ZoneType.VerdantLab)
            {
                Description = "Verdant Order research facility. Collectors hunt 'specimens' here. Horrors within.",
                Width = 55,
                Height = 55,
                DangerLevel = 2.8f,
                EnemyCount = 10,
                HasMerchant = false,
                LootMultiplier = 2.0f,  // Medical supplies, bio-chemicals
                ControllingFaction = FactionType.VerdantOrder
            };
            theNursery.AddExit(ZoneExitDirection.South, "forward_base_purity", new Point(22, 1));
            theNursery.AddExit(ZoneExitDirection.West, "inner_ruins_east", new Point(48, 27));
            AddZone(theNursery);
            
            // ==========================================
            // INNER RUINS - Deeper into the Zone
            // More danger, better loot, faction patrols
            // ==========================================
            
            var innerRuinsSouth = new ZoneData("inner_ruins_south", "Inner Ruins - South", ZoneType.InnerRuins)
            {
                Description = "Deeper ruins. The violet shimmer grows stronger. Patrol carefully.",
                Width = 55,
                Height = 55,
                DangerLevel = 1.8f,
                EnemyCount = 7,
                HasMerchant = false,
                LootMultiplier = 1.5f,
                ControllingFaction = FactionType.TheChanged
            };
            innerRuinsSouth.AddExit(ZoneExitDirection.South, "outer_ruins_north", new Point(25, 1));
            innerRuinsSouth.AddExit(ZoneExitDirection.North, "the_spire", new Point(27, 48));
            innerRuinsSouth.AddExit(ZoneExitDirection.East, "inner_ruins_east", new Point(1, 27));
            innerRuinsSouth.AddExit(ZoneExitDirection.West, "dark_forest", new Point(48, 27));
            AddZone(innerRuinsSouth);
            
            var innerRuinsEast = new ZoneData("inner_ruins_east", "Inner Ruins - East", ZoneType.InnerRuins)
            {
                Description = "Crumbling towers lean at impossible angles. Time moves strangely here.",
                Width = 55,
                Height = 55,
                DangerLevel = 2.0f,
                EnemyCount = 8,
                HasMerchant = false,
                LootMultiplier = 1.6f,
                ControllingFaction = FactionType.TheChanged
            };
            innerRuinsEast.AddExit(ZoneExitDirection.South, "syndicate_post", new Point(20, 1));
            innerRuinsEast.AddExit(ZoneExitDirection.West, "inner_ruins_south", new Point(53, 27));
            innerRuinsEast.AddExit(ZoneExitDirection.East, "the_nursery", new Point(1, 27));
            innerRuinsEast.AddExit(ZoneExitDirection.North, "vault_omega", new Point(25, 48));
            AddZone(innerRuinsEast);
            
            // ==========================================
            // THE SPIRE - Gene-Elder Stronghold
            // Powerful mutant leaders, respected and feared
            // ==========================================
            var theSpire = new ZoneData("the_spire", "The Spire", ZoneType.GeneElderHold)
            {
                Description = "Ancient tower, home to the Gene-Elders. Show respect or face their wrath.",
                Width = 40,
                Height = 50,
                DangerLevel = 1.5f,  // Safe if you're Changed
                EnemyCount = 4,
                HasMerchant = true,
                LootMultiplier = 1.2f,
                ControllingFaction = FactionType.GeneElders
            };
            theSpire.AddExit(ZoneExitDirection.South, "inner_ruins_south", new Point(27, 1));
            theSpire.AddExit(ZoneExitDirection.North, "deep_zone_south", new Point(20, 48));
            AddZone(theSpire);
            
            // ==========================================
            // VAULT OMEGA - Sealed Aethelgard Bunker
            // 400 years old, untouched, relics inside
            // ==========================================
            var vaultOmega = new ZoneData("vault_omega", "Vault Omega", ZoneType.AncientVault)
            {
                Description = "Sealed Aethelgard bunker. 400 years untouched. What technology lies within?",
                Width = 60,
                Height = 60,
                DangerLevel = 2.5f,
                EnemyCount = 8,  // Security systems still active
                HasMerchant = false,
                LootMultiplier = 3.0f,  // Ancient relics!
                ControllingFaction = FactionType.TheChanged  // Unclaimed
            };
            vaultOmega.AddExit(ZoneExitDirection.South, "inner_ruins_east", new Point(27, 1));
            vaultOmega.AddExit(ZoneExitDirection.North, "deep_zone_east", new Point(30, 48));
            AddZone(vaultOmega);
            
            // ==========================================
            // DARK FOREST - Void-touched Wilderness
            // Trees grow wrong, shadows move on their own
            // ==========================================
            var darkForest = new ZoneData("dark_forest", "The Dark Forest", ZoneType.DarkForest)
            {
                Description = "Void-touched forest. Trees grow in spirals. Shadows have eyes.",
                Width = 50,
                Height = 50,
                DangerLevel = 2.2f,
                EnemyCount = 7,
                HasMerchant = false,
                LootMultiplier = 1.4f,
                ControllingFaction = FactionType.VoidCult
            };
            darkForest.AddExit(ZoneExitDirection.East, "inner_ruins_south", new Point(1, 27));
            darkForest.AddExit(ZoneExitDirection.South, "twisted_woods", new Point(25, 1));
            darkForest.AddExit(ZoneExitDirection.North, "void_temple", new Point(25, 48));
            AddZone(darkForest);
            
            // ==========================================
            // TWISTED WOODS - Outer Forest
            // Mutated wildlife, less corrupted than Dark Forest
            // ==========================================
            var twistedWoods = new ZoneData("twisted_woods", "Twisted Woods", ZoneType.Forest)
            {
                Description = "Mutated forest at the Zone's edge. The beasts here are territorial.",
                Width = 50,
                Height = 50,
                DangerLevel = 1.4f,
                EnemyCount = 6,
                HasMerchant = false,
                LootMultiplier = 1.0f,
                ControllingFaction = FactionType.Wildlife
            };
            twistedWoods.AddExit(ZoneExitDirection.East, "rusthollow", new Point(1, 22));
            twistedWoods.AddExit(ZoneExitDirection.North, "dark_forest", new Point(25, 48));
            AddZone(twistedWoods);
            
            // ==========================================
            // SCAVENGER PLAINS - Southern Wasteland
            // Open terrain, raiders, easy scavenging
            // ==========================================
            var scavengerPlains = new ZoneData("scavenger_plains", "Scavenger Plains", ZoneType.Wasteland)
            {
                Description = "Open wasteland south of Rusthollow. Scavengers and raiders roam freely.",
                Width = 55,
                Height = 55,
                DangerLevel = 1.1f,
                EnemyCount = 5,
                HasMerchant = false,
                LootMultiplier = 0.9f,
                ControllingFaction = FactionType.TheChanged
            };
            scavengerPlains.AddExit(ZoneExitDirection.North, "rusthollow", new Point(22, 43));
            AddZone(scavengerPlains);
            
            // ==========================================
            // VOID TEMPLE - Cult of the Consuming Void
            // Dark Science practitioners, embrace corruption
            // ==========================================
            var voidTemple = new ZoneData("void_temple", "Temple of the Consuming Void", ZoneType.VoidRift)
            {
                Description = "The Void Cult's hidden temple. They embrace the corruption others fear.",
                Width = 45,
                Height = 45,
                DangerLevel = 2.5f,
                EnemyCount = 8,
                HasMerchant = true,  // Void Shard trader
                LootMultiplier = 1.8f,
                ControllingFaction = FactionType.VoidCult
            };
            voidTemple.AddExit(ZoneExitDirection.South, "dark_forest", new Point(25, 1));
            voidTemple.AddExit(ZoneExitDirection.North, "deep_zone_west", new Point(22, 48));
            AddZone(voidTemple);
            
            // ==========================================
            // DEEP ZONE - Near the Epicenter
            // Extreme corruption, Void Spawn common
            // ==========================================
            
            var deepZoneSouth = new ZoneData("deep_zone_south", "Deep Zone - South", ZoneType.DeepZone)
            {
                Description = "The corruption is overwhelming. Reality warps visibly. Turn back.",
                Width = 50,
                Height = 50,
                DangerLevel = 2.8f,
                EnemyCount = 9,
                HasMerchant = false,
                LootMultiplier = 2.2f,
                ControllingFaction = FactionType.VoidSpawn
            };
            deepZoneSouth.AddExit(ZoneExitDirection.South, "the_spire", new Point(20, 1));
            deepZoneSouth.AddExit(ZoneExitDirection.North, "the_wound", new Point(25, 48));
            deepZoneSouth.AddExit(ZoneExitDirection.East, "deep_zone_east", new Point(1, 25));
            deepZoneSouth.AddExit(ZoneExitDirection.West, "deep_zone_west", new Point(48, 25));
            AddZone(deepZoneSouth);
            
            var deepZoneEast = new ZoneData("deep_zone_east", "Deep Zone - East", ZoneType.DeepZone)
            {
                Description = "Gravity shifts without warning. Time dilates in pockets.",
                Width = 50,
                Height = 50,
                DangerLevel = 3.0f,
                EnemyCount = 10,
                HasMerchant = false,
                LootMultiplier = 2.4f,
                ControllingFaction = FactionType.VoidSpawn
            };
            deepZoneEast.AddExit(ZoneExitDirection.South, "vault_omega", new Point(30, 1));
            deepZoneEast.AddExit(ZoneExitDirection.West, "deep_zone_south", new Point(48, 25));
            deepZoneEast.AddExit(ZoneExitDirection.North, "the_wound", new Point(40, 48));
            AddZone(deepZoneEast);
            
            var deepZoneWest = new ZoneData("deep_zone_west", "Deep Zone - West", ZoneType.DeepZone)
            {
                Description = "The air shimmers violet. Whispers echo from nowhere.",
                Width = 50,
                Height = 50,
                DangerLevel = 2.9f,
                EnemyCount = 9,
                HasMerchant = false,
                LootMultiplier = 2.3f,
                ControllingFaction = FactionType.VoidSpawn
            };
            deepZoneWest.AddExit(ZoneExitDirection.South, "void_temple", new Point(22, 1));
            deepZoneWest.AddExit(ZoneExitDirection.East, "deep_zone_south", new Point(1, 25));
            deepZoneWest.AddExit(ZoneExitDirection.North, "the_wound", new Point(10, 48));
            AddZone(deepZoneWest);
            
            // ==========================================
            // THE RIFT - Active Tear in Reality
            // Void Spawn nest, endgame dungeon
            // ==========================================
            var theRift = new ZoneData("the_rift", "The Rift", ZoneType.VoidRift)
            {
                Description = "An active tear in reality. Void Spawn pour through endlessly.",
                Width = 45,
                Height = 45,
                DangerLevel = 3.5f,
                EnemyCount = 12,
                HasMerchant = false,
                LootMultiplier = 2.8f,
                ControllingFaction = FactionType.VoidSpawn
            };
            theRift.AddExit(ZoneExitDirection.South, "the_wound", new Point(40, 1));
            AddZone(theRift);
            
            // ==========================================
            // THE WOUND - Near the Epicenter
            // Permanent tear where The Severance began
            // ==========================================
            var theWound = new ZoneData("the_wound", "The Wound", ZoneType.Epicenter)
            {
                Description = "The edge of Ground Zero. Reality is paper-thin here.",
                Width = 60,
                Height = 60,
                DangerLevel = 3.8f,
                EnemyCount = 12,
                HasMerchant = false,
                LootMultiplier = 3.5f,
                ControllingFaction = FactionType.VoidSpawn
            };
            theWound.AddExit(ZoneExitDirection.South, "deep_zone_south", new Point(25, 1));
            theWound.AddExit(ZoneExitDirection.North, "the_epicenter", new Point(30, 58));
            theWound.AddExit(ZoneExitDirection.East, "the_rift", new Point(22, 43));
            AddZone(theWound);
            
            // ==========================================
            // THE EPICENTER - Ground Zero
            // Where The Severance began, reality breaks
            // ENDGAME AREA
            // ==========================================
            var theEpicenter = new ZoneData("the_epicenter", "The Epicenter", ZoneType.Epicenter)
            {
                Description = "Ground Zero of The Severance. Reality itself is broken. The Void bleeds through.",
                Width = 70,
                Height = 70,
                DangerLevel = 5.0f,  // Maximum danger
                EnemyCount = 15,
                HasMerchant = false,
                LootMultiplier = 5.0f,  // Best loot in game
                ControllingFaction = FactionType.VoidSpawn
            };
            theEpicenter.AddExit(ZoneExitDirection.South, "the_wound", new Point(30, 1));
            AddZone(theEpicenter);
            
            // ==========================================
            // FREE ZONES - Player Exploration & Base Building
            // Random biomes for farming materials and building bases
            // ==========================================
            
            CreateFreeZones();
            
            // Set starting zone
            _currentZoneId = "rusthollow";
            
            System.Diagnostics.Debug.WriteLine($">>> ZoneManager: Initialized {_zones.Count} zones (World of Orodia) <<<");
        }
        
        private void AddZone(ZoneData zone)
        {
            _zones[zone.Id] = zone;
        }
        
        /// <summary>
        /// Create Free Zones - procedurally generated zones for farming and base building
        /// </summary>
        private void CreateFreeZones()
        {
            Random rand = new Random(42);  // Fixed seed for consistent world layout
            
            // Biome types for free zones
            ZoneType[] biomes = { ZoneType.Wasteland, ZoneType.Forest, ZoneType.Cave, ZoneType.Ruins };
            string[] biomeNames = { "Wasteland", "Forest", "Cavern", "Ruins" };
            string[] biomeAdjectives = { "Forgotten", "Hidden", "Remote", "Isolated", "Abandoned", "Untamed", "Wild", "Desolate" };
            
            // ==========================================
            // FREE ZONE 1 - South of Scavenger Plains (Easy - starter area)
            // ==========================================
            var freeZone1 = CreateFreeZone("free_zone_1", rand, biomes, biomeNames, biomeAdjectives);
            freeZone1.DangerLevel = 0.8f;
            freeZone1.Description = "A quiet area perfect for setting up camp. Few threats here.";
            freeZone1.AddExit(ZoneExitDirection.North, "scavenger_plains", new Point(27, 53));
            AddZone(freeZone1);
            _zones["scavenger_plains"].AddExit(ZoneExitDirection.South, "free_zone_1", new Point(27, 1));
            
            // ==========================================
            // FREE ZONE 2 - West of Twisted Woods (Forest biome)
            // ==========================================
            var freeZone2 = CreateFreeZone("free_zone_2", rand, biomes, biomeNames, biomeAdjectives);
            freeZone2.BiomeType = ZoneType.Forest;
            freeZone2.Type = ZoneType.Forest;
            freeZone2.DangerLevel = 1.0f;
            freeZone2.ResourceMultiplier = 1.3f;
            freeZone2.Description = "Dense forest, rich in natural resources. Good for gathering wood and herbs.";
            freeZone2.AddExit(ZoneExitDirection.East, "twisted_woods", new Point(1, 25));
            AddZone(freeZone2);
            _zones["twisted_woods"].AddExit(ZoneExitDirection.West, "free_zone_2", new Point(48, 25));
            
            // ==========================================
            // FREE ZONE 3 - East of Forward Base Purity (accessible from east)
            // ==========================================
            var freeZone3 = CreateFreeZone("free_zone_3", rand, biomes, biomeNames, biomeAdjectives);
            freeZone3.DangerLevel = 1.2f;
            freeZone3.ResourceMultiplier = 1.2f;
            freeZone3.Description = "An unexplored area at the Zone's edge. Resources are plentiful.";
            freeZone3.AddExit(ZoneExitDirection.West, "forward_base_purity", new Point(48, 25));
            AddZone(freeZone3);
            _zones["forward_base_purity"].AddExit(ZoneExitDirection.East, "free_zone_3", new Point(1, 25));
            
            // ==========================================
            // FREE ZONE 4 - West of Scavenger Plains (safe area for base building)
            // ==========================================
            var freeZone4 = CreateFreeZone("free_zone_4", rand, biomes, biomeNames, biomeAdjectives);
            freeZone4.DangerLevel = 0.7f;
            freeZone4.ResourceMultiplier = 1.0f;
            freeZone4.Description = "A safe clearing west of the plains. Ideal location for a permanent base.";
            freeZone4.AddExit(ZoneExitDirection.East, "scavenger_plains", new Point(1, 25));
            AddZone(freeZone4);
            _zones["scavenger_plains"].AddExit(ZoneExitDirection.West, "free_zone_4", new Point(48, 25));
            
            // ==========================================
            // FREE ZONE 5 - Cave system (Mining) - West of Dark Forest
            // ==========================================
            var freeZone5 = CreateFreeZone("free_zone_5", rand, biomes, biomeNames, biomeAdjectives);
            freeZone5.BiomeType = ZoneType.Cave;
            freeZone5.Type = ZoneType.Cave;
            freeZone5.Name = GenerateFreeZoneName(rand, "Cavern", biomeAdjectives);
            freeZone5.DangerLevel = 1.5f;
            freeZone5.ResourceMultiplier = 1.5f;
            freeZone5.Description = "A cave system rich in minerals. Watch for cave-dwellers.";
            freeZone5.AddExit(ZoneExitDirection.East, "dark_forest", new Point(1, 25));
            AddZone(freeZone5);
            _zones["dark_forest"].AddExit(ZoneExitDirection.West, "free_zone_5", new Point(48, 25));
            
            // ==========================================
            // FREE ZONE 6 - East of Syndicate Post (Trading route)
            // ==========================================
            var freeZone6 = CreateFreeZone("free_zone_6", rand, biomes, biomeNames, biomeAdjectives);
            freeZone6.DangerLevel = 1.1f;
            freeZone6.ResourceMultiplier = 1.1f;
            freeZone6.Description = "Along the trade routes. Sometimes merchants pass through.";
            freeZone6.AddExit(ZoneExitDirection.West, "syndicate_post", new Point(38, 20));
            AddZone(freeZone6);
            _zones["syndicate_post"].AddExit(ZoneExitDirection.East, "free_zone_6", new Point(1, 20));
            
            // ==========================================
            // FREE ZONE 7 - Far West wilderness (Medium danger)
            // ==========================================
            var freeZone7 = CreateFreeZone("free_zone_7", rand, biomes, biomeNames, biomeAdjectives);
            freeZone7.DangerLevel = 1.8f;
            freeZone7.ResourceMultiplier = 1.4f;
            freeZone7.LootMultiplier = 1.3f;
            freeZone7.Description = "Far from civilization. Dangerous, but rich in untapped resources.";
            freeZone7.AddExit(ZoneExitDirection.East, "void_temple", new Point(1, 22));
            AddZone(freeZone7);
            _zones["void_temple"].AddExit(ZoneExitDirection.West, "free_zone_7", new Point(43, 22));
            
            // ==========================================
            // FREE ZONE 8 - South of Outer Ruins East (Scavenging)
            // ==========================================
            var freeZone8 = CreateFreeZone("free_zone_8", rand, biomes, biomeNames, biomeAdjectives);
            freeZone8.BiomeType = ZoneType.Ruins;
            freeZone8.Type = ZoneType.Ruins;
            freeZone8.Name = GenerateFreeZoneName(rand, "Ruins", biomeAdjectives);
            freeZone8.DangerLevel = 1.4f;
            freeZone8.ResourceMultiplier = 1.2f;
            freeZone8.LootMultiplier = 1.4f;
            freeZone8.Description = "Collapsed structures with salvageable materials. Good scavenging.";
            freeZone8.AddExit(ZoneExitDirection.North, "outer_ruins_east", new Point(25, 48));
            AddZone(freeZone8);
            _zones["outer_ruins_east"].AddExit(ZoneExitDirection.South, "free_zone_8", new Point(25, 1));
            
            // ==========================================
            // FREE ZONE 9 - Deep east (High risk, high reward)
            // ==========================================
            var freeZone9 = CreateFreeZone("free_zone_9", rand, biomes, biomeNames, biomeAdjectives);
            freeZone9.BiomeType = ZoneType.Wasteland;
            freeZone9.Type = ZoneType.Wasteland;
            freeZone9.Name = GenerateFreeZoneName(rand, "Wasteland", biomeAdjectives);
            freeZone9.Width = 60;
            freeZone9.Height = 60;
            freeZone9.DangerLevel = 2.0f;
            freeZone9.ResourceMultiplier = 1.6f;
            freeZone9.LootMultiplier = 1.5f;
            freeZone9.Description = "A remote wasteland. Dangerous, but the resources are worth the risk.";
            freeZone9.AddExit(ZoneExitDirection.West, "vault_omega", new Point(58, 30));
            AddZone(freeZone9);
            _zones["vault_omega"].AddExit(ZoneExitDirection.East, "free_zone_9", new Point(1, 30));
            
            // ==========================================
            // FREE ZONE 10 - West of The Spire (Gene-Elder territory)
            // ==========================================
            var freeZone10 = CreateFreeZone("free_zone_10", rand, biomes, biomeNames, biomeAdjectives);
            freeZone10.DangerLevel = 1.6f;
            freeZone10.ResourceMultiplier = 1.3f;
            freeZone10.Description = "Under the shadow of The Spire. The Gene-Elders tolerate settlers here.";
            freeZone10.AddExit(ZoneExitDirection.East, "the_spire", new Point(1, 25));
            AddZone(freeZone10);
            _zones["the_spire"].AddExit(ZoneExitDirection.West, "free_zone_10", new Point(38, 25));
            
            System.Diagnostics.Debug.WriteLine($">>> Created 10 Free Zones for exploration and base building <<<");
        }
        
        /// <summary>
        /// Create a single free zone with random properties
        /// </summary>
        private ZoneData CreateFreeZone(string id, Random rand, ZoneType[] biomes, string[] biomeNames, string[] adjectives)
        {
            // Pick random biome
            int biomeIndex = rand.Next(biomes.Length);
            ZoneType biome = biomes[biomeIndex];
            string biomeName = biomeNames[biomeIndex];
            
            // Generate name
            string name = GenerateFreeZoneName(rand, biomeName, adjectives);
            
            // Random size (45-60)
            int size = rand.Next(45, 61);
            
            var zone = new ZoneData(id, name, biome)
            {
                Width = size,
                Height = size,
                DangerLevel = 1.0f + (float)(rand.NextDouble() * 0.5),  // 1.0 - 1.5
                EnemyCount = rand.Next(3, 7),
                HasMerchant = false,
                LootMultiplier = 1.0f,
                ControllingFaction = FactionType.Wildlife,  // Unclaimed
                
                // FREE ZONE SPECIFIC
                IsFreeZone = true,
                AllowBaseBuilding = true,
                ResourceMultiplier = 1.0f + (float)(rand.NextDouble() * 0.3),  // 1.0 - 1.3
                BiomeType = biome,
                Description = "An unexplored area. Good for gathering resources and setting up camp."
            };
            
            return zone;
        }
        
        /// <summary>
        /// Generate a name for a free zone
        /// </summary>
        private string GenerateFreeZoneName(Random rand, string biomeName, string[] adjectives)
        {
            string adj = adjectives[rand.Next(adjectives.Length)];
            return $"{adj} {biomeName}";
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
            
            // Clear to base terrain based on zone type
            TileType baseTerrain = zone.Type switch
            {
                ZoneType.Wasteland => TileType.Dirt,
                ZoneType.Settlement => TileType.Stone,
                ZoneType.TradingPost => TileType.Stone,
                ZoneType.OuterRuins => TileType.Stone,
                ZoneType.InnerRuins => TileType.Stone,
                ZoneType.DeepZone => TileType.Stone,
                ZoneType.Epicenter => TileType.Stone,
                ZoneType.Forest => TileType.Grass,
                ZoneType.DarkForest => TileType.Grass,
                ZoneType.Cave => TileType.Stone,
                ZoneType.Laboratory => TileType.Stone,
                ZoneType.VoidRift => TileType.Stone,
                ZoneType.AncientVault => TileType.Stone,
                ZoneType.GeneElderHold => TileType.Stone,
                ZoneType.SanctumOutpost => TileType.Stone,
                ZoneType.VerdantLab => TileType.Stone,
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
            
            // Add terrain variation based on zone type
            switch (zone.Type)
            {
                case ZoneType.Wasteland:
                    GenerateWasteland(world, rand);
                    break;
                case ZoneType.OuterRuins:
                case ZoneType.InnerRuins:
                case ZoneType.Ruins:
                    GenerateRuins(world, rand, zone.DangerLevel);
                    break;
                case ZoneType.DeepZone:
                case ZoneType.Epicenter:
                    GenerateDeepZone(world, rand, zone.DangerLevel);
                    break;
                case ZoneType.Forest:
                case ZoneType.DarkForest:
                    GenerateForest(world, rand, zone.Type == ZoneType.DarkForest);
                    break;
                case ZoneType.Cave:
                case ZoneType.VoidRift:
                    GenerateCave(world, rand);
                    break;
                case ZoneType.Settlement:
                case ZoneType.TradingPost:
                case ZoneType.GeneElderHold:
                    GenerateSettlement(world, rand);
                    break;
                case ZoneType.Laboratory:
                case ZoneType.AncientVault:
                case ZoneType.VerdantLab:
                    GenerateLaboratory(world, rand);
                    break;
                case ZoneType.SanctumOutpost:
                    GenerateMilitaryBase(world, rand);
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
            // Scatter rocks and debris
            for (int i = 0; i < world.Width * world.Height / 15; i++)
            {
                int x = rand.Next(world.Width);
                int y = rand.Next(world.Height);
                world.SetTile(x, y, TileType.Stone);
            }
            
            // Some sand patches
            for (int i = 0; i < 8; i++)
            {
                int cx = rand.Next(world.Width);
                int cy = rand.Next(world.Height);
                int radius = rand.Next(3, 7);
                
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (dx * dx + dy * dy <= radius * radius)
                        {
                            int x = cx + dx;
                            int y = cy + dy;
                            if (x >= 0 && x < world.Width && y >= 0 && y < world.Height)
                            {
                                world.SetTile(x, y, TileType.Sand);
                            }
                        }
                    }
                }
            }
        }
        
        private void GenerateRuins(WorldGrid world, Random rand, float dangerLevel)
        {
            // More walls in deeper ruins
            int wallDensity = (int)(dangerLevel * 15);
            
            // Ruined building outlines
            for (int i = 0; i < 6 + (int)(dangerLevel * 2); i++)
            {
                int bx = rand.Next(5, world.Width - 10);
                int by = rand.Next(5, world.Height - 10);
                int bw = rand.Next(4, 10);
                int bh = rand.Next(4, 10);
                
                // Draw walls with gaps (ruins)
                for (int x = bx; x < bx + bw; x++)
                {
                    if (rand.NextDouble() > 0.3)
                        world.SetTile(x, by, TileType.StoneWall);
                    if (rand.NextDouble() > 0.3)
                        world.SetTile(x, by + bh - 1, TileType.StoneWall);
                }
                for (int y = by; y < by + bh; y++)
                {
                    if (rand.NextDouble() > 0.3)
                        world.SetTile(bx, y, TileType.StoneWall);
                    if (rand.NextDouble() > 0.3)
                        world.SetTile(bx + bw - 1, y, TileType.StoneWall);
                }
            }
            
            // Scatter debris
            for (int i = 0; i < wallDensity; i++)
            {
                int x = rand.Next(world.Width);
                int y = rand.Next(world.Height);
                world.SetTile(x, y, TileType.StoneWall);
            }
        }
        
        private void GenerateDeepZone(WorldGrid world, Random rand, float dangerLevel)
        {
            // Base ruins generation
            GenerateRuins(world, rand, dangerLevel);
            
            // Add "corruption patches" - areas of strange terrain
            int patchCount = (int)(dangerLevel * 3);
            for (int i = 0; i < patchCount; i++)
            {
                int cx = rand.Next(world.Width);
                int cy = rand.Next(world.Height);
                int radius = rand.Next(4, 9);
                
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (dx * dx + dy * dy <= radius * radius)
                        {
                            int x = cx + dx;
                            int y = cy + dy;
                            if (x >= 0 && x < world.Width && y >= 0 && y < world.Height)
                            {
                                // Mix of water (void pools) and grass (strange growth)
                                if (rand.NextDouble() > 0.5)
                                    world.SetTile(x, y, TileType.Water);
                                else
                                    world.SetTile(x, y, TileType.Grass);
                            }
                        }
                    }
                }
            }
        }
        
        private void GenerateForest(WorldGrid world, Random rand, bool isDark)
        {
            // Dense tree coverage
            float treeDensity = isDark ? 0.35f : 0.25f;
            
            for (int x = 0; x < world.Width; x++)
            {
                for (int y = 0; y < world.Height; y++)
                {
                    if (rand.NextDouble() < treeDensity)
                    {
                        world.SetTile(x, y, TileType.StoneWall);  // Trees as walls
                    }
                }
            }
            
            // Clear some paths
            for (int i = 0; i < 5; i++)
            {
                int startX = rand.Next(world.Width);
                int startY = rand.Next(world.Height);
                int endX = rand.Next(world.Width);
                int endY = rand.Next(world.Height);
                
                // Simple line clearing
                int steps = Math.Max(Math.Abs(endX - startX), Math.Abs(endY - startY));
                for (int s = 0; s <= steps; s++)
                {
                    float t = (float)s / Math.Max(1, steps);
                    int x = (int)(startX + (endX - startX) * t);
                    int y = (int)(startY + (endY - startY) * t);
                    
                    if (x >= 0 && x < world.Width && y >= 0 && y < world.Height)
                    {
                        world.SetTile(x, y, TileType.Grass);
                        // Widen path
                        if (x > 0) world.SetTile(x - 1, y, TileType.Grass);
                        if (x < world.Width - 1) world.SetTile(x + 1, y, TileType.Grass);
                    }
                }
            }
            
            // Add water features in dark forest
            if (isDark)
            {
                for (int i = 0; i < 3; i++)
                {
                    int cx = rand.Next(world.Width);
                    int cy = rand.Next(world.Height);
                    int radius = rand.Next(2, 5);
                    
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            if (dx * dx + dy * dy <= radius * radius)
                            {
                                int x = cx + dx;
                                int y = cy + dy;
                                if (x >= 0 && x < world.Width && y >= 0 && y < world.Height)
                                {
                                    world.SetTile(x, y, TileType.Water);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        private void GenerateCave(WorldGrid world, Random rand)
        {
            // Start with all walls
            for (int x = 0; x < world.Width; x++)
            {
                for (int y = 0; y < world.Height; y++)
                {
                    world.SetTile(x, y, TileType.StoneWall);
                }
            }
            
            // Carve out cave using cellular automata-ish approach
            for (int i = 0; i < 5; i++)
            {
                int cx = rand.Next(10, world.Width - 10);
                int cy = rand.Next(10, world.Height - 10);
                
                // Carve irregular room
                int points = rand.Next(5, 10);
                for (int p = 0; p < points; p++)
                {
                    int px = cx + rand.Next(-8, 9);
                    int py = cy + rand.Next(-8, 9);
                    int radius = rand.Next(3, 7);
                    
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            if (dx * dx + dy * dy <= radius * radius)
                            {
                                int x = px + dx;
                                int y = py + dy;
                                if (x >= 1 && x < world.Width - 1 && y >= 1 && y < world.Height - 1)
                                {
                                    world.SetTile(x, y, TileType.Stone);
                                }
                            }
                        }
                    }
                }
            }
            
            // Connect caves with tunnels
            for (int i = 0; i < 3; i++)
            {
                int x = rand.Next(world.Width);
                int y = rand.Next(world.Height);
                int targetX = rand.Next(world.Width);
                int targetY = rand.Next(world.Height);
                
                while (x != targetX || y != targetY)
                {
                    if (x >= 1 && x < world.Width - 1 && y >= 1 && y < world.Height - 1)
                    {
                        world.SetTile(x, y, TileType.Stone);
                        world.SetTile(x + 1, y, TileType.Stone);
                        world.SetTile(x, y + 1, TileType.Stone);
                    }
                    
                    if (rand.NextDouble() > 0.5 && x != targetX)
                        x += (targetX > x) ? 1 : -1;
                    else if (y != targetY)
                        y += (targetY > y) ? 1 : -1;
                }
            }
        }
        
        private void GenerateSettlement(WorldGrid world, Random rand)
        {
            // Central plaza
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
            
            // Buildings around plaza
            for (int i = 0; i < 6; i++)
            {
                int bx = centerX + rand.Next(-15, 16);
                int by = centerY + rand.Next(-15, 16);
                int bw = rand.Next(4, 8);
                int bh = rand.Next(4, 8);
                
                // Draw complete building
                for (int x = bx; x < bx + bw; x++)
                {
                    if (x >= 0 && x < world.Width)
                    {
                        if (by >= 0 && by < world.Height)
                            world.SetTile(x, by, TileType.StoneWall);
                        if (by + bh - 1 >= 0 && by + bh - 1 < world.Height)
                            world.SetTile(x, by + bh - 1, TileType.StoneWall);
                    }
                }
                for (int y = by; y < by + bh; y++)
                {
                    if (y >= 0 && y < world.Height)
                    {
                        if (bx >= 0 && bx < world.Width)
                            world.SetTile(bx, y, TileType.StoneWall);
                        if (bx + bw - 1 >= 0 && bx + bw - 1 < world.Width)
                            world.SetTile(bx + bw - 1, y, TileType.StoneWall);
                    }
                }
                
                // Door
                int doorSide = rand.Next(4);
                int doorX = bx + bw / 2;
                int doorY = by + bh / 2;
                switch (doorSide)
                {
                    case 0: doorY = by; break;
                    case 1: doorY = by + bh - 1; break;
                    case 2: doorX = bx; break;
                    case 3: doorX = bx + bw - 1; break;
                }
                if (doorX >= 0 && doorX < world.Width && doorY >= 0 && doorY < world.Height)
                {
                    world.SetTile(doorX, doorY, TileType.Stone);
                }
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
            
            // Random openings in walls (damage/decay)
            for (int i = 0; i < 30; i++)
            {
                int x = rand.Next(world.Width);
                int y = rand.Next(world.Height);
                world.SetTile(x, y, TileType.Stone);
            }
        }
        
        private void GenerateMilitaryBase(WorldGrid world, Random rand)
        {
            // Perimeter wall
            for (int x = 3; x < world.Width - 3; x++)
            {
                world.SetTile(x, 3, TileType.StoneWall);
                world.SetTile(x, world.Height - 4, TileType.StoneWall);
            }
            for (int y = 3; y < world.Height - 3; y++)
            {
                world.SetTile(3, y, TileType.StoneWall);
                world.SetTile(world.Width - 4, y, TileType.StoneWall);
            }
            
            // Gates
            world.SetTile(world.Width / 2, 3, TileType.Stone);
            world.SetTile(world.Width / 2, world.Height - 4, TileType.Stone);
            
            // Internal buildings (barracks, armory, command)
            int[][] buildings = new int[][] {
                new int[] { 8, 8, 10, 8 },
                new int[] { 8, 20, 10, 8 },
                new int[] { 25, 10, 12, 12 }
            };
            
            foreach (var b in buildings)
            {
                int bx = b[0], by = b[1], bw = b[2], bh = b[3];
                
                if (bx + bw >= world.Width || by + bh >= world.Height) continue;
                
                for (int x = bx; x < bx + bw; x++)
                {
                    world.SetTile(x, by, TileType.StoneWall);
                    world.SetTile(x, by + bh - 1, TileType.StoneWall);
                }
                for (int y = by; y < by + bh; y++)
                {
                    world.SetTile(bx, y, TileType.StoneWall);
                    world.SetTile(bx + bw - 1, y, TileType.StoneWall);
                }
                
                // Door
                world.SetTile(bx + bw / 2, by + bh - 1, TileType.Stone);
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
            
            // First visit - generate new enemies based on zone type and faction
            var occupiedTiles = new HashSet<Point>();
            Random rand = new Random(zone.Seed + DateTime.Now.Millisecond);
            
            int hostileCount = zone.EnemyCount;
            int passiveCount = GetPassiveCount(zone.Type, rand);
            
            // Generate hostile enemies
            for (int i = 0; i < hostileCount; i++)
            {
                Point spawnTile = FindSpawnTile(zone, occupiedTiles, rand);
                occupiedTiles.Add(spawnTile);
                
                EnemyType enemyType = PickEnemyForZone(zone, rand);
                Vector2 position = new Vector2(spawnTile.X * tileSize, spawnTile.Y * tileSize);
                
                var enemy = EnemyEntity.Create(enemyType, position, enemies.Count + 1);
                enemies.Add(enemy);
            }
            
            // Generate passive creatures
            for (int i = 0; i < passiveCount; i++)
            {
                Point spawnTile = FindSpawnTile(zone, occupiedTiles, rand);
                occupiedTiles.Add(spawnTile);
                
                EnemyType enemyType = PickPassiveForZone(zone.Type, rand);
                Vector2 position = new Vector2(spawnTile.X * tileSize, spawnTile.Y * tileSize);
                
                var enemy = EnemyEntity.Create(enemyType, position, enemies.Count + 1);
                enemies.Add(enemy);
            }
            
            System.Diagnostics.Debug.WriteLine($">>> Generated {enemies.Count} enemies for {zone.Name} <<<");
            return enemies;
        }
        
        private EnemyType PickEnemyForZone(ZoneData zone, Random rand)
        {
            // Pick enemies based on controlling faction
            return zone.ControllingFaction switch
            {
                FactionType.UnitedSanctum => PickSanctumEnemy(rand),
                FactionType.IronSyndicate => PickSyndicateEnemy(rand),
                FactionType.VerdantOrder => PickVerdantEnemy(rand),
                FactionType.VoidCult => PickVoidCultEnemy(rand),
                FactionType.VoidSpawn => PickVoidSpawnEnemy(zone.DangerLevel, rand),
                FactionType.GeneElders => PickChangedEnemy(rand),
                FactionType.TheChanged => PickWastelandEnemy(zone.Type, zone.DangerLevel, rand),
                FactionType.Wildlife => PickWildlifeEnemy(zone.Type, rand),
                _ => PickWastelandEnemy(zone.Type, zone.DangerLevel, rand)
            };
        }
        
        private EnemyType PickSanctumEnemy(Random rand)
        {
            // Sanctum military - high-tech soldiers
            double roll = rand.NextDouble();
            if (roll < 0.5) return EnemyType.Raider;  // Placeholder - would be SanctumTrooper
            if (roll < 0.8) return EnemyType.Hunter;  // Placeholder - would be SanctumEnforcer
            return EnemyType.Abomination;  // Placeholder - would be PurgeDrone
        }
        
        private EnemyType PickSyndicateEnemy(Random rand)
        {
            // Iron Syndicate - mercenaries and slavers
            double roll = rand.NextDouble();
            if (roll < 0.6) return EnemyType.Raider;  // Mercenary
            if (roll < 0.9) return EnemyType.Hunter;  // Slave Driver
            return EnemyType.Abomination;  // Combat Automaton
        }
        
        private EnemyType PickVerdantEnemy(Random rand)
        {
            // Verdant Order - bio-engineers and collectors
            double roll = rand.NextDouble();
            if (roll < 0.4) return EnemyType.Hunter;  // Collector
            if (roll < 0.7) return EnemyType.Raider;  // Purifier
            return EnemyType.MutantBeast;  // Gene-Hound
        }
        
        private EnemyType PickVoidCultEnemy(Random rand)
        {
            // Void Cult - Dark Science practitioners
            double roll = rand.NextDouble();
            if (roll < 0.5) return EnemyType.Raider;  // Cultist
            if (roll < 0.8) return EnemyType.Abomination;  // Void-Touched
            return EnemyType.MutantBeast;  // Summoned creature
        }
        
        private EnemyType PickVoidSpawnEnemy(float dangerLevel, Random rand)
        {
            // Void Spawn - creatures from the Void itself
            double roll = rand.NextDouble();
            
            if (dangerLevel >= 4.0)
            {
                // Epicenter - worst of the worst
                if (roll < 0.3) return EnemyType.Abomination;  // Void Horror
                if (roll < 0.6) return EnemyType.MutantBeast;  // Void Crawler
                return EnemyType.Hunter;  // Void Wraith
            }
            else if (dangerLevel >= 2.5)
            {
                // Deep Zone
                if (roll < 0.4) return EnemyType.MutantBeast;
                if (roll < 0.8) return EnemyType.Abomination;
                return EnemyType.Hunter;
            }
            else
            {
                // Lighter void presence
                if (roll < 0.6) return EnemyType.MutantBeast;
                return EnemyType.Raider;
            }
        }
        
        private EnemyType PickChangedEnemy(Random rand)
        {
            // Gene-Elder territory - powerful mutants (usually not hostile)
            double roll = rand.NextDouble();
            if (roll < 0.7) return EnemyType.MutantBeast;
            return EnemyType.Abomination;
        }
        
        private EnemyType PickWastelandEnemy(ZoneType zoneType, float dangerLevel, Random rand)
        {
            // Generic wasteland enemies - raiders and mutant beasts
            double roll = rand.NextDouble();
            
            if (zoneType == ZoneType.OuterRuins || zoneType == ZoneType.Wasteland)
            {
                if (roll < 0.6) return EnemyType.Raider;
                return EnemyType.MutantBeast;
            }
            else if (zoneType == ZoneType.InnerRuins)
            {
                if (roll < 0.4) return EnemyType.Raider;
                if (roll < 0.7) return EnemyType.Hunter;
                return EnemyType.MutantBeast;
            }
            else
            {
                if (roll < 0.3) return EnemyType.Raider;
                if (roll < 0.6) return EnemyType.Hunter;
                return EnemyType.Abomination;
            }
        }
        
        private EnemyType PickWildlifeEnemy(ZoneType zoneType, Random rand)
        {
            // Wildlife - mutated animals
            return EnemyType.MutantBeast;
        }
        
        private EnemyType PickPassiveForZone(ZoneType zoneType, Random rand)
        {
            // Passive creatures based on zone type
            return EnemyType.MutantBeast;  // Most passive wildlife
        }
        
        private int GetPassiveCount(ZoneType zoneType, Random rand)
        {
            return zoneType switch
            {
                ZoneType.Wasteland => rand.Next(2, 5),
                ZoneType.OuterRuins => rand.Next(1, 3),
                ZoneType.InnerRuins => rand.Next(0, 2),
                ZoneType.DeepZone => rand.Next(0, 1),
                ZoneType.Epicenter => 0,
                ZoneType.Forest => rand.Next(4, 8),
                ZoneType.DarkForest => rand.Next(2, 5),
                ZoneType.Cave => rand.Next(2, 4),
                ZoneType.Settlement => rand.Next(0, 2),
                ZoneType.TradingPost => rand.Next(0, 1),
                ZoneType.Laboratory => rand.Next(1, 3),
                ZoneType.VoidRift => 0,
                _ => rand.Next(1, 4)
            };
        }
        
        private Point FindSpawnTile(ZoneData zone, HashSet<Point> occupied, Random rand)
        {
            int minX = 5;
            int maxX = zone.Width - 5;
            int minY = 5;
            int maxY = zone.Height - 5;
            
            // Try random positions
            for (int attempt = 0; attempt < 50; attempt++)
            {
                Point pos = new Point(rand.Next(minX, maxX), rand.Next(minY, maxY));
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
    }
}
