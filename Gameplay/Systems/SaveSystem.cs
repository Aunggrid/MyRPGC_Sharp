// Gameplay/Systems/SaveSystem.cs
// Save/Load system with full game state persistence
// Version 2: Added Factions, FogOfWar, Research, NPCs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using MyRPG.Data;
using MyRPG.Gameplay.Character;
using MyRPG.Gameplay.Items;
using MyRPG.Gameplay.Entities;
using MyRPG.Gameplay.World;
using MyRPG.Gameplay.Building;

namespace MyRPG.Gameplay.Systems
{
    // ============================================
    // SAVE DATA STRUCTURES
    // ============================================
    
    public class GameSaveData
    {
        public int Version { get; set; } = 2;  // Updated version
        public DateTime SaveTime { get; set; }
        public string SlotName { get; set; }
        
        public PlayerSaveData Player { get; set; }
        public TimeSaveData Time { get; set; }
        public List<EnemySaveData> Enemies { get; set; }
        public List<GroundItemSaveData> GroundItems { get; set; }
        public QuestsSaveData Quests { get; set; }
        public WorldSaveData World { get; set; }
        
        // NEW in Version 2
        public FactionsSaveData Factions { get; set; }
        public ResearchSaveData Research { get; set; }
        public FogOfWarSaveData FogOfWar { get; set; }
        public List<NPCSaveData> NPCs { get; set; }
        
        public int PlayerLevel { get; set; }
        
        // NEW in Version 3: Tutorial progress
        public TutorialSaveData Tutorial { get; set; }
    }
    
    public class PlayerSaveData
    {
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float CurrentHealth { get; set; }
        public int Level { get; set; }
        public float CurrentXP { get; set; }
        public int Gold { get; set; }
        public int MutationPoints { get; set; }
        public int FreeMutationPicks { get; set; }
        public int PendingAttributePoints { get; set; }
        public int ReservedAP { get; set; }
        public int CurrentEsperPoints { get; set; }
        public SciencePath SciencePath { get; set; }
        
        public AttributesSaveData Attributes { get; set; }
        public List<BodyPartSaveData> BodyParts { get; set; }
        public List<MutationSaveData> Mutations { get; set; }
        public List<TraitType> Traits { get; set; }
        public InventorySaveData Inventory { get; set; }
        public SurvivalSaveData Survival { get; set; }
    }
    
    public class AttributesSaveData
    {
        public int STR { get; set; }
        public int AGI { get; set; }
        public int END { get; set; }
        public int INT { get; set; }
        public int PER { get; set; }
        public int WIL { get; set; }
    }
    
    public class BodyPartSaveData
    {
        public string Id { get; set; }
        public BodyPartType Type { get; set; }
        public string Name { get; set; }
        public float CurrentHealth { get; set; }
        public float MaxHealth { get; set; }
        public bool IsMutationPart { get; set; }
        public string ParentId { get; set; }
        public string EquippedItemId { get; set; }
        public string TwoHandedPairId { get; set; }
        public List<InjurySaveData> Injuries { get; set; }
        public List<BodyPartAilment> Ailments { get; set; }
    }
    
    public class InjurySaveData
    {
        public InjuryType Type { get; set; }
        public float Severity { get; set; }
        public float BleedRate { get; set; }
        public float HealProgress { get; set; }
    }
    
    public class MutationSaveData
    {
        public MutationType Type { get; set; }
        public int Level { get; set; }
    }
    
    public class InventorySaveData
    {
        public List<ItemSaveData> Items { get; set; }
    }
    
    public class ItemSaveData
    {
        public string ItemDefId { get; set; }
        public int StackCount { get; set; }
        public float Durability { get; set; }
        public ItemQuality Quality { get; set; }
    }
    
    public class SurvivalSaveData
    {
        public float Hunger { get; set; }
        public float Thirst { get; set; }
        public float Rest { get; set; }
        public float Temperature { get; set; }
    }
    
    public class TimeSaveData
    {
        public float TimeOfDay { get; set; }
        public int DayNumber { get; set; }
        public Season CurrentSeason { get; set; }
    }

    public class EnemySaveData
    {
        public string Id { get; set; }
        public EnemyType Type { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float CurrentHealth { get; set; }
        public float MaxHealth { get; set; }
        public bool IsProvoked { get; set; }
    }
    
    public class GroundItemSaveData
    {
        public string ItemDefId { get; set; }
        public int StackCount { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
    }
    
    public class QuestsSaveData
    {
        public List<QuestProgressSaveData> ActiveQuests { get; set; }
        public List<string> CompletedQuestIds { get; set; }
    }
    
