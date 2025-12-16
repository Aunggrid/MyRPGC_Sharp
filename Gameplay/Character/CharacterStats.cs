// Gameplay/Character/CharacterStats.cs
// Unified character statistics combining Body, Mutations, Traits, and Status Effects

using System;
using System.Collections.Generic;
using MyRPG.Data;
using MyRPG.Gameplay.Systems;

namespace MyRPG.Gameplay.Character
{
    public class CharacterStats
    {
        // ============================================
        // COMPONENT SYSTEMS
        // ============================================
        
        public Body Body { get; private set; }
        public List<MutationInstance> Mutations { get; private set; }
        public List<TraitType> Traits { get; private set; }
        public List<StatusEffect> StatusEffects { get; private set; }
        public SciencePath SciencePath { get; set; }
        
        // System references for calculations
        private MutationSystem _mutationSystem;
        private TraitSystem _traitSystem;
        private StatusEffectSystem _statusSystem;
        
        // ============================================
        // BASE STATS (before modifiers)
        // ============================================
        
        public float BaseHealth { get; set; } = 100f;
        public float BaseSpeed { get; set; } = 200f;
        public float BaseDamage { get; set; } = 10f;
        public float BaseAccuracy { get; set; } = 0.75f;      // 75% base hit chance
        public float BaseSightRange { get; set; } = 10f;      // Tiles
        public int BaseActionPoints { get; set; } = 3;
        
        // ============================================
        // CALCULATED STATS (with all modifiers)
        // ============================================
        
        public float MaxHealth => CalculateMaxHealth();
        public float CurrentHealth { get; set; }
        public float Speed => CalculateSpeed();
        public float Damage => CalculateDamage();
        public float Accuracy => CalculateAccuracy();
        public float SightRange => CalculateSightRange();
        public int ActionPoints => CalculateActionPoints();
        public float DamageResistance => CalculateDamageResistance();
        public float RegenRate => CalculateRegenRate();
        
        // Survival stats
        public float HungerRate => CalculateHungerRate();
        public float XPMultiplier => CalculateXPMultiplier();
        public float ResearchSpeed => CalculateResearchSpeed();
        public float TradeMultiplier => CalculateTradeMultiplier();
        
        // ============================================
        // EXPERIENCE & LEVELING
        // ============================================
        
        public int Level { get; private set; } = 1;
        public float CurrentXP { get; private set; } = 0f;
        public float XPToNextLevel => Level * 100f;  // Simple scaling: 100, 200, 300...
        public int MutationPoints { get; private set; } = 0;
        public int FreeMutationPicks { get; private set; } = 0;  // Every 4 levels
        
        // ============================================
        // CONSTRUCTOR
        // ============================================
        
        public CharacterStats(MutationSystem mutationSystem, TraitSystem traitSystem, StatusEffectSystem statusSystem)
        {
            _mutationSystem = mutationSystem;
            _traitSystem = traitSystem;
            _statusSystem = statusSystem;
            
            Body = new Body();
            Mutations = new List<MutationInstance>();
            Traits = new List<TraitType>();
            StatusEffects = new List<StatusEffect>();
            
            CurrentHealth = MaxHealth;
        }
        
        // ============================================
        // CHARACTER CREATION
        // ============================================
        
        public void ApplyCharacterBuild(CharacterBuild build, SciencePath path)
        {
            SciencePath = path;
            
            // Apply backstory
            Traits.Add(build.Backstory);
            
            // Apply additional traits
            foreach (var trait in build.Traits)
            {
                Traits.Add(trait);
            }
            
            // Set starting mutation points
            MutationPoints = build.MutationPoints;
            
            // Recalculate health with new modifiers
            CurrentHealth = MaxHealth;
            
            System.Diagnostics.Debug.WriteLine($">>> CHARACTER CREATED: {build} | Path: {path} <<<");
        }
        
        // ============================================
        // LEVELING
        // ============================================
        
