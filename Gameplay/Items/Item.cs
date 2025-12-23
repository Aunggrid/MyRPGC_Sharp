// Gameplay/Items/Item.cs
// Item system with definitions and instances

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;

namespace MyRPG.Gameplay.Items
{
    // ============================================
    // ITEM INSTANCE (actual item in inventory/world)
    // ============================================
    
    public class Item
    {
        // Identity
        public string Id { get; private set; }
        public string ItemDefId { get; private set; }       // Reference to definition
        public ItemDefinition Definition { get; private set; }
        
        // Instance-specific data
        public int StackCount { get; set; } = 1;
        public ItemQuality Quality { get; set; } = ItemQuality.Normal;
        public float Durability { get; set; } = 100f;       // 0-100, some items degrade
        public float MaxDurability { get; set; } = 100f;
        
        // Position (if dropped in world)
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public bool IsInWorld { get; set; } = false;
        
        // Derived properties
        public string Name => Definition?.Name ?? "Unknown";
        public string Description => Definition?.Description ?? "";
        public ItemCategory Category => Definition?.Category ?? ItemCategory.Junk;
        public bool IsStackable => Definition?.IsStackable ?? false;
        public int MaxStack => Definition?.MaxStackSize ?? 1;
        public float Weight => (Definition?.Weight ?? 0f) * StackCount;
        public int Value => (int)((Definition?.BaseValue ?? 0) * GetQualityMultiplier());
        
        // Combat bonuses from equipment
        public int ActionPointBonus => Definition?.ActionPointBonus ?? 0;
        public int MovementPointBonus => Definition?.MovementPointBonus ?? 0;
        public int EsperPointBonus => Definition?.EsperPointBonus ?? 0;
        public float EsperPowerBonus => Definition?.EsperPowerBonus ?? 0f;
        
        // Constructor
        public Item(string itemDefId, ItemQuality quality = ItemQuality.Normal, int count = 1)
        {
            Id = Guid.NewGuid().ToString().Substring(0, 8);
            ItemDefId = itemDefId;
            Definition = ItemDatabase.Get(itemDefId);
            Quality = quality;
            StackCount = count;
            
            if (Definition != null)
            {
                MaxDurability = Definition.BaseDurability;
                Durability = MaxDurability;
            }
        }
        
        // ============================================
        // QUALITY EFFECTS
        // ============================================
        
        public float GetQualityMultiplier()
        {
            return Quality switch
            {
                ItemQuality.Broken => 0.25f,    // Barely functional
                ItemQuality.Poor => 0.50f,      // Subpar
                ItemQuality.Normal => 1.0f,     // Standard
                ItemQuality.Good => 1.15f,      // Noticeable upgrade
                ItemQuality.Excellent => 1.30f, // Strong upgrade
                ItemQuality.Masterwork => 1.50f, // Top tier - worth seeking
                _ => 1.0f
            };
        }
        
        public float GetEffectiveDamage()
        {
            if (Definition == null) return 0;
            float baseDmg = Definition.Damage;
            float qualityMod = GetQualityMultiplier();
            float durabilityMod = Durability / MaxDurability;
            return baseDmg * qualityMod * durabilityMod;
        }
        
        public float GetEffectiveArmor()
        {
            if (Definition == null) return 0;
            float baseArmor = Definition.Armor;
            float qualityMod = GetQualityMultiplier();
            float durabilityMod = Durability / MaxDurability;
            return baseArmor * qualityMod * durabilityMod;
        }
        
        // ============================================
        // STACKING
        // ============================================
        
        public bool CanStackWith(Item other)
        {
            if (other == null) return false;
            if (!IsStackable) return false;
            if (ItemDefId != other.ItemDefId) return false;
            if (Quality != other.Quality) return false;
            return StackCount + other.StackCount <= MaxStack;
        }
        
        public int AddToStack(int amount)
        {
            int canAdd = MaxStack - StackCount;
            int toAdd = Math.Min(amount, canAdd);
            StackCount += toAdd;
            return amount - toAdd; // Return remainder
        }
        
        public Item Split(int amount)
        {
            if (amount >= StackCount) return null;
            if (amount <= 0) return null;
            
            StackCount -= amount;
            return new Item(ItemDefId, Quality, amount);
        }
        
        // ============================================
        // DURABILITY
        // ============================================
        
        public void TakeDurabilityDamage(float amount)
        {
            Durability = Math.Max(0, Durability - amount);
            
            if (Durability <= 0)
            {
                Quality = ItemQuality.Broken;
            }
        }
        
        public void Repair(float amount)
        {
            Durability = Math.Min(MaxDurability, Durability + amount);
            
            if (Quality == ItemQuality.Broken && Durability > 10)
            {
                Quality = ItemQuality.Poor;
            }
        }
        
        public bool IsBroken => Durability <= 0 || Quality == ItemQuality.Broken;
        
        // ============================================
        // DISPLAY
        // ============================================
        
        public string GetDisplayName()
        {
            string qualityPrefix = Quality switch
            {
                ItemQuality.Broken => "[Broken] ",
                ItemQuality.Poor => "[Poor] ",
                ItemQuality.Good => "[Good] ",
                ItemQuality.Excellent => "[Excellent] ",
                ItemQuality.Masterwork => "[Masterwork] ",
                _ => ""
            };
            
            string stackSuffix = StackCount > 1 ? $" x{StackCount}" : "";
            
            return $"{qualityPrefix}{Name}{stackSuffix}";
        }
        
