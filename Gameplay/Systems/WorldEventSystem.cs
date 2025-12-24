// Gameplay/Systems/WorldEventSystem.cs
// Rimworld-style random events for dynamic gameplay

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MyRPG.Data;
using MyRPG.Gameplay.Entities;
using MyRPG.Gameplay.World;

namespace MyRPG.Gameplay.Systems
{
    // ============================================
    // EVENT TYPE ENUM (internal to this system)
    // ============================================
    
    public enum WorldEventType
    {
        None,
        
        // Neutral Events
        VoidStorm,
        TraderCaravan,
        WanderingMutant,
        RelicSignal,
        
        // Hostile Events - Changed
        RaiderAttack,
        BeastMigration,
        HiveSwarming,
        
        // Hostile Events - Triad
        SanctumPurge,
        SyndicateRaid,
        VerdantCollection,
        
        // Hostile Events - Void
        VoidTide,
        RealityFracture,
        
        // Positive Events
        CacheDiscovered,
        FriendlyScavengers,
        AnomalyBloom,
        AncientBroadcast
    }
    
    // ============================================
    // EVENT DEFINITION
    // ============================================
    
    public class WorldEventDefinition
    {
        public WorldEventType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string NotificationText { get; set; }  // What shows on screen
        
        // Timing
        public float MinDaysBetween { get; set; } = 1.0f;     // Minimum days before this can trigger again
        public float BaseProbability { get; set; } = 0.1f;    // Base chance per hour (0-1)
        
        // Conditions
        public float MinDangerLevel { get; set; } = 0f;       // Zone must be at least this dangerous
        public float MaxDangerLevel { get; set; } = 10f;      // Zone must be at most this dangerous
        public bool RequiresFreeZone { get; set; } = false;   // Only in free zones
        public bool RequiresLoreZone { get; set; } = false;   // Only in lore zones
        public List<ZoneType> AllowedZoneTypes { get; set; } = new List<ZoneType>();  // Empty = all
        
        // Effects
        public int EnemySpawnCount { get; set; } = 0;
        public List<EnemyType> SpawnableEnemies { get; set; } = new List<EnemyType>();
        public int ItemSpawnCount { get; set; } = 0;
        public List<string> SpawnableItems { get; set; } = new List<string>();
        public bool SpawnsTrader { get; set; } = false;
        public float DurationHours { get; set; } = 2.0f;      // How long the event lasts
        
        // Category for UI
        public EventCategory Category { get; set; } = EventCategory.Neutral;
        
        public WorldEventDefinition(WorldEventType type, string name)
        {
            Type = type;
            Name = name;
            Description = "";
            NotificationText = name;
        }
    }
    
    public enum EventCategory
    {
        Neutral,    // Gray
        Positive,   // Green
        Warning,    // Yellow
        Hostile,    // Red
        Void        // Purple
    }
    
    // ============================================
    // ACTIVE EVENT INSTANCE
    // ============================================
    
    public class ActiveWorldEvent
    {
        public WorldEventType Type { get; set; }
        public WorldEventDefinition Definition { get; set; }
        public float StartTime { get; set; }      // Game time when started
        public float EndTime { get; set; }        // Game time when it ends
        public bool IsResolved { get; set; } = false;
        public string ZoneId { get; set; }        // Zone where event occurred
        
        // Spawned entities (for cleanup)
        public List<string> SpawnedEnemyIds { get; set; } = new List<string>();
        public List<string> SpawnedNPCIds { get; set; } = new List<string>();
        public List<Point> SpawnedItemPositions { get; set; } = new List<Point>();
        
        // Progress tracking
        public int EnemiesKilled { get; set; } = 0;
        public int EnemiesRemaining { get; set; } = 0;
        
        public float RemainingHours(float currentTime) => Math.Max(0, EndTime - currentTime);
        public bool IsExpired(float currentTime) => currentTime >= EndTime;
    }
    
    // ============================================
    // WORLD EVENT SYSTEM
    // ============================================
    
    public class WorldEventSystem
    {
        // Definitions
        private Dictionary<WorldEventType, WorldEventDefinition> _definitions;
        
        // Active events
        private List<ActiveWorldEvent> _activeEvents = new List<ActiveWorldEvent>();
        private Dictionary<WorldEventType, float> _lastEventTime = new Dictionary<WorldEventType, float>();
        
