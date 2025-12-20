// Gameplay/Character/Body.cs
// Complete body simulation with parts, mutations, and aggregate calculations

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;
using MyRPG.Gameplay.Items;

namespace MyRPG.Gameplay.Character
{
    public class Body
    {
        // All body parts indexed by unique ID
        public Dictionary<string, BodyPart> Parts { get; private set; } = new Dictionary<string, BodyPart>();
        
        // Quick access to parts by type (can have multiple of same type)
        private Dictionary<BodyPartType, List<string>> _partsByType = new Dictionary<BodyPartType, List<string>>();
        
        // Track which mutations have been applied
        public List<MutationType> AppliedMutations { get; private set; } = new List<MutationType>();
        
        // ============================================
        // AGGREGATE STATS (calculated from parts)
        // ============================================
        
        /// <summary>
        /// Movement capability (0-2+). 0 = immobile, 1 = normal, >1 = enhanced
        /// </summary>
        public float MovementCapacity => CalculateMovementCapacity();
        
        /// <summary>
        /// Manipulation capability for using items/weapons
        /// </summary>
        public float ManipulationCapacity => CalculateManipulationCapacity();
        
        /// <summary>
        /// Vision capability (affects perception, accuracy)
        /// </summary>
        public float VisionCapacity => CalculateVisionCapacity();
        
        /// <summary>
        /// Overall consciousness (brain function)
        /// </summary>
        public float Consciousness => CalculateConsciousness();
        
        /// <summary>
        /// Number of functional hands (for weapon wielding)
        /// </summary>
        public int FunctionalHands => CountFunctionalParts(BodyPartType.LeftHand, BodyPartType.RightHand);
        
        /// <summary>
        /// Number of functional arms (for implant capacity, carrying)
        /// </summary>
        public int FunctionalArms => CountFunctionalParts(BodyPartType.LeftArm, BodyPartType.RightArm);
        
        /// <summary>
        /// Is the body alive?
        /// </summary>
        public bool IsAlive => CheckVitalParts();
        
        // ============================================
        // INITIALIZATION
        // ============================================
        
        public Body()
        {
            CreateDefaultHumanoidBody();
        }
        
        private void CreateDefaultHumanoidBody()
        {
            // HEAD
            AddPart(new BodyPart("Head", BodyPartType.Head) { MaxHealth = 75 });
            AddPart(new BodyPart("Brain", BodyPartType.Brain) { MaxHealth = 50, ParentId = "Head" });
            AddPart(new BodyPart("LeftEye", BodyPartType.LeftEye) { MaxHealth = 25, ParentId = "Head" });
            AddPart(new BodyPart("RightEye", BodyPartType.RightEye) { MaxHealth = 25, ParentId = "Head" });
            AddPart(new BodyPart("Nose", BodyPartType.Nose) { MaxHealth = 20, ParentId = "Head" });
            AddPart(new BodyPart("Jaw", BodyPartType.Jaw) { MaxHealth = 40, ParentId = "Head" });
            
            // TORSO
            AddPart(new BodyPart("Torso", BodyPartType.Torso) { MaxHealth = 150 });
            AddPart(new BodyPart("Heart", BodyPartType.Heart) { MaxHealth = 50, ParentId = "Torso" });
            AddPart(new BodyPart("LeftLung", BodyPartType.LeftLung) { MaxHealth = 40, ParentId = "Torso" });
            AddPart(new BodyPart("RightLung", BodyPartType.RightLung) { MaxHealth = 40, ParentId = "Torso" });
            AddPart(new BodyPart("Stomach", BodyPartType.Stomach) { MaxHealth = 45, ParentId = "Torso" });
            AddPart(new BodyPart("Liver", BodyPartType.Liver) { MaxHealth = 45, ParentId = "Torso" });
            
            // LEFT ARM
            AddPart(new BodyPart("LeftArm", BodyPartType.LeftArm) { MaxHealth = 60, ParentId = "Torso" });
            AddPart(new BodyPart("LeftHand", BodyPartType.LeftHand) { MaxHealth = 35, ParentId = "LeftArm" });
            
            // RIGHT ARM
            AddPart(new BodyPart("RightArm", BodyPartType.RightArm) { MaxHealth = 60, ParentId = "Torso" });
            AddPart(new BodyPart("RightHand", BodyPartType.RightHand) { MaxHealth = 35, ParentId = "RightArm" });
            
            // LEFT LEG
            AddPart(new BodyPart("LeftLeg", BodyPartType.LeftLeg) { MaxHealth = 70, ParentId = "Torso" });
            AddPart(new BodyPart("LeftFoot", BodyPartType.LeftFoot) { MaxHealth = 35, ParentId = "LeftLeg" });
            
            // RIGHT LEG
            AddPart(new BodyPart("RightLeg", BodyPartType.RightLeg) { MaxHealth = 70, ParentId = "Torso" });
            AddPart(new BodyPart("RightFoot", BodyPartType.RightFoot) { MaxHealth = 35, ParentId = "RightLeg" });
            
            // Set child relationships
            UpdateChildRelationships();
        }
        
