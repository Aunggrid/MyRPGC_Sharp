// Gameplay/Systems/WieldingSystem.cs
// Handles weapon wielding, grip modes, dual-wielding penalties, and multi-weapon attacks

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;
using MyRPG.Gameplay.Character;
using MyRPG.Gameplay.Items;

namespace MyRPG.Gameplay.Systems
{
    // ============================================
    // EQUIPPED WEAPON INFO
    // ============================================
    
    public class EquippedWeaponInfo
    {
        public Item Weapon { get; set; }
        public EquipSlot Slot { get; set; }
        public GripMode GripMode { get; set; } = GripMode.Default;
        public bool IsTwoHanding { get; set; } = false;
        public int HandsUsed { get; set; } = 1;
        
        // Calculated combat values (after modifiers)
        public float EffectiveDamage { get; set; }
        public float EffectiveAccuracy { get; set; }
        public float AttackSpeedModifier { get; set; } = 1f;
        
        // Penalty tracking
        public float DualWieldPenalty { get; set; } = 0f;
        public float GripPenalty { get; set; } = 0f;
        public float TotalPenalty => DualWieldPenalty + GripPenalty;
        
        public bool IsMelee => Weapon?.Definition != null && 
            (Weapon.Definition.WeaponType == WeaponType.Knife ||
             Weapon.Definition.WeaponType == WeaponType.Sword ||
             Weapon.Definition.WeaponType == WeaponType.Axe ||
             Weapon.Definition.WeaponType == WeaponType.Club ||
             Weapon.Definition.WeaponType == WeaponType.Spear ||
             Weapon.Definition.WeaponType == WeaponType.Unarmed);
        
        public bool IsRanged => Weapon?.Definition != null &&
            (Weapon.Definition.WeaponType == WeaponType.Bow ||
             Weapon.Definition.WeaponType == WeaponType.Crossbow ||
             Weapon.Definition.WeaponType == WeaponType.Pistol ||
             Weapon.Definition.WeaponType == WeaponType.Rifle ||
             Weapon.Definition.WeaponType == WeaponType.Shotgun ||
             Weapon.Definition.WeaponType == WeaponType.EnergyWeapon);
    }
    
    // ============================================
    // ATTACK RESULT
    // ============================================
    
    public class WeaponAttackResult
    {
        public EquippedWeaponInfo Weapon { get; set; }
        public float BaseDamage { get; set; }
        public float FinalDamage { get; set; }
        public float HitChance { get; set; }
        public bool WasHit { get; set; }
        public bool WasCritical { get; set; }
        public string AttackDescription { get; set; }
    }
    
    // ============================================
    // WIELDING SYSTEM
    // ============================================
    
    public class WieldingSystem
    {
        // Constants for penalty/bonus calculations - CHALLENGING BUT FAIR
        public const float BASE_DUAL_WIELD_PENALTY = 0.25f;     // 25% base penalty - requires investment
        public const float BASE_ONE_HAND_PENALTY = 0.30f;       // 30% penalty for one-handing a 2H weapon
        public const float BASE_TWO_HAND_BONUS = 0.18f;         // 18% bonus for two-handing
        public const float AKIMBO_RANGED_PENALTY = 0.30f;       // 30% accuracy penalty - akimbo is risky
        
        // Attribute scaling - investment pays off
        public const float AGI_DUAL_WIELD_REDUCTION = 0.012f;   // Each AGI point reduces penalty by 1.2%
        public const float STR_DUAL_WIELD_REDUCTION = 0.006f;   // Each STR point reduces penalty by 0.6%
        public const float STR_TWO_HAND_BONUS = 0.012f;         // Each STR adds 1.2% to two-hand damage
        public const float PER_AKIMBO_REDUCTION = 0.012f;       // Each PER reduces akimbo penalty by 1.2%
        public const float AGI_AKIMBO_REDUCTION = 0.006f;       // Each AGI reduces akimbo penalty by 0.6%
        
        private static Random _random = new Random();
        
        // ============================================
        // MAIN API
        // ============================================
        
        /// <summary>
        /// Get all equipped weapons with their calculated combat stats
        /// </summary>
        public static List<EquippedWeaponInfo> GetEquippedWeapons(CharacterStats stats)
        {
            var weapons = new List<EquippedWeaponInfo>();
            var equipment = stats.Inventory.Equipment;
            
            // Check all hand slots
            var handSlots = new[] { EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.ExtraArm1, EquipSlot.ExtraArm2 };
            
            foreach (var slot in handSlots)
            {
                var item = equipment.GetEquipped(slot);
                if (item != null && item.Definition?.Category == ItemCategory.Weapon)
                {
                    var info = new EquippedWeaponInfo
                    {
                        Weapon = item,
                        Slot = slot,
                        GripMode = equipment.GetGripMode(slot),
                        HandsUsed = 1
                    };
                    weapons.Add(info);
                }
            }
            
            // Also check two-hand slot
            var twoHandItem = equipment.GetEquipped(EquipSlot.TwoHand);
            if (twoHandItem != null && twoHandItem.Definition?.Category == ItemCategory.Weapon)
            {
                var info = new EquippedWeaponInfo
                {
                    Weapon = twoHandItem,
                    Slot = EquipSlot.TwoHand,
                    GripMode = GripMode.TwoHand,
                    IsTwoHanding = true,
                    HandsUsed = 2
                };
                weapons.Add(info);
            }
            
            // Calculate effective stats for each weapon
            CalculateWeaponStats(weapons, stats);
            
            return weapons;
        }
        
