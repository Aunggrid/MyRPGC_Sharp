// Gameplay/Character/CharacterStats.cs
// Unified character statistics combining Body, Mutations, Traits, Attributes, and Status Effects

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;
using MyRPG.Gameplay.Systems;
using MyRPG.Gameplay.Items;

namespace MyRPG.Gameplay.Character
{
    public class CharacterStats
    {
        // ============================================
        // COMPONENT SYSTEMS
        // ============================================
        
        public Body Body { get; private set; }
        public Attributes Attributes { get; private set; }
        public Inventory Inventory { get; private set; }
        public SurvivalNeeds Survival { get; private set; }
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
        
        public float BaseHealth { get; set; } = 90f;       // Reduced from 100, but survivable
        public float BaseSpeed { get; set; } = 200f;
        public float BaseDamage { get; set; } = 8f;        // Unarmed is weak - get a weapon!
        public float BaseAccuracy { get; set; } = 0.65f;   // 65% - misses happen, PER matters
        public float BaseSightRange { get; set; } = 9f;    // Slightly reduced - enemies can ambush
        public int BaseActionPoints { get; set; } = 2;     // Actions per turn
        public int BaseMovementPoints { get; set; } = 3;   // Restored - tactics need mobility
        public int BaseEsperPoints { get; set; } = 0;      // Esper points for psychic abilities
        public int BaseMaxReservedAP { get; set; } = 1;    // Max AP that can be saved
        
        // ============================================
        // RESERVED AP SYSTEM
        // ============================================
        
        /// <summary>
        /// AP saved from previous turn(s)
        /// </summary>
        public int ReservedAP { get; set; } = 0;
        
        /// <summary>
        /// Maximum AP that can be reserved (base 2, can increase with mutations/traits)
        /// </summary>
        public int MaxReservedAP => CalculateMaxReservedAP();
        
        /// <summary>
        /// Bonus reserved AP from mutations/traits
        /// </summary>
        public int ReservedAPBonus { get; set; } = 0;
        
        private int CalculateMaxReservedAP()
        {
            int max = BaseMaxReservedAP;
            max += ReservedAPBonus;
            
            // Trait bonuses
            var traitBonuses = _traitSystem?.CalculateBonuses(Traits);
            if (traitBonuses != null)
            {
                max += traitBonuses.ReservedAPBonus;
            }
            
            // Mutation bonuses
            var mutationBonuses = _mutationSystem?.CalculateBonuses(Mutations);
            if (mutationBonuses != null)
            {
                max += mutationBonuses.ReservedAPBonus;
            }
            
            return Math.Max(0, max);
        }
        
        /// <summary>
        /// Reserve unused AP at end of turn (called by combat system)
        /// </summary>
        public void ReserveAP(int unusedAP)
        {
            ReservedAP = Math.Min(ReservedAP + unusedAP, MaxReservedAP);
        }
        
        /// <summary>
        /// Get total AP available this turn (base + reserved)
        /// </summary>
        public int GetTotalAPForTurn()
        {
            return ActionPoints + ReservedAP;
        }
        
        /// <summary>
        /// Consume reserved AP (returns how many were consumed)
        /// </summary>
        public int ConsumeReservedAP(int amount)
        {
            int consumed = Math.Min(amount, ReservedAP);
            ReservedAP -= consumed;
            return consumed;
        }
        
        /// <summary>
        /// Clear reserved AP (e.g., when combat ends)
        /// </summary>
        public void ClearReservedAP()
        {
            ReservedAP = 0;
        }
        
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
        public int MovementPoints => CalculateMovementPoints();
        public int EsperPoints => CalculateEsperPoints();       // EP for esper abilities
        public float EsperPower => CalculateEsperPower();       // Effectiveness of esper abilities
        public float DamageResistance => CalculateDamageResistance();
        public float RegenRate => CalculateRegenRate();
        
        // Current EP (depletes when using abilities)
        public int CurrentEsperPoints { get; set; } = 0;
        
        // Survival stats
        public float HungerRate => CalculateHungerRate();
        public float XPMultiplier => CalculateXPMultiplier();
        public float ResearchSpeed => CalculateResearchSpeed();
        public float TradeMultiplier => CalculateTradeMultiplier();
        
        // INT unlocks
        public int ResearchSlots => CalculateResearchSlots();   // How many research projects at once
        public int RecipeUnlocks => CalculateRecipeUnlocks();   // Bonus recipes unlocked
        
        // ============================================
        // EXPERIENCE & LEVELING
        // ============================================
        
        public int Level { get; private set; } = 1;
        public float CurrentXP { get; private set; } = 0f;
        public float XPToNextLevel => Level * 120f;  // 120 per level - rewards combat but not grindy
        public int MutationPoints { get; private set; } = 0;
        public int FreeMutationPicks { get; private set; } = 0;  // Every 4 levels
        
