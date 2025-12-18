// Gameplay/Systems/ResearchSystem.cs
// Tech tree and research progression system

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
        Tinker,         // Conventional technology
        Dark,           // Anomaly-based science
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
        public float Progress { get; set; } = 0f;  // 0-100%

        public ResearchNode(string id, string name, ResearchCategory category)
        {
            Id = id;
            Name = name;
            Category = category;
        }

        public bool IsComplete => State == ResearchState.Completed;
        public float ProgressPercent => Progress / ResearchTime * 100f;
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
        // INITIALIZATION
        // ============================================

        private void InitializeResearchTree()
        {
            // ==================
            // SURVIVAL TREE (All paths)
            // ==================

            AddNode(new ResearchNode("survival_basics", "Survival Basics", ResearchCategory.Survival)
            {
                Description = "Learn fundamental survival techniques.",
                Tier = 1,
                ResearchTime = 30,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 5 },
                UnlocksRecipes = new List<string> { "campfire_craft", "torch_craft" },
                StatBonuses = new Dictionary<string, float> { ["HungerRate"] = -0.1f }
            });

            AddNode(new ResearchNode("water_purification", "Water Purification", ResearchCategory.Survival)
            {
                Description = "Methods to make water safe for drinking.",
                Tier = 1,
                Prerequisites = new List<string> { "survival_basics" },
                ResearchTime = 45,
                ResourceCost = new Dictionary<string, int> { ["cloth"] = 5, ["scrap_metal"] = 3 },
                UnlocksRecipes = new List<string> { "water_filter_craft" },
                StatBonuses = new Dictionary<string, float> { ["ThirstRate"] = -0.1f }
            });

            AddNode(new ResearchNode("advanced_medicine", "Advanced Medicine", ResearchCategory.Survival)
            {
                Description = "Create more effective healing items.",
                Tier = 2,
                Prerequisites = new List<string> { "survival_basics" },
                ResearchTime = 90,
                ResourceCost = new Dictionary<string, int> { ["cloth"] = 10, ["herbs"] = 5 },
                RequiredLevel = 3,
                UnlocksRecipes = new List<string> { "medkit_craft", "antidote_craft" }
            });

            AddNode(new ResearchNode("preservation", "Food Preservation", ResearchCategory.Survival)
            {
                Description = "Keep food fresh longer.",
                Tier = 2,
                Prerequisites = new List<string> { "water_purification" },
                ResearchTime = 60,
                ResourceCost = new Dictionary<string, int> { ["salt"] = 5, ["wood"] = 10 },
                UnlocksStructures = new List<string> { "Smokehouse", "IceBox" },
                StatBonuses = new Dictionary<string, float> { ["FoodDecayRate"] = -0.25f }
            });

            AddNode(new ResearchNode("shelter_mastery", "Shelter Mastery", ResearchCategory.Survival)
            {
                Description = "Build stronger, more efficient shelters.",
                Tier = 3,
                Prerequisites = new List<string> { "preservation" },
                ResearchTime = 120,
                ResourceCost = new Dictionary<string, int> { ["wood"] = 30, ["scrap_metal"] = 15 },
                RequiredLevel = 5,
                UnlocksStructures = new List<string> { "ReinforcedWall", "InsulatedBed" },
                StatBonuses = new Dictionary<string, float> { ["RestEfficiency"] = 0.25f }
            });

            // ==================
            // COMBAT TREE (All paths)
            // ==================

            AddNode(new ResearchNode("combat_basics", "Combat Training", ResearchCategory.Combat)
            {
                Description = "Basic combat techniques and weapon maintenance.",
                Tier = 1,
                ResearchTime = 45,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 8 },
                UnlocksRecipes = new List<string> { "knife_craft", "club_craft" },
                StatBonuses = new Dictionary<string, float> { ["BaseDamage"] = 1f }
            });

            AddNode(new ResearchNode("armor_crafting", "Armor Crafting", ResearchCategory.Combat)
            {
                Description = "Create protective gear from scavenged materials.",
                Tier = 1,
                Prerequisites = new List<string> { "combat_basics" },
                ResearchTime = 60,
                ResourceCost = new Dictionary<string, int> { ["cloth"] = 10, ["scrap_metal"] = 10 },
                UnlocksRecipes = new List<string> { "leather_armor_craft", "scrap_helmet_craft" }
            });

            AddNode(new ResearchNode("weapon_smithing", "Weapon Smithing", ResearchCategory.Combat)
            {
                Description = "Forge better weapons from metal.",
                Tier = 2,
                Prerequisites = new List<string> { "combat_basics" },
                ResearchTime = 90,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 20, ["components"] = 5 },
                RequiredLevel = 3,
                UnlocksRecipes = new List<string> { "machete_craft", "spear_craft" },
                UnlocksStructures = new List<string> { "Forge" }
            });

            AddNode(new ResearchNode("tactical_training", "Tactical Training", ResearchCategory.Combat)
            {
                Description = "Advanced combat maneuvers.",
                Tier = 3,
                Prerequisites = new List<string> { "weapon_smithing", "armor_crafting" },
                ResearchTime = 120,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 15, ["cloth"] = 15 },
                RequiredLevel = 5,
                StatBonuses = new Dictionary<string, float> { ["CritChance"] = 0.05f, ["DodgeChance"] = 0.05f }
            });

            // ==================
            // TINKER TREE (Tinker path only)
            // ==================

            AddNode(new ResearchNode("tinker_fundamentals", "Tinker Fundamentals", ResearchCategory.Tinker)
            {
                Description = "Basic principles of pre-war technology.",
                Tier = 1,
                RequiredPath = SciencePath.Tinker,
                ResearchTime = 60,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 10, ["components"] = 3 },
                UnlocksRecipes = new List<string> { "repair_kit_craft" },
                UnlocksStructures = new List<string> { "Workbench" }
            });

            AddNode(new ResearchNode("electronics", "Electronics", ResearchCategory.Tinker)
            {
                Description = "Salvage and repair electronic devices.",
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
                Description = "Construct and maintain firearms.",
                Tier = 2,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "tinker_fundamentals", "weapon_smithing" },
                ResearchTime = 120,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 25, ["components"] = 10 },
                RequiredLevel = 4,
                UnlocksRecipes = new List<string> { "pistol_craft", "ammo_craft" }
            });

            AddNode(new ResearchNode("automation", "Automation", ResearchCategory.Tinker)
            {
                Description = "Build machines that work for you.",
                Tier = 3,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "electronics" },
                ResearchTime = 150,
                ResourceCost = new Dictionary<string, int> { ["scrap_electronics"] = 20, ["components"] = 15, ["scrap_metal"] = 20 },
                RequiredLevel = 6,
                UnlocksStructures = new List<string> { "AutoTurret", "WaterPump", "Fabricator" }
            });

            AddNode(new ResearchNode("power_armor", "Power Armor", ResearchCategory.Tinker)
            {
                Description = "The pinnacle of pre-war military technology.",
                Tier = 4,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "automation", "ballistics", "tactical_training" },
                ResearchTime = 300,
                ResourceCost = new Dictionary<string, int> { ["scrap_metal"] = 50, ["components"] = 30, ["scrap_electronics"] = 25 },
                RequiredLevel = 8,
                UnlocksRecipes = new List<string> { "power_armor_craft" },
                StatBonuses = new Dictionary<string, float> { ["MaxHealth"] = 50f, ["Armor"] = 10f }
            });

            AddNode(new ResearchNode("energy_weapons", "Energy Weapons", ResearchCategory.Tinker)
            {
                Description = "Harness energy for devastating weapons.",
                Tier = 5,
                RequiredPath = SciencePath.Tinker,
                Prerequisites = new List<string> { "power_armor" },
                ResearchTime = 360,
                ResourceCost = new Dictionary<string, int> { ["scrap_electronics"] = 40, ["components"] = 25, ["energy_cell"] = 10 },
                RequiredLevel = 10,
                UnlocksRecipes = new List<string> { "laser_rifle_craft", "plasma_cutter_craft" }
            });

            // ==================
            // DARK SCIENCE TREE (Dark path only)
            // ==================

            AddNode(new ResearchNode("dark_initiation", "Dark Initiation", ResearchCategory.Dark)
            {
                Description = "Begin to understand the anomalies that mutated your ancestors.",
                Tier = 1,
                RequiredPath = SciencePath.Dark,
                ResearchTime = 60,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 3, ["bone"] = 5 },
                UnlocksMutations = new List<MutationType> { MutationType.NightVision },
                UnlocksStructures = new List<string> { "RitualCircle" }
            });

            AddNode(new ResearchNode("mutation_control", "Mutation Control", ResearchCategory.Dark)
            {
                Description = "Learn to guide your body's mutations.",
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
                Description = "Shape living tissue into tools and weapons.",
                Tier = 2,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "dark_initiation" },
                ResearchTime = 120,
                ResourceCost = new Dictionary<string, int> { ["bone"] = 15, ["sinew"] = 10, ["anomaly_shard"] = 5 },
                RequiredLevel = 4,
                UnlocksRecipes = new List<string> { "bone_blade_craft", "chitin_armor_craft" }
            });

            AddNode(new ResearchNode("psionic_awakening", "Psionic Awakening", ResearchCategory.Dark)
            {
                Description = "Unlock the latent psychic potential in mutant minds.",
                Tier = 3,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "mutation_control" },
                ResearchTime = 150,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 10, ["brain_tissue"] = 3 },
                RequiredLevel = 6,
                UnlocksMutations = new List<MutationType> { MutationType.FearAura, MutationType.Telepathy },
                UnlocksAbility = "mind_blast"
            });

            AddNode(new ResearchNode("hive_connection", "Hive Connection", ResearchCategory.Dark)
            {
                Description = "Tap into the collective consciousness of mutant-kind.",
                Tier = 4,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "psionic_awakening", "flesh_crafting" },
                ResearchTime = 240,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 20, ["brain_tissue"] = 5, ["mutagen"] = 10 },
                RequiredLevel = 8,
                UnlocksMutations = new List<MutationType> { MutationType.VoidTouch },
                StatBonuses = new Dictionary<string, float> { ["XPGain"] = 0.25f, ["DetectionRange"] = 3f }
            });

            AddNode(new ResearchNode("apotheosis", "Apotheosis", ResearchCategory.Dark)
            {
                Description = "Transcend your mortal form.",
                Tier = 5,
                RequiredPath = SciencePath.Dark,
                Prerequisites = new List<string> { "hive_connection" },
                ResearchTime = 360,
                ResourceCost = new Dictionary<string, int> { ["anomaly_shard"] = 30, ["essence"] = 5, ["mutagen"] = 20 },
                RequiredLevel = 10,
                UnlocksMutations = new List<MutationType> { MutationType.UnstableForm },
                StatBonuses = new Dictionary<string, float> { ["MaxHealth"] = 100f, ["HealthRegen"] = 1f, ["AllResistance"] = 0.1f }
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
            return _nodes.Values.Where(n => n.Category == category).OrderBy(n => n.Tier).ToList();
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
            if (_currentResearch != null) return false;  // Already researching something

            // Check resources
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

            // Update availability for newly unlocked nodes
            UpdateAvailability(10);  // Will be called with actual level from game
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

            // Reset all nodes
            foreach (var node in _nodes.Values)
            {
                node.State = ResearchState.Locked;
                node.Progress = 0f;
            }

            // Mark completed
            foreach (var id in completedIds)
            {
                var node = GetNode(id);
                if (node != null)
                {
                    node.State = ResearchState.Completed;
                    node.Progress = node.ResearchTime;
                }
            }

            // Restore in-progress
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