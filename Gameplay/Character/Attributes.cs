// Gameplay/Character/Attributes.cs
// Attribute system - base stats that affect everything and gate mutations

using System;
using System.Collections.Generic;
using MyRPG.Data;

namespace MyRPG.Gameplay.Character
{
    public class Attributes
    {
        // Base attributes (start at 5, range 1-20)
        private Dictionary<AttributeType, int> _values = new Dictionary<AttributeType, int>();
        
        // Constants
        public const int MIN_VALUE = 1;
        public const int MAX_VALUE = 20;
        public const int STARTING_VALUE = 5;
        
        public Attributes()
        {
            // Initialize all attributes to starting value
            foreach (AttributeType attr in Enum.GetValues(typeof(AttributeType)))
            {
                _values[attr] = STARTING_VALUE;
            }
        }
        
        // ============================================
        // GET / SET
        // ============================================
        
        public int Get(AttributeType type)
        {
            return _values.TryGetValue(type, out int val) ? val : STARTING_VALUE;
        }
        
        public void Set(AttributeType type, int value)
        {
            _values[type] = Math.Clamp(value, MIN_VALUE, MAX_VALUE);
        }
        
        public void Increase(AttributeType type, int amount = 1)
        {
            Set(type, Get(type) + amount);
        }
        
        // Quick accessors
        public int STR => Get(AttributeType.STR);
        public int AGI => Get(AttributeType.AGI);
        public int END => Get(AttributeType.END);
        public int INT => Get(AttributeType.INT);
        public int PER => Get(AttributeType.PER);
        public int WIL => Get(AttributeType.WIL);
        
        // ============================================
        // STAT BONUSES (calculated from attributes)
        // ============================================
        
        // --- COMBAT BONUSES ---
        
        /// <summary>
        /// Bonus to melee damage (STR)
        /// </summary>
        public float MeleeDamageBonus => (STR - 5) * 0.1f; // +10% per point above 5
        
        /// <summary>
        /// Bonus to carry weight (STR)
        /// </summary>
        public float CarryWeightBonus => STR * 5f; // 5 kg per STR
        
        /// <summary>
        /// Bonus to movement speed (AGI)
        /// </summary>
        public float SpeedBonus => (AGI - 5) * 0.05f; // +5% per point above 5
        
        /// <summary>
        /// Dodge chance bonus (AGI)
        /// </summary>
        public float DodgeBonus => AGI * 0.02f; // 2% per AGI
        
        /// <summary>
        /// Bonus to max health (END)
        /// </summary>
        public float HealthBonus => (END - 5) * 10f; // +10 HP per point above 5
        
        /// <summary>
        /// Damage resistance (END)
        /// </summary>
        public float ResistanceBonus => END * 0.005f; // 0.5% per END
        
        /// <summary>
        /// Sight range bonus (PER)
        /// </summary>
        public float SightRangeBonus => (PER - 5) * 1f; // +1 tile per point above 5
        
        /// <summary>
        /// Accuracy bonus (PER)
        /// </summary>
        public float AccuracyBonus => PER * 0.02f; // 2% per PER
        
        /// <summary>
        /// Mental resistance (WIL) - resist panic, mind control
        /// </summary>
        public float MentalResistance => WIL * 0.05f; // 5% per WIL
        
        // --- SURVIVAL BONUSES ---
        
        /// <summary>
        /// Building/construction speed (STR) - how fast you place structures
        /// </summary>
        public float BuildingSpeedBonus => (STR - 5) * 0.08f; // +8% per point above 5
        
        /// <summary>
        /// Mining/chopping speed (STR) - resource extraction
        /// </summary>
        public float MiningSpeedBonus => (STR - 5) * 0.1f; // +10% per point above 5
        
        /// <summary>
        /// Crafting speed (AGI) - dexterity for handwork
        /// </summary>
        public float CraftingSpeedBonus => (AGI - 5) * 0.08f; // +8% per point above 5
        
        /// <summary>
        /// Planting/harvesting speed (AGI)
        /// </summary>
        public float PlantingSpeedBonus => (AGI - 5) * 0.1f; // +10% per point above 5
        
