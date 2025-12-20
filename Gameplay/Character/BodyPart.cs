// Gameplay/Character/BodyPart.cs
// Individual body part with condition, injuries, equipment, implants, and function tracking

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;
using MyRPG.Gameplay.Items;

namespace MyRPG.Gameplay.Character
{
    /// <summary>
    /// Injury types that can affect body parts (Rimworld-style)
    /// </summary>
    public enum InjuryType
    {
        None,
        Cut,
        Bruise,
        Scratch,
        Bite,
        Gunshot,
        Burn,
        Frostbite,
        Crush,
        Stab,
        Shrapnel,
        ChemicalBurn,
        Fracture,         // Bone broken
        Laceration,       // Deep cut
        Puncture,         // Deep hole
        Amputation        // Part removed
    }
    
    /// <summary>
    /// Conditions/diseases that can affect body parts
    /// </summary>
    public enum BodyPartAilment
    {
        None,
        Bleeding,         // Loses blood over time
        Infected,         // Gets worse without treatment
        Inflamed,         // Painful, reduced function
        Scarred,          // Permanent efficiency reduction
        Necrotic,         // Tissue death, spreads
        Paralyzed,        // Cannot use
        Cancerous,        // Grows, spreads
        Mutating,         // Unstable mutation in progress
        Poisoned,         // Toxin damage over time
        Irradiated        // Radiation sickness
    }
    
    /// <summary>
    /// Individual injury instance on a body part
    /// </summary>
    public class Injury
    {
        public string Id { get; set; } = Guid.NewGuid().ToString().Substring(0, 8);
        public InjuryType Type { get; set; }
        public float Severity { get; set; }         // 0-1, affects efficiency reduction
        public float HealProgress { get; set; }     // 0-1, how much healed
        public bool IsPermanent { get; set; }       // Scars don't heal
        public float BleedRate { get; set; }        // HP lost per hour (0 = not bleeding)
        public float InfectionChance { get; set; }  // % chance to get infected if untreated
        public float Age { get; set; }              // Hours since injury
        
        public bool IsHealed => HealProgress >= 1.0f && !IsPermanent;
        public float EffectiveSeverity => Severity * (1f - HealProgress);
        
        public Injury(InjuryType type, float severity)
        {
            Type = type;
            Severity = severity;
            
            // Set bleed rate based on injury type
            BleedRate = type switch
            {
                InjuryType.Cut => severity * 0.5f,
                InjuryType.Laceration => severity * 1.0f,
                InjuryType.Gunshot => severity * 1.5f,
                InjuryType.Stab => severity * 0.8f,
                InjuryType.Bite => severity * 0.3f,
                InjuryType.Shrapnel => severity * 0.6f,
                InjuryType.Amputation => severity * 3.0f,
                _ => 0f
            };
            
            // Set infection chance
            InfectionChance = type switch
            {
                InjuryType.Bite => 0.4f,
                InjuryType.Cut => 0.1f,
                InjuryType.Stab => 0.15f,
                InjuryType.Gunshot => 0.2f,
                InjuryType.Shrapnel => 0.25f,
                _ => 0.05f
            };
        }
        
        public string GetDescription()
        {
            string severityStr = Severity switch
            {
                >= 0.9f => "Severe",
                >= 0.7f => "Major",
                >= 0.5f => "Moderate",
                >= 0.3f => "Minor",
                _ => "Trivial"
            };
            return $"{severityStr} {Type}";
        }
    }
    
    public class BodyPart
    {
        // Identity
        public string Id { get; private set; }              // Unique ID like "LeftArm_1" or "LeftArm_2"
        public string Name { get; set; }                    // Display name
        public BodyPartType Type { get; private set; }
        public BodyPartCategory Category { get; private set; }
        
        // Hierarchy
        public string ParentId { get; set; }                // What this is attached to
        public List<string> ChildIds { get; set; } = new List<string>();
        
        // Health
        public float MaxHealth { get; set; } = 100f;
        public float CurrentHealth { get; set; } = 100f;
        public BodyPartCondition Condition { get; private set; } = BodyPartCondition.Healthy;
        
        // HP System Weights
        /// <summary>
        /// How important this part is for overall HP calculation.
        /// Head/Brain/Heart = high, Arms/Legs = low
        /// </summary>
        public float ImportanceWeight => GetImportanceWeight(Type);
        