        // ============================================
        // PART MANAGEMENT
        // ============================================
        
        public void AddPart(BodyPart part)
        {
            Parts[part.Id] = part;
            
            // Track by type
            if (!_partsByType.ContainsKey(part.Type))
            {
                _partsByType[part.Type] = new List<string>();
            }
            _partsByType[part.Type].Add(part.Id);
            
            // Set health to max
            part.CurrentHealth = part.MaxHealth;
        }
        
        public void RemovePart(string partId)
        {
            if (Parts.TryGetValue(partId, out var part))
            {
                part.Remove();
                
                // Also remove all children
                foreach (var childId in part.ChildIds.ToList())
                {
                    RemovePart(childId);
                }
            }
        }
        
        public BodyPart GetPart(string partId)
        {
            return Parts.TryGetValue(partId, out var part) ? part : null;
        }
        
        public List<BodyPart> GetPartsByType(BodyPartType type)
        {
            if (!_partsByType.ContainsKey(type)) return new List<BodyPart>();
            
            return _partsByType[type]
                .Where(id => Parts.ContainsKey(id))
                .Select(id => Parts[id])
                .ToList();
        }
        
        private void UpdateChildRelationships()
        {
            foreach (var part in Parts.Values)
            {
                if (!string.IsNullOrEmpty(part.ParentId) && Parts.ContainsKey(part.ParentId))
                {
                    var parent = Parts[part.ParentId];
                    if (!parent.ChildIds.Contains(part.Id))
                    {
                        parent.ChildIds.Add(part.Id);
                    }
                }
            }
        }
        
        // ============================================
        // MUTATIONS
        // ============================================
        
        /// <summary>
        /// Apply a mutation that adds new body parts
        /// </summary>
        public void ApplyMutation(MutationType mutation, int level = 1)
        {
            AppliedMutations.Add(mutation);
            
            switch (mutation)
            {
                case MutationType.ExtraArms:
                    // Use proper MutantArm and MutantHand types for extra limbs
                    AddMutationPart(BodyPartType.MutantArm, "MutantArm_L", "Left Mutant Arm", mutation, "Torso");
                    AddMutationPart(BodyPartType.MutantHand, "MutantHand_L", "Left Mutant Hand", mutation, "MutantArm_L");
                    AddMutationPart(BodyPartType.MutantArm, "MutantArm_R", "Right Mutant Arm", mutation, "Torso");
                    AddMutationPart(BodyPartType.MutantHand, "MutantHand_R", "Right Mutant Hand", mutation, "MutantArm_R");
                    break;
                    
                case MutationType.ExtraLegs:
                    // Extra legs for movement
                    AddMutationPart(BodyPartType.MutantLeg, "MutantLeg_L", "Left Mutant Leg", mutation, "Torso");
                    AddMutationPart(BodyPartType.MutantLeg, "MutantLeg_R", "Right Mutant Leg", mutation, "Torso");
                    break;
                    
                case MutationType.ExtraEyes:
                    for (int i = 0; i < level; i++)
                    {
                        string id = $"ExtraEye_{i + 1}";
                        AddMutationPart(BodyPartType.ExtraEye, id, $"Extra Eye {i + 1}", mutation, "Head");
                    }
                    break;
                    
                case MutationType.Tail:
                    AddMutationPart(BodyPartType.Tail, "Tail", "Mutant Tail", mutation, "Torso");
                    break;
                    
                case MutationType.Wings:
                    AddMutationPart(BodyPartType.Wings, "Wings", "Mutant Wings", mutation, "Torso");
                    break;
                    
                case MutationType.Carapace:
                    AddMutationPart(BodyPartType.Carapace, "Carapace", "Armored Carapace", mutation, "Torso");
                    break;
                    
                case MutationType.PsionicAwakening:
                    AddMutationPart(BodyPartType.PsionicNode, "PsionicNode", "Psionic Node", mutation, "Brain");
                    break;
                    
                case MutationType.VenomGlands:
                    AddMutationPart(BodyPartType.VenomGland, "VenomGland", "Venom Gland", mutation, "Jaw");
                    break;
                    
                case MutationType.AquaticAdaptation:
                    AddMutationPart(BodyPartType.Gills, "Gills", "Mutant Gills", mutation, "Torso");
                    break;
                    
                case MutationType.Claws:
                    // Claws modify existing hands rather than adding parts
                    foreach (var hand in GetPartsByType(BodyPartType.LeftHand)
                        .Concat(GetPartsByType(BodyPartType.RightHand))
                        .Concat(GetPartsByType(BodyPartType.MutantHand)))
                    {
                        if (!hand.Name.Contains("Clawed"))
                            hand.Name = hand.Name + " (Clawed)";
                    }
                    break;
                    
                case MutationType.Telepathy:
                case MutationType.Telekinesis:
                case MutationType.PsychicScream:
                case MutationType.MindShield:
                case MutationType.PsionicBlast:
                case MutationType.DominateWill:
                    // Psychic mutations enhance existing psionic node if present, otherwise add one
                    if (!Parts.ContainsKey("PsionicNode"))
                    {
                        AddMutationPart(BodyPartType.PsionicNode, "PsionicNode", "Psionic Node", mutation, "Brain");
                    }
                    break;
            }
            
            UpdateChildRelationships();
        }
        