        /// <summary>
        /// Stamina/work endurance (END) - how long before exhaustion
        /// </summary>
        public float WorkEnduranceBonus => (END - 5) * 0.1f; // +10% per point above 5
        
        /// <summary>
        /// Environmental resistance (END) - cold, heat, radiation
        /// </summary>
        public float EnvironmentalResistBonus => END * 0.03f; // 3% per END
        
        // --- SCIENCE/CRAFTING BONUSES ---
        
        /// <summary>
        /// Research speed bonus (INT)
        /// </summary>
        public float ResearchBonus => (INT - 5) * 0.1f; // +10% per point above 5
        
        /// <summary>
        /// Crafting quality bonus (INT) - chance for better results
        /// </summary>
        public float CraftingQualityBonus => (INT - 5) * 0.05f; // +5% per point above 5
        
        /// <summary>
        /// Building quality bonus (INT) - structures have more HP/durability
        /// </summary>
        public float BuildingQualityBonus => (INT - 5) * 0.05f; // +5% per point above 5
        
        /// <summary>
        /// Medical effectiveness (INT) - healing items work better
        /// </summary>
        public float MedicalBonus => (INT - 5) * 0.1f; // +10% per point above 5
        
        /// <summary>
        /// Cooking quality (INT) - food gives more nutrition
        /// </summary>
        public float CookingQualityBonus => (INT - 5) * 0.08f; // +8% per point above 5
        
        // --- PERCEPTION BONUSES ---
        
        /// <summary>
        /// Foraging/scavenging success rate (PER)
        /// </summary>
        public float ForagingBonus => (PER - 5) * 0.1f; // +10% per point above 5
        
        /// <summary>
        /// Resource detection range (PER) - spot hidden items/resources
        /// </summary>
        public float ResourceDetectionBonus => (PER - 5) * 0.5f; // +0.5 tiles per point above 5
        
        /// <summary>
        /// Trap detection (PER)
        /// </summary>
        public float TrapDetectionBonus => PER * 0.05f; // 5% per PER
        
        // --- WILLPOWER BONUSES ---
        
        /// <summary>
        /// Dark science affinity (WIL)
        /// </summary>
        public float DarkScienceBonus => (WIL - 5) * 0.1f; // +10% per point above 5
        
        /// <summary>
        /// Plant growth bonus (WIL) - connection to life energy
        /// </summary>
        public float PlantGrowthBonus => (WIL - 5) * 0.1f; // +10% per point above 5
        
        /// <summary>
        /// Animal taming success (WIL)
        /// </summary>
        public float TamingBonus => (WIL - 5) * 0.08f; // +8% per point above 5
        
        /// <summary>
        /// Sanity/stress resistance (WIL)
        /// </summary>
        public float StressResistBonus => WIL * 0.04f; // 4% per WIL
        
        // ============================================
        // REQUIREMENT CHECKING
        // ============================================
        
        /// <summary>
        /// Check if attributes meet a set of requirements
        /// </summary>
        public bool MeetsRequirements(Dictionary<AttributeType, int> requirements)
        {
            if (requirements == null) return true;
            
            foreach (var req in requirements)
            {
                if (Get(req.Key) < req.Value)
                {
                    return false;
                }
            }
            return true;
        }
        
        /// <summary>
        /// Check if a single attribute requirement is met
        /// </summary>
        public bool MeetsRequirement(AttributeType type, int minValue)
        {
            return Get(type) >= minValue;
        }
        
        // ============================================
        // RANDOMIZATION (for character creation)
        // ============================================
        
