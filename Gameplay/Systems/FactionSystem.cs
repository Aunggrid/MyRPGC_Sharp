// Gameplay/Systems/FactionSystem.cs
// Faction reputation and relationship system
// Tracks player standing with all factions, affects trading and hostility

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;

namespace MyRPG.Gameplay.Systems
{
    // NOTE: FactionStanding enum is defined in Data/Enums.cs
    
    // ============================================
    // FACTION DEFINITION (Static Data)
    // ============================================
    
    public class FactionDefinition
    {
        public FactionType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ShortDesc { get; set; }
        
        // Lore
        public string Philosophy { get; set; }
        public string Territory { get; set; }
        public CurrencyType PreferredCurrency { get; set; }
        public string Leader { get; set; }
        
        // How this faction views mutants by default (-100 to 100)
        public int DefaultMutantAttitude { get; set; } = 0;
        
        // Gameplay modifiers
        public float TradeMarkup { get; set; } = 1.0f;
        public float TradeSellRate { get; set; } = 0.5f;
        public bool WillAttackMutants { get; set; } = false;
        public bool CanBeAllied { get; set; } = true;
        
        // UI
        public string ColorHex { get; set; } = "#FFFFFF";
        
        public FactionDefinition(FactionType type, string name)
        {
            Type = type;
            Name = name;
        }
    }
    
    // ============================================
    // FACTION STANDING DATA (Per-Player Runtime)
    // ============================================
    
    public class FactionStandingData
    {
        public FactionType Faction { get; set; }
        public int Reputation { get; set; } = 0;
        
        // History
        public int EnemiesKilled { get; set; } = 0;
        public int QuestsCompleted { get; set; } = 0;
        public int ItemsTraded { get; set; } = 0;
        public int GoldSpent { get; set; } = 0;
        public int GoldEarned { get; set; } = 0;
        
        // Special flags
        public bool IsBanned { get; set; } = false;
        public bool HasSpecialStatus { get; set; } = false;
        public string SpecialTitle { get; set; } = null;
        
        // Timestamps
        public float LastInteractionDay { get; set; } = 0f;
        
        public FactionStandingData(FactionType faction)
        {
            Faction = faction;
        }
        
        public FactionStanding Standing
        {
            get
            {
                if (IsBanned) return FactionStanding.Hated;
                
                return Reputation switch
                {
                    >= 80 => FactionStanding.Revered,
                    >= 60 => FactionStanding.Allied,
                    >= 30 => FactionStanding.Friendly,
                    >= -10 => FactionStanding.Neutral,
                    >= -40 => FactionStanding.Unfriendly,
                    >= -70 => FactionStanding.Hostile,
                    _ => FactionStanding.Hated
                };
            }
        }
        
        public float GetPriceModifier()
        {
            return Standing switch
            {
                FactionStanding.Revered => 0.7f,
                FactionStanding.Allied => 0.8f,
                FactionStanding.Friendly => 0.9f,
                FactionStanding.Neutral => 1.0f,
                FactionStanding.Unfriendly => 1.2f,
                FactionStanding.Hostile => 1.5f,
                FactionStanding.Hated => 2.0f,
                _ => 1.0f
            };
        }
        
        public float GetSellModifier()
        {
            return Standing switch
            {
                FactionStanding.Revered => 0.7f,
                FactionStanding.Allied => 0.6f,
                FactionStanding.Friendly => 0.55f,
                FactionStanding.Neutral => 0.5f,
                FactionStanding.Unfriendly => 0.4f,
                FactionStanding.Hostile => 0.3f,
                FactionStanding.Hated => 0.2f,
                _ => 0.5f
            };
        }
    }
    
    // ============================================
    // FACTION SYSTEM
    // ============================================
    
    public class FactionSystem
    {
        private Dictionary<FactionType, FactionDefinition> _definitions = new Dictionary<FactionType, FactionDefinition>();
        private Dictionary<FactionType, FactionStandingData> _standings = new Dictionary<FactionType, FactionStandingData>();
        private float _currentDay = 0f;
        