        private void AddMutationPart(BodyPartType type, string id, string name, MutationType sourceMutation, string parentId = "Torso")
        {
            var part = new BodyPart(id, type, name)
            {
                IsMutationPart = true,
                SourceMutation = sourceMutation,
                ParentId = parentId,
                MaxHealth = GetDefaultMutationPartHealth(type)
            };
            part.CurrentHealth = part.MaxHealth;
            
            AddPart(part);
        }
        
        private float GetDefaultMutationPartHealth(BodyPartType type)
        {
            return type switch
            {
                BodyPartType.LeftArm or BodyPartType.RightArm => 50f,
                BodyPartType.LeftHand or BodyPartType.RightHand => 30f,
                BodyPartType.MutantArm => 45f,
                BodyPartType.MutantHand => 28f,
                BodyPartType.MutantLeg => 55f,
                BodyPartType.ExtraEye => 20f,
                BodyPartType.Tail => 40f,
                BodyPartType.Wings => 60f,
                BodyPartType.Tentacle => 45f,
                BodyPartType.Carapace => 80f,
                BodyPartType.PsionicNode => 25f,
                BodyPartType.VenomGland => 20f,
                BodyPartType.Antennae => 15f,
                BodyPartType.Gills => 30f,
                _ => 30f
            };
        }
        
        // ============================================
        // DAMAGE DISTRIBUTION
        // ============================================
        
        /// <summary>
        /// Take damage to a random appropriate body part
        /// </summary>
        public BodyPart TakeDamage(float amount, DamageType type = DamageType.Physical, bool targetLimbs = false)
        {
            var validTargets = Parts.Values
                .Where(p => p.Condition != BodyPartCondition.Missing && p.Condition != BodyPartCondition.Destroyed)
                .ToList();
            
            if (targetLimbs)
            {
                // Prefer arms/legs for non-lethal targeting
                var limbs = validTargets.Where(p => 
                    p.Category == BodyPartCategory.Manipulation || 
                    p.Category == BodyPartCategory.Movement).ToList();
                    
                if (limbs.Any()) validTargets = limbs;
            }
            
            if (!validTargets.Any()) return null;
            
            // Weight by body part size (torso more likely than eye)
            var random = new Random();
            var target = validTargets[random.Next(validTargets.Count)];
            
            target.TakeDamage(amount, type);
            
            return target;
        }
        
        /// <summary>
        /// Take damage to a specific body part
        /// </summary>
        public void TakeDamageTopart(string partId, float amount, DamageType type = DamageType.Physical)
        {
            if (Parts.TryGetValue(partId, out var part))
            {
                part.TakeDamage(amount, type);
            }
        }
        
        // ============================================
        // CAPACITY CALCULATIONS
        // ============================================
        
        private float CalculateMovementCapacity()
        {
            var legs = GetPartsByType(BodyPartType.LeftLeg).Concat(GetPartsByType(BodyPartType.RightLeg)).ToList();
            var feet = GetPartsByType(BodyPartType.LeftFoot).Concat(GetPartsByType(BodyPartType.RightFoot)).ToList();
            
            if (!legs.Any()) return 0f; // No legs = no walking
            
            float legEfficiency = legs.Average(l => l.Efficiency);
            float footEfficiency = feet.Any() ? feet.Average(f => f.Efficiency) : 0.5f;
            
            float baseCapacity = (legEfficiency * 0.7f) + (footEfficiency * 0.3f);
            
            // Tail can help balance
            var tail = GetPartsByType(BodyPartType.Tail).FirstOrDefault();
            if (tail != null && tail.Efficiency > 0)
            {
                baseCapacity += 0.1f * tail.Efficiency;
            }
            
            // Wings enable flight (different movement type, but boost general mobility)
            var wings = GetPartsByType(BodyPartType.Wings).FirstOrDefault();
            if (wings != null && wings.Efficiency > 0.5f)
            {
                baseCapacity += 0.2f * wings.Efficiency;
            }
            
            return Math.Clamp(baseCapacity, 0f, 2f);
        }
        