        // NEW: Pending attribute points (must spend before mutation)
        public int PendingAttributePoints { get; private set; } = 0;
        
        // Currency
        public int Gold { get; set; } = 100;  // Starting gold
        
        // ============================================
        // CONSTRUCTOR
        // ============================================
        
        public CharacterStats(MutationSystem mutationSystem, TraitSystem traitSystem, StatusEffectSystem statusSystem)
        {
            _mutationSystem = mutationSystem;
            _traitSystem = traitSystem;
            _statusSystem = statusSystem;
            
            Body = new Body();
            Attributes = new Attributes();
            Inventory = new Inventory(18, 40f); // 18 slots, 40kg - plan your loadout
            Survival = new SurvivalNeeds();
            Mutations = new List<MutationInstance>();
            Traits = new List<TraitType>();
            StatusEffects = new List<StatusEffect>();
            
            CurrentHealth = MaxHealth;
            CurrentEsperPoints = EsperPoints;
        }
        
        /// <summary>
        /// Restore state from save data (for loading games)
        /// </summary>
        public void RestoreState(int level, float currentXP, int mutationPoints, int freeMutationPicks, int pendingAttributePoints, int gold = 100)
        {
            Level = level;
            CurrentXP = currentXP;
            MutationPoints = mutationPoints;
            FreeMutationPicks = freeMutationPicks;
            PendingAttributePoints = pendingAttributePoints;
            Gold = gold;
        }
        
        // ============================================
        // CHARACTER CREATION
        // ============================================
        
        public void ApplyCharacterBuild(CharacterBuild build, SciencePath path)
        {
            SciencePath = path;
            
            // Copy attributes from build (already randomized during character creation)
            foreach (AttributeType attr in Enum.GetValues(typeof(AttributeType)))
            {
                Attributes.Set(attr, build.Attributes.Get(attr));
            }
            
            // Update inventory capacity based on STR
            Inventory.MaxWeight = 30f + Attributes.CarryWeightBonus;
            
            // Apply backstory
            Traits.Add(build.Backstory);
            
            // Apply additional traits
            foreach (var trait in build.Traits)
            {
                Traits.Add(trait);
            }
            
            // Set starting mutation points
            MutationPoints = build.MutationPoints;
            
            // Give starting items based on backstory
            GiveStartingItems(build.Backstory);
            
            // Apply survival rate modifiers from traits
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            Survival.HungerRateModifier = traitBonuses.HungerRateModifier;
            
            // Recalculate health with new modifiers (body is at 100%)
            SyncHPWithBody();
            
            System.Diagnostics.Debug.WriteLine($">>> CHARACTER CREATED: {build} | Path: {path} <<<");
            System.Diagnostics.Debug.WriteLine($">>> ATTRIBUTES: {Attributes.GetDisplayString()} <<<");
            System.Diagnostics.Debug.WriteLine($">>> HP: {CurrentHealth:F0}/{MaxHealth:F0} <<<");
        }
        
        /// <summary>
        /// Give starting items based on backstory
        /// </summary>
        private void GiveStartingItems(TraitType backstory)
        {
            // Everyone starts with basic supplies
            Inventory.TryAddItem("food_jerky", 5);
            Inventory.TryAddItem("water_dirty", 3);
            Inventory.TryAddItem("bandage", 2);
            
            // Backstory-specific items
            switch (backstory)
            {
                case TraitType.LabEscapee:
                    Inventory.TryAddItem("medkit", 1);
                    Inventory.TryAddItem("scrap_electronics", 5);
                    break;
                    
                case TraitType.WastelandBorn:
                    Inventory.TryAddItem("knife_rusty", 1);
                    Inventory.TryAddItem("food_jerky", 5); // Extra food
                    Inventory.TryAddItem("water_purified", 2);
                    break;
                    
                case TraitType.FailedExperiment:
                    Inventory.TryAddItem("stimpack", 1);
                    Inventory.TryAddItem("void_essence", 2);
                    break;
                    
                case TraitType.TribalMutant:
                    Inventory.TryAddItem("spear_makeshift", 1);
                    Inventory.TryAddItem("leather", 5);
                    Inventory.TryAddItem("mutant_meat", 3);
                    break;
                    
                case TraitType.UrbanSurvivor:
                    Inventory.TryAddItem("knife_combat", 1);
                    Inventory.TryAddItem("armor_leather", 1);
                    Inventory.TryAddItem("scrap_metal", 10);
                    break;
                    
                default:
                    // Generic start
                    Inventory.TryAddItem("pipe_club", 1);
                    break;
            }
            
            System.Diagnostics.Debug.WriteLine($">>> Starting items given for {backstory} <<<");
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
            
            // Give attribute point (must spend before mutation selection)
            PendingAttributePoints++;
            
            // Every 4 levels, get a free mutation pick
            if (Level % 4 == 0)
            {
                FreeMutationPicks++;
                System.Diagnostics.Debug.WriteLine($">>> FREE MUTATION PICK AVAILABLE! <<<");
            }
            
            System.Diagnostics.Debug.WriteLine($">>> LEVEL UP! Now level {Level}. Choose an attribute to increase! <<<");
        }
        