        // Events
        public event Action<FactionType, FactionStanding, FactionStanding> OnStandingChanged;
        public event Action<FactionType, int> OnReputationChanged;
        public event Action<FactionType> OnFactionBecameHostile;
        public event Action<FactionType> OnFactionBecameAllied;
        public event Action<string> OnFactionMessage;
        
        public FactionSystem()
        {
            InitializeFactions();
            InitializePlayerStandings();
        }
        
        private void InitializeFactions()
        {
            // THE CHANGED
            _definitions[FactionType.TheChanged] = new FactionDefinition(FactionType.TheChanged, "The Changed")
            {
                Description = "Your fellow mutants - survivors of the Severance.",
                ShortDesc = "Fellow mutants",
                Philosophy = "Survival through adaptation.",
                PreferredCurrency = CurrencyType.VoidShard,
                DefaultMutantAttitude = 50,
                WillAttackMutants = false,
                TradeMarkup = 0.9f,
                TradeSellRate = 0.55f,
                ColorHex = "#8B4513"
            };
            
            _definitions[FactionType.GeneElders] = new FactionDefinition(FactionType.GeneElders, "Gene-Elders")
            {
                Description = "Ancient mutant tribal leaders.",
                ShortDesc = "Mutant elders",
                Philosophy = "Mutation is sacred.",
                PreferredCurrency = CurrencyType.MutantFavor,
                DefaultMutantAttitude = 70,
                WillAttackMutants = false,
                TradeMarkup = 1.2f,
                TradeSellRate = 0.6f,
                ColorHex = "#9932CC"
            };
            
            _definitions[FactionType.VoidCult] = new FactionDefinition(FactionType.VoidCult, "Void Cult")
            {
                Description = "Dark Science worshippers of the Void.",
                ShortDesc = "Void worshippers",
                Philosophy = "Embrace the Void.",
                PreferredCurrency = CurrencyType.VoidShard,
                DefaultMutantAttitude = 30,
                WillAttackMutants = false,
                TradeMarkup = 1.3f,
                TradeSellRate = 0.45f,
                ColorHex = "#4B0082"
            };
            
            // THE TRIAD
            _definitions[FactionType.UnitedSanctum] = new FactionDefinition(FactionType.UnitedSanctum, "United Sanctum")
            {
                Description = "High-tech military - views mutants as hazards.",
                ShortDesc = "Tech military",
                Philosophy = "Purity through technology.",
                PreferredCurrency = CurrencyType.SanctumCredits,
                DefaultMutantAttitude = -80,
                WillAttackMutants = true,
                TradeMarkup = 1.4f,
                TradeSellRate = 0.3f,
                ColorHex = "#4169E1"
            };
            
            _definitions[FactionType.IronSyndicate] = new FactionDefinition(FactionType.IronSyndicate, "Iron Syndicate")
            {
                Description = "Trade kingdom - views mutants as labor.",
                ShortDesc = "Merchants",
                Philosophy = "Everything has a price.",
                PreferredCurrency = CurrencyType.Gold,
                DefaultMutantAttitude = -20,
                WillAttackMutants = false,
                TradeMarkup = 1.1f,
                TradeSellRate = 0.55f,
                ColorHex = "#FFD700"
            };
            
            _definitions[FactionType.VerdantOrder] = new FactionDefinition(FactionType.VerdantOrder, "Verdant Order")
            {
                Description = "Bio-engineers - views mutants as specimens.",
                ShortDesc = "Bio-engineers",
                Philosophy = "Life is sacred, study it.",
                PreferredCurrency = CurrencyType.VerdantTithes,
                DefaultMutantAttitude = -60,
                WillAttackMutants = true,
                TradeMarkup = 1.3f,
                TradeSellRate = 0.4f,
                ColorHex = "#228B22"
            };
            
            // NEUTRAL
            _definitions[FactionType.Traders] = new FactionDefinition(FactionType.Traders, "Traders Guild")
            {
                Description = "Neutral merchants who trade with all.",
                ShortDesc = "Neutral traders",
                Philosophy = "Neutrality is survival.",
                PreferredCurrency = CurrencyType.TradeBeads,
                DefaultMutantAttitude = 20,
                WillAttackMutants = false,
                TradeMarkup = 1.0f,
                TradeSellRate = 0.5f,
                ColorHex = "#DAA520"
            };
            
            _definitions[FactionType.Wildlife] = new FactionDefinition(FactionType.Wildlife, "Wildlife")
            {
                Description = "Mutated animals of the Zone.",
                DefaultMutantAttitude = 0,
                CanBeAllied = false,
                ColorHex = "#8B4513"
            };
            
            _definitions[FactionType.VoidSpawn] = new FactionDefinition(FactionType.VoidSpawn, "Void Spawn")
            {
                Description = "Hostile creatures from the Void.",
                DefaultMutantAttitude = -100,
                WillAttackMutants = true,
                CanBeAllied = false,
                ColorHex = "#800080"
            };
            
            // BANDITS (No reputation impact - hostile to everyone)
            _definitions[FactionType.Bandits] = new FactionDefinition(FactionType.Bandits, "Bandits")
            {
                Description = "Lawless raiders who attack anyone. No faction ties.",
                ShortDesc = "Hostile raiders",
                Philosophy = "Take what you can.",
                DefaultMutantAttitude = -100,  // Always hostile
                WillAttackMutants = true,
                CanBeAllied = false,           // Cannot improve relations
                ColorHex = "#8B0000"           // Dark red
            };
        }
        