        private float CalculateManipulationCapacity()
        {
            var arms = GetPartsByType(BodyPartType.LeftArm).Concat(GetPartsByType(BodyPartType.RightArm)).ToList();
            var hands = GetPartsByType(BodyPartType.LeftHand).Concat(GetPartsByType(BodyPartType.RightHand)).ToList();
            var tentacles = GetPartsByType(BodyPartType.Tentacle);
            
            if (!arms.Any() && !tentacles.Any()) return 0f;
            
            float armEfficiency = arms.Any() ? arms.Average(a => a.Efficiency) : 0f;
            float handEfficiency = hands.Any() ? hands.Average(h => h.Efficiency) : 0f;
            float tentacleBonus = tentacles.Sum(t => t.Efficiency * 0.3f);
            
            // Having more than 2 arms increases capacity beyond 1.0
            int armCount = arms.Count(a => a.Efficiency > 0.1f);
            float multiArmBonus = armCount > 2 ? (armCount - 2) * 0.25f : 0f;
            
            float baseCapacity = (armEfficiency * 0.4f) + (handEfficiency * 0.6f) + tentacleBonus + multiArmBonus;
            
            return Math.Clamp(baseCapacity, 0f, 2f);
        }
        
        private float CalculateVisionCapacity()
        {
            var eyes = GetPartsByType(BodyPartType.LeftEye)
                .Concat(GetPartsByType(BodyPartType.RightEye))
                .Concat(GetPartsByType(BodyPartType.ExtraEye))
                .ToList();
            
            if (!eyes.Any()) return 0f; // Blind
            
            float totalVision = eyes.Sum(e => e.Efficiency * 0.5f); // Each eye contributes 50%
            
            // Extra eyes beyond 2 give bonus but diminishing returns
            int functionalEyes = eyes.Count(e => e.Efficiency > 0.1f);
            if (functionalEyes > 2)
            {
                totalVision += (functionalEyes - 2) * 0.15f;
            }
            
            return Math.Clamp(totalVision, 0f, 2f);
        }
        
        private float CalculateConsciousness()
        {
            var brain = GetPart("Brain");
            if (brain == null) return 0f;
            
            return brain.Efficiency;
        }
        
        private int CountFunctionalParts(params BodyPartType[] types)
        {
            int count = 0;
            foreach (var type in types)
            {
                count += GetPartsByType(type).Count(p => p.Efficiency > 0.1f);
            }
            return count;
        }
        
        private bool CheckVitalParts()
        {
            foreach (var part in Parts.Values.Where(p => p.IsVital))
            {
                if (part.Condition == BodyPartCondition.Destroyed || part.Condition == BodyPartCondition.Missing)
                {
                    return false;
                }
            }
            return true;
        }
        
        // ============================================
        // HP CALCULATION FROM BODY PARTS
        // ============================================
        
        /// <summary>
        /// Base HP value (100) - this gets modified by attributes/mutations in CharacterStats
        /// </summary>
        public const float BASE_HP = 100f;
        
        /// <summary>
        /// Calculate body health as a percentage (0.0 to 1.0)
        /// Based on weighted average of all body parts
        /// </summary>
        public float BodyHealthPercent
        {
            get
            {
                float totalWeight = 0f;
                float weightedHealth = 0f;
                
                foreach (var part in Parts.Values)
                {
                    if (part.Condition != BodyPartCondition.Missing && part.Condition != BodyPartCondition.Destroyed)
                    {
                        float weight = part.ImportanceWeight;
                        totalWeight += weight;
                        weightedHealth += (part.CurrentHealth / part.MaxHealth) * weight;
                    }
                }
                
                return totalWeight > 0 ? weightedHealth / totalWeight : 0f;
            }
        }
        
        /// <summary>
        /// Get total importance weight of all valid body parts (for scaling)
        /// </summary>
        public float TotalImportanceWeight
        {
            get
            {
                float total = 0f;
                foreach (var part in Parts.Values)
                {
                    if (part.Condition != BodyPartCondition.Missing && part.Condition != BodyPartCondition.Destroyed)
                    {
                        total += part.ImportanceWeight;
                    }
                }
                return total;
            }
        }
        
        /// <summary>
        /// MaxHP is now just base 100 - actual max is calculated in CharacterStats
        /// </summary>
        public float MaxHP => BASE_HP;
        
        /// <summary>
        /// CurrentHP based on body health percentage
        /// </summary>
        public float CurrentHP => BASE_HP * BodyHealthPercent;
        
        // ============================================
        // DAMAGE DISTRIBUTION SYSTEM
        // ============================================
        
        /// <summary>
        /// Structure to track damage result for UI/combat log
        /// </summary>
        public struct DamageResult
        {
            public BodyPart HitPart;
            public float RawDamage;
            public float ArmorReduction;
            public float FinalDamage;
            public float HPLost;
            public bool IsCriticalHit;
            public bool IsInstantDeath;
            public bool CanRelocateOrgan;  // For Moveable Vital Organ mutation
        }
        
