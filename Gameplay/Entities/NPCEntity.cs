// Gameplay/Entities/NPCEntity.cs
// NPC entity for merchants, quest givers, and other friendly NPCs

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MyRPG.Data;
using MyRPG.Gameplay.Items;
using MyRPG.Gameplay.Character;

namespace MyRPG.Gameplay.Entities
{
    // ============================================
    // NPC TYPE ENUM
    // ============================================
    
    public enum NPCType
    {
        Merchant,       // General goods trader
        WeaponSmith,    // Weapons and armor
        Alchemist,      // Medicine, chems
        TechDealer,     // Tinker science items
        VoidMerchant,   // Dark science items
        ScrapDealer,    // Buys junk
        QuestGiver,     // Gives quests
        Doctor,         // Heals player
        Wanderer,       // Random traveler
        Informant       // Sells information
    }
    
    // ============================================
    // MERCHANT STOCK ITEM
    // ============================================
    
    public class MerchantStock
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public int MaxQuantity { get; set; }
        public float PriceModifier { get; set; } = 1.0f;
        
        public MerchantStock(string itemId, int quantity, int maxQuantity = -1, float priceModifier = 1.0f)
        {
            ItemId = itemId;
            Quantity = quantity;
            MaxQuantity = maxQuantity > 0 ? maxQuantity : quantity;
            PriceModifier = priceModifier;
        }
        