        private void InitializePlayerStandings()
        {
            foreach (var factionType in Enum.GetValues(typeof(FactionType)).Cast<FactionType>())
            {
                if (factionType == FactionType.Player) continue;
                
                var standing = new FactionStandingData(factionType);
                if (_definitions.TryGetValue(factionType, out var def))
                {
                    standing.Reputation = def.DefaultMutantAttitude;
                }
                _standings[factionType] = standing;
            }
        }
        
        // ============================================
        // REPUTATION CHANGES
        // ============================================
        
        public void ModifyReputation(FactionType faction, int amount, string reason = null)
        {
            if (faction == FactionType.Player) return;
            if (!_standings.TryGetValue(faction, out var standing)) return;
            
            var oldStanding = standing.Standing;
            standing.Reputation = Math.Clamp(standing.Reputation + amount, -100, 100);
            standing.LastInteractionDay = _currentDay;
            
            var newStanding = standing.Standing;
            
            OnReputationChanged?.Invoke(faction, amount);
            
            if (oldStanding != newStanding)
            {
                OnStandingChanged?.Invoke(faction, oldStanding, newStanding);
                
                if (newStanding == FactionStanding.Allied)
                {
                    OnFactionBecameAllied?.Invoke(faction);
                    OnFactionMessage?.Invoke($"🤝 Now Allied with {_definitions[faction].Name}!");
                }
                else if (newStanding == FactionStanding.Hostile)
                {
                    OnFactionBecameHostile?.Invoke(faction);
                    OnFactionMessage?.Invoke($"⚔️ {_definitions[faction].Name} is now Hostile!");
                }
            }
            
            ApplyRippleEffects(faction, amount);
            
            if (!string.IsNullOrEmpty(reason))
            {
                string changeText = amount >= 0 ? $"+{amount}" : $"{amount}";
                OnFactionMessage?.Invoke($"{_definitions[faction].Name}: {changeText} ({reason})");
            }
        }
        
        private void ApplyRippleEffects(FactionType sourceFaction, int amount)
        {
            var relationships = GetFactionRelationships(sourceFaction);
            
            foreach (var (otherFaction, relationship) in relationships)
            {
                if (!_standings.TryGetValue(otherFaction, out var standing)) continue;
                
                int rippleAmount = (int)(amount * relationship * 0.3f);
                if (rippleAmount != 0)
                {
                    standing.Reputation = Math.Clamp(standing.Reputation + rippleAmount, -100, 100);
                }
            }
        }
        
