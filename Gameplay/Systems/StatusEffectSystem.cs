// Gameplay/Systems/StatusEffectSystem.cs
// Manages status effects, durations, stacking, and effect chains

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;

namespace MyRPG.Gameplay.Systems
{
    // ============================================
    // STATUS EFFECT INSTANCE
    // ============================================
    
    public class StatusEffect
    {
        public StatusEffectType Type { get; set; }
        public string SourceId { get; set; }            // What caused this effect
        public float Duration { get; set; }             // Remaining duration (seconds or turns)
        public int Stacks { get; set; } = 1;            // For stackable effects
        public float Intensity { get; set; } = 1.0f;    // Severity multiplier
        
        // Timing
        public bool IsPermanent { get; set; } = false;  // Doesn't expire
        public bool UseTurns { get; set; } = false;     // True = turn-based, False = real-time
        
        public StatusEffect(StatusEffectType type, float duration, bool useTurns = false)
        {
            Type = type;
            Duration = duration;
            UseTurns = useTurns;
        }
        
        public override string ToString()
        {
            string stackStr = Stacks > 1 ? $" x{Stacks}" : "";
            string durationStr = IsPermanent ? "" : $" ({Duration:F1}{(UseTurns ? "t" : "s")})";
            return $"{Type}{stackStr}{durationStr}";
        }
    }
    
    // ============================================
    // STATUS EFFECT DEFINITION (static data)
    // ============================================
    
    public class StatusEffectDefinition
    {
        public StatusEffectType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public StatusCategory Category { get; set; }
        public StatusStackBehavior StackBehavior { get; set; }
        public int MaxStacks { get; set; } = 1;
        
        // Stat modifications
        public float SpeedModifier { get; set; } = 1.0f;
        public float DamageModifier { get; set; } = 1.0f;
        public float AccuracyModifier { get; set; } = 1.0f;
        public float DamagePerTick { get; set; } = 0f;      // DoT effects
        public float HealPerTick { get; set; } = 0f;        // HoT effects
        
        // Visual/Audio
        public string IconPath { get; set; }
        public string ParticleEffect { get; set; }
    }
    
    // ============================================
    // EFFECT CHAIN RULE
    // ============================================
    
    public class EffectChainRule
    {
        public StatusEffectType Existing { get; set; }      // Effect already on target
        public StatusEffectType Trigger { get; set; }       // Incoming effect
        public StatusEffectType Result { get; set; }        // New effect created
        public float ResultDuration { get; set; }           // Duration of result
        public bool ConsumeExisting { get; set; } = false;  // Remove existing effect?
        public bool ConsumeTrigger { get; set; } = false;   // Don't apply trigger?
        
        public EffectChainRule(StatusEffectType existing, StatusEffectType trigger, StatusEffectType result, float duration)
        {
            Existing = existing;
            Trigger = trigger;
            Result = result;
            ResultDuration = duration;
        }
    }
    
    // ============================================
    // STATUS EFFECT SYSTEM
    // ============================================
    
    public class StatusEffectSystem
    {
        // All effect definitions (static data)
        private Dictionary<StatusEffectType, StatusEffectDefinition> _definitions;
        
        // All chain rules
        private List<EffectChainRule> _chainRules;
        
        public StatusEffectSystem()
        {
            InitializeDefinitions();
            InitializeChainRules();
        }
        
        // ============================================
        // APPLY EFFECT
        // ============================================
        
