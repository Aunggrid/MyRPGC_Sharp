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
        
        // Point cost (positive = costs points (good trait), negative = gives points (bad trait))
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
        
        // Combat bonuses (additive)
        public int ActionPointBonus { get; set; } = 0;
        public int MovementPointBonus { get; set; } = 0;
        public int ReservedAPBonus { get; set; } = 0;          // Bonus to max reserved AP
        
        // Esper bonuses (additive)
        public int EsperPointBonus { get; set; } = 0;
        public float EsperPowerBonus { get; set; } = 0f;
        
        // Research/INT bonuses
        public int ResearchSlotBonus { get; set; } = 0;
        public int RecipeUnlockBonus { get; set; } = 0;
        
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
        public bool HasPsychicPotential { get; set; } = false;
        
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
            
            // Randomize attributes
            build.Attributes.Randomize(_random);
            
            // Pick random backstory
            var backstories = _definitions.Values.Where(d => d.IsBackstory).ToList();
            var backstory = backstories[_random.Next(backstories.Count)];
            build.Backstory = backstory.Type;
            
            // FIX: SUBTRACT point cost (positive cost = costs points, negative = gives points)
            build.MutationPoints -= backstory.PointCost;
            
            // Roll 1-3 random traits (mix of good and bad)
            int traitCount = _random.Next(1, 4);
            var availableTraits = GetAvailableTraits(new List<TraitType> { backstory.Type });
            
            for (int i = 0; i < traitCount && availableTraits.Count > 0; i++)
            {
                var trait = availableTraits[_random.Next(availableTraits.Count)];
                
                // FIX: Check point limits with SUBTRACTION (2-6 range)
                int newPoints = build.MutationPoints - trait.PointCost;
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
                
                // Combat bonuses (additive)
                bonuses.ActionPointBonus += def.ActionPointBonus;
                bonuses.MovementPointBonus += def.MovementPointBonus;
                bonuses.ReservedAPBonus += def.ReservedAPBonus;
                
                // Esper bonuses (additive)
                bonuses.EsperPointBonus += def.EsperPointBonus;
                bonuses.EsperPowerBonus += def.EsperPowerBonus;
                
                // Research bonuses
                bonuses.ResearchSlotBonus += def.ResearchSlotBonus;
                bonuses.RecipeUnlockBonus += def.RecipeUnlockBonus;
                
                // Social bonuses (additive)
                bonuses.DisguiseBonus += def.DisguiseBonus;
                bonuses.IntimidationBonus += def.IntimidationBonus;
                bonuses.PersuasionBonus += def.PersuasionBonus;
                
                // Flags (any trait setting these overrides)
                if (!def.CanSpeak) bonuses.CanSpeak = false;
                if (!def.CanDisguise) bonuses.CanDisguise = false;
                if (def.IsNightPerson) bonuses.IsNightPerson = true;
                if (def.CanEatCorpses) bonuses.CanEatCorpses = true;
                if (def.IsPacifist) bonuses.IsPacifist = true;
                if (def.HasPsychicPotential) bonuses.HasPsychicPotential = true;
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
                // Backstories are generally neutral (0) or slightly adjusted
                
                [TraitType.LabEscapee] = new TraitDefinition
                {
                    Type = TraitType.LabEscapee,
                    Name = "Lab Escapee",
                    Description = "Escaped from a research facility. Start with one implant, but hunters are after you.",
                    Category = TraitCategory.Backstory,
                    IsBackstory = true,
                    PointCost = 0, // Neutral - benefits balanced by danger
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
                    Description = "Something went wrong during your creation. Unstable but potentially powerful. (-1 point)",
                    Category = TraitCategory.Backstory,
                    IsBackstory = true,
                    PointCost = -1, // GIVES 1 extra point (it's a bad thing - unstable)
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
                    TradeModifier = 1.15f, // Worse prices (bad)
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
                    Description = "Cannot speak. Must rely on gestures or writing. Trading and persuasion severely limited. (+2 points)",
                    Category = TraitCategory.Communication,
                    PointCost = -2, // GIVES 2 points (bad trait)
                    CanSpeak = false,
                    PersuasionBonus = -0.5f,
                    TradeModifier = 1.3f,
                    Conflicts = new List<TraitType> { TraitType.Eloquent }
                },
                
                [TraitType.Eloquent] = new TraitDefinition
                {
                    Type = TraitType.Eloquent,
                    Name = "Eloquent",
                    Description = "Silver-tongued speaker. Better prices and more dialogue options. (-1 point)",
                    Category = TraitCategory.Communication,
                    PointCost = 1, // COSTS 1 point (good trait)
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
                    PointCost = 0, // Neutral - mixed benefits
                    IntimidationBonus = 0.3f,
                    PersuasionBonus = -0.1f
                },
                
                // ========== PHYSICAL ==========
                
                [TraitType.Frail] = new TraitDefinition
                {
                    Type = TraitType.Frail,
                    Name = "Frail",
                    Description = "Weak constitution. Less health but faster movement. (+1 point)",
                    Category = TraitCategory.Physical,
                    PointCost = -1, // GIVES 1 point (bad trait - less health)
                    HealthModifier = 0.75f,
                    SpeedModifier = 1.1f,
                    Conflicts = new List<TraitType> { TraitType.Tough }
                },
                
                [TraitType.Tough] = new TraitDefinition
                {
                    Type = TraitType.Tough,
                    Name = "Tough",
                    Description = "Hardy constitution. More health but slower. (-1 point)",
                    Category = TraitCategory.Physical,
                    PointCost = 1, // COSTS 1 point (good trait - more health)
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
                    PointCost = 0, // Balanced
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
                    PointCost = 0, // Balanced
                    // Ambush detection handled in combat system
                },
                
                [TraitType.Focused] = new TraitDefinition
                {
                    Type = TraitType.Focused,
                    Name = "Focused",
                    Description = "Intense concentration. Better research speed. (-1 point)",
                    Category = TraitCategory.Mental,
                    PointCost = 1, // COSTS 1 point (good trait)
                    ResearchModifier = 1.2f,
                    AccuracyModifier = 1.1f
                },
                
                [TraitType.QuickLearner] = new TraitDefinition
                {
                    Type = TraitType.QuickLearner,
                    Name = "Quick Learner",
                    Description = "Learn faster from experience. (-2 points)",
                    Category = TraitCategory.Mental,
                    PointCost = 2, // COSTS 2 points (very good trait)
                    XPModifier = 1.25f,
                    Conflicts = new List<TraitType> { TraitType.SlowLearner }
                },
                
                [TraitType.SlowLearner] = new TraitDefinition
                {
                    Type = TraitType.SlowLearner,
                    Name = "Slow Learner",
                    Description = "Takes longer to learn new things. (+1 point)",
                    Category = TraitCategory.Mental,
                    PointCost = -1, // GIVES 1 point (bad trait)
                    XPModifier = 0.8f,
                    Conflicts = new List<TraitType> { TraitType.QuickLearner }
                },
                
                // ========== SOCIAL ==========
                
                [TraitType.Disguised] = new TraitDefinition
                {
                    Type = TraitType.Disguised,
                    Name = "Passable",
                    Description = "Can pass as human with some effort. Better faction relations. (-2 points)",
                    Category = TraitCategory.Social,
                    PointCost = 2, // COSTS 2 points (very good trait)
                    CanDisguise = true,
                    DisguiseBonus = 0.3f,
                    Conflicts = new List<TraitType> { TraitType.ObviousMutant }
                },
                
                [TraitType.ObviousMutant] = new TraitDefinition
                {
                    Type = TraitType.ObviousMutant,
                    Name = "Obvious Mutant",
                    Description = "Clearly inhuman. Cannot disguise, higher aggression from humans. (+1 point)",
                    Category = TraitCategory.Social,
                    PointCost = -1, // GIVES 1 point (bad trait)
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
                    PointCost = 0 // Balanced
                    // Faction modifiers handled in faction system
                },
                
                // ========== QUIRKS ==========
                
                [TraitType.Cannibal] = new TraitDefinition
                {
                    Type = TraitType.Cannibal,
                    Name = "Cannibal",
                    Description = "Can eat human corpses without penalty. Others find this disturbing. (+1 point)",
                    Category = TraitCategory.Quirk,
                    PointCost = -1, // GIVES 1 point (socially bad, but useful)
                    CanEatCorpses = true
                },
                
                [TraitType.Pacifist] = new TraitDefinition
                {
                    Type = TraitType.Pacifist,
                    Name = "Pacifist",
                    Description = "Abhors violence. Worse at combat but better at diplomacy.",
                    Category = TraitCategory.Quirk,
                    PointCost = 0, // Balanced
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
                    PointCost = 0, // Balanced
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
                    PointCost = 0, // Balanced
                    IsNightPerson = true
                    // Day/night modifiers handled in time system
                },
                
                // ========== NEW COMBAT TRAITS ==========
                
                [TraitType.Athletic] = new TraitDefinition
                {
                    Type = TraitType.Athletic,
                    Name = "Athletic",
                    Description = "Exceptional physical conditioning. +1 Movement Point per turn.",
                    Category = TraitCategory.Physical,
                    PointCost = 2,  // Costs 2 points
                    MovementPointBonus = 1,
                    SpeedModifier = 1.1f
                },
                
                [TraitType.Sluggish] = new TraitDefinition
                {
                    Type = TraitType.Sluggish,
                    Name = "Sluggish",
                    Description = "Slow and lethargic. -1 Movement Point per turn. (+1 point)",
                    Category = TraitCategory.Physical,
                    PointCost = -1,  // Gives 1 point
                    MovementPointBonus = -1,
                    SpeedModifier = 0.9f,
                    Conflicts = new List<TraitType> { TraitType.Athletic, TraitType.Nimble }
                },
                
                [TraitType.Nimble] = new TraitDefinition
                {
                    Type = TraitType.Nimble,
                    Name = "Nimble",
                    Description = "Light on your feet. +1 MP and better dodging.",
                    Category = TraitCategory.Physical,
                    PointCost = 2,
                    MovementPointBonus = 1,
                    SpeedModifier = 1.15f,
                    Conflicts = new List<TraitType> { TraitType.Bulky, TraitType.Sluggish }
                },
                
                [TraitType.Bulky] = new TraitDefinition
                {
                    Type = TraitType.Bulky,
                    Name = "Bulky",
                    Description = "Heavy build. More HP but -1 Movement Point.",
                    Category = TraitCategory.Physical,
                    PointCost = 0,  // Balanced
                    MovementPointBonus = -1,
                    HealthModifier = 1.2f,
                    Conflicts = new List<TraitType> { TraitType.Nimble }
                },
                
                [TraitType.CombatTraining] = new TraitDefinition
                {
                    Type = TraitType.CombatTraining,
                    Name = "Combat Training",
                    Description = "Formal combat training. +1 Action Point per turn.",
                    Category = TraitCategory.Combat,
                    PointCost = 3,  // Expensive - AP is powerful
                    ActionPointBonus = 1,
                    AccuracyModifier = 1.05f
                },
                
                [TraitType.BattleHardened] = new TraitDefinition
                {
                    Type = TraitType.BattleHardened,
                    Name = "Battle Hardened",
                    Description = "Veteran of many fights. +1 AP and +10% damage.",
                    Category = TraitCategory.Combat,
                    PointCost = 4,  // Very expensive
                    ActionPointBonus = 1,
                    DamageModifier = 1.1f
                },
                
                [TraitType.Clumsy] = new TraitDefinition
                {
                    Type = TraitType.Clumsy,
                    Name = "Clumsy",
                    Description = "Awkward in combat. -1 AP. (+2 points)",
                    Category = TraitCategory.Combat,
                    PointCost = -2,  // Gives 2 points
                    ActionPointBonus = -1,
                    AccuracyModifier = 0.9f,
                    Conflicts = new List<TraitType> { TraitType.CombatTraining, TraitType.BattleHardened }
                },
                
                [TraitType.QuickReflexes] = new TraitDefinition
                {
                    Type = TraitType.QuickReflexes,
                    Name = "Quick Reflexes",
                    Description = "Fast reactions. +1 MP and initiative bonus.",
                    Category = TraitCategory.Combat,
                    PointCost = 2,
                    MovementPointBonus = 1,
                    SpeedModifier = 1.1f
                },
                
                [TraitType.TacticalMind] = new TraitDefinition
                {
                    Type = TraitType.TacticalMind,
                    Name = "Tactical Mind",
                    Description = "Strategic thinker. +1 max reserved AP allows saving more actions.",
                    Category = TraitCategory.Combat,
                    PointCost = 2,
                    ReservedAPBonus = 1
                },
                
                // ========== PSYCHIC TRAITS ==========
                
                [TraitType.PsychicSensitive] = new TraitDefinition
                {
                    Type = TraitType.PsychicSensitive,
                    Name = "Psychic Sensitive",
                    Description = "Born with latent psychic potential. +3 EP, unlocks esper abilities.",
                    Category = TraitCategory.Psychic,
                    PointCost = 2,
                    EsperPointBonus = 3,
                    HasPsychicPotential = true
                },
                
                [TraitType.PsychicBlank] = new TraitDefinition
                {
                    Type = TraitType.PsychicBlank,
                    Name = "Psychic Blank",
                    Description = "No psychic presence. Immune to mind attacks but 0 EP. (+1 point)",
                    Category = TraitCategory.Psychic,
                    PointCost = -1,  // Gives 1 point - tradeoff
                    EsperPointBonus = -100,  // Effectively 0 EP
                    Conflicts = new List<TraitType> { TraitType.PsychicSensitive, TraitType.IronWill }
                },
                
                [TraitType.IronWill] = new TraitDefinition
                {
                    Type = TraitType.IronWill,
                    Name = "Iron Will",
                    Description = "Unbreakable mental fortitude. +2 EP and resist mental effects.",
                    Category = TraitCategory.Psychic,
                    PointCost = 2,
                    EsperPointBonus = 2,
                    Conflicts = new List<TraitType> { TraitType.PsychicBlank }
                },
                
                [TraitType.GeniusIntellect] = new TraitDefinition
                {
                    Type = TraitType.GeniusIntellect,
                    Name = "Genius Intellect",
                    Description = "Brilliant mind. +1 research slot, +2 recipe unlocks.",
                    Category = TraitCategory.Mental,
                    PointCost = 2,
                    ResearchModifier = 1.25f,
                    ResearchSlotBonus = 1,
                    RecipeUnlockBonus = 2,
                    Conflicts = new List<TraitType> { TraitType.Scatterbrained }
                },
                
                [TraitType.Scatterbrained] = new TraitDefinition
                {
                    Type = TraitType.Scatterbrained,
                    Name = "Scatterbrained",
                    Description = "Easily distracted. -25% research speed. (+1 point)",
                    Category = TraitCategory.Mental,
                    PointCost = -1,
                    ResearchModifier = 0.75f,
                    Conflicts = new List<TraitType> { TraitType.GeniusIntellect, TraitType.Focused }
                },
                
                // ========== NEW BACKSTORIES ==========
                
                [TraitType.FormerSoldier] = new TraitDefinition
                {
                    Type = TraitType.FormerSoldier,
                    Name = "Former Soldier",
                    Description = "Military training before the fall. +1 AP and combat bonuses.",
                    Category = TraitCategory.Backstory,
                    IsBackstory = true,
                    PointCost = 0,
                    ActionPointBonus = 1,
                    AccuracyModifier = 1.1f,
                    StartingItems = new List<string> { "knife_combat", "armor_raider" }
                },
                
                [TraitType.Scientist] = new TraitDefinition
                {
                    Type = TraitType.Scientist,
                    Name = "Scientist",
                    Description = "Pre-war researcher. +2 research slots and bonus recipes.",
                    Category = TraitCategory.Backstory,
                    IsBackstory = true,
                    PointCost = 0,
                    ResearchModifier = 1.3f,
                    ResearchSlotBonus = 2,
                    RecipeUnlockBonus = 3,
                    DamageModifier = 0.9f,  // Not combat trained
                    StartingItems = new List<string> { "med_kit", "psi_focus_crystal" }
                },
                
                [TraitType.PsychicProdigy] = new TraitDefinition
                {
                    Type = TraitType.PsychicProdigy,
                    Name = "Psychic Prodigy",
                    Description = "Born with powerful psychic gifts. +5 EP and enhanced esper power.",
                    Category = TraitCategory.Backstory,
                    IsBackstory = true,
                    PointCost = 0,
                    EsperPointBonus = 5,
                    EsperPowerBonus = 0.2f,
                    HasPsychicPotential = true,
                    HealthModifier = 0.9f,  // Fragile body
                    StartingItems = new List<string> { "psi_amplifier" }
                },
                
                [TraitType.DarkCultist] = new TraitDefinition
                {
                    Type = TraitType.DarkCultist,
                    Name = "Dark Cultist",
                    Description = "Follower of dark science. +3 EP with affinity for forbidden knowledge.",
                    Category = TraitCategory.Backstory,
                    IsBackstory = true,
                    PointCost = 0,
                    EsperPointBonus = 3,
                    ResearchModifier = 1.15f,
                    TradeModifier = 1.2f,  // Distrusted
                    HasPsychicPotential = true,
                    StartingItems = new List<string> { "dark_ritual_tome" }
                },
                
                [TraitType.Mechanic] = new TraitDefinition
                {
                    Type = TraitType.Mechanic,
                    Name = "Mechanic",
                    Description = "Skilled with machines. Better crafting and starts with tools.",
                    Category = TraitCategory.Backstory,
                    IsBackstory = true,
                    PointCost = 0,
                    ResearchModifier = 1.1f,
                    RecipeUnlockBonus = 2,
                    StartingItems = new List<string> { "toolkit", "scrap_metal" }
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
        public Attributes Attributes { get; set; } = new Attributes();
        
        public List<TraitType> AllTraits => new List<TraitType> { Backstory }.Concat(Traits).ToList();
        
        public override string ToString()
        {
            var traitStr = Traits.Any() ? string.Join(", ", Traits) : "None";
            return $"[{Backstory}] Traits: {traitStr} | Points: {MutationPoints} | {Attributes.GetDisplayString()}";
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
        
        // Combat bonuses (additive)
        public int ActionPointBonus { get; set; } = 0;
        public int MovementPointBonus { get; set; } = 0;
        public int ReservedAPBonus { get; set; } = 0;
        
        // Esper bonuses (additive)
        public int EsperPointBonus { get; set; } = 0;
        public float EsperPowerBonus { get; set; } = 0f;
        
        // Research/INT bonuses
        public int ResearchSlotBonus { get; set; } = 0;
        public int RecipeUnlockBonus { get; set; } = 0;
        
        // Additive social
        public float DisguiseBonus { get; set; } = 0f;
        public float IntimidationBonus { get; set; } = 0f;
        public float PersuasionBonus { get; set; } = 0f;
        
        // Flags
        public bool CanSpeak { get; set; } = true;
        public bool CanDisguise { get; set; } = true;
        public bool IsNightPerson { get; set; } = false;
        public bool CanEatCorpses { get; set; } = false;
        public bool IsPacifist { get; set; } = false;
        public bool HasPsychicPotential { get; set; } = false;
    }
}