        private Dictionary<FactionType, float> GetFactionRelationships(FactionType faction)
        {
            var result = new Dictionary<FactionType, float>();
            
            switch (faction)
            {
                case FactionType.TheChanged:
                    result[FactionType.GeneElders] = 0.6f;
                    result[FactionType.VoidCult] = 0.2f;
                    result[FactionType.UnitedSanctum] = -0.8f;
                    result[FactionType.VerdantOrder] = -0.6f;
                    break;
                    
                case FactionType.UnitedSanctum:
                    result[FactionType.TheChanged] = -0.8f;
                    result[FactionType.VoidCult] = -1.0f;
                    result[FactionType.VerdantOrder] = 0.3f;
                    break;
                    
                case FactionType.VerdantOrder:
                    result[FactionType.TheChanged] = -0.6f;
                    result[FactionType.UnitedSanctum] = 0.3f;
                    break;
            }
            
            return result;
        }
        
        // ============================================
        // GAMEPLAY EVENTS
        // ============================================
        
        public void OnEnemyKilled(EnemyType enemyType, bool wasProvoked = false)
        {
            FactionType faction = GetFactionForEnemy(enemyType);
            
            // BANDITS and WILDLIFE - No reputation impact!
            // These are lawless raiders and animals, not faction members
            if (faction == FactionType.Bandits || faction == FactionType.Wildlife)
            {
                return;  // No reputation change for killing bandits/animals
            }
            
            if (!_standings.TryGetValue(faction, out var standing)) return;
            
            standing.EnemiesKilled++;
            
            int loss = wasProvoked ? -2 : -5;
            if (faction == FactionType.UnitedSanctum || faction == FactionType.VerdantOrder)
                loss *= 2;
            
            ModifyReputation(faction, loss, "killed member");
            
            // Bonus with enemy factions
            if (faction == FactionType.UnitedSanctum)
            {
                ModifyReputation(FactionType.TheChanged, 1, "killed Sanctum");
            }
            else if (faction == FactionType.VerdantOrder)
            {
                ModifyReputation(FactionType.TheChanged, 1, "killed Verdant");
            }
        }
        
        public void OnQuestCompleted(FactionType faction, int difficulty = 1)
        {
            if (!_standings.TryGetValue(faction, out var standing)) return;
            standing.QuestsCompleted++;
            ModifyReputation(faction, 5 + difficulty * 3, "quest");
        }
        
        public void OnTradeCompleted(FactionType faction, int goldValue, bool playerBought)
        {
            if (!_standings.TryGetValue(faction, out var standing)) return;
            standing.ItemsTraded++;
            if (goldValue >= 50)
                ModifyReputation(faction, 1, "trade");
        }
        
        // ============================================
        // QUERIES
        // ============================================
        
        public FactionDefinition GetDefinition(FactionType faction) =>
            _definitions.TryGetValue(faction, out var def) ? def : null;
        
        public FactionStandingData GetStanding(FactionType faction) =>
            _standings.TryGetValue(faction, out var s) ? s : null;
        
        public FactionStanding GetStandingLevel(FactionType faction) =>
            _standings.TryGetValue(faction, out var s) ? s.Standing : FactionStanding.Neutral;
        
        public int GetReputation(FactionType faction) =>
            _standings.TryGetValue(faction, out var s) ? s.Reputation : 0;
        
        public bool IsHostile(FactionType faction)
        {
            // Always hostile factions
            if (faction == FactionType.VoidSpawn) return true;
            if (faction == FactionType.Bandits) return true;
            
            var standing = GetStandingLevel(faction);
            return standing == FactionStanding.Hostile || standing == FactionStanding.Hated;
        }
        
        public bool WillTrade(FactionType faction)
        {
            if (!_definitions.TryGetValue(faction, out var def)) return false;
            if (!def.CanBeAllied) return false;
            return GetStandingLevel(faction) != FactionStanding.Hated;
        }
        
