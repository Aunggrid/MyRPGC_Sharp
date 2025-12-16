// Gameplay/Character/Trait.cs
// Trait definitions and management - permanent character attributes

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;

namespace MyRPG.Gameplay.Character
{
    // ============================================
    // TRAIT INSTANCE
    // ============================================
    
    public class TraitInstance
    {
        public TraitType Type { get; set; }
        public string Source { get; set; } // "Creation", "Quest:xyz", etc.
        
        public TraitInstance(TraitType type, string source = "Creation")
        {
            Type = type;
            Source = source;
        }
        
        public override string ToString() => Type.ToString();
    }
    
    // ============================================
    // TRAIT DEFINITION
    // ============================================
    
    public class TraitDefinition
    {
        public TraitType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public TraitCategory Category { get; set; }
        
        // Point cost (negative = gives points, positive = costs points)
        public int PointCost { get; set; } = 0;
        
        // Conflicts with other traits
        public List<TraitType> Conflicts { get; set; } = new List<TraitType>();
        
        // Stat modifiers
        public float HealthModifier { get; set; } = 1.0f;
        public float SpeedModifier { get; set; } = 1.0f;
        public float DamageModifier { get; set; } = 1.0f;
        public float AccuracyModifier { get; set; } = 1.0f;
        public float XPModifier { get; set; } = 1.0f;
        public float ResearchModifier { get; set; } = 1.0f;
        public float TradeModifier { get; set; } = 1.0f;        // Price multiplier (lower = better)
        public float HungerRateModifier { get; set; } = 1.0f;   // Higher = get hungry faster
        public float HealingModifier { get; set; } = 1.0f;
        
        // Social modifiers
        public float DisguiseBonus { get; set; } = 0f;          // Ability to pass as human
        public float IntimidationBonus { get; set; } = 0f;
        public float PersuasionBonus { get; set; } = 0f;
        
        // Special flags
        public bool CanSpeak { get; set; } = true;
        public bool CanDisguise { get; set; } = true;
        public bool IsNightPerson { get; set; } = false;
        public bool CanEatCorpses { get; set; } = false;
        public bool IsPacifist { get; set; } = false;
        
        // Backstory specific
        public bool IsBackstory { get; set; } = false;
        public List<string> StartingItems { get; set; } = new List<string>();
    }
    
    // ============================================
    // TRAIT SYSTEM
    // ============================================
    
    public class TraitSystem
    {
        private Dictionary<TraitType, TraitDefinition> _definitions;
        private Random _random = new Random();
        
        public TraitSystem()
        {
            InitializeDefinitions();
        }
        
        // ============================================
        // CHARACTER CREATION
        // ============================================
        
        /// <summary>
        /// Generate a random backstory with associated traits.
        /// Returns the backstory trait and any bonus/penalty traits.
        /// </summary>
        public CharacterBuild GenerateRandomBuild(int basePoints = 4)
        {
            var build = new CharacterBuild { MutationPoints = basePoints };
            
            // Pick random backstory
            var backstories = _definitions.Values.Where(d => d.IsBackstory).ToList();
            var backstory = backstories[_random.Next(backstories.Count)];
            build.Backstory = backstory.Type;
            build.MutationPoints += backstory.PointCost; // Backstory adjusts points
            
            // Roll 1-3 random traits (mix of good and bad)
            int traitCount = _random.Next(1, 4);
            var availableTraits = GetAvailableTraits(new List<TraitType> { backstory.Type });
            
            for (int i = 0; i < traitCount && availableTraits.Count > 0; i++)
            {
                var trait = availableTraits[_random.Next(availableTraits.Count)];
                
                // Check point limits (2-6 range)
                int newPoints = build.MutationPoints + trait.PointCost;
                if (newPoints < 2 || newPoints > 6)
                {
                    // Skip this trait, would break limits
                    availableTraits.Remove(trait);
                    i--; // Try again
                    continue;
                }
                
                build.Traits.Add(trait.Type);
                build.MutationPoints = newPoints;
                
                // Remove this trait and conflicts from pool
                availableTraits.Remove(trait);
                availableTraits.RemoveAll(t => trait.Conflicts.Contains(t.Type));
            }
            
            // Clamp final points just in case
            build.MutationPoints = Math.Clamp(build.MutationPoints, 2, 6);
            
            return build;
        }
        
        /// <summary>
        /// Get traits available given current selections.
        /// </summary>
        public List<TraitDefinition> GetAvailableTraits(List<TraitType> currentTraits)
        {
            var selected = currentTraits.ToHashSet();
            
            return _definitions.Values
                .Where(d => !d.IsBackstory)  // Backstories handled separately
                .Where(d => !selected.Contains(d.Type))  // Not already selected
                .Where(d => !d.Conflicts.Any(c => selected.Contains(c)))  // No conflicts
                .ToList();
        }
        