        /// <summary>
        /// Apply an effect to a target's effect list. Handles stacking and chains.
        /// Returns list of all effects that were applied (including chain reactions).
        /// </summary>
        public List<StatusEffect> ApplyEffect(
            List<StatusEffect> targetEffects, 
            StatusEffectType effectType, 
            float duration,
            bool useTurns = false,
            string sourceId = null)
        {
            var appliedEffects = new List<StatusEffect>();
            
            // Check for chain reactions BEFORE applying
            var chainResults = CheckChainReactions(targetEffects, effectType);
            
            // Apply chain results
            foreach (var chain in chainResults)
            {
                if (chain.ConsumeExisting)
                {
                    targetEffects.RemoveAll(e => e.Type == chain.Existing);
                }
                
                var chainEffect = new StatusEffect(chain.Result, chain.ResultDuration, useTurns);
                targetEffects.Add(chainEffect);
                appliedEffects.Add(chainEffect);
                
                System.Diagnostics.Debug.WriteLine($">>> CHAIN REACTION: {chain.Existing} + {chain.Trigger} = {chain.Result}! <<<");
            }
            
            // If trigger was consumed by a chain, don't apply it
            bool triggerConsumed = chainResults.Any(c => c.ConsumeTrigger);
            
            if (!triggerConsumed)
            {
                var newEffect = ApplyOrStackEffect(targetEffects, effectType, duration, useTurns, sourceId);
                if (newEffect != null)
                {
                    appliedEffects.Add(newEffect);
                }
            }
            
            return appliedEffects;
        }
        
        private StatusEffect ApplyOrStackEffect(
            List<StatusEffect> targetEffects,
            StatusEffectType effectType,
            float duration,
            bool useTurns,
            string sourceId)
        {
            var definition = GetDefinition(effectType);
            var existing = targetEffects.FirstOrDefault(e => e.Type == effectType);
            
            if (existing != null)
            {
                // Handle stacking based on behavior
                switch (definition.StackBehavior)
                {
                    case StatusStackBehavior.RefreshDuration:
                        existing.Duration = Math.Max(existing.Duration, duration);
                        return null; // No new effect added
                        
                    case StatusStackBehavior.StackIntensity:
                        if (existing.Stacks < definition.MaxStacks)
                        {
                            existing.Stacks++;
                            existing.Intensity += 0.5f; // Each stack adds 50% intensity
                            existing.Duration = Math.Max(existing.Duration, duration);
                        }
                        return null;
                        
                    case StatusStackBehavior.StackDuration:
                        existing.Duration += duration;
                        return null;
                        
                    case StatusStackBehavior.NoStack:
                        return null; // Effect already active, ignore
                }
            }
            
            // No existing effect, create new one
            var newEffect = new StatusEffect(effectType, duration, useTurns)
            {
                SourceId = sourceId
            };
            targetEffects.Add(newEffect);
            
            System.Diagnostics.Debug.WriteLine($">>> APPLIED: {effectType} for {duration}{(useTurns ? " turns" : "s")} <<<");
            
            return newEffect;
        }
        
        // ============================================
        // CHAIN REACTION CHECK
        // ============================================
        
        private List<EffectChainRule> CheckChainReactions(List<StatusEffect> currentEffects, StatusEffectType incoming)
        {
            var triggeredChains = new List<EffectChainRule>();
            
            foreach (var rule in _chainRules)
            {
                if (rule.Trigger == incoming)
                {
                    // Check if target has the existing effect needed for this chain
                    if (currentEffects.Any(e => e.Type == rule.Existing))
                    {
                        triggeredChains.Add(rule);
                    }
                }
            }
            
            return triggeredChains;
        }
        
        // ============================================
        // REMOVE / EXPIRE EFFECTS
        // ============================================
        
        public void RemoveEffect(List<StatusEffect> targetEffects, StatusEffectType effectType)
        {
            targetEffects.RemoveAll(e => e.Type == effectType);
        }
        
        /// <summary>
        /// Update effects for real-time mode. Call each frame with deltaTime.
        /// </summary>
        public List<StatusEffectType> UpdateEffectsRealTime(List<StatusEffect> targetEffects, float deltaTime)
        {
            var expired = new List<StatusEffectType>();
            
            for (int i = targetEffects.Count - 1; i >= 0; i--)
            {
                var effect = targetEffects[i];
                
                if (effect.IsPermanent || effect.UseTurns) continue;
                
                effect.Duration -= deltaTime;
                
                if (effect.Duration <= 0)
                {
                    expired.Add(effect.Type);
                    targetEffects.RemoveAt(i);
                    System.Diagnostics.Debug.WriteLine($">>> EXPIRED: {effect.Type} <<<");
                }
            }
            
            return expired;
        }
        