        /// <summary>
        /// How likely this part is to be hit by attacks.
        /// Larger exposed parts = higher chance
        /// </summary>
        public float TargetWeight => GetTargetWeight(Type);
        
        /// <summary>
        /// Is this an instant-death part if destroyed?
        /// </summary>
        public bool IsCriticalPart => Type == BodyPartType.Head || Type == BodyPartType.Brain || Type == BodyPartType.Heart;
        
        // Function (0.0 to 1.0+)
        public float Efficiency => CalculateEfficiency();
        
        // Injuries (Rimworld-style)
        public List<Injury> Injuries { get; set; } = new List<Injury>();
        public List<BodyPartAilment> Ailments { get; set; } = new List<BodyPartAilment>();
        
        // Equipment slot for this body part (hands can hold weapons, etc.)
        public Item EquippedItem { get; set; } = null;
        
        // Two-handed weapon support - if this hand is holding the "other" end of a 2H weapon
        public string TwoHandedPairId { get; set; } = null;  // ID of the other hand holding the same weapon
        public bool IsHoldingTwoHandedWeapon => EquippedItem?.Definition?.IsTwoHanded == true;
        public bool CanEquipWeapon => Type == BodyPartType.LeftHand || Type == BodyPartType.RightHand || 
                                       Type == BodyPartType.MutantHand;
        public bool CanEquipArmor => Type == BodyPartType.Torso || Type == BodyPartType.Head ||
                                      Type == BodyPartType.LeftLeg || Type == BodyPartType.RightLeg ||
                                      Type == BodyPartType.LeftArm || Type == BodyPartType.RightArm;
        
        // Implants
        public int MaxImplantSlots { get; set; } = 1;
        public List<Implant> InstalledImplants { get; set; } = new List<Implant>();
        
        // Status Effects on this specific part
        public List<StatusEffectType> LocalEffects { get; set; } = new List<StatusEffectType>();
        
        // Mutation flag (was this added by mutation?)
        public bool IsMutationPart { get; set; } = false;
        public MutationType? SourceMutation { get; set; } = null;
        
        // Is this part vital? (death if destroyed)
        public bool IsVital => Category == BodyPartCategory.Vital;
        
        // Bleeding tracking
        public float TotalBleedRate => Injuries.Sum(i => i.BleedRate * (1f - i.HealProgress));
        public bool IsBleeding => TotalBleedRate > 0.1f;
        public bool IsInfected => Ailments.Contains(BodyPartAilment.Infected);
        public bool HasFracture => Injuries.Any(i => i.Type == InjuryType.Fracture);
        
        // Constructor
        public BodyPart(string id, BodyPartType type, string name = null)
        {
            Id = id;
            Type = type;
            Name = name ?? GetDisplayName(type);
            Category = GetDefaultCategory(type);
            MaxImplantSlots = GetDefaultImplantSlots(type);
        }
        
        // ============================================
        // DAMAGE & INJURIES
        // ============================================
        
        public Injury TakeDamage(float amount, DamageType damageType = DamageType.Physical)
        {
            CurrentHealth -= amount;
            
            // Create injury based on damage type
            InjuryType injuryType = damageType switch
            {
                DamageType.Physical => amount > 15 ? InjuryType.Laceration : InjuryType.Cut,
                DamageType.Fire => InjuryType.Burn,
                DamageType.Cold => InjuryType.Frostbite,
                DamageType.Poison => InjuryType.ChemicalBurn,
                DamageType.Radiation => InjuryType.ChemicalBurn,
                DamageType.Electric => InjuryType.Burn,
                DamageType.Acid => InjuryType.ChemicalBurn,
                _ => InjuryType.Bruise
            };
            
            float severity = Math.Clamp(amount / MaxHealth, 0.1f, 1.0f);
            var injury = new Injury(injuryType, severity);
            Injuries.Add(injury);
            
            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0;
                Condition = BodyPartCondition.Destroyed;
            }
            else
            {
                UpdateCondition();
            }
            
            // Fire damage might cause Burning
            if (damageType == DamageType.Fire && !LocalEffects.Contains(StatusEffectType.Burning))
            {
                LocalEffects.Add(StatusEffectType.Burning);
            }
            
            return injury;
        }
        
