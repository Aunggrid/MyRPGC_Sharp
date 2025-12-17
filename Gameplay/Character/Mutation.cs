// Gameplay/Character/Mutation.cs
// Mutation definitions and management system

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;

namespace MyRPG.Gameplay.Character
{
    // ============================================
    // MUTATION INSTANCE (on a character)
    // ============================================
    
    public class MutationInstance
    {
        public MutationType Type { get; set; }
        public int Level { get; set; } = 1;
        public int MaxLevel { get; set; }
        
        public MutationInstance(MutationType type, int maxLevel)
        {
            Type = type;
            MaxLevel = maxLevel;
        }
        
        public bool CanLevelUp => Level < MaxLevel;
        
        public override string ToString()
        {
            string levelStr = MaxLevel > 1 ? $" Lv.{Level}/{MaxLevel}" : "";
            return $"{Type}{levelStr}";
        }
    }
    
    // ============================================
    // MUTATION DEFINITION (static data)
    // ============================================
    
    public class MutationDefinition
    {
        public MutationType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public MutationCategory Category { get; set; }
        public int MaxLevel { get; set; } = 1;
        
        // Requirements
        public List<MutationType> Prerequisites { get; set; } = new List<MutationType>();
        public List<MutationType> Conflicts { get; set; } = new List<MutationType>();
        
        // Attribute requirements (mutation won't appear unless met)
        public Dictionary<AttributeType, int> AttributeRequirements { get; set; } = new Dictionary<AttributeType, int>();
        
        // Effects per level (scaled by level)
        public bool AddsBodyParts { get; set; } = false;
        public float SpeedBonusPerLevel { get; set; } = 0f;
        public float DamageBonusPerLevel { get; set; } = 0f;
        public float HealthBonusPerLevel { get; set; } = 0f;
        public float ResistancePerLevel { get; set; } = 0f;
        public float SightRangePerLevel { get; set; } = 0f;
        public float RegenPerLevel { get; set; } = 0f;
        
        // Special flags
        public bool GrantsNightVision { get; set; } = false;
        public bool GrantsWaterBreathing { get; set; } = false;
        public bool GrantsWallClimb { get; set; } = false;
        public bool GrantsFlight { get; set; } = false;
        public bool GrantsStealth { get; set; } = false;
        
        // Rarity (affects chance of appearing in random selection)
        public MutationRarity Rarity { get; set; } = MutationRarity.Common;
        
        /// <summary>
        /// Helper to add a single attribute requirement
        /// </summary>
        public MutationDefinition RequiresAttribute(AttributeType type, int minValue)
        {
            AttributeRequirements[type] = minValue;
            return this;
        }
    }
    
    public enum MutationRarity
    {
        Common,     // 60% of pool
        Uncommon,   // 25% of pool
        Rare,       // 12% of pool
        Legendary   // 3% of pool
    }
    
    // ============================================
    // MUTATION SYSTEM
    // ============================================
    
    public class MutationSystem
    {
        private Dictionary<MutationType, MutationDefinition> _definitions;
        private Random _random = new Random();
        
        public MutationSystem()
        {
            InitializeDefinitions();
        }
        
        // ============================================
        // RANDOM SELECTION (for level-up)
        // ============================================
        
        /// <summary>
        /// Get random mutation choices for level-up.
        /// Default 3 choices, can be modified by perks/buffs.
        /// </summary>
        public List<MutationDefinition> GetRandomChoices(
            List<MutationInstance> currentMutations, 
            Attributes attributes = null,
            int choiceCount = 3,
            bool isFreeChoice = false)
        {
            if (isFreeChoice)
            {
                // Free choice: return ALL available mutations
                return GetAvailableMutations(currentMutations, attributes);
            }
            
            var available = GetAvailableMutations(currentMutations, attributes);
            if (available.Count <= choiceCount)
            {
                return available;
            }
            
            // Weighted random selection based on rarity
            var selected = new List<MutationDefinition>();
            var pool = new List<MutationDefinition>(available);
            
            while (selected.Count < choiceCount && pool.Count > 0)
            {
                var choice = SelectWeightedRandom(pool);
                selected.Add(choice);
                pool.Remove(choice);
            }
            
            return selected;
        }
        