        // Get stats summary including quality modifiers
        public string GetStatsDisplay()
        {
            if (Definition == null) return "";
            
            var stats = new List<string>();
            float qualityMod = GetQualityMultiplier();
            
            // Weapon stats
            if (Definition.Damage > 0)
            {
                float effectiveDmg = GetEffectiveDamage();
                if (Quality != ItemQuality.Normal)
                    stats.Add($"Damage: {effectiveDmg:F1} ({Definition.Damage:F0} x{qualityMod:F2})");
                else
                    stats.Add($"Damage: {effectiveDmg:F1}");
            }
            
            if (Definition.Range > 1)
                stats.Add($"Range: {Definition.Range}");
            
            if (Definition.Accuracy != 0)
            {
                float effectiveAcc = Definition.Accuracy * qualityMod;
                stats.Add($"Accuracy: {(effectiveAcc >= 0 ? "+" : "")}{effectiveAcc:F0}%");
            }
            
            // Armor stats
            if (Definition.Armor > 0)
            {
                float effectiveArmor = GetEffectiveArmor();
                if (Quality != ItemQuality.Normal)
                    stats.Add($"Armor: {effectiveArmor:F1} ({Definition.Armor:F0} x{qualityMod:F2})");
                else
                    stats.Add($"Armor: {effectiveArmor:F1}");
            }
            
            // Combat point bonuses
            if (Definition.ActionPointBonus != 0)
                stats.Add($"AP: {(Definition.ActionPointBonus >= 0 ? "+" : "")}{Definition.ActionPointBonus}");
            if (Definition.MovementPointBonus != 0)
                stats.Add($"MP: {(Definition.MovementPointBonus >= 0 ? "+" : "")}{Definition.MovementPointBonus}");
            if (Definition.EsperPointBonus != 0)
                stats.Add($"EP: {(Definition.EsperPointBonus >= 0 ? "+" : "")}{Definition.EsperPointBonus}");
            
            // Weapon properties
            if (Definition.WeaponLength != WeaponLength.None)
                stats.Add($"Length: {Definition.WeaponLength}");
            if (Definition.HandsRequired == 2)
                stats.Add("Two-Handed");
            else if (Definition.CanUseTwoHand && Definition.HandsRequired == 1)
                stats.Add("Versatile");
            
            return string.Join(" | ", stats);
        }
        
        // Get effective accuracy with quality modifier
        public float GetEffectiveAccuracy()
        {
            if (Definition == null) return 0;
            return Definition.Accuracy * GetQualityMultiplier();
        }
        
        // Get weapon length
        public WeaponLength GetWeaponLength()
        {
            return Definition?.WeaponLength ?? WeaponLength.None;
        }
        
        // Check if this weapon can be used with different grips
        public bool IsVersatile => Definition != null && 
                                   Definition.HandsRequired == 1 && 
                                   Definition.CanUseTwoHand;
        
        public override string ToString()
        {
            return GetDisplayName();
        }
    }
    
    // ============================================
    // ITEM DEFINITION (static data)
    // ============================================
    
    public class ItemDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ItemCategory Category { get; set; }
        public ItemRarity Rarity { get; set; } = ItemRarity.Common;
        
        // Stacking
        public bool IsStackable { get; set; } = false;
        public int MaxStackSize { get; set; } = 1;
        
        // Physical
        public float Weight { get; set; } = 0.1f;           // kg
        public int BaseValue { get; set; } = 1;             // Base sell price
        public float BaseDurability { get; set; } = 100f;
        
        // Equipment
        public EquipSlot EquipSlot { get; set; } = EquipSlot.None;
        public WeaponType WeaponType { get; set; } = WeaponType.Unarmed;
        public WeaponLength WeaponLength { get; set; } = WeaponLength.None;  // NEW: Short/Medium/Long
        public ArmorSlot ArmorSlot { get; set; } = ArmorSlot.Torso;
        
        // Combat stats (BASE values - modified by quality)
        public float Damage { get; set; } = 0f;
        public float AttackSpeed { get; set; } = 1f;        // Attacks per turn
        public int Range { get; set; } = 1;                 // Tiles
        public float Accuracy { get; set; } = 0f;           // Bonus to hit
        public float Armor { get; set; } = 0f;              // Damage reduction
        public float ArmorPenetration { get; set; } = 0f;   // Ignores this % of armor (0.5 = 50%)
        
        // Combat point bonuses (from equipment)
        public int ActionPointBonus { get; set; } = 0;      // +AP from tactical gear, gloves
        public int MovementPointBonus { get; set; } = 0;    // +MP from boots, leg armor
        public int EsperPointBonus { get; set; } = 0;       // +EP from psionic amplifiers
        public float EsperPowerBonus { get; set; } = 0f;    // +% esper effectiveness
        
        // Grip and wielding options
        public int HandsRequired { get; set; } = 1;         // Default hands needed (1 or 2)
        public bool CanUseOneHand { get; set; } = true;     // Can be used with one hand
        public bool CanUseTwoHand { get; set; } = true;     // Can be used with two hands
        public float TwoHandDamageBonus { get; set; } = 0.25f;  // +25% damage when two-handing (STR adds to this)
        public float DualWieldPenalty { get; set; } = 0.15f;    // 15% accuracy/damage penalty per extra weapon
        public bool IsTwoHanded => HandsRequired >= 2 && !CanUseOneHand;
        
        // Consumable effects
        public ConsumableType ConsumableType { get; set; } = ConsumableType.Food;
        public float HungerRestore { get; set; } = 0f;
        public float ThirstRestore { get; set; } = 0f;
        public float HealthRestore { get; set; } = 0f;          // Flat HP restore
        public float HealPercent { get; set; } = 0f;            // Percentage of MaxHP to heal (e.g., 15 = 15%)
        public float RadiationRemove { get; set; } = 0f;
        public StatusEffectType? AppliesEffect { get; set; } = null;
        public float EffectDuration { get; set; } = 0f;
        
        // Medical - Body part healing
        public bool IsMedical { get; set; } = false;
        public float BodyPartHealAmount { get; set; } = 0f;      // HP restored to body part
        public bool CanHealBleeding { get; set; } = false;
        public bool CanHealInfection { get; set; } = false;
        public bool CanHealFracture { get; set; } = false;
        public BodyPartType? TargetBodyPartType { get; set; } = null;  // Specific body part type (null = any)
        
