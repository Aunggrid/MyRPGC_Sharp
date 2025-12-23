// Data/Enums.cs
// Central location for all game enumerations

namespace MyRPG.Data
{
    // ============================================
    // ATTRIBUTES
    // ============================================
    
    public enum AttributeType
    {
        STR,    // Strength - Melee damage, carry weight, intimidation
        AGI,    // Agility - Speed, dodge, ranged accuracy  
        END,    // Endurance - Health, stamina, resistance
        INT,    // Intelligence - Research speed, crafting, tech mutations
        PER,    // Perception - Sight range, accuracy, detection
        WIL     // Willpower - Mental resistance, dark science, psionic mutations
    }
    
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
        Tentacle,
        MutantArm,      // Additional arm from mutation
        MutantHand,     // Hand attached to mutant arm (can equip weapons)
        MutantLeg,      // Additional leg
        Carapace,       // Armored shell
        PsionicNode,    // Psychic organ
        VenomGland,     // Poison production
        Antennae,       // Enhanced sensing
        Gills           // Water breathing
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
        ExtraArms,          // +2 arm parts, can wield more weapons
        ExtraEyes,          // +1-2 eye parts, better perception
        Claws,              // Natural weapons on hands
        ThickHide,          // Damage resistance
        Tail,               // Balance, extra attacks
        Wings,              // Limited flight/glide
        Carapace,           // Armored shell (+armor)
        ExtraLegs,          // +2 leg parts (+MP)
        
        // Physical - Enhancements  
        NightVision,        // See in darkness
        Regeneration,       // Passive healing
        ToxinFilter,        // Poison resistance
        AcidBlood,          // Damages attackers
        Camouflage,         // Stealth bonus
        AdrenalGlands,      // +AP when below 50% HP
        DenseMusculature,   // +damage, +carry weight
        FlexibleJoints,     // +MP, dodge bonus
        EnhancedReflexes,   // +AGI effects, reduced dual-wield penalty
        EagleEye,           // +PER, better ranged accuracy, reduced akimbo penalty
        
        // Psychic / Esper (uses EP)
        PsionicAwakening,   // Unlocks EP, base +3 EP
        Telepathy,          // Detect minds, limited communication, +2 EP
        PrecognitionMinor,  // Initiative bonus, +1 EP
        Telekinesis,        // Move objects, push enemies, +2 EP
        PsychicScream,      // AoE stun ability, +1 EP
        MindShield,         // Resist mental effects, +1 EP
        EmpatheticLink,     // Sense enemy intent, +2 EP
        PsionicBlast,       // Ranged psychic damage, +2 EP
        DominateWill,       // Chance to mind control, +3 EP
        
        // Mental (INT-related)
        ComplexBrain,       // Research speed +25%
        EideticMemory,      // +1 research slot
        TechSavant,         // +2 recipe unlocks
        AnalyticalMind,     // Better crafting quality
        
        // Movement
        TreeJump,           // Jump between trees, forest evasion
        WallCrawl,          // Climb surfaces
        Burrowing,          // Move through soft ground
        AquaticAdaptation,  // Breathe underwater, swim fast
        Sprinter,           // +2 MP
        
        // Utility
        PhotosynthesisSkin, // Reduce hunger in sunlight
        EchoLocation,       // Detect through walls
        ThermalSense,       // See heat signatures
        VenomGlands,        // Poison attacks
        
        // Weird/Dark
        VoidTouch,          // Dark science affinity, +2 EP (dark)
        CorpseEater,        // Can eat corpses safely
        FearAura,           // Enemies may flee
        UnstableForm,       // Random effects, high risk/reward
        Hivemind,           // Control swarm creatures, +3 EP
        ShadowMeld,         // Stealth in darkness, +1 MP at night
        
