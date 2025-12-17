// Gameplay/Building/BuildingSystem.cs
// Manages structure placement, definitions, and building operations

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MyRPG.Gameplay.World;

namespace MyRPG.Gameplay.Building
{
    public class BuildingSystem
    {
        // All structure definitions
        private Dictionary<StructureType, StructureDefinition> _definitions;
        
        // All placed structures in the world (by position for quick lookup)
        private Dictionary<Point, Structure> _structures = new Dictionary<Point, Structure>();
        
        // Structure ID counter
        private int _nextId = 1;
        
        // Build mode state
        public bool InBuildMode { get; set; } = false;
        public StructureType? SelectedStructure { get; set; } = null;
        public Point? PreviewPosition { get; set; } = null;
        
        // Events
        public event Action<Structure> OnStructurePlaced;
        public event Action<Structure> OnStructureCompleted;
        public event Action<Structure> OnStructureDestroyed;
        
        // ============================================
        // INITIALIZATION
        // ============================================
        
        public BuildingSystem()
        {
            InitializeDefinitions();
        }
        
        // ============================================
        // STRUCTURE QUERY
        // ============================================
        
        public Structure GetStructureAt(Point position)
        {
            return _structures.GetValueOrDefault(position);
        }
        
        public Structure GetStructureAt(int x, int y)
        {
            return GetStructureAt(new Point(x, y));
        }
        
        public bool HasStructureAt(Point position)
        {
            return _structures.ContainsKey(position);
        }
        
        public List<Structure> GetAllStructures()
        {
            return _structures.Values.ToList();
        }
        
        public List<Structure> GetStructuresByType(StructureType type)
        {
            return _structures.Values.Where(s => s.Type == type).ToList();
        }
        
        public List<Structure> GetStructuresByCategory(StructureCategory category)
        {
            return _structures.Values.Where(s => s.Definition.Category == category).ToList();
        }
        
        public List<Structure> GetIncompleteStructures()
        {
            return _structures.Values
                .Where(s => s.State == StructureState.Blueprint || s.State == StructureState.UnderConstruction)
                .ToList();
        }
        
        /// <summary>
        /// Check if a tile is blocked by a structure
        /// </summary>
        public bool IsBlockedByStructure(Point position)
        {
            var structure = GetStructureAt(position);
            return structure != null && structure.BlocksMovement;
        }
        
        /// <summary>
        /// Get structures providing warmth at a position
        /// </summary>
        public float GetWarmthAt(Point position, int range = 5)
        {
            float warmth = 0f;
            
            foreach (var structure in _structures.Values)
            {
                if (!structure.IsFunctional) continue;
                if (structure.Definition.WarmthRadius <= 0) continue;
                
                float dist = Vector2.Distance(position.ToVector2(), structure.Position.ToVector2());
                if (dist <= structure.Definition.WarmthRadius)
                {
                    float falloff = 1f - (dist / structure.Definition.WarmthRadius);
                    warmth += structure.Definition.WarmthAmount * falloff;
                }
            }
            
            return warmth;
        }
        
        /// <summary>
        /// Get light level at a position (0-1)
        /// </summary>
        public float GetLightAt(Point position)
        {
            float light = 0f;
            
            foreach (var structure in _structures.Values)
            {
                if (!structure.IsFunctional) continue;
                if (structure.Definition.LightRadius <= 0) continue;
                
                float dist = Vector2.Distance(position.ToVector2(), structure.Position.ToVector2());
                if (dist <= structure.Definition.LightRadius)
                {
                    float falloff = 1f - (dist / structure.Definition.LightRadius);
                    light = Math.Max(light, falloff);
                }
            }
            
            return Math.Clamp(light, 0f, 1f);
        }
        
        // ============================================
        // PLACEMENT VALIDATION
        // ============================================
        