        // ============================================
        // STAT CALCULATIONS
        // ============================================
        
        public TraitStatBonuses CalculateBonuses(List<TraitType> traits)
        {
            var bonuses = new TraitStatBonuses();
            
            foreach (var traitType in traits)
            {
                var def = GetDefinition(traitType);
                if (def == null) continue;
                
                // Multiplicative modifiers
                bonuses.HealthModifier *= def.HealthModifier;
                bonuses.SpeedModifier *= def.SpeedModifier;
                bonuses.DamageModifier *= def.DamageModifier;
                bonuses.AccuracyModifier *= def.AccuracyModifier;
                bonuses.XPModifier *= def.XPModifier;
                bonuses.ResearchModifier *= def.ResearchModifier;
                bonuses.TradeModifier *= def.TradeModifier;
                bonuses.HungerRateModifier *= def.HungerRateModifier;
                bonuses.HealingModifier *= def.HealingModifier;
                
                // Additive bonuses
                bonuses.DisguiseBonus += def.DisguiseBonus;
                bonuses.IntimidationBonus += def.IntimidationBonus;
                bonuses.PersuasionBonus += def.PersuasionBonus;
                
                // Flags (any trait setting these overrides)
                if (!def.CanSpeak) bonuses.CanSpeak = false;
                if (!def.CanDisguise) bonuses.CanDisguise = false;
                if (def.IsNightPerson) bonuses.IsNightPerson = true;
                if (def.CanEatCorpses) bonuses.CanEatCorpses = true;
                if (def.IsPacifist) bonuses.IsPacifist = true;
            }
            
            return bonuses;
        }
        
        // ============================================
        // HELPERS
        // ============================================
        
        public TraitDefinition GetDefinition(TraitType type)
        {
            return _definitions.TryGetValue(type, out var def) ? def : null;
        }
        
        public List<TraitDefinition> GetAllBackstories()
        {
            return _definitions.Values.Where(d => d.IsBackstory).ToList();
        }
        
        // ============================================
        // DEFINITIONS
        // ============================================
        