    public class QuestProgressSaveData
    {
        public string QuestId { get; set; }
        public Dictionary<string, int> ObjectiveProgress { get; set; }
    }
    
    public class WorldSaveData
    {
        public string CurrentZoneId { get; set; }
        public List<StructureSaveData> Structures { get; set; }
    }
    
    public class StructureSaveData
    {
        public string Type { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public string State { get; set; }
        public float Health { get; set; }
    }
    
    // ============================================
    // NEW SAVE DATA STRUCTURES (Version 2)
    // ============================================
    
    /// <summary>
    /// Faction reputation data
    /// </summary>
    public class FactionsSaveData
    {
        public Dictionary<string, int> Reputations { get; set; } = new Dictionary<string, int>();
    }
    
    /// <summary>
    /// Research progress data
    /// </summary>
    public class ResearchSaveData
    {
        public List<string> CompletedResearchIds { get; set; } = new List<string>();
        public string CurrentResearchId { get; set; }
        public float CurrentResearchProgress { get; set; }
    }
    
    /// <summary>
    /// Fog of War exploration data
    /// </summary>
    /// <summary>
    /// Tutorial progress data
    /// </summary>
    public class TutorialSaveData
    {
        public List<string> ShownHintIds { get; set; } = new List<string>();
        public bool TutorialsEnabled { get; set; } = true;
    }

    public class FogOfWarSaveData
    {
        // Store explored tiles as list of "x,y" strings per zone
        // More efficient than storing entire bool[,] array
        public Dictionary<string, List<string>> ExploredTilesPerZone { get; set; } = new Dictionary<string, List<string>>();
    }
    
    /// <summary>
    /// NPC state data
    /// </summary>
    public class NPCSaveData
    {
        public string Id { get; set; }
        public string NPCType { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public int Gold { get; set; }
        public List<MerchantStockSaveData> Stock { get; set; }
        public string ZoneId { get; set; }
    }
    
    public class MerchantStockSaveData
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
    }
    
    // ============================================
    // SAVE SLOT INFO
    // ============================================
    
    public class SaveSlotInfo
    {
        public int Slot { get; set; }
        public bool Exists { get; set; }
        public bool IsCorrupted { get; set; }
        public DateTime SaveTime { get; set; }
        public int PlayerLevel { get; set; }
        
        // NEW in Version 3: Tutorial progress
        public TutorialSaveData Tutorial { get; set; }
        public string SlotName { get; set; }
        public string ZoneName { get; set; }
        
        public string GetDisplayText()
        {
            if (!Exists) return "[ EMPTY ]";
            if (IsCorrupted) return "[ CORRUPTED ]";
            string zoneInfo = !string.IsNullOrEmpty(ZoneName) ? $" - {ZoneName}" : "";
            return $"Level {PlayerLevel}{zoneInfo} - {SaveTime:MMM dd, HH:mm}";
        }
    }
    
    // ============================================
    // SAVE SYSTEM (Static Methods)
    // ============================================
    
    public static class SaveSystem
    {
        private const string SAVE_FOLDER = "Saves";
        private const string QUICKSAVE_FILE = "quicksave.json";
        public const int MAX_SLOTS = 5;
        
        private static string SavePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SAVE_FOLDER);
        private static string QuickSavePath => Path.Combine(SavePath, QUICKSAVE_FILE);
        
        private static JsonSerializerOptions JsonOptions => new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        private static void EnsureSaveDirectory()
        {
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
            }
        }
        
        private static string GetSlotFilePath(int slot)
        {
            return Path.Combine(SavePath, $"save_slot_{slot}.json");
        }
        
        // ============================================
        // QUICK SAVE/LOAD
        // ============================================
        
        public static bool QuickSaveExists()
        {
            return File.Exists(QuickSavePath);
        }
        
        public static bool QuickSave(
            PlayerEntity player,
            List<EnemyEntity> enemies,
            List<WorldItem> groundItems,
            WorldGrid world,
            BuildingSystem building,
            SurvivalSystem survivalSystem,
            List<NPCEntity> npcs = null,
            string currentZoneId = "rusthollow")
        {
            try
            {
                EnsureSaveDirectory();
                
                var saveData = CreateFullSaveData(
                    player, enemies, groundItems, world, building, 
                    survivalSystem, npcs, currentZoneId, "Quick Save"
                );
                
                string json = JsonSerializer.Serialize(saveData, JsonOptions);
                File.WriteAllText(QuickSavePath, json);
                
                System.Diagnostics.Debug.WriteLine($">>> Quick saved to {QuickSavePath} <<<");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> QUICK SAVE ERROR: {ex.Message} <<<");
                return false;
            }
        }
        