        // ============================================
        // ATTRIBUTE SPENDING
        // ============================================
        
        /// <summary>
        /// Check if player needs to spend attribute points before mutation
        /// </summary>
        public bool HasPendingAttributePoints => PendingAttributePoints > 0;
        
        /// <summary>
        /// Spend an attribute point to increase an attribute.
        /// Returns true if successful, grants mutation point after.
        /// </summary>
        public bool SpendAttributePoint(AttributeType attribute)
        {
            if (PendingAttributePoints <= 0) return false;
            
            // Check if already at max
            if (Attributes.Get(attribute) >= Attributes.MAX_VALUE)
            {
                System.Diagnostics.Debug.WriteLine($">>> {attribute} is already at maximum! <<<");
                return false;
            }
            
            // Increase attribute
            Attributes.Increase(attribute);
            PendingAttributePoints--;
            
            // NOW grant mutation point
            MutationPoints++;
            
            System.Diagnostics.Debug.WriteLine($">>> {attribute} increased to {Attributes.Get(attribute)}! Now select a mutation. <<<");
            
            // Recalculate health (END affects max health)
            if (attribute == AttributeType.END)
            {
                float healthPercent = CurrentHealth / MaxHealth;
                CurrentHealth = MaxHealth * healthPercent; // Maintain percentage
            }
            
            return true;
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
        
        /// <summary>
        /// Add mutation points (from quest rewards, etc.)
        /// </summary>
        public void AddMutationPoints(int amount)
        {
            MutationPoints += amount;
            System.Diagnostics.Debug.WriteLine($">>> Added {amount} mutation points! Total: {MutationPoints} <<<");
        }
        
        // ============================================
        // SAVE/LOAD HELPER METHODS
        // ============================================
        
        public void SetLevel(int level)
        {
            Level = Math.Max(1, level);
        }
        
        public void SetXP(float xp)
        {
            CurrentXP = Math.Max(0, xp);
        }
        
        public void SetMutationPoints(int points)
        {
            MutationPoints = Math.Max(0, points);
        }
        
        public void SetFreeMutationPicks(int picks)
        {
            FreeMutationPicks = Math.Max(0, picks);
        }
        
        public void SetPendingAttributePoints(int points)
        {
            PendingAttributePoints = Math.Max(0, points);
        }
        
        public void ClearMutations()
        {
            Mutations.Clear();
            // Remove mutation body parts
            var mutationParts = Body.Parts.Values.Where(p => p.IsMutationPart).ToList();
            foreach (var part in mutationParts)
            {
                Body.Parts.Remove(part.Id);
            }
        }
        
        public void AddMutation(MutationType type, int level)
        {
            var existing = Mutations.FirstOrDefault(m => m.Type == type);
            if (existing != null)
            {
                existing.Level = level;
            }
            else
            {
                Mutations.Add(new MutationInstance(type, level));
            }
        }
        
        public void ClearTraits()
        {
            Traits.Clear();
        }
        
        public void AddTrait(TraitType trait)
        {
            if (!Traits.Contains(trait))
            {
                Traits.Add(trait);
            }
        }
        
        // ============================================
        // STAT CALCULATIONS
        // ============================================
        
        private float CalculateMaxHealth()
        {
            // Base HP is 100
            float health = 100f;
            
            // Attribute bonus (END) - typically +2 HP per END point
            health += Attributes.HealthBonus;
            
            // Trait modifiers (multiplicative)
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            health *= traitBonuses.HealthModifier;
            
            // Mutation bonuses (additive)
            var mutationBonuses = _mutationSystem.CalculateBonuses(Mutations);
            health += mutationBonuses.HealthBonus;
            
            return Math.Max(1f, health);
        }
        
        /// <summary>
        /// Get current HP based on body health percentage × max health
        /// </summary>
        public float GetBodyHP()
        {
            return MaxHealth * Body.BodyHealthPercent;
        }
        
        private float CalculateSpeed()
        {
            float speed = BaseSpeed;
            
            // Attribute bonus (AGI)
            speed *= (1f + Attributes.SpeedBonus);
            
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
            
            // Survival need modifiers
            speed *= Survival.GetSpeedModifier();
            
            return Math.Max(10f, speed); // Minimum speed
        }
        
        private float CalculateDamage()
        {
            float damage = BaseDamage;
            
            // Add weapon damage if equipped
            var weapon = Inventory?.GetWeapon();
            if (weapon != null)
            {
                damage = weapon.GetEffectiveDamage();
            }
            
            // Attribute bonus (STR for melee)
            damage *= (1f + Attributes.MeleeDamageBonus);
            
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
            
            // Survival need modifiers
            damage *= Survival.GetDamageModifier();
            
            return Math.Max(1f, damage);
        }
        
        /// <summary>
        /// Get weapon attack range (1 for melee/unarmed)
        /// </summary>
        public int GetAttackRange()
        {
            // Check body-equipped weapons first (new system)
            var bodyWeapons = Body?.GetEquippedWeapons();
            if (bodyWeapons != null && bodyWeapons.Count > 0)
            {
                // Return the longest range weapon
                int maxRange = 1;
                foreach (var w in bodyWeapons)
                {
                    if (w.Definition != null && w.Definition.Range > maxRange)
                    {
                        maxRange = w.Definition.Range;
                    }
                }
                return maxRange;
            }
            
            // Fallback to inventory slot system (legacy)
            var invWeapon = Inventory?.GetWeapon();
            if (invWeapon?.Definition != null)
            {
                return invWeapon.Definition.Range;
            }
            return 1; // Unarmed melee
        }
        
        /// <summary>
        /// Get the primary weapon (first equipped or best range)
        /// </summary>
        public Item GetPrimaryWeapon()
        {
            var bodyWeapons = Body?.GetEquippedWeapons();
            if (bodyWeapons != null && bodyWeapons.Count > 0)
            {
                return bodyWeapons[0];
            }
            return Inventory?.GetWeapon();
        }
        
        /// <summary>
        /// Check if can attack (has ammo if needed)
        /// </summary>
        public bool CanAttack()
        {
            var weapon = GetPrimaryWeapon();
            if (weapon == null) return true; // Unarmed always works
            
            // Check ammo
            if (weapon.Definition?.RequiresAmmo != null)
            {
                return Inventory.HasAmmoFor(weapon);
            }
            
            return !weapon.IsBroken;
        }
        
        /// <summary>
        /// Consume ammo after attack (if needed)
        /// </summary>
        public void ConsumeAmmoForAttack()
        {
            var weapon = GetPrimaryWeapon();
            if (weapon?.Definition?.RequiresAmmo != null)
            {
                Inventory.ConsumeAmmo(weapon);
            }
        }
        
        private float CalculateAccuracy()
        {
            float accuracy = BaseAccuracy;
            
            // Attribute bonus (PER)
            accuracy += Attributes.AccuracyBonus;
            
            // Vision affects accuracy
            float visionMod = Math.Min(1.2f, 0.5f + (Body.VisionCapacity * 0.5f));
            accuracy *= visionMod;
            
            // Consciousness affects everything
            accuracy *= Body.Consciousness;
            
            // Trait modifiers
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            accuracy *= traitBonuses.AccuracyModifier;
            
            // Survival need modifiers
            accuracy *= Survival.GetAccuracyModifier();
            
            return Math.Clamp(accuracy, 0.1f, 0.99f); // 10-99% hit chance
        }
        
        /// <summary>
        /// Calculate hit chance against a target at a specific distance
        /// Ranged weapons have penalties at close range (especially 2H weapons)
        /// </summary>
        public float GetHitChance(int distance)
        {
            float baseAccuracy = Accuracy;
            var weapon = GetPrimaryWeapon();
            
            if (weapon == null)
            {
                // Unarmed - no distance penalty
                return baseAccuracy;
            }
            
            int weaponRange = weapon.Definition?.Range ?? 1;
            bool isTwoHanded = weapon.Definition?.IsTwoHanded ?? false;
            bool isRanged = weaponRange > 1;
            
            if (!isRanged)
            {
                // Melee weapon - no distance penalty
                return baseAccuracy;
            }
            
            // Ranged weapon accuracy modifiers based on distance
            float distanceModifier = 1f;
            
            if (distance <= 1)
            {
                // Point blank - big penalty for ranged weapons
                // 2H weapons (bow, rifle) are harder to aim at close range
                distanceModifier = isTwoHanded ? 0.4f : 0.6f;  // 40% or 60% of normal accuracy
            }
            else if (distance == 2)
            {
                // Very close - moderate penalty
                distanceModifier = isTwoHanded ? 0.65f : 0.8f;
            }
            else if (distance == 3)
            {
                // Close - slight penalty for 2H
                distanceModifier = isTwoHanded ? 0.85f : 0.95f;
            }
            else if (distance >= weaponRange - 1)
            {
                // Near max range - slight penalty
                distanceModifier = 0.9f;
            }
            else if (distance > weaponRange)
            {
                // Beyond range - severe penalty
                float overRange = distance - weaponRange;
                distanceModifier = Math.Max(0.1f, 1f - (overRange * 0.3f));
            }
            // else: optimal range (4 to range-2) = 100%
            
            return Math.Clamp(baseAccuracy * distanceModifier, 0.05f, 0.99f);
        }
        
        /// <summary>
        /// Get melee damage when using a ranged weapon in close combat
        /// </summary>
        public float GetMeleeDamageWithRangedWeapon()
        {
            var weapon = GetPrimaryWeapon();
            
            if (weapon == null)
            {
                // Unarmed
                return Body.GetTotalWeaponDamage();
            }
            
            bool isRanged = (weapon.Definition?.Range ?? 1) > 1;
            
            if (!isRanged)
            {
                // Already melee weapon - use normal damage
                return Damage;
            }
            
            // Ranged weapon used as melee - reduced damage
            // Pistol whip, bow bash, etc.
            float baseMeleeDamage = weapon.Definition?.Damage ?? 5f;
            bool isTwoHanded = weapon.Definition?.IsTwoHanded ?? false;
            
            // 2H weapons do slightly more melee damage (heavier)
            float meleeMod = isTwoHanded ? 0.4f : 0.25f;
            
            return baseMeleeDamage * meleeMod + (Attributes.STR * 0.5f);
        }
        
        /// <summary>
        /// Get melee accuracy (usually higher than ranged at close distance)
        /// </summary>
        public float GetMeleeAccuracy()
        {
            float accuracy = Accuracy;
            
            // Melee is generally more accurate at close range
            // STR bonus for melee
            accuracy += Attributes.STR * 0.02f;
            
            return Math.Clamp(accuracy, 0.1f, 0.95f);
        }
        
        private float CalculateSightRange()
        {
            float range = BaseSightRange;
            
            // Attribute bonus (PER)
            range += Attributes.SightRangeBonus;
            
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
            
            // Equipment bonuses (gloves, tactical gear, etc.)
            int equipBonus = Inventory?.GetEquipmentAPBonus() ?? 0;
            ap += equipBonus;
            
            // Mutation bonuses
            var mutationBonuses = _mutationSystem.CalculateBonuses(Mutations);
            ap += mutationBonuses.ActionPointBonus;
            
            // Trait bonuses
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            ap += traitBonuses.ActionPointBonus;
            
            // Consciousness reduction (severe injury)
            if (Body.Consciousness < 0.5f) ap--;
            
            // Stunned = 0 AP
            if (_statusSystem.HasEffect(StatusEffects, StatusEffectType.Stunned))
            {
                return 0;
            }
            
            return Math.Max(1, ap);  // Minimum 1 AP
        }
        
        private int CalculateMovementPoints()
        {
            int mp = BaseMovementPoints;
            
            // Equipment bonuses (boots, leg armor, etc.)
            int equipBonus = Inventory?.GetEquipmentMPBonus() ?? 0;
            mp += equipBonus;
            
            // Mutation bonuses
            var mutationBonuses = _mutationSystem.CalculateBonuses(Mutations);
            mp += mutationBonuses.MovementPointBonus;
            
            // Trait bonuses
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            mp += traitBonuses.MovementPointBonus;
            
            // AGI bonus: Small bonus at very high agility
            // Every 3 AGI above 12 = +1 MP
            int agiBonus = Math.Max(0, (Attributes.AGI - 12) / 3);
            mp += agiBonus;
            
            // Mobility reduction from body damage (legs, etc.)
            mp = (int)(mp * Body.MovementCapacity);
            
            // Weight penalty: Encumbered = fewer MP
            float encumbrance = Inventory?.GetEncumbrancePercent() ?? 0f;
            if (encumbrance > 0.75f) mp -= 2;
            else if (encumbrance > 0.5f) mp--;
            
            // Status effects
            if (_statusSystem.HasEffect(StatusEffects, StatusEffectType.Slowed))
                mp = (int)(mp * 0.5f);
            if (_statusSystem.HasEffect(StatusEffects, StatusEffectType.Stunned))
                return 0;
            if (_statusSystem.HasEffect(StatusEffects, StatusEffectType.Frozen))
                return 0;
            
            return Math.Max(1, mp);  // Minimum 1 MP
        }
        
        private int CalculateEsperPoints()
        {
            int ep = BaseEsperPoints;
            
            // WILL is primary source of EP
            // Base: WIL / 2 EP
            ep += Attributes.WIL / 2;
            
            // High WIL bonus: WIL 12+ gives extra EP
            if (Attributes.WIL >= 12) ep += 2;
            if (Attributes.WIL >= 16) ep += 3;
            
            // Mutation bonuses (psychic mutations)
            var mutationBonuses = _mutationSystem.CalculateBonuses(Mutations);
            ep += mutationBonuses.EsperPointBonus;
            
            // Trait bonuses
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            ep += traitBonuses.EsperPointBonus;
            
            // Equipment (psionic amplifiers, etc.)
            int equipBonus = Inventory?.GetEquipmentEPBonus() ?? 0;
            ep += equipBonus;
            
            return Math.Max(0, ep);
        }
        
        private float CalculateEsperPower()
        {
            float power = 1.0f;  // Base 100% effectiveness
            
            // WILL is primary: +5% per point above 10
            power += (Attributes.WIL - 10) * 0.05f;
            
            // INT provides small bonus: +2% per point above 10
            power += (Attributes.INT - 10) * 0.02f;
            
            // Mutation bonuses
            var mutationBonuses = _mutationSystem.CalculateBonuses(Mutations);
            power += mutationBonuses.EsperPowerBonus;
            
            // Equipment bonuses
            float equipBonus = Inventory?.GetEquipmentEsperPowerBonus() ?? 0f;
            power += equipBonus;
            
            return Math.Max(0.5f, power);  // Minimum 50% effectiveness
        }
        
        private int CalculateResearchSlots()
        {
            int slots = 1;  // Base: 1 research at a time
            
            // INT unlocks more research slots
            if (Attributes.INT >= 10) slots++;
            if (Attributes.INT >= 14) slots++;
            if (Attributes.INT >= 18) slots++;
            
            // Trait bonuses (Scholar, etc.)
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            slots += traitBonuses.ResearchSlotBonus;
            
            return slots;
        }
        
        private int CalculateRecipeUnlocks()
        {
            int unlocks = 0;
            
            // INT unlocks bonus recipes
            // Every 2 INT above 10 = 1 bonus recipe tier
            unlocks = Math.Max(0, (Attributes.INT - 10) / 2);
            
            // Trait bonuses
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            unlocks += traitBonuses.RecipeUnlockBonus;
            
            return unlocks;
        }
        
        private float CalculateDamageResistance()
        {
            float resistance = 0f;
            
            // Armor from equipment (each point = 1% resistance)
            float totalArmor = Inventory?.GetTotalArmor() ?? 0f;
            resistance += totalArmor * 0.01f;
            
            // Attribute bonus (END)
            resistance += Attributes.ResistanceBonus;
            
            // Mutation bonuses
            var mutationBonuses = _mutationSystem.CalculateBonuses(Mutations);
            resistance += mutationBonuses.ResistanceBonus;
            
            return Math.Clamp(resistance, 0f, 0.9f); // Max 90% resistance
        }
        
        /// <summary>
        /// Get total armor value from all equipment
        /// </summary>
        public float GetTotalArmor()
        {
            return Inventory?.GetTotalArmor() ?? 0f;
        }
        
        /// <summary>
        /// Get currently equipped weapon (or null if unarmed)
        /// </summary>
        public Item GetEquippedWeapon()
        {
            return Inventory?.GetWeapon();
        }
        
        /// <summary>
        /// Get item equipped in a specific slot
        /// </summary>
        public Item GetEquipped(EquipSlot slot)
        {
            return Inventory?.GetEquipped(slot);
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
        
        // Moveable Vital Organ cooldown tracking
        public float VitalOrganCooldownDays { get; set; } = 0f;
        public bool CanUseMoveableVitalOrgan => HasMutation(MutationType.MoveableVitalOrgan) && VitalOrganCooldownDays <= 0;
        
        // Track pending vital organ relocation (for popup)
        public Body.DamageResult? PendingVitalOrganDamage { get; set; } = null;
        
        /// <summary>
        /// Take damage using the body part system
        /// Returns the damage result for UI/combat log
        /// </summary>
        public Body.DamageResult TakeDamageToBody(float amount, DamageType type = DamageType.Physical)
        {
            // Apply resistance first
            float resistedAmount = amount * (1f - DamageResistance);
            
            // Body handles part selection, armor, and damage
            var result = Body.TakeDamage(resistedAmount, type);
            
            // Update overall HP based on body parts
            SyncHPWithBody();
            
            // Check for instant death
            if (result.IsInstantDeath)
            {
                // Check for Moveable Vital Organ mutation
                if (CanUseMoveableVitalOrgan)
                {
                    result.CanRelocateOrgan = true;
                    PendingVitalOrganDamage = result;
                    // Don't set HP to 0 yet - wait for player choice
                }
                else
                {
                    CurrentHealth = 0;
                    System.Diagnostics.Debug.WriteLine($">>> INSTANT DEATH! {result.HitPart?.Name} destroyed! <<<");
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Legacy TakeDamage for compatibility
        /// </summary>
        public void TakeDamage(float amount, DamageType type = DamageType.Physical)
        {
            TakeDamageToBody(amount, type);
        }
        
        /// <summary>
        /// Sync CurrentHealth with body parts
        /// </summary>
        public void SyncHPWithBody()
        {
            CurrentHealth = GetBodyHP();
            if (!Body.IsAlive)
            {
                CurrentHealth = 0;
            }
            
            // If HP is at or very close to max, ensure all body parts are fully healed
            // This prevents the weird case where HP shows 100% but body parts are damaged
            if (CurrentHealth >= MaxHealth - 0.5f)
            {
                CurrentHealth = MaxHealth;
                foreach (var part in Body.Parts.Values)
                {
                    if (part.Condition != BodyPartCondition.Missing && 
                        part.Condition != BodyPartCondition.Destroyed)
                    {
                        part.CurrentHealth = part.MaxHealth;
                        // Also clear all injuries and ailments when fully healed
                        part.Injuries.Clear();
                        part.Ailments.Clear();
                    }
                }
            }
        }
        
        /// <summary>
        /// Relocate damage from critical part to target part (Moveable Vital Organ)
        /// </summary>
        public bool RelocateVitalOrganDamage(BodyPart targetPart)
        {
            if (PendingVitalOrganDamage == null) return false;
            if (!CanUseMoveableVitalOrgan) return false;
            
            var criticalPart = PendingVitalOrganDamage.Value.HitPart;
            if (criticalPart == null) return false;
            
            // Perform the relocation
            bool success = Body.RelocateDamageFromCriticalPart(criticalPart, targetPart);
            
            if (success)
            {
                // Set cooldown based on mutation level
                int level = GetMutationLevel(MutationType.MoveableVitalOrgan);
                VitalOrganCooldownDays = 4 - level;  // Level 1 = 3 days, Level 2 = 2 days, Level 3 = 1 day
                
                // Clear pending and sync HP
                PendingVitalOrganDamage = null;
                SyncHPWithBody();
                
                System.Diagnostics.Debug.WriteLine($">>> Vital organ relocated! Cooldown: {VitalOrganCooldownDays} days <<<");
            }
            
            return success;
        }
        
        /// <summary>
        /// Skip the vital organ relocation (player chooses death or doesn't use it)
        /// </summary>
        public void SkipVitalOrganRelocation()
        {
            if (PendingVitalOrganDamage != null)
            {
                CurrentHealth = 0;
                PendingVitalOrganDamage = null;
                System.Diagnostics.Debug.WriteLine(">>> Player chose not to relocate vital organ. DEATH! <<<");
            }
        }
        
        /// <summary>
        /// Update cooldowns (call from time system)
        /// </summary>
        public void TickCooldowns(float days)
        {
            if (VitalOrganCooldownDays > 0)
            {
                VitalOrganCooldownDays = Math.Max(0, VitalOrganCooldownDays - days);
            }
        }
        
        public void Heal(float amount)
        {
            if (amount <= 0) return;
            
            // Trait modifiers
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            amount *= traitBonuses.HealingModifier;
            
            float oldHP = CurrentHealth;
            float targetHP = Math.Min(oldHP + amount, MaxHealth);
            
            // Get damaged parts
            var damagedParts = Body.Parts.Values
                .Where(p => p.Condition != BodyPartCondition.Missing && 
                           p.Condition != BodyPartCondition.Destroyed &&
                           p.CurrentHealth < p.MaxHealth)
                .ToList();
            
            if (damagedParts.Count == 0)
            {
                return;
            }
            
            // Iteratively heal body parts until we reach target HP
            int iterations = 0;
            const int MAX_ITERATIONS = 20;
            
            while (GetBodyHP() < targetHP - 0.5f && iterations < MAX_ITERATIONS)
            {
                float remaining = targetHP - GetBodyHP();
                float totalMissingHP = damagedParts.Sum(p => p.MaxHealth - p.CurrentHealth);
                
                if (totalMissingHP < 0.1f) break;
                
                float healMultiplier = Math.Min(1f, (remaining / MaxHealth) * 3f + 0.1f);
                
                bool anyHealed = false;
                foreach (var part in damagedParts)
                {
                    float missingHP = part.MaxHealth - part.CurrentHealth;
                    if (missingHP > 0.1f)
                    {
                        float healAmount = missingHP * healMultiplier;
                        healAmount = Math.Max(0.5f, healAmount);
                        part.Heal(healAmount);
                        anyHealed = true;
                    }
                }
                
                if (!anyHealed) break;
                iterations++;
            }
            
            SyncHPWithBody();
            CurrentHealth = Math.Min(CurrentHealth, MaxHealth);
        }
        
        /// <summary>
        /// Heal by percentage of MaxHP, distributing to body parts proportionally
        /// This is the main healing method for items like Bandage
        /// </summary>
        public float HealByPercent(float percent)
        {
            if (percent <= 0) return 0f;
            
            float oldHP = CurrentHealth;
            
            // Calculate target HP to heal
            float hpToHeal = MaxHealth * (percent / 100f);
            
            // Trait modifiers
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            hpToHeal *= traitBonuses.HealingModifier;
            
            float targetHP = Math.Min(oldHP + hpToHeal, MaxHealth);
            
            // If already at or above target, nothing to do
            if (CurrentHealth >= targetHP - 0.1f)
            {
                return 0f;
            }
            
            // Get damaged parts
            var damagedParts = Body.Parts.Values
                .Where(p => p.Condition != BodyPartCondition.Missing && 
                           p.Condition != BodyPartCondition.Destroyed &&
                           p.CurrentHealth < p.MaxHealth)
                .ToList();
            
            if (damagedParts.Count == 0)
            {
                return 0f;
            }
            
            // Iteratively heal body parts until we reach target HP
            // This is needed because HP = MaxHealth * BodyHealthPercent (weighted average)
            // so healing body parts doesn't map 1:1 to overall HP
            int iterations = 0;
            const int MAX_ITERATIONS = 20;
            
            while (GetBodyHP() < targetHP - 0.5f && iterations < MAX_ITERATIONS)
            {
                float remaining = targetHP - GetBodyHP();
                
                // Calculate total missing HP across all parts
                float totalMissingHP = damagedParts.Sum(p => p.MaxHealth - p.CurrentHealth);
                
                if (totalMissingHP < 0.1f)
                {
                    break; // All parts full
                }
                
                // Heal each part proportionally to their missing HP
                // Use aggressive multiplier to reach target faster
                float healMultiplier = Math.Min(1f, (remaining / MaxHealth) * 3f + 0.1f);
                
                bool anyHealed = false;
                foreach (var part in damagedParts)
                {
                    float missingHP = part.MaxHealth - part.CurrentHealth;
                    if (missingHP > 0.1f)
                    {
                        float healAmount = missingHP * healMultiplier;
                        healAmount = Math.Max(0.5f, healAmount); // Minimum heal per iteration
                        part.Heal(healAmount);
                        anyHealed = true;
                    }
                }
                
                if (!anyHealed) break;
                iterations++;
            }
            
            // Final sync
            SyncHPWithBody();
            
            float actualHealed = CurrentHealth - oldHP;
            System.Diagnostics.Debug.WriteLine($">>> HealByPercent: {percent}% target={hpToHeal:F1} HP, actual healed={actualHealed:F1} HP (iterations: {iterations}) <<<");
            
            return actualHealed;
        }
        
        /// <summary>
        /// Heal the most damaged body part (for inventory quick-heal)
        /// Returns HP restored
        /// </summary>
        public float HealMostDamagedPart(float healPercent)
        {
            var part = Body.GetMostCriticalPart();
            if (part == null) return 0f;
            
            // Heal by percentage of the part's max health
            float healAmount = part.MaxHealth * (healPercent / 100f);
            
            // Trait modifier
            var traitBonuses = _traitSystem.CalculateBonuses(Traits);
            healAmount *= traitBonuses.HealingModifier;
            
            float hpRestored = Body.HealPart(part, healAmount);
            SyncHPWithBody();
            
            return hpRestored;
        }
        
        // ============================================
        // DEBUG
        // ============================================
        
        public string GetStatusReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== CHARACTER STATS ===");
            report.AppendLine($"Level: {Level} ({CurrentXP:F0}/{XPToNextLevel:F0} XP)");
            report.AppendLine();
            report.AppendLine("--- Attributes ---");
            report.AppendLine($"  {Attributes.GetDisplayString()}");
            report.AppendLine();
            report.AppendLine("--- Combat Stats ---");
            report.AppendLine($"  Health: {CurrentHealth:F0}/{MaxHealth:F0}");
            report.AppendLine($"  Speed: {Speed:F0}");
            report.AppendLine($"  Damage: {Damage:F1}");
            report.AppendLine($"  Accuracy: {Accuracy:P0}");
            report.AppendLine($"  Sight Range: {SightRange:F0}");
            report.AppendLine($"  Action Points: {ActionPoints} AP");
            report.AppendLine($"  Movement Points: {MovementPoints} MP");
            report.AppendLine($"  Damage Resist: {DamageResistance:P0}");
            report.AppendLine($"  Regen: {RegenRate:F1}/tick");
            report.AppendLine();
            report.AppendLine($"Science Path: {SciencePath}");
            report.AppendLine($"Pending Attr Points: {PendingAttributePoints} | Mutation Points: {MutationPoints} | Free Picks: {FreeMutationPicks}");
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
