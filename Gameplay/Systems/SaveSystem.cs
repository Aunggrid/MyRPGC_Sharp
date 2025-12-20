// Gameplay/Systems/SaveSystem.cs
// Minimal save/load system

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
        public int Version { get; set; } = 1;
        public DateTime SaveTime { get; set; }
        public string SlotName { get; set; }
        
        public PlayerSaveData Player { get; set; }
        public TimeSaveData Time { get; set; }
        public List<EnemySaveData> Enemies { get; set; }
        public List<GroundItemSaveData> GroundItems { get; set; }
        public QuestsSaveData Quests { get; set; }
        public WorldSaveData World { get; set; }
        
        public int PlayerLevel { get; set; }
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
    }
    
    // ============================================
    // SAVE SYSTEM (Static Methods)
    // ============================================
    
    public static class SaveSystem
    {
        private const string SAVE_FOLDER = "Saves";
        private const string QUICKSAVE_FILE = "quicksave.json";
        
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
            SurvivalSystem survivalSystem)
        {
            try
            {
                EnsureSaveDirectory();
                
                var saveData = new GameSaveData
                {
                    Version = 1,
                    SaveTime = DateTime.Now,
                    SlotName = "Quick Save",
                    PlayerLevel = player.Stats.Level
                };
                
                // Save player
                saveData.Player = CreatePlayerSaveData(player);
                
                // Save time - use properties that exist
                saveData.Time = new TimeSaveData
                {
                    TimeOfDay = 8f,
                    DayNumber = 1,
                    CurrentSeason = Season.Spring
                };
                
                // Try to get actual time values
                try
                {
                    if (survivalSystem != null)
                    {
                        // Use reflection or known properties
                        var hourProp = survivalSystem.GetType().GetProperty("CurrentHour");
                        if (hourProp != null) saveData.Time.TimeOfDay = (float)hourProp.GetValue(survivalSystem);
                        
                        var dayProp = survivalSystem.GetType().GetProperty("Day");
                        if (dayProp != null) saveData.Time.DayNumber = (int)dayProp.GetValue(survivalSystem);
                        
                        var seasonProp = survivalSystem.GetType().GetProperty("CurrentSeason");
                        if (seasonProp != null) saveData.Time.CurrentSeason = (Season)seasonProp.GetValue(survivalSystem);
                    }
                }
                catch { }
                
                // Save enemies
                saveData.Enemies = new List<EnemySaveData>();
                if (enemies != null)
                {
                    foreach (var enemy in enemies.Where(e => e.IsAlive))
                    {
                        saveData.Enemies.Add(new EnemySaveData
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
                
                // Save ground items
                saveData.GroundItems = new List<GroundItemSaveData>();
                if (groundItems != null)
                {
                    foreach (var wi in groundItems)
                    {
                        saveData.GroundItems.Add(new GroundItemSaveData
                        {
                            ItemDefId = wi.Item.ItemDefId,
                            StackCount = wi.Item.StackCount,
                            PositionX = wi.Position.X,
                            PositionY = wi.Position.Y
                        });
                    }
                }
                
                // Save world (structures) - simplified
                saveData.World = new WorldSaveData
                {
                    CurrentZoneId = "current",
                    Structures = new List<StructureSaveData>()
                };
                
                // Save quests - simplified
                saveData.Quests = new QuestsSaveData
                {
                    ActiveQuests = new List<QuestProgressSaveData>(),
                    CompletedQuestIds = new List<string>()
                };
                
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
                // Try to set time via reflection since API may vary
                var method = survivalSystem.GetType().GetMethod("SetTime");
                if (method != null)
                {
                    method.Invoke(survivalSystem, new object[] { data.TimeOfDay, data.DayNumber, data.CurrentSeason });
                }
            }
            catch { }
        }
        
        public static void RestoreStructures(BuildingSystem building, WorldGrid world, List<StructureSaveData> structures)
        {
            // Simplified - structures not fully saved in this version
            try
            {
                building?.ClearAllStructures();
            }
            catch { }
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
            // Quests not fully saved in this version
        }
    }
}
