// Gameplay/Systems/QuestSystem.cs
// Quest definitions, tracking, and management
// EXPANDED: 50+ quests with faction reputation, main story, and faction quest lines

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MyRPG.Data;

namespace MyRPG.Gameplay.Systems
{
    // ============================================
    // QUEST ENUMS
    // ============================================
    
    public enum QuestType
    {
        Main,           // Main story quests
        Side,           // Optional side quests
        Bounty,         // Repeatable kill quests
        Faction,        // Faction-specific quests
        Exploration,    // Visit locations
        Hidden          // Secret quests discovered through gameplay
    }
    
    public enum QuestState
    {
        Unavailable,    // Prerequisites not met
        Available,      // Can be accepted
        Active,         // Currently tracking
        Completed,      // Objectives done, ready to turn in
        TurnedIn,       // Finished and rewarded
        Failed          // Failed (timed out, etc)
    }
    
    public enum ObjectiveType
    {
        Kill,           // Kill X enemies (any type)
        KillType,       // Kill X of specific enemy type
        KillFaction,    // Kill X enemies of a faction
        Collect,        // Collect X of item
        Deliver,        // Bring item to NPC
        TalkTo,         // Speak with NPC
        Explore,        // Visit a zone
        Build,          // Build a structure
        Craft,          // Craft an item
        Survive,        // Survive X days
        Research,       // Complete a research node
        ReachLevel      // Reach player level X
    }
    
    // ============================================
    // QUEST OBJECTIVE
    // ============================================
    
    public class QuestObjective
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public ObjectiveType Type { get; set; }
        
        // Target info (depends on type)
        public string TargetId { get; set; }      // Item ID, NPC ID, Zone ID, etc.
        public EnemyType? TargetEnemyType { get; set; }  // For KillType
        public FactionType? TargetFaction { get; set; }  // For KillFaction
        public int RequiredCount { get; set; } = 1;
        
        // Progress
        public int CurrentCount { get; set; } = 0;
        public bool IsComplete => CurrentCount >= RequiredCount;
        
        // Optional
        public bool IsOptional { get; set; } = false;
        public bool IsHidden { get; set; } = false;  // Reveal when previous done
        
        public QuestObjective(string id, ObjectiveType type, string description)
        {
            Id = id;
            Type = type;
            Description = description;
        }
        