        // Ammo
        public string RequiresAmmo { get; set; } = null;    // Item ID of ammo needed
        public int AmmoPerShot { get; set; } = 1;
        
        // Crafting
        public bool IsCraftingMaterial { get; set; } = false;
    }
    
    // ============================================
    // ITEM DATABASE (all item definitions)
    // ============================================
    
    public static class ItemDatabase
    {
        private static Dictionary<string, ItemDefinition> _items;
        private static bool _initialized = false;
        
        public static void Initialize()
        {
            if (_initialized) return;
            
            _items = new Dictionary<string, ItemDefinition>();
            
            // ========== WEAPONS - MELEE ==========
            
            AddItem(new ItemDefinition
            {
                Id = "knife_rusty",
                Name = "Rusty Knife",
                Description = "A crude blade, better than nothing.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Common,
                Weight = 0.3f,
                BaseValue = 5,
                EquipSlot = EquipSlot.MainHand,
                WeaponType = WeaponType.Knife,
                WeaponLength = WeaponLength.Short,
                Damage = 7f,    // Weak but not useless
                AttackSpeed = 1.2f,
                Range = 1,
                Accuracy = -2f, // Slight penalty - it's rusty
                HandsRequired = 1,
                CanUseOneHand = true,
                CanUseTwoHand = false,
                DualWieldPenalty = 0.10f  // Knives are easy to dual wield
            });
            
            AddItem(new ItemDefinition
            {
                Id = "knife_combat",
                Name = "Combat Knife",
                Description = "Military-grade blade. Fast and deadly.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.4f,
                BaseValue = 25,
                EquipSlot = EquipSlot.MainHand,
                WeaponType = WeaponType.Knife,
                WeaponLength = WeaponLength.Short,
                Damage = 11f,   // Good upgrade from rusty
                AttackSpeed = 1.3f,
                Range = 1,
                Accuracy = 3f,
                HandsRequired = 1,
                CanUseOneHand = true,
                CanUseTwoHand = false,
                DualWieldPenalty = 0.10f
            });
            
            AddItem(new ItemDefinition
            {
                Id = "machete",
                Name = "Machete",
                Description = "Heavy blade good for clearing brush... and enemies. Versatile grip.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Common,
                Weight = 0.8f,
                BaseValue = 15,
                EquipSlot = EquipSlot.MainHand,
                WeaponType = WeaponType.Sword,
                WeaponLength = WeaponLength.Medium,
                Damage = 13f,   // Solid early weapon
                AttackSpeed = 0.9f,
                Range = 1,
                HandsRequired = 1,
                CanUseOneHand = true,
                CanUseTwoHand = true,
                TwoHandDamageBonus = 0.20f,
                DualWieldPenalty = 0.15f
            });
            
            AddItem(new ItemDefinition
            {
                Id = "pipe_club",
                Name = "Lead Pipe",
                Description = "Heavy metal pipe. Simple but effective.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Common,
                Weight = 1.5f,
                BaseValue = 3,
                EquipSlot = EquipSlot.MainHand,
                WeaponType = WeaponType.Club,
                WeaponLength = WeaponLength.Medium,
                Damage = 11f,   // Decent damage
                AttackSpeed = 0.8f,
                Range = 1,
                Accuracy = -2f, // Heavy and awkward
                HandsRequired = 1,
                CanUseOneHand = true,
                CanUseTwoHand = true,
                TwoHandDamageBonus = 0.15f,
                DualWieldPenalty = 0.20f  // Awkward to dual wield
            });
            
            AddItem(new ItemDefinition
            {
                Id = "axe_fire",
                Name = "Fire Axe",
                Description = "Emergency fire axe. Heavy and devastating. Can be used one-handed with penalty.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Uncommon,
                Weight = 2.0f,
                BaseValue = 30,
                EquipSlot = EquipSlot.TwoHand,
                WeaponType = WeaponType.Axe,
                WeaponLength = WeaponLength.Medium,
                Damage = 22f,
                AttackSpeed = 0.7f,
                Range = 1,
                HandsRequired = 2,
                CanUseOneHand = true,  // Can be one-handed but with penalty
                CanUseTwoHand = true,
                TwoHandDamageBonus = 0.30f,
                DualWieldPenalty = 0.25f  // Heavy, hard to dual wield
            });
            
            AddItem(new ItemDefinition
            {
                Id = "spear_makeshift",
                Name = "Makeshift Spear",
                Description = "Sharpened stick with a knife tied to it. Has reach. Two-handed for best results.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Common,
                Weight = 1.2f,
                BaseValue = 8,
                EquipSlot = EquipSlot.TwoHand,
                WeaponType = WeaponType.Spear,
                WeaponLength = WeaponLength.Long,
                Damage = 10f,
                AttackSpeed = 1.0f,
                Range = 2,
                HandsRequired = 2,
                CanUseOneHand = true,  // Can use one-handed but less effective
                CanUseTwoHand = true,
                TwoHandDamageBonus = 0.35f
            });
            
            // ========== WEAPONS - RANGED ==========
            
            AddItem(new ItemDefinition
            {
                Id = "bow_simple",
                Name = "Simple Bow",
                Description = "Handmade bow. Quiet and reliable. Requires two hands.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Common,
                Weight = 0.8f,
                BaseValue = 20,
                EquipSlot = EquipSlot.TwoHand,
                WeaponType = WeaponType.Bow,
                WeaponLength = WeaponLength.Long,
                Damage = 12f,
                AttackSpeed = 0.8f,
                Range = 6,
                Accuracy = 5f,
                RequiresAmmo = "arrow_basic",
                HandsRequired = 2,
                CanUseOneHand = false,
                CanUseTwoHand = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "pistol_9mm",
                Name = "9mm Pistol",
                Description = "Standard sidearm. Accurate and compact. Can be dual-wielded.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.9f,
                BaseValue = 75,
                EquipSlot = EquipSlot.MainHand,
                WeaponType = WeaponType.Pistol,
                WeaponLength = WeaponLength.Short,
                Damage = 18f,
                AttackSpeed = 1.0f,
                Range = 8,
                Accuracy = 10f,
                RequiresAmmo = "ammo_9mm",
                HandsRequired = 1,
                CanUseOneHand = true,
                CanUseTwoHand = true,
                TwoHandDamageBonus = 0.10f,  // Slight accuracy bonus when steadied
                DualWieldPenalty = 0.20f     // Akimbo penalty
            });
            
            AddItem(new ItemDefinition
            {
                Id = "shotgun_pump",
                Name = "Pump Shotgun",
                Description = "Devastating at close range. Loud. Requires two hands.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Rare,
                Weight = 3.5f,
                BaseValue = 150,
                EquipSlot = EquipSlot.TwoHand,
                WeaponType = WeaponType.Shotgun,
                WeaponLength = WeaponLength.Long,
                Damage = 28f,  // Reduced from 35
                AttackSpeed = 0.6f,
                Range = 4,
                Accuracy = -5f,  // Spread
                RequiresAmmo = "ammo_shells",
                HandsRequired = 2,
                CanUseOneHand = true,  // Can hip-fire with penalty
                CanUseTwoHand = true,
                TwoHandDamageBonus = 0.20f
            });
            
            // ========== AMMO ==========
            
            AddItem(new ItemDefinition
            {
                Id = "arrow_basic",
                Name = "Arrow",
                Description = "Basic wooden arrow.",
                Category = ItemCategory.Ammo,
                Rarity = ItemRarity.Common,
                Weight = 0.05f,
                BaseValue = 1,
                IsStackable = true,
                MaxStackSize = 50
            });
            
            AddItem(new ItemDefinition
            {
                Id = "ammo_9mm",
                Name = "9mm Rounds",
                Description = "Standard pistol ammunition.",
                Category = ItemCategory.Ammo,
                Rarity = ItemRarity.Common,
                Weight = 0.02f,
                BaseValue = 2,
                IsStackable = true,
                MaxStackSize = 100
            });
            
            AddItem(new ItemDefinition
            {
                Id = "ammo_shells",
                Name = "Shotgun Shells",
                Description = "12 gauge shotgun shells.",
                Category = ItemCategory.Ammo,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.04f,
                BaseValue = 5,
                IsStackable = true,
                MaxStackSize = 50
            });
            
            // ========== ARMOR ==========
            
            AddItem(new ItemDefinition
            {
                Id = "armor_leather",
                Name = "Leather Jacket",
                Description = "Worn leather jacket. Some protection.",
                Category = ItemCategory.Armor,
                Rarity = ItemRarity.Common,
                Weight = 2.0f,
                BaseValue = 20,
                EquipSlot = EquipSlot.Torso,
                ArmorSlot = ArmorSlot.Torso,
                Armor = 5f
            });
            
            AddItem(new ItemDefinition
            {
                Id = "armor_raider",
                Name = "Raider Armor",
                Description = "Scrap metal strapped together. Ugly but effective.",
                Category = ItemCategory.Armor,
                Rarity = ItemRarity.Uncommon,
                Weight = 5.0f,
                BaseValue = 50,
                EquipSlot = EquipSlot.Torso,
                ArmorSlot = ArmorSlot.Torso,
                Armor = 12f
            });
            
            AddItem(new ItemDefinition
            {
                Id = "helmet_hardhat",
                Name = "Hard Hat",
                Description = "Construction helmet. Protects from falling debris.",
                Category = ItemCategory.Armor,
                Rarity = ItemRarity.Common,
                Weight = 0.5f,
                BaseValue = 10,
                EquipSlot = EquipSlot.Head,
                ArmorSlot = ArmorSlot.Head,
                Armor = 3f
            });
            
            // ========== EQUIPMENT WITH COMBAT BONUSES ==========
            
            AddItem(new ItemDefinition
            {
                Id = "boots_combat",
                Name = "Combat Boots",
                Description = "Military boots. Enhanced mobility in combat.",
                Category = ItemCategory.Armor,
                Rarity = ItemRarity.Uncommon,
                Weight = 1.0f,
                BaseValue = 35,
                EquipSlot = EquipSlot.Feet,
                ArmorSlot = ArmorSlot.Feet,
                Armor = 3f,
                MovementPointBonus = 1  // +1 MP
            });
            
            AddItem(new ItemDefinition
            {
                Id = "boots_runner",
                Name = "Runner's Boots",
                Description = "Lightweight boots made for speed.",
                Category = ItemCategory.Armor,
                Rarity = ItemRarity.Rare,
                Weight = 0.5f,
                BaseValue = 80,
                EquipSlot = EquipSlot.Feet,
                ArmorSlot = ArmorSlot.Feet,
                Armor = 1f,
                MovementPointBonus = 2  // +2 MP
            });
            
            AddItem(new ItemDefinition
            {
                Id = "gloves_tactical",
                Name = "Tactical Gloves",
                Description = "Precision combat gloves. Better weapon handling.",
                Category = ItemCategory.Armor,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.3f,
                BaseValue = 40,
                EquipSlot = EquipSlot.Hands,
                ArmorSlot = ArmorSlot.Hands,
                Armor = 2f,
                Accuracy = 0.05f,
                ActionPointBonus = 1  // +1 AP
            });
            
            AddItem(new ItemDefinition
            {
                Id = "armor_exosuit",
                Name = "Exoskeleton Frame",
                Description = "Powered exoskeleton. Enhanced strength and mobility.",
                Category = ItemCategory.Armor,
                Rarity = ItemRarity.Legendary,
                Weight = 8.0f,
                BaseValue = 500,
                EquipSlot = EquipSlot.Torso,
                ArmorSlot = ArmorSlot.Torso,
                Armor = 20f,
                ActionPointBonus = 1,
                MovementPointBonus = 1
            });
            
            // ========== PSIONIC EQUIPMENT ==========
            
            AddItem(new ItemDefinition
            {
                Id = "psi_amplifier",
                Name = "Psionic Amplifier",
                Description = "A neural device that enhances psychic abilities.",
                Category = ItemCategory.Armor,
                Rarity = ItemRarity.Rare,
                Weight = 0.2f,
                BaseValue = 150,
                EquipSlot = EquipSlot.Head,
                ArmorSlot = ArmorSlot.Head,
                Armor = 1f,
                EsperPointBonus = 3,
                EsperPowerBonus = 0.15f  // +15% esper power
            });
            
            AddItem(new ItemDefinition
            {
                Id = "psi_focus_crystal",
                Name = "Focus Crystal",
                Description = "A strange crystal that focuses mental energy.",
                Category = ItemCategory.Armor,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.1f,
                BaseValue = 60,
                EquipSlot = EquipSlot.OffHand,
                EsperPointBonus = 2,
                EsperPowerBonus = 0.1f  // +10% esper power
            });
            
            AddItem(new ItemDefinition
            {
                Id = "psi_crown",
                Name = "Psionic Crown",
                Description = "A crown of twisted metal that resonates with psychic energy.",
                Category = ItemCategory.Armor,
                Rarity = ItemRarity.Legendary,
                Weight = 0.5f,
                BaseValue = 300,
                EquipSlot = EquipSlot.Head,
                ArmorSlot = ArmorSlot.Head,
                Armor = 2f,
                EsperPointBonus = 5,
                EsperPowerBonus = 0.25f  // +25% esper power
            });
            
            // ========== MEDICAL ITEMS ==========
            
            AddItem(new ItemDefinition
            {
                Id = "med_bandage",
                Name = "Bandage",
                Description = "Basic bandage. Stops bleeding and heals 15% HP.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Common,
                Weight = 0.1f,
                BaseValue = 5,
                IsStackable = true,
                MaxStackSize = 10,
                ConsumableType = ConsumableType.Medicine,
                IsMedical = true,
                HealPercent = 12f,               // Moderate healing - 10-11 HP
                CanHealBleeding = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "med_kit",
                Name = "Medical Kit",
                Description = "Complete medical kit. Heals 25% HP and stops bleeding.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.5f,
                BaseValue = 25,
                IsStackable = true,
                MaxStackSize = 5,
                ConsumableType = ConsumableType.Medicine,
                IsMedical = true,
                HealPercent = 25f,               // Good healing - ~22 HP
                CanHealBleeding = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "med_antibiotics",
                Name = "Antibiotics",
                Description = "Pre-war antibiotics. Cures infections.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.1f,
                BaseValue = 40,
                IsStackable = true,
                MaxStackSize = 5,
                ConsumableType = ConsumableType.Medicine,
                IsMedical = true,
                CanHealInfection = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "med_splint",
                Name = "Splint",
                Description = "Makeshift splint. Helps fractures heal faster.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Common,
                Weight = 0.3f,
                BaseValue = 15,
                IsStackable = true,
                MaxStackSize = 5,
                ConsumableType = ConsumableType.Medicine,
                IsMedical = true,
                CanHealFracture = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "med_doctor_bag",
                Name = "Doctor's Bag",
                Description = "Professional medical supplies. Treats all injuries.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Rare,
                Weight = 1.0f,
                BaseValue = 100,
                IsStackable = true,
                MaxStackSize = 3,
                ConsumableType = ConsumableType.Medicine,
                IsMedical = true,
                BodyPartHealAmount = 50f,
                CanHealBleeding = true,
                CanHealInfection = true,
                CanHealFracture = true
            });
            
            // ========== CONSUMABLES - FOOD ==========
            
            AddItem(new ItemDefinition
            {
                Id = "food_canned",
                Name = "Canned Food",
                Description = "Pre-war canned goods. Still edible... probably.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Common,
                Weight = 0.3f,
                BaseValue = 8,
                IsStackable = true,
                MaxStackSize = 10,
                ConsumableType = ConsumableType.Food,
                HungerRestore = 30f
            });
            
            AddItem(new ItemDefinition
            {
                Id = "food_jerky",
                Name = "Dried Meat",
                Description = "Preserved meat. Chewy but nutritious.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Common,
                Weight = 0.1f,
                BaseValue = 5,
                IsStackable = true,
                MaxStackSize = 20,
                ConsumableType = ConsumableType.Food,
                HungerRestore = 20f
            });
            
            AddItem(new ItemDefinition
            {
                Id = "food_mutfruit",
                Name = "Mutfruit",
                Description = "Mutated fruit. Looks weird but tastes fine.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Common,
                Weight = 0.2f,
                BaseValue = 3,
                IsStackable = true,
                MaxStackSize = 15,
                ConsumableType = ConsumableType.Food,
                HungerRestore = 15f,
                ThirstRestore = 5f
            });
            
            AddItem(new ItemDefinition
            {
                Id = "food_steak",
                Name = "Cooked Steak",
                Description = "Properly cooked meat. Filling and delicious.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.3f,
                BaseValue = 20,
                IsStackable = true,
                MaxStackSize = 5,
                ConsumableType = ConsumableType.Food,
                HungerRestore = 50f
            });
            
            // ========== CONSUMABLES - WATER ==========
            
            AddItem(new ItemDefinition
            {
                Id = "water_dirty",
                Name = "Dirty Water",
                Description = "Water of questionable quality. Might make you sick.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Common,
                Weight = 0.5f,
                BaseValue = 2,
                IsStackable = true,
                MaxStackSize = 10,
                ConsumableType = ConsumableType.Water,
                ThirstRestore = 25f,
                AppliesEffect = StatusEffectType.Poisoned,
                EffectDuration = 30f
            });
            
            AddItem(new ItemDefinition
            {
                Id = "water_purified",
                Name = "Purified Water",
                Description = "Clean, safe drinking water.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.5f,
                BaseValue = 10,
                IsStackable = true,
                MaxStackSize = 10,
                ConsumableType = ConsumableType.Water,
                ThirstRestore = 40f
            });
            
            // ========== CONSUMABLES - MEDICINE ==========
            
            AddItem(new ItemDefinition
            {
                Id = "medkit",
                Name = "Medkit",
                Description = "Standard medical supplies. Heals wounds.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.3f,
                BaseValue = 30,
                IsStackable = true,
                MaxStackSize = 5,
                ConsumableType = ConsumableType.Medicine,
                HealthRestore = 40f
            });
            
            AddItem(new ItemDefinition
            {
                Id = "bandage",
                Name = "Bandage",
                Description = "Simple bandage. Stops bleeding, minor healing.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Common,
                Weight = 0.1f,
                BaseValue = 5,
                IsStackable = true,
                MaxStackSize = 10,
                ConsumableType = ConsumableType.Medicine,
                HealthRestore = 15f
            });
            
            AddItem(new ItemDefinition
            {
                Id = "antidote",
                Name = "Antidote",
                Description = "Cures poison and toxins.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.1f,
                BaseValue = 25,
                IsStackable = true,
                MaxStackSize = 5,
                ConsumableType = ConsumableType.Antidote
            });
            
            AddItem(new ItemDefinition
            {
                Id = "rad_away",
                Name = "Rad-Away",
                Description = "Flushes radiation from the body.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Rare,
                Weight = 0.2f,
                BaseValue = 50,
                IsStackable = true,
                MaxStackSize = 5,
                ConsumableType = ConsumableType.RadAway,
                RadiationRemove = 50f
            });
            
            AddItem(new ItemDefinition
            {
                Id = "stimpack",
                Name = "Stimpack",
                Description = "Military-grade healing stimulant. Fast acting.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Rare,
                Weight = 0.1f,
                BaseValue = 75,
                IsStackable = true,
                MaxStackSize = 5,
                ConsumableType = ConsumableType.Stimulant,
                HealthRestore = 60f,
                AppliesEffect = StatusEffectType.Focused,
                EffectDuration = 60f
            });
            
            // ========== MATERIALS ==========
            
            AddItem(new ItemDefinition
            {
                Id = "scrap_metal",
                Name = "Scrap Metal",
                Description = "Bits of metal. Useful for crafting and repairs.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Common,
                Weight = 0.5f,
                BaseValue = 2,
                IsStackable = true,
                MaxStackSize = 50,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "scrap_electronics",
                Name = "Electronic Components",
                Description = "Salvaged circuits and wires.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.2f,
                BaseValue = 8,
                IsStackable = true,
                MaxStackSize = 30,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "cloth",
                Name = "Cloth",
                Description = "Fabric scraps for crafting.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Common,
                Weight = 0.1f,
                BaseValue = 1,
                IsStackable = true,
                MaxStackSize = 50,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "leather",
                Name = "Leather",
                Description = "Animal hide, tanned and ready for use.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Common,
                Weight = 0.3f,
                BaseValue = 5,
                IsStackable = true,
                MaxStackSize = 30,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "wood",
                Name = "Wood",
                Description = "Lumber for building and fuel.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Common,
                Weight = 1.0f,
                BaseValue = 1,
                IsStackable = true,
                MaxStackSize = 50,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "mutant_meat",
                Name = "Mutant Meat",
                Description = "Meat from a mutated creature. Cook before eating!",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Common,
                Weight = 0.5f,
                BaseValue = 3,
                IsStackable = true,
                MaxStackSize = 20,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "void_essence",
                Name = "Void Essence",
                Description = "Dark energy crystallized. Used in Dark Science.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Rare,
                Weight = 0.1f,
                BaseValue = 50,
                IsStackable = true,
                MaxStackSize = 10,
                IsCraftingMaterial = true
            });
            
            // ========== BUILDING RESOURCES ==========
            
            AddItem(new ItemDefinition
            {
                Id = "stone",
                Name = "Stone",
                Description = "Raw stone blocks. Heavy but durable building material.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Common,
                Weight = 2.0f,
                BaseValue = 2,
                IsStackable = true,
                MaxStackSize = 30,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "metal",
                Name = "Scrap Metal",
                Description = "Salvaged metal pieces. Can be used for construction and crafting.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Uncommon,
                Weight = 1.5f,
                BaseValue = 10,
                IsStackable = true,
                MaxStackSize = 25,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "cloth",
                Name = "Cloth",
                Description = "Fabric scraps. Used for bedding, clothing, and bandages.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Common,
                Weight = 0.2f,
                BaseValue = 3,
                IsStackable = true,
                MaxStackSize = 40,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "components",
                Name = "Components",
                Description = "Mechanical parts and electronics. Required for advanced construction.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.5f,
                BaseValue = 25,
                IsStackable = true,
                MaxStackSize = 15,
                IsCraftingMaterial = true
            });
            
            // ========== RESEARCH MATERIALS ==========
            
            AddItem(new ItemDefinition
            {
                Id = "anomaly_shard",
                Name = "Anomaly Shard",
                Description = "A crystallized fragment of pure anomalous energy. Pulsates with an otherworldly glow.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Rare,
                Weight = 0.2f,
                BaseValue = 50,
                IsStackable = true,
                MaxStackSize = 20,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "mutagen",
                Name = "Mutagen",
                Description = "A volatile substance that accelerates mutation. Handle with extreme caution.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Rare,
                Weight = 0.3f,
                BaseValue = 75,
                IsStackable = true,
                MaxStackSize = 10,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "bone",
                Name = "Bone",
                Description = "Sturdy bone harvested from creatures. Used in dark crafting.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Common,
                Weight = 0.4f,
                BaseValue = 5,
                IsStackable = true,
                MaxStackSize = 30,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "sinew",
                Name = "Sinew",
                Description = "Strong tendons from mutant creatures. Surprisingly flexible and durable.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.2f,
                BaseValue = 15,
                IsStackable = true,
                MaxStackSize = 20,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "brain_tissue",
                Name = "Brain Tissue",
                Description = "Preserved neural matter. Essential for psionic research.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Rare,
                Weight = 0.5f,
                BaseValue = 100,
                IsStackable = true,
                MaxStackSize = 5,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "essence",
                Name = "Pure Essence",
                Description = "The concentrated life force of a powerful mutant. Radiates an unsettling warmth.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Epic,
                Weight = 0.1f,
                BaseValue = 250,
                IsStackable = true,
                MaxStackSize = 3,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "energy_cell",
                Name = "Energy Cell",
                Description = "Pre-war power cell. Still holds a charge after centuries.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Rare,
                Weight = 0.3f,
                BaseValue = 80,
                IsStackable = true,
                MaxStackSize = 10,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "herbs",
                Name = "Medicinal Herbs",
                Description = "Plants with healing properties. Used in medicine crafting.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Common,
                Weight = 0.1f,
                BaseValue = 8,
                IsStackable = true,
                MaxStackSize = 30,
                IsCraftingMaterial = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "salt",
                Name = "Salt",
                Description = "Essential for food preservation and some crafting recipes.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Common,
                Weight = 0.2f,
                BaseValue = 5,
                IsStackable = true,
                MaxStackSize = 50,
                IsCraftingMaterial = true
            });
            
            // ========== JUNK ==========
            
            AddItem(new ItemDefinition
            {
                Id = "junk_bottle",
                Name = "Empty Bottle",
                Description = "Glass bottle. Can hold liquids.",
                Category = ItemCategory.Junk,
                Rarity = ItemRarity.Common,
                Weight = 0.2f,
                BaseValue = 1,
                IsStackable = true,
                MaxStackSize = 20
            });
            
            AddItem(new ItemDefinition
            {
                Id = "junk_bones",
                Name = "Bones",
                Description = "Assorted bones. Crafting material or fuel.",
                Category = ItemCategory.Junk,
                Rarity = ItemRarity.Common,
                Weight = 0.3f,
                BaseValue = 1,
                IsStackable = true,
                MaxStackSize = 30
            });
            
            // ==================
            // CRAFTABLE ITEMS
            // ==================
            
            AddItem(new ItemDefinition
            {
                Id = "torch",
                Name = "Torch",
                Description = "A simple light source. Burns for a while.",
                Category = ItemCategory.Tool,
                Rarity = ItemRarity.Common,
                Weight = 0.5f,
                BaseValue = 5,
                IsStackable = true,
                MaxStackSize = 10
            });
            
            AddItem(new ItemDefinition
            {
                Id = "food_stew",
                Name = "Hearty Stew",
                Description = "Nutritious meat and vegetable stew. Very filling.",
                Category = ItemCategory.Consumable,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.4f,
                BaseValue = 25,
                IsStackable = true,
                MaxStackSize = 5,
                HungerRestore = 50f,
                ThirstRestore = 20f,
                HealthRestore = 10f
            });
            
            // ========== CURRENCY - VOID SHARDS ==========
            
            AddItem(new ItemDefinition
            {
                Id = "void_shard",
                Name = "Void Shard",
                Description = "Crystallized Void energy. The universal currency of the Exclusion Zone. Glows with an unsettling purple light.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.05f,
                BaseValue = 10,
                IsStackable = true,
                MaxStackSize = 999
            });
            
            AddItem(new ItemDefinition
            {
                Id = "void_shard_pure",
                Name = "Pure Void Shard",
                Description = "Highly concentrated Void energy. Used in Dark Science rituals. The Changed say it whispers.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Rare,
                Weight = 0.1f,
                BaseValue = 50,
                IsStackable = true,
                MaxStackSize = 99
            });
            
            AddItem(new ItemDefinition
            {
                Id = "sanctum_credit_chip",
                Name = "Sanctum Credit Chip",
                Description = "Digital currency from the United Sanctum. Worth nothing in the Zone, but valuable to traders.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.01f,
                BaseValue = 25,
                IsStackable = true,
                MaxStackSize = 999
            });
            
            // ========== UNITED SANCTUM WEAPONS ==========
            
            AddItem(new ItemDefinition
            {
                Id = "sanctum_rifle",
                Name = "Sanctum Energy Rifle",
                Description = "Standard issue energy weapon of Purge Squads. Clean, efficient, deadly. Requires energy cells.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Rare,
                Weight = 2.5f,
                BaseValue = 200,
                EquipSlot = EquipSlot.TwoHand,
                WeaponType = WeaponType.EnergyWeapon,
                WeaponLength = WeaponLength.Long,
                Damage = 20f,
                AttackSpeed = 0.9f,
                Range = 8,
                Accuracy = 10f,
                RequiresAmmo = "energy_cell",
                HandsRequired = 2,
                CanUseOneHand = false,
                CanUseTwoHand = true
            });
            
            AddItem(new ItemDefinition
            {
                Id = "sanctum_pistol",
                Name = "Sanctum Sidearm",
                Description = "Compact energy pistol. Backup weapon for Sanctum soldiers. Low damage but accurate.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.8f,
                BaseValue = 100,
                EquipSlot = EquipSlot.MainHand,
                WeaponType = WeaponType.EnergyWeapon,
                WeaponLength = WeaponLength.Short,
                Damage = 12f,
                AttackSpeed = 1.1f,
                Range = 5,
                Accuracy = 8f,
                RequiresAmmo = "energy_cell",
                HandsRequired = 1,
                CanUseOneHand = true,
                CanUseTwoHand = true,
                TwoHandDamageBonus = 0.10f,
                DualWieldPenalty = 0.15f
            });
            
            // ========== VERDANT ORDER WEAPONS ==========
            
            AddItem(new ItemDefinition
            {
                Id = "verdant_tranq",
                Name = "Verdant Tranquilizer",
                Description = "Bio-engineered dart gun. Fires paralytic toxins. The Verdant prefer their specimens alive.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Rare,
                Weight = 1.5f,
                BaseValue = 150,
                EquipSlot = EquipSlot.TwoHand,
                WeaponType = WeaponType.Rifle,
                WeaponLength = WeaponLength.Long,
                Damage = 8f,  // Low damage, but applies status
                AttackSpeed = 0.7f,
                Range = 7,
                Accuracy = 5f,
                RequiresAmmo = "tranq_dart",
                HandsRequired = 2
            });
            
            AddItem(new ItemDefinition
            {
                Id = "verdant_flamer",
                Name = "Purification Torch",
                Description = "Verdant flamethrower. Used to 'cleanse impurity'. Short range but devastating.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Rare,
                Weight = 4.0f,
                BaseValue = 180,
                EquipSlot = EquipSlot.TwoHand,
                WeaponType = WeaponType.EnergyWeapon,
                WeaponLength = WeaponLength.Medium,
                Damage = 15f,
                AttackSpeed = 0.6f,
                Range = 3,
                RequiresAmmo = "fuel_canister",
                HandsRequired = 2
            });
            
            // ========== ANCIENT RELICS (Aethelgard Tech) ==========
            
            AddItem(new ItemDefinition
            {
                Id = "relic_blade",
                Name = "Aethelgard Vibro-Blade",
                Description = "400-year-old technology that still hums with power. The blade vibrates at frequencies that ignore armor.",
                Category = ItemCategory.Weapon,
                Rarity = ItemRarity.Legendary,
                Weight = 1.0f,
                BaseValue = 500,
                EquipSlot = EquipSlot.MainHand,
                WeaponType = WeaponType.Sword,
                WeaponLength = WeaponLength.Medium,
                Damage = 25f,
                AttackSpeed = 1.2f,
                Range = 1,
                Accuracy = 10f,
                ArmorPenetration = 0.5f,  // Ignores 50% armor
                HandsRequired = 1,
                CanUseOneHand = true,
                CanUseTwoHand = true,
                TwoHandDamageBonus = 0.25f
            });
            
            AddItem(new ItemDefinition
            {
                Id = "relic_scanner",
                Name = "Aethelgard Scanner",
                Description = "Pre-Severance detection device. Shows nearby enemies and valuable items. Partially functional.",
                Category = ItemCategory.Tool,
                Rarity = ItemRarity.Epic,
                Weight = 0.5f,
                BaseValue = 300
            });
            
            AddItem(new ItemDefinition
            {
                Id = "relic_core",
                Name = "Void Reactor Core",
                Description = "The heart of Aethelgard's forbidden experiments. Radiates immense power. Handle with extreme caution.",
                Category = ItemCategory.Quest,
                Rarity = ItemRarity.Legendary,
                Weight = 5.0f,
                BaseValue = 1000
            });
            
            // ========== VOID-TOUCHED MATERIALS ==========
            
            AddItem(new ItemDefinition
            {
                Id = "void_flesh",
                Name = "Void-Touched Flesh",
                Description = "Meat from a creature corrupted by the Void. Nutritious but may cause... changes.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Uncommon,
                Weight = 0.3f,
                BaseValue = 15,
                IsStackable = true,
                MaxStackSize = 20
            });
            
            AddItem(new ItemDefinition
            {
                Id = "void_ichor",
                Name = "Void Ichor",
                Description = "Liquid corruption from deep in the Zone. Essential for Dark Science rituals.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Rare,
                Weight = 0.2f,
                BaseValue = 40,
                IsStackable = true,
                MaxStackSize = 10
            });
            
            AddItem(new ItemDefinition
            {
                Id = "reality_fragment",
                Name = "Reality Fragment",
                Description = "A piece of 'normal' space trapped in crystal. Extremely rare. Can stabilize Void corruption.",
                Category = ItemCategory.Material,
                Rarity = ItemRarity.Epic,
                Weight = 0.1f,
                BaseValue = 200,
                IsStackable = true,
                MaxStackSize = 5
            });
            
            _initialized = true;
            System.Diagnostics.Debug.WriteLine($">>> ItemDatabase initialized with {_items.Count} items <<<");
        }
        
        private static void AddItem(ItemDefinition def)
        {
            _items[def.Id] = def;
        }
        
        public static ItemDefinition Get(string id)
        {
            if (!_initialized) Initialize();
            return _items.TryGetValue(id, out var def) ? def : null;
        }
        
        public static List<ItemDefinition> GetAll()
        {
            if (!_initialized) Initialize();
            return new List<ItemDefinition>(_items.Values);
        }
        
        public static List<ItemDefinition> GetByCategory(ItemCategory category)
        {
            if (!_initialized) Initialize();
            var result = new List<ItemDefinition>();
            foreach (var item in _items.Values)
            {
                if (item.Category == category) result.Add(item);
            }
            return result;
        }
        
        public static List<ItemDefinition> GetByRarity(ItemRarity rarity)
        {
            if (!_initialized) Initialize();
            var result = new List<ItemDefinition>();
            foreach (var item in _items.Values)
            {
                if (item.Rarity == rarity) result.Add(item);
            }
            return result;
        }
        
        /// <summary>
        /// Create a random item of specified rarity
        /// </summary>
        public static Item CreateRandom(ItemRarity? rarity = null, Random random = null)
        {
            if (!_initialized) Initialize();
            random ??= new Random();
            
            var pool = rarity.HasValue ? GetByRarity(rarity.Value) : GetAll();
            if (pool.Count == 0) return null;
            
            var def = pool[random.Next(pool.Count)];
            var quality = RollQuality(random);
            
            return new Item(def.Id, quality);
        }
        
        private static ItemQuality RollQuality(Random random)
        {
            int roll = random.Next(100);
            
            if (roll < 5) return ItemQuality.Broken;        // 5%
            if (roll < 20) return ItemQuality.Poor;         // 15%
            if (roll < 70) return ItemQuality.Normal;       // 50%
            if (roll < 90) return ItemQuality.Good;         // 20%
            if (roll < 98) return ItemQuality.Excellent;    // 8%
            return ItemQuality.Masterwork;                   // 2%
        }
    }
}
