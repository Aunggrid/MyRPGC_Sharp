// Gameplay/Systems/SaveSystem.cs
// Handles saving and loading game state

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using MyRPG.Data;
using MyRPG.Gameplay.Building;
using MyRPG.Gameplay.Character;
using MyRPG.Gameplay.Entities;
using MyRPG.Gameplay.Items;
using MyRPG.Gameplay.World;

namespace MyRPG.Gameplay.Systems
{
    public class SaveSystem
    {
        private static readonly string SaveDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyRPG", "Saves"
        );
        
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        
        // ============================================
        // INITIALIZATION
        // ============================================
        
        static SaveSystem()
        {
            // Ensure save directory exists
            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }
        }
        
        // ============================================
        // SAVE GAME
        // ============================================
        
        /// <summary>
        /// Save the current game state
        /// </summary>
        public static bool SaveGame(
            string saveName,
            PlayerEntity player,
            List<EnemyEntity> enemies,
            List<WorldItem> groundItems,
            WorldGrid world,
            BuildingSystem buildingSystem,
            SurvivalSystem survivalSystem)
        {
            try
            {
                var saveData = new SaveData
                {
                    Version = "0.3",
                    SaveTime = DateTime.Now,
                    SaveName = saveName,
                    Player = CreatePlayerSaveData(player),
                    World = CreateWorldSaveData(world, buildingSystem),
                    Time = CreateTimeSaveData(survivalSystem),
                    Enemies = CreateEnemySaveData(enemies),
                    GroundItems = CreateGroundItemsSaveData(groundItems),
                    Quests = CreateQuestsSaveData()
                };
                
                string fileName = $"{saveName}.json";
                string filePath = Path.Combine(SaveDirectory, fileName);
                
                string json = JsonSerializer.Serialize(saveData, JsonOptions);
                File.WriteAllText(filePath, json);
                
                System.Diagnostics.Debug.WriteLine($">>> Game saved to: {filePath} <<<");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> Save failed: {ex.Message} <<<");
                return false;
            }
        }
        
        /// <summary>
        /// Quick save with auto-generated name
        /// </summary>
        public static bool QuickSave(
            PlayerEntity player,
            List<EnemyEntity> enemies,
            List<WorldItem> groundItems,
            WorldGrid world,
            BuildingSystem buildingSystem,
            SurvivalSystem survivalSystem)
        {
            return SaveGame("quicksave", player, enemies, groundItems, world, buildingSystem, survivalSystem);
        }
        
        // ============================================
        // LOAD GAME
        // ============================================
        
        /// <summary>
        /// Load a saved game
        /// </summary>
        public static SaveData LoadGame(string saveName)
        {
            try
            {
                string fileName = saveName.EndsWith(".json") ? saveName : $"{saveName}.json";
                string filePath = Path.Combine(SaveDirectory, fileName);
                
                if (!File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine($">>> Save file not found: {filePath} <<<");
                    return null;
                }
                
                string json = File.ReadAllText(filePath);
                var saveData = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
                
                System.Diagnostics.Debug.WriteLine($">>> Game loaded from: {filePath} <<<");
                return saveData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($">>> Load failed: {ex.Message} <<<");
                return null;
            }
        }
        
        /// <summary>
        /// Quick load the quicksave file
        /// </summary>
        public static SaveData QuickLoad()
        {
            return LoadGame("quicksave");
        }
        
        // ============================================
        // CHECK SAVE EXISTS
        // ============================================
        
        public static bool SaveExists(string saveName)
        {
            string fileName = saveName.EndsWith(".json") ? saveName : $"{saveName}.json";
            string filePath = Path.Combine(SaveDirectory, fileName);
            return File.Exists(filePath);
        }
        
        public static bool QuickSaveExists()
        {
            return SaveExists("quicksave");
        }
        
        public static List<string> GetAllSaves()
        {
            if (!Directory.Exists(SaveDirectory))
                return new List<string>();
            
            return Directory.GetFiles(SaveDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderByDescending(f => File.GetLastWriteTime(Path.Combine(SaveDirectory, f + ".json")))
                .ToList();
        }
        
        public static string GetSaveDirectory()
        {
            return SaveDirectory;
        }
        
        // ============================================
        // CREATE SAVE DATA HELPERS
        // ============================================
        
        private static PlayerSaveData CreatePlayerSaveData(PlayerEntity player)
        {
            var stats = player.Stats;
            
            return new PlayerSaveData
            {
                PositionX = player.Position.X,
                PositionY = player.Position.Y,
                CurrentHealth = stats.CurrentHealth,
                Level = stats.Level,
                CurrentXP = stats.CurrentXP,
                MutationPoints = stats.MutationPoints,
                FreeMutationPicks = stats.FreeMutationPicks,
                PendingAttributePoints = stats.PendingAttributePoints,
                Gold = stats.Gold,
                SciencePath = stats.SciencePath,
                
                Attributes = new AttributesSaveData
                {
                    STR = stats.Attributes.STR,
                    AGI = stats.Attributes.AGI,
                    END = stats.Attributes.END,
                    INT = stats.Attributes.INT,
                    PER = stats.Attributes.PER,
                    WIL = stats.Attributes.WIL
                },
                
                Mutations = stats.Mutations.Select(m => new MutationSaveData
                {
                    Type = m.Type,
                    Level = m.Level
                }).ToList(),
                
                Traits = new List<TraitType>(stats.Traits),
                
                Inventory = CreateInventorySaveData(stats.Inventory),
                
                Survival = new SurvivalSaveData
                {
                    Hunger = stats.Survival.Hunger,
                    Thirst = stats.Survival.Thirst,
                    Rest = stats.Survival.Rest,
                    Temperature = stats.Survival.Temperature
                }
            };
        }
        
        private static InventorySaveData CreateInventorySaveData(Inventory inventory)
        {
            var saveData = new InventorySaveData
            {
                MaxSlots = inventory.MaxSlots,
                MaxWeight = inventory.MaxWeight,
                Items = new List<ItemSaveData>(),
                Equipment = new Dictionary<EquipSlot, ItemSaveData>()
            };
            
            // Save inventory items
            foreach (var item in inventory.GetAllItems())
            {
                saveData.Items.Add(CreateItemSaveData(item));
            }
            
            // Save equipped items
            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                if (slot == EquipSlot.None) continue;
                
                var equipped = inventory.GetEquipped(slot);
                if (equipped != null)
                {
                    saveData.Equipment[slot] = CreateItemSaveData(equipped);
                }
            }
            
            return saveData;
        }
        
        private static ItemSaveData CreateItemSaveData(Item item)
        {
            return new ItemSaveData
            {
                DefinitionId = item.ItemDefId,
                Quality = item.Quality,
                StackCount = item.StackCount,
                Durability = item.Durability
            };
        }
        
        private static WorldSaveData CreateWorldSaveData(WorldGrid world, BuildingSystem buildingSystem)
        {
            var saveData = new WorldSaveData
            {
                Width = world.Width,
                Height = world.Height,
                TileSize = world.TileSize,
                Structures = new List<StructureSaveData>()
            };
            
            foreach (var structure in buildingSystem.GetAllStructures())
            {
                saveData.Structures.Add(new StructureSaveData
                {
                    Type = structure.Type,
                    TileX = structure.Position.X,
                    TileY = structure.Position.Y,
                    Health = structure.CurrentHealth,
                    MaxHealth = structure.Definition.MaxHealth,
                    State = structure.State,
                    BuildProgress = structure.BuildProgress,
                    IsOpen = structure.IsOpen,
                    DepositedResources = new Dictionary<string, int>(structure.DepositedResources)
                });
            }
            
            return saveData;
        }
        
        private static TimeSaveData CreateTimeSaveData(SurvivalSystem survivalSystem)
        {
            int hours = (int)survivalSystem.GameHour;
            int minutes = (int)((survivalSystem.GameHour - hours) * 60);
            
            return new TimeSaveData
            {
                Day = survivalSystem.GameDay,
                Hour = hours,
                Minute = minutes,
                Season = survivalSystem.CurrentSeason
            };
        }
        
        private static QuestsSaveData CreateQuestsSaveData()
        {
            var questSystem = GameServices.Quests;
            
            var activeQuestsData = questSystem.GetActiveQuests().Select(q => new ActiveQuestSaveData
            {
                QuestId = q.QuestId,
                ObjectiveProgress = q.Objectives.ToDictionary(o => o.Id, o => o.CurrentCount)
            }).ToList();
            
            return new QuestsSaveData
            {
                CompletedQuests = questSystem.GetCompletedQuestIds(),
                ActiveQuests = activeQuestsData
            };
        }
        
        private static List<EnemySaveData> CreateEnemySaveData(List<EnemyEntity> enemies)
        {
            return enemies.Select(e => new EnemySaveData
            {
                Type = e.Type,
                PositionX = e.Position.X,
                PositionY = e.Position.Y,
                CurrentHealth = e.CurrentHealth,
                MaxHealth = e.MaxHealth,
                IsAlive = e.IsAlive,
                State = e.State
            }).ToList();
        }
        
        private static List<WorldItemSaveData> CreateGroundItemsSaveData(List<WorldItem> groundItems)
        {
            return groundItems.Select(wi => new WorldItemSaveData
            {
                Item = CreateItemSaveData(wi.Item),
                PositionX = wi.Position.X,
                PositionY = wi.Position.Y
            }).ToList();
        }
        
        // ============================================
        // RESTORE GAME STATE HELPERS
        // ============================================
        
        /// <summary>
        /// Restore player state from save data
        /// </summary>
        public static void RestorePlayer(PlayerEntity player, PlayerSaveData data, MutationSystem mutationSystem)
        {
            player.Position = new Vector2(data.PositionX, data.PositionY);
            
            var stats = player.Stats;
            
            // Restore attributes using Set method
            stats.Attributes.Set(AttributeType.STR, data.Attributes.STR);
            stats.Attributes.Set(AttributeType.AGI, data.Attributes.AGI);
            stats.Attributes.Set(AttributeType.END, data.Attributes.END);
            stats.Attributes.Set(AttributeType.INT, data.Attributes.INT);
            stats.Attributes.Set(AttributeType.PER, data.Attributes.PER);
            stats.Attributes.Set(AttributeType.WIL, data.Attributes.WIL);
            
            // Restore health
            stats.CurrentHealth = data.CurrentHealth;
            
            // Restore level/XP using RestoreState method
            stats.RestoreState(
                data.Level,
                data.CurrentXP,
                data.MutationPoints,
                data.FreeMutationPicks,
                data.PendingAttributePoints,
                data.Gold
            );
            
            // Science path
            stats.SciencePath = data.SciencePath;
            
            // Restore mutations
            stats.Mutations.Clear();
            foreach (var mutData in data.Mutations)
            {
                var definition = mutationSystem.GetDefinition(mutData.Type);
                if (definition != null)
                {
                    var instance = new MutationInstance(mutData.Type, definition.MaxLevel);
                    instance.Level = mutData.Level;
                    stats.Mutations.Add(instance);
                }
            }
            
            // Restore traits
            stats.Traits.Clear();
            stats.Traits.AddRange(data.Traits);
            
            // Restore survival
            stats.Survival.SetAllValues(
                data.Survival.Hunger,
                data.Survival.Thirst,
                data.Survival.Rest,
                data.Survival.Temperature
            );
            
            // Restore inventory
            RestoreInventory(stats.Inventory, data.Inventory);
        }
        
        /// <summary>
        /// Restore inventory from save data
        /// </summary>
        public static void RestoreInventory(Inventory inventory, InventorySaveData data)
        {
            // Clear current inventory
            inventory.Clear();
            
            // Set capacity
            inventory.MaxSlots = data.MaxSlots;
            inventory.MaxWeight = data.MaxWeight;
            
            // Restore items
            foreach (var itemData in data.Items)
            {
                var item = new Item(itemData.DefinitionId, itemData.Quality, itemData.StackCount);
                item.Durability = itemData.Durability;
                inventory.TryAddItem(item);
            }
            
            // Restore equipment
            foreach (var kvp in data.Equipment)
            {
                var item = new Item(kvp.Value.DefinitionId, kvp.Value.Quality, kvp.Value.StackCount);
                item.Durability = kvp.Value.Durability;
                inventory.EquipItem(item);
            }
        }
        
        /// <summary>
        /// Restore structures from save data
        /// </summary>
        public static void RestoreStructures(BuildingSystem buildingSystem, WorldGrid world, List<StructureSaveData> structures)
        {
            // Clear existing structures
            buildingSystem.ClearAllStructures();
            
            // Recreate structures
            foreach (var data in structures)
            {
                var position = new Point(data.TileX, data.TileY);
                
                // Use PlaceInstant to create complete structures
                var structure = buildingSystem.PlaceInstant(data.Type, position, world);
                
                if (structure != null)
                {
                    structure.CurrentHealth = data.Health;
                    structure.State = data.State;
                    structure.BuildProgress = data.BuildProgress;
                    structure.IsOpen = data.IsOpen;
                    
                    // Restore deposited resources
                    structure.DepositedResources.Clear();
                    foreach (var kvp in data.DepositedResources)
                    {
                        structure.DepositedResources[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        
        /// <summary>
        /// Create enemies from save data
        /// </summary>
        public static List<EnemyEntity> RestoreEnemies(List<EnemySaveData> enemyData)
        {
            var enemies = new List<EnemyEntity>();
            int idCounter = 1;
            
            foreach (var data in enemyData)
            {
                if (!data.IsAlive) continue;
                
                var enemy = new EnemyEntity($"enemy_{idCounter++}", data.Type);
                enemy.Position = new Vector2(data.PositionX, data.PositionY);
                
                // Restore health
                float damage = data.MaxHealth - data.CurrentHealth;
                if (damage > 0)
                {
                    enemy.TakeDamage(damage);
                }
                
                // Restore state
                enemy.State = data.State;
                
                enemies.Add(enemy);
            }
            
            return enemies;
        }
        
        /// <summary>
        /// Create ground items from save data
        /// </summary>
        public static List<WorldItem> RestoreGroundItems(List<WorldItemSaveData> itemData)
        {
            var items = new List<WorldItem>();
            
            foreach (var data in itemData)
            {
                var item = new Item(data.Item.DefinitionId, data.Item.Quality, data.Item.StackCount);
                item.Durability = data.Item.Durability;
                
                var worldItem = new WorldItem(item, new Vector2(data.PositionX, data.PositionY));
                items.Add(worldItem);
            }
            
            return items;
        }
        
        /// <summary>
        /// Restore time system from save data
        /// </summary>
        public static void RestoreTime(SurvivalSystem survivalSystem, TimeSaveData data)
        {
            float hour = data.Hour + (data.Minute / 60f);
            survivalSystem.SetTime(data.Day, hour, data.Season);
        }
        
        public static void RestoreQuests(QuestsSaveData data)
        {
            if (data == null) return;
            
            var questSystem = GameServices.Quests;
            
            // Restore completed quests
            questSystem.RestoreCompletedQuests(data.CompletedQuests);
            
            // Restore active quests with their progress
            foreach (var activeQuest in data.ActiveQuests)
            {
                questSystem.RestoreActiveQuest(activeQuest.QuestId, activeQuest.ObjectiveProgress);
            }
            
            System.Diagnostics.Debug.WriteLine($">>> Restored {data.CompletedQuests.Count} completed quests, {data.ActiveQuests.Count} active quests <<<");
        }
    }
}