        /// <summary>
        /// Update effects for turn-based mode. Call at end of turn.
        /// </summary>
        public List<StatusEffectType> UpdateEffectsTurnBased(List<StatusEffect> targetEffects)
        {
            var expired = new List<StatusEffectType>();
            
            for (int i = targetEffects.Count - 1; i >= 0; i--)
            {
                var effect = targetEffects[i];
                
                if (effect.IsPermanent || !effect.UseTurns) continue;
                
                effect.Duration -= 1;
                
                if (effect.Duration <= 0)
                {
                    expired.Add(effect.Type);
                    targetEffects.RemoveAt(i);
                    System.Diagnostics.Debug.WriteLine($">>> EXPIRED (turn): {effect.Type} <<<");
                }
            }
            
            return expired;
        }
        
        // ============================================
        // STAT CALCULATIONS
        // ============================================
        
        public float GetSpeedModifier(List<StatusEffect> effects)
        {
            float modifier = 1.0f;
            
            foreach (var effect in effects)
            {
                var def = GetDefinition(effect.Type);
                float effectMod = def.SpeedModifier;
                
                // Apply intensity scaling
                if (effect.Intensity != 1.0f)
                {
                    effectMod = 1.0f + (effectMod - 1.0f) * effect.Intensity;
                }
                
                modifier *= effectMod;
            }
            
            return Math.Max(0.1f, modifier); // Minimum 10% speed
        }
        
        public float GetDamageModifier(List<StatusEffect> effects)
        {
            float modifier = 1.0f;
            
            foreach (var effect in effects)
            {
                var def = GetDefinition(effect.Type);
                modifier *= def.DamageModifier;
            }
            
            return modifier;
        }
        
        public float GetDamageOverTime(List<StatusEffect> effects)
        {
            float total = 0f;
            
            foreach (var effect in effects)
            {
                var def = GetDefinition(effect.Type);
                total += def.DamagePerTick * effect.Intensity * effect.Stacks;
            }
            
            return total;
        }
        
        // ============================================
        // QUERY HELPERS
        // ============================================
        
        public bool HasEffect(List<StatusEffect> effects, StatusEffectType type)
        {
            return effects.Any(e => e.Type == type);
        }
        
        public bool HasAnyEffect(List<StatusEffect> effects, params StatusEffectType[] types)
        {
            return effects.Any(e => types.Contains(e.Type));
        }
        
        public StatusEffectDefinition GetDefinition(StatusEffectType type)
        {
            return _definitions.TryGetValue(type, out var def) ? def : CreateDefaultDefinition(type);
        }
        
        // ============================================
        // DATA INITIALIZATION
        // ============================================
        
        private void InitializeDefinitions()
        {
            _definitions = new Dictionary<StatusEffectType, StatusEffectDefinition>
            {
                // ELEMENTAL
                [StatusEffectType.Wet] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Wet,
                    Name = "Wet",
                    Description = "Soaked with water. Slowed and vulnerable to electricity.",
                    Category = StatusCategory.Elemental,
                    StackBehavior = StatusStackBehavior.RefreshDuration,
                    SpeedModifier = 0.9f  // 10% slow
                },
                
                [StatusEffectType.Burning] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Burning,
                    Name = "Burning",
                    Description = "On fire! Taking damage over time.",
                    Category = StatusCategory.Elemental,
                    StackBehavior = StatusStackBehavior.StackIntensity,
                    MaxStacks = 3,
                    DamagePerTick = 5f
                },
                