        /// <summary>
        /// Calculate effective damage, accuracy, and penalties for all equipped weapons
        /// </summary>
        public static void CalculateWeaponStats(List<EquippedWeaponInfo> weapons, CharacterStats stats)
        {
            if (weapons.Count == 0) return;
            
            int meleeCount = weapons.Count(w => w.IsMelee);
            int rangedCount = weapons.Count(w => w.IsRanged);
            
            foreach (var weaponInfo in weapons)
            {
                var weapon = weaponInfo.Weapon;
                var def = weapon.Definition;
                if (def == null) continue;
                
                // Base stats from weapon (with quality)
                float baseDamage = weapon.GetEffectiveDamage();
                float baseAccuracy = weapon.GetEffectiveAccuracy();
                
                // Reset penalties
                weaponInfo.DualWieldPenalty = 0f;
                weaponInfo.GripPenalty = 0f;
                weaponInfo.AttackSpeedModifier = 1f;
                
                // Determine if two-handing
                weaponInfo.IsTwoHanding = weaponInfo.Slot == EquipSlot.TwoHand || 
                                          weaponInfo.GripMode == GripMode.TwoHand;
                
                // === TWO-HANDING BONUS ===
                if (weaponInfo.IsTwoHanding && def.CanUseTwoHand)
                {
                    // Base two-hand bonus + STR scaling
                    float twoHandBonus = def.TwoHandDamageBonus + (stats.Attributes.STR * STR_TWO_HAND_BONUS);
                    baseDamage *= (1f + twoHandBonus);
                    weaponInfo.HandsUsed = 2;
                }
                
                // === ONE-HANDING PENALTY (for normally two-handed weapons) ===
                else if (def.HandsRequired >= 2 && !weaponInfo.IsTwoHanding && def.CanUseOneHand)
                {
                    // Penalty for one-handing a heavy weapon, reduced by STR
                    float penalty = BASE_ONE_HAND_PENALTY - (stats.Attributes.STR * 0.01f);
                    penalty = Math.Max(0.05f, penalty); // Minimum 5% penalty
                    weaponInfo.GripPenalty = penalty;
                    baseDamage *= (1f - penalty);
                    baseAccuracy -= penalty * 10; // Also affects accuracy
                }
                
                // === DUAL WIELDING / MULTI-WIELDING PENALTIES ===
                if (weaponInfo.IsMelee && meleeCount > 1)
                {
                    // Calculate dual wield penalty
                    float basePenalty = def.DualWieldPenalty;
                    
                    // Reduce by AGI (main) and STR (secondary)
                    float reduction = (stats.Attributes.AGI * AGI_DUAL_WIELD_REDUCTION) + 
                                     (stats.Attributes.STR * STR_DUAL_WIELD_REDUCTION);
                    
                    // Apply trait/mutation bonuses
                    reduction += GetDualWieldBonuses(stats);
                    
                    float finalPenalty = Math.Max(0f, basePenalty - reduction);
                    weaponInfo.DualWieldPenalty = finalPenalty;
                    
                    baseDamage *= (1f - finalPenalty);
                    baseAccuracy -= finalPenalty * 5;
                    
                    // But gain attack speed!
                    weaponInfo.AttackSpeedModifier = 1f + (0.1f * (meleeCount - 1));
                }
                
                // === AKIMBO RANGED PENALTIES ===
                if (weaponInfo.IsRanged && rangedCount > 1)
                {
                    float basePenalty = AKIMBO_RANGED_PENALTY + def.DualWieldPenalty;
                    
                    // Reduce by PER (main) and AGI (secondary)
                    float reduction = (stats.Attributes.PER * PER_AKIMBO_REDUCTION) + 
                                     (stats.Attributes.AGI * AGI_AKIMBO_REDUCTION);
                    
                    // Apply trait/mutation bonuses
                    reduction += GetAkimboBonuses(stats);
                    
                    float finalPenalty = Math.Max(0f, basePenalty - reduction);
                    weaponInfo.DualWieldPenalty = finalPenalty;
                    
                    baseDamage *= (1f - finalPenalty * 0.5f); // Half penalty to damage
                    baseAccuracy -= finalPenalty * 15;         // Full penalty to accuracy
                    
                    // Gain fire rate
                    weaponInfo.AttackSpeedModifier = 1f + (0.15f * (rangedCount - 1));
                }
                
                // Store calculated values
                weaponInfo.EffectiveDamage = Math.Max(1f, baseDamage);
                weaponInfo.EffectiveAccuracy = baseAccuracy;
            }
        }
        
