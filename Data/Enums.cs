// Data/Enums.cs
// Central location for all game enumerations

namespace MyRPG.Data
{
    // ============================================
    // BODY PART SYSTEM
    // ============================================
    
    public enum BodyPartType
    {
        // Head
        Head,
        Brain,
        LeftEye,
        RightEye,
        Nose,
        Jaw,
        
        // Torso
        Torso,
        Heart,
        LeftLung,
        RightLung,
        Stomach,
        Liver,
        
        // Arms (can have multiples via mutation)
        LeftArm,
        RightArm,
        LeftHand,
        RightHand,
        
        // Legs
        LeftLeg,
        RightLeg,
        LeftFoot,
        RightFoot,
        
        // Mutation-added parts
        Tail,
        Wings,
        ExtraEye,
        Tentacle
    }
    
    public enum BodyPartCondition
    {
        Healthy,        // 100% function
        Scratched,      // 90% function
        Bruised,        // 80% function
        Cut,            // 70% function
        Injured,        // 50% function
        SeverelyInjured,// 25% function
        Broken,         // 10% function
        Destroyed,      // 0% function, needs removal
        Missing         // Gone completely
    }
    
    public enum BodyPartCategory
    {
        Vital,          // Death if destroyed (heart, brain)
        Important,      // Major penalties if destroyed
        Utility,        // Loss is inconvenient but survivable
        Sensory,        // Affects perception
        Manipulation,   // Affects item use
        Movement        // Affects mobility
    }
    
    // ============================================
    // STATUS EFFECTS
    // ============================================
    
    public enum StatusEffectType
    {
        // Elemental
        Wet,
        Burning,
        Frozen,
        Electrified,
        Oiled,
        
        // Physical
        Bleeding,
        Exhausted,
        Stunned,
        Slowed,
        Paralyzed,
        Poisoned,
        
        // Mental
        Panicked,
        Focused,
        Berserk,
        Dazed,
        Confused,
        
        // Environmental
        Muddy,
        Irradiated,
        Hypothermia,
        Heatstroke,
        
        // Combat
        InCover,
        Flanked,
        Suppressed,
        Overwatch
    }
    
    public enum StatusCategory
    {
        Elemental,
        Physical,
        Mental,
        Environmental,
        Combat,
        Buff,
        Debuff
    }
    
    public enum StatusStackBehavior
    {
        RefreshDuration,    // New application refreshes timer
        StackIntensity,     // Stacks increase severity
        StackDuration,      // Stacks add duration
        NoStack             // Cannot reapply while active
    }
    
    // ============================================
    // MUTATIONS
    // ============================================
    
    public enum MutationType
    {
        // Physical - Body Modifications
        ExtraArms,          // +2 arm parts
        ExtraEyes,          // +1-2 eye parts, better perception
        Claws,              // Natural weapons on hands
        ThickHide,          // Damage resistance
        Tail,               // Balance, extra attacks
        Wings,              // Limited flight/glide
        
        // Physical - Enhancements  
        NightVision,        // See in darkness
        Regeneration,       // Passive healing
        ToxinFilter,        // Poison resistance
        AcidBlood,          // Damages attackers
        Camouflage,         // Stealth bonus
        
        // Mental
        ComplexBrain,       // Research speed
        Telepathy,          // Detect minds, limited communication
        PrecognitionMinor,  // Initiative bonus
        
        // Movement
        TreeJump,           // Jump between trees, forest evasion
        WallCrawl,          // Climb surfaces
        Burrowing,          // Move through soft ground
        AquaticAdaptation,  // Breathe underwater, swim fast
        
        // Utility
        PhotosynthesisSkin, // Reduce hunger in sunlight
        EchoLocation,       // Detect through walls
        ThermalSense,       // See heat signatures
        
        // Weird/Dark
        VoidTouch,          // Dark science affinity
        CorpseEater,        // Can eat corpses safely
        FearAura,           // Enemies may flee
        UnstableForm        // Random effects, high risk/reward
    }
    
    public enum MutationCategory
    {
        Physical,
        Mental,
        Movement,
        Sensory,
        Utility,
        Dark
    }
    
    // ============================================
    // TRAITS
    // ============================================
    