        private static Random _random = new Random();
        
        /// <summary>
        /// Take damage with random body part selection and armor calculation
        /// </summary>
        public DamageResult TakeDamage(float rawDamage, DamageType damageType = DamageType.Physical)
        {
            var result = new DamageResult
            {
                RawDamage = rawDamage,
                IsCriticalHit = false,
                IsInstantDeath = false,
                CanRelocateOrgan = false
            };
            
            // Select random body part weighted by target weight
            result.HitPart = SelectRandomTargetPart();
            if (result.HitPart == null)
            {
                // No valid target, all parts destroyed
                result.IsInstantDeath = true;
                return result;
            }
            
            // Calculate armor reduction from equipped item on that part
            float armor = result.HitPart.GetArmorValue();
            
            // Also check parent part for armor (e.g., torso armor protects internal organs)
            if (!string.IsNullOrEmpty(result.HitPart.ParentId) && Parts.TryGetValue(result.HitPart.ParentId, out var parent))
            {
                armor += parent.GetArmorValue() * 0.5f;  // 50% of parent's armor
            }
            
            // Apply armor reduction (diminishing returns formula)
            result.ArmorReduction = armor > 0 ? rawDamage * (armor / (armor + 50f)) : 0f;
            result.FinalDamage = Math.Max(1f, rawDamage - result.ArmorReduction);  // Minimum 1 damage
            
            // Apply damage to the body part
            result.HitPart.TakeDamage(result.FinalDamage, damageType);
            
            // Calculate HP lost based on importance
            result.HPLost = result.FinalDamage * result.HitPart.ImportanceWeight;
            
            // Check for instant death on critical parts
            if (result.HitPart.IsCriticalPart && result.HitPart.CurrentHealth <= 0)
            {
                result.IsInstantDeath = true;
                // Check for Moveable Vital Organ mutation (will be set by CharacterStats)
                result.CanRelocateOrgan = true;
            }
            
            return result;
        }
        
        /// <summary>
        /// Select a random body part weighted by target weight
        /// </summary>
        private BodyPart SelectRandomTargetPart()
        {
            var validParts = Parts.Values
                .Where(p => p.Condition != BodyPartCondition.Missing && 
                           p.Condition != BodyPartCondition.Destroyed &&
                           p.TargetWeight > 0)
                .ToList();
            
            if (validParts.Count == 0) return null;
            
            // Calculate total weight
            float totalWeight = validParts.Sum(p => p.TargetWeight);
            
            // Random selection
            float roll = (float)_random.NextDouble() * totalWeight;
            float cumulative = 0f;
            
            foreach (var part in validParts)
            {
                cumulative += part.TargetWeight;
                if (roll <= cumulative)
                {
                    return part;
                }
            }
            
            return validParts.Last();
        }
        
        /// <summary>
        /// Get the most damaged body part (for auto-healing)
        /// </summary>
        public BodyPart GetMostDamagedPart()
        {
            return Parts.Values
                .Where(p => p.Condition != BodyPartCondition.Missing && 
                           p.Condition != BodyPartCondition.Destroyed &&
                           p.CurrentHealth < p.MaxHealth)
                .OrderByDescending(p => (p.MaxHealth - p.CurrentHealth) * p.ImportanceWeight)  // Prioritize by damage × importance
                .FirstOrDefault();
        }
        
        /// <summary>
        /// Get the most critical part that needs healing (bleeding/infected first, then damaged)
        /// </summary>
        public BodyPart GetMostCriticalPart()
        {
            // First priority: bleeding parts
            var bleedingPart = Parts.Values
                .Where(p => p.IsBleeding)
                .OrderByDescending(p => p.TotalBleedRate)
                .FirstOrDefault();
            if (bleedingPart != null) return bleedingPart;
            
            // Second priority: infected parts
            var infectedPart = Parts.Values
                .Where(p => p.IsInfected)
                .FirstOrDefault();
            if (infectedPart != null) return infectedPart;
            
            // Third: most damaged
            return GetMostDamagedPart();
        }
        
        /// <summary>
        /// Heal a body part and return HP restored to overall health
        /// </summary>
        public float HealPart(BodyPart part, float healAmount)
        {
            if (part == null) return 0f;
            
            float oldHealth = part.CurrentHealth;
            part.Heal(healAmount);
            float actualHeal = part.CurrentHealth - oldHealth;
            
            // HP restored is heal amount × importance
            return actualHeal * part.ImportanceWeight;
        }
        
        /// <summary>
        /// Heal the most damaged part (for inventory quick-heal)
        /// Returns HP restored to overall health
        /// </summary>
        public float HealMostDamagedPart(float healAmount)
        {
            var part = GetMostDamagedPart();
            return HealPart(part, healAmount);
        }
        