        public void Restock()
        {
            Quantity = MaxQuantity;
        }
    }
    
    // ============================================
    // NPC ENTITY
    // ============================================
    
    public class NPCEntity
    {
        // Identity
        public string Id { get; set; }
        public string Name { get; set; }
        public NPCType Type { get; set; }
        
        // Position
        public Vector2 Position { get; set; }
        public bool IsAlive { get; set; } = true;
        
        // Trading
        public List<MerchantStock> Stock { get; set; } = new List<MerchantStock>();
        public int Gold { get; set; } = 500;
        public float BuyPriceMultiplier { get; set; } = 0.5f;   // What NPC pays for items (50%)
        public float SellPriceMultiplier { get; set; } = 1.5f;  // What NPC charges (150%)
        
        // Dialogue
        public string Greeting { get; set; } = "Welcome, traveler.";
        public string Farewell { get; set; } = "Safe travels.";
        
        // Visual
        public Color DisplayColor { get; set; } = Color.Cyan;
        
        // Restock timer
        public float RestockTimer { get; set; } = 0f;
        public float RestockInterval { get; set; } = 300f;  // 5 minutes
        
        // ============================================
        // FACTORY METHODS
        // ============================================
        
        public static NPCEntity CreateGeneralMerchant(string id, Vector2 position)
        {
            var npc = new NPCEntity
            {
                Id = id,
                Name = GenerateMerchantName(),
                Type = NPCType.Merchant,
                Position = position,
                Gold = 500,
                DisplayColor = Color.Cyan,
                Greeting = "Looking to trade? I've got supplies.",
                BuyPriceMultiplier = 0.5f,
                SellPriceMultiplier = 1.4f
            };
            
            // Stock general goods
            npc.Stock.Add(new MerchantStock("bandage", 10));
            npc.Stock.Add(new MerchantStock("med_kit", 3));
            npc.Stock.Add(new MerchantStock("canned_food", 8));
            npc.Stock.Add(new MerchantStock("water_bottle", 8));
            npc.Stock.Add(new MerchantStock("torch", 5));
            npc.Stock.Add(new MerchantStock("rope", 3));
            npc.Stock.Add(new MerchantStock("lockpick", 5));
            npc.Stock.Add(new MerchantStock("repair_kit", 2));
            
            return npc;
        }
        
        public static NPCEntity CreateWeaponsMerchant(string id, Vector2 position)
        {
            var npc = new NPCEntity
            {
                Id = id,
                Name = GenerateWeaponsmithName(),
                Type = NPCType.WeaponSmith,
                Position = position,
                Gold = 800,
                DisplayColor = Color.OrangeRed,
                Greeting = "Need firepower? You've come to the right place.",
                BuyPriceMultiplier = 0.6f,  // Better prices for weapons
                SellPriceMultiplier = 1.3f
            };
            
            // Stock weapons and armor
            npc.Stock.Add(new MerchantStock("knife", 3));
            npc.Stock.Add(new MerchantStock("machete", 2));
            npc.Stock.Add(new MerchantStock("pipe_club", 3));
            npc.Stock.Add(new MerchantStock("spear", 2));
            npc.Stock.Add(new MerchantStock("pistol", 1));
            npc.Stock.Add(new MerchantStock("pistol_ammo", 30));
            npc.Stock.Add(new MerchantStock("rifle_ammo", 20));
            npc.Stock.Add(new MerchantStock("leather_armor", 2));
            npc.Stock.Add(new MerchantStock("scrap_helmet", 2));
            npc.Stock.Add(new MerchantStock("leather_boots", 2));
            
            return npc;
        }
        
        public static NPCEntity CreateWanderer(string id, Vector2 position)
        {
            var npc = new NPCEntity
            {
                Id = id,
                Name = GenerateWandererName(),
                Type = NPCType.Wanderer,
                Position = position,
                Gold = 150,
                DisplayColor = Color.Gray,
                Greeting = "Ah, another survivor. Care to trade what little I have?",
                BuyPriceMultiplier = 0.4f,
                SellPriceMultiplier = 1.2f  // Cheaper prices
            };
            
            // Random scavenged items
            Random rand = new Random(id.GetHashCode());
            
            if (rand.NextDouble() < 0.7f) npc.Stock.Add(new MerchantStock("canned_food", rand.Next(1, 4)));
            if (rand.NextDouble() < 0.5f) npc.Stock.Add(new MerchantStock("water_bottle", rand.Next(1, 3)));
            if (rand.NextDouble() < 0.3f) npc.Stock.Add(new MerchantStock("bandage", rand.Next(1, 3)));
            if (rand.NextDouble() < 0.2f) npc.Stock.Add(new MerchantStock("scrap_metal", rand.Next(2, 6)));
            if (rand.NextDouble() < 0.1f) npc.Stock.Add(new MerchantStock("knife", 1));
            
            return npc;
        }
        
        public static NPCEntity CreateAlchemist(string id, Vector2 position)
        {
            var npc = new NPCEntity
            {
                Id = id,
                Name = GenerateAlchemistName(),
                Type = NPCType.Alchemist,
                Position = position,
                Gold = 400,
                DisplayColor = Color.LimeGreen,
                Greeting = "Potions, medicines, and more exotic substances...",
                BuyPriceMultiplier = 0.5f,
                SellPriceMultiplier = 1.5f
            };
            
            npc.Stock.Add(new MerchantStock("bandage", 15));
            npc.Stock.Add(new MerchantStock("med_kit", 5));
            npc.Stock.Add(new MerchantStock("antidote", 3));
            npc.Stock.Add(new MerchantStock("stim_pack", 3));
            npc.Stock.Add(new MerchantStock("rad_away", 2));
            npc.Stock.Add(new MerchantStock("healing_herbs", 10));
            npc.Stock.Add(new MerchantStock("chemical_compound", 5));
            
            return npc;
        }
        
        public static NPCEntity CreateDoctor(string id, Vector2 position)
        {
            var npc = new NPCEntity
            {
                Id = id,
                Name = "Doc " + GenerateName(),
                Type = NPCType.Doctor,
                Position = position,
                Gold = 300,
                DisplayColor = Color.White,
                Greeting = "I can patch you up... for a price.",
                BuyPriceMultiplier = 0.4f,
                SellPriceMultiplier = 1.6f
            };
            
            npc.Stock.Add(new MerchantStock("bandage", 20));
            npc.Stock.Add(new MerchantStock("med_kit", 8));
            npc.Stock.Add(new MerchantStock("surgical_kit", 2));
            npc.Stock.Add(new MerchantStock("blood_pack", 3));
            
            return npc;
        }
        
        // ============================================
        // NAME GENERATORS
        // ============================================
        
        private static readonly string[] FirstNames = { "Jax", "Vera", "Kira", "Milo", "Zara", "Finn", "Nova", "Rex", "Luna", "Axel", "Thorn", "Shade", "Rust", "Ember", "Cinder" };
        private static readonly string[] Nicknames = { "the Trader", "Quickfingers", "Old", "Lucky", "Honest", "One-Eye", "Scarface", "the Mute", "Whispers", "" };
        private static readonly string[] WandererNames = { "Drifter", "Stranger", "Nomad", "Vagrant", "Traveler", "Outcast", "Survivor", "Scavenger" };
        
        private static Random _nameRandom = new Random();
        
        private static string GenerateName()
        {
            return FirstNames[_nameRandom.Next(FirstNames.Length)];
        }
        
        private static string GenerateMerchantName()
        {
            string first = FirstNames[_nameRandom.Next(FirstNames.Length)];
            string nick = Nicknames[_nameRandom.Next(Nicknames.Length)];
            return string.IsNullOrEmpty(nick) ? first : $"{nick} {first}";
        }
        
        private static string GenerateWeaponsmithName()
        {
            string first = FirstNames[_nameRandom.Next(FirstNames.Length)];
            return $"{first} the Armorer";
        }
        
        private static string GenerateWandererName()
        {
            if (_nameRandom.NextDouble() < 0.5f)
            {
                return $"The {WandererNames[_nameRandom.Next(WandererNames.Length)]}";
            }
            return FirstNames[_nameRandom.Next(FirstNames.Length)];
        }
        
        private static string GenerateAlchemistName()
        {
            string first = FirstNames[_nameRandom.Next(FirstNames.Length)];
            return $"{first} the Alchemist";
        }
        
        // ============================================
        // TRADING METHODS
        // ============================================
        
        /// <summary>
        /// Get the price NPC charges to sell an item to player
        /// </summary>
        public int GetSellPrice(string itemId)
        {
            var stock = Stock.FirstOrDefault(s => s.ItemId == itemId);
            var itemDef = ItemDatabase.Get(itemId);
            
            if (itemDef == null) return 999999;
            
            float basePrice = itemDef.BaseValue;
            float modifier = stock?.PriceModifier ?? 1.0f;
            
            return Math.Max(1, (int)(basePrice * SellPriceMultiplier * modifier));
        }
        
        /// <summary>
        /// Get the price NPC pays to buy an item from player
        /// </summary>
        public int GetBuyPrice(Item item)
        {
            if (item?.Definition == null) return 0;
            
            float basePrice = item.Definition.BaseValue;
            float qualityMod = GetQualityModifier(item.Quality);
            
            // Reduce price for damaged items
            float durabilityMod = item.MaxDurability > 0 
                ? (float)item.Durability / item.MaxDurability 
                : 1.0f;
            
            return Math.Max(1, (int)(basePrice * BuyPriceMultiplier * qualityMod * durabilityMod));
        }
        
        private float GetQualityModifier(ItemQuality quality)
        {
            return quality switch
            {
                ItemQuality.Broken => 0.1f,
                ItemQuality.Poor => 0.5f,
                ItemQuality.Normal => 1.0f,
                ItemQuality.Good => 1.25f,
                ItemQuality.Excellent => 1.5f,
                ItemQuality.Masterwork => 2.0f,
                _ => 1.0f
            };
        }
        
        /// <summary>
        /// NPC sells item to player
        /// </summary>
        public bool SellToPlayer(string itemId, int quantity, Inventory playerInventory, ref int playerGold)
        {
            var stock = Stock.FirstOrDefault(s => s.ItemId == itemId);
            if (stock == null || stock.Quantity < quantity) return false;
            
            int totalPrice = GetSellPrice(itemId) * quantity;
            if (playerGold < totalPrice) return false;
            
            // Try to add to player inventory
            if (!playerInventory.TryAddItem(itemId, quantity)) return false;
            
            // Transaction successful
            stock.Quantity -= quantity;
            playerGold -= totalPrice;
            Gold += totalPrice;
            
            return true;
        }
        
        /// <summary>
        /// NPC buys item from player
        /// </summary>
        public bool BuyFromPlayer(Item item, int quantity, Inventory playerInventory, ref int playerGold)
        {
            if (item == null || quantity <= 0) return false;
            if (item.StackCount < quantity) return false;
            
            int pricePerItem = GetBuyPrice(item);
            int totalPrice = pricePerItem * quantity;
            
            // Check if NPC has enough gold
            if (Gold < totalPrice) return false;
            
            // Remove from player inventory
            if (quantity >= item.StackCount)
            {
                playerInventory.RemoveItem(item);
            }
            else
            {
                item.StackCount -= quantity;
            }
            
            // Complete transaction
            playerGold += totalPrice;
            Gold -= totalPrice;
            
            // Add to NPC stock (optional - they might resell it)
            var existingStock = Stock.FirstOrDefault(s => s.ItemId == item.Id);
            if (existingStock != null)
            {
                existingStock.Quantity += quantity;
            }
            else
            {
                Stock.Add(new MerchantStock(item.Id, quantity, quantity * 2));
            }
            
            return true;
        }
        
        // ============================================
        // UPDATE
        // ============================================
        
        public void Update(float deltaTime)
        {
            // Restock timer
            RestockTimer += deltaTime;
            if (RestockTimer >= RestockInterval)
            {
                RestockTimer = 0f;
                foreach (var stock in Stock)
                {
                    stock.Restock();
                }
            }
        }
        
        // ============================================
        // UTILITY
        // ============================================
        
        public Point GetTilePosition(int tileSize)
        {
            return new Point((int)(Position.X / tileSize), (int)(Position.Y / tileSize));
        }
        
        public float DistanceTo(Vector2 other)
        {
            return Vector2.Distance(Position, other);
        }
        
        public bool CanTrade => Type == NPCType.Merchant || Type == NPCType.WeaponSmith || 
                                Type == NPCType.Alchemist || Type == NPCType.Wanderer ||
                                Type == NPCType.TechDealer || Type == NPCType.VoidMerchant ||
                                Type == NPCType.ScrapDealer || Type == NPCType.Doctor;
    }
}