        public static GameSaveData QuickLoad()
        {
            try
            {
                if (!File.Exists(QuickSavePath))
                {
                    return null;
                }
                
                string json = File.ReadAllText(QuickSavePath);
                return JsonSerializer.Deserialize<GameSaveData>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> QUICK LOAD ERROR: {ex.Message} <<<");
                return null;
            }
        }
        
        // ============================================
        // SLOT SAVE/LOAD
        // ============================================
        
        public static bool SlotExists(int slot)
        {
            if (slot < 0 || slot >= MAX_SLOTS) return false;
            return File.Exists(GetSlotFilePath(slot));
        }
        
        public static bool SaveToSlot(
            int slot,
            PlayerEntity player,
            List<EnemyEntity> enemies,
            List<WorldItem> groundItems,
            WorldGrid world,
            BuildingSystem building,
            SurvivalSystem survivalSystem,
            List<NPCEntity> npcs = null,
            string currentZoneId = "rusthollow",
            string slotName = null)
        {
            if (slot < 0 || slot >= MAX_SLOTS) return false;
            
            try
            {
                EnsureSaveDirectory();
                
                string name = string.IsNullOrEmpty(slotName) ? $"Save Slot {slot + 1}" : slotName;
                var saveData = CreateFullSaveData(
                    player, enemies, groundItems, world, building,
                    survivalSystem, npcs, currentZoneId, name
                );
                
                string json = JsonSerializer.Serialize(saveData, JsonOptions);
                File.WriteAllText(GetSlotFilePath(slot), json);
                
                System.Diagnostics.Debug.WriteLine($">>> Saved to slot {slot}, Zone: {currentZoneId} <<<");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> SLOT SAVE ERROR: {ex.Message} <<<");
                return false;
            }
        }
        
        public static GameSaveData LoadFromSlot(int slot)
        {
            if (slot < 0 || slot >= MAX_SLOTS) return null;
            
            try
            {
                string path = GetSlotFilePath(slot);
                if (!File.Exists(path))
                {
                    return null;
                }
                
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<GameSaveData>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> SLOT LOAD ERROR: {ex.Message} <<<");
                return null;
            }
        }
        
        public static bool DeleteSlot(int slot)
        {
            if (slot < 0 || slot >= MAX_SLOTS) return false;
            
            try
            {
                string path = GetSlotFilePath(slot);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        public static SaveSlotInfo GetSlotInfo(int slot)
        {
            if (slot < 0 || slot >= MAX_SLOTS) return null;
            
            string path = GetSlotFilePath(slot);
            var info = new SaveSlotInfo
            {
                Slot = slot,
                Exists = File.Exists(path)
            };
            
            if (info.Exists)
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<GameSaveData>(json);
                    info.SaveTime = data.SaveTime;
                    info.PlayerLevel = data.PlayerLevel;
                    info.SlotName = data.SlotName;
                    info.ZoneName = data.World?.CurrentZoneId;
                }
                catch
                {
                    info.IsCorrupted = true;
                }
            }
            
            return info;
        }
        
        public static List<SaveSlotInfo> GetAllSlotInfo()
        {
            var slots = new List<SaveSlotInfo>();
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                slots.Add(GetSlotInfo(i));
            }
            return slots;
        }
        
        // ============================================
        // CREATE SAVE DATA
        // ============================================
        
        private static GameSaveData CreateFullSaveData(
            PlayerEntity player,
            List<EnemyEntity> enemies,
            List<WorldItem> groundItems,
            WorldGrid world,
            BuildingSystem building,
            SurvivalSystem survivalSystem,
            List<NPCEntity> npcs,
            string currentZoneId,
            string slotName)
        {
            var saveData = new GameSaveData
            {
                Version = 2,
                SaveTime = DateTime.Now,
                SlotName = slotName,
                PlayerLevel = player.Stats.Level
            };
            
            // Save player
            saveData.Player = CreatePlayerSaveData(player);
            
            // Save time
            saveData.Time = CreateTimeSaveData(survivalSystem);
            
            // Save enemies
            saveData.Enemies = CreateEnemiesSaveData(enemies);
            
            // Save ground items
            saveData.GroundItems = CreateGroundItemsSaveData(groundItems);
            
            // Save world/structures
            saveData.World = CreateWorldSaveData(currentZoneId, building);
            
            // Save quests
            saveData.Quests = CreateQuestsSaveData();
            
            // NEW: Save factions
            saveData.Factions = CreateFactionsSaveData();
            
            // NEW: Save research
            saveData.Research = CreateResearchSaveData(player.Stats.SciencePath);
            
            // NEW: Save fog of war
            saveData.FogOfWar = CreateFogOfWarSaveData(currentZoneId);
            
            // NEW: Save NPCs
            saveData.NPCs = CreateNPCsSaveData(npcs, currentZoneId);
            
            // NEW: Save tutorial progress
            saveData.Tutorial = CreateTutorialSaveData();
            
            return saveData;
        }
        
