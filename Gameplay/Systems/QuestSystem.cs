// Gameplay/Systems/QuestSystem.cs
// Quest definitions, tracking, and management

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
        Delivery,       // Bring item to NPC
        Exploration     // Visit locations
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
        Kill,           // Kill X enemies (optionally of type Y)
        KillType,       // Kill X of specific enemy type
        Collect,        // Collect X of item
        Deliver,        // Bring item to NPC
        TalkTo,         // Speak with NPC
        Explore,        // Visit a zone
        Build,          // Build a structure
        Craft,          // Craft an item
        Survive         // Survive X days
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
        
        public string GetDisplayText()
        {
            var parts = new List<string>();
            if (Gold > 0) parts.Add($"{Gold} Gold");
            if (XP > 0) parts.Add($"{XP} XP");
            if (MutationPoints > 0) parts.Add($"{MutationPoints} MP");
            if (Items.Count > 0) parts.Add($"{Items.Count} Item(s)");
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
        
        // Objectives
        public List<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();
        
        // Rewards
        public QuestReward Reward { get; set; } = new QuestReward();
        
        // Flags
        public bool IsRepeatable { get; set; } = false;
        public bool AutoComplete { get; set; } = false;  // Complete without turn-in
        
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
            // ==================
            // MAIN QUESTS
            // ==================
            
            // Starting quest - tutorial
            var welcomeQuest = new QuestDefinition("main_welcome", "Welcome to the Wasteland", QuestType.Main)
            {
                Description = "Get your bearings in this harsh new world. Talk to the local trader and prepare for survival.",
                GiverNPCId = null,  // Auto-given at start
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("talk_trader", ObjectiveType.TalkTo, "Talk to the Trader")
                    {
                        TargetId = "trader_start",
                        RequiredCount = 1
                    },
                    new QuestObjective("kill_first", ObjectiveType.Kill, "Defeat your first enemy")
                    {
                        RequiredCount = 1
                    },
                    new QuestObjective("pickup_item", ObjectiveType.Collect, "Pick up any item")
                    {
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 50,
                    XP = 100
                },
                AutoComplete = true
            };
            AddQuest(welcomeQuest);
            
            // First exploration quest
            var exploreQuest = new QuestDefinition("main_explore", "Into the Unknown", QuestType.Main)
            {
                Description = "The wasteland stretches far beyond this camp. Explore the surrounding areas to find resources and threats.",
                GiverNPCId = "trader_start",
                RequiredQuests = new List<string> { "main_welcome" },
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("visit_ruins", ObjectiveType.Explore, "Visit the Ruined Outskirts")
                    {
                        TargetId = "ruins_south",
                        RequiredCount = 1
                    },
                    new QuestObjective("visit_forest", ObjectiveType.Explore, "Visit the Twisted Woods")
                    {
                        TargetId = "forest_west",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 100,
                    XP = 200,
                    Items = new Dictionary<string, int> { ["medkit"] = 2 }
                }
            };
            AddQuest(exploreQuest);
            
            // Settlement quest
            var findHavenQuest = new QuestDefinition("main_haven", "Finding Haven", QuestType.Main)
            {
                Description = "Rumors speak of a safe settlement to the south. Find it and establish contact.",
                GiverNPCId = "trader_start",
                RequiredQuests = new List<string> { "main_explore" },
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("reach_haven", ObjectiveType.Explore, "Find the settlement called Haven")
                    {
                        TargetId = "settlement",
                        RequiredCount = 1
                    },
                    new QuestObjective("talk_haven", ObjectiveType.TalkTo, "Speak with a merchant in Haven")
                    {
                        TargetId = "trader_settlement",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 200,
                    XP = 300,
                    MutationPoints = 1
                }
            };
            AddQuest(findHavenQuest);
            
            // ==================
            // SIDE QUESTS
            // ==================
            
            // Raider bounty
            var raiderBounty = new QuestDefinition("side_raiders", "Raider Trouble", QuestType.Bounty)
            {
                Description = "Raiders have been attacking travelers. Eliminate them to make the roads safer.",
                GiverNPCId = "trader_start",
                IsRepeatable = true,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("kill_raiders", ObjectiveType.KillType, "Kill Raiders")
                    {
                        TargetEnemyType = EnemyType.Raider,
                        RequiredCount = 5
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 75,
                    XP = 150
                }
            };
            AddQuest(raiderBounty);
            
            // Beast hunter
            var beastBounty = new QuestDefinition("side_beasts", "Beast Hunter", QuestType.Bounty)
            {
                Description = "Mutant beasts roam the wilderness. Thin their numbers.",
                GiverNPCId = "trader_start",
                RequiredLevel = 2,
                IsRepeatable = true,
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("kill_beasts", ObjectiveType.KillType, "Kill Mutant Beasts")
                    {
                        TargetEnemyType = EnemyType.MutantBeast,
                        RequiredCount = 3
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 100,
                    XP = 200
                }
            };
            AddQuest(beastBounty);
            
            // Scavenger quest
            var scavengerQuest = new QuestDefinition("side_scavenge", "Scavenger", QuestType.Side)
            {
                Description = "Resources are scarce. Gather materials for the camp.",
                GiverNPCId = "trader_start",
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("collect_metal", ObjectiveType.Collect, "Collect Scrap Metal")
                    {
                        TargetId = "scrap_metal",
                        RequiredCount = 10
                    },
                    new QuestObjective("collect_cloth", ObjectiveType.Collect, "Collect Cloth")
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
            };
            AddQuest(scavengerQuest);
            
            // Cave exploration
            var caveQuest = new QuestDefinition("side_cave", "Into the Depths", QuestType.Side)
            {
                Description = "Rumors speak of valuable resources in the caves to the north. Explore them if you dare.",
                GiverNPCId = "trader_start",
                RequiredLevel = 3,
                RequiredQuests = new List<string> { "main_explore" },
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("find_cave", ObjectiveType.Explore, "Find the Cave Entrance")
                    {
                        TargetId = "cave_entrance",
                        RequiredCount = 1
                    },
                    new QuestObjective("explore_depths", ObjectiveType.Explore, "Reach the Abyssal Depths")
                    {
                        TargetId = "cave_depths",
                        RequiredCount = 1
                    },
                    new QuestObjective("kill_abom", ObjectiveType.KillType, "Defeat an Abomination")
                    {
                        TargetEnemyType = EnemyType.Abomination,
                        RequiredCount = 1,
                        IsOptional = true
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 200,
                    XP = 400,
                    MutationPoints = 2
                }
            };
            AddQuest(caveQuest);
            
            // Building quest
            var builderQuest = new QuestDefinition("side_builder", "Home Sweet Home", QuestType.Side)
            {
                Description = "Establish a base of operations. Build some basic structures.",
                GiverNPCId = "trader_start",
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("build_campfire", ObjectiveType.Build, "Build a Campfire")
                    {
                        TargetId = "Campfire",
                        RequiredCount = 1
                    },
                    new QuestObjective("build_bed", ObjectiveType.Build, "Build a Bed")
                    {
                        TargetId = "Bed",
                        RequiredCount = 1
                    },
                    new QuestObjective("build_storage", ObjectiveType.Build, "Build a Storage Box")
                    {
                        TargetId = "Storage Box",
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 50,
                    XP = 150,
                    Items = new Dictionary<string, int> { ["wood"] = 20, ["scrap_metal"] = 10 }
                }
            };
            AddQuest(builderQuest);
            
            // Crafting quest
            var crafterQuest = new QuestDefinition("side_crafter", "Self-Sufficient", QuestType.Side)
            {
                Description = "Learn to craft your own supplies. It's essential for survival.",
                GiverNPCId = "trader_start",
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective("craft_bandage", ObjectiveType.Craft, "Craft a Cloth Bandage")
                    {
                        TargetId = "bandage_craft",
                        RequiredCount = 1
                    },
                    new QuestObjective("craft_weapon", ObjectiveType.Craft, "Craft any weapon")
                    {
                        RequiredCount = 1
                    }
                },
                Reward = new QuestReward
                {
                    Gold = 40,
                    XP = 100,
                    UnlockRecipes = new List<string> { "medkit_craft" }
                }
            };
            AddQuest(crafterQuest);
            
            System.Diagnostics.Debug.WriteLine($">>> QuestSystem: Initialized {_definitions.Count} quests <<<");
        }
        
        private void AddQuest(QuestDefinition quest)
        {
            _definitions[quest.Id] = quest;
        }
        
        // ============================================
        // QUEST ACCESS
        // ============================================
        
        public QuestDefinition GetDefinition(string id)
        {
            return _definitions.GetValueOrDefault(id);
        }
        
        public List<QuestDefinition> GetAllDefinitions()
        {
            return _definitions.Values.ToList();
        }
        
        public QuestInstance GetActiveQuest(string id)
        {
            return _activeQuests.GetValueOrDefault(id);
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
        
        // ============================================
        // QUEST AVAILABILITY
        // ============================================
        
        public bool CanAcceptQuest(string questId, int playerLevel)
        {
            var def = GetDefinition(questId);
            if (def == null) return false;
            
            // Already active or completed (and not repeatable)
            if (_activeQuests.ContainsKey(questId)) return false;
            if (_completedQuests.Contains(questId) && !def.IsRepeatable) return false;
            
            // Level requirement
            if (playerLevel < def.RequiredLevel) return false;
            
            // Prerequisite quests
            foreach (var reqQuest in def.RequiredQuests)
            {
                if (!_completedQuests.Contains(reqQuest)) return false;
            }
            
            return true;
        }
        
        public List<QuestDefinition> GetAvailableQuests(int playerLevel, string currentZone = null)
        {
            return _definitions.Values
                .Where(q => CanAcceptQuest(q.Id, playerLevel))
                .Where(q => string.IsNullOrEmpty(q.RequiredZone) || q.RequiredZone == currentZone)
                .ToList();
        }
        
        public List<QuestDefinition> GetQuestsFromNPC(string npcId, int playerLevel)
        {
            return GetAvailableQuests(playerLevel)
                .Where(q => q.GiverNPCId == npcId)
                .ToList();
        }
        
        public List<QuestInstance> GetQuestsToTurnIn(string npcId)
        {
            return _activeQuests.Values
                .Where(q => q.State == QuestState.Completed)
                .Where(q => (q.Definition.TurnInNPCId ?? q.Definition.GiverNPCId) == npcId)
                .ToList();
        }
        
        // ============================================
        // QUEST ACTIONS
        // ============================================
        
        public bool AcceptQuest(string questId)
        {
            var def = GetDefinition(questId);
            if (def == null) return false;
            
            // Remove from completed if repeatable
            if (def.IsRepeatable && _completedQuests.Contains(questId))
            {
                _completedQuests.Remove(questId);
            }
            
            var instance = new QuestInstance(def);
            _activeQuests[questId] = instance;
            
            OnQuestStarted?.Invoke(instance);
            System.Diagnostics.Debug.WriteLine($">>> Quest accepted: {def.Name} <<<");
            
            return true;
        }
        
        public bool TurnInQuest(string questId, Action<QuestReward> applyReward)
        {
            var instance = GetActiveQuest(questId);
            if (instance == null || instance.State != QuestState.Completed) return false;
            
            // Apply rewards
            applyReward?.Invoke(instance.Definition.Reward);
            
            // Mark as turned in
            instance.State = QuestState.TurnedIn;
            _activeQuests.Remove(questId);
            _completedQuests.Add(questId);
            
            OnQuestTurnedIn?.Invoke(instance);
            System.Diagnostics.Debug.WriteLine($">>> Quest turned in: {instance.Definition.Name} <<<");
            
            return true;
        }
        
        public void AbandonQuest(string questId)
        {
            if (_activeQuests.ContainsKey(questId))
            {
                _activeQuests.Remove(questId);
                System.Diagnostics.Debug.WriteLine($">>> Quest abandoned: {questId} <<<");
            }
        }
        
        // ============================================
        // PROGRESS TRACKING
        // ============================================
        
        public void OnEnemyKilled(EnemyType enemyType)
        {
            foreach (var quest in _activeQuests.Values)
            {
                if (quest.State != QuestState.Active) continue;
                
                foreach (var obj in quest.Objectives)
                {
                    if (obj.IsComplete) continue;
                    
                    bool progress = false;
                    
                    // Generic kill objective
                    if (obj.Type == ObjectiveType.Kill)
                    {
                        obj.CurrentCount++;
                        progress = true;
                    }
                    // Specific enemy type
                    else if (obj.Type == ObjectiveType.KillType && obj.TargetEnemyType == enemyType)
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
                        CheckQuestCompletion(quest);
                    }
                }
            }
        }
        
        public void OnItemCollected(string itemId, int count = 1)
        {
            foreach (var quest in _activeQuests.Values)
            {
                if (quest.State != QuestState.Active) continue;
                
                foreach (var obj in quest.Objectives)
                {
                    if (obj.IsComplete) continue;
                    
                    // Generic collect (any item) or specific item
                    if (obj.Type == ObjectiveType.Collect)
                    {
                        if (string.IsNullOrEmpty(obj.TargetId) || obj.TargetId == itemId)
                        {
                            obj.CurrentCount += count;
                            OnObjectiveProgress?.Invoke(quest, obj);
                            if (obj.IsComplete)
                            {
                                OnObjectiveComplete?.Invoke(quest, obj);
                            }
                            CheckQuestCompletion(quest);
                        }
                    }
                }
            }
        }
        
        public void OnNPCTalkedTo(string npcId)
        {
            foreach (var quest in _activeQuests.Values)
            {
                if (quest.State != QuestState.Active) continue;
                
                foreach (var obj in quest.Objectives)
                {
                    if (obj.IsComplete) continue;
                    
                    if (obj.Type == ObjectiveType.TalkTo)
                    {
                        // Match specific NPC or any NPC (empty target)
                        if (string.IsNullOrEmpty(obj.TargetId) || obj.TargetId == npcId || 
                            npcId.StartsWith(obj.TargetId.Replace("_start", "_")))  // Match trader_start with trader_settlement etc
                        {
                            obj.CurrentCount++;
                            OnObjectiveProgress?.Invoke(quest, obj);
                            if (obj.IsComplete)
                            {
                                OnObjectiveComplete?.Invoke(quest, obj);
                            }
                            CheckQuestCompletion(quest);
                        }
                    }
                }
            }
        }
        
        public void OnZoneEntered(string zoneId)
        {
            foreach (var quest in _activeQuests.Values)
            {
                if (quest.State != QuestState.Active) continue;
                
                foreach (var obj in quest.Objectives)
                {
                    if (obj.IsComplete) continue;
                    
                    if (obj.Type == ObjectiveType.Explore && obj.TargetId == zoneId)
                    {
                        obj.CurrentCount++;
                        OnObjectiveProgress?.Invoke(quest, obj);
                        if (obj.IsComplete)
                        {
                            OnObjectiveComplete?.Invoke(quest, obj);
                        }
                        CheckQuestCompletion(quest);
                    }
                }
            }
        }
        
        public void OnStructureBuilt(string structureName)
        {
            foreach (var quest in _activeQuests.Values)
            {
                if (quest.State != QuestState.Active) continue;
                
                foreach (var obj in quest.Objectives)
                {
                    if (obj.IsComplete) continue;
                    
                    if (obj.Type == ObjectiveType.Build)
                    {
                        if (string.IsNullOrEmpty(obj.TargetId) || obj.TargetId == structureName)
                        {
                            obj.CurrentCount++;
                            OnObjectiveProgress?.Invoke(quest, obj);
                            if (obj.IsComplete)
                            {
                                OnObjectiveComplete?.Invoke(quest, obj);
                            }
                            CheckQuestCompletion(quest);
                        }
                    }
                }
            }
        }
        
        public void OnItemCrafted(string recipeId)
        {
            foreach (var quest in _activeQuests.Values)
            {
                if (quest.State != QuestState.Active) continue;
                
                foreach (var obj in quest.Objectives)
                {
                    if (obj.IsComplete) continue;
                    
                    if (obj.Type == ObjectiveType.Craft)
                    {
                        if (string.IsNullOrEmpty(obj.TargetId) || obj.TargetId == recipeId)
                        {
                            obj.CurrentCount++;
                            OnObjectiveProgress?.Invoke(quest, obj);
                            if (obj.IsComplete)
                            {
                                OnObjectiveComplete?.Invoke(quest, obj);
                            }
                            CheckQuestCompletion(quest);
                        }
                    }
                }
            }
        }
        
        private void CheckQuestCompletion(QuestInstance quest)
        {
            if (quest.IsComplete && quest.State == QuestState.Active)
            {
                quest.State = QuestState.Completed;
                OnQuestCompleted?.Invoke(quest);
                System.Diagnostics.Debug.WriteLine($">>> Quest ready to turn in: {quest.Definition.Name} <<<");
                
                // Auto-complete quests don't need turn-in
                if (quest.Definition.AutoComplete)
                {
                    // Will be turned in next frame with rewards
                }
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
        
        /// <summary>
        /// Reset all quest state (for new game)
        /// </summary>
        public void Reset()
        {
            _activeQuests.Clear();
            _completedQuests.Clear();
            System.Diagnostics.Debug.WriteLine(">>> QuestSystem RESET <<<");
        }
    }
}
