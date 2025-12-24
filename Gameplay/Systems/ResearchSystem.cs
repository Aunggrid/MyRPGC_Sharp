// Gameplay/Systems/ResearchSystem.cs
// Tech tree and research progression system
// Expanded with lore-appropriate nodes for Tinker and Dark Science paths

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;

namespace MyRPG.Gameplay.Systems
{
    // ============================================
    // RESEARCH ENUMS
    // ============================================

    public enum ResearchCategory
    {
        Tinker,         // Conventional technology (Triad-derived)
        Dark,           // Anomaly-based science (Void powers)
        Survival,       // Basic survival (available to all)
        Combat          // Combat techniques (available to all)
    }

    public enum ResearchState
    {
        Locked,         // Prerequisites not met
        Available,      // Can be researched
        InProgress,     // Currently researching
        Completed       // Finished
    }

    // ============================================
    // RESEARCH NODE
    // ============================================

    public class ResearchNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ResearchCategory Category { get; set; }
        public int Tier { get; set; } = 1;  // 1-5, higher = more advanced

        // Requirements
        public List<string> Prerequisites { get; set; } = new List<string>();
        public Dictionary<string, int> ResourceCost { get; set; } = new Dictionary<string, int>();
        public int ResearchTime { get; set; } = 60;  // Seconds to complete
        public int RequiredLevel { get; set; } = 1;
        public SciencePath? RequiredPath { get; set; }  // null = any path can research

        // Unlocks
        public List<string> UnlocksRecipes { get; set; } = new List<string>();
        public List<MutationType> UnlocksMutations { get; set; } = new List<MutationType>();
        public List<string> UnlocksStructures { get; set; } = new List<string>();
        public Dictionary<string, float> StatBonuses { get; set; } = new Dictionary<string, float>();
        public string UnlocksAbility { get; set; }  // Special ability ID

        // State (runtime)
        public ResearchState State { get; set; } = ResearchState.Locked;
        public float Progress { get; set; } = 0f;

        public ResearchNode(string id, string name, ResearchCategory category)
        {
            Id = id;
            Name = name;
            Category = category;
        }