        public void AddInjury(InjuryType type, float severity)
        {
            var injury = new Injury(type, severity);
            Injuries.Add(injury);
            
            float damage = severity * MaxHealth * 0.5f;
            CurrentHealth = Math.Max(0, CurrentHealth - damage);
            UpdateCondition();
        }
        
        public void AddAilment(BodyPartAilment ailment)
        {
            if (!Ailments.Contains(ailment))
            {
                Ailments.Add(ailment);
            }
        }
        
        public bool RemoveAilment(BodyPartAilment ailment)
        {
            return Ailments.Remove(ailment);
        }
        
        // ============================================
        // HEALING
        // ============================================
        
        public void Heal(float amount)
        {
            if (Condition == BodyPartCondition.Missing || Condition == BodyPartCondition.Destroyed)
            {
                return; // Cannot heal missing/destroyed parts
            }
            
            CurrentHealth = Math.Min(CurrentHealth + amount, MaxHealth);
            UpdateCondition();
        }
        
        /// <summary>
        /// Heal injuries over time (natural healing)
        /// </summary>
        public void TickHealing(float hours, float healingRate = 1.0f)
        {
            foreach (var injury in Injuries.ToList())
            {
                if (injury.IsPermanent) continue;
                
                // Base healing rate per hour
                float healAmount = 0.01f * healingRate * hours;
                
                // Infected injuries heal slower
                if (Ailments.Contains(BodyPartAilment.Infected))
                {
                    healAmount *= 0.2f;
                }
                
                injury.HealProgress = Math.Min(1.0f, injury.HealProgress + healAmount);
                injury.Age += hours;
                
                // Reduce bleed rate as injury heals
                injury.BleedRate *= (1f - injury.HealProgress);
                
                // Random chance to get infected
                if (injury.Age > 6 && injury.HealProgress < 0.5f && !Ailments.Contains(BodyPartAilment.Infected))
                {
                    if (new Random().NextDouble() < injury.InfectionChance * 0.01f * hours)
                    {
                        AddAilment(BodyPartAilment.Infected);
                    }
                }
            }
            
            // Remove fully healed injuries
            Injuries.RemoveAll(i => i.IsHealed);
            
            // Update health based on remaining injuries
            RecalculateHealth();
        }
        
        /// <summary>
        /// Apply medical item to this body part
        /// </summary>
        public bool ApplyMedicalItem(Item item)
        {
            if (item?.Definition == null || !item.Definition.IsMedical) return false;
            
            bool success = false;
            
            // Heal HP
            if (item.Definition.BodyPartHealAmount > 0)
            {
                Heal(item.Definition.BodyPartHealAmount);
                success = true;
            }
            
            // Stop bleeding
            if (item.Definition.CanHealBleeding && IsBleeding)
            {
                foreach (var injury in Injuries)
                {
                    injury.BleedRate = 0;
                }
                success = true;
            }
            
            // Cure infection
            if (item.Definition.CanHealInfection && IsInfected)
            {
                RemoveAilment(BodyPartAilment.Infected);
                success = true;
            }
            
            // Treat fracture
            if (item.Definition.CanHealFracture && HasFracture)
            {
                var fracture = Injuries.FirstOrDefault(i => i.Type == InjuryType.Fracture);
                if (fracture != null)
                {
                    fracture.HealProgress += 0.5f;  // Splints help fractures heal faster
                }
                success = true;
            }
            
            return success;
        }
        
        /// <summary>
        /// Equip an item to this body part (for hands/armor slots)
        /// </summary>
        public bool EquipItem(Item item)
        {
            if (Condition == BodyPartCondition.Missing || Condition == BodyPartCondition.Destroyed)
                return false;
            
            if (CanEquipWeapon && item.Category == ItemCategory.Weapon)
            {
                EquippedItem = item;
                return true;
            }
            
            if (CanEquipArmor && item.Category == ItemCategory.Armor)
            {
                EquippedItem = item;
                return true;
            }
            
            return false;
        }
        
        public Item UnequipItem()
        {
            var item = EquippedItem;
            EquippedItem = null;
            return item;
        }
        
        private void RecalculateHealth()
        {
            // Health is reduced by severity of injuries
            float totalSeverity = Injuries.Sum(i => i.EffectiveSeverity);
            float targetHealth = MaxHealth * (1f - totalSeverity * 0.5f);
            CurrentHealth = Math.Clamp(CurrentHealth, 0, Math.Max(targetHealth, MaxHealth * 0.1f));
            UpdateCondition();
        }
        