        /// <summary>
        /// Relocate damage from a critical part to another part (Moveable Vital Organ mutation)
        /// </summary>
        public bool RelocateDamageFromCriticalPart(BodyPart criticalPart, BodyPart targetPart)
        {
            if (criticalPart == null || targetPart == null) return false;
            if (!criticalPart.IsCriticalPart) return false;
            if (targetPart.IsCriticalPart) return false;  // Can't move to another critical part
            
            // How much damage to transfer (enough to save the critical part)
            float damageToTransfer = criticalPart.MaxHealth * 0.25f - criticalPart.CurrentHealth;
            if (damageToTransfer <= 0) return false;  // Critical part is actually fine
            
            // Heal critical part back to 25% health
            criticalPart.Heal(damageToTransfer);
            
            // Target part takes the damage (with 1.5x penalty for the relocation)
            targetPart.TakeDamage(damageToTransfer * 1.5f);
            
            return true;
        }
        
        // ============================================
        // DEBUFFS FROM BROKEN PARTS
        // ============================================
        
        /// <summary>
        /// Get melee damage modifier based on arm/hand condition
        /// </summary>
        public float GetMeleeDamageModifier()
        {
            float modifier = 1.0f;
            
            // Check arms
            var arms = GetPartsByType(BodyPartType.LeftArm)
                .Concat(GetPartsByType(BodyPartType.RightArm))
                .Concat(GetPartsByType(BodyPartType.MutantArm));
            
            int brokenArms = arms.Count(a => a.Condition == BodyPartCondition.Broken || a.CurrentHealth <= 0);
            int totalArms = arms.Count();
            
            if (totalArms > 0 && brokenArms > 0)
            {
                modifier -= (0.25f * brokenArms);  // -25% per broken arm
            }
            
            // Check hands
            var hands = GetEquippableHands();
            int brokenHands = hands.Count(h => h.Condition == BodyPartCondition.Broken || h.CurrentHealth <= 0);
            
            if (brokenHands > 0)
            {
                modifier -= (0.2f * brokenHands);  // -20% per broken hand
            }
            
            return Math.Max(0.1f, modifier);  // Minimum 10% damage
        }
        
        /// <summary>
        /// Get movement modifier based on leg/foot condition
        /// </summary>
        public float GetMovementModifier()
        {
            float modifier = 1.0f;
            
            // Check legs
            var legs = GetPartsByType(BodyPartType.LeftLeg)
                .Concat(GetPartsByType(BodyPartType.RightLeg))
                .Concat(GetPartsByType(BodyPartType.MutantLeg));
            
            int brokenLegs = legs.Count(l => l.Condition == BodyPartCondition.Broken || l.CurrentHealth <= 0);
            int totalLegs = legs.Count();
            
            if (totalLegs > 0 && brokenLegs > 0)
            {
                modifier -= (0.3f * brokenLegs);  // -30% per broken leg
            }
            
            // Check feet
            var feet = GetPartsByType(BodyPartType.LeftFoot)
                .Concat(GetPartsByType(BodyPartType.RightFoot));
            
            int brokenFeet = feet.Count(f => f.Condition == BodyPartCondition.Broken || f.CurrentHealth <= 0);
            
            if (brokenFeet > 0)
            {
                modifier -= (0.15f * brokenFeet);  // -15% per broken foot
            }
            
            return Math.Max(0.1f, modifier);  // Minimum 10% movement
        }
        
        /// <summary>
        /// Get accuracy modifier based on eye condition
        /// </summary>
        public float GetAccuracyModifier()
        {
            float modifier = 1.0f;
            
            var eyes = GetPartsByType(BodyPartType.LeftEye)
                .Concat(GetPartsByType(BodyPartType.RightEye))
                .Concat(GetPartsByType(BodyPartType.ExtraEye));
            
            int brokenEyes = eyes.Count(e => e.Condition == BodyPartCondition.Broken || e.CurrentHealth <= 0);
            int totalEyes = eyes.Count();
            
            if (totalEyes > 0)
            {
                // Lose 40% accuracy per broken eye
                modifier -= (0.4f * brokenEyes);
                
                // Extra eyes give slight bonus
                int functionalExtraEyes = GetPartsByType(BodyPartType.ExtraEye).Count(e => e.Efficiency > 0.5f);
                modifier += (0.1f * functionalExtraEyes);
            }
            
            return Math.Max(0.1f, modifier);
        }
        
        /// <summary>
        /// Get AP modifier based on lung condition
        /// </summary>
        public int GetAPModifier()
        {
            var lungs = GetPartsByType(BodyPartType.LeftLung)
                .Concat(GetPartsByType(BodyPartType.RightLung));
            
            int brokenLungs = lungs.Count(l => l.Condition == BodyPartCondition.Broken || l.CurrentHealth <= 0);
            
            // -1 AP per broken lung
            return -brokenLungs;
        }
        