        public float GetBuyPriceMultiplier(FactionType faction)
        {
            if (!_definitions.TryGetValue(faction, out var def)) return 1f;
            if (!_standings.TryGetValue(faction, out var standing)) return 1f;
            return def.TradeMarkup * standing.GetPriceModifier();
        }
        
        public float GetSellPriceMultiplier(FactionType faction)
        {
            if (!_definitions.TryGetValue(faction, out var def)) return 0.5f;
            if (!_standings.TryGetValue(faction, out var standing)) return 0.5f;
            return def.TradeSellRate * (standing.GetSellModifier() / 0.5f);
        }
        
        public FactionType GetFactionForEnemy(EnemyType enemyType)
        {
            return enemyType switch
            {
                // BANDITS - Lawless raiders, NO reputation impact when killed
                EnemyType.Raider => FactionType.Bandits,
                EnemyType.Hunter => FactionType.Bandits,  // Hostile hunters are bandits
                
                // THE CHANGED - Only specific hostile mutants (rare encounters)
                EnemyType.Abomination => FactionType.TheChanged,  // Corrupted mutants
                EnemyType.Spitter => FactionType.TheChanged,
                EnemyType.Psionic => FactionType.TheChanged,
                EnemyType.Brute => FactionType.TheChanged,
                EnemyType.Stalker => FactionType.TheChanged,
                EnemyType.HiveMother => FactionType.TheChanged,
                EnemyType.Swarmling => FactionType.TheChanged,
                
                // UNITED SANCTUM
                EnemyType.SanctumTrooper => FactionType.UnitedSanctum,
                EnemyType.SanctumEnforcer => FactionType.UnitedSanctum,
                EnemyType.SanctumCommando => FactionType.UnitedSanctum,
                EnemyType.PurgeDrone => FactionType.UnitedSanctum,
                
                // IRON SYNDICATE
                EnemyType.SyndicateMerc => FactionType.IronSyndicate,
                EnemyType.SyndicateHeavy => FactionType.IronSyndicate,
                EnemyType.SlaveDriver => FactionType.IronSyndicate,
                EnemyType.SyndicateMech => FactionType.IronSyndicate,
                
                // VERDANT ORDER
                EnemyType.VerdantCollector => FactionType.VerdantOrder,
                EnemyType.VerdantPurifier => FactionType.VerdantOrder,
                EnemyType.VerdantBiomancer => FactionType.VerdantOrder,
                EnemyType.GeneHound => FactionType.VerdantOrder,
                
                // VOID SPAWN
                EnemyType.VoidWraith => FactionType.VoidSpawn,
                EnemyType.VoidCrawler => FactionType.VoidSpawn,
                EnemyType.VoidHorror => FactionType.VoidSpawn,
                
                // WILDLIFE - Animals, no reputation impact
                EnemyType.MutantBeast => FactionType.Wildlife,
                EnemyType.Scavenger => FactionType.Wildlife,
                EnemyType.GiantInsect => FactionType.Wildlife,
                EnemyType.WildBoar => FactionType.Wildlife,
                EnemyType.MutantDeer => FactionType.Wildlife,
                EnemyType.CaveSlug => FactionType.Wildlife,
                
                _ => FactionType.Wildlife
            };
        }
        
        public void Update(float currentDay)
        {
            _currentDay = currentDay;
            
            // Slow decay toward default
            foreach (var standing in _standings.Values)
            {
                if (_currentDay - standing.LastInteractionDay > 7f)
                {
                    var def = GetDefinition(standing.Faction);
                    if (def == null) continue;
                    
                    int target = def.DefaultMutantAttitude;
                    if (standing.Reputation > target) standing.Reputation--;
                    else if (standing.Reputation < target) standing.Reputation++;
                    
                    standing.LastInteractionDay = _currentDay;
                }
            }
        }
        
        // ============================================
        // SERIALIZATION
        // ============================================
        
        public Dictionary<FactionType, int> GetReputationSnapshot() =>
            _standings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Reputation);
        