        /// <summary>
        /// Get bonus to dual wield penalty reduction from traits/mutations
        /// </summary>
        private static float GetDualWieldBonuses(CharacterStats stats)
        {
            float bonus = 0f;
            
            // Check mutations
            foreach (var mutation in stats.Mutations)
            {
                switch (mutation.Type)
                {
                    case MutationType.ExtraArms:
                        bonus += 0.05f * mutation.Level; // Extra arms make multi-wielding easier
                        break;
                    case MutationType.EnhancedReflexes:
                        bonus += 0.03f * mutation.Level;
                        break;
                }
            }
            
            // Check traits
            // (Could add specific traits like "Ambidextrous" here)
            
            return bonus;
        }
        
        /// <summary>
        /// Get bonus to akimbo penalty reduction from traits/mutations
        /// </summary>
        private static float GetAkimboBonuses(CharacterStats stats)
        {
            float bonus = 0f;
            
            foreach (var mutation in stats.Mutations)
            {
                switch (mutation.Type)
                {
                    case MutationType.ExtraArms:
                        bonus += 0.03f * mutation.Level;
                        break;
                    case MutationType.EagleEye:
                        bonus += 0.04f * mutation.Level;
                        break;
                }
            }
            
            return bonus;
        }
        
        // ============================================
        // ATTACK CALCULATIONS
        // ============================================
        
        /// <summary>
        /// Calculate total attacks available based on equipped weapons
        /// </summary>
        public static int GetTotalAttacksPerAction(CharacterStats stats)
        {
            var weapons = GetEquippedWeapons(stats);
            if (weapons.Count == 0) return 1; // Unarmed
            
            // Each weapon = 1 attack (for melee)
            // Ranged weapons don't stack attacks the same way
            int meleeWeapons = weapons.Count(w => w.IsMelee);
            int rangedWeapons = weapons.Count(w => w.IsRanged);
            
            if (meleeWeapons > 0)
            {
                return meleeWeapons; // Each melee weapon attacks
            }
            else if (rangedWeapons > 0)
            {
                // Ranged: 1 attack but fires all weapons
                return 1; // Will hit with all weapons in sequence
            }
            
            return 1;
        }
        
        /// <summary>
        /// Get all attack results for a single attack action
        /// </summary>
        public static List<WeaponAttackResult> CalculateAttacks(CharacterStats attacker, float baseHitChance, float targetArmor)
        {
            var results = new List<WeaponAttackResult>();
            var weapons = GetEquippedWeapons(attacker);
            
            if (weapons.Count == 0)
            {
                // Unarmed attack
                results.Add(CreateUnarmedAttack(attacker, baseHitChance, targetArmor));
                return results;
            }
            
            // Each equipped weapon gets an attack
            foreach (var weaponInfo in weapons)
            {
                var result = CalculateSingleAttack(weaponInfo, attacker, baseHitChance, targetArmor);
                results.Add(result);
            }
            
            return results;
        }
        
        private static WeaponAttackResult CreateUnarmedAttack(CharacterStats attacker, float baseHitChance, float targetArmor)
        {
            float damage = 3f + (attacker.Attributes.STR * 0.5f);
            float hitChance = baseHitChance + (attacker.Attributes.AGI * 2f);
            
            bool hit = _random.NextDouble() * 100 < hitChance;
            bool crit = hit && _random.NextDouble() < 0.05 + (attacker.Attributes.AGI * 0.01);
            
            float finalDamage = hit ? Math.Max(1f, damage - targetArmor * 0.5f) : 0f;
            if (crit) finalDamage *= 1.5f;
            
            return new WeaponAttackResult
            {
                Weapon = null,
                BaseDamage = damage,
                FinalDamage = finalDamage,
                HitChance = hitChance,
                WasHit = hit,
                WasCritical = crit,
                AttackDescription = crit ? "Critical unarmed strike!" : (hit ? "Unarmed strike" : "Missed (unarmed)")
            };
        }
        