        private static PlayerSaveData CreatePlayerSaveData(PlayerEntity player)
        {
            var stats = player.Stats;
            
            var data = new PlayerSaveData
            {
                PositionX = player.Position.X,
                PositionY = player.Position.Y,
                CurrentHealth = stats.CurrentHealth,
                Level = stats.Level,
                CurrentXP = stats.CurrentXP,
                Gold = stats.Gold,
                MutationPoints = stats.MutationPoints,
                FreeMutationPicks = stats.FreeMutationPicks,
                PendingAttributePoints = stats.PendingAttributePoints,
                ReservedAP = stats.ReservedAP,
                CurrentEsperPoints = stats.CurrentEsperPoints,
                SciencePath = stats.SciencePath
            };
            
            // Attributes
            data.Attributes = new AttributesSaveData
            {
                STR = stats.Attributes.STR,
                AGI = stats.Attributes.AGI,
                END = stats.Attributes.END,
                INT = stats.Attributes.INT,
                PER = stats.Attributes.PER,
                WIL = stats.Attributes.WIL
            };
            
            // Body parts
            data.BodyParts = new List<BodyPartSaveData>();
            foreach (var part in stats.Body.Parts.Values)
            {
                var partData = new BodyPartSaveData
                {
                    Id = part.Id,
                    Type = part.Type,
                    Name = part.Name,
                    CurrentHealth = part.CurrentHealth,
                    MaxHealth = part.MaxHealth,
                    IsMutationPart = part.IsMutationPart,
                    ParentId = part.ParentId,
                    EquippedItemId = part.EquippedItem?.ItemDefId,
                    TwoHandedPairId = part.TwoHandedPairId,
                    Ailments = part.Ailments.ToList(),
                    Injuries = new List<InjurySaveData>()
                };
                
                foreach (var injury in part.Injuries)
                {
                    partData.Injuries.Add(new InjurySaveData
                    {
                        Type = injury.Type,
                        Severity = injury.Severity,
                        BleedRate = injury.BleedRate,
                        HealProgress = injury.HealProgress
                    });
                }
                
                data.BodyParts.Add(partData);
            }
            
            // Mutations
            data.Mutations = stats.Mutations.Select(m => new MutationSaveData
            {
                Type = m.Type,
                Level = m.Level
            }).ToList();
            
            // Traits
            data.Traits = stats.Traits.ToList();
            
            // Inventory
            data.Inventory = new InventorySaveData
            {
                Items = new List<ItemSaveData>()
            };
            
            foreach (var item in stats.Inventory.GetAllItems())
            {
                data.Inventory.Items.Add(new ItemSaveData
                {
                    ItemDefId = item.ItemDefId,
                    StackCount = item.StackCount,
                    Durability = item.Durability,
                    Quality = item.Quality
                });
            }
            
            // Survival
            data.Survival = new SurvivalSaveData
            {
                Hunger = stats.Survival.Hunger,
                Thirst = stats.Survival.Thirst,
                Rest = stats.Survival.Rest,
                Temperature = stats.Survival.Temperature
            };
            
            return data;
        }
        
        private static TimeSaveData CreateTimeSaveData(SurvivalSystem survivalSystem)
        {
            var data = new TimeSaveData
            {
                TimeOfDay = 8f,
                DayNumber = 1,
                CurrentSeason = Season.Spring
            };
            
            try
            {
                if (survivalSystem != null)
                {
                    data.TimeOfDay = survivalSystem.GameHour;
                    data.DayNumber = (int)survivalSystem.GameDay;
                    data.CurrentSeason = survivalSystem.CurrentSeason;
                }
            }
            catch { }
            
            return data;
        }
        
        private static List<EnemySaveData> CreateEnemiesSaveData(List<EnemyEntity> enemies)
        {
            var data = new List<EnemySaveData>();
            
            if (enemies != null)
            {
                foreach (var enemy in enemies.Where(e => e.IsAlive))
                {
                    data.Add(new EnemySaveData
                    {
                        Id = enemy.Id,
                        Type = enemy.Type,
                        PositionX = enemy.Position.X,
                        PositionY = enemy.Position.Y,
                        CurrentHealth = enemy.CurrentHealth,
                        MaxHealth = enemy.MaxHealth,
                        IsProvoked = enemy.IsProvoked
                    });
                }
            }
            
            return data;
        }
        