        // ============================================
        // EFFICIENCY CALCULATION
        // ============================================
        
        private float CalculateEfficiency()
        {
            if (Condition == BodyPartCondition.Missing) return 0f;
            if (Condition == BodyPartCondition.Destroyed) return 0f;
            
            // Base efficiency from health
            float baseEfficiency = Condition switch
            {
                BodyPartCondition.Healthy => 1.0f,
                BodyPartCondition.Scratched => 0.9f,
                BodyPartCondition.Bruised => 0.8f,
                BodyPartCondition.Cut => 0.7f,
                BodyPartCondition.Injured => 0.5f,
                BodyPartCondition.SeverelyInjured => 0.25f,
                BodyPartCondition.Broken => 0.1f,
                _ => 0f
            };
            
            // Injury penalty
            float injuryPenalty = Injuries.Sum(i => i.EffectiveSeverity * 0.1f);
            baseEfficiency -= injuryPenalty;
            
            // Ailment penalties
            if (Ailments.Contains(BodyPartAilment.Infected)) baseEfficiency *= 0.8f;
            if (Ailments.Contains(BodyPartAilment.Inflamed)) baseEfficiency *= 0.9f;
            if (Ailments.Contains(BodyPartAilment.Paralyzed)) baseEfficiency = 0f;
            if (Ailments.Contains(BodyPartAilment.Scarred)) baseEfficiency *= 0.95f;
            
            // Implants can modify efficiency
            foreach (var implant in InstalledImplants)
            {
                baseEfficiency *= implant.EfficiencyModifier;
            }
            
            // Status effects on this part
            if (LocalEffects.Contains(StatusEffectType.Frozen)) baseEfficiency *= 0.5f;
            if (LocalEffects.Contains(StatusEffectType.Burning)) baseEfficiency *= 0.7f;
            
            return Math.Clamp(baseEfficiency, 0f, 2f); // Can exceed 1.0 with good implants
        }
        
        // ============================================
        // IMPLANTS
        // ============================================
        
        public bool CanInstallImplant()
        {
            return InstalledImplants.Count < MaxImplantSlots &&
                   Condition != BodyPartCondition.Missing &&
                   Condition != BodyPartCondition.Destroyed;
        }
        
        public bool InstallImplant(Implant implant)
        {
            if (!CanInstallImplant()) return false;
            InstalledImplants.Add(implant);
            return true;
        }
        
        public bool RemoveImplant(Implant implant)
        {
            return InstalledImplants.Remove(implant);
        }
        
        // ============================================
        // REMOVAL (amputation/destruction)
        // ============================================
        
        public void Remove()
        {
            Condition = BodyPartCondition.Missing;
            CurrentHealth = 0;
            InstalledImplants.Clear();
            LocalEffects.Clear();
            Injuries.Clear();
            Ailments.Clear();
            EquippedItem = null;
        }
        
        private void UpdateCondition()
        {
            float healthPercent = CurrentHealth / MaxHealth;
            
            Condition = healthPercent switch
            {
                >= 0.95f => BodyPartCondition.Healthy,
                >= 0.80f => BodyPartCondition.Scratched,
                >= 0.60f => BodyPartCondition.Bruised,
                >= 0.40f => BodyPartCondition.Cut,
                >= 0.25f => BodyPartCondition.Injured,
                >= 0.10f => BodyPartCondition.SeverelyInjured,
                > 0f => BodyPartCondition.Broken,
                _ => BodyPartCondition.Destroyed
            };
        }
        
        private static string GetDisplayName(BodyPartType type)
        {
            return type switch
            {
                BodyPartType.LeftArm => "Left Arm",
                BodyPartType.RightArm => "Right Arm",
                BodyPartType.LeftHand => "Left Hand",
                BodyPartType.RightHand => "Right Hand",
                BodyPartType.LeftLeg => "Left Leg",
                BodyPartType.RightLeg => "Right Leg",
                BodyPartType.LeftFoot => "Left Foot",
                BodyPartType.RightFoot => "Right Foot",
                BodyPartType.LeftEye => "Left Eye",
                BodyPartType.RightEye => "Right Eye",
                BodyPartType.LeftLung => "Left Lung",
                BodyPartType.RightLung => "Right Lung",
                BodyPartType.MutantArm => "Mutant Arm",
                BodyPartType.MutantHand => "Mutant Hand",
                BodyPartType.MutantLeg => "Mutant Leg",
                BodyPartType.ExtraEye => "Extra Eye",
                BodyPartType.PsionicNode => "Psionic Node",
                BodyPartType.VenomGland => "Venom Gland",
                _ => type.ToString()
            };
        }
        