        public string GetProgressText()
        {
            if (RequiredCount == 1)
            {
                return IsComplete ? "[X]" : "[ ]";
            }
            return $"({CurrentCount}/{RequiredCount})";
        }
    }
    
    // ============================================
    // QUEST REWARD
    // ============================================
    
    public class QuestReward
    {
        public int Gold { get; set; } = 0;
        public int XP { get; set; } = 0;
        public int MutationPoints { get; set; } = 0;
        public Dictionary<string, int> Items { get; set; } = new Dictionary<string, int>();
        public List<string> UnlockRecipes { get; set; } = new List<string>();
        
        // NEW: Faction reputation rewards
        public Dictionary<FactionType, int> ReputationChanges { get; set; } = new Dictionary<FactionType, int>();
        
        public string GetDisplayText()
        {
            var parts = new List<string>();
            if (Gold > 0) parts.Add($"{Gold} Gold");
            if (XP > 0) parts.Add($"{XP} XP");
            if (MutationPoints > 0) parts.Add($"{MutationPoints} MP");
            if (Items.Count > 0) parts.Add($"{Items.Count} Item(s)");
            if (ReputationChanges.Count > 0)
            {
                foreach (var rep in ReputationChanges)
                {
                    string sign = rep.Value >= 0 ? "+" : "";
                    parts.Add($"{sign}{rep.Value} {rep.Key}");
                }
            }
            return string.Join(", ", parts);
        }
    }
    
    // ============================================
    // QUEST DEFINITION
    // ============================================
    
    public class QuestDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public QuestType Type { get; set; }
        
        // Who gives/completes this quest
        public string GiverNPCId { get; set; }      // NPC who gives the quest
        public string TurnInNPCId { get; set; }     // NPC to turn in (null = same as giver)
        
        // Requirements
        public int RequiredLevel { get; set; } = 1;
        public List<string> RequiredQuests { get; set; } = new List<string>();  // Must complete first
        public string RequiredZone { get; set; }    // Must be in this zone to accept
        public FactionType? RequiredFaction { get; set; }  // Must have positive rep with this faction
        public SciencePath? RequiredSciencePath { get; set; }  // Must have chosen this path
        public int RequiredReputation { get; set; } = -100;  // Min reputation with RequiredFaction
        
        // Objectives
        public List<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();
        
        // Rewards
        public QuestReward Reward { get; set; } = new QuestReward();
        
        // Flags
        public bool IsRepeatable { get; set; } = false;
        public bool AutoComplete { get; set; } = false;  // Complete without turn-in
        public bool IsHidden { get; set; } = false;  // Not shown until discovered
        
        // Lore/flavor
        public string LoreText { get; set; }  // Additional story text
        public FactionType? QuestFaction { get; set; }  // Which faction this quest is for
        
        public QuestDefinition(string id, string name, QuestType type)
        {
            Id = id;
            Name = name;
            Type = type;
        }
    }
    
    // ============================================
    // QUEST INSTANCE (Active Quest)
    // ============================================
    
    public class QuestInstance
    {
        public string QuestId { get; set; }
        public QuestDefinition Definition { get; private set; }
        public QuestState State { get; set; }
        public List<QuestObjective> Objectives { get; set; }
        public DateTime StartedAt { get; set; }
        
        public bool IsComplete => Objectives.Where(o => !o.IsOptional).All(o => o.IsComplete);
        
        public QuestInstance(QuestDefinition definition)
        {
            QuestId = definition.Id;
            Definition = definition;
            State = QuestState.Active;
            StartedAt = DateTime.Now;
            
            // Clone objectives for tracking
            Objectives = definition.Objectives.Select(o => new QuestObjective(o.Id, o.Type, o.Description)
            {
                TargetId = o.TargetId,
                TargetEnemyType = o.TargetEnemyType,
                TargetFaction = o.TargetFaction,
                RequiredCount = o.RequiredCount,
                CurrentCount = 0,
                IsOptional = o.IsOptional,
                IsHidden = o.IsHidden
            }).ToList();
        }
        
        public QuestObjective GetObjective(string id)
        {
            return Objectives.FirstOrDefault(o => o.Id == id);
        }
    }
    
    // ============================================
    // QUEST SYSTEM
    // ============================================
    
    public class QuestSystem
    {
        private Dictionary<string, QuestDefinition> _definitions = new Dictionary<string, QuestDefinition>();
        private Dictionary<string, QuestInstance> _activeQuests = new Dictionary<string, QuestInstance>();
        private HashSet<string> _completedQuests = new HashSet<string>();
        private HashSet<string> _discoveredQuests = new HashSet<string>();  // For hidden quests
        
        // Events
        public event Action<QuestInstance> OnQuestStarted;
        public event Action<QuestInstance> OnQuestCompleted;
        public event Action<QuestInstance> OnQuestTurnedIn;
        public event Action<QuestInstance, QuestObjective> OnObjectiveProgress;
        public event Action<QuestInstance, QuestObjective> OnObjectiveComplete;
        
        public QuestSystem()
        {
            InitializeQuests();
        }
        
        // ============================================
        // QUEST DEFINITIONS
        // ============================================
        
        private void InitializeQuests()
        {
            // ==========================================
            // ACT 1: SURVIVAL & DISCOVERY (Main Quests)
            // ==========================================
            
            InitializeMainQuestsAct1();
            
            // ==========================================
            // ACT 2: FACTION ALLEGIANCE (Main Quests)
            // ==========================================
            
            InitializeMainQuestsAct2();
            
            // ==========================================
            // ACT 3: THE VOID THREAT (Main Quests)
            // ==========================================
            
            InitializeMainQuestsAct3();
            
            // ==========================================
            // FACTION QUESTS
            // ==========================================
            
            InitializeChangedQuests();      // Fellow mutants
            InitializeSanctumQuests();      // United Sanctum
            InitializeSyndicateQuests();    // Iron Syndicate
            InitializeVerdantQuests();      // Verdant Order
            InitializeVoidCultQuests();     // Cult of the Void
            InitializeGeneElderQuests();    // Gene Elders
            
            // ==========================================
            // BOUNTY QUESTS (Repeatable)
            // ==========================================
            
            InitializeBountyQuests();
            
            // ==========================================
            // SIDE QUESTS
            // ==========================================
            
            InitializeSideQuests();
            
            System.Diagnostics.Debug.WriteLine($">>> QuestSystem initialized with {_definitions.Count} quests <<<");
        }
        
        // ============================================
        // ACT 1: SURVIVAL & DISCOVERY
        // ============================================
        
        private void InitializeMainQuestsAct1()
        {
            // Quest 1: Tutorial / First steps
            AddQuest(new QuestDefinition("main_01_awakening", "Awakening", QuestType.Main)
            {
                Description = "You awaken in the wastes of Orodia, 400 years after The Severance tore reality apart. Your mutations mark you as one of The Changed - survivors who adapted to the Void's corruption. Find your bearings and learn to survive.",
                LoreText = "The Exclusion Zone was once the heart of Aethelgard, the greatest civilization of the old world. Now it is a wasteland of anomalies and mutations.",
                GiverNPCId = null,  // Auto-given
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_move", ObjectiveType.Survive, "Take your first steps in the wasteland")
                    {
                        RequiredCount = 0  // Auto-completes immediately - just flavor text
                    },
                    new QuestObjective("obj_kill", ObjectiveType.Kill, "Defend yourself - defeat an enemy")
                    {
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_pickup", ObjectiveType.Collect, "Scavenge - pick up any item")
                    {
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 100,
                    Gold = 25,
                    Items = new Dictionary<string, int> { ["bandage"] = 3, ["water_bottle"] = 2 }
                },
                AutoComplete = true
            });
            
            // Quest 2: Meet the locals
            AddQuest(new QuestDefinition("main_02_rusthollow", "Welcome to Rusthollow", QuestType.Main)
            {
                Description = "Rusthollow is one of the few safe havens in the Exclusion Zone. The traders here deal with all factions - for the right price. Speak with the local merchant to learn about the current state of Orodia.",
                LoreText = "Rusthollow was built in the shell of an old factory. Its walls keep out most threats, and its neutrality keeps the factions at bay.",
                GiverNPCId = null,
                RequiredQuests = new List<string> { "main_01_awakening" },
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_talk_trader", ObjectiveType.TalkTo, "Speak with the Rusthollow Trader")
                    {
                        TargetId = "trader_rusthollow",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_equip", ObjectiveType.Collect, "Equip a weapon (pick up if needed)")
                    {
                        TargetId = "any_weapon",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 150,
                    Gold = 50,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.Traders, 5 }
                    }
                }
            });
            
            // Quest 3: Survival basics
            AddQuest(new QuestDefinition("main_03_survival", "The Basics of Survival", QuestType.Main)
            {
                Description = "The wasteland is unforgiving. You'll need to manage your hunger, thirst, and rest to survive. The trader mentioned that scavenging the outer ruins yields good materials.",
                GiverNPCId = "trader_rusthollow",
                RequiredQuests = new List<string> { "main_02_rusthollow" },
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_food", ObjectiveType.Collect, "Find food (canned food or meat)")
                    {
                        RequiredCount = 3
                    },
                    new QuestObjective("obj_water", ObjectiveType.Collect, "Find water")
                    {
                        TargetId = "water_bottle",
                        RequiredCount = 2
                    },
                    new QuestObjective("obj_scrap", ObjectiveType.Collect, "Collect scrap metal")
                    {
                        TargetId = "scrap_metal",
                        RequiredCount = 5
                    }
                },
                Reward = new QuestReward
                {
                    XP = 200,
                    Gold = 75,
                    Items = new Dictionary<string, int> { ["med_kit"] = 1 }
                }
            });
            
            // Quest 4: First exploration
            AddQuest(new QuestDefinition("main_04_exploration", "Into the Ruins", QuestType.Main)
            {
                Description = "The Outer Ruins hold the decayed remains of old Aethelgard. Dangerous, but full of salvage. The trader wants you to scout the northern ruins and report what you find.",
                LoreText = "Before The Severance, these ruins were thriving city blocks. Now they're home to raiders and worse.",
                GiverNPCId = "trader_rusthollow",
                RequiredQuests = new List<string> { "main_03_survival" },
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_ruins_north", ObjectiveType.Explore, "Explore the Outer Ruins - North")
                    {
                        TargetId = "outer_ruins_north",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_kill_raiders", ObjectiveType.KillType, "Clear out Raiders")
                    {
                        TargetEnemyType = EnemyType.Raider,
                        RequiredCount = 3
                    }
                },
                Reward = new QuestReward
                {
                    XP = 250,
                    Gold = 100,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.TheChanged, 5 },
                        { FactionType.Traders, 3 }
                    }
                }
            });
            
            // Quest 5: Build your base
            AddQuest(new QuestDefinition("main_05_homestead", "A Place to Call Home", QuestType.Main)
            {
                Description = "Survival means having shelter. The trader suggests you establish a small base - even a simple campfire and storage chest would be a start.",
                GiverNPCId = "trader_rusthollow",
                RequiredQuests = new List<string> { "main_04_exploration" },
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_campfire", ObjectiveType.Build, "Build a Campfire")
                    {
                        TargetId = "Campfire",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_storage", ObjectiveType.Build, "Build a Storage Chest")
                    {
                        TargetId = "StorageChest",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_bed", ObjectiveType.Build, "Build a Bedroll")
                    {
                        TargetId = "Bedroll",
                        RequiredCount = 1,
                        IsOptional = true
                    }
                },
                Reward = new QuestReward
                {
                    XP = 300,
                    Gold = 50,
                    UnlockRecipes = new List<string> { "workbench_craft" },
                    Items = new Dictionary<string, int> { ["wood"] = 20, ["scrap_metal"] = 10 }
                }
            });
        }
        
        // ============================================
        // ACT 2: FACTION ALLEGIANCE
        // ============================================
        
        private void InitializeMainQuestsAct2()
        {
            // Quest 6: The factions of Orodia
            AddQuest(new QuestDefinition("main_06_factions", "The Powers That Be", QuestType.Main)
            {
                Description = "Orodia is divided between powerful factions, each with their own agenda. The trader has contacts with the Iron Syndicate - tech merchants who value profit above all. Meeting them could open new opportunities... or dangers.",
                LoreText = "The five great factions emerged from the chaos after The Severance. Each claims to have the answer to humanity's survival.",
                GiverNPCId = "trader_rusthollow",
                RequiredQuests = new List<string> { "main_05_homestead" },
                RequiredLevel = 3,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_syndicate", ObjectiveType.Explore, "Travel to Syndicate Post Seven")
                    {
                        TargetId = "syndicate_post",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_talk_syndicate", ObjectiveType.TalkTo, "Speak with a Syndicate Merchant")
                    {
                        TargetId = "trader_syndicate_post",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 350,
                    Gold = 150,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.IronSyndicate, 10 }
                    }
                }
            });
            
            // Quest 7: Sanctum threat
            AddQuest(new QuestDefinition("main_07_sanctum_threat", "The Purifiers", QuestType.Main)
            {
                Description = "The United Sanctum views all mutants as abominations to be purged. Their Forward Base Purity lies to the east, a constant threat. The Syndicate wants intelligence on their movements.",
                LoreText = "The United Sanctum believes technology can restore humanity to its 'pure' form. They see mutation as corruption, not evolution.",
                GiverNPCId = "trader_syndicate_post",
                RequiredQuests = new List<string> { "main_06_factions" },
                RequiredLevel = 4,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_scout", ObjectiveType.Explore, "Scout Forward Base Purity (approach carefully)")
                    {
                        TargetId = "forward_base_purity",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_survive", ObjectiveType.Kill, "Survive the encounter (or avoid detection)")
                    {
                        RequiredCount = 1,
                        IsOptional = true
                    }
                },
                Reward = new QuestReward
                {
                    XP = 400,
                    Gold = 200,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.IronSyndicate, 10 },
                        { FactionType.UnitedSanctum, -5 }
                    }
                }
            });
            
            // Quest 8: The Gene Elders
            AddQuest(new QuestDefinition("main_08_gene_elders", "The Ancient Ones", QuestType.Main)
            {
                Description = "Deep in the Inner Ruins stands The Spire - home of the Gene Elders. These ancient mutants have lived since before The Severance. They may hold answers about your mutations... and Orodia's fate.",
                LoreText = "The Gene Elders remember the old world. They were among the first to change, and they have watched the factions rise and fall.",
                GiverNPCId = "trader_syndicate_post",
                RequiredQuests = new List<string> { "main_07_sanctum_threat" },
                RequiredLevel = 5,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_inner_ruins", ObjectiveType.Explore, "Navigate through Inner Ruins - South")
                    {
                        TargetId = "inner_ruins_south",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_spire", ObjectiveType.Explore, "Reach The Spire")
                    {
                        TargetId = "the_spire",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_elder", ObjectiveType.TalkTo, "Seek audience with a Gene Elder")
                    {
                        TargetId = "elder_the_spire",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 500,
                    Gold = 100,
                    MutationPoints = 2,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.GeneElders, 15 },
                        { FactionType.TheChanged, 5 }
                    }
                }
            });
            
            // Quest 9: Choose your path
            AddQuest(new QuestDefinition("main_09_science_path", "The Dual Sciences", QuestType.Main)
            {
                Description = "The Gene Elders speak of two paths to power: Tinker Science - the preserved technology of the old world, and Dark Science - harnessing the Void's corruption. You must choose which path to walk.",
                LoreText = "Tinker Science is reliable but limited. Dark Science is powerful but dangerous. Few master both.",
                GiverNPCId = "elder_the_spire",
                RequiredQuests = new List<string> { "main_08_gene_elders" },
                RequiredLevel = 6,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_research", ObjectiveType.Research, "Complete your first research")
                    {
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_craft_advanced", ObjectiveType.Craft, "Craft an advanced item")
                    {
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 500,
                    MutationPoints = 1,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.GeneElders, 10 }
                    }
                }
            });
            
            // Quest 10: The Verdant Order
            AddQuest(new QuestDefinition("main_10_verdant", "The Bio-Engineers", QuestType.Main)
            {
                Description = "The Gene Elders warn of the Verdant Order - fanatics who believe they can 'perfect' mutation through forced evolution. Their laboratory, The Nursery, lies to the east. Investigate their experiments.",
                LoreText = "The Verdant Order split from the Gene Elders centuries ago. They view mutation as raw material to be shaped, not accepted.",
                GiverNPCId = "elder_the_spire",
                RequiredQuests = new List<string> { "main_09_science_path" },
                RequiredLevel = 7,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_nursery", ObjectiveType.Explore, "Infiltrate The Nursery")
                    {
                        TargetId = "the_nursery",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_verdant_kill", ObjectiveType.KillFaction, "Deal with Verdant Collectors")
                    {
                        TargetFaction = FactionType.VerdantOrder,
                        RequiredCount = 3
                    },
                    new QuestObjective("obj_specimen", ObjectiveType.Collect, "Recover experiment data (anomaly shards)")
                    {
                        TargetId = "anomaly_shard",
                        RequiredCount = 3,
                        IsOptional = true
                    }
                },
                Reward = new QuestReward
                {
                    XP = 600,
                    Gold = 250,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.VerdantOrder, -15 },
                        { FactionType.GeneElders, 10 },
                        { FactionType.TheChanged, 10 }
                    }
                }
            });
        }
        
        // ============================================
        // ACT 3: THE VOID THREAT
        // ============================================
        
        private void InitializeMainQuestsAct3()
        {
            // Quest 11: The Void stirs
            AddQuest(new QuestDefinition("main_11_void_signs", "Signs of the Void", QuestType.Main)
            {
                Description = "Something is wrong. The anomalies are growing stronger, reality is thinning. The Gene Elders sense it - The Void that caused The Severance is stirring again. You must investigate.",
                LoreText = "400 years ago, The Severance opened a door to The Void. That door was never fully closed.",
                GiverNPCId = "elder_the_spire",
                RequiredQuests = new List<string> { "main_10_verdant" },
                RequiredLevel = 8,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_forest", ObjectiveType.Explore, "Investigate anomalies in the Dark Forest")
                    {
                        TargetId = "dark_forest",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_void_kill", ObjectiveType.KillType, "Destroy Void-Touched creatures")
                    {
                        TargetEnemyType = EnemyType.VoidWraith,
                        RequiredCount = 2
                    },
                    new QuestObjective("obj_shards", ObjectiveType.Collect, "Collect Void essence (anomaly shards)")
                    {
                        TargetId = "anomaly_shard",
                        RequiredCount = 5
                    }
                },
                Reward = new QuestReward
                {
                    XP = 700,
                    Gold = 200,
                    MutationPoints = 1,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.TheChanged, 15 }
                    }
                }
            });
            
            // Quest 12: The Cult
            AddQuest(new QuestDefinition("main_12_void_cult", "Temple of the Consuming Void", QuestType.Main)
            {
                Description = "The disturbances lead to a hidden temple - home of the Void Cult. These fanatics worship The Void, seeking to complete what The Severance started. They must be stopped... or perhaps you could learn their secrets.",
                LoreText = "The Void Cult believes that dissolution into The Void is the ultimate transcendence. They seek to tear reality apart.",
                GiverNPCId = "elder_the_spire",
                RequiredQuests = new List<string> { "main_11_void_signs" },
                RequiredLevel = 9,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_temple", ObjectiveType.Explore, "Enter the Temple of the Consuming Void")
                    {
                        TargetId = "void_temple",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_cult_kill", ObjectiveType.Kill, "Confront the cultists")
                    {
                        RequiredCount = 5
                    }
                },
                Reward = new QuestReward
                {
                    XP = 800,
                    Gold = 300,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.VoidCult, -20 },
                        { FactionType.TheChanged, 10 }
                    }
                }
            });
            
            // Quest 13: Into the Deep Zone
            AddQuest(new QuestDefinition("main_13_deep_zone", "Beyond the Boundary", QuestType.Main)
            {
                Description = "The Deep Zone - where reality breaks down entirely. Few who enter return unchanged. The Elders say the source of the new disturbance lies at the heart of the Exclusion Zone. You must go where others dare not.",
                LoreText = "The Deep Zone is where The Severance was strongest. Time and space do not behave normally there.",
                GiverNPCId = "elder_the_spire",
                RequiredQuests = new List<string> { "main_12_void_cult" },
                RequiredLevel = 10,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_deep_south", ObjectiveType.Explore, "Enter the Deep Zone - South")
                    {
                        TargetId = "deep_zone_south",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_horror", ObjectiveType.KillType, "Survive encounters with Void Horrors")
                    {
                        TargetEnemyType = EnemyType.VoidHorror,
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 1000,
                    Gold = 400,
                    MutationPoints = 2,
                    Items = new Dictionary<string, int> { ["essence"] = 5 }
                }
            });
            
            // Quest 14: The Wound
            AddQuest(new QuestDefinition("main_14_the_wound", "The Wound in Reality", QuestType.Main)
            {
                Description = "At the center of the Deep Zone lies The Wound - the exact point where The Severance occurred. The fabric of reality is thin here. You feel the Void calling to you. This is where it all began... and where it might end.",
                LoreText = "The Wound has never healed. For 400 years, it has slowly been growing.",
                GiverNPCId = null,  // Auto after previous
                RequiredQuests = new List<string> { "main_13_deep_zone" },
                RequiredLevel = 12,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_wound", ObjectiveType.Explore, "Reach The Wound")
                    {
                        TargetId = "the_wound",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_void_spawns", ObjectiveType.KillType, "Defeat the Void's guardians")
                    {
                        TargetEnemyType = EnemyType.VoidHorror,
                        RequiredCount = 2
                    }
                },
                Reward = new QuestReward
                {
                    XP = 1500,
                    Gold = 500,
                    MutationPoints = 3
                }
            });
            
            // Quest 15: Final confrontation
            AddQuest(new QuestDefinition("main_15_epicenter", "The Epicenter", QuestType.Main)
            {
                Description = "The Epicenter awaits. Here, at the very heart of The Severance, you will face the source of the Void's corruption. Your mutations, your science, your choices - they have all led to this moment. The fate of Orodia rests on what you do next.",
                LoreText = "Some say The Severance was an accident. Others say it was deliberate. The truth lies at the Epicenter.",
                GiverNPCId = null,
                RequiredQuests = new List<string> { "main_14_the_wound" },
                RequiredLevel = 15,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_epicenter", ObjectiveType.Explore, "Enter The Epicenter")
                    {
                        TargetId = "the_epicenter",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_final", ObjectiveType.Kill, "Confront the source")
                    {
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 3000,
                    Gold = 1000,
                    MutationPoints = 5
                }
            });
        }
        
        // ============================================
        // THE CHANGED (Fellow Mutants) QUESTS
        // ============================================
        
        private void InitializeChangedQuests()
        {
            AddQuest(new QuestDefinition("changed_01_mutual_aid", "Mutual Aid", QuestType.Faction)
            {
                Description = "A group of Changed are pinned down by Sanctum troops in the ruins. They're our people - we don't leave our own behind.",
                QuestFaction = FactionType.TheChanged,
                GiverNPCId = "trader_rusthollow",
                RequiredLevel = 3,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_kill_sanctum", ObjectiveType.KillFaction, "Drive off Sanctum forces")
                    {
                        TargetFaction = FactionType.UnitedSanctum,
                        RequiredCount = 4
                    }
                },
                Reward = new QuestReward
                {
                    XP = 300,
                    Gold = 100,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.TheChanged, 15 },
                        { FactionType.UnitedSanctum, -10 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("changed_02_medicine", "Medicine Run", QuestType.Faction)
            {
                Description = "Our people are sick. The mutation sickness takes many forms, and we need medicine. Scavenge medical supplies from the ruins.",
                QuestFaction = FactionType.TheChanged,
                GiverNPCId = "trader_rusthollow",
                RequiredQuests = new List<string> { "changed_01_mutual_aid" },
                RequiredLevel = 4,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_medkits", ObjectiveType.Collect, "Collect Med Kits")
                    {
                        TargetId = "med_kit",
                        RequiredCount = 5
                    },
                    new QuestObjective("obj_bandages", ObjectiveType.Collect, "Collect Bandages")
                    {
                        TargetId = "bandage",
                        RequiredCount = 10
                    }
                },
                Reward = new QuestReward
                {
                    XP = 350,
                    Gold = 75,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.TheChanged, 10 }
                    },
                    Items = new Dictionary<string, int> { ["mutagen"] = 2 }
                }
            });
            
            AddQuest(new QuestDefinition("changed_03_safe_passage", "Safe Passage", QuestType.Faction)
            {
                Description = "A caravan of Changed refugees needs to pass through raider territory. Clear the way for them.",
                QuestFaction = FactionType.TheChanged,
                GiverNPCId = "trader_rusthollow",
                RequiredQuests = new List<string> { "changed_02_medicine" },
                RequiredLevel = 5,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_raiders", ObjectiveType.KillType, "Eliminate Raiders threatening the route")
                    {
                        TargetEnemyType = EnemyType.Raider,
                        RequiredCount = 8
                    },
                    new QuestObjective("obj_abom", ObjectiveType.KillType, "Kill any Abominations blocking the path")
                    {
                        TargetEnemyType = EnemyType.Abomination,
                        RequiredCount = 2,
                        IsOptional = true
                    }
                },
                Reward = new QuestReward
                {
                    XP = 450,
                    Gold = 150,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.TheChanged, 15 },
                        { FactionType.Traders, 5 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("changed_04_mutation_embrace", "Embrace the Change", QuestType.Faction)
            {
                Description = "Some of our young are afraid of their mutations. Show them that mutation is strength - demonstrate what a true Child of the Changed can do.",
                QuestFaction = FactionType.TheChanged,
                GiverNPCId = "trader_rusthollow",
                RequiredQuests = new List<string> { "changed_03_safe_passage" },
                RequiredLevel = 6,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_level", ObjectiveType.ReachLevel, "Reach Level 7 (prove your strength)")
                    {
                        RequiredCount = 7
                    },
                    new QuestObjective("obj_kills", ObjectiveType.Kill, "Defeat powerful enemies")
                    {
                        RequiredCount = 15
                    }
                },
                Reward = new QuestReward
                {
                    XP = 500,
                    MutationPoints = 2,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.TheChanged, 20 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("changed_05_elder_blessing", "Elder's Blessing", QuestType.Faction)
            {
                Description = "You have proven yourself a true Child of the Changed. The Gene Elders wish to grant you their blessing - seek them at The Spire.",
                QuestFaction = FactionType.TheChanged,
                GiverNPCId = "trader_rusthollow",
                RequiredQuests = new List<string> { "changed_04_mutation_embrace" },
                RequiredLevel = 8,
                RequiredReputation = 30,
                RequiredFaction = FactionType.TheChanged,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_spire", ObjectiveType.Explore, "Journey to The Spire")
                    {
                        TargetId = "the_spire",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_elder", ObjectiveType.TalkTo, "Receive the Elder's Blessing")
                    {
                        TargetId = "elder_the_spire",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 750,
                    MutationPoints = 3,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.TheChanged, 25 },
                        { FactionType.GeneElders, 15 }
                    }
                }
            });
        }
        
        // ============================================
        // UNITED SANCTUM QUESTS
        // ============================================
        
        private void InitializeSanctumQuests()
        {
            AddQuest(new QuestDefinition("sanctum_01_prove_worth", "Prove Your Worth", QuestType.Faction)
            {
                Description = "The Sanctum despises mutants, but they respect strength. Prove you're useful by eliminating Void-touched creatures that threaten their patrols.",
                QuestFaction = FactionType.UnitedSanctum,
                LoreText = "The Sanctum believes mutants are corrupted, but some commanders are pragmatic enough to use them as disposable assets.",
                GiverNPCId = "trader_syndicate_post",  // Syndicate broker connection
                RequiredLevel = 5,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_void", ObjectiveType.KillType, "Kill Void-Touched creatures")
                    {
                        TargetEnemyType = EnemyType.VoidWraith,
                        RequiredCount = 3
                    },
                    new QuestObjective("obj_stalkers", ObjectiveType.KillType, "Eliminate Stalkers")
                    {
                        TargetEnemyType = EnemyType.Stalker,
                        RequiredCount = 2
                    }
                },
                Reward = new QuestReward
                {
                    XP = 400,
                    Gold = 200,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.UnitedSanctum, 10 },
                        { FactionType.TheChanged, -5 }  // Your people won't like you helping purists
                    }
                }
            });
            
            AddQuest(new QuestDefinition("sanctum_02_tech_recovery", "Tech Recovery", QuestType.Faction)
            {
                Description = "A Sanctum supply convoy was destroyed in the ruins. Recover their technology before scavengers claim it.",
                QuestFaction = FactionType.UnitedSanctum,
                GiverNPCId = "trader_syndicate_post",
                RequiredQuests = new List<string> { "sanctum_01_prove_worth" },
                RequiredLevel = 6,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_electronics", ObjectiveType.Collect, "Recover Sanctum electronics")
                    {
                        TargetId = "scrap_electronics",
                        RequiredCount = 10
                    },
                    new QuestObjective("obj_cells", ObjectiveType.Collect, "Recover energy cells")
                    {
                        TargetId = "energy_cell",
                        RequiredCount = 5
                    }
                },
                Reward = new QuestReward
                {
                    XP = 450,
                    Gold = 250,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.UnitedSanctum, 15 }
                    },
                    Items = new Dictionary<string, int> { ["pistol"] = 1, ["ammo_pistol"] = 20 }
                }
            });
            
            AddQuest(new QuestDefinition("sanctum_03_verdant_strike", "Strike Against the Verdant", QuestType.Faction)
            {
                Description = "The Sanctum and Verdant Order are eternal enemies. A Sanctum commander offers a substantial reward for disrupting Verdant operations.",
                QuestFaction = FactionType.UnitedSanctum,
                GiverNPCId = "trader_syndicate_post",
                RequiredQuests = new List<string> { "sanctum_02_tech_recovery" },
                RequiredLevel = 7,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_verdant", ObjectiveType.KillFaction, "Eliminate Verdant Order operatives")
                    {
                        TargetFaction = FactionType.VerdantOrder,
                        RequiredCount = 5
                    },
                    new QuestObjective("obj_hounds", ObjectiveType.KillType, "Kill their Gene Hounds")
                    {
                        TargetEnemyType = EnemyType.GeneHound,
                        RequiredCount = 3
                    }
                },
                Reward = new QuestReward
                {
                    XP = 550,
                    Gold = 350,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.UnitedSanctum, 20 },
                        { FactionType.VerdantOrder, -25 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("sanctum_04_uneasy_alliance", "Uneasy Alliance", QuestType.Faction)
            {
                Description = "Against all odds, a Sanctum commander wants to meet you in person. This could be a trap... or an opportunity.",
                QuestFaction = FactionType.UnitedSanctum,
                GiverNPCId = "trader_syndicate_post",
                RequiredQuests = new List<string> { "sanctum_03_verdant_strike" },
                RequiredLevel = 8,
                RequiredReputation = 20,
                RequiredFaction = FactionType.UnitedSanctum,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_base", ObjectiveType.Explore, "Go to Forward Base Purity")
                    {
                        TargetId = "forward_base_purity",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_commander", ObjectiveType.TalkTo, "Meet with the Commander")
                    {
                        TargetId = "commander_forward_base_purity",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 600,
                    Gold = 200,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.UnitedSanctum, 15 }
                    },
                    UnlockRecipes = new List<string> { "energy_rifle_craft" }
                }
            });
            
            AddQuest(new QuestDefinition("sanctum_05_purge_the_void", "Purge the Void", QuestType.Faction)
            {
                Description = "The Commander has a mission: destroy a Void Cult ritual site. Even a mutant can be useful against the greater threat.",
                QuestFaction = FactionType.UnitedSanctum,
                GiverNPCId = "commander_forward_base_purity",
                RequiredQuests = new List<string> { "sanctum_04_uneasy_alliance" },
                RequiredLevel = 10,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_temple", ObjectiveType.Explore, "Assault the Void Temple")
                    {
                        TargetId = "void_temple",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_cultists", ObjectiveType.KillFaction, "Eliminate Void Cultists")
                    {
                        TargetFaction = FactionType.VoidCult,
                        RequiredCount = 8
                    },
                    new QuestObjective("obj_horror", ObjectiveType.KillType, "Destroy the Void Horror")
                    {
                        TargetEnemyType = EnemyType.VoidHorror,
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 1000,
                    Gold = 500,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.UnitedSanctum, 30 },
                        { FactionType.VoidCult, -50 },
                        { FactionType.TheChanged, 5 }  // Even your people respect this
                    },
                    Items = new Dictionary<string, int> { ["power_armor_piece"] = 1 }
                }
            });
        }
        
        // ============================================
        // IRON SYNDICATE QUESTS
        // ============================================
        
        private void InitializeSyndicateQuests()
        {
            AddQuest(new QuestDefinition("syndicate_01_trade_route", "Trade Route Security", QuestType.Faction)
            {
                Description = "The Syndicate's trade routes are being harassed by raiders. Clear them out and you'll have the Syndicate's favor... and their coin.",
                QuestFaction = FactionType.IronSyndicate,
                GiverNPCId = "trader_syndicate_post",
                RequiredLevel = 4,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_raiders", ObjectiveType.KillType, "Kill Raiders along the trade route")
                    {
                        TargetEnemyType = EnemyType.Raider,
                        RequiredCount = 6
                    }
                },
                Reward = new QuestReward
                {
                    XP = 350,
                    Gold = 300,  // Syndicate pays well
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.IronSyndicate, 15 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("syndicate_02_salvage_run", "Salvage Run", QuestType.Faction)
            {
                Description = "The Syndicate has identified a pre-Severance tech cache in the ruins. Retrieve it before anyone else does.",
                QuestFaction = FactionType.IronSyndicate,
                GiverNPCId = "trader_syndicate_post",
                RequiredQuests = new List<string> { "syndicate_01_trade_route" },
                RequiredLevel = 5,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_ruins", ObjectiveType.Explore, "Reach the cache location in Outer Ruins - East")
                    {
                        TargetId = "outer_ruins_east",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_tech", ObjectiveType.Collect, "Recover tech components")
                    {
                        TargetId = "components",
                        RequiredCount = 10
                    },
                    new QuestObjective("obj_electronics", ObjectiveType.Collect, "Recover electronics")
                    {
                        TargetId = "scrap_electronics",
                        RequiredCount = 8
                    }
                },
                Reward = new QuestReward
                {
                    XP = 400,
                    Gold = 400,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.IronSyndicate, 15 }
                    },
                    Items = new Dictionary<string, int> { ["repair_kit"] = 3 }
                }
            });
            
            AddQuest(new QuestDefinition("syndicate_03_competition", "Eliminate the Competition", QuestType.Faction)
            {
                Description = "A rival merchant has been undercutting Syndicate prices. The Syndicate wants you to... discourage them. Permanently.",
                QuestFaction = FactionType.IronSyndicate,
                LoreText = "The Syndicate's motto: 'Business is war, and war is profitable.'",
                GiverNPCId = "trader_syndicate_post",
                RequiredQuests = new List<string> { "syndicate_02_salvage_run" },
                RequiredLevel = 6,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_mercs", ObjectiveType.Kill, "Deal with the rival's guards")
                    {
                        RequiredCount = 4
                    },
                    new QuestObjective("obj_goods", ObjectiveType.Collect, "Confiscate their merchandise")
                    {
                        RequiredCount = 5
                    }
                },
                Reward = new QuestReward
                {
                    XP = 450,
                    Gold = 500,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.IronSyndicate, 20 },
                        { FactionType.Traders, -10 }  // Other traders are nervous
                    }
                }
            });
            
            AddQuest(new QuestDefinition("syndicate_04_sanctum_sabotage", "Sanctum Sabotage", QuestType.Faction)
            {
                Description = "The Sanctum has been seizing Syndicate shipments. Time for payback. Destroy a Sanctum patrol and recover the stolen goods.",
                QuestFaction = FactionType.IronSyndicate,
                GiverNPCId = "trader_syndicate_post",
                RequiredQuests = new List<string> { "syndicate_03_competition" },
                RequiredLevel = 7,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_sanctum", ObjectiveType.KillFaction, "Destroy Sanctum patrol")
                    {
                        TargetFaction = FactionType.UnitedSanctum,
                        RequiredCount = 5
                    },
                    new QuestObjective("obj_drone", ObjectiveType.KillType, "Disable their Purge Drone")
                    {
                        TargetEnemyType = EnemyType.PurgeDrone,
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 550,
                    Gold = 600,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.IronSyndicate, 25 },
                        { FactionType.UnitedSanctum, -20 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("syndicate_05_vault_heist", "The Vault Job", QuestType.Faction)
            {
                Description = "Vault Omega - a pre-Severance bunker full of priceless tech. The Syndicate wants you to crack it open. The reward will be... substantial.",
                QuestFaction = FactionType.IronSyndicate,
                GiverNPCId = "trader_syndicate_post",
                RequiredQuests = new List<string> { "syndicate_04_sanctum_sabotage" },
                RequiredLevel = 9,
                RequiredReputation = 30,
                RequiredFaction = FactionType.IronSyndicate,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_vault", ObjectiveType.Explore, "Infiltrate Vault Omega")
                    {
                        TargetId = "vault_omega",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_tech", ObjectiveType.Collect, "Retrieve the vault's tech cache")
                    {
                        TargetId = "components",
                        RequiredCount = 20
                    },
                    new QuestObjective("obj_relic", ObjectiveType.Collect, "Find the pre-Severance relic")
                    {
                        TargetId = "relic",
                        RequiredCount = 1,
                        IsOptional = true
                    }
                },
                Reward = new QuestReward
                {
                    XP = 1000,
                    Gold = 1000,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.IronSyndicate, 30 }
                    },
                    UnlockRecipes = new List<string> { "power_armor_craft", "energy_weapon_craft" }
                }
            });
        }
        
        // ============================================
        // VERDANT ORDER QUESTS
        // ============================================
        
        private void InitializeVerdantQuests()
        {
            AddQuest(new QuestDefinition("verdant_01_specimen", "Specimen Collection", QuestType.Faction)
            {
                Description = "The Verdant Order studies mutation. They pay well for live specimens... or parts from fresh kills.",
                QuestFaction = FactionType.VerdantOrder,
                LoreText = "The Verdant believe they can perfect mutation, creating a new form of humanity.",
                GiverNPCId = "trader_syndicate_post",
                RequiredLevel = 4,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_beasts", ObjectiveType.KillType, "Collect samples from Mutant Beasts")
                    {
                        TargetEnemyType = EnemyType.MutantBeast,
                        RequiredCount = 5
                    },
                    new QuestObjective("obj_mutagen", ObjectiveType.Collect, "Collect mutagen samples")
                    {
                        TargetId = "mutagen",
                        RequiredCount = 3
                    }
                },
                Reward = new QuestReward
                {
                    XP = 350,
                    Gold = 250,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.VerdantOrder, 10 },
                        { FactionType.TheChanged, -5 }  // Helping those who experiment on your kind
                    }
                }
            });
            
            AddQuest(new QuestDefinition("verdant_02_hive_study", "Hive Study", QuestType.Faction)
            {
                Description = "The Hive creatures fascinate the Verdant. They want samples from a Hive Mother and its swarmlings.",
                QuestFaction = FactionType.VerdantOrder,
                GiverNPCId = "trader_syndicate_post",
                RequiredQuests = new List<string> { "verdant_01_specimen" },
                RequiredLevel = 6,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_mother", ObjectiveType.KillType, "Kill a Hive Mother")
                    {
                        TargetEnemyType = EnemyType.HiveMother,
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_swarm", ObjectiveType.KillType, "Collect Swarmling samples")
                    {
                        TargetEnemyType = EnemyType.Swarmling,
                        RequiredCount = 10
                    }
                },
                Reward = new QuestReward
                {
                    XP = 500,
                    Gold = 400,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.VerdantOrder, 15 }
                    },
                    Items = new Dictionary<string, int> { ["mutagen"] = 5 }
                }
            });
            
            AddQuest(new QuestDefinition("verdant_03_void_sample", "Void Sample", QuestType.Faction)
            {
                Description = "The Verdant want samples from Void-touched creatures. The anomaly shards they carry are particularly valuable.",
                QuestFaction = FactionType.VerdantOrder,
                GiverNPCId = "trader_syndicate_post",
                RequiredQuests = new List<string> { "verdant_02_hive_study" },
                RequiredLevel = 7,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_void", ObjectiveType.KillType, "Harvest Void Wraiths")
                    {
                        TargetEnemyType = EnemyType.VoidWraith,
                        RequiredCount = 4
                    },
                    new QuestObjective("obj_shards", ObjectiveType.Collect, "Collect anomaly shards")
                    {
                        TargetId = "anomaly_shard",
                        RequiredCount = 8
                    }
                },
                Reward = new QuestReward
                {
                    XP = 550,
                    Gold = 500,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.VerdantOrder, 20 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("verdant_04_nursery_tour", "The Nursery Tour", QuestType.Faction)
            {
                Description = "Your contributions have impressed the Verdant. They invite you to visit The Nursery - their main laboratory. Few outsiders receive this honor.",
                QuestFaction = FactionType.VerdantOrder,
                GiverNPCId = "trader_syndicate_post",
                RequiredQuests = new List<string> { "verdant_03_void_sample" },
                RequiredLevel = 8,
                RequiredReputation = 20,
                RequiredFaction = FactionType.VerdantOrder,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_nursery", ObjectiveType.Explore, "Visit The Nursery")
                    {
                        TargetId = "the_nursery",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_biomancer", ObjectiveType.TalkTo, "Meet with a Verdant Biomancer")
                    {
                        TargetId = "biomancer_the_nursery",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 600,
                    MutationPoints = 1,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.VerdantOrder, 15 }
                    },
                    UnlockRecipes = new List<string> { "bio_stimulant_craft", "mutation_stabilizer_craft" }
                }
            });
            
            AddQuest(new QuestDefinition("verdant_05_perfect_specimen", "The Perfect Specimen", QuestType.Faction)
            {
                Description = "The Verdant believe you could be their perfect specimen. They offer to enhance your mutations... if you prove yourself worthy.",
                QuestFaction = FactionType.VerdantOrder,
                LoreText = "Warning: The Verdant's 'enhancements' are not always voluntary.",
                GiverNPCId = "biomancer_the_nursery",
                RequiredQuests = new List<string> { "verdant_04_nursery_tour" },
                RequiredLevel = 10,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_essence", ObjectiveType.Collect, "Collect essence for the procedure")
                    {
                        TargetId = "essence",
                        RequiredCount = 10
                    },
                    new QuestObjective("obj_shards", ObjectiveType.Collect, "Provide rare anomaly shards")
                    {
                        TargetId = "anomaly_shard",
                        RequiredCount = 10
                    },
                    new QuestObjective("obj_horror", ObjectiveType.KillType, "Prove your strength against a Void Horror")
                    {
                        TargetEnemyType = EnemyType.VoidHorror,
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 1000,
                    MutationPoints = 3,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.VerdantOrder, 30 },
                        { FactionType.TheChanged, -10 }
                    }
                }
            });
        }
        
        // ============================================
        // VOID CULT QUESTS (Dark Science Path)
        // ============================================
        
        private void InitializeVoidCultQuests()
        {
            AddQuest(new QuestDefinition("void_01_whispers", "Whispers in the Dark", QuestType.Faction)
            {
                Description = "You hear whispers... the Void calls to those who will listen. Seek out the voices in the Dark Forest.",
                QuestFaction = FactionType.VoidCult,
                LoreText = "The Void is not evil. It is transformation. It is truth beyond the lies of reality.",
                GiverNPCId = null,  // Auto-discovered in Dark Forest
                RequiredLevel = 6,
                RequiredSciencePath = SciencePath.Dark,
                IsHidden = true,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_forest", ObjectiveType.Explore, "Follow the whispers in the Dark Forest")
                    {
                        TargetId = "dark_forest",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_shards", ObjectiveType.Collect, "Attune to the Void (collect anomaly shards)")
                    {
                        TargetId = "anomaly_shard",
                        RequiredCount = 5
                    }
                },
                Reward = new QuestReward
                {
                    XP = 400,
                    MutationPoints = 1,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.VoidCult, 15 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("void_02_ritual", "The First Ritual", QuestType.Faction)
            {
                Description = "The Cult has noticed you. They offer to teach you the first of their dark rituals... but you must prove your dedication.",
                QuestFaction = FactionType.VoidCult,
                GiverNPCId = "cultist_dark_forest",
                RequiredQuests = new List<string> { "void_01_whispers" },
                RequiredLevel = 7,
                RequiredSciencePath = SciencePath.Dark,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_essence", ObjectiveType.Collect, "Gather essence for the ritual")
                    {
                        TargetId = "essence",
                        RequiredCount = 5
                    },
                    new QuestObjective("obj_void_kill", ObjectiveType.KillType, "Absorb a Void Wraith's power")
                    {
                        TargetEnemyType = EnemyType.VoidWraith,
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 500,
                    MutationPoints = 2,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.VoidCult, 20 },
                        { FactionType.UnitedSanctum, -15 },
                        { FactionType.TheChanged, -5 }
                    },
                    UnlockRecipes = new List<string> { "void_ritual_craft" }
                }
            });
            
            AddQuest(new QuestDefinition("void_03_temple", "The Temple Awaits", QuestType.Faction)
            {
                Description = "You have proven yourself. The Temple of the Consuming Void opens its doors to you. But the journey there is dangerous.",
                QuestFaction = FactionType.VoidCult,
                GiverNPCId = "cultist_dark_forest",
                RequiredQuests = new List<string> { "void_02_ritual" },
                RequiredLevel = 8,
                RequiredSciencePath = SciencePath.Dark,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_temple", ObjectiveType.Explore, "Journey to the Temple of the Consuming Void")
                    {
                        TargetId = "void_temple",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_priest", ObjectiveType.TalkTo, "Seek the Void Priest")
                    {
                        TargetId = "priest_void_temple",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 600,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.VoidCult, 25 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("void_04_sacrifice", "The Sacrifice", QuestType.Faction)
            {
                Description = "The Void demands sacrifice. Bring the Priest rare materials infused with reality's essence... materials that only exist where reality is thin.",
                QuestFaction = FactionType.VoidCult,
                GiverNPCId = "priest_void_temple",
                RequiredQuests = new List<string> { "void_03_temple" },
                RequiredLevel = 9,
                RequiredSciencePath = SciencePath.Dark,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_deep", ObjectiveType.Explore, "Venture into the Deep Zone")
                    {
                        TargetId = "deep_zone_west",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_shards", ObjectiveType.Collect, "Collect pure anomaly shards")
                    {
                        TargetId = "anomaly_shard",
                        RequiredCount = 15
                    },
                    new QuestObjective("obj_essence", ObjectiveType.Collect, "Collect concentrated essence")
                    {
                        TargetId = "essence",
                        RequiredCount = 10
                    }
                },
                Reward = new QuestReward
                {
                    XP = 800,
                    MutationPoints = 2,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.VoidCult, 30 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("void_05_transcendence", "Transcendence", QuestType.Faction)
            {
                Description = "The final ritual. The Void offers you transcendence - to shed your mortal limitations and embrace the infinite darkness. Are you ready?",
                QuestFaction = FactionType.VoidCult,
                LoreText = "Those who complete the ritual are changed forever. Some say they become one with the Void itself.",
                GiverNPCId = "priest_void_temple",
                RequiredQuests = new List<string> { "void_04_sacrifice" },
                RequiredLevel = 12,
                RequiredSciencePath = SciencePath.Dark,
                RequiredReputation = 50,
                RequiredFaction = FactionType.VoidCult,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_rift", ObjectiveType.Explore, "Enter The Rift")
                    {
                        TargetId = "the_rift",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_horror", ObjectiveType.KillType, "Defeat a Void Horror in single combat")
                    {
                        TargetEnemyType = EnemyType.VoidHorror,
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_ritual", ObjectiveType.Craft, "Complete the Transcendence Ritual")
                    {
                        TargetId = "transcendence_ritual",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 2000,
                    MutationPoints = 5,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.VoidCult, 50 },
                        { FactionType.UnitedSanctum, -50 },
                        { FactionType.TheChanged, -20 }
                    }
                }
            });
        }
        
        // ============================================
        // GENE ELDER QUESTS
        // ============================================
        
        private void InitializeGeneElderQuests()
        {
            AddQuest(new QuestDefinition("elder_01_history", "Echoes of the Past", QuestType.Faction)
            {
                Description = "The Gene Elders remember the old world. They ask you to recover artifacts from before The Severance - physical proof of what was lost.",
                QuestFaction = FactionType.GeneElders,
                GiverNPCId = "elder_the_spire",
                RequiredLevel = 6,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_ruins", ObjectiveType.Explore, "Search the Inner Ruins")
                    {
                        TargetId = "inner_ruins_south",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_artifacts", ObjectiveType.Collect, "Recover pre-Severance artifacts")
                    {
                        TargetId = "relic",
                        RequiredCount = 3
                    }
                },
                Reward = new QuestReward
                {
                    XP = 450,
                    Gold = 150,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.GeneElders, 15 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("elder_02_mutation_study", "The Nature of Change", QuestType.Faction)
            {
                Description = "The Elders wish to study how mutations manifest in the current generation. They need samples from various mutated creatures.",
                QuestFaction = FactionType.GeneElders,
                GiverNPCId = "elder_the_spire",
                RequiredQuests = new List<string> { "elder_01_history" },
                RequiredLevel = 7,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_mutagen", ObjectiveType.Collect, "Collect mutagen samples")
                    {
                        TargetId = "mutagen",
                        RequiredCount = 5
                    },
                    new QuestObjective("obj_psionic", ObjectiveType.KillType, "Study a Psionic mutant")
                    {
                        TargetEnemyType = EnemyType.Psionic,
                        RequiredCount = 2
                    },
                    new QuestObjective("obj_brute", ObjectiveType.KillType, "Study a Brute mutant")
                    {
                        TargetEnemyType = EnemyType.Brute,
                        RequiredCount = 2
                    }
                },
                Reward = new QuestReward
                {
                    XP = 500,
                    MutationPoints = 1,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.GeneElders, 15 },
                        { FactionType.TheChanged, 5 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("elder_03_verdant_threat", "The Verdant Heresy", QuestType.Faction)
            {
                Description = "The Verdant Order were once students of the Gene Elders. Now they twist mutation for their own ends. The Elders want them stopped.",
                QuestFaction = FactionType.GeneElders,
                GiverNPCId = "elder_the_spire",
                RequiredQuests = new List<string> { "elder_02_mutation_study" },
                RequiredLevel = 8,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_verdant", ObjectiveType.KillFaction, "Strike against Verdant operations")
                    {
                        TargetFaction = FactionType.VerdantOrder,
                        RequiredCount = 6
                    },
                    new QuestObjective("obj_biomancer", ObjectiveType.KillType, "Defeat a Verdant Biomancer")
                    {
                        TargetEnemyType = EnemyType.VerdantBiomancer,
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 600,
                    Gold = 200,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.GeneElders, 20 },
                        { FactionType.VerdantOrder, -30 },
                        { FactionType.TheChanged, 10 }
                    }
                }
            });
            
            AddQuest(new QuestDefinition("elder_04_ancient_knowledge", "Ancient Knowledge", QuestType.Faction)
            {
                Description = "The Elders speak of Vault Omega - a bunker that contains knowledge from before The Severance. They want you to retrieve their ancient records.",
                QuestFaction = FactionType.GeneElders,
                GiverNPCId = "elder_the_spire",
                RequiredQuests = new List<string> { "elder_03_verdant_threat" },
                RequiredLevel = 9,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_vault", ObjectiveType.Explore, "Enter Vault Omega")
                    {
                        TargetId = "vault_omega",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_data", ObjectiveType.Collect, "Recover ancient data cores")
                    {
                        TargetId = "components",
                        RequiredCount = 10
                    }
                },
                Reward = new QuestReward
                {
                    XP = 750,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.GeneElders, 25 }
                    },
                    UnlockRecipes = new List<string> { "mutation_enhancer_craft" }
                }
            });
            
            AddQuest(new QuestDefinition("elder_05_legacy", "The Elder's Legacy", QuestType.Faction)
            {
                Description = "You have proven yourself worthy. The Gene Elders offer to share their greatest secret - the truth about The Severance and what lies at the Epicenter.",
                QuestFaction = FactionType.GeneElders,
                LoreText = "Few outside the Elder Council know the full truth. You will be among them.",
                GiverNPCId = "elder_the_spire",
                RequiredQuests = new List<string> { "elder_04_ancient_knowledge" },
                RequiredLevel = 11,
                RequiredReputation = 50,
                RequiredFaction = FactionType.GeneElders,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_deep", ObjectiveType.Explore, "Journey to the Deep Zone with Elder guidance")
                    {
                        TargetId = "deep_zone_south",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_wound", ObjectiveType.Explore, "Witness The Wound")
                    {
                        TargetId = "the_wound",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_elder_talk", ObjectiveType.TalkTo, "Receive the Elder's truth")
                    {
                        TargetId = "elder_the_spire",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    XP = 1500,
                    MutationPoints = 3,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.GeneElders, 40 },
                        { FactionType.TheChanged, 20 }
                    }
                }
            });
        }
        
        // ============================================
        // BOUNTY QUESTS (Repeatable)
        // ============================================
        
        private void InitializeBountyQuests()
        {
            AddQuest(new QuestDefinition("bounty_raiders", "Raider Bounty", QuestType.Bounty)
            {
                Description = "Raiders threaten all travelers. Eliminate them for a bounty.",
                GiverNPCId = "trader_rusthollow",
                IsRepeatable = true,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_kill", ObjectiveType.KillType, "Kill Raiders")
                    {
                        TargetEnemyType = EnemyType.Raider,
                        RequiredCount = 5
                    }
                },
                Reward = new QuestReward { Gold = 100, XP = 150 }
            });
            
            AddQuest(new QuestDefinition("bounty_beasts", "Beast Bounty", QuestType.Bounty)
            {
                Description = "Mutant beasts are a constant threat. Thin their numbers.",
                GiverNPCId = "trader_rusthollow",
                IsRepeatable = true,
                RequiredLevel = 2,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_kill", ObjectiveType.KillType, "Kill Mutant Beasts")
                    {
                        TargetEnemyType = EnemyType.MutantBeast,
                        RequiredCount = 4
                    }
                },
                Reward = new QuestReward { Gold = 120, XP = 180 }
            });
            
            AddQuest(new QuestDefinition("bounty_void", "Void Hunter", QuestType.Bounty)
            {
                Description = "Void-touched creatures are dangerous. The Syndicate pays well for their elimination.",
                GiverNPCId = "trader_syndicate_post",
                IsRepeatable = true,
                RequiredLevel = 5,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_kill", ObjectiveType.KillType, "Kill Void Wraiths")
                    {
                        TargetEnemyType = EnemyType.VoidWraith,
                        RequiredCount = 3
                    }
                },
                Reward = new QuestReward { Gold = 200, XP = 250 }
            });
            
            AddQuest(new QuestDefinition("bounty_hive", "Hive Clearer", QuestType.Bounty)
            {
                Description = "Hive creatures multiply rapidly. Keep their population in check.",
                GiverNPCId = "trader_syndicate_post",
                IsRepeatable = true,
                RequiredLevel = 4,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_mother", ObjectiveType.KillType, "Kill a Hive Mother")
                    {
                        TargetEnemyType = EnemyType.HiveMother,
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_swarm", ObjectiveType.KillType, "Kill Swarmlings")
                    {
                        TargetEnemyType = EnemyType.Swarmling,
                        RequiredCount = 8
                    }
                },
                Reward = new QuestReward { Gold = 175, XP = 220 }
            });
            
            AddQuest(new QuestDefinition("bounty_sanctum", "Sanctum Patrol", QuestType.Bounty)
            {
                Description = "Sanctum patrols threaten mutant settlements. Eliminate them.",
                GiverNPCId = "trader_rusthollow",
                IsRepeatable = true,
                RequiredLevel = 6,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_kill", ObjectiveType.KillFaction, "Eliminate Sanctum forces")
                    {
                        TargetFaction = FactionType.UnitedSanctum,
                        RequiredCount = 4
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 200,
                    XP = 300,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.TheChanged, 5 },
                        { FactionType.UnitedSanctum, -5 }
                    }
                }
            });
        }
        
        // ============================================
        // SIDE QUESTS
        // ============================================
        
        private void InitializeSideQuests()
        {
            AddQuest(new QuestDefinition("side_scavenge", "Scavenger", QuestType.Side)
            {
                Description = "Resources are scarce. Gather materials for the camp.",
                GiverNPCId = "trader_rusthollow",
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_metal", ObjectiveType.Collect, "Collect Scrap Metal")
                    {
                        TargetId = "scrap_metal",
                        RequiredCount = 10
                    },
                    new QuestObjective("obj_cloth", ObjectiveType.Collect, "Collect Cloth")
                    {
                        TargetId = "cloth",
                        RequiredCount = 5
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 60,
                    XP = 100,
                    Items = new Dictionary<string, int> { ["bandage"] = 5 }
                }
            });
            
            AddQuest(new QuestDefinition("side_cave_explorer", "Into the Depths", QuestType.Side)
            {
                Description = "Strange sounds echo from the caves. Investigate what lurks within.",
                GiverNPCId = "trader_rusthollow",
                RequiredLevel = 4,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_explore", ObjectiveType.Explore, "Explore the Dark Forest caves")
                    {
                        TargetId = "dark_forest",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_crawlers", ObjectiveType.KillType, "Kill Void Crawlers")
                    {
                        TargetEnemyType = EnemyType.VoidCrawler,
                        RequiredCount = 3
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 150,
                    XP = 250,
                    Items = new Dictionary<string, int> { ["anomaly_shard"] = 3 }
                }
            });
            
            AddQuest(new QuestDefinition("side_builder", "Home Sweet Home", QuestType.Side)
            {
                Description = "Building a proper shelter takes work. Construct the essentials.",
                GiverNPCId = "trader_rusthollow",
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_fire", ObjectiveType.Build, "Build a Campfire")
                    {
                        TargetId = "Campfire",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_bed", ObjectiveType.Build, "Build a Bedroll")
                    {
                        TargetId = "Bedroll",
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_storage", ObjectiveType.Build, "Build Storage")
                    {
                        TargetId = "StorageChest",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 50,
                    XP = 150,
                    Items = new Dictionary<string, int> { ["wood"] = 20 }
                }
            });
            
            AddQuest(new QuestDefinition("side_crafter", "Self-Sufficient", QuestType.Side)
            {
                Description = "True survival means making what you need. Craft essential items.",
                GiverNPCId = "trader_rusthollow",
                RequiredLevel = 3,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_weapon", ObjectiveType.Craft, "Craft a weapon")
                    {
                        RequiredCount = 1
                    },
                    new QuestObjective("obj_bandage", ObjectiveType.Craft, "Craft bandages")
                    {
                        TargetId = "bandage",
                        RequiredCount = 5
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 75,
                    XP = 200,
                    UnlockRecipes = new List<string> { "medkit_craft" }
                }
            });
            
            AddQuest(new QuestDefinition("side_researcher", "Scientific Method", QuestType.Side)
            {
                Description = "Knowledge is power. Complete research to unlock new possibilities.",
                GiverNPCId = "trader_syndicate_post",
                RequiredLevel = 5,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_research", ObjectiveType.Research, "Complete any research")
                    {
                        RequiredCount = 2
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 100,
                    XP = 300,
                    Items = new Dictionary<string, int> { ["components"] = 10, ["scrap_electronics"] = 5 }
                }
            });
            
            AddQuest(new QuestDefinition("side_survivor", "True Survivor", QuestType.Side)
            {
                Description = "Prove you can survive the wasteland's harshest conditions.",
                GiverNPCId = "trader_rusthollow",
                RequiredLevel = 3,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("obj_survive", ObjectiveType.Survive, "Survive 7 days")
                    {
                        RequiredCount = 7
                    },
                    new QuestObjective("obj_kills", ObjectiveType.Kill, "Defeat enemies while surviving")
                    {
                        RequiredCount = 20
                    }
                },
                Reward = new QuestReward
                {
                    XP = 400,
                    MutationPoints = 1,
                    ReputationChanges = new Dictionary<FactionType, int>
                    {
                        { FactionType.TheChanged, 10 }
                    }
                }
            });
        }
        
        // ============================================
        // HELPER METHODS
        // ============================================
        
        private void AddQuest(QuestDefinition quest)
        {
            _definitions[quest.Id] = quest;
        }
        
        public QuestDefinition GetDefinition(string id)
        {
            return _definitions.TryGetValue(id, out var def) ? def : null;
        }
        
        public List<QuestDefinition> GetAllDefinitions()
        {
            return _definitions.Values.ToList();
        }
        
        public List<QuestDefinition> GetAvailableQuests(int playerLevel, HashSet<string> completed = null)
        {
            completed = completed ?? _completedQuests;
            
            return _definitions.Values
                .Where(q => !q.IsHidden || _discoveredQuests.Contains(q.Id))
                .Where(q => q.RequiredLevel <= playerLevel)
                .Where(q => q.RequiredQuests.All(r => completed.Contains(r)))
                .Where(q => !_activeQuests.ContainsKey(q.Id))
                .Where(q => q.IsRepeatable || !completed.Contains(q.Id))
                .ToList();
        }
        
        /// <summary>
        /// Get quests available from a specific NPC
        /// </summary>
        public List<QuestDefinition> GetQuestsFromNPC(string npcId, int playerLevel)
        {
            return _definitions.Values
                .Where(q => q.GiverNPCId == npcId)
                .Where(q => !q.IsHidden || _discoveredQuests.Contains(q.Id))
                .Where(q => q.RequiredLevel <= playerLevel)
                .Where(q => q.RequiredQuests.All(r => _completedQuests.Contains(r)))
                .Where(q => !_activeQuests.ContainsKey(q.Id))
                .Where(q => q.IsRepeatable || !_completedQuests.Contains(q.Id))
                .ToList();
        }
        
        /// <summary>
        /// Get quests that can be turned in to this NPC
        /// </summary>
        public List<QuestInstance> GetCompletedQuestsForNPC(string npcId)
        {
            return _activeQuests.Values
                .Where(q => q.IsComplete)
                .Where(q => {
                    var turnInId = q.Definition.TurnInNPCId ?? q.Definition.GiverNPCId;
                    return turnInId == npcId;
                })
                .ToList();
        }
        
        /// <summary>
        /// Check if NPC has any quests available (for UI indicator)
        /// </summary>
        public bool NPCHasQuests(string npcId, int playerLevel)
        {
            return GetQuestsFromNPC(npcId, playerLevel).Any() || GetCompletedQuestsForNPC(npcId).Any();
        }
        
        /// <summary>
        /// Alias for GetCompletedQuestsForNPC (for backward compatibility)
        /// </summary>
        public List<QuestInstance> GetQuestsToTurnIn(string npcId)
        {
            return GetCompletedQuestsForNPC(npcId);
        }
        
        /// <summary>
        /// Get faction-specific quests available
        /// </summary>
        public List<QuestDefinition> GetFactionQuests(FactionType faction, int playerLevel, int factionReputation)
        {
            return _definitions.Values
                .Where(q => q.QuestFaction == faction)
                .Where(q => !q.IsHidden || _discoveredQuests.Contains(q.Id))
                .Where(q => q.RequiredLevel <= playerLevel)
                .Where(q => q.RequiredReputation <= factionReputation)
                .Where(q => q.RequiredQuests.All(r => _completedQuests.Contains(r)))
                .Where(q => !_activeQuests.ContainsKey(q.Id))
                .Where(q => q.IsRepeatable || !_completedQuests.Contains(q.Id))
                .ToList();
        }
        
        public List<QuestInstance> GetActiveQuests()
        {
            return _activeQuests.Values.ToList();
        }
        
        public bool IsQuestCompleted(string id)
        {
            return _completedQuests.Contains(id);
        }
        
        public bool IsQuestActive(string id)
        {
            return _activeQuests.ContainsKey(id);
        }
        
        public QuestInstance GetActiveQuest(string id)
        {
            return _activeQuests.TryGetValue(id, out var quest) ? quest : null;
        }
        
        public bool CanAcceptQuest(string questId, int playerLevel)
        {
            var def = GetDefinition(questId);
            if (def == null) return false;
            if (def.RequiredLevel > playerLevel) return false;
            if (_activeQuests.ContainsKey(questId)) return false;
            if (!def.IsRepeatable && _completedQuests.Contains(questId)) return false;
            if (!def.RequiredQuests.All(r => _completedQuests.Contains(r))) return false;
            if (def.IsHidden && !_discoveredQuests.Contains(questId)) return false;
            
            return true;
        }
        
        public bool AcceptQuest(string questId)
        {
            var def = GetDefinition(questId);
            if (def == null) return false;
            
            var instance = new QuestInstance(def);
            _activeQuests[questId] = instance;
            
            OnQuestStarted?.Invoke(instance);
            System.Diagnostics.Debug.WriteLine($">>> Quest Started: {def.Name} <<<");
            
            return true;
        }
        
        public bool TurnInQuest(string questId, Action<QuestReward> applyReward)
        {
            if (!_activeQuests.TryGetValue(questId, out var quest)) return false;
            if (!quest.IsComplete) return false;
            
            _activeQuests.Remove(questId);
            _completedQuests.Add(questId);
            
            quest.State = QuestState.TurnedIn;
            applyReward?.Invoke(quest.Definition.Reward);
            
            OnQuestTurnedIn?.Invoke(quest);
            System.Diagnostics.Debug.WriteLine($">>> Quest Completed: {quest.Definition.Name} <<<");
            
            return true;
        }
        
        public void AbandonQuest(string questId)
        {
            if (_activeQuests.Remove(questId))
            {
                System.Diagnostics.Debug.WriteLine($">>> Quest Abandoned: {questId} <<<");
            }
        }
        
        // ============================================
        // PROGRESS TRACKING
        // ============================================
        
        public void OnEnemyKilled(EnemyType enemyType, FactionType? faction = null)
        {
            foreach (var quest in _activeQuests.Values)
            {
                foreach (var obj in quest.Objectives.Where(o => !o.IsComplete))
                {
                    bool progress = false;
                    
                    if (obj.Type == ObjectiveType.Kill)
                    {
                        obj.CurrentCount++;
                        progress = true;
                    }
                    else if (obj.Type == ObjectiveType.KillType && obj.TargetEnemyType == enemyType)
                    {
                        obj.CurrentCount++;
                        progress = true;
                    }
                    else if (obj.Type == ObjectiveType.KillFaction && obj.TargetFaction == faction)
                    {
                        obj.CurrentCount++;
                        progress = true;
                    }
                    
                    if (progress)
                    {
                        OnObjectiveProgress?.Invoke(quest, obj);
                        if (obj.IsComplete)
                        {
                            OnObjectiveComplete?.Invoke(quest, obj);
                        }
                        if (quest.IsComplete)
                        {
                            quest.State = QuestState.Completed;
                            OnQuestCompleted?.Invoke(quest);
                        }
                    }
                }
            }
        }
        
        public void OnItemCollected(string itemId, int count = 1)
        {
            foreach (var quest in _activeQuests.Values)
            {
                foreach (var obj in quest.Objectives.Where(o => !o.IsComplete))
                {
                    if (obj.Type == ObjectiveType.Collect)
                    {
                        // Check if it's "any" collect or specific item
                        if (string.IsNullOrEmpty(obj.TargetId) || obj.TargetId == itemId || obj.TargetId == "any_weapon")
                        {
                            obj.CurrentCount += count;
                            OnObjectiveProgress?.Invoke(quest, obj);
                            if (obj.IsComplete)
                            {
                                OnObjectiveComplete?.Invoke(quest, obj);
                            }
                            if (quest.IsComplete)
                            {
                                quest.State = QuestState.Completed;
                                OnQuestCompleted?.Invoke(quest);
                            }
                        }
                    }
                }
            }
        }
        
        public void OnNPCTalkedTo(string npcId)
        {
            foreach (var quest in _activeQuests.Values)
            {
                foreach (var obj in quest.Objectives.Where(o => !o.IsComplete && o.Type == ObjectiveType.TalkTo))
                {
                    if (obj.TargetId == npcId || string.IsNullOrEmpty(obj.TargetId))
                    {
                        obj.CurrentCount++;
                        OnObjectiveProgress?.Invoke(quest, obj);
                        if (obj.IsComplete)
                        {
                            OnObjectiveComplete?.Invoke(quest, obj);
                        }
                        if (quest.IsComplete)
                        {
                            quest.State = QuestState.Completed;
                            OnQuestCompleted?.Invoke(quest);
                        }
                    }
                }
            }
        }
        
        public void OnZoneEntered(string zoneId)
        {
            foreach (var quest in _activeQuests.Values)
            {
                foreach (var obj in quest.Objectives.Where(o => !o.IsComplete && o.Type == ObjectiveType.Explore))
                {
                    if (obj.TargetId == zoneId)
                    {
                        obj.CurrentCount++;
                        OnObjectiveProgress?.Invoke(quest, obj);
                        if (obj.IsComplete)
                        {
                            OnObjectiveComplete?.Invoke(quest, obj);
                        }
                        if (quest.IsComplete)
                        {
                            quest.State = QuestState.Completed;
                            OnQuestCompleted?.Invoke(quest);
                        }
                    }
                }
            }
            
            // Check for hidden quest discovery
            DiscoverHiddenQuests(zoneId);
        }
        
        public void OnStructureBuilt(string structureName)
        {
            foreach (var quest in _activeQuests.Values)
            {
                foreach (var obj in quest.Objectives.Where(o => !o.IsComplete && o.Type == ObjectiveType.Build))
                {
                    if (obj.TargetId == structureName || string.IsNullOrEmpty(obj.TargetId))
                    {
                        obj.CurrentCount++;
                        OnObjectiveProgress?.Invoke(quest, obj);
                        if (obj.IsComplete)
                        {
                            OnObjectiveComplete?.Invoke(quest, obj);
                        }
                        if (quest.IsComplete)
                        {
                            quest.State = QuestState.Completed;
                            OnQuestCompleted?.Invoke(quest);
                        }
                    }
                }
            }
        }
        
        public void OnItemCrafted(string recipeId)
        {
            foreach (var quest in _activeQuests.Values)
            {
                foreach (var obj in quest.Objectives.Where(o => !o.IsComplete && o.Type == ObjectiveType.Craft))
                {
                    if (string.IsNullOrEmpty(obj.TargetId) || obj.TargetId == recipeId)
                    {
                        obj.CurrentCount++;
                        OnObjectiveProgress?.Invoke(quest, obj);
                        if (obj.IsComplete)
                        {
                            OnObjectiveComplete?.Invoke(quest, obj);
                        }
                        if (quest.IsComplete)
                        {
                            quest.State = QuestState.Completed;
                            OnQuestCompleted?.Invoke(quest);
                        }
                    }
                }
            }
        }
        
        public void OnResearchCompleted(string researchId)
        {
            foreach (var quest in _activeQuests.Values)
            {
                foreach (var obj in quest.Objectives.Where(o => !o.IsComplete && o.Type == ObjectiveType.Research))
                {
                    obj.CurrentCount++;
                    OnObjectiveProgress?.Invoke(quest, obj);
                    if (obj.IsComplete)
                    {
                        OnObjectiveComplete?.Invoke(quest, obj);
                    }
                    if (quest.IsComplete)
                    {
                        quest.State = QuestState.Completed;
                        OnQuestCompleted?.Invoke(quest);
                    }
                }
            }
        }
        
        public void OnDaySurvived()
        {
            foreach (var quest in _activeQuests.Values)
            {
                foreach (var obj in quest.Objectives.Where(o => !o.IsComplete && o.Type == ObjectiveType.Survive))
                {
                    obj.CurrentCount++;
                    OnObjectiveProgress?.Invoke(quest, obj);
                    if (obj.IsComplete)
                    {
                        OnObjectiveComplete?.Invoke(quest, obj);
                    }
                    if (quest.IsComplete)
                    {
                        quest.State = QuestState.Completed;
                        OnQuestCompleted?.Invoke(quest);
                    }
                }
            }
        }
        
        public void OnLevelUp(int newLevel)
        {
            foreach (var quest in _activeQuests.Values)
            {
                foreach (var obj in quest.Objectives.Where(o => !o.IsComplete && o.Type == ObjectiveType.ReachLevel))
                {
                    if (newLevel >= obj.RequiredCount)
                    {
                        obj.CurrentCount = obj.RequiredCount;
                        OnObjectiveProgress?.Invoke(quest, obj);
                        OnObjectiveComplete?.Invoke(quest, obj);
                        if (quest.IsComplete)
                        {
                            quest.State = QuestState.Completed;
                            OnQuestCompleted?.Invoke(quest);
                        }
                    }
                }
            }
        }
        
        // ============================================
        // HIDDEN QUEST DISCOVERY
        // ============================================
        
        private void DiscoverHiddenQuests(string zoneId)
        {
            // Void Cult quest discovered in Dark Forest
            if (zoneId == "dark_forest" && !_discoveredQuests.Contains("void_01_whispers"))
            {
                _discoveredQuests.Add("void_01_whispers");
                System.Diagnostics.Debug.WriteLine(">>> Hidden Quest Discovered: Whispers in the Dark <<<");
            }
        }
        
        public void DiscoverQuest(string questId)
        {
            if (_definitions.ContainsKey(questId))
            {
                _discoveredQuests.Add(questId);
            }
        }
        
        // ============================================
        // SAVE/LOAD SUPPORT
        // ============================================
        
        public List<string> GetCompletedQuestIds()
        {
            return _completedQuests.ToList();
        }
        
        public void RestoreCompletedQuests(List<string> questIds)
        {
            _completedQuests = new HashSet<string>(questIds);
        }
        
        public void RestoreActiveQuest(string questId, Dictionary<string, int> objectiveProgress)
        {
            var def = GetDefinition(questId);
            if (def == null) return;
            
            var instance = new QuestInstance(def);
            
            // Restore progress
            foreach (var obj in instance.Objectives)
            {
                if (objectiveProgress.ContainsKey(obj.Id))
                {
                    obj.CurrentCount = objectiveProgress[obj.Id];
                }
            }
            
            // Check if already complete
            if (instance.IsComplete)
            {
                instance.State = QuestState.Completed;
            }
            
            _activeQuests[questId] = instance;
        }
        
        public void Reset()
        {
            _activeQuests.Clear();
            _completedQuests.Clear();
            _discoveredQuests.Clear();
            System.Diagnostics.Debug.WriteLine(">>> QuestSystem RESET <<<");
        }
    }
}