        // ============================================
        // IMPLANT SLOTS
        // ============================================
        
        public int GetTotalImplantSlots()
        {
            return Parts.Values
                .Where(p => p.Condition != BodyPartCondition.Missing && p.Condition != BodyPartCondition.Destroyed)
                .Sum(p => p.MaxImplantSlots);
        }
        
        public int GetUsedImplantSlots()
        {
            return Parts.Values.Sum(p => p.InstalledImplants.Count);
        }
        
        public int GetAvailableImplantSlots()
        {
            return GetTotalImplantSlots() - GetUsedImplantSlots();
        }
        
        // ============================================
        // MULTI-LIMB EQUIPMENT SYSTEM
        // ============================================
        
        /// <summary>
        /// Get all hands that can equip weapons (includes mutant hands)
        /// </summary>
        public List<BodyPart> GetEquippableHands()
        {
            var hands = new List<BodyPart>();
            
            // Original hands
            hands.AddRange(GetPartsByType(BodyPartType.LeftHand));
            hands.AddRange(GetPartsByType(BodyPartType.RightHand));
            
            // Mutant hands
            hands.AddRange(GetPartsByType(BodyPartType.MutantHand));
            
            // Filter to only functional ones
            return hands
                .Where(h => h.Condition != BodyPartCondition.Missing && 
                            h.Condition != BodyPartCondition.Destroyed &&
                            h.Efficiency > 0.1f)
                .ToList();
        }
        
        /// <summary>
        /// Get all equipped weapons from all hands
        /// </summary>
        public List<Item> GetEquippedWeapons()
        {
            var weapons = new List<Item>();
            var seenWeapons = new HashSet<Item>();  // Avoid counting 2H weapons twice
            
            foreach (var hand in GetEquippableHands())
            {
                if (hand.EquippedItem != null && 
                    hand.EquippedItem.Category == ItemCategory.Weapon &&
                    !seenWeapons.Contains(hand.EquippedItem))
                {
                    weapons.Add(hand.EquippedItem);
                    seenWeapons.Add(hand.EquippedItem);
                }
            }
            
            return weapons;
        }
        
        /// <summary>
        /// Calculate total damage from all equipped weapons (multi-weapon attacks)
        /// </summary>
        public float GetTotalWeaponDamage()
        {
            float totalDamage = 0f;
            
            foreach (var weapon in GetEquippedWeapons())
            {
                totalDamage += weapon.GetEffectiveDamage();
            }
            
            // If no weapons, unarmed damage based on hands
            if (totalDamage == 0)
            {
                totalDamage = GetEquippableHands().Count * 3f;  // 3 unarmed damage per hand
            }
            
            return totalDamage;
        }
        
        /// <summary>
        /// Get number of attacks per turn based on equipped weapons
        /// </summary>
        public int GetAttacksPerTurn()
        {
            var weapons = GetEquippedWeapons();
            
            // Base 1 attack, extra attack for each additional weapon (diminishing returns)
            if (weapons.Count <= 1) return 1;
            if (weapons.Count == 2) return 2;
            if (weapons.Count == 3) return 2;  // 3rd weapon doesn't add attack
            return 3;  // Max 3 attacks per turn even with 4+ weapons
        }
        
        /// <summary>
        /// Equip a weapon to available hand(s). Two-handed weapons use 2 hands.
        /// </summary>
        public bool EquipWeaponToHand(Item weapon)
        {
            var hands = GetEquippableHands();
            int handsNeeded = weapon.Definition?.HandsRequired ?? 1;
            
            // Get empty hands
            var emptyHands = hands.Where(h => h.EquippedItem == null && h.TwoHandedPairId == null).ToList();
            
            if (emptyHands.Count < handsNeeded)
            {
                return false;  // Not enough free hands
            }
            
            if (handsNeeded >= 2)
            {
                // Two-handed weapon - use first two empty hands
                var primaryHand = emptyHands[0];
                var secondaryHand = emptyHands[1];
                
                primaryHand.EquippedItem = weapon;
                primaryHand.TwoHandedPairId = secondaryHand.Id;
                
                secondaryHand.EquippedItem = weapon;  // Same weapon reference
                secondaryHand.TwoHandedPairId = primaryHand.Id;
                
                return true;
            }
            else
            {
                // One-handed weapon
                var hand = emptyHands[0];
                hand.EquippedItem = weapon;
                return true;
            }
        }
        