        // Configuration
        private float _eventCheckInterval = 1.0f;  // Hours between checks
        private float _lastCheckTime = 0f;
        private float _globalEventCooldown = 0.5f; // Hours between any events
        private float _lastAnyEventTime = 0f;
        
        // Random
        private Random _random = new Random();
        
        // Events
        public event Action<ActiveWorldEvent> OnEventStarted;
        public event Action<ActiveWorldEvent> OnEventEnded;
        public event Action<string> OnEventNotification;
        
        // Public access
        public List<ActiveWorldEvent> ActiveEvents => _activeEvents;
        public bool HasActiveEvent => _activeEvents.Count > 0;
        
        public WorldEventSystem()
        {
            InitializeDefinitions();
        }
        
        // ============================================
        // DEFINITIONS
        // ============================================
        
        private void InitializeDefinitions()
        {
            _definitions = new Dictionary<WorldEventType, WorldEventDefinition>();
            
            // ==================
            // NEUTRAL EVENTS
            // ==================
            
            _definitions[WorldEventType.VoidStorm] = new WorldEventDefinition(WorldEventType.VoidStorm, "Void Storm")
            {
                Description = "A purple storm sweeps across the area. Take cover or risk mutation!",
                NotificationText = "⚡ VOID STORM APPROACHING!",
                BaseProbability = 0.05f,
                MinDaysBetween = 2f,
                MinDangerLevel = 1.0f,
                DurationHours = 3.0f,
                Category = EventCategory.Void
            };
            
            _definitions[WorldEventType.TraderCaravan] = new WorldEventDefinition(WorldEventType.TraderCaravan, "Trader Caravan")
            {
                Description = "A group of Syndicate traders has arrived.",
                NotificationText = "💰 Trader caravan passing through!",
                BaseProbability = 0.08f,
                MinDaysBetween = 1f,
                SpawnsTrader = true,
                DurationHours = 6.0f,
                Category = EventCategory.Positive
            };
            
            _definitions[WorldEventType.WanderingMutant] = new WorldEventDefinition(WorldEventType.WanderingMutant, "Wandering Mutant")
            {
                Description = "A friendly Changed seeks shelter.",
                NotificationText = "👤 A wandering mutant approaches...",
                BaseProbability = 0.06f,
                MinDaysBetween = 1.5f,
                SpawnsTrader = true,  // Uses wanderer NPC type
                DurationHours = 4.0f,
                Category = EventCategory.Neutral
            };
            
            _definitions[WorldEventType.RelicSignal] = new WorldEventDefinition(WorldEventType.RelicSignal, "Relic Signal")
            {
                Description = "Ancient Aethelgard technology has activated nearby!",
                NotificationText = "📡 Strange signal detected nearby!",
                BaseProbability = 0.04f,
                MinDaysBetween = 3f,
                MinDangerLevel = 1.5f,
                ItemSpawnCount = 3,
                SpawnableItems = new List<string> { "relic_scrap", "tech_parts", "energy_cell", "ancient_datapad" },
                DurationHours = 2.0f,
                Category = EventCategory.Positive
            };
            
            // ==================
            // HOSTILE - CHANGED (Mutant Raiders)
            // ==================
            
            _definitions[WorldEventType.RaiderAttack] = new WorldEventDefinition(WorldEventType.RaiderAttack, "Raider Attack")
            {
                Description = "Hostile mutant raiders are attacking!",
                NotificationText = "⚔️ RAIDER ATTACK!",
                BaseProbability = 0.07f,
                MinDaysBetween = 1f,
                EnemySpawnCount = 4,
                SpawnableEnemies = new List<EnemyType> { EnemyType.Raider, EnemyType.Raider, EnemyType.Hunter },
                DurationHours = 0f,  // Ends when enemies killed
                Category = EventCategory.Hostile
            };
            
            _definitions[WorldEventType.BeastMigration] = new WorldEventDefinition(WorldEventType.BeastMigration, "Beast Migration")
            {
                Description = "A pack of mutant beasts is passing through!",
                NotificationText = "🐺 Beast pack approaching!",
                BaseProbability = 0.06f,
                MinDaysBetween = 1.5f,
                AllowedZoneTypes = new List<ZoneType> { ZoneType.Wasteland, ZoneType.Forest, ZoneType.DarkForest },
                EnemySpawnCount = 5,
                SpawnableEnemies = new List<EnemyType> { EnemyType.MutantBeast, EnemyType.MutantBeast, EnemyType.WildBoar },
                DurationHours = 0f,
                Category = EventCategory.Warning
            };
            
            _definitions[WorldEventType.HiveSwarming] = new WorldEventDefinition(WorldEventType.HiveSwarming, "Hive Swarming")
            {
                Description = "A HiveMother has begun spawning! Destroy it quickly!",
                NotificationText = "🕷️ HIVE SWARM DETECTED!",
                BaseProbability = 0.03f,
                MinDaysBetween = 3f,
                MinDangerLevel = 1.5f,
                EnemySpawnCount = 6,
                SpawnableEnemies = new List<EnemyType> { EnemyType.HiveMother, EnemyType.GiantInsect, EnemyType.GiantInsect, EnemyType.GiantInsect },
                DurationHours = 0f,
                Category = EventCategory.Hostile
            };
            
            // ==================
            // HOSTILE - TRIAD FACTIONS
            // ==================
            
            _definitions[WorldEventType.SanctumPurge] = new WorldEventDefinition(WorldEventType.SanctumPurge, "Sanctum Purge Squad")
            {
                Description = "United Sanctum Purge Squad detected! They're hunting mutants!",
                NotificationText = "🔵 SANCTUM PURGE SQUAD INCOMING!",
                BaseProbability = 0.04f,
                MinDaysBetween = 2f,
                MinDangerLevel = 1.0f,
                EnemySpawnCount = 4,
                SpawnableEnemies = new List<EnemyType> { EnemyType.Hunter, EnemyType.Hunter, EnemyType.Raider },
                DurationHours = 0f,
                Category = EventCategory.Hostile
            };
            
            _definitions[WorldEventType.SyndicateRaid] = new WorldEventDefinition(WorldEventType.SyndicateRaid, "Syndicate Slavers")
            {
                Description = "Iron Syndicate slavers are hunting for 'merchandise'!",
                NotificationText = "🟡 SYNDICATE SLAVERS APPROACHING!",
                BaseProbability = 0.04f,
                MinDaysBetween = 2f,
                EnemySpawnCount = 3,
                SpawnableEnemies = new List<EnemyType> { EnemyType.Hunter, EnemyType.Raider, EnemyType.Stalker },
                DurationHours = 0f,
                Category = EventCategory.Hostile
            };
            
            _definitions[WorldEventType.VerdantCollection] = new WorldEventDefinition(WorldEventType.VerdantCollection, "Verdant Collectors")
            {
                Description = "Verdant Order Collectors are hunting for 'specimens'!",
                NotificationText = "🟢 VERDANT COLLECTORS DETECTED!",
                BaseProbability = 0.03f,
                MinDaysBetween = 2.5f,
                MinDangerLevel = 1.5f,
                EnemySpawnCount = 3,
                SpawnableEnemies = new List<EnemyType> { EnemyType.Hunter, EnemyType.Psionic },
                DurationHours = 0f,
                Category = EventCategory.Hostile
            };
            
            // ==================
            // HOSTILE - VOID
            // ==================
            
            _definitions[WorldEventType.VoidTide] = new WorldEventDefinition(WorldEventType.VoidTide, "Void Tide")
            {
                Description = "Reality weakens... Void Spawn emerge from tears in space!",
                NotificationText = "🟣 VOID TIDE! Reality is tearing!",
                BaseProbability = 0.03f,
                MinDaysBetween = 3f,
                MinDangerLevel = 2.0f,
                EnemySpawnCount = 5,
                SpawnableEnemies = new List<EnemyType> { EnemyType.Abomination, EnemyType.MutantBeast, EnemyType.Stalker },
                DurationHours = 0f,
                Category = EventCategory.Void
            };
            
            _definitions[WorldEventType.RealityFracture] = new WorldEventDefinition(WorldEventType.RealityFracture, "Reality Fracture")
            {
                Description = "Space-time is unstable! Gravity and time behave erratically!",
                NotificationText = "💥 REALITY FRACTURE! Time is unstable!",
                BaseProbability = 0.02f,
                MinDaysBetween = 4f,
                MinDangerLevel = 2.5f,
                DurationHours = 1.0f,
                Category = EventCategory.Void
            };
            
            // ==================
            // POSITIVE EVENTS
            // ==================
            
            _definitions[WorldEventType.CacheDiscovered] = new WorldEventDefinition(WorldEventType.CacheDiscovered, "Supply Cache")
            {
                Description = "A hidden supply cache has been discovered nearby!",
                NotificationText = "📦 Supply cache discovered!",
                BaseProbability = 0.06f,
                MinDaysBetween = 1f,
                ItemSpawnCount = 5,
                SpawnableItems = new List<string> { "food_canned", "water_clean", "bandage", "ammo_9mm", "scrap_metal", "cloth" },
                DurationHours = 24.0f,  // Stays until collected
                Category = EventCategory.Positive
            };
            
            _definitions[WorldEventType.FriendlyScavengers] = new WorldEventDefinition(WorldEventType.FriendlyScavengers, "Friendly Scavengers")
            {
                Description = "A group of friendly Changed scavengers arrives, willing to trade.",
                NotificationText = "👥 Friendly scavengers arrived!",
                BaseProbability = 0.05f,
                MinDaysBetween = 1.5f,
                SpawnsTrader = true,
                DurationHours = 4.0f,
                Category = EventCategory.Positive
            };
            
            _definitions[WorldEventType.AnomalyBloom] = new WorldEventDefinition(WorldEventType.AnomalyBloom, "Anomaly Bloom")
            {
                Description = "Void Shards are crystallizing in the area!",
                NotificationText = "💎 Anomaly Bloom! Void Shards nearby!",
                BaseProbability = 0.04f,
                MinDaysBetween = 2f,
                MinDangerLevel = 1.5f,
                ItemSpawnCount = 4,
                SpawnableItems = new List<string> { "void_shard", "void_shard", "essence_fragment" },
                DurationHours = 12.0f,
                Category = EventCategory.Positive
            };
            
            _definitions[WorldEventType.AncientBroadcast] = new WorldEventDefinition(WorldEventType.AncientBroadcast, "Ancient Broadcast")
            {
                Description = "An ancient Aethelgard broadcast has been detected! Could lead to treasure...",
                NotificationText = "📻 Ancient broadcast detected!",
                BaseProbability = 0.03f,
                MinDaysBetween = 3f,
                MinDangerLevel = 1.0f,
                ItemSpawnCount = 2,
                SpawnableItems = new List<string> { "ancient_datapad", "relic_scrap" },
                DurationHours = 6.0f,
                Category = EventCategory.Positive
            };
            
            System.Diagnostics.Debug.WriteLine($">>> WorldEventSystem: Initialized {_definitions.Count} event definitions <<<");
        }
        