                [StatusEffectType.Frozen] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Frozen,
                    Name = "Frozen",
                    Description = "Encased in ice. Severely slowed.",
                    Category = StatusCategory.Elemental,
                    StackBehavior = StatusStackBehavior.RefreshDuration,
                    SpeedModifier = 0.3f  // 70% slow
                },
                
                [StatusEffectType.Electrified] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Electrified,
                    Name = "Electrified",
                    Description = "Shocked by electricity.",
                    Category = StatusCategory.Elemental,
                    StackBehavior = StatusStackBehavior.RefreshDuration,
                    DamagePerTick = 3f,
                    AccuracyModifier = 0.8f
                },
                
                [StatusEffectType.Oiled] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Oiled,
                    Name = "Oiled",
                    Description = "Covered in oil. Highly flammable!",
                    Category = StatusCategory.Elemental,
                    StackBehavior = StatusStackBehavior.RefreshDuration,
                    SpeedModifier = 0.95f
                },
                
                // PHYSICAL
                [StatusEffectType.Bleeding] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Bleeding,
                    Name = "Bleeding",
                    Description = "Losing blood. Damage over time.",
                    Category = StatusCategory.Physical,
                    StackBehavior = StatusStackBehavior.StackIntensity,
                    MaxStacks = 5,
                    DamagePerTick = 2f
                },
                
                [StatusEffectType.Stunned] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Stunned,
                    Name = "Stunned",
                    Description = "Cannot act!",
                    Category = StatusCategory.Physical,
                    StackBehavior = StatusStackBehavior.RefreshDuration,
                    SpeedModifier = 0f  // Can't move
                },
                
                [StatusEffectType.Slowed] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Slowed,
                    Name = "Slowed",
                    Description = "Movement reduced.",
                    Category = StatusCategory.Physical,
                    StackBehavior = StatusStackBehavior.StackIntensity,
                    MaxStacks = 3,
                    SpeedModifier = 0.7f
                },
                
                [StatusEffectType.Exhausted] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Exhausted,
                    Name = "Exhausted",
                    Description = "Tired. Reduced effectiveness.",
                    Category = StatusCategory.Physical,
                    StackBehavior = StatusStackBehavior.NoStack,
                    SpeedModifier = 0.8f,
                    DamageModifier = 0.8f,
                    AccuracyModifier = 0.9f
                },
                
                [StatusEffectType.Poisoned] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Poisoned,
                    Name = "Poisoned",
                    Description = "Toxins in the body. Damage over time.",
                    Category = StatusCategory.Physical,
                    StackBehavior = StatusStackBehavior.StackIntensity,
                    MaxStacks = 3,
                    DamagePerTick = 3f
                },
                
                // MENTAL
                [StatusEffectType.Panicked] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Panicked,
                    Name = "Panicked",
                    Description = "Terrified! May flee or act erratically.",
                    Category = StatusCategory.Mental,
                    StackBehavior = StatusStackBehavior.RefreshDuration,
                    AccuracyModifier = 0.6f
                },
                
                [StatusEffectType.Focused] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Focused,
                    Name = "Focused",
                    Description = "Concentrated. Improved accuracy.",
                    Category = StatusCategory.Buff,
                    StackBehavior = StatusStackBehavior.RefreshDuration,
                    AccuracyModifier = 1.2f,
                    DamageModifier = 1.1f
                },
                
                [StatusEffectType.Berserk] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Berserk,
                    Name = "Berserk",
                    Description = "Uncontrollable rage! More damage, less defense.",
                    Category = StatusCategory.Mental,
                    StackBehavior = StatusStackBehavior.NoStack,
                    DamageModifier = 1.5f
                },
                
                [StatusEffectType.Dazed] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Dazed,
                    Name = "Dazed",
                    Description = "Confused and disoriented.",
                    Category = StatusCategory.Mental,
                    StackBehavior = StatusStackBehavior.RefreshDuration,
                    AccuracyModifier = 0.7f,
                    SpeedModifier = 0.85f
                },
                
                // ENVIRONMENTAL
                [StatusEffectType.Hypothermia] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Hypothermia,
                    Name = "Hypothermia",
                    Description = "Dangerously cold. Slowed and taking damage.",
                    Category = StatusCategory.Environmental,
                    StackBehavior = StatusStackBehavior.StackIntensity,
                    MaxStacks = 3,
                    SpeedModifier = 0.6f,
                    DamagePerTick = 2f
                },
                
                [StatusEffectType.Heatstroke] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Heatstroke,
                    Name = "Heatstroke",
                    Description = "Overheating. Exhausted and taking damage.",
                    Category = StatusCategory.Environmental,
                    StackBehavior = StatusStackBehavior.StackIntensity,
                    MaxStacks = 3,
                    SpeedModifier = 0.7f,
                    DamagePerTick = 1.5f
                },
                
                [StatusEffectType.Irradiated] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.Irradiated,
                    Name = "Irradiated",
                    Description = "Exposed to radiation. Long-term health effects.",
                    Category = StatusCategory.Environmental,
                    StackBehavior = StatusStackBehavior.StackIntensity,
                    MaxStacks = 10,
                    DamagePerTick = 0.5f
                },
                
                // COMBAT
                [StatusEffectType.InCover] = new StatusEffectDefinition
                {
                    Type = StatusEffectType.InCover,
                    Name = "In Cover",
                    Description = "Protected from ranged attacks.",
                    Category = StatusCategory.Combat,
                    StackBehavior = StatusStackBehavior.NoStack
                }
            };
        }
        
        private void InitializeChainRules()
        {
            _chainRules = new List<EffectChainRule>
            {
                // Wet + Electricity = Stunned
                new EffectChainRule(StatusEffectType.Wet, StatusEffectType.Electrified, StatusEffectType.Stunned, 3f)
                {
                    ConsumeExisting = true,  // Remove Wet
                    ConsumeTrigger = false   // Still apply Electrified
                },
                
                // Wet + Cold = Frozen
                new EffectChainRule(StatusEffectType.Wet, StatusEffectType.Frozen, StatusEffectType.Frozen, 5f)
                {
                    ConsumeExisting = true
                },
                
                // Oiled + Fire = Intense Burning
                new EffectChainRule(StatusEffectType.Oiled, StatusEffectType.Burning, StatusEffectType.Burning, 8f)
                {
                    ConsumeExisting = true
                    // The burning will have extra intensity due to the chain
                },
                
                // Wet + Fire = Removes both (steam)
                new EffectChainRule(StatusEffectType.Wet, StatusEffectType.Burning, StatusEffectType.Dazed, 2f)
                {
                    ConsumeExisting = true,  // Remove Wet
                    ConsumeTrigger = true    // Don't apply Burning
                },
                
                // Frozen + Fire = Wet (thaw)
                new EffectChainRule(StatusEffectType.Frozen, StatusEffectType.Burning, StatusEffectType.Wet, 5f)
                {
                    ConsumeExisting = true,  // Remove Frozen
                    ConsumeTrigger = true    // Don't apply Burning
                },
                
                // Wet + Hypothermia = Worse Hypothermia
                new EffectChainRule(StatusEffectType.Wet, StatusEffectType.Hypothermia, StatusEffectType.Hypothermia, 10f)
                {
                    ConsumeExisting = false  // Keep Wet, stack Hypothermia intensity
                }
            };
        }
        
        private StatusEffectDefinition CreateDefaultDefinition(StatusEffectType type)
        {
            return new StatusEffectDefinition
            {
                Type = type,
                Name = type.ToString(),
                Description = "Unknown effect.",
                Category = StatusCategory.Debuff,
                StackBehavior = StatusStackBehavior.RefreshDuration
            };
        }
    }
}