        public bool IsComplete => State == ResearchState.Completed;
        public float ProgressPercent => ResearchTime > 0 ? (Progress / ResearchTime * 100f) : 0f;
    }

    // ============================================
    // RESEARCH SYSTEM
    // ============================================

    public class ResearchSystem
    {
        private Dictionary<string, ResearchNode> _nodes = new Dictionary<string, ResearchNode>();
        private string _currentResearch = null;
        private SciencePath _playerPath = SciencePath.Tinker;

        // Events
        public event Action<ResearchNode> OnResearchStarted;
        public event Action<ResearchNode> OnResearchCompleted;
        public event Action<ResearchNode, float> OnResearchProgress;

        public ResearchSystem()
        {
            InitializeResearchTree();
        }

        // ============================================
        // INITIALIZATION - ALL RESEARCH TREES
        // ============================================

        private void InitializeResearchTree()
        {
            // ==========================================
            // SURVIVAL TREE (All paths)
            // Core survival skills for the Exclusion Zone
            // ==========================================

            // --- TIER 1: Basics ---
            AddNode(new ResearchNode("survival_basics", "Survival Basics", ResearchCategory.Survival)
            {
                Description = "Fundamental techniques for surviving in the Zone. Every Changed learns these from birth.",
                Tier = 1,
                ResearchTime = 30,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 5 },
                UnlocksRecipes = new List<string> { "campfire_craft", "torch_craft" },
                StatBonuses = new Dictionary<string, float> { ["HungerRate"] = -0.1f }
            });

            AddNode(new ResearchNode("water_purification", "Water Purification", ResearchCategory.Survival)
            {
                Description = "Methods to filter Void-tainted water. Essential in the corrupted Zone.",
                Tier = 1,
                Prerequisites = new List<string> { "survival_basics" },
                ResearchTime = 45,
                ResourceCost = new Dictionary<string, int> { ["cloth"] = 5, ["scrap_metal"] = 3 },
                UnlocksRecipes = new List<string> { "water_filter_craft" },
                StatBonuses = new Dictionary<string, float> { ["ThirstRate"] = -0.1f }
            });

            // --- TIER 2: Specialized Survival ---
            AddNode(new ResearchNode("advanced_medicine", "Advanced Medicine", ResearchCategory.Survival)
            {
                Description = "Create effective healing items from Zone flora and salvaged supplies.",
                Tier = 2,
                Prerequisites = new List<string> { "survival_basics" },
                ResearchTime = 90,
                ResourceCost = new Dictionary<string, int> { ["cloth"] = 10, ["herbs"] = 5 },
                RequiredLevel = 3,
                UnlocksRecipes = new List<string> { "medkit_craft", "antidote_craft" }
            });

            AddNode(new ResearchNode("preservation", "Food Preservation", ResearchCategory.Survival)
            {
                Description = "Techniques to keep food from spoiling in the Zone's unpredictable conditions.",
                Tier = 2,
                Prerequisites = new List<string> { "water_purification" },
                ResearchTime = 60,
                ResourceCost = new Dictionary<string, int> { ["salt"] = 5, ["wood"] = 10 },
                UnlocksStructures = new List<string> { "Smokehouse", "IceBox" },
                StatBonuses = new Dictionary<string, float> { ["FoodDecayRate"] = -0.25f }
            });

            AddNode(new ResearchNode("radiation_resistance", "Radiation Resistance", ResearchCategory.Survival)
            {
                Description = "Understanding the Zone's radiation. Protective gear and treatments for exposure.",
                Tier = 2,
                Prerequisites = new List<string> { "survival_basics" },
                ResearchTime = 75,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 10, ["cloth"] = 8 },
                RequiredLevel = 3,
                UnlocksRecipes = new List<string> { "rad_suit_craft", "rad_away_craft" },
                StatBonuses = new Dictionary<string, float> { ["RadiationResistance"] = 0.15f }
            });

            AddNode(new ResearchNode("scavengers_eye", "Scavenger's Eye", ResearchCategory.Survival)
            {
                Description = "Learn to spot valuable salvage that others miss. The Zone rewards the observant.",
                Tier = 2,
                Prerequisites = new List<string> { "survival_basics" },
                ResearchTime = 60,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 5, ["components"] = 3 },
                RequiredLevel = 2,
                StatBonuses = new Dictionary<string, float> { ["LootQuality"] = 0.15f, ["DetectionRange"] = 2f }
            });

            // --- TIER 3: Mastery ---
            AddNode(new ResearchNode("shelter_mastery", "Shelter Mastery", ResearchCategory.Survival)
            {
                Description = "Build stronger, more efficient shelters to weather the Zone's dangers.",
                Tier = 3,
                Prerequisites = new List<string> { "preservation" },
                ResearchTime = 120,
                ResourceCost = new Dictionary<string, int> { ["wood"] = 30, ["scrap_metal"] = 15 },
                RequiredLevel = 5,
                UnlocksStructures = new List<string> { "ReinforcedWall", "InsulatedBed" },
                StatBonuses = new Dictionary<string, float> { ["RestEfficiency"] = 0.25f }
            });

            AddNode(new ResearchNode("temperature_adaptation", "Temperature Adaptation", ResearchCategory.Survival)
            {
                Description = "Gear and techniques to survive the Zone's extreme temperature swings.",
                Tier = 3,
                Prerequisites = new List<string> { "radiation_resistance", "shelter_mastery" },
                ResearchTime = 100,
                ResourceCost = new Dictionary<string, int> { ["cloth"] = 20, ["leather"] = 10 },
                RequiredLevel = 5,
                UnlocksRecipes = new List<string> { "thermal_suit_craft", "cooling_vest_craft" },
                StatBonuses = new Dictionary<string, float> { ["TemperatureResistance"] = 0.25f }
            });

            // --- TIER 4: Expert ---
            AddNode(new ResearchNode("zone_navigation", "Zone Navigation", ResearchCategory.Survival)
            {
                Description = "Master navigation through anomaly fields and corrupted terrain.",
                Tier = 4,
                Prerequisites = new List<string> { "temperature_adaptation", "scavengers_eye" },
                ResearchTime = 150,
                ResourceCost = new Dictionary<string, int> { ["components"] = 15, ["anomaly_shard"] = 5 },
                RequiredLevel = 7,
                StatBonuses = new Dictionary<string, float> { ["MovementSpeed"] = 0.15f, ["AnomalyDetection"] = 0.3f },
                UnlocksAbility = "detect_anomaly"
            });

            // ==========================================
            // COMBAT TREE (All paths)
            // Universal combat techniques
            // ==========================================

            // --- TIER 1: Fundamentals ---
            AddNode(new ResearchNode("combat_basics", "Combat Training", ResearchCategory.Combat)
            {
                Description = "Basic combat techniques passed down through generations of Changed warriors.",
                Tier = 1,
                ResearchTime = 45,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 8 },
                UnlocksRecipes = new List<string> { "knife_craft", "club_craft" },
                StatBonuses = new Dictionary<string, float> { ["BaseDamage"] = 1f }
            });

            AddNode(new ResearchNode("armor_crafting", "Armor Crafting", ResearchCategory.Combat)
            {
                Description = "Create protective gear from scavenged materials and mutant hides.",
                Tier = 1,
                Prerequisites = new List<string> { "combat_basics" },
                ResearchTime = 60,
                ResourceCost = new Dictionary<string, int> { ["cloth"] = 10, ["scrap_metal"] = 10 },
                UnlocksRecipes = new List<string> { "leather_armor_craft", "scrap_helmet_craft" }
            });

            // --- TIER 2: Specialization ---
            AddNode(new ResearchNode("weapon_smithing", "Weapon Smithing", ResearchCategory.Combat)
            {
                Description = "Forge superior weapons from salvaged metal. Quality kills.",
                Tier = 2,
                Prerequisites = new List<string> { "combat_basics" },
                ResearchTime = 90,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 20, ["components"] = 5 },
                RequiredLevel = 3,
                UnlocksRecipes = new List<string> { "machete_craft", "spear_craft" },
                UnlocksStructures = new List<string> { "Forge" }
            });

            AddNode(new ResearchNode("ranged_mastery", "Ranged Mastery", ResearchCategory.Combat)
            {
                Description = "Master the art of ranged combat. Essential for surviving Triad patrols.",
                Tier = 2,
                Prerequisites = new List<string> { "combat_basics" },
                ResearchTime = 75,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 15, ["wood"] = 10 },
                RequiredLevel = 3,
                UnlocksRecipes = new List<string> { "bow_craft", "crossbow_craft" },
                StatBonuses = new Dictionary<string, float> { ["RangedAccuracy"] = 0.1f }
            });

            AddNode(new ResearchNode("defensive_stance", "Defensive Techniques", ResearchCategory.Combat)
            {
                Description = "Learn to minimize damage and hold your ground against overwhelming odds.",
                Tier = 2,
                Prerequisites = new List<string> { "armor_crafting" },
                ResearchTime = 80,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 15, ["leather"] = 10 },
                RequiredLevel = 3,
                UnlocksRecipes = new List<string> { "shield_craft" },
                StatBonuses = new Dictionary<string, float> { ["BlockChance"] = 0.1f, ["DamageReduction"] = 0.05f }
            });

            // --- TIER 3: Advanced ---
            AddNode(new ResearchNode("tactical_training", "Tactical Training", ResearchCategory.Combat)
            {
                Description = "Advanced combat maneuvers. Outthink your enemies.",
                Tier = 3,
                Prerequisites = new List<string> { "weapon_smithing", "armor_crafting" },
                ResearchTime = 120,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 15, ["cloth"] = 15 },
                RequiredLevel = 5,
                StatBonuses = new Dictionary<string, float> { ["CritChance"] = 0.05f, ["DodgeChance"] = 0.05f }
            });

            AddNode(new ResearchNode("critical_strikes", "Critical Strikes", ResearchCategory.Combat)
            {
                Description = "Target weak points for devastating damage. Every enemy has a vulnerability.",
                Tier = 3,
                Prerequisites = new List<string> { "weapon_smithing", "ranged_mastery" },
                ResearchTime = 100,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 20, ["components"] = 8 },
                RequiredLevel = 5,
                StatBonuses = new Dictionary<string, float> { ["CritChance"] = 0.08f, ["CritDamage"] = 0.25f }
            });

            // --- TIER 4: Expert ---
            AddNode(new ResearchNode("battle_hardened", "Battle Hardened", ResearchCategory.Combat)
            {
                Description = "Years of Zone combat have made you nearly unstoppable.",
                Tier = 4,
                Prerequisites = new List<string> { "tactical_training", "critical_strikes" },
                ResearchTime = 180,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 30, ["leather"] = 20, ["components"] = 10 },
                RequiredLevel = 8,
                StatBonuses = new Dictionary<string, float>
                {
                    ["MaxHealth"] = 25f,
                    ["BaseDamage"] = 3f,
                    ["DodgeChance"] = 0.05f
                },
                UnlocksAbility = "second_wind"
            });

            // ==========================================
            // TINKER TREE (Tinker path only)
            // Technology salvaged from The Triad
            // ==========================================

            // --- TIER 1: Fundamentals ---
            AddNode(new ResearchNode("tinker_fundamentals", "Tinker Fundamentals", ResearchCategory.Tinker)
            {
                Description = "Basic principles of pre-Severance technology. The foundation of all Tinker science.",
                Tier = 1,
                RequiredPath = SciencePath.Tinker,
                ResearchTime = 60,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 10, ["components"] = 3 },
                UnlocksRecipes = new List<string> { "repair_kit_craft" },
                UnlocksStructures = new List<string> { "Workbench" }
            });

            // --- TIER 2: Branches ---
            AddNode(new ResearchNode("electronics", "Electronics", ResearchCategory.Tinker)
            {
                Description = "Salvage and repair electronic devices. The Syndicate guards this knowledge jealously.",
                Tier = 2,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "tinker_fundamentals" },
                ResearchTime = 90,
                ResourceCost = new Dictionary<string, int> { ["scrap_electronics"] = 10, ["components"] = 5 },
                RequiredLevel = 3,
                UnlocksRecipes = new List<string> { "flashlight_craft", "radio_craft" },
                UnlocksStructures = new List<string> { "Generator" }
            });

            AddNode(new ResearchNode("ballistics", "Ballistics", ResearchCategory.Tinker)
            {
                Description = "Construct and maintain firearms. Sanctum tech, dangerous to possess.",
                Tier = 2,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "tinker_fundamentals", "weapon_smithing" },
                ResearchTime = 120,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 25, ["components"] = 10 },
                RequiredLevel = 4,
                UnlocksRecipes = new List<string> { "pistol_craft", "ammo_craft" }
            });

            AddNode(new ResearchNode("medical_tech", "Medical Technology", ResearchCategory.Tinker)
            {
                Description = "Verdant Order healing technology. Effective but morally questionable origins.",
                Tier = 2,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "tinker_fundamentals", "advanced_medicine" },
                ResearchTime = 100,
                ResourceCost = new Dictionary<string, int> { ["scrap_electronics"] = 8, ["components"] = 6, ["herbs"] = 10 },
                RequiredLevel = 4,
                UnlocksRecipes = new List<string> { "stimpak_craft", "auto_injector_craft" },
                StatBonuses = new Dictionary<string, float> { ["HealingEfficiency"] = 0.2f }
            });

            // --- TIER 3: Advanced Tech ---
            AddNode(new ResearchNode("automation", "Automation", ResearchCategory.Tinker)
            {
                Description = "Build machines that work for you. Syndicate factory secrets.",
                Tier = 3,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "electronics" },
                ResearchTime = 150,
                ResourceCost = new Dictionary<string, int> { ["scrap_electronics"] = 20, ["components"] = 15, ["scrap_metal"] = 20 },
                RequiredLevel = 6,
                UnlocksStructures = new List<string> { "AutoTurret", "WaterPump", "Fabricator" }
            });

            AddNode(new ResearchNode("cybernetics", "Cybernetics", ResearchCategory.Tinker)
            {
                Description = "Integrate machine with flesh. Iron Syndicate implant technology.",
                Tier = 3,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "electronics", "medical_tech" },
                ResearchTime = 140,
                ResourceCost = new Dictionary<string, int> { ["scrap_electronics"] = 15, ["components"] = 12, ["scrap_metal"] = 10 },
                RequiredLevel = 6,
                UnlocksRecipes = new List<string> { "reflex_implant_craft", "dermal_plating_craft" },
                StatBonuses = new Dictionary<string, float> { ["ImplantSlots"] = 1f }
            });

            AddNode(new ResearchNode("robotics", "Robotics", ResearchCategory.Tinker)
            {
                Description = "Construct autonomous drones and robots. Sanctum military secrets.",
                Tier = 3,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "automation" },
                ResearchTime = 160,
                ResourceCost = new Dictionary<string, int> { ["scrap_electronics"] = 25, ["components"] = 20, ["scrap_metal"] = 15 },
                RequiredLevel = 7,
                UnlocksRecipes = new List<string> { "scout_drone_craft", "repair_bot_craft" },
                UnlocksStructures = new List<string> { "DroneStation" }
            });

            AddNode(new ResearchNode("military_tech", "Military Technology", ResearchCategory.Tinker)
            {
                Description = "United Sanctum military hardware. Extremely dangerous, extremely illegal.",
                Tier = 3,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "ballistics" },
                ResearchTime = 150,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 30, ["components"] = 15, ["scrap_electronics"] = 10 },
                RequiredLevel = 6,
                UnlocksRecipes = new List<string> { "assault_rifle_craft", "frag_grenade_craft" },
                StatBonuses = new Dictionary<string, float> { ["RangedDamage"] = 0.15f }
            });

            // --- TIER 4: High Tech ---
            AddNode(new ResearchNode("power_armor", "Power Armor", ResearchCategory.Tinker)
            {
                Description = "The pinnacle of pre-Severance military technology. Sanctum's elite guard wear these.",
                Tier = 4,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "automation", "military_tech", "tactical_training" },
                ResearchTime = 300,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 50, ["components"] = 30, ["scrap_electronics"] = 25 },
                RequiredLevel = 8,
                UnlocksRecipes = new List<string> { "power_armor_craft" },
                StatBonuses = new Dictionary<string, float> { ["MaxHealth"] = 50f, ["Armor"] = 10f }
            });

            AddNode(new ResearchNode("relic_study", "Relic Study", ResearchCategory.Tinker)
            {
                Description = "Understand Aethelgard technology. 400 years old, still more advanced than anything today.",
                Tier = 4,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "electronics", "cybernetics" },
                ResearchTime = 200,
                ResourceCost = new Dictionary<string, int> { ["scrap_electronics"] = 30, ["components"] = 20, ["relic_scrap"] = 5 },
                RequiredLevel = 8,
                UnlocksRecipes = new List<string> { "relic_repair_craft" },
                StatBonuses = new Dictionary<string, float> { ["ResearchSpeed"] = 0.2f, ["TechSalvageChance"] = 0.3f }
            });

            // --- TIER 5: Ultimate Tech ---
            AddNode(new ResearchNode("energy_weapons", "Energy Weapons", ResearchCategory.Tinker)
            {
                Description = "Harness energy for devastating weapons. Aethelgard's military legacy.",
                Tier = 5,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "power_armor", "relic_study" },
                ResearchTime = 360,
                ResourceCost = new Dictionary<string, int> { ["scrap_electronics"] = 40, ["components"] = 25, ["energy_cell"] = 10 },
                RequiredLevel = 10,
                UnlocksRecipes = new List<string> { "laser_rifle_craft", "plasma_cutter_craft" }
            });

            AddNode(new ResearchNode("neural_interface", "Neural Interface", ResearchCategory.Tinker)
            {
                Description = "Direct brain-machine connection. The ultimate fusion of flesh and technology.",
                Tier = 5,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "cybernetics", "relic_study" },
                ResearchTime = 400,
                ResourceCost = new Dictionary<string, int> { ["scrap_electronics"] = 50, ["components"] = 30, ["brain_tissue"] = 3 },
                RequiredLevel = 10,
                UnlocksRecipes = new List<string> { "neural_jack_craft" },
                StatBonuses = new Dictionary<string, float>
                {
                    ["INT"] = 2f,
                    ["PER"] = 2f,
                    ["ResearchSpeed"] = 0.25f
                },
                UnlocksAbility = "machine_link"
            });

            // ==========================================
            // DARK SCIENCE TREE (Dark path only)
            // Void-based powers and mutations
            // ==========================================

            // --- TIER 1: Initiation ---
            AddNode(new ResearchNode("dark_initiation", "Dark Initiation", ResearchCategory.Dark)
            {
                Description = "Begin to understand the anomalies that mutated your ancestors. The Void calls.",
                Tier = 1,
                RequiredPath = SciencePath.Dark,
                ResearchTime = 60,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 3, ["bone"] = 5 },
                UnlocksMutations = new List<MutationType> { MutationType.NightVision },
                UnlocksStructures = new List<string> { "RitualCircle" }
            });

            // --- TIER 2: Branches ---
            AddNode(new ResearchNode("mutation_control", "Mutation Control", ResearchCategory.Dark)
            {
                Description = "Learn to guide your body's mutations. Shape your evolution.",
                Tier = 2,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "dark_initiation" },
                ResearchTime = 90,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 5, ["mutagen"] = 3 },
                RequiredLevel = 3,
                UnlocksMutations = new List<MutationType> { MutationType.Regeneration, MutationType.ThickHide },
                StatBonuses = new Dictionary<string, float> { ["MutationEfficiency"] = 0.15f }
            });

            AddNode(new ResearchNode("flesh_crafting", "Flesh Crafting", ResearchCategory.Dark)
            {
                Description = "Shape living tissue into tools and weapons. Grotesque but effective.",
                Tier = 2,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "dark_initiation" },
                ResearchTime = 120,
                ResourceCost = new Dictionary<string, int> { ["bone"] = 15, ["sinew"] = 10, ["anomaly_shard"] = 5 },
                RequiredLevel = 4,
                UnlocksRecipes = new List<string> { "bone_blade_craft", "chitin_armor_craft" }
            });

            AddNode(new ResearchNode("void_attunement", "Void Attunement", ResearchCategory.Dark)
            {
                Description = "Develop resistance to Void corruption. Become one with the anomaly.",
                Tier = 2,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "dark_initiation" },
                ResearchTime = 100,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 8, ["void_ichor"] = 3 },
                RequiredLevel = 3,
                StatBonuses = new Dictionary<string, float>
                {
                    ["VoidResistance"] = 0.2f,
                    ["CorruptionRate"] = -0.15f
                },
                UnlocksMutations = new List<MutationType> { MutationType.ToxinFilter }
            });

            AddNode(new ResearchNode("blood_rituals", "Blood Rituals", ResearchCategory.Dark)
            {
                Description = "Sacrifice vitality for power. The Void Cult's oldest traditions.",
                Tier = 2,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "dark_initiation" },
                ResearchTime = 110,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 5, ["bone"] = 10, ["blood_sample"] = 5 },
                RequiredLevel = 4,
                UnlocksRecipes = new List<string> { "blood_vial_craft", "sacrifice_dagger_craft" },
                UnlocksAbility = "blood_sacrifice"
            });

            // --- TIER 3: Advanced Dark Science ---
            AddNode(new ResearchNode("psionic_awakening", "Psionic Awakening", ResearchCategory.Dark)
            {
                Description = "Unlock the latent psychic potential in mutant minds. The Void whispers secrets.",
                Tier = 3,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "mutation_control" },
                ResearchTime = 150,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 10, ["brain_tissue"] = 3 },
                RequiredLevel = 6,
                UnlocksMutations = new List<MutationType> { MutationType.FearAura, MutationType.Telepathy },
                UnlocksAbility = "mind_blast"
            });

            AddNode(new ResearchNode("reality_weaving", "Reality Weaving", ResearchCategory.Dark)
            {
                Description = "Manipulate the fabric of reality itself. Near the Epicenter, space is just a suggestion.",
                Tier = 3,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "void_attunement", "flesh_crafting" },
                ResearchTime = 160,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 12, ["reality_fragment"] = 2 },
                RequiredLevel = 6,
                StatBonuses = new Dictionary<string, float> { ["DodgeChance"] = 0.1f },
                UnlocksAbility = "phase_shift"
            });

            AddNode(new ResearchNode("void_summoning", "Void Summoning", ResearchCategory.Dark)
            {
                Description = "Call forth creatures from beyond the veil. Dangerous allies from the other side.",
                Tier = 3,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "blood_rituals" },
                ResearchTime = 170,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 15, ["void_ichor"] = 5, ["essence"] = 3 },
                RequiredLevel = 7,
                UnlocksRecipes = new List<string> { "summoning_focus_craft" },
                UnlocksAbility = "summon_voidling",
                UnlocksStructures = new List<string> { "SummoningCircle" }
            });

            // --- TIER 4: Master Dark Science ---
            AddNode(new ResearchNode("hive_connection", "Hive Connection", ResearchCategory.Dark)
            {
                Description = "Tap into the collective consciousness of mutant-kind. You are never alone.",
                Tier = 4,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "psionic_awakening", "flesh_crafting" },
                ResearchTime = 240,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 20, ["brain_tissue"] = 5, ["mutagen"] = 10 },
                RequiredLevel = 8,
                UnlocksMutations = new List<MutationType> { MutationType.VoidTouch },
                StatBonuses = new Dictionary<string, float> { ["XPGain"] = 0.25f, ["DetectionRange"] = 3f }
            });

            AddNode(new ResearchNode("corruption_mastery", "Corruption Mastery", ResearchCategory.Dark)
            {
                Description = "Master the Void's corruption instead of fearing it. Transform the curse into a blessing.",
                Tier = 4,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "void_attunement", "reality_weaving" },
                ResearchTime = 220,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 18, ["void_ichor"] = 8, ["mutagen"] = 5 },
                RequiredLevel = 8,
                StatBonuses = new Dictionary<string, float>
                {
                    ["VoidResistance"] = 0.3f,
                    ["VoidDamage"] = 0.25f,
                    ["CorruptionRate"] = -0.3f
                },
                UnlocksMutations = new List<MutationType> { MutationType.AcidBlood }
            });

            // --- TIER 5: Ultimate Dark Science ---
            AddNode(new ResearchNode("apotheosis", "Apotheosis", ResearchCategory.Dark)
            {
                Description = "Transcend your mortal form. Become something more than human, more than mutant.",
                Tier = 5,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "hive_connection", "corruption_mastery" },
                ResearchTime = 360,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 30, ["essence"] = 5, ["mutagen"] = 20 },
                RequiredLevel = 10,
                UnlocksMutations = new List<MutationType> { MutationType.UnstableForm },
                StatBonuses = new Dictionary<string, float>
                {
                    ["MaxHealth"] = 100f,
                    ["HealthRegen"] = 1f,
                    ["AllResistance"] = 0.1f
                }
            });

            AddNode(new ResearchNode("void_gate", "Void Gate", ResearchCategory.Dark)
            {
                Description = "Open a stable portal to the Void. What the Aethelgard Magi-Scientists failed to control.",
                Tier = 5,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "void_summoning", "corruption_mastery" },
                ResearchTime = 400,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 40, ["reality_fragment"] = 5, ["void_ichor"] = 15 },
                RequiredLevel = 10,
                UnlocksStructures = new List<string> { "VoidGate" },
                UnlocksAbility = "void_step",
                StatBonuses = new Dictionary<string, float>
                {
                    ["VoidDamage"] = 0.5f,
                    ["MovementSpeed"] = 0.2f
                }
            });

            // Update initial availability
            UpdateAvailability(1);

            System.Diagnostics.Debug.WriteLine($">>> ResearchSystem: Initialized {_nodes.Count} research nodes <<<");
        }

        private void AddNode(ResearchNode node)
        {
            _nodes[node.Id] = node;
        }

        // ============================================
        // ACCESS METHODS
        // ============================================

        public void SetPlayerPath(SciencePath path)
        {
            _playerPath = path;
            UpdateAvailability(1);
        }

        public ResearchNode GetNode(string id)
        {
            return _nodes.GetValueOrDefault(id);
        }

        public List<ResearchNode> GetAllNodes()
        {
            return _nodes.Values.ToList();
        }

        public List<ResearchNode> GetNodesByCategory(ResearchCategory category)
        {
            return _nodes.Values.Where(n => n.Category == category).OrderBy(n => n.Tier).ThenBy(n => n.Name).ToList();
        }

        public List<ResearchNode> GetAvailableNodes()
        {
            return _nodes.Values.Where(n => n.State == ResearchState.Available).ToList();
        }

        public List<ResearchNode> GetCompletedNodes()
        {
            return _nodes.Values.Where(n => n.State == ResearchState.Completed).ToList();
        }

        public ResearchNode GetCurrentResearch()
        {
            return _currentResearch != null ? GetNode(_currentResearch) : null;
        }

        public bool IsResearching => _currentResearch != null;

        // ============================================
        // AVAILABILITY CHECKING
        // ============================================

        public void UpdateAvailability(int playerLevel)
        {
            foreach (var node in _nodes.Values)
            {
                if (node.State == ResearchState.Completed || node.State == ResearchState.InProgress)
                    continue;

                // Check path requirement
                if (node.RequiredPath.HasValue && node.RequiredPath.Value != _playerPath)
                {
                    node.State = ResearchState.Locked;
                    continue;
                }

                // Check level requirement
                if (playerLevel < node.RequiredLevel)
                {
                    node.State = ResearchState.Locked;
                    continue;
                }

                // Check prerequisites
                bool prereqsMet = true;
                foreach (var prereq in node.Prerequisites)
                {
                    var prereqNode = GetNode(prereq);
                    if (prereqNode == null || prereqNode.State != ResearchState.Completed)
                    {
                        prereqsMet = false;
                        break;
                    }
                }

                node.State = prereqsMet ? ResearchState.Available : ResearchState.Locked;
            }
        }

        public bool CanStartResearch(string nodeId, Dictionary<string, int> playerResources)
        {
            var node = GetNode(nodeId);
            if (node == null) return false;
            if (node.State != ResearchState.Available) return false;
            if (_currentResearch != null) return false;

            foreach (var cost in node.ResourceCost)
            {
                if (!playerResources.ContainsKey(cost.Key) || playerResources[cost.Key] < cost.Value)
                    return false;
            }

            return true;
        }

        // ============================================
        // RESEARCH ACTIONS
        // ============================================

        public bool StartResearch(string nodeId, Action<string, int> consumeResource)
        {
            var node = GetNode(nodeId);
            if (node == null || node.State != ResearchState.Available) return false;
            if (_currentResearch != null) return false;

            // Consume resources
            foreach (var cost in node.ResourceCost)
            {
                consumeResource(cost.Key, cost.Value);
            }

            node.State = ResearchState.InProgress;
            node.Progress = 0f;
            _currentResearch = nodeId;

            OnResearchStarted?.Invoke(node);
            System.Diagnostics.Debug.WriteLine($">>> Started research: {node.Name} <<<");

            return true;
        }

        public void Update(float deltaTime)
        {
            if (_currentResearch == null) return;

            var node = GetNode(_currentResearch);
            if (node == null) return;

            node.Progress += deltaTime;
            OnResearchProgress?.Invoke(node, node.ProgressPercent);

            if (node.Progress >= node.ResearchTime)
            {
                CompleteResearch(node);
            }
        }

        private void CompleteResearch(ResearchNode node)
        {
            node.State = ResearchState.Completed;
            node.Progress = node.ResearchTime;
            _currentResearch = null;

            OnResearchCompleted?.Invoke(node);
            System.Diagnostics.Debug.WriteLine($">>> Research complete: {node.Name} <<<");

            UpdateAvailability(10);
        }

        public void CancelResearch()
        {
            if (_currentResearch == null) return;

            var node = GetNode(_currentResearch);
            if (node != null)
            {
                node.State = ResearchState.Available;
                node.Progress = 0f;
            }

            _currentResearch = null;
            System.Diagnostics.Debug.WriteLine(">>> Research cancelled <<<");
        }

        // ============================================
        // SAVE/LOAD
        // ============================================

        public List<string> GetCompletedResearchIds()
        {
            return _nodes.Values
                .Where(n => n.State == ResearchState.Completed)
                .Select(n => n.Id)
                .ToList();
        }

        public (string nodeId, float progress)? GetCurrentResearchProgress()
        {
            if (_currentResearch == null) return null;
            var node = GetNode(_currentResearch);
            return (node.Id, node.Progress);
        }

        public void RestoreResearch(List<string> completedIds, string currentId, float currentProgress, SciencePath path)
        {
            _playerPath = path;

            foreach (var node in _nodes.Values)
            {
                node.State = ResearchState.Locked;
                node.Progress = 0f;
            }

            foreach (var id in completedIds)
            {
                var node = GetNode(id);
                if (node != null)
                {
                    node.State = ResearchState.Completed;
                    node.Progress = node.ResearchTime;
                }
            }

            if (!string.IsNullOrEmpty(currentId))
            {
                var node = GetNode(currentId);
                if (node != null)
                {
                    node.State = ResearchState.InProgress;
                    node.Progress = currentProgress;
                    _currentResearch = currentId;
                }
            }

            UpdateAvailability(10);
        }

        public void Reset()
        {
            foreach (var node in _nodes.Values)
            {
                node.State = ResearchState.Locked;
                node.Progress = 0f;
            }
            _currentResearch = null;
            UpdateAvailability(1);
            System.Diagnostics.Debug.WriteLine(">>> ResearchSystem RESET <<<");
        }
    }
}