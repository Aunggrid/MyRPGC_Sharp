// Gameplay/Systems/SaveData.cs
// Data structures for save/load serialization

using System;
using System.Collections.Generic;
using MyRPG.Data;

namespace MyRPG.Gameplay.Systems
{
    /// <summary>
    /// Root save data container
    /// </summary>
    public class SaveData
    {
        public string Version { get; set; } = "0.3";
        public DateTime SaveTime { get; set; }
        public string SaveName { get; set; }
        
        public PlayerSaveData Player { get; set; }
        public WorldSaveData World { get; set; }
        public TimeSaveData Time { get; set; }
        public List<EnemySaveData> Enemies { get; set; } = new List<EnemySaveData>();
        public List<WorldItemSaveData> GroundItems { get; set; } = new List<WorldItemSaveData>();
        public QuestsSaveData Quests { get; set; }
    }
    
    /// <summary>
    /// Player character data
    /// </summary>
    public class PlayerSaveData
    {
        // Position
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        
        // Core stats
        public float CurrentHealth { get; set; }
        public int Level { get; set; }
        public float CurrentXP { get; set; }
        public int MutationPoints { get; set; }
        public int FreeMutationPicks { get; set; }
        public int PendingAttributePoints { get; set; }
        public int Gold { get; set; }
        
        // Attributes
        public AttributesSaveData Attributes { get; set; }
        
        // Science path
        public SciencePath SciencePath { get; set; }
        
        // Collections
        public List<MutationSaveData> Mutations { get; set; } = new List<MutationSaveData>();
        public List<TraitType> Traits { get; set; } = new List<TraitType>();
        
        // Inventory
        public InventorySaveData Inventory { get; set; }
        
        // Survival
        public SurvivalSaveData Survival { get; set; }
    }
    
    /// <summary>
    /// Attribute values
    /// </summary>
    public class AttributesSaveData
    {
        public int STR { get; set; }
        public int AGI { get; set; }
        public int END { get; set; }
        public int INT { get; set; }
        public int PER { get; set; }
        public int WIL { get; set; }
    }
    
    /// <summary>
    /// Mutation instance data
    /// </summary>
    public class MutationSaveData
    {
        public MutationType Type { get; set; }
        public int Level { get; set; }
    }
    
    /// <summary>
    /// Inventory and equipment
    /// </summary>
    public class InventorySaveData
    {
        public int MaxSlots { get; set; }
        public float MaxWeight { get; set; }
        public List<ItemSaveData> Items { get; set; } = new List<ItemSaveData>();
        public Dictionary<EquipSlot, ItemSaveData> Equipment { get; set; } = new Dictionary<EquipSlot, ItemSaveData>();
    }
    
    /// <summary>
    /// Individual item data
    /// </summary>
    public class ItemSaveData
    {
        public string DefinitionId { get; set; }
        public ItemQuality Quality { get; set; }
        public int StackCount { get; set; }
        public float Durability { get; set; }
    }
    
    /// <summary>
    /// Survival needs state
    /// </summary>
    public class SurvivalSaveData
    {
        public float Hunger { get; set; }
        public float Thirst { get; set; }
        public float Rest { get; set; }
        public float Temperature { get; set; }
    }
    
    /// <summary>
    /// World/structure data
    /// </summary>
    public class WorldSaveData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int TileSize { get; set; }
        public List<StructureSaveData> Structures { get; set; } = new List<StructureSaveData>();
    }
    
    /// <summary>
    /// Individual structure data
    /// </summary>
    public class StructureSaveData
    {
        public StructureType Type { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public StructureState State { get; set; }
        public float BuildProgress { get; set; }
        public bool IsOpen { get; set; }  // For doors
        public Dictionary<string, int> DepositedResources { get; set; } = new Dictionary<string, int>();
    }
    
    /// <summary>
    /// Enemy data
    /// </summary>
    public class EnemySaveData
    {
        public EnemyType Type { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float CurrentHealth { get; set; }
        public float MaxHealth { get; set; }
        public bool IsAlive { get; set; }
        public EnemyState State { get; set; }
    }
    
    /// <summary>
    /// Ground item data
    /// </summary>
    public class WorldItemSaveData
    {
        public ItemSaveData Item { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
    }
    
    /// <summary>
    /// Time/day cycle data
    /// </summary>
    public class TimeSaveData
    {
        public int Day { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public Season Season { get; set; }
    }
    
    /// <summary>
    /// Quest system data
    /// </summary>
    public class QuestsSaveData
    {
        public List<string> CompletedQuests { get; set; } = new List<string>();
        public List<ActiveQuestSaveData> ActiveQuests { get; set; } = new List<ActiveQuestSaveData>();
    }
    
    /// <summary>
    /// Active quest progress data
    /// </summary>
    public class ActiveQuestSaveData
    {
        public string QuestId { get; set; }
        public Dictionary<string, int> ObjectiveProgress { get; set; } = new Dictionary<string, int>();
    }
}