    public enum TraitType
    {
        // Communication
        CantSpeak,          // Cannot talk to NPCs normally
        Eloquent,           // Better prices, more dialog options
        Intimidating,       // Fear-based persuasion
        
        // Physical Background
        Frail,              // Less HP
        Tough,              // More HP
        FastMetabolism,     // Heal faster, hunger faster
        SlowMetabolism,     // Heal slower, hunger slower
        
        // Mental
        Paranoid,           // Detect ambush, but stress easier
        Focused,            // Research bonus
        QuickLearner,       // XP bonus
        SlowLearner,        // XP penalty
        
        // Social
        Disguised,          // Can pass as human (with effort)
        ObviousMutant,      // Cannot hide mutation
        Outcast,            // Mutant factions trust you more
        
        // Quirks
        Cannibal,           // Can eat humans
        Pacifist,           // Combat penalties, social bonuses
        Bloodlust,          // Combat bonuses, social penalties
        NightOwl,           // Bonuses at night, penalties in day
        
        // Backstory (set at creation)
        LabEscapee,         // Start with 1 implant, hunted
        WastelandBorn,      // Survival bonuses
        FailedExperiment,   // Random mutation, unstable
        TribalMutant,       // Combat bonus, tech penalty
        UrbanSurvivor       // Stealth bonus, trade bonus
    }
    
    public enum TraitCategory
    {
        Communication,
        Physical,
        Mental,
        Social,
        Quirk,
        Backstory
    }
    
    // ============================================
    // SCIENCE PATHS
    // ============================================
    
    public enum SciencePath
    {
        Tinker,     // Technology, implants, guns
        Dark        // Rituals, anomalies, transmutation
    }
    
    // ============================================
    // COMBAT
    // ============================================
    
    public enum CombatState
    {
        Exploration,        // Real-time, no combat
        CombatInitiated,    // Transitioning to combat
        InCombat,           // Turn-based active
        CombatEnding        // Transitioning out
    }
    
    public enum ActionType
    {
        Move,
        Attack,
        UseItem,
        UseAbility,
        Reload,
        Wait,
        Overwatch,
        Interact
    }
    
    public enum CoverType
    {
        None,
        Half,       // 25% miss chance vs ranged
        Full        // 50% miss chance vs ranged
    }
    
    public enum DamageType
    {
        Physical,
        Fire,
        Cold,
        Electric,
        Poison,
        Radiation,
        Void        // Dark science damage
    }
    
    // ============================================
    // SURVIVAL
    // ============================================
    
    public enum NeedType
    {
        Hunger,
        Thirst,
        Rest,
        Temperature
    }
    
    public enum NeedLevel
    {
        Satisfied,      // 75-100%
        Peckish,        // 50-74%
        Hungry,         // 25-49%
        Starving,       // 1-24%
        Critical        // 0%
    }
    
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }
    
    public enum TimeOfDay
    {
        Dawn,       // 5-7
        Morning,    // 7-12
        Afternoon,  // 12-17
        Evening,    // 17-20
        Dusk,       // 20-21
        Night       // 21-5
    }
    
    // ============================================
    // WORLD
    // ============================================
    
    public enum ZoneType
    {
        Spawn,
        Wasteland,
        TradingPost,
        Objective,
        Ruins,
        Swamp,
        DarkForest,
        RadiationZone,
        MysteryZone,
        Settlement
    }
    
    public enum ZoneDifficulty
    {
        Safe,
        Easy,
        Medium,
        Hard,
        Deadly,
        Unknown
    }
    
    // ============================================
    // FACTIONS
    // ============================================
    
    public enum FactionType
    {
        Player,
        MutantTribes,       // Fellow mutants
        TechCity,           // High-tech normal faction
        Traders,            // Neutral merchants
        MutantHunters,      // Hostile to mutants
        VoidCult,           // Dark science users
        Wildlife,           // Animals, neutral/hostile
        Monsters            // Always hostile
    }
    
    public enum FactionStanding
    {
        Hostile,        // Attack on sight
        Unfriendly,     // Won't help, might attack
        Neutral,        // Will trade, cautious
        Friendly,       // Will help, good prices
        Allied          // Fight alongside you
    }
}