        /// <summary>
        /// Randomize attributes with a point-buy style variance
        /// Total points stay roughly the same, but distribution varies
        /// </summary>
        public void Randomize(Random random = null)
        {
            random ??= new Random();
            
            // Start all at 5 (total = 30 points)
            foreach (AttributeType attr in Enum.GetValues(typeof(AttributeType)))
            {
                _values[attr] = STARTING_VALUE;
            }
            
            // Do some random swaps: take from one, give to another
            int swaps = random.Next(3, 7); // 3-6 swaps
            var attrList = new List<AttributeType>((AttributeType[])Enum.GetValues(typeof(AttributeType)));
            
            for (int i = 0; i < swaps; i++)
            {
                var from = attrList[random.Next(attrList.Count)];
                var to = attrList[random.Next(attrList.Count)];
                
                if (from != to && Get(from) > MIN_VALUE + 1 && Get(to) < MAX_VALUE - 1)
                {
                    _values[from]--;
                    _values[to]++;
                }
            }
        }
        
        // ============================================
        // DISPLAY
        // ============================================
        
        public string GetDisplayString()
        {
            return $"STR:{STR} AGI:{AGI} END:{END} INT:{INT} PER:{PER} WIL:{WIL}";
        }
        
        public string GetDetailedReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== ATTRIBUTES ===");
            report.AppendLine($"STR: {STR}");
            report.AppendLine($"  Combat: Melee +{MeleeDamageBonus:P0}");
            report.AppendLine($"  Survival: Build Speed +{BuildingSpeedBonus:P0}, Mining +{MiningSpeedBonus:P0}, Carry +{CarryWeightBonus}kg");
            report.AppendLine($"AGI: {AGI}");
            report.AppendLine($"  Combat: Speed +{SpeedBonus:P0}, Dodge {DodgeBonus:P0}");
            report.AppendLine($"  Survival: Craft Speed +{CraftingSpeedBonus:P0}, Plant Speed +{PlantingSpeedBonus:P0}");
            report.AppendLine($"END: {END}");
            report.AppendLine($"  Combat: Health +{HealthBonus}, Resist {ResistanceBonus:P0}");
            report.AppendLine($"  Survival: Work Endurance +{WorkEnduranceBonus:P0}, Env Resist +{EnvironmentalResistBonus:P0}");
            report.AppendLine($"INT: {INT}");
            report.AppendLine($"  Science: Research +{ResearchBonus:P0}");
            report.AppendLine($"  Quality: Craft +{CraftingQualityBonus:P0}, Build +{BuildingQualityBonus:P0}, Medical +{MedicalBonus:P0}");
            report.AppendLine($"PER: {PER}");
            report.AppendLine($"  Combat: Sight +{SightRangeBonus}, Accuracy +{AccuracyBonus:P0}");
            report.AppendLine($"  Survival: Forage +{ForagingBonus:P0}, Resource Detect +{ResourceDetectionBonus}, Trap Detect +{TrapDetectionBonus:P0}");
            report.AppendLine($"WIL: {WIL}");
            report.AppendLine($"  Mental: Resist {MentalResistance:P0}, Stress Resist +{StressResistBonus:P0}");
            report.AppendLine($"  Nature: Dark Science +{DarkScienceBonus:P0}, Plant Growth +{PlantGrowthBonus:P0}, Taming +{TamingBonus:P0}");
            return report.ToString();
        }
        
        /// <summary>
        /// Get attribute name for display
        /// </summary>
        public static string GetAttributeName(AttributeType type)
        {
            return type switch
            {
                AttributeType.STR => "Strength",
                AttributeType.AGI => "Agility",
                AttributeType.END => "Endurance",
                AttributeType.INT => "Intelligence",
                AttributeType.PER => "Perception",
                AttributeType.WIL => "Willpower",
                _ => type.ToString()
            };
        }
        
        /// <summary>
        /// Get attribute description (short)
        /// </summary>
        public static string GetAttributeDescription(AttributeType type)
        {
            return type switch
            {
                AttributeType.STR => "Melee, building speed, mining, carry weight",
                AttributeType.AGI => "Move speed, dodge, crafting speed, planting",
                AttributeType.END => "Health, resistance, work endurance, environment",
                AttributeType.INT => "Research, craft quality, build quality, medical",
                AttributeType.PER => "Sight, accuracy, foraging, trap detection",
                AttributeType.WIL => "Mental resist, dark science, plant growth, taming",
                _ => ""
            };
        }
    }
}