        public void AddXP(float amount)
        {
            amount *= XPMultiplier;
            CurrentXP += amount;
            
            System.Diagnostics.Debug.WriteLine($">>> GAINED {amount:F0} XP ({CurrentXP:F0}/{XPToNextLevel:F0}) <<<");
            
            while (CurrentXP >= XPToNextLevel)
            {
                LevelUp();
            }
        }
        
        private void LevelUp()
        {
            CurrentXP -= XPToNextLevel;
            Level++;
            MutationPoints++;
            
            // Every 4 levels, get a free mutation pick
            if (Level % 4 == 0)
            {
                FreeMutationPicks++;
                System.Diagnostics.Debug.WriteLine($">>> FREE MUTATION PICK AVAILABLE! <<<");
            }
            
            System.Diagnostics.Debug.WriteLine($">>> LEVEL UP! Now level {Level}. Mutation points: {MutationPoints} <<<");
        }
        
        // ============================================
        // MUTATION APPLICATION
        // ============================================
        
        /// <summary>
        /// Get random mutation choices for spending a mutation point.
        /// </summary>
        public List<MutationDefinition> GetMutationChoices(int choiceCount = 3)
        {
            bool isFreeChoice = FreeMutationPicks > 0;
            return _mutationSystem.GetRandomChoices(Mutations, choiceCount, isFreeChoice);
        }
        
        /// <summary>
        /// Spend a mutation point to acquire or level up a mutation.
        /// </summary>
        public bool SpendMutationPoint(MutationType type, bool useFreeChoice = false)
        {
            if (useFreeChoice)
            {
                if (FreeMutationPicks <= 0) return false;
                FreeMutationPicks--;
            }
            else
            {
                if (MutationPoints <= 0) return false;
                MutationPoints--;
            }
            
            var result = _mutationSystem.ApplyMutation(Mutations, Body, type);
            
            // Recalculate max health (might have changed)
            if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
            
            return result != null;
        }
        
        // ============================================
        // STAT CALCULATIONS
        // ============================================
        
        private float CalculateMaxHealth()
        {
            float health = BaseHealth;
            
            // Body capacity affects health
            health *= Body.IsAlive ? 1.0f : 0f;
            
            // Trait modifiers
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            health *= traitBonuses.HealthModifier;
            
            // Mutation bonuses
            var mutationBonuses = _mutationSystem.CalculateBonuses(Mutations);
            health += mutationBonuses.HealthBonus;
            
            return Math.Max(1f, health);
        }
        
        private float CalculateSpeed()
        {
            float speed = BaseSpeed;
            
            // Body movement capacity
            speed *= Body.MovementCapacity;
            
            // Trait modifiers
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            speed *= traitBonuses.SpeedModifier;
            
            // Mutation bonuses (additive %)
            var mutationBonuses = _mutationSystem.CalculateBonuses(Mutations);
            speed *= (1f + mutationBonuses.SpeedBonus);
            
            // Status effect modifiers
            speed *= _statusSystem.GetSpeedModifier(StatusEffects);
            
            return Math.Max(10f, speed); // Minimum speed
        }
        
        private float CalculateDamage()
        {
            float damage = BaseDamage;
            
            // Manipulation capacity affects melee damage
            float manipMod = 0.5f + (Body.ManipulationCapacity * 0.5f); // 50-100% based on arms
            damage *= manipMod;
            
            // Trait modifiers
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            damage *= traitBonuses.DamageModifier;
            
            // Mutation bonuses
            var mutationBonuses = _mutationSystem.CalculateBonuses(Mutations);
            damage += mutationBonuses.DamageBonus;
            
            // Status effects
            damage *= _statusSystem.GetDamageModifier(StatusEffects);
            
            return Math.Max(1f, damage);
        }
        