        private void InitializeDefinitions()
        {
            _definitions = new Dictionary<TraitType, TraitDefinition>
            {
                // ========== BACKSTORIES ==========
                
                [TraitType.LabEscapee] = new TraitDefinition
                {
                    Type = TraitType.LabEscapee,
                    Name = "Lab Escapee",
                    Description = "Escaped from a research facility. Start with one implant, but hunters are after you.",
                    Category = TraitCategory.Backstory,
                    IsBackstory = true,
                    PointCost = 0, // Neutral
                    ResearchModifier = 1.1f,
                    StartingItems = new List<string> { "BasicImplant", "LabCoat" }
                },
                
                [TraitType.WastelandBorn] = new TraitDefinition
                {
                    Type = TraitType.WastelandBorn,
                    Name = "Wasteland Born",
                    Description = "Born and raised in the wasteland. Survival expert but wary of technology.",
                    Category = TraitCategory.Backstory,
                    IsBackstory = true,
                    PointCost = 0,
                    HungerRateModifier = 0.85f, // 15% less hunger
                    ResearchModifier = 0.9f,
                    StartingItems = new List<string> { "SurvivalKit", "WastelandMap" }
                },
                
                [TraitType.FailedExperiment] = new TraitDefinition
                {
                    Type = TraitType.FailedExperiment,
                    Name = "Failed Experiment",
                    Description = "Something went wrong during your creation. Unstable but potentially powerful.",
                    Category = TraitCategory.Backstory,
                    IsBackstory = true,
                    PointCost = 1, // Gives extra point due to instability
                    HealthModifier = 0.9f,
                    StartingItems = new List<string> { "UnstableMutagen" }
                },
                
                [TraitType.TribalMutant] = new TraitDefinition
                {
                    Type = TraitType.TribalMutant,
                    Name = "Tribal Mutant",
                    Description = "Raised by a mutant tribe. Strong warrior, suspicious of outsiders.",
                    Category = TraitCategory.Backstory,
                    IsBackstory = true,
                    PointCost = 0,
                    DamageModifier = 1.1f,
                    TradeModifier = 1.15f, // Worse prices
                    StartingItems = new List<string> { "TribalWeapon", "WarPaint" }
                },
                
                [TraitType.UrbanSurvivor] = new TraitDefinition
                {
                    Type = TraitType.UrbanSurvivor,
                    Name = "Urban Survivor",
                    Description = "Survived in the ruins of cities. Good at scavenging and staying hidden.",
                    Category = TraitCategory.Backstory,
                    IsBackstory = true,
                    PointCost = 0,
                    TradeModifier = 0.9f, // Better prices
                    DisguiseBonus = 0.1f,
                    StartingItems = new List<string> { "Lockpick", "TatteredCloak" }
                },
                
                // ========== COMMUNICATION ==========
                
                [TraitType.CantSpeak] = new TraitDefinition
                {
                    Type = TraitType.CantSpeak,
                    Name = "Mute",
                    Description = "Cannot speak. Must rely on gestures or writing. Trading and persuasion severely limited.",
                    Category = TraitCategory.Communication,
                    PointCost = -2, // Gives 2 points
                    CanSpeak = false,
                    PersuasionBonus = -0.5f,
                    TradeModifier = 1.3f,
                    Conflicts = new List<TraitType> { TraitType.Eloquent }
                },
                
                [TraitType.Eloquent] = new TraitDefinition
                {
                    Type = TraitType.Eloquent,
                    Name = "Eloquent",
                    Description = "Silver-tongued speaker. Better prices and more dialogue options.",
                    Category = TraitCategory.Communication,
                    PointCost = 1, // Costs 1 point
                    PersuasionBonus = 0.3f,
                    TradeModifier = 0.85f,
                    Conflicts = new List<TraitType> { TraitType.CantSpeak }
                },
                
                [TraitType.Intimidating] = new TraitDefinition
                {
                    Type = TraitType.Intimidating,
                    Name = "Intimidating",
                    Description = "Your presence frightens others. Can threaten for better outcomes, but makes friends wary.",
                    Category = TraitCategory.Communication,
                    PointCost = 0,
                    IntimidationBonus = 0.3f,
                    PersuasionBonus = -0.1f
                },
                
                // ========== PHYSICAL ==========
                
                [TraitType.Frail] = new TraitDefinition
                {
                    Type = TraitType.Frail,
                    Name = "Frail",
                    Description = "Weak constitution. Less health but faster movement.",
                    Category = TraitCategory.Physical,
                    PointCost = -1,
                    HealthModifier = 0.75f,
                    SpeedModifier = 1.1f,
                    Conflicts = new List<TraitType> { TraitType.Tough }
                },
                
                [TraitType.Tough] = new TraitDefinition
                {
                    Type = TraitType.Tough,
                    Name = "Tough",
                    Description = "Hardy constitution. More health but slower.",
                    Category = TraitCategory.Physical,
                    PointCost = 1,
                    HealthModifier = 1.25f,
                    SpeedModifier = 0.95f,
                    Conflicts = new List<TraitType> { TraitType.Frail }
                },
                
                [TraitType.FastMetabolism] = new TraitDefinition
                {
                    Type = TraitType.FastMetabolism,
                    Name = "Fast Metabolism",
                    Description = "Heal quickly but need more food.",
                    Category = TraitCategory.Physical,
                    PointCost = 0, // Balanced
                    HealingModifier = 1.3f,
                    HungerRateModifier = 1.3f,
                    Conflicts = new List<TraitType> { TraitType.SlowMetabolism }
                },
                
                [TraitType.SlowMetabolism] = new TraitDefinition
                {
                    Type = TraitType.SlowMetabolism,
                    Name = "Slow Metabolism",
                    Description = "Need less food but heal slowly.",
                    Category = TraitCategory.Physical,
                    PointCost = 0,
                    HealingModifier = 0.7f,
                    HungerRateModifier = 0.7f,
                    Conflicts = new List<TraitType> { TraitType.FastMetabolism }
                },
                
                // ========== MENTAL ==========
                
                [TraitType.Paranoid] = new TraitDefinition
                {
                    Type = TraitType.Paranoid,
                    Name = "Paranoid",
                    Description = "Always watching. Detect ambushes but stress more easily.",
                    Category = TraitCategory.Mental,
                    PointCost = 0,
                    // Ambush detection handled in combat system
                },
                
                [TraitType.Focused] = new TraitDefinition
                {
                    Type = TraitType.Focused,
                    Name = "Focused",
                    Description = "Intense concentration. Better research speed.",
                    Category = TraitCategory.Mental,
                    PointCost = 1,
                    ResearchModifier = 1.2f,
                    AccuracyModifier = 1.1f
                },
                
                [TraitType.QuickLearner] = new TraitDefinition
                {
                    Type = TraitType.QuickLearner,
                    Name = "Quick Learner",
                    Description = "Learn faster from experience.",
                    Category = TraitCategory.Mental,
                    PointCost = 2,
                    XPModifier = 1.25f,
                    Conflicts = new List<TraitType> { TraitType.SlowLearner }
                },
                
                [TraitType.SlowLearner] = new TraitDefinition
                {
                    Type = TraitType.SlowLearner,
                    Name = "Slow Learner",
                    Description = "Takes longer to learn new things.",
                    Category = TraitCategory.Mental,
                    PointCost = -1,
                    XPModifier = 0.8f,
                    Conflicts = new List<TraitType> { TraitType.QuickLearner }
                },
                
                // ========== SOCIAL ==========
                
                [TraitType.Disguised] = new TraitDefinition
                {
                    Type = TraitType.Disguised,
                    Name = "Passable",
                    Description = "Can pass as human with some effort. Better faction relations.",
                    Category = TraitCategory.Social,
                    PointCost = 2,
                    CanDisguise = true,
                    DisguiseBonus = 0.3f,
                    Conflicts = new List<TraitType> { TraitType.ObviousMutant }
                },
                
                [TraitType.ObviousMutant] = new TraitDefinition
                {
                    Type = TraitType.ObviousMutant,
                    Name = "Obvious Mutant",
                    Description = "Clearly inhuman. Cannot disguise, higher aggression from humans.",
                    Category = TraitCategory.Social,
                    PointCost = -1,
                    CanDisguise = false,
                    TradeModifier = 1.2f,
                    IntimidationBonus = 0.2f,
                    Conflicts = new List<TraitType> { TraitType.Disguised }
                },
                
                [TraitType.Outcast] = new TraitDefinition
                {
                    Type = TraitType.Outcast,
                    Name = "Outcast",
                    Description = "Rejected by all. Mutant factions trust you more, humans less.",
                    Category = TraitCategory.Social,
                    PointCost = 0
                    // Faction modifiers handled in faction system
                },
                
                // ========== QUIRKS ==========
                
                [TraitType.Cannibal] = new TraitDefinition
                {
                    Type = TraitType.Cannibal,
                    Name = "Cannibal",
                    Description = "Can eat human corpses without penalty. Others find this disturbing.",
                    Category = TraitCategory.Quirk,
                    PointCost = -1,
                    CanEatCorpses = true
                },
                
                [TraitType.Pacifist] = new TraitDefinition
                {
                    Type = TraitType.Pacifist,
                    Name = "Pacifist",
                    Description = "Abhors violence. Worse at combat but better at diplomacy.",
                    Category = TraitCategory.Quirk,
                    PointCost = 0,
                    IsPacifist = true,
                    DamageModifier = 0.7f,
                    PersuasionBonus = 0.3f,
                    Conflicts = new List<TraitType> { TraitType.Bloodlust }
                },
                
                [TraitType.Bloodlust] = new TraitDefinition
                {
                    Type = TraitType.Bloodlust,
                    Name = "Bloodlust",
                    Description = "Loves violence. Better at combat, worse at diplomacy.",
                    Category = TraitCategory.Quirk,
                    PointCost = 0,
                    DamageModifier = 1.2f,
                    PersuasionBonus = -0.2f,
                    IntimidationBonus = 0.2f,
                    Conflicts = new List<TraitType> { TraitType.Pacifist }
                },
                
                [TraitType.NightOwl] = new TraitDefinition
                {
                    Type = TraitType.NightOwl,
                    Name = "Night Owl",
                    Description = "More effective at night, sluggish during the day.",
                    Category = TraitCategory.Quirk,
                    PointCost = 0,
                    IsNightPerson = true
                    // Day/night modifiers handled in time system
                }
            };
        }
    }
    