        public WorldEventDefinition GetDefinition(WorldEventType type)
        {
            return _definitions.GetValueOrDefault(type);
        }
        
        // ============================================
        // UPDATE LOOP
        // ============================================
        
        /// <summary>
        /// Call this each game update with current game time (in hours)
        /// </summary>
        public void Update(float gameTimeHours, ZoneData currentZone)
        {
            // Update active events
            UpdateActiveEvents(gameTimeHours);
            
            // Check for new events periodically
            if (gameTimeHours - _lastCheckTime >= _eventCheckInterval)
            {
                _lastCheckTime = gameTimeHours;
                TryTriggerEvent(gameTimeHours, currentZone);
            }
        }
        
        private void UpdateActiveEvents(float gameTimeHours)
        {
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                var evt = _activeEvents[i];
                
                // Check if event should end
                bool shouldEnd = false;
                
                // Time-based end
                if (evt.Definition.DurationHours > 0 && evt.IsExpired(gameTimeHours))
                {
                    shouldEnd = true;
                }
                
                // Combat-based end (all enemies killed)
                if (evt.Definition.EnemySpawnCount > 0 && evt.Definition.DurationHours == 0)
                {
                    if (evt.EnemiesRemaining <= 0)
                    {
                        shouldEnd = true;
                    }
                }
                
                if (shouldEnd || evt.IsResolved)
                {
                    EndEvent(evt);
                    _activeEvents.RemoveAt(i);
                }
            }
        }
        