        private static BodyPartCategory GetDefaultCategory(BodyPartType type)
        {
            return type switch
            {
                BodyPartType.Brain => BodyPartCategory.Vital,
                BodyPartType.Heart => BodyPartCategory.Vital,
                
                BodyPartType.Head => BodyPartCategory.Important,
                BodyPartType.Torso => BodyPartCategory.Important,
                BodyPartType.LeftLung => BodyPartCategory.Important,
                BodyPartType.RightLung => BodyPartCategory.Important,
                BodyPartType.Liver => BodyPartCategory.Important,
                BodyPartType.Stomach => BodyPartCategory.Important,
                
                BodyPartType.LeftEye => BodyPartCategory.Sensory,
                BodyPartType.RightEye => BodyPartCategory.Sensory,
                BodyPartType.Nose => BodyPartCategory.Sensory,
                BodyPartType.ExtraEye => BodyPartCategory.Sensory,
                BodyPartType.Antennae => BodyPartCategory.Sensory,
                BodyPartType.PsionicNode => BodyPartCategory.Sensory,
                
                BodyPartType.LeftArm => BodyPartCategory.Manipulation,
                BodyPartType.RightArm => BodyPartCategory.Manipulation,
                BodyPartType.LeftHand => BodyPartCategory.Manipulation,
                BodyPartType.RightHand => BodyPartCategory.Manipulation,
                BodyPartType.Tentacle => BodyPartCategory.Manipulation,
                BodyPartType.MutantArm => BodyPartCategory.Manipulation,
                BodyPartType.MutantHand => BodyPartCategory.Manipulation,
                
                BodyPartType.LeftLeg => BodyPartCategory.Movement,
                BodyPartType.RightLeg => BodyPartCategory.Movement,
                BodyPartType.LeftFoot => BodyPartCategory.Movement,
                BodyPartType.RightFoot => BodyPartCategory.Movement,
                BodyPartType.Tail => BodyPartCategory.Movement,
                BodyPartType.Wings => BodyPartCategory.Movement,
                BodyPartType.MutantLeg => BodyPartCategory.Movement,
                
                BodyPartType.Carapace => BodyPartCategory.Utility,
                BodyPartType.VenomGland => BodyPartCategory.Utility,
                BodyPartType.Gills => BodyPartCategory.Utility,
                
                _ => BodyPartCategory.Utility
            };
        }
        
        private static int GetDefaultImplantSlots(BodyPartType type)
        {
            return type switch
            {
                BodyPartType.Brain => 3,        // Lots of neural implants possible
                BodyPartType.Torso => 2,
                BodyPartType.LeftArm => 2,
                BodyPartType.RightArm => 2,
                BodyPartType.MutantArm => 2,
                BodyPartType.LeftHand => 1,
                BodyPartType.RightHand => 1,
                BodyPartType.LeftEye => 1,
                BodyPartType.RightEye => 1,
                BodyPartType.LeftLeg => 1,
                BodyPartType.RightLeg => 1,
                _ => 0                          // Internal organs, etc.
            };
        }
        
