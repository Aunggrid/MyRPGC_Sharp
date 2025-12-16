// Gameplay/Character/BodyPart.cs
// Individual body part with condition, implants, and function tracking

using System;
using System.Collections.Generic;
using MyRPG.Data;

namespace MyRPG.Gameplay.Character
{
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
        
        // Function (0.0 to 1.0)
        public float Efficiency => CalculateEfficiency();
        
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
        
        // Constructor
        public BodyPart(string id, BodyPartType type, string name = null)
        {
            Id = id;
            Type = type;
            Name = name ?? type.ToString();
            Category = GetDefaultCategory(type);
            MaxImplantSlots = GetDefaultImplantSlots(type);
        }
        
        // ============================================
        // DAMAGE & HEALING
        // ============================================
        
        public void TakeDamage(float amount, DamageType damageType = DamageType.Physical)
        {
            CurrentHealth -= amount;
            
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
        }
        
        public void Heal(float amount)
        {
            if (Condition == BodyPartCondition.Missing || Condition == BodyPartCondition.Destroyed)
            {
                return; // Cannot heal missing/destroyed parts
            }
            
            CurrentHealth = Math.Min(CurrentHealth + amount, MaxHealth);
            UpdateCondition();
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
        }
        
        // ============================================
        // DEFAULTS
        // ============================================
        
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
                
                BodyPartType.LeftArm => BodyPartCategory.Manipulation,
                BodyPartType.RightArm => BodyPartCategory.Manipulation,
                BodyPartType.LeftHand => BodyPartCategory.Manipulation,
                BodyPartType.RightHand => BodyPartCategory.Manipulation,
                BodyPartType.Tentacle => BodyPartCategory.Manipulation,
                
                BodyPartType.LeftLeg => BodyPartCategory.Movement,
                BodyPartType.RightLeg => BodyPartCategory.Movement,
                BodyPartType.LeftFoot => BodyPartCategory.Movement,
                BodyPartType.RightFoot => BodyPartCategory.Movement,
                BodyPartType.Tail => BodyPartCategory.Movement,
                BodyPartType.Wings => BodyPartCategory.Movement,
                
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
                BodyPartType.LeftHand => 1,
                BodyPartType.RightHand => 1,
                BodyPartType.LeftEye => 1,
                BodyPartType.RightEye => 1,
                BodyPartType.LeftLeg => 1,
                BodyPartType.RightLeg => 1,
                _ => 0                          // Internal organs, etc.
            };
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