        /// <summary>
        /// Unequip a weapon from hand(s). Handles two-handed weapons.
        /// </summary>
        public Item UnequipWeaponFromHands(Item weapon)
        {
            var hands = GetEquippableHands();
            
            foreach (var hand in hands)
            {
                if (hand.EquippedItem == weapon)
                {
                    // If two-handed, also clear the paired hand
                    if (!string.IsNullOrEmpty(hand.TwoHandedPairId))
                    {
                        var pairedHand = Parts.Values.FirstOrDefault(p => p.Id == hand.TwoHandedPairId);
                        if (pairedHand != null)
                        {
                            pairedHand.EquippedItem = null;
                            pairedHand.TwoHandedPairId = null;
                        }
                    }
                    
                    hand.EquippedItem = null;
                    hand.TwoHandedPairId = null;
                    return weapon;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Get number of free hands available
        /// </summary>
        public int GetFreeHandCount()
        {
            return GetEquippableHands().Count(h => h.EquippedItem == null && h.TwoHandedPairId == null);
        }
        
        /// <summary>
        /// Check if weapon can be equipped (enough hands)
        /// </summary>
        public bool CanEquipWeapon(Item weapon)
        {
            int handsNeeded = weapon.Definition?.HandsRequired ?? 1;
            return GetFreeHandCount() >= handsNeeded;
        }
        
        /// <summary>
        /// Get hand that has a specific weapon equipped
        /// </summary>
        public BodyPart GetHandWithWeapon(Item weapon)
        {
            return GetEquippableHands().FirstOrDefault(h => h.EquippedItem == weapon);
        }
        
        // ============================================
        // INJURY TRACKING (Rimworld-style)
        // ============================================
        
        /// <summary>
        /// Total bleed rate from all body parts
        /// </summary>
        public float TotalBleedRate => Parts.Values.Sum(p => p.TotalBleedRate);
        
        /// <summary>
        /// Is any body part bleeding?
        /// </summary>
        public bool IsBleeding => TotalBleedRate > 0.1f;
        
        /// <summary>
        /// Is any body part infected?
        /// </summary>
        public bool HasInfection => Parts.Values.Any(p => p.IsInfected);
        
        /// <summary>
        /// Get all current injuries across the body
        /// </summary>
        public List<(BodyPart Part, Injury Injury)> GetAllInjuries()
        {
            var injuries = new List<(BodyPart, Injury)>();
            
            foreach (var part in Parts.Values)
            {
                foreach (var injury in part.Injuries)
                {
                    injuries.Add((part, injury));
                }
            }
            
            return injuries;
        }
        
        /// <summary>
        /// Tick all body parts for healing/bleeding/infection
        /// </summary>
        public void TickBodyParts(float hours, float healingRate = 1.0f)
        {
            foreach (var part in Parts.Values)
            {
                part.TickHealing(hours, healingRate);
            }
        }
        
        /// <summary>
        /// Get body part that would benefit most from medical attention
        /// </summary>
        public BodyPart GetMostUrgentPart()
        {
            return Parts.Values
                .Where(p => p.Condition != BodyPartCondition.Missing && p.Condition != BodyPartCondition.Destroyed)
                .OrderByDescending(p => p.TotalBleedRate)
                .ThenBy(p => p.IsInfected ? 0 : 1)
                .ThenBy(p => p.CurrentHealth / p.MaxHealth)
                .FirstOrDefault();
        }
        
        // ============================================
        // DEBUG/DISPLAY
        // ============================================
        
        public string GetStatusReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== BODY STATUS ===");
            report.AppendLine($"Alive: {IsAlive}");
            report.AppendLine($"Movement: {MovementCapacity:P0}");
            report.AppendLine($"Manipulation: {ManipulationCapacity:P0}");
            report.AppendLine($"Vision: {VisionCapacity:P0}");
            report.AppendLine($"Consciousness: {Consciousness:P0}");
            report.AppendLine($"Functional Hands: {FunctionalHands} (Equippable: {GetEquippableHands().Count})");
            report.AppendLine($"Functional Arms: {FunctionalArms}");
            report.AppendLine($"Implant Slots: {GetUsedImplantSlots()}/{GetTotalImplantSlots()}");
            
            if (IsBleeding)
                report.AppendLine($"BLEEDING: {TotalBleedRate:F1} HP/hour");
            if (HasInfection)
                report.AppendLine("WARNING: INFECTION DETECTED");
            
            report.AppendLine();
            report.AppendLine("--- Parts ---");
            
            foreach (var part in Parts.Values.OrderBy(p => p.ParentId ?? ""))
            {
                string mutationTag = part.IsMutationPart ? " [MUTANT]" : "";
                string equipTag = part.EquippedItem != null ? $" [{part.EquippedItem.Name}]" : "";
                string injuryTag = part.Injuries.Any() ? $" ({part.Injuries.Count} injuries)" : "";
                report.AppendLine($"  {part}{mutationTag}{equipTag}{injuryTag}");
            }
            
            return report.ToString();
        }
    }
}