        // ============================================
        // EVENT TRIGGERING
        // ============================================
        
        private void TryTriggerEvent(float gameTimeHours, ZoneData zone)
        {
            if (zone == null) return;
            
            // Global cooldown
            if (gameTimeHours - _lastAnyEventTime < _globalEventCooldown) return;
            
            // Don't trigger events in safe settlements
            if (zone.Type == ZoneType.Settlement && zone.DangerLevel < 1.0f) return;
            
            // Collect valid events
            List<WorldEventType> validEvents = new List<WorldEventType>();
            
            foreach (var kvp in _definitions)
            {
                var def = kvp.Value;
                
                // Check cooldown
                if (_lastEventTime.TryGetValue(kvp.Key, out float lastTime))
                {
                    float daysSince = (gameTimeHours - lastTime) / 24f;
                    if (daysSince < def.MinDaysBetween) continue;
                }
                
                // Check zone danger level
                if (zone.DangerLevel < def.MinDangerLevel) continue;
                if (zone.DangerLevel > def.MaxDangerLevel) continue;
                
                // Check zone type restrictions
                if (def.AllowedZoneTypes.Count > 0 && !def.AllowedZoneTypes.Contains(zone.Type)) continue;
                
                // Check free zone requirement
                if (def.RequiresFreeZone && !zone.IsFreeZone) continue;
                if (def.RequiresLoreZone && zone.IsFreeZone) continue;
                
                // Already have this event active?
                if (_activeEvents.Exists(e => e.Type == kvp.Key)) continue;
                
                validEvents.Add(kvp.Key);
            }
            
            if (validEvents.Count == 0) return;
            
            // Roll for each valid event
            foreach (var eventType in validEvents)
            {
                var def = _definitions[eventType];
                
                // Danger level increases hostile event chance
                float dangerMod = zone.DangerLevel >= 2.0f ? 1.5f : 1.0f;
                
                // Free zones have lower hostile event chance
                float zoneMod = zone.IsFreeZone ? 0.6f : 1.0f;
                
                float finalProb = def.BaseProbability * dangerMod * zoneMod;
                
                if (_random.NextDouble() < finalProb)
                {
                    TriggerEvent(eventType, gameTimeHours, zone);
                    return;  // Only one event at a time
                }
            }
        }
        