        public void LoadReputationSnapshot(Dictionary<FactionType, int> snapshot)
        {
            foreach (var kvp in snapshot)
            {
                if (_standings.TryGetValue(kvp.Key, out var standing))
                    standing.Reputation = kvp.Value;
            }
        }
        
        // ============================================
        // ENEMY TYPE TO FACTION MAPPING
        // ============================================
        
        /// <summary>
        /// Determine which faction an enemy belongs to based on their type
        /// </summary>
        public static FactionType? GetFactionFromEnemyType(EnemyType enemyType)
        {
            return enemyType switch
            {
                // United Sanctum (Tech Kingdom)
                EnemyType.SanctumTrooper => FactionType.UnitedSanctum,
                EnemyType.SanctumEnforcer => FactionType.UnitedSanctum,
                EnemyType.SanctumCommando => FactionType.UnitedSanctum,
                EnemyType.PurgeDrone => FactionType.UnitedSanctum,
                
                // Iron Syndicate (Trade Kingdom)
                EnemyType.SyndicateMerc => FactionType.IronSyndicate,
                EnemyType.SyndicateHeavy => FactionType.IronSyndicate,
                EnemyType.SlaveDriver => FactionType.IronSyndicate,
                EnemyType.SyndicateMech => FactionType.IronSyndicate,
                
                // Verdant Order (Religious Kingdom)
                EnemyType.VerdantCollector => FactionType.VerdantOrder,
                EnemyType.VerdantPurifier => FactionType.VerdantOrder,
                EnemyType.VerdantBiomancer => FactionType.VerdantOrder,
                EnemyType.GeneHound => FactionType.VerdantOrder,
                
                // Void Spawn (Void creatures)
                EnemyType.VoidWraith => FactionType.VoidSpawn,
                EnemyType.VoidCrawler => FactionType.VoidSpawn,
                EnemyType.VoidHorror => FactionType.VoidSpawn,
                
                // The Changed (Mutants) - usually not hostile to player
                EnemyType.Psionic => FactionType.TheChanged,
                EnemyType.Brute => FactionType.TheChanged,
                EnemyType.Stalker => FactionType.TheChanged,
                EnemyType.HiveMother => FactionType.TheChanged,
                EnemyType.Swarmling => FactionType.TheChanged,
                EnemyType.Spitter => FactionType.TheChanged,
                
                // Bandits (Hostile raiders - no faction)
                EnemyType.Raider => FactionType.Bandits,
                EnemyType.Abomination => FactionType.Bandits,
                
                // Wildlife (No faction affiliation)
                EnemyType.MutantBeast => FactionType.Wildlife,
                EnemyType.Scavenger => FactionType.Wildlife,
                EnemyType.GiantInsect => FactionType.Wildlife,
                EnemyType.WildBoar => FactionType.Wildlife,
                EnemyType.MutantDeer => FactionType.Wildlife,
                EnemyType.CaveSlug => FactionType.Wildlife,
                EnemyType.Hunter => FactionType.Wildlife,
                
                _ => null
            };
        }
        
        // ============================================
        // DEBUG / UI
        // ============================================
        
        public string GetFactionSummary()
        {
            var lines = new List<string> { "=== FACTION STANDINGS ===" };
            
            foreach (var faction in _standings.Values.OrderByDescending(s => s.Reputation))
            {
                var def = GetDefinition(faction.Faction);
                if (def == null) continue;
                lines.Add($"{def.Name}: {faction.Reputation} ({faction.Standing})");
            }
            
            return string.Join("\n", lines);
        }
        
        public List<(string Name, int Rep, FactionStanding Standing, string Color)> GetAllStandingsForUI()
        {
            var result = new List<(string, int, FactionStanding, string)>();
            
            foreach (var faction in _standings.Values.OrderByDescending(s => s.Reputation))
            {
                var def = GetDefinition(faction.Faction);
                if (def == null || !def.CanBeAllied) continue;
                result.Add((def.Name, faction.Reputation, faction.Standing, def.ColorHex));
            }
            
            return result;
        }
    }
}