        /// <summary>
        /// Get all mutations the character can acquire or level up.
        /// Filters by current mutations, prerequisites, conflicts, AND attribute requirements.
        /// </summary>
        public List<MutationDefinition> GetAvailableMutations(List<MutationInstance> currentMutations, Attributes attributes = null)
        {
            var available = new List<MutationDefinition>();
            var currentTypes = currentMutations.Select(m => m.Type).ToHashSet();
            
            foreach (var def in _definitions.Values)
            {
                // Check if already maxed
                var existing = currentMutations.FirstOrDefault(m => m.Type == def.Type);
                if (existing != null && !existing.CanLevelUp)
                {
                    continue; // Already at max level
                }
                
                // Check prerequisites
                bool hasPrereqs = def.Prerequisites.All(p => currentTypes.Contains(p));
                if (!hasPrereqs) continue;
                
                // Check conflicts
                bool hasConflict = def.Conflicts.Any(c => currentTypes.Contains(c));
                if (hasConflict) continue;
                
                // Check attribute requirements
                if (attributes != null && def.AttributeRequirements.Count > 0)
                {
                    if (!attributes.MeetsRequirements(def.AttributeRequirements))
                    {
                        continue; // Doesn't meet attribute requirements
                    }
                }
                
                available.Add(def);
            }
            
            return available;
        }
        
        private MutationDefinition SelectWeightedRandom(List<MutationDefinition> pool)
        {
            // Build weighted list
            var weights = new List<(MutationDefinition def, float weight)>();
            float totalWeight = 0f;
            
            foreach (var def in pool)
            {
                float weight = def.Rarity switch
                {
                    MutationRarity.Common => 60f,
                    MutationRarity.Uncommon => 25f,
                    MutationRarity.Rare => 12f,
                    MutationRarity.Legendary => 3f,
                    _ => 30f
                };
                weights.Add((def, weight));
                totalWeight += weight;
            }
            
            // Random selection
            float roll = (float)_random.NextDouble() * totalWeight;
            float cumulative = 0f;
            
            foreach (var (def, weight) in weights)
            {
                cumulative += weight;
                if (roll <= cumulative)
                {
                    return def;
                }
            }
            
            return pool.Last();
        }
        
        // ============================================
        // APPLY MUTATION
        // ============================================
        
        /// <summary>
        /// Apply or level up a mutation on a character.
        /// Returns the mutation instance.
        /// </summary>
        public MutationInstance ApplyMutation(
            List<MutationInstance> characterMutations,
            Body characterBody,
            MutationType type)
        {
            var definition = GetDefinition(type);
            var existing = characterMutations.FirstOrDefault(m => m.Type == type);
            
            if (existing != null)
            {
                // Level up existing mutation
                if (existing.CanLevelUp)
                {
                    existing.Level++;
                    ApplyMutationEffects(characterBody, definition, existing.Level, true);
                    System.Diagnostics.Debug.WriteLine($">>> MUTATION LEVELED: {type} to Lv.{existing.Level} <<<");
                    return existing;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($">>> MUTATION ALREADY MAX: {type} <<<");
                    return null;
                }
            }
            else
            {
                // Add new mutation
                var newMutation = new MutationInstance(type, definition.MaxLevel);
                characterMutations.Add(newMutation);
                ApplyMutationEffects(characterBody, definition, 1, false);
                System.Diagnostics.Debug.WriteLine($">>> MUTATION ACQUIRED: {type} <<<");
                return newMutation;
            }
        }
        
        private void ApplyMutationEffects(Body body, MutationDefinition def, int level, bool isLevelUp)
        {
            // Add body parts (only on first acquisition)
            if (def.AddsBodyParts && !isLevelUp)
            {
                body.ApplyMutation(def.Type, level);
            }
            
            // Other effects are calculated dynamically based on current mutations
            // (handled by CharacterStats)
        }
        
        // ============================================
        // STAT CALCULATIONS
        // ============================================
        
        /// <summary>
        /// Calculate total stat bonuses from all mutations.
        /// </summary>
        public MutationStatBonuses CalculateBonuses(List<MutationInstance> mutations)
        {
            var bonuses = new MutationStatBonuses();
            
            foreach (var mutation in mutations)
            {
                var def = GetDefinition(mutation.Type);
                int level = mutation.Level;
                
                bonuses.SpeedBonus += def.SpeedBonusPerLevel * level;
                bonuses.DamageBonus += def.DamageBonusPerLevel * level;
                bonuses.HealthBonus += def.HealthBonusPerLevel * level;
                bonuses.ResistanceBonus += def.ResistancePerLevel * level;
                bonuses.SightRangeBonus += def.SightRangePerLevel * level;
                bonuses.RegenBonus += def.RegenPerLevel * level;
                
                // Special abilities
                if (def.GrantsNightVision) bonuses.HasNightVision = true;
                if (def.GrantsWaterBreathing) bonuses.HasWaterBreathing = true;
                if (def.GrantsWallClimb) bonuses.HasWallClimb = true;
                if (def.GrantsFlight) bonuses.HasFlight = true;
                if (def.GrantsStealth) bonuses.HasStealth = true;
            }
            
            return bonuses;
        }
        