        // Survival
        MoveableVitalOrgan  // Can relocate damage from critical parts to other parts (once per 3 days)
    }
    
    public enum MutationCategory
    {
        Physical,
        Mental,
        Psychic,        // Esper mutations
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
        SilverTongue,       // +20% trade prices
        
        // Physical Background
        Frail,              // Less HP
        Tough,              // More HP
        FastMetabolism,     // Heal faster, hunger faster
        SlowMetabolism,     // Heal slower, hunger slower
        Athletic,           // +1 MP
        Sluggish,           // -1 MP
        Nimble,             // +dodge chance, +1 MP
        Bulky,              // +HP, -1 MP
        
        // Combat Aptitude
        CombatTraining,     // +1 AP
        BattleHardened,     // +1 AP, +damage
        Clumsy,             // -1 AP
        QuickReflexes,      // +initiative, +1 MP
        Berserker,          // +damage when hurt, -accuracy
        PreciseStriker,     // +accuracy, +crit
        TacticalMind,       // +1 max reserved AP
        
        // Mental / Psychic
        Paranoid,           // Detect ambush, but stress easier
        Focused,            // Research bonus
        QuickLearner,       // XP bonus
        SlowLearner,        // XP penalty
        PsychicSensitive,   // +3 EP, unlocks esper abilities
        PsychicBlank,       // 0 EP, immune to psychic attacks
        GeniusIntellect,    // +1 research slot, +2 recipe unlocks
        Scatterbrained,     // -research speed, -1 research slot
        
        // Social
        Disguised,          // Can pass as human (with effort)
        ObviousMutant,      // Cannot hide mutation
        Outcast,            // Mutant factions trust you more
        Charming,           // Better NPC relations
        Antisocial,         // Worse NPC relations
        
        // Quirks
        Cannibal,           // Can eat humans
        Pacifist,           // Combat penalties, social bonuses
        Bloodlust,          // Combat bonuses, social penalties
        NightOwl,           // Bonuses at night, penalties in day
        Insomniac,          // Need less rest, but tired debuff more common
        IronWill,           // +2 EP, resist mental effects
        
        // Backstory (set at creation, gives starting bonuses)
        LabEscapee,         // Start with 1 implant, +1 research slot, hunted
        WastelandBorn,      // Survival bonuses, +1 MP
        FailedExperiment,   // Random mutation, +2 EP, unstable
        TribalMutant,       // +1 AP melee, tech penalty
        UrbanSurvivor,      // Stealth bonus, trade bonus
        FormerSoldier,      // +1 AP, +accuracy, start with weapon
        Scientist,          // +2 research slots, +3 recipe unlocks
        PsychicProdigy,     // +5 EP, +esper power
        DarkCultist,        // Dark science affinity, +3 EP, starts with ritual
        Mechanic            // +craft quality, start with tools
    }
    
    public enum TraitCategory
    {
        Communication,
        Physical,
        Combat,
        Mental,
        Psychic,
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
        Void,       // Dark science damage
        Acid,       // Corrosive damage
        Psychic     // Mental damage
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
    
    /// <summary>
    /// Zone types within the Exclusion Zone of Orodia
    /// </summary>
    public enum ZoneType
    {
        // Basic Zone Types
        Spawn,
        Wasteland,
        Forest,
        Swamp,
        Cave,
        
        // Ruins of Aethelgard
        Ruins,              // Generic ruins
        OuterRuins,         // Edge of the Zone - least corruption
        InnerRuins,         // Deeper ruins - more danger, better loot
        DeepZone,           // Near the epicenter - extreme corruption
        Epicenter,          // Ground zero of The Severance - reality breaks here
        
        // Settlements
        Settlement,         // Generic settlement
        TradingPost,        // Neutral trading hub
        MutantCamp,         // Changed tribal settlement
        TradeOutpost,       // Syndicate trading post
        SanctumOutpost,     // Sanctum military forward base
        VerdantLab,         // Verdant research facility
        
        // Special/Dangerous Locations
        DarkForest,
        RadiationZone,
        MysteryZone,
        Laboratory,
        AncientVault,       // Sealed Aethelgard bunker - relics inside
        VoidRift,           // Active tear in reality - Void Spawn here
        GeneElderHold,      // Powerful mutant leader's territory
        AnomalySite,        // Strong Void activity - Dark Science materials
        
        // Objectives
        Objective
    }
    
    public enum ZoneExitDirection
    {
        North,
        South,
        East,
        West
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
    // FACTIONS (The World of Orodia)
    // ============================================
    
    public enum FactionType
    {
        // Player
        Player,
        
        // "The Changed" - Mutant Society inside the Exclusion Zone
        TheChanged,         // Fellow mutants, your people
        GeneElders,         // Tribal leaders, powerful mutants
        VoidCult,           // Dark Science practitioners
        
        // "The Triad" - Three Kingdoms surrounding the Zone
        UnitedSanctum,      // High-tech militaristic kingdom - views mutants as "hazards"
        IronSyndicate,      // Industrial trade kingdom - views mutants as "cheap labor"
        VerdantOrder,       // Bio-engineer religious kingdom - views mutants as "test subjects"
        
        // Neutral/Wildlife
        Traders,            // Zone scavengers who trade with anyone
        Wildlife,           // Mutated animals, mostly neutral
        VoidSpawn           // Creatures from the Void itself - always hostile
    }
    
    public enum FactionStanding
    {
        Hated,          // Kill on sight, send death squads
        Hostile,        // Attack on sight
        Unfriendly,     // Won't help, might attack if provoked
        Neutral,        // Will trade, cautious
        Friendly,       // Will help, good prices
        Allied,         // Fight alongside you
        Revered         // Legendary status, unique rewards
    }
    
    // ============================================
    // ITEMS
    // ============================================
    
    public enum ItemCategory
    {
        Weapon,         // Melee and ranged weapons
        Armor,          // Protective gear
        Consumable,     // Food, medicine, potions
        Material,       // Crafting components
        Tool,           // Utility items
        Ammo,           // Ammunition for ranged weapons
        Junk,           // Can be broken down or sold
        Quest,          // Quest-related items
        Implant,        // Cybernetic implants from The Triad
        Relic,          // Ancient Aethelgard technology (400+ years old)
        Currency        // Trade goods: Void Shards, Tech Credits, etc.
    }
    
    /// <summary>
    /// Currency types in Orodia
    /// </summary>
    public enum CurrencyType
    {
        // Zone Currency (The Changed)
        VoidShard,          // Crystallized Void energy - universal currency in the Zone
        MutantFavor,        // Reputation tokens with Gene-Elders
        
        // Triad Currencies
        SanctumCredits,     // United Sanctum digital currency
        SyndicateScrip,     // Iron Syndicate trade notes
        VerdantTithes,      // Verdant Order religious tokens
        
        // Special
        AncientRelic,       // Aethelgard artifacts - extremely valuable everywhere
        EssenceFragment     // Pure Void essence - Dark Science crafting
    }
    
    public enum ItemRarity
    {
        Common,         // Gray - everywhere
        Uncommon,       // Green - occasional find
        Rare,           // Blue - lucky find
        Epic,           // Purple - very rare
        Legendary,      // Gold - unique
        Relic           // Cyan - Ancient Aethelgard tech
    }
    
    public enum WeaponType
    {
        // Melee
        Unarmed,
        Knife,
        Sword,
        Axe,
        Club,
        Spear,
        
        // Ranged
        Bow,
        Crossbow,
        Pistol,
        Rifle,
        Shotgun,
        EnergyWeapon
    }
    
    public enum WeaponLength
    {
        None,       // Not a weapon or unarmed
        Short,      // Knives, daggers, pistols - no reach advantage
        Medium,     // Swords, axes, clubs, SMGs - balanced
        Long,       // Spears, polearms, rifles - reach advantage
        VeryLong    // Pikes, sniper rifles - maximum reach
    }
    
    public enum GripMode
    {
        Default,    // Use weapon's default grip
        OneHand,    // Force one-handed (if possible)
        TwoHand     // Force two-handed (if possible)
    }
    
    public enum ArmorSlot
    {
        Head,
        Torso,
        Legs,
        Feet,
        Hands,
        Accessory       // Rings, amulets, etc.
    }
    
    public enum ConsumableType
    {
        Food,           // Restores hunger
        Water,          // Restores thirst
        Medicine,       // Heals HP
        Stimulant,      // Temporary buff
        Antidote,       // Cures poison
        RadAway         // Removes radiation
    }
    
    public enum ItemQuality
    {
        Broken,         // 25% effectiveness
        Poor,           // 50% effectiveness
        Normal,         // 100% effectiveness
        Good,           // 125% effectiveness
        Excellent,      // 150% effectiveness
        Masterwork      // 200% effectiveness
    }
    
    public enum EquipSlot
    {
        None,           // Cannot be equipped
        MainHand,       // Primary weapon
        OffHand,        // Shield or secondary
        TwoHand,        // Two-handed weapon
        ExtraArm1,      // Extra arm slot (from mutation)
        ExtraArm2,      // Extra arm slot (from mutation)
        Head,
        Torso,
        Legs,
        Feet,
        Hands,
        Accessory1,
        Accessory2
    }
    
    // ============================================
    // ENEMIES
    // ============================================
    
    public enum EnemyType
    {
        // === THE CHANGED (Hostile Mutants) ===
        Raider,             // Mutant scavenger, basic melee
        MutantBeast,        // Feral mutant, fast and aggressive
        Abomination,        // Heavily mutated horror, tank
        
        // === VOID-TOUCHED (Special Mutants) ===
        Spitter,            // Ranged acid attack, applies Burning
        Psionic,            // Mental attacks, can Stun/Confuse
        Brute,              // Heavy melee, knockback
        Stalker,            // Stealth assassin, bonus ambush damage
        HiveMother,         // Spawns Swarmlings
        Swarmling,          // Weak but numerous
        
        // === UNITED SANCTUM (Tech Kingdom - Purge Squads) ===
        SanctumTrooper,     // Basic soldier with energy rifle
        SanctumEnforcer,    // Heavy armor, shotgun
        SanctumCommando,    // Elite, power armor + laser rifle
        PurgeDrone,         // Flying robot, ranged attacks
        
        // === IRON SYNDICATE (Trade Kingdom - Mercenaries) ===
        SyndicateMerc,      // Hired gun, balanced
        SyndicateHeavy,     // LMG, suppression
        SlaveDriver,        // Whip + pistol, captures mutants
        SyndicateMech,      // Small combat robot
        
        // === VERDANT ORDER (Religious Kingdom - Collectors) ===
        VerdantCollector,   // Bio-suit, tranq rifle - captures mutants
        VerdantPurifier,    // Flamethrower, burns "impurity"
        VerdantBiomancer,   // Healer + buffs other Verdant
        GeneHound,          // Engineered hunting beast
        
        // === VOID SPAWN (Creatures from The Void) ===
        VoidWraith,         // Phasing ghost, hard to hit
        VoidCrawler,        // Spider-like, webs
        VoidHorror,         // Boss-tier, reality-bending attacks
        
        // === WILDLIFE (Mutated Animals) ===
        Scavenger,          // Rat-like creature, flees when attacked
        GiantInsect,        // Bug, drops chitin
        WildBoar,           // Charges when attacked, good meat
        MutantDeer,         // Flees, fast, good leather
        CaveSlug,           // Slow, drops slime/materials
        Hunter              // Predatory beast, stalks prey
    }
    
    public enum EnemyAbility
    {
        None,
        
        // Mutant abilities
        AcidSpit,           // Ranged attack that applies Burning
        PsionicBlast,       // Stuns target
        Knockback,          // Pushes target back 1-2 tiles
        Ambush,             // Double damage from stealth
        SpawnSwarmling,     // Creates a Swarmling
        Charge,             // Rush at target, bonus damage
        Regenerate,         // Heals each turn
        Explode,            // Damage on death
        
        // Tech abilities (Sanctum/Syndicate)
        Overwatch,          // Shoots enemies that move in sight
        Suppression,        // Reduces target accuracy
        ShieldBash,         // Melee stun
        CallReinforcement,  // Summons more enemies
        
        // Verdant abilities
        Tranquilize,        // Puts target to sleep
        Purify,             // Fire damage + removes buffs
        BioHeal,            // Heals nearby allies
        
        // Void abilities
        PhaseShift,         // Becomes untargetable briefly
        RealityTear,        // AoE damage
        VoidPull            // Pulls target closer
    }
    
    public enum CreatureBehavior
    {
        Aggressive,     // Always attacks on sight
        Passive,        // Only attacks when attacked first
        Territorial,    // Attacks if you get too close
        Cowardly        // Flees when attacked
    }
    
    /// <summary>
    /// AI Personality - affects tactical decision making (Rimworld-style)
    /// </summary>
    public enum AIPersonality
    {
        Balanced,       // Standard tactics, mix of aggression/caution
        Aggressive,     // Charges in, prioritizes offense
        Cautious,       // Keeps distance, retreats early
        Tactical,       // Seeks flanks, uses abilities smartly
        Berserk,        // Never retreats, all-out attack
        Cowardly        // Runs at first sign of danger
    }
    
    public enum EnemyState
    {
        Idle,
        Patrolling,
        Chasing,
        Attacking,
        Fleeing,
        Stunned,
        Dead
    }
    
    // ============================================
    // STRUCTURES / BUILDING
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
        Blueprint,
        UnderConstruction,
        Complete,
        Damaged,
        Destroyed
    }
    
    // ============================================
    // TILES / WORLD
    // ============================================
    
    public enum TileType
    {
        Grass,
        Dirt,
        Stone,
        Sand,
        Water,
        StoneWall,
        DeepWater
    }
    
    // ============================================
    // CRAFTING SYSTEM
    // ============================================
    
    public enum RecipeCategory
    {
        Basic,          // No workstation required
        Weapons,
        Armor,
        Tools,
        Consumables,
        Materials,
        Structures,
        Gadgets,        // Tinker science items
        Anomalies       // Dark science items
    }
    
    public enum WorkstationType
    {
        None,           // Basic crafting (hands only)
        CraftingBench,  // General crafting
        Forge,          // Metal weapons/armor
        CookingStation, // Food and potions
        AlchemyTable,   // Chemicals and drugs
        TinkerBench,    // Gadgets (Tinker science)
        RitualCircle    // Anomalies (Dark science)
    }
    
    // ============================================
    // WORLD OF ORODIA - EVENTS
    // ============================================
    
    /// <summary>
    /// Random world events (Rimworld-style)
    /// </summary>
    public enum WorldEvent
    {
        // Neutral Events
        VoidStorm,              // Purple storm - take cover or gain mutations
        TraderCaravan,          // Syndicate traders arrive
        WanderingMutant,        // Friendly Changed seeking shelter
        RelicSignal,            // Ancient tech activates nearby
        
        // Hostile Events - Changed
        RaiderAttack,           // Hostile mutant raiders
        BeastMigration,         // Pack of Mutant Beasts passing through
        HiveSwarming,           // HiveMother spawning event
        
        // Hostile Events - Triad
        SanctumPurge,           // Purge Squad sent to cleanse the area
        SyndicateRaid,          // Syndicate slavers hunting mutants
        VerdantCollection,      // Verdant Collectors hunting "specimens"
        
        // Hostile Events - Void
        VoidTide,               // Reality weakens - Void Spawn appear
        RealityFracture,        // Gravity/time goes haywire temporarily
        
        // Positive Events
        CacheDiscovered,        // Hidden supply cache found
        FriendlyScavengers,     // Changed scavengers willing to trade
        AnomalyBloom,           // Void Shards crystallize nearby
        AncientBroadcast        // Clue to nearby Aethelgard vault
    }
    
    /// <summary>
    /// Player origin backstory
    /// </summary>
    public enum PlayerOrigin
    {
        NullBorn,           // 5th generation mutant - balanced stats, no memories of "before"
        FirstGen,           // Recently mutated - higher stats but unstable mutations
        GeneElderChild,     // Born to tribal leader - bonus reputation with Changed
        VoidTouched,        // Heavy Void exposure - strong mutations, low sanity
        TriadDefector,      // Former Sanctum/Syndicate/Verdant - tech skills, hunted
        ScavengerBorn,      // Trader family - bonus to barter and scavenging
        CultInitiate        // Raised by Void Cult - Dark Science affinity
    }
}