        /// <summary>
        /// Check if a structure can be placed at a position
        /// </summary>
        public bool CanPlaceAt(StructureType type, Point position, WorldGrid grid)
        {
            var def = GetDefinition(type);
            if (def == null) return false;
            
            // Check all tiles the structure occupies
            for (int x = 0; x < def.Width; x++)
            {
                for (int y = 0; y < def.Height; y++)
                {
                    Point checkPos = new Point(position.X + x, position.Y + y);
                    
                    // Check bounds
                    if (!grid.IsInBounds(checkPos)) return false;
                    
                    // Check tile walkability (can't build on water, etc.)
                    var tile = grid.Tiles[checkPos.X, checkPos.Y];
                    if (!tile.IsWalkable && def.Category != StructureCategory.Floor) return false;
                    
                    // Check for existing structures
                    if (HasStructureAt(checkPos)) return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Get placement validity color for preview
        /// </summary>
        public Color GetPlacementColor(StructureType type, Point position, WorldGrid grid)
        {
            if (CanPlaceAt(type, position, grid))
            {
                return Color.Green * 0.5f;
            }
            return Color.Red * 0.5f;
        }
        
        // ============================================
        // PLACEMENT & REMOVAL
        // ============================================
        
        /// <summary>
        /// Place a structure blueprint at a position
        /// </summary>
        public Structure PlaceBlueprint(StructureType type, Point position, WorldGrid grid)
        {
            if (!CanPlaceAt(type, position, grid)) return null;
            
            var def = GetDefinition(type);
            string id = $"structure_{_nextId++}";
            
            var structure = new Structure(id, type, def, position)
            {
                State = StructureState.Blueprint,
                BuildProgress = 0f
            };
            
            // For all tiles this structure occupies
            for (int x = 0; x < def.Width; x++)
            {
                for (int y = 0; y < def.Height; y++)
                {
                    Point occupyPos = new Point(position.X + x, position.Y + y);
                    _structures[occupyPos] = structure;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($">>> Blueprint placed: {def.Name} at ({position.X}, {position.Y}) <<<");
            OnStructurePlaced?.Invoke(structure);
            
            return structure;
        }
        
        /// <summary>
        /// Instantly place a completed structure (for testing/cheats)
        /// </summary>
        public Structure PlaceInstant(StructureType type, Point position, WorldGrid grid)
        {
            var structure = PlaceBlueprint(type, position, grid);
            if (structure != null)
            {
                structure.State = StructureState.Complete;
                structure.BuildProgress = 1f;
                // Auto-fill resources
                foreach (var cost in structure.Definition.BuildCost)
                {
                    structure.DepositedResources[cost.Key] = cost.Value;
                }
            }
            return structure;
        }
        
        /// <summary>
        /// Remove a structure
        /// </summary>
        public bool RemoveStructure(Point position)
        {
            var structure = GetStructureAt(position);
            if (structure == null) return false;
            
            // Remove from all tiles it occupies
            var def = structure.Definition;
            for (int x = 0; x < def.Width; x++)
            {
                for (int y = 0; y < def.Height; y++)
                {
                    Point occupyPos = new Point(structure.Position.X + x, structure.Position.Y + y);
                    _structures.Remove(occupyPos);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($">>> Structure removed: {def.Name} at ({position.X}, {position.Y}) <<<");
            OnStructureDestroyed?.Invoke(structure);
            
            return true;
        }
        
        /// <summary>
        /// Cancel a blueprint (refund deposited resources)
        /// </summary>
        public Dictionary<string, int> CancelBlueprint(Point position)
        {
            var structure = GetStructureAt(position);
            if (structure == null) return null;
            if (structure.State == StructureState.Complete) return null;
            
            var refund = new Dictionary<string, int>(structure.DepositedResources);
            RemoveStructure(position);
            
            return refund;
        }
        
        // ============================================
        // CONSTRUCTION
        // ============================================
        
        /// <summary>
        /// Work on constructing a structure (called when player is adjacent)
        /// </summary>
        public bool WorkOnConstruction(Point position, float workAmount = 1f)
        {
            var structure = GetStructureAt(position);
            if (structure == null) return false;
            
            if (structure.State == StructureState.Blueprint)
            {
                // Need resources first
                System.Diagnostics.Debug.WriteLine($">>> {structure.Definition.Name} needs resources: {string.Join(", ", structure.GetRemainingResources().Select(kv => $"{kv.Key}x{kv.Value}"))} <<<");
                return false;
            }
            
            if (structure.State == StructureState.UnderConstruction)
            {
                bool completed = structure.AddBuildProgress(workAmount);
                if (completed)
                {
                    OnStructureCompleted?.Invoke(structure);
                }
                return true;
            }
            
            return false;
        }
        
        // ============================================
        // BUILD MODE
        // ============================================
        
        public void EnterBuildMode(StructureType type)
        {
            InBuildMode = true;
            SelectedStructure = type;
            PreviewPosition = null;
            System.Diagnostics.Debug.WriteLine($">>> BUILD MODE: {type} <<<");
        }
        
        public void ExitBuildMode()
        {
            InBuildMode = false;
            SelectedStructure = null;
            PreviewPosition = null;
            System.Diagnostics.Debug.WriteLine(">>> BUILD MODE OFF <<<");
        }
        
        public void UpdatePreview(Point position)
        {
            PreviewPosition = position;
        }
        
        // ============================================
        // DEFINITIONS
        // ============================================
        
        public StructureDefinition GetDefinition(StructureType type)
        {
            return _definitions.GetValueOrDefault(type);
        }
        
        public List<StructureDefinition> GetAllDefinitions()
        {
            return _definitions.Values.ToList();
        }
        
        public List<StructureDefinition> GetDefinitionsByCategory(StructureCategory category)
        {
            return _definitions.Values.Where(d => d.Category == category).ToList();
        }
        
        private void InitializeDefinitions()
        {
            _definitions = new Dictionary<StructureType, StructureDefinition>
            {
                // ========== WALLS ==========
                
                [StructureType.WoodWall] = new StructureDefinition
                {
                    Type = StructureType.WoodWall,
                    Name = "Wood Wall",
                    Description = "A simple wooden wall. Provides basic protection.",
                    Category = StructureCategory.Wall,
                    BlocksMovement = true,
                    BlocksVision = true,
                    MaxHealth = 100f,
                    CoverValue = 0.5f,
                    BuildCost = new Dictionary<string, int> { { "wood", 5 } },
                    BuildTime = 3f,
                    DisplayColor = new Color(139, 90, 43)  // Brown
                },
                
                [StructureType.StoneWall] = new StructureDefinition
                {
                    Type = StructureType.StoneWall,
                    Name = "Stone Wall",
                    Description = "A sturdy stone wall. Strong but slow to build.",
                    Category = StructureCategory.Wall,
                    BlocksMovement = true,
                    BlocksVision = true,
                    MaxHealth = 250f,
                    CoverValue = 0.75f,
                    BuildCost = new Dictionary<string, int> { { "stone", 8 } },
                    BuildTime = 8f,
                    DisplayColor = Color.Gray
                },
                
                [StructureType.MetalWall] = new StructureDefinition
                {
                    Type = StructureType.MetalWall,
                    Name = "Metal Wall",
                    Description = "Reinforced metal wall. Very strong.",
                    Category = StructureCategory.Wall,
                    BlocksMovement = true,
                    BlocksVision = true,
                    MaxHealth = 400f,
                    CoverValue = 0.9f,
                    BuildCost = new Dictionary<string, int> { { "metal", 10 } },
                    BuildTime = 12f,
                    BuildSkillRequired = 2,
                    DisplayColor = new Color(150, 150, 160)
                },
                
                // ========== DOORS ==========
                
                [StructureType.WoodDoor] = new StructureDefinition
                {
                    Type = StructureType.WoodDoor,
                    Name = "Wood Door",
                    Description = "A wooden door. Can be opened and closed.",
                    Category = StructureCategory.Door,
                    BlocksMovement = true,
                    BlocksVision = true,
                    CanBeOpened = true,
                    Interactable = true,
                    MaxHealth = 60f,
                    BuildCost = new Dictionary<string, int> { { "wood", 3 } },
                    BuildTime = 2f,
                    DisplayColor = new Color(160, 100, 50)
                },
                
                [StructureType.MetalDoor] = new StructureDefinition
                {
                    Type = StructureType.MetalDoor,
                    Name = "Metal Door",
                    Description = "A reinforced metal door. Very secure.",
                    Category = StructureCategory.Door,
                    BlocksMovement = true,
                    BlocksVision = true,
                    CanBeOpened = true,
                    Interactable = true,
                    MaxHealth = 200f,
                    BuildCost = new Dictionary<string, int> { { "metal", 5 } },
                    BuildTime = 5f,
                    BuildSkillRequired = 1,
                    DisplayColor = new Color(120, 120, 130)
                },
                
                // ========== FLOORS ==========
                
                [StructureType.WoodFloor] = new StructureDefinition
                {
                    Type = StructureType.WoodFloor,
                    Name = "Wood Floor",
                    Description = "Wooden flooring. Improves movement and comfort.",
                    Category = StructureCategory.Floor,
                    BlocksMovement = false,
                    BlocksVision = false,
                    MaxHealth = 50f,
                    BuildCost = new Dictionary<string, int> { { "wood", 2 } },
                    BuildTime = 1f,
                    DisplayColor = new Color(180, 130, 70)
                },
                
                [StructureType.StoneFloor] = new StructureDefinition
                {
                    Type = StructureType.StoneFloor,
                    Name = "Stone Floor",
                    Description = "Stone tile flooring. Durable and clean.",
                    Category = StructureCategory.Floor,
                    BlocksMovement = false,
                    BlocksVision = false,
                    MaxHealth = 100f,
                    BuildCost = new Dictionary<string, int> { { "stone", 3 } },
                    BuildTime = 2f,
                    DisplayColor = new Color(140, 140, 140)
                },
                
                // ========== FURNITURE ==========
                
                [StructureType.Bed] = new StructureDefinition
                {
                    Type = StructureType.Bed,
                    Name = "Bed",
                    Description = "A simple bed. Sleep here to restore rest quickly.",
                    Category = StructureCategory.Furniture,
                    BlocksMovement = true,
                    BlocksVision = false,
                    Interactable = true,
                    MaxHealth = 50f,
                    RestQuality = 1.0f,  // Full rest quality
                    BuildCost = new Dictionary<string, int> { { "wood", 4 }, { "cloth", 2 } },
                    BuildTime = 4f,
                    DisplayColor = new Color(200, 150, 150)
                },
                
                [StructureType.Campfire] = new StructureDefinition
                {
                    Type = StructureType.Campfire,
                    Name = "Campfire",
                    Description = "A warm campfire. Provides light and heat. Can cook food.",
                    Category = StructureCategory.Furniture,
                    BlocksMovement = true,
                    BlocksVision = false,
                    Interactable = true,
                    MaxHealth = 30f,
                    LightRadius = 5f,
                    WarmthRadius = 4f,
                    WarmthAmount = 20f,
                    BuildCost = new Dictionary<string, int> { { "wood", 3 } },
                    BuildTime = 1f,
                    DisplayColor = Color.OrangeRed
                },
                
                [StructureType.StorageBox] = new StructureDefinition
                {
                    Type = StructureType.StorageBox,
                    Name = "Storage Box",
                    Description = "A container for storing items. Holds up to 20 items.",
                    Category = StructureCategory.Furniture,
                    BlocksMovement = true,
                    BlocksVision = false,
                    Interactable = true,
                    MaxHealth = 40f,
                    StorageSlots = 20,
                    BuildCost = new Dictionary<string, int> { { "wood", 4 } },
                    BuildTime = 2f,
                    DisplayColor = new Color(120, 80, 40)
                },
                
                // ========== WORKSTATIONS ==========
                
                [StructureType.CraftingBench] = new StructureDefinition
                {
                    Type = StructureType.CraftingBench,
                    Name = "Crafting Bench",
                    Description = "A workbench for crafting items and equipment.",
                    Category = StructureCategory.Workstation,
                    BlocksMovement = true,
                    BlocksVision = false,
                    Interactable = true,
                    MaxHealth = 80f,
                    BuildCost = new Dictionary<string, int> { { "wood", 6 }, { "metal", 2 } },
                    BuildTime = 5f,
                    DisplayColor = new Color(100, 70, 40)
                },
                
                [StructureType.ResearchTable] = new StructureDefinition
                {
                    Type = StructureType.ResearchTable,
                    Name = "Research Table",
                    Description = "A table for conducting research and unlocking new technologies.",
                    Category = StructureCategory.Workstation,
                    BlocksMovement = true,
                    BlocksVision = false,
                    Interactable = true,
                    MaxHealth = 60f,
                    BuildCost = new Dictionary<string, int> { { "wood", 4 }, { "metal", 3 }, { "components", 2 } },
                    BuildTime = 8f,
                    BuildSkillRequired = 1,
                    DisplayColor = new Color(60, 60, 100)
                },
                
                [StructureType.CookingStation] = new StructureDefinition
                {
                    Type = StructureType.CookingStation,
                    Name = "Cooking Station",
                    Description = "A proper cooking area. Makes better meals than a campfire.",
                    Category = StructureCategory.Workstation,
                    BlocksMovement = true,
                    BlocksVision = false,
                    Interactable = true,
                    MaxHealth = 70f,
                    WarmthRadius = 2f,
                    WarmthAmount = 10f,
                    BuildCost = new Dictionary<string, int> { { "stone", 5 }, { "metal", 2 } },
                    BuildTime = 6f,
                    DisplayColor = new Color(80, 80, 80)
                },
                
                // ========== LIGHTING ==========
                
                [StructureType.Torch] = new StructureDefinition
                {
                    Type = StructureType.Torch,
                    Name = "Torch",
                    Description = "A simple torch. Provides light in dark areas.",
                    Category = StructureCategory.Light,
                    BlocksMovement = false,
                    BlocksVision = false,
                    MaxHealth = 20f,
                    LightRadius = 4f,
                    BuildCost = new Dictionary<string, int> { { "wood", 1 } },
                    BuildTime = 0.5f,
                    DisplayColor = Color.Yellow
                },
                
                // ========== DEFENSE ==========
                
                [StructureType.Barricade] = new StructureDefinition
                {
                    Type = StructureType.Barricade,
                    Name = "Barricade",
                    Description = "A quick defensive barrier. Slows enemies and provides cover.",
                    Category = StructureCategory.Defense,
                    BlocksMovement = true,
                    BlocksVision = false,
                    MaxHealth = 50f,
                    CoverValue = 0.4f,
                    BuildCost = new Dictionary<string, int> { { "wood", 2 } },
                    BuildTime = 1.5f,
                    DisplayColor = new Color(100, 70, 30)
                }
            };
        }
        
        // ============================================
        // DEBUG
        // ============================================
        
        public string GetStatusReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== BUILDING STATUS ===");
            report.AppendLine($"Total Structures: {_structures.Count}");
            report.AppendLine($"Build Mode: {(InBuildMode ? SelectedStructure.ToString() : "OFF")}");
            report.AppendLine();
            
            var grouped = _structures.Values.Distinct().GroupBy(s => s.State);
            foreach (var group in grouped)
            {
                report.AppendLine($"[{group.Key}]: {group.Count()}");
                foreach (var structure in group.Take(5))
                {
                    report.AppendLine($"  - {structure}");
                }
                if (group.Count() > 5) report.AppendLine($"  ... and {group.Count() - 5} more");
            }
            
            return report.ToString();
        }
    }
}