    // ============================================
    // HELPER CLASSES
    // ============================================
    
    public class CharacterBuild
    {
        public TraitType Backstory { get; set; }
        public List<TraitType> Traits { get; set; } = new List<TraitType>();
        public int MutationPoints { get; set; } = 4;
        
        public List<TraitType> AllTraits => new List<TraitType> { Backstory }.Concat(Traits).ToList();
        
        public override string ToString()
        {
            var traitStr = Traits.Any() ? string.Join(", ", Traits) : "None";
            return $"[{Backstory}] Traits: {traitStr} | Points: {MutationPoints}";
        }
    }
    
    public class TraitStatBonuses
    {
        // Multiplicative
        public float HealthModifier { get; set; } = 1.0f;
        public float SpeedModifier { get; set; } = 1.0f;
        public float DamageModifier { get; set; } = 1.0f;
        public float AccuracyModifier { get; set; } = 1.0f;
        public float XPModifier { get; set; } = 1.0f;
        public float ResearchModifier { get; set; } = 1.0f;
        public float TradeModifier { get; set; } = 1.0f;
        public float HungerRateModifier { get; set; } = 1.0f;
        public float HealingModifier { get; set; } = 1.0f;
        
        // Additive
        public float DisguiseBonus { get; set; } = 0f;
        public float IntimidationBonus { get; set; } = 0f;
        public float PersuasionBonus { get; set; } = 0f;
        
        // Flags
        public bool CanSpeak { get; set; } = true;
        public bool CanDisguise { get; set; } = true;
        public bool IsNightPerson { get; set; } = false;
        public bool CanEatCorpses { get; set; } = false;
        public bool IsPacifist { get; set; } = false;
    }
}
