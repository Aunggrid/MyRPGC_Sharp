// Gameplay/Building/Structure.cs
// Structure definitions for base building system

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MyRPG.Gameplay.Building
{
    // ============================================
    // STRUCTURE TYPE
    // ============================================
    
    public enum StructureType
    {
        // Walls & Barriers
        WoodWall,
        StoneWall,
        MetalWall,
        
        // Doors
        WoodDoor,
        MetalDoor,
        
        // Floors
        WoodFloor,
        StoneFloor,
        
        // Furniture
        Bed,
        Campfire,
        StorageBox,
        
        // Workstations
        CraftingBench,
        ResearchTable,
        CookingStation,
        
        // Utility
        Torch,
        Barricade
    }
    
    public enum StructureCategory
    {
        Wall,
        Door,
        Floor,
        Furniture,
        Workstation,
        Light,
        Defense
    }
    
    public enum StructureState
    {
        Blueprint,      // Planned but not built
        UnderConstruction,  // Being built (has some resources)
        Complete,       // Fully functional
        Damaged,        // Needs repair
        Destroyed       // No longer functional
    }
    
    // ============================================
    // STRUCTURE DEFINITION (static data)
    // ============================================
    
    public class StructureDefinition
    {
        public StructureType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public StructureCategory Category { get; set; }
        
        // Size (in tiles)
        public int Width { get; set; } = 1;
        public int Height { get; set; } = 1;
        
        // Properties
        public bool BlocksMovement { get; set; } = false;
        public bool BlocksVision { get; set; } = false;
        public bool Interactable { get; set; } = false;
        public bool CanBeOpened { get; set; } = false;  // For doors
        
        // Stats
        public float MaxHealth { get; set; } = 100f;
        public float CoverValue { get; set; } = 0f;     // 0-1, protection in combat
        
        // Construction
        public Dictionary<string, int> BuildCost { get; set; } = new Dictionary<string, int>();
        public float BuildTime { get; set; } = 5f;      // Seconds to build
        public int BuildSkillRequired { get; set; } = 0; // Minimum crafting skill
        
        // Special effects
        public float LightRadius { get; set; } = 0f;    // For torches, campfires
        public float WarmthRadius { get; set; } = 0f;   // For campfires
        public float WarmthAmount { get; set; } = 0f;   // Temperature bonus
        public float RestQuality { get; set; } = 0f;    // For beds (0-1)
        public int StorageSlots { get; set; } = 0;      // For storage containers
        
        // Visual
        public Color DisplayColor { get; set; } = Color.Gray;
        public Color BlueprintColor { get; set; } = Color.CornflowerBlue * 0.5f;
    }
    
    // ============================================
    // STRUCTURE INSTANCE (placed in world)
    // ============================================
    
    public class Structure
    {
        // Identity
        public string Id { get; private set; }
        public StructureType Type { get; private set; }
        public StructureDefinition Definition { get; private set; }
        
        // Position (tile coordinates)
        public Point Position { get; set; }
        
        // State
        public StructureState State { get; set; } = StructureState.Blueprint;
        public float CurrentHealth { get; set; }
        public float BuildProgress { get; set; } = 0f;  // 0-1
        
        // Door state
        public bool IsOpen { get; set; } = false;
        
        // Storage (for containers)
        public List<string> StoredItems { get; set; } = new List<string>();
        
        // Resources deposited toward construction
        public Dictionary<string, int> DepositedResources { get; set; } = new Dictionary<string, int>();
        
        // Owner/faction
        public string OwnerId { get; set; } = "player";
        
        // ============================================
        // CONSTRUCTOR
        // ============================================
        
        public Structure(string id, StructureType type, StructureDefinition definition, Point position)
        {
            Id = id;
            Type = type;
            Definition = definition;
            Position = position;
            CurrentHealth = definition.MaxHealth;
        }
        
        // ============================================
        // PROPERTIES
        // ============================================
        
        public bool BlocksMovement
        {
            get
            {
                if (State == StructureState.Blueprint) return false;
                if (State == StructureState.Destroyed) return false;
                if (Definition.CanBeOpened && IsOpen) return false;
                return Definition.BlocksMovement;
            }
        }
        
        public bool BlocksVision
        {
            get
            {
                if (State == StructureState.Blueprint) return false;
                if (State == StructureState.Destroyed) return false;
                if (Definition.CanBeOpened && IsOpen) return false;
                return Definition.BlocksVision;
            }
        }
        
        public bool IsComplete => State == StructureState.Complete;
        public bool IsFunctional => State == StructureState.Complete || State == StructureState.Damaged;
        
        public float HealthPercent => CurrentHealth / Definition.MaxHealth;
        
        // ============================================
        // CONSTRUCTION
        // ============================================
        
        /// <summary>
        /// Check if all required resources have been deposited
        /// </summary>
        public bool HasAllResources()
        {
            foreach (var cost in Definition.BuildCost)
            {
                int deposited = DepositedResources.GetValueOrDefault(cost.Key, 0);
                if (deposited < cost.Value) return false;
            }
            return true;
        }
        
        /// <summary>
        /// Get remaining resources needed
        /// </summary>
        public Dictionary<string, int> GetRemainingResources()
        {
            var remaining = new Dictionary<string, int>();
            foreach (var cost in Definition.BuildCost)
            {
                int deposited = DepositedResources.GetValueOrDefault(cost.Key, 0);
                int needed = cost.Value - deposited;
                if (needed > 0) remaining[cost.Key] = needed;
            }
            return remaining;
        }
        
        /// <summary>
        /// Deposit a resource toward construction
        /// </summary>
        public bool DepositResource(string resourceId, int amount)
        {
            if (!Definition.BuildCost.ContainsKey(resourceId)) return false;
            
            int current = DepositedResources.GetValueOrDefault(resourceId, 0);
            int max = Definition.BuildCost[resourceId];
            int canDeposit = Math.Min(amount, max - current);
            
            if (canDeposit <= 0) return false;
            
            DepositedResources[resourceId] = current + canDeposit;
            
            if (State == StructureState.Blueprint && HasAllResources())
            {
                State = StructureState.UnderConstruction;
            }
            
            return true;
        }
        
        /// <summary>
        /// Add build progress (called when player/pawn works on it)
        /// </summary>
        public bool AddBuildProgress(float amount)
        {
            if (State != StructureState.UnderConstruction) return false;
            
            BuildProgress += amount / Definition.BuildTime;
            
            if (BuildProgress >= 1f)
            {
                BuildProgress = 1f;
                State = StructureState.Complete;
                System.Diagnostics.Debug.WriteLine($">>> {Definition.Name} construction complete! <<<");
                return true; // Completed
            }
            
            return false;
        }
        
        // ============================================
        // DAMAGE & REPAIR
        // ============================================
        
        public void TakeDamage(float amount)
        {
            CurrentHealth -= amount;
            
            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0;
                State = StructureState.Destroyed;
                System.Diagnostics.Debug.WriteLine($">>> {Definition.Name} destroyed! <<<");
            }
            else if (CurrentHealth < Definition.MaxHealth * 0.5f)
            {
                if (State == StructureState.Complete)
                {
                    State = StructureState.Damaged;
                }
            }
        }
        
        public void Repair(float amount)
        {
            if (State == StructureState.Destroyed) return;
            
            CurrentHealth = Math.Min(CurrentHealth + amount, Definition.MaxHealth);
            
            if (CurrentHealth >= Definition.MaxHealth * 0.5f && State == StructureState.Damaged)
            {
                State = StructureState.Complete;
            }
        }
        
        // ============================================
        // INTERACTION
        // ============================================
        
        /// <summary>
        /// Toggle door open/closed
        /// </summary>
        public bool ToggleDoor()
        {
            if (!Definition.CanBeOpened) return false;
            if (!IsFunctional) return false;
            
            IsOpen = !IsOpen;
            System.Diagnostics.Debug.WriteLine($">>> {Definition.Name} {(IsOpen ? "opened" : "closed")} <<<");
            return true;
        }
        
        // ============================================
        // DISPLAY
        // ============================================
        
        public Color GetDisplayColor()
        {
            return State switch
            {
                StructureState.Blueprint => Definition.BlueprintColor,
                StructureState.UnderConstruction => Color.Lerp(Definition.BlueprintColor, Definition.DisplayColor, BuildProgress),
                StructureState.Complete => Definition.DisplayColor,
                StructureState.Damaged => Color.Lerp(Definition.DisplayColor, Color.DarkRed, 0.3f),
                StructureState.Destroyed => Color.DarkGray * 0.5f,
                _ => Color.Gray
            };
        }
        
        public override string ToString()
        {
            return $"{Definition.Name} [{State}] at ({Position.X}, {Position.Y}) HP:{CurrentHealth:F0}/{Definition.MaxHealth:F0}";
        }
    }
}