        /// <summary>
        /// Force trigger a specific event (for testing or quests)
        /// </summary>
        public ActiveWorldEvent TriggerEvent(WorldEventType type, float gameTimeHours, ZoneData zone)
        {
            var def = _definitions.GetValueOrDefault(type);
            if (def == null) return null;
            
            var evt = new ActiveWorldEvent
            {
                Type = type,
                Definition = def,
                StartTime = gameTimeHours,
                EndTime = def.DurationHours > 0 ? gameTimeHours + def.DurationHours : float.MaxValue,
                ZoneId = zone?.Id ?? "unknown",
                EnemiesRemaining = def.EnemySpawnCount
            };
            
            _activeEvents.Add(evt);
            _lastEventTime[type] = gameTimeHours;
            _lastAnyEventTime = gameTimeHours;
            
            // Notify
            OnEventStarted?.Invoke(evt);
            OnEventNotification?.Invoke(def.NotificationText);
            
            System.Diagnostics.Debug.WriteLine($">>> EVENT TRIGGERED: {def.Name} in {zone?.Name ?? "unknown"} <<<");
            
            return evt;
        }
        
        private void EndEvent(ActiveWorldEvent evt)
        {
            evt.IsResolved = true;
            OnEventEnded?.Invoke(evt);
            
            System.Diagnostics.Debug.WriteLine($">>> EVENT ENDED: {evt.Definition.Name} <<<");
        }
        
        // ============================================
        // SPAWNING HELPERS
        // ============================================
        
        /// <summary>
        /// Get enemies to spawn for an event
        /// </summary>
        public List<EnemyType> GetEnemiesToSpawn(ActiveWorldEvent evt)
        {
            var def = evt.Definition;
            if (def.SpawnableEnemies.Count == 0 || def.EnemySpawnCount == 0)
                return new List<EnemyType>();
            
            var result = new List<EnemyType>();
            for (int i = 0; i < def.EnemySpawnCount; i++)
            {
                var enemyType = def.SpawnableEnemies[_random.Next(def.SpawnableEnemies.Count)];
                result.Add(enemyType);
            }
            
            return result;
        }
        