        private static List<GroundItemSaveData> CreateGroundItemsSaveData(List<WorldItem> groundItems)
        {
            var data = new List<GroundItemSaveData>();
            
            if (groundItems != null)
            {
                foreach (var wi in groundItems)
                {
                    data.Add(new GroundItemSaveData
                    {
                        ItemDefId = wi.Item.ItemDefId,
                        StackCount = wi.Item.StackCount,
                        PositionX = wi.Position.X,
                        PositionY = wi.Position.Y
                    });
                }
            }
            
            return data;
        }
        
        private static WorldSaveData CreateWorldSaveData(string currentZoneId, BuildingSystem building)
        {
            var data = new WorldSaveData
            {
                CurrentZoneId = currentZoneId,
                Structures = new List<StructureSaveData>()
            };
            
            // Save structures
            if (building != null)
            {
                try
                {
                    foreach (var structure in building.GetAllStructures())
                    {
                        data.Structures.Add(new StructureSaveData
                        {
                            Type = structure.Type.ToString(),
                            TileX = structure.Position.X,
                            TileY = structure.Position.Y,
                            State = structure.State.ToString(),
                            Health = structure.CurrentHealth
                        });
                    }
                }
                catch { }
            }
            
            return data;
        }
        
        private static QuestsSaveData CreateQuestsSaveData()
        {
            var data = new QuestsSaveData
            {
                ActiveQuests = new List<QuestProgressSaveData>(),
                CompletedQuestIds = new List<string>()
            };
            
            try
            {
                if (GameServices.IsInitialized && GameServices.Quests != null)
                {
                    // Get active quests
                    foreach (var quest in GameServices.Quests.GetActiveQuests())
                    {
                        var questProgress = new QuestProgressSaveData
                        {
                            QuestId = quest.QuestId,
                            ObjectiveProgress = new Dictionary<string, int>()
                        };
                        
                        foreach (var obj in quest.Objectives)
                        {
                            questProgress.ObjectiveProgress[obj.Id] = obj.CurrentCount;
                        }
                        
                        data.ActiveQuests.Add(questProgress);
                    }
                    
                    // Get completed quests
                    data.CompletedQuestIds = GameServices.Quests.GetCompletedQuestIds().ToList();
                }
            }
            catch { }
            
            return data;
        }
        
        // ============================================
        // NEW: FACTION SAVE DATA
        // ============================================
        
        // ============================================
        // NEW: TUTORIAL SAVE DATA
        // ============================================
        
        private static TutorialSaveData CreateTutorialSaveData()
        {
            var data = new TutorialSaveData();
            
            try
            {
                if (GameServices.IsInitialized && GameServices.Tutorial != null)
                {
                    data.ShownHintIds = GameServices.Tutorial.GetShownHintIds();
                    data.TutorialsEnabled = GameServices.Tutorial.TutorialsEnabled;
                }
            }
            catch { }
            
            return data;
        }
        
        // ============================================
        private static FactionsSaveData CreateFactionsSaveData()
        {
            var data = new FactionsSaveData();
            
            try
            {
                if (GameServices.IsInitialized && GameServices.Factions != null)
                {
                    var snapshot = GameServices.Factions.GetReputationSnapshot();
                    foreach (var kvp in snapshot)
                    {
                        data.Reputations[kvp.Key.ToString()] = kvp.Value;
                    }
                }
            }
            catch { }
            
            return data;
        }
        
        // ============================================
        // NEW: RESEARCH SAVE DATA
        // ============================================
        
        private static ResearchSaveData CreateResearchSaveData(SciencePath playerPath)
        {
            var data = new ResearchSaveData();
            
            try
            {
                if (GameServices.IsInitialized && GameServices.Research != null)
                {
                    data.CompletedResearchIds = GameServices.Research.GetCompletedResearchIds();
                    
                    var currentProgress = GameServices.Research.GetCurrentResearchProgress();
                    if (currentProgress.HasValue)
                    {
                        data.CurrentResearchId = currentProgress.Value.nodeId;
                        data.CurrentResearchProgress = currentProgress.Value.progress;
                    }
                }
            }
            catch { }
            
            return data;
        }
        
        // ============================================
        // NEW: FOG OF WAR SAVE DATA
        // ============================================
        