        // ============================================
        // DEFINITIONS
        // ============================================
        
        public MutationDefinition GetDefinition(MutationType type)
        {
            return _definitions.TryGetValue(type, out var def) ? def : null;
        }
        
        private void InitializeDefinitions()
        {
            _definitions = new Dictionary<MutationType, MutationDefinition>
            {
                // ========== PHYSICAL - BODY MODS ==========
                
                [MutationType.ExtraArms] = new MutationDefinition
                {
                    Type = MutationType.ExtraArms,
                    Name = "Extra Arms",
                    Description = "Grow an additional pair of arms. [Requires STR 7]",
                    Category = MutationCategory.Physical,
                    MaxLevel = 1,
                    AddsBodyParts = true,
                    Rarity = MutationRarity.Rare,
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.STR, 7 } }
                },
                
                [MutationType.ExtraEyes] = new MutationDefinition
                {
                    Type = MutationType.ExtraEyes,
                    Name = "Extra Eyes",
                    Description = "Grow additional eyes. Each level adds one eye. [Requires PER 6]",
                    Category = MutationCategory.Sensory,
                    MaxLevel = 3,
                    AddsBodyParts = true,
                    SightRangePerLevel = 5f,
                    Rarity = MutationRarity.Uncommon,
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.PER, 6 } }
                },
                
                [MutationType.Claws] = new MutationDefinition
                {
                    Type = MutationType.Claws,
                    Name = "Claws",
                    Description = "Hands become clawed. Natural weapons that scale with level.",
                    Category = MutationCategory.Physical,
                    MaxLevel = 5,
                    AddsBodyParts = false,
                    DamageBonusPerLevel = 2f,
                    Rarity = MutationRarity.Common
                    // No requirements - basic mutation
                },
                
                [MutationType.ThickHide] = new MutationDefinition
                {
                    Type = MutationType.ThickHide,
                    Name = "Thick Hide",
                    Description = "Skin toughens into natural armor.",
                    Category = MutationCategory.Physical,
                    MaxLevel = 5,
                    ResistancePerLevel = 0.05f,
                    Rarity = MutationRarity.Common
                    // No requirements - basic mutation
                },
                
                [MutationType.Tail] = new MutationDefinition
                {
                    Type = MutationType.Tail,
                    Name = "Tail",
                    Description = "Grow a prehensile tail. Improves balance. [Requires AGI 5]",
                    Category = MutationCategory.Physical,
                    MaxLevel = 1,
                    AddsBodyParts = true,
                    SpeedBonusPerLevel = 0.05f,
                    Rarity = MutationRarity.Uncommon,
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.AGI, 5 } }
                },
                
                [MutationType.Wings] = new MutationDefinition
                {
                    Type = MutationType.Wings,
                    Name = "Wings",
                    Description = "Grow wings capable of limited flight. [Requires AGI 8, END 6]",
                    Category = MutationCategory.Movement,
                    MaxLevel = 1,
                    AddsBodyParts = true,
                    GrantsFlight = true,
                    Rarity = MutationRarity.Legendary,
                    Prerequisites = new List<MutationType> { MutationType.ThickHide },
                    AttributeRequirements = new Dictionary<AttributeType, int> 
                    { 
                        { AttributeType.AGI, 8 }, 
                        { AttributeType.END, 6 } 
                    }
                },
                
                // ========== PHYSICAL - ENHANCEMENTS ==========
                
                [MutationType.NightVision] = new MutationDefinition
                {
                    Type = MutationType.NightVision,
                    Name = "Night Vision",
                    Description = "Eyes adapt to see in darkness.",
                    Category = MutationCategory.Sensory,
                    MaxLevel = 3,
                    GrantsNightVision = true,
                    SightRangePerLevel = 10f,
                    Rarity = MutationRarity.Common
                    // No requirements - basic mutation
                },
                
                [MutationType.Regeneration] = new MutationDefinition
                {
                    Type = MutationType.Regeneration,
                    Name = "Regeneration",
                    Description = "Body heals rapidly over time. [Requires END 6]",
                    Category = MutationCategory.Physical,
                    MaxLevel = 5,
                    RegenPerLevel = 1f,
                    Rarity = MutationRarity.Uncommon,
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.END, 6 } }
                },
                
                [MutationType.ToxinFilter] = new MutationDefinition
                {
                    Type = MutationType.ToxinFilter,
                    Name = "Toxin Filter",
                    Description = "Body becomes resistant to poisons and radiation. [Requires END 5]",
                    Category = MutationCategory.Physical,
                    MaxLevel = 3,
                    ResistancePerLevel = 0.15f,
                    Rarity = MutationRarity.Uncommon,
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.END, 5 } }
                },
                
                [MutationType.AcidBlood] = new MutationDefinition
                {
                    Type = MutationType.AcidBlood,
                    Name = "Acid Blood",
                    Description = "Blood becomes corrosive. Damages attackers. [Requires STR 6, END 5]",
                    Category = MutationCategory.Physical,
                    MaxLevel = 3,
                    DamageBonusPerLevel = 3f,
                    Rarity = MutationRarity.Rare,
                    AttributeRequirements = new Dictionary<AttributeType, int> 
                    { 
                        { AttributeType.STR, 6 }, 
                        { AttributeType.END, 5 } 
                    }
                },
                
                [MutationType.Camouflage] = new MutationDefinition
                {
                    Type = MutationType.Camouflage,
                    Name = "Camouflage",
                    Description = "Skin can change color to blend with surroundings. [Requires AGI 6]",
                    Category = MutationCategory.Utility,
                    MaxLevel = 3,
                    GrantsStealth = true,
                    Rarity = MutationRarity.Uncommon,
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.AGI, 6 } }
                },
                
                // ========== MENTAL ==========
                
                [MutationType.ComplexBrain] = new MutationDefinition
                {
                    Type = MutationType.ComplexBrain,
                    Name = "Complex Brain",
                    Description = "Brain develops additional folds. Faster research. [Requires INT 6]",
                    Category = MutationCategory.Mental,
                    MaxLevel = 3,
                    Rarity = MutationRarity.Uncommon,
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.INT, 6 } }
                },
                
                [MutationType.Telepathy] = new MutationDefinition
                {
                    Type = MutationType.Telepathy,
                    Name = "Telepathy",
                    Description = "Can sense nearby minds and communicate without speech. [Requires WIL 7, INT 6]",
                    Category = MutationCategory.Mental,
                    MaxLevel = 1,
                    SightRangePerLevel = 15f,
                    Rarity = MutationRarity.Rare,
                    Prerequisites = new List<MutationType> { MutationType.ComplexBrain },
                    AttributeRequirements = new Dictionary<AttributeType, int> 
                    { 
                        { AttributeType.WIL, 7 }, 
                        { AttributeType.INT, 6 } 
                    }
                },
                
                [MutationType.PrecognitionMinor] = new MutationDefinition
                {
                    Type = MutationType.PrecognitionMinor,
                    Name = "Minor Precognition",
                    Description = "Brief flashes of the near future. Initiative bonus. [Requires WIL 8, PER 6]",
                    Category = MutationCategory.Mental,
                    MaxLevel = 1,
                    SpeedBonusPerLevel = 0.2f,
                    Rarity = MutationRarity.Legendary,
                    Prerequisites = new List<MutationType> { MutationType.ComplexBrain },
                    AttributeRequirements = new Dictionary<AttributeType, int> 
                    { 
                        { AttributeType.WIL, 8 }, 
                        { AttributeType.PER, 6 } 
                    }
                },
                
                // ========== MOVEMENT ==========
                
                [MutationType.TreeJump] = new MutationDefinition
                {
                    Type = MutationType.TreeJump,
                    Name = "Tree Jump",
                    Description = "Can leap between trees. Evasion bonus in forests. [Requires AGI 6]",
                    Category = MutationCategory.Movement,
                    MaxLevel = 1,
                    SpeedBonusPerLevel = 0.15f,
                    Rarity = MutationRarity.Uncommon,
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.AGI, 6 } }
                },
                
                [MutationType.WallCrawl] = new MutationDefinition
                {
                    Type = MutationType.WallCrawl,
                    Name = "Wall Crawl",
                    Description = "Can climb vertical surfaces and ceilings. [Requires AGI 7, STR 5]",
                    Category = MutationCategory.Movement,
                    MaxLevel = 1,
                    GrantsWallClimb = true,
                    Rarity = MutationRarity.Rare,
                    AttributeRequirements = new Dictionary<AttributeType, int> 
                    { 
                        { AttributeType.AGI, 7 }, 
                        { AttributeType.STR, 5 } 
                    }
                },
                
                [MutationType.Burrowing] = new MutationDefinition
                {
                    Type = MutationType.Burrowing,
                    Name = "Burrowing",
                    Description = "Can dig through soft ground. Ambush capability. [Requires STR 7]",
                    Category = MutationCategory.Movement,
                    MaxLevel = 1,
                    Rarity = MutationRarity.Rare,
                    Conflicts = new List<MutationType> { MutationType.Wings },
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.STR, 7 } }
                },
                
                [MutationType.AquaticAdaptation] = new MutationDefinition
                {
                    Type = MutationType.AquaticAdaptation,
                    Name = "Aquatic Adaptation",
                    Description = "Can breathe underwater and swim rapidly. [Requires END 6]",
                    Category = MutationCategory.Movement,
                    MaxLevel = 1,
                    GrantsWaterBreathing = true,
                    SpeedBonusPerLevel = 0.5f,
                    Rarity = MutationRarity.Uncommon,
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.END, 6 } }
                },
                
                // ========== UTILITY ==========
                
                [MutationType.PhotosynthesisSkin] = new MutationDefinition
                {
                    Type = MutationType.PhotosynthesisSkin,
                    Name = "Photosynthesis Skin",
                    Description = "Skin absorbs sunlight for energy. Reduced hunger. [Requires END 5]",
                    Category = MutationCategory.Utility,
                    MaxLevel = 3,
                    Rarity = MutationRarity.Uncommon,
                    Conflicts = new List<MutationType> { MutationType.NightVision },
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.END, 5 } }
                },
                
                [MutationType.EchoLocation] = new MutationDefinition
                {
                    Type = MutationType.EchoLocation,
                    Name = "Echo Location",
                    Description = "Can sense surroundings through sound. Detect hidden enemies. [Requires PER 7]",
                    Category = MutationCategory.Sensory,
                    MaxLevel = 1,
                    SightRangePerLevel = 10f,
                    Rarity = MutationRarity.Uncommon,
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.PER, 7 } }
                },
                
                [MutationType.ThermalSense] = new MutationDefinition
                {
                    Type = MutationType.ThermalSense,
                    Name = "Thermal Sense",
                    Description = "Can see heat signatures. Detect warm-blooded creatures. [Requires PER 6]",
                    Category = MutationCategory.Sensory,
                    MaxLevel = 1,
                    Rarity = MutationRarity.Uncommon,
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.PER, 6 } }
                },
                
                // ========== DARK/WEIRD ==========
                
                [MutationType.VoidTouch] = new MutationDefinition
                {
                    Type = MutationType.VoidTouch,
                    Name = "Void Touch",
                    Description = "Connection to the dark energy. Enhanced Dark Science. [Requires WIL 6]",
                    Category = MutationCategory.Dark,
                    MaxLevel = 3,
                    DamageBonusPerLevel = 5f,
                    Rarity = MutationRarity.Rare,
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.WIL, 6 } }
                },
                
                [MutationType.CorpseEater] = new MutationDefinition
                {
                    Type = MutationType.CorpseEater,
                    Name = "Corpse Eater",
                    Description = "Can safely consume corpses for nutrition.",
                    Category = MutationCategory.Dark,
                    MaxLevel = 1,
                    Rarity = MutationRarity.Common
                    // No requirements - basic mutation
                },
                
                [MutationType.FearAura] = new MutationDefinition
                {
                    Type = MutationType.FearAura,
                    Name = "Fear Aura",
                    Description = "Enemies may flee in terror when nearby. [Requires WIL 8]",
                    Category = MutationCategory.Dark,
                    MaxLevel = 3,
                    Rarity = MutationRarity.Rare,
                    Prerequisites = new List<MutationType> { MutationType.VoidTouch },
                    AttributeRequirements = new Dictionary<AttributeType, int> { { AttributeType.WIL, 8 } }
                },
                
                [MutationType.UnstableForm] = new MutationDefinition
                {
                    Type = MutationType.UnstableForm,
                    Name = "Unstable Form",
                    Description = "Body is in constant flux. Random beneficial and detrimental effects.",
                    Category = MutationCategory.Dark,
                    MaxLevel = 1,
                    Rarity = MutationRarity.Legendary
                }
            };
        }
    }
    
    // ============================================
    // STAT BONUSES CONTAINER
    // ============================================
    
    public class MutationStatBonuses
    {
        public float SpeedBonus { get; set; } = 0f;
        public float DamageBonus { get; set; } = 0f;
        public float HealthBonus { get; set; } = 0f;
        public float ResistanceBonus { get; set; } = 0f;
        public float SightRangeBonus { get; set; } = 0f;
        public float RegenBonus { get; set; } = 0f;
        
        // Special abilities
        public bool HasNightVision { get; set; } = false;
        public bool HasWaterBreathing { get; set; } = false;
        public bool HasWallClimb { get; set; } = false;
        public bool HasFlight { get; set; } = false;
        public bool HasStealth { get; set; } = false;
    }
}