        private float CalculateAccuracy()
        {
            float accuracy = BaseAccuracy;
            
            // Vision affects accuracy
            float visionMod = Math.Min(1.2f, 0.5f + (Body.VisionCapacity * 0.5f));
            accuracy *= visionMod;
            
            // Consciousness affects everything
            accuracy *= Body.Consciousness;
            
            // Trait modifiers
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            accuracy *= traitBonuses.AccuracyModifier;
            
            return Math.Clamp(accuracy, 0.1f, 0.99f); // 10-99% hit chance
        }
        
        private float CalculateSightRange()
        {
            float range = BaseSightRange;
            
            // Vision capacity
            range *= Body.VisionCapacity;
            
            // Mutation bonuses
            var mutationBonuses = _mutationSystem.CalculateBonuses(Mutations);
            range += mutationBonuses.SightRangeBonus;
            
            return Math.Max(2f, range); // Minimum sight
        }
        
        private int CalculateActionPoints()
        {
            int ap = BaseActionPoints;
            
            // Consciousness reduction
            if (Body.Consciousness < 0.5f) ap--;
            
            // Stunned = 0 AP
            if (_statusSystem.HasEffect(StatusEffects, StatusEffectType.Stunned))
            {
                return 0;
            }
            
            return Math.Max(0, ap);
        }
        
        private float CalculateDamageResistance()
        {
            float resistance = 0f;
            
            // Mutation bonuses
            var mutationBonuses = _mutationSystem.CalculateBonuses(Mutations);
            resistance += mutationBonuses.ResistanceBonus;
            
            return Math.Clamp(resistance, 0f, 0.9f); // Max 90% resistance
        }
        
        private float CalculateRegenRate()
        {
            float regen = 0f;
            
            // Mutation bonuses
            var mutationBonuses = _mutationSystem.CalculateBonuses(Mutations);
            regen += mutationBonuses.RegenBonus;
            
            // Trait modifiers
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            regen *= traitBonuses.HealingModifier;
            
            return Math.Max(0f, regen);
        }
        
        private float CalculateHungerRate()
        {
            float rate = 1.0f;
            
            // Trait modifiers
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            rate *= traitBonuses.HungerRateModifier;
            
            // Extra body parts = more hunger
            int extraParts = Body.Parts.Count - 20; // Base human has ~20 parts
            if (extraParts > 0)
            {
                rate *= 1f + (extraParts * 0.02f); // 2% more hunger per extra part
            }
            
            return rate;
        }
        
        private float CalculateXPMultiplier()
        {
            float mult = 1.0f;
            
            // Trait modifiers
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            mult *= traitBonuses.XPModifier;
            
            return mult;
        }
        
        private float CalculateResearchSpeed()
        {
            float speed = 1.0f;
            
            // Consciousness affects research
            speed *= Body.Consciousness;
            
            // Trait modifiers
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            speed *= traitBonuses.ResearchModifier;
            
            // Complex Brain mutation
            int complexBrainLevel = GetMutationLevel(MutationType.ComplexBrain);
            if (complexBrainLevel > 0)
            {
                speed *= 1f + (complexBrainLevel * 0.1f); // 10% per level
            }
            
            return speed;
        }
        
        private float CalculateTradeMultiplier()
        {
            float mult = 1.0f;
            
            // Trait modifiers (lower = better prices)
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            mult *= traitBonuses.TradeModifier;
            
            return mult;
        }
        
        // ============================================
        // SPECIAL ABILITIES (from mutations)
        // ============================================
        
        public bool HasNightVision => _mutationSystem.CalculateBonuses(Mutations).HasNightVision;
        public bool HasWaterBreathing => _mutationSystem.CalculateBonuses(Mutations).HasWaterBreathing;
        public bool HasWallClimb => _mutationSystem.CalculateBonuses(Mutations).HasWallClimb;
        public bool HasFlight => _mutationSystem.CalculateBonuses(Mutations).HasFlight;
        public bool HasStealth => _mutationSystem.CalculateBonuses(Mutations).HasStealth;
        