        private static FogOfWarSaveData CreateFogOfWarSaveData(string currentZoneId)
        {
            var data = new FogOfWarSaveData();
            
            try
            {
                if (GameServices.IsInitialized && GameServices.FogOfWar != null)
                {
                    var exploredGrid = GameServices.FogOfWar.GetExplorationData();
                    if (exploredGrid != null)
                    {
                        var exploredTiles = new List<string>();
                        int width = exploredGrid.GetLength(0);
                        int height = exploredGrid.GetLength(1);
                        
                        for (int x = 0; x < width; x++)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                if (exploredGrid[x, y])
                                {
                                    exploredTiles.Add($"{x},{y}");
                                }
                            }
                        }
                        
                        data.ExploredTilesPerZone[currentZoneId] = exploredTiles;
                    }
                }
            }
            catch { }
            
            return data;
        }
        
        // ============================================
        // NEW: NPC SAVE DATA
        // ============================================
        
        private static List<NPCSaveData> CreateNPCsSaveData(List<NPCEntity> npcs, string currentZoneId)
        {
            var data = new List<NPCSaveData>();
            
            if (npcs == null) return data;
            
            try
            {
                foreach (var npc in npcs)
                {
                    var npcData = new NPCSaveData
                    {
                        Id = npc.Id,
                        NPCType = npc.Type.ToString(),
                        PositionX = npc.Position.X,
                        PositionY = npc.Position.Y,
                        Gold = npc.Gold,
                        ZoneId = currentZoneId,
                        Stock = new List<MerchantStockSaveData>()
                    };
                    
                    foreach (var stock in npc.Stock)
                    {
                        npcData.Stock.Add(new MerchantStockSaveData
                        {
                            ItemId = stock.ItemId,
                            Quantity = stock.Quantity
                        });
                    }
                    
                    data.Add(npcData);
                }
            }
            catch { }
            
            return data;
        }
        
        // ============================================
        // RESTORE METHODS
        // ============================================
        
        public static void RestorePlayer(PlayerEntity player, PlayerSaveData data, MutationSystem mutationSystem)
        {
            if (data == null || player == null) return;
            
            var stats = player.Stats;
            
            // Position
            player.Position = new Vector2(data.PositionX, data.PositionY);
            
            // Basic stats
            stats.CurrentHealth = data.CurrentHealth;
            stats.SetLevel(data.Level);
            stats.SetXP(data.CurrentXP);
            stats.Gold = data.Gold;
            stats.SetMutationPoints(data.MutationPoints);
            stats.SetFreeMutationPicks(data.FreeMutationPicks);
            stats.SetPendingAttributePoints(data.PendingAttributePoints);
            stats.ReservedAP = data.ReservedAP;
            stats.CurrentEsperPoints = data.CurrentEsperPoints;
            stats.SciencePath = data.SciencePath;
            
            // Attributes
            if (data.Attributes != null)
            {
                stats.Attributes.SetAll(
                    data.Attributes.STR,
                    data.Attributes.AGI,
                    data.Attributes.END,
                    data.Attributes.INT,
                    data.Attributes.PER,
                    data.Attributes.WIL
                );
            }
            
            // Body parts - restore health and injuries
            if (data.BodyParts != null)
            {
                foreach (var partData in data.BodyParts)
                {
                    if (stats.Body.Parts.TryGetValue(partData.Id, out var part))
                    {
                        part.CurrentHealth = partData.CurrentHealth;
                        part.TwoHandedPairId = partData.TwoHandedPairId;
                        
                        // Restore injuries
                        part.Injuries.Clear();
                        if (partData.Injuries != null)
                        {
                            foreach (var injuryData in partData.Injuries)
                            {
                                var injury = new Injury(injuryData.Type, injuryData.Severity);
                                injury.BleedRate = injuryData.BleedRate;
                                injury.HealProgress = injuryData.HealProgress;
                                part.Injuries.Add(injury);
                            }
                        }
                        
                        // Restore ailments
                        part.Ailments.Clear();
                        if (partData.Ailments != null)
                        {
                            foreach (var ailment in partData.Ailments)
                            {
                                part.Ailments.Add(ailment);
                            }
                        }
                        
                        // Restore equipped item
                        if (!string.IsNullOrEmpty(partData.EquippedItemId))
                        {
                            try
                            {
                                var item = new Item(partData.EquippedItemId);
                                part.EquippedItem = item;
                            }
                            catch { }
                        }
                    }
                }
            }
            
            // Clear and restore mutations
            stats.ClearMutations();
            if (data.Mutations != null)
            {
                foreach (var mutData in data.Mutations)
                {
                    stats.AddMutation(mutData.Type, mutData.Level);
                }
            }
            
            // Traits
            stats.ClearTraits();
            if (data.Traits != null)
            {
                foreach (var trait in data.Traits)
                {
                    stats.AddTrait(trait);
                }
            }
            
            // Inventory
            stats.Inventory.Clear();
            if (data.Inventory?.Items != null)
            {
                foreach (var itemData in data.Inventory.Items)
                {
                    try
                    {
                        var item = new Item(itemData.ItemDefId, itemData.Quality, itemData.StackCount);
                        item.Durability = itemData.Durability;
                        stats.Inventory.TryAddItem(item);
                    }
                    catch { }
                }
            }
            
            stats.SyncHPWithBody();
        }
        
        public static void RestoreTime(SurvivalSystem survivalSystem, TimeSaveData data)
        {
            if (data == null || survivalSystem == null) return;
            
            try
            {
                // SetTime takes (day, hour, season) - note order!
                survivalSystem.SetTime(data.DayNumber, data.TimeOfDay, data.CurrentSeason);
            }
            catch { }
        }
        
        public static void RestoreStructures(BuildingSystem building, WorldGrid world, List<StructureSaveData> structures)
        {
            if (building == null) return;
            
            try
            {
                building.ClearAllStructures();
                
                if (structures != null)
                {
                    foreach (var structData in structures)
                    {
                        if (Enum.TryParse<StructureType>(structData.Type, out var type))
                        {
                            var pos = new Point(structData.TileX, structData.TileY);
                            var structure = building.PlaceInstant(type, pos, world);
                            
                            if (structure != null)
                            {
                                if (Enum.TryParse<StructureState>(structData.State, out var state))
                                {
                                    structure.State = state;
                                }
                                structure.CurrentHealth = structData.Health;
                            }
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($">>> Restored {structures?.Count ?? 0} structures <<<");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> RESTORE STRUCTURES ERROR: {ex.Message} <<<");
            }
        }
        
        public static List<EnemyEntity> RestoreEnemies(List<EnemySaveData> enemyData)
        {
            var enemies = new List<EnemyEntity>();
            
            if (enemyData == null) return enemies;
            
            int index = 0;
            foreach (var data in enemyData)
            {
                try
                {
                    var enemy = EnemyEntity.Create(data.Type, new Vector2(data.PositionX, data.PositionY), index++);
                    if (enemy != null)
                    {
                        enemy.MaxHealth = data.MaxHealth;
                        enemy.CurrentHealth = data.CurrentHealth;
                        enemy.IsProvoked = data.IsProvoked;
                        enemies.Add(enemy);
                    }
                }
                catch { }
            }
            
            return enemies;
        }
        
        public static List<WorldItem> RestoreGroundItems(List<GroundItemSaveData> itemData)
        {
            var items = new List<WorldItem>();
            
            if (itemData == null) return items;
            
            foreach (var data in itemData)
            {
                try
                {
                    var item = new Item(data.ItemDefId, ItemQuality.Normal, data.StackCount);
                    var pos = new Vector2(data.PositionX, data.PositionY);
                    items.Add(new WorldItem(item, pos));
                }
                catch { }
            }
            
            return items;
        }
        
        public static void RestoreQuests(QuestsSaveData data)
        {
            if (data == null) return;
            if (!GameServices.IsInitialized || GameServices.Quests == null) return;
            
            try
            {
                GameServices.Quests.Reset();
                
                // Restore completed quests
                if (data.CompletedQuestIds != null)
                {
                    GameServices.Quests.RestoreCompletedQuests(data.CompletedQuestIds);
                }
                
                // Restore active quests with progress
                if (data.ActiveQuests != null)
                {
                    foreach (var questProgress in data.ActiveQuests)
                    {
                        GameServices.Quests.RestoreActiveQuest(
                            questProgress.QuestId, 
                            questProgress.ObjectiveProgress ?? new Dictionary<string, int>()
                        );
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($">>> Restored {data.CompletedQuestIds?.Count ?? 0} completed, {data.ActiveQuests?.Count ?? 0} active quests <<<");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> RESTORE QUESTS ERROR: {ex.Message} <<<");
            }
        }
        
        // ============================================
        // NEW: RESTORE FACTIONS
        // ============================================
        
        public static void RestoreFactions(FactionsSaveData data)
        {
            if (data == null) return;
            if (!GameServices.IsInitialized || GameServices.Factions == null) return;
            
            try
            {
                var snapshot = new Dictionary<FactionType, int>();
                
                foreach (var kvp in data.Reputations)
                {
                    if (Enum.TryParse<FactionType>(kvp.Key, out var faction))
                    {
                        snapshot[faction] = kvp.Value;
                    }
                }
                
                GameServices.Factions.LoadReputationSnapshot(snapshot);
                System.Diagnostics.Debug.WriteLine($">>> Restored {snapshot.Count} faction reputations <<<");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> RESTORE FACTIONS ERROR: {ex.Message} <<<");
            }
        }
        
        // ============================================
        // NEW: RESTORE RESEARCH
        // ============================================
        
        // ============================================
        // NEW: RESTORE TUTORIAL
        // ============================================
        
        public static void RestoreTutorial(TutorialSaveData data)
        {
            if (data == null) return;
            if (!GameServices.IsInitialized || GameServices.Tutorial == null) return;
            
            try
            {
                GameServices.Tutorial.RestoreShownHints(
                    data.ShownHintIds ?? new List<string>(),
                    data.TutorialsEnabled
                );
                
                System.Diagnostics.Debug.WriteLine($">>> Restored {data.ShownHintIds?.Count ?? 0} shown tutorial hints <<<");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> RESTORE TUTORIAL ERROR: {ex.Message} <<<");
            }
        }
        
        public static void RestoreResearch(ResearchSaveData data, SciencePath playerPath)
        {
            if (data == null) return;
            if (!GameServices.IsInitialized || GameServices.Research == null) return;
            
            try
            {
                GameServices.Research.RestoreResearch(
                    data.CompletedResearchIds ?? new List<string>(),
                    data.CurrentResearchId,
                    data.CurrentResearchProgress,
                    playerPath
                );
                
                System.Diagnostics.Debug.WriteLine($">>> Restored {data.CompletedResearchIds?.Count ?? 0} completed research nodes <<<");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> RESTORE RESEARCH ERROR: {ex.Message} <<<");
            }
        }
        
        // ============================================
        // NEW: RESTORE FOG OF WAR
        // ============================================
        
        public static void RestoreFogOfWar(FogOfWarSaveData data, string currentZoneId, int worldWidth, int worldHeight)
        {
            if (data == null) return;
            if (!GameServices.IsInitialized || GameServices.FogOfWar == null) return;
            
            try
            {
                if (data.ExploredTilesPerZone.TryGetValue(currentZoneId, out var exploredTiles))
                {
                    var exploredGrid = new bool[worldWidth, worldHeight];
                    
                    foreach (var tileStr in exploredTiles)
                    {
                        var parts = tileStr.Split(',');
                        if (parts.Length == 2 && 
                            int.TryParse(parts[0], out int x) && 
                            int.TryParse(parts[1], out int y))
                        {
                            if (x >= 0 && x < worldWidth && y >= 0 && y < worldHeight)
                            {
                                exploredGrid[x, y] = true;
                            }
                        }
                    }
                    
                    GameServices.FogOfWar.LoadExplorationData(exploredGrid);
                    System.Diagnostics.Debug.WriteLine($">>> Restored {exploredTiles.Count} explored tiles for {currentZoneId} <<<");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> RESTORE FOG OF WAR ERROR: {ex.Message} <<<");
            }
        }
        
        // ============================================
        // NEW: RESTORE NPCs
        // ============================================
        
        public static List<NPCEntity> RestoreNPCs(List<NPCSaveData> npcData, string currentZoneId)
        {
            var npcs = new List<NPCEntity>();
            
            if (npcData == null) return npcs;
            
            foreach (var data in npcData.Where(n => n.ZoneId == currentZoneId))
            {
                try
                {
                    if (Enum.TryParse<NPCType>(data.NPCType, out var npcType))
                    {
                        var pos = new Vector2(data.PositionX, data.PositionY);
                        NPCEntity npc = null;
                        
                        // Create NPC based on type
                        switch (npcType)
                        {
                            case NPCType.Merchant:
                                npc = NPCEntity.CreateGeneralMerchant(data.Id, pos);
                                break;
                            case NPCType.WeaponSmith:
                                npc = NPCEntity.CreateWeaponsMerchant(data.Id, pos);
                                break;
                            case NPCType.Alchemist:
                                npc = NPCEntity.CreateAlchemist(data.Id, pos);
                                break;
                            case NPCType.Doctor:
                                npc = NPCEntity.CreateDoctor(data.Id, pos);
                                break;
                            case NPCType.Wanderer:
                                npc = NPCEntity.CreateWanderer(data.Id, pos);
                                break;
                            default:
                                npc = NPCEntity.CreateGeneralMerchant(data.Id, pos);
                                break;
                        }
                        
                        if (npc != null)
                        {
                            npc.Gold = data.Gold;
                            
                            // Restore stock
                            if (data.Stock != null && data.Stock.Count > 0)
                            {
                                npc.Stock.Clear();
                                foreach (var stockData in data.Stock)
                                {
                                    npc.Stock.Add(new MerchantStock(stockData.ItemId, stockData.Quantity));
                                }
                            }
                            
                            npcs.Add(npc);
                        }
                    }
                }
                catch { }
            }
            
            return npcs;
        }
    }
}