        /// <summary>
        /// How important this part is for overall HP calculation.
        /// Critical parts contribute more to max HP.
        /// </summary>
        private static float GetImportanceWeight(BodyPartType type)
        {
            return type switch
            {
                // Critical - instant death if destroyed
                BodyPartType.Brain => 2.5f,
                BodyPartType.Heart => 3.0f,
                BodyPartType.Head => 2.0f,
                
                // Important - major HP contribution
                BodyPartType.Torso => 1.5f,
                BodyPartType.LeftLung => 1.2f,
                BodyPartType.RightLung => 1.2f,
                BodyPartType.Liver => 1.2f,
                BodyPartType.Stomach => 1.0f,
                
                // Moderate - standard contribution
                BodyPartType.LeftArm => 0.6f,
                BodyPartType.RightArm => 0.6f,
                BodyPartType.MutantArm => 0.5f,
                BodyPartType.LeftLeg => 0.7f,
                BodyPartType.RightLeg => 0.7f,
                BodyPartType.MutantLeg => 0.5f,
                
                // Low - minor contribution
                BodyPartType.LeftHand => 0.3f,
                BodyPartType.RightHand => 0.3f,
                BodyPartType.MutantHand => 0.25f,
                BodyPartType.LeftFoot => 0.3f,
                BodyPartType.RightFoot => 0.3f,
                BodyPartType.LeftEye => 0.2f,
                BodyPartType.RightEye => 0.2f,
                BodyPartType.Nose => 0.1f,
                BodyPartType.Jaw => 0.2f,
                
                // Mutation/Special parts
                BodyPartType.Tail => 0.3f,
                BodyPartType.Wings => 0.4f,
                BodyPartType.Tentacle => 0.3f,
                BodyPartType.Carapace => 0.5f,
                BodyPartType.VenomGland => 0.2f,
                BodyPartType.Gills => 0.3f,
                BodyPartType.PsionicNode => 0.3f,
                BodyPartType.ExtraEye => 0.15f,
                BodyPartType.Antennae => 0.1f,
                
                _ => 0.2f
            };
        }
        
        /// <summary>
        /// How likely this part is to be hit by attacks.
        /// Larger, more exposed parts = higher chance
        /// </summary>
        private static float GetTargetWeight(BodyPartType type)
        {
            return type switch
            {
                // Most exposed - high chance
                BodyPartType.Torso => 3.0f,        // Big target
                BodyPartType.Head => 1.5f,         // Obvious target but smaller
                BodyPartType.LeftArm => 1.5f,
                BodyPartType.RightArm => 1.5f,
                BodyPartType.LeftLeg => 2.0f,
                BodyPartType.RightLeg => 2.0f,
                
                // Moderately exposed
                BodyPartType.LeftHand => 0.8f,
                BodyPartType.RightHand => 0.8f,
                BodyPartType.LeftFoot => 0.6f,
                BodyPartType.RightFoot => 0.6f,
                BodyPartType.MutantArm => 1.2f,
                BodyPartType.MutantLeg => 1.5f,
                BodyPartType.MutantHand => 0.6f,
                
                // Protected/internal - low chance
                BodyPartType.Brain => 0.1f,        // Protected by skull
                BodyPartType.Heart => 0.2f,        // Protected by ribcage
                BodyPartType.LeftLung => 0.3f,
                BodyPartType.RightLung => 0.3f,
                BodyPartType.Liver => 0.2f,
                BodyPartType.Stomach => 0.3f,
                BodyPartType.LeftEye => 0.3f,
                BodyPartType.RightEye => 0.3f,
                BodyPartType.Nose => 0.2f,
                BodyPartType.Jaw => 0.4f,
                
                // Special/mutation parts
                BodyPartType.Tail => 1.0f,
                BodyPartType.Wings => 1.5f,        // Big target when deployed
                BodyPartType.Tentacle => 0.8f,
                BodyPartType.Carapace => 2.0f,     // Shell is front-facing
                BodyPartType.VenomGland => 0.1f,   // Internal
                BodyPartType.Gills => 0.2f,
                BodyPartType.PsionicNode => 0.1f,  // Internal
                BodyPartType.ExtraEye => 0.2f,
                BodyPartType.Antennae => 0.3f,
                
                _ => 0.5f
            };
        }
        
        /// <summary>
        /// Get the armor/protection value from equipped item
        /// </summary>
        public float GetArmorValue()
        {
            if (EquippedItem == null) return 0f;
            if (EquippedItem.Definition == null) return 0f;
            return EquippedItem.Definition.Armor;
        }
        
        // ============================================
        // DEBUG/DISPLAY
        // ============================================
        
        public override string ToString()
        {
            return $"{Name} [{Condition}] HP:{CurrentHealth:F0}/{MaxHealth:F0} Eff:{Efficiency:P0}";
        }
    }
    
    // ============================================
    // IMPLANT (placeholder for now)
    // ============================================
    
    public class Implant
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public float EfficiencyModifier { get; set; } = 1.0f;   // 1.2 = 20% boost
        public SciencePath RequiredPath { get; set; }           // Tinker or Dark
        
        // Stats this implant provides
        public Dictionary<string, float> StatModifiers { get; set; } = new Dictionary<string, float>();
        
        public Implant(string id, string name, SciencePath path)
        {
            Id = id;
            Name = name;
            RequiredPath = path;
        }
    }
}