        // Trait abilities
        public bool CanSpeak => _traitSystem.CalculateBonuses(Traits).CanSpeak;
        public bool CanDisguise => _traitSystem.CalculateBonuses(Traits).CanDisguise;
        public bool IsNightPerson => _traitSystem.CalculateBonuses(Traits).IsNightPerson;
        public bool CanEatCorpses => _traitSystem.CalculateBonuses(Traits).CanEatCorpses ||
                                     Mutations.Exists(m => m.Type == MutationType.CorpseEater);
        
        // ============================================
        // HELPERS
        // ============================================
        
        public int GetMutationLevel(MutationType type)
        {
            var mutation = Mutations.Find(m => m.Type == type);
            return mutation?.Level ?? 0;
        }
        
        public bool HasMutation(MutationType type)
        {
            return Mutations.Exists(m => m.Type == type);
        }
        
        public bool HasTrait(TraitType type)
        {
            return Traits.Contains(type);
        }
        
        // ============================================
        // DAMAGE & HEALING
        // ============================================
        
        public void TakeDamage(float amount, DamageType type = DamageType.Physical)
        {
            // Apply resistance
            float resistedAmount = amount * (1f - DamageResistance);
            
            // Distribute some damage to body parts
            if (resistedAmount > 10f)
            {
                Body.TakeDamage(resistedAmount * 0.3f, type); // 30% goes to body parts
            }
            
            CurrentHealth -= resistedAmount;
            
            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0;
                System.Diagnostics.Debug.WriteLine(">>> CHARACTER DIED! <<<");
            }
        }
        
        public void Heal(float amount)
        {
            // Trait modifiers
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            amount *= traitBonuses.HealingModifier;
            
            CurrentHealth = Math.Min(CurrentHealth + amount, MaxHealth);
        }
        
        // ============================================
        // DEBUG
        // ============================================
        
        public string GetStatusReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== CHARACTER STATS ===");
            report.AppendLine($"Level: {Level} ({CurrentXP:F0}/{XPToNextLevel:F0} XP)");
            report.AppendLine($"Health: {CurrentHealth:F0}/{MaxHealth:F0}");
            report.AppendLine($"Speed: {Speed:F0}");
            report.AppendLine($"Damage: {Damage:F1}");
            report.AppendLine($"Accuracy: {Accuracy:P0}");
            report.AppendLine($"Sight Range: {SightRange:F0}");
            report.AppendLine($"Action Points: {ActionPoints}");
            report.AppendLine($"Damage Resist: {DamageResistance:P0}");
            report.AppendLine($"Regen: {RegenRate:F1}/tick");
            report.AppendLine();
            report.AppendLine($"Science Path: {SciencePath}");
            report.AppendLine($"Mutation Points: {MutationPoints} | Free Picks: {FreeMutationPicks}");
            report.AppendLine();
            report.AppendLine("--- Traits ---");
            foreach (var trait in Traits)
            {
                report.AppendLine($"  {trait}");
            }
            report.AppendLine();
            report.AppendLine("--- Mutations ---");
            foreach (var mutation in Mutations)
            {
                report.AppendLine($"  {mutation}");
            }
            report.AppendLine();
            report.AppendLine("--- Status Effects ---");
            foreach (var effect in StatusEffects)
            {
                report.AppendLine($"  {effect}");
            }
            report.AppendLine();
            report.AppendLine("--- Abilities ---");
            if (HasNightVision) report.AppendLine("  Night Vision");
            if (HasWaterBreathing) report.AppendLine("  Water Breathing");
            if (HasWallClimb) report.AppendLine("  Wall Climb");
            if (HasFlight) report.AppendLine("  Flight");
            if (HasStealth) report.AppendLine("  Stealth");
            if (!CanSpeak) report.AppendLine("  [Cannot Speak]");
            if (!CanDisguise) report.AppendLine("  [Cannot Disguise]");
            if (CanEatCorpses) report.AppendLine("  Can Eat Corpses");
            
            return report.ToString();
        }
    }
}
