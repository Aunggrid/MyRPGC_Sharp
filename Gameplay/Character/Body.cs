// Gameplay/Character/Body.cs
// Complete body simulation with parts, mutations, and aggregate calculations

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;

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
                    AddMutationPart(BodyPartType.LeftArm, "LeftArm2", "Left Arm (Mutant)", mutation);
                    AddMutationPart(BodyPartType.LeftHand, "LeftHand2", "Left Hand (Mutant)", mutation, "LeftArm2");
                    AddMutationPart(BodyPartType.RightArm, "RightArm2", "Right Arm (Mutant)", mutation);
                    AddMutationPart(BodyPartType.RightHand, "RightHand2", "Right Hand (Mutant)", mutation, "RightArm2");
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
                    
                case MutationType.Claws:
                    // Claws modify existing hands rather than adding parts
                    foreach (var hand in GetPartsByType(BodyPartType.LeftHand).Concat(GetPartsByType(BodyPartType.RightHand)))
                    {
                        hand.Name = hand.Name + " (Clawed)";
                        // Would add a "Claws" component or stat modifier here
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
                BodyPartType.ExtraEye => 20f,
                BodyPartType.Tail => 40f,
                BodyPartType.Wings => 60f,
                BodyPartType.Tentacle => 45f,
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
            report.AppendLine($"Functional Hands: {FunctionalHands}");
            report.AppendLine($"Functional Arms: {FunctionalArms}");
            report.AppendLine($"Implant Slots: {GetUsedImplantSlots()}/{GetTotalImplantSlots()}");
            report.AppendLine();
            report.AppendLine("--- Parts ---");
            
            foreach (var part in Parts.Values.OrderBy(p => p.ParentId ?? ""))
            {
                string mutationTag = part.IsMutationPart ? " [MUTANT]" : "";
                report.AppendLine($"  {part}{mutationTag}");
            }
            
            return report.ToString();
        }
    }
}