        private static WeaponAttackResult CalculateSingleAttack(EquippedWeaponInfo weaponInfo, CharacterStats attacker, float baseHitChance, float targetArmor)
        {
            float damage = weaponInfo.EffectiveDamage;
            float accuracy = weaponInfo.EffectiveAccuracy;
            
            // Add attribute bonuses
            if (weaponInfo.IsMelee)
            {
                damage += attacker.Attributes.STR * 0.5f;
                accuracy += attacker.Attributes.AGI * 1.5f;
            }
            else
            {
                damage += attacker.Attributes.PER * 0.3f;
                accuracy += attacker.Attributes.PER * 2f + attacker.Attributes.AGI * 0.5f;
            }
            
            float hitChance = baseHitChance + accuracy;
            hitChance = Math.Clamp(hitChance, 5f, 95f); // Always 5-95% chance
            
            bool hit = _random.NextDouble() * 100 < hitChance;
            bool crit = hit && _random.NextDouble() < 0.05 + (attacker.Attributes.PER * 0.01);
            
            float finalDamage = hit ? Math.Max(1f, damage - targetArmor * 0.3f) : 0f;
            if (crit) finalDamage *= 1.5f;
            
            // Build description
            string desc;
            if (!hit)
            {
                desc = $"Missed with {weaponInfo.Weapon.Name}";
            }
            else if (crit)
            {
                desc = $"CRITICAL with {weaponInfo.Weapon.Name}!";
            }
            else
            {
                desc = $"Hit with {weaponInfo.Weapon.Name}";
            }
            
            if (weaponInfo.TotalPenalty > 0.01f)
            {
                desc += $" ({weaponInfo.TotalPenalty * 100:F0}% penalty)";
            }
            else if (weaponInfo.IsTwoHanding)
            {
                desc += " (two-handed)";
            }
            
            return new WeaponAttackResult
            {
                Weapon = weaponInfo,
                BaseDamage = damage,
                FinalDamage = finalDamage,
                HitChance = hitChance,
                WasHit = hit,
                WasCritical = crit,
                AttackDescription = desc
            };
        }
        
        // ============================================
        // UTILITY METHODS
        // ============================================
        
        /// <summary>
        /// Get a summary of wielding status for UI display
        /// </summary>
        public static string GetWieldingStatusSummary(CharacterStats stats)
        {
            var weapons = GetEquippedWeapons(stats);
            
            if (weapons.Count == 0)
                return "Unarmed";
            
            if (weapons.Count == 1)
            {
                var w = weapons[0];
                if (w.IsTwoHanding)
                    return $"{w.Weapon.Name} (Two-Handed)";
                else
                    return w.Weapon.Name;
            }
            
            // Multiple weapons
            int melee = weapons.Count(w => w.IsMelee);
            int ranged = weapons.Count(w => w.IsRanged);
            
            if (melee > 1)
            {
                float avgPenalty = weapons.Where(w => w.IsMelee).Average(w => w.DualWieldPenalty);
                return $"Multi-Wield ({melee} weapons, {avgPenalty * 100:F0}% penalty)";
            }
            
            if (ranged > 1)
            {
                float avgPenalty = weapons.Where(w => w.IsRanged).Average(w => w.DualWieldPenalty);
                return $"Akimbo ({ranged} weapons, {avgPenalty * 100:F0}% penalty)";
            }
            
            return $"{weapons.Count} weapons equipped";
        }
        
        /// <summary>
        /// Get total damage potential from all weapons
        /// </summary>
        public static float GetTotalDamagePotential(CharacterStats stats)
        {
            var weapons = GetEquippedWeapons(stats);
            return weapons.Sum(w => w.EffectiveDamage);
        }
        
        /// <summary>
        /// Check if player can equip another weapon in a specific slot
        /// </summary>
        public static bool CanEquipWeaponInSlot(CharacterStats stats, Item weapon, EquipSlot targetSlot)
        {
            if (weapon?.Definition == null) return false;
            if (weapon.Definition.Category != ItemCategory.Weapon) return false;
            
            var def = weapon.Definition;
            
            // Check if slot is a hand slot
            bool isHandSlot = targetSlot == EquipSlot.MainHand || 
                             targetSlot == EquipSlot.OffHand ||
                             targetSlot == EquipSlot.ExtraArm1 ||
                             targetSlot == EquipSlot.ExtraArm2 ||
                             targetSlot == EquipSlot.TwoHand;
            
            if (!isHandSlot) return false;
            
            // Two-hand slot requires CanUseTwoHand
            if (targetSlot == EquipSlot.TwoHand && !def.CanUseTwoHand)
                return false;
            
            // One-hand slots require CanUseOneHand
            if (targetSlot != EquipSlot.TwoHand && !def.CanUseOneHand)
                return false;
            
            // Check if slot exists (extra arms need mutation)
            if (targetSlot == EquipSlot.ExtraArm1 || targetSlot == EquipSlot.ExtraArm2)
            {
                bool hasExtraArms = stats.Mutations.Any(m => m.Type == MutationType.ExtraArms);
                if (!hasExtraArms) return false;
            }
            
            return true;
        }
    }
}