        /// <summary>
        /// Get items to spawn for an event
        /// </summary>
        public List<string> GetItemsToSpawn(ActiveWorldEvent evt)
        {
            var def = evt.Definition;
            if (def.SpawnableItems.Count == 0 || def.ItemSpawnCount == 0)
                return new List<string>();
            
            var result = new List<string>();
            for (int i = 0; i < def.ItemSpawnCount; i++)
            {
                var itemId = def.SpawnableItems[_random.Next(def.SpawnableItems.Count)];
                result.Add(itemId);
            }
            
            return result;
        }
        
        /// <summary>
        /// Mark an enemy as killed for event tracking
        /// </summary>
        public void OnEnemyKilledInEvent(string enemyId)
        {
            foreach (var evt in _activeEvents)
            {
                if (evt.SpawnedEnemyIds.Contains(enemyId))
                {
                    evt.EnemiesKilled++;
                    evt.EnemiesRemaining--;
                    evt.SpawnedEnemyIds.Remove(enemyId);
                    
                    if (evt.EnemiesRemaining <= 0 && evt.Definition.EnemySpawnCount > 0)
                    {
                        // Event complete!
                        OnEventNotification?.Invoke($"✓ {evt.Definition.Name} - All enemies defeated!");
                    }
                    break;
                }
            }
        }
        
        /// <summary>
        /// Register spawned enemies with an event
        /// </summary>
        public void RegisterSpawnedEnemies(ActiveWorldEvent evt, List<string> enemyIds)
        {
            evt.SpawnedEnemyIds.AddRange(enemyIds);
            evt.EnemiesRemaining = enemyIds.Count;
        }
        
        // ============================================
        // QUERIES
        // ============================================
        
        public ActiveWorldEvent GetActiveEvent(WorldEventType type)
        {
            return _activeEvents.Find(e => e.Type == type);
        }
        
        public bool HasActiveHostileEvent()
        {
            return _activeEvents.Exists(e => 
                e.Definition.Category == EventCategory.Hostile || 
                e.Definition.Category == EventCategory.Void);
        }
        
        public List<ActiveWorldEvent> GetEventsInZone(string zoneId)
        {
            return _activeEvents.FindAll(e => e.ZoneId == zoneId);
        }
        
        /// <summary>
        /// Get all active events for UI display
        /// </summary>
        public List<(string Name, float RemainingHours, EventCategory Category, int EnemiesLeft)> GetActiveEventInfo(float currentTime)
        {
            var result = new List<(string, float, EventCategory, int)>();
            
            foreach (var evt in _activeEvents)
            {
                result.Add((
                    evt.Definition.Name,
                    evt.RemainingHours(currentTime),
                    evt.Definition.Category,
                    evt.EnemiesRemaining
                ));
            }
            
            return result;
        }
        
        // ============================================
        // VOID STORM EFFECTS
        // ============================================
        
        /// <summary>
        /// Check if player is exposed to void storm (not in building)
        /// Returns mutation chance modifier
        /// </summary>
        public float GetVoidStormExposure(bool playerInBuilding)
        {
            var stormEvent = GetActiveEvent(WorldEventType.VoidStorm);
            if (stormEvent == null) return 0f;
            
            // If in building, no exposure
            if (playerInBuilding) return 0f;
            
            // Exposure increases chance of random mutation
            return 0.1f;  // 10% chance per hour of exposure
        }
        
        // ============================================
        // REALITY FRACTURE EFFECTS
        // ============================================
        
        /// <summary>
        /// Get movement modifier during reality fracture
        /// </summary>
        public float GetRealityFractureMovementMod()
        {
            var fractureEvent = GetActiveEvent(WorldEventType.RealityFracture);
            if (fractureEvent == null) return 1.0f;
            
            // Random movement speed changes
            return 0.5f + (float)_random.NextDouble();  // 0.5x to 1.5x
        }
        
        /// <summary>
        /// Get time dilation during reality fracture
        /// </summary>
        public float GetRealityFractureTimeMod()
        {
            var fractureEvent = GetActiveEvent(WorldEventType.RealityFracture);
            if (fractureEvent == null) return 1.0f;
            
            // Time passes at different rates
            return 0.25f + (float)_random.NextDouble() * 1.5f;  // 0.25x to 1.75x
        }
    }
}
