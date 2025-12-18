// Gameplay/Entities/NPCEntity.cs
// Non-hostile NPCs including merchants

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MyRPG.Data;
using MyRPG.Gameplay.Items;

namespace MyRPG.Gameplay.Entities
{
    // ============================================
    // NPC TYPE
    // ============================================
    
    public enum NPCType
    {
        Merchant,       // Buys and sells items
        Wanderer,       // Random traveler, may trade
        QuestGiver,     // Offers quests (future)
        Settler         // Base inhabitant (future)
    }
    
    public enum NPCState
    {
        Idle,
        Talking,        // In conversation/trade with player
        Walking,        // Moving around
        Sleeping        // Night time
    }
    
    // ============================================
    // MERCHANT INVENTORY
    // ============================================
    
    public class MerchantStock
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public int MaxQuantity { get; set; }
        public float PriceMultiplier { get; set; } = 1.0f;  // Merchant markup
        
        public MerchantStock(string itemId, int quantity, int maxQuantity, float priceMultiplier = 1.2f)
        {
            ItemId = itemId;
            Quantity = quantity;
            MaxQuantity = maxQuantity;
            PriceMultiplier = priceMultiplier;
        }
    }
    
    // ============================================
    // NPC ENTITY
    // ============================================
    
    public class NPCEntity
    {
        // Identity
        public string Id { get; private set; }
        public string Name { get; set; }
        public NPCType Type { get; private set; }
        
        // Position
        public Vector2 Position;
        public float Speed { get; set; } = 50f;
        
        // State
        public NPCState State { get; set; } = NPCState.Idle;
        public bool IsAlive { get; set; } = true;
        
        // Dialogue
        public string Greeting { get; set; } = "Hello, traveler.";
        public string[] DialogueOptions { get; set; } = new string[0];
        
        // Merchant specific
        public List<MerchantStock> Stock { get; private set; } = new List<MerchantStock>();
        public float BuyPriceMultiplier { get; set; } = 0.5f;   // What merchant pays for your items
        public float SellPriceMultiplier { get; set; } = 1.2f;  // What you pay for merchant items
        public int Gold { get; set; } = 500;                    // Merchant's money
        
        // Visual
        public Color DisplayColor { get; set; } = Color.Cyan;
        
        // ============================================
        // CONSTRUCTOR
        // ============================================
        
        public NPCEntity(string id, NPCType type, string name)
        {
            Id = id;
            Type = type;
            Name = name;
            
            // Set defaults based on type
            switch (type)
            {
                case NPCType.Merchant:
                    DisplayColor = Color.Gold;
                    Greeting = "Welcome! Take a look at my wares.";
                    break;
                case NPCType.Wanderer:
                    DisplayColor = Color.LightBlue;
                    Greeting = "The wasteland is dangerous. Stay safe.";
                    break;
                case NPCType.QuestGiver:
                    DisplayColor = Color.Yellow;
                    Greeting = "I could use someone with your skills...";
                    break;
                case NPCType.Settler:
                    DisplayColor = Color.LightGreen;
                    Greeting = "Nice to see a friendly face.";
                    break;
            }
        }
        
        // ============================================
        // MERCHANT FUNCTIONS
        // ============================================
        
        /// <summary>
        /// Initialize merchant with stock
        /// </summary>
        public void SetupMerchantStock(List<MerchantStock> stock)
        {
            Stock = stock;
        }
        
        /// <summary>
        /// Get sell price (what player pays to buy from merchant)
        /// </summary>
        public int GetSellPrice(string itemId)
        {
            var itemDef = ItemDatabase.Get(itemId);
            if (itemDef == null) return 0;
            
            var stock = Stock.FirstOrDefault(s => s.ItemId == itemId);
            float multiplier = stock?.PriceMultiplier ?? SellPriceMultiplier;
            
            return (int)(itemDef.BaseValue * multiplier);
        }
        
        /// <summary>
        /// Get buy price (what merchant pays player for their items)
        /// </summary>
        public int GetBuyPrice(Item item)
        {
            if (item?.Definition == null) return 0;
            
            // Base value * quality modifier * buy multiplier
            float qualityMod = item.GetQualityMultiplier();
            return (int)(item.Definition.BaseValue * qualityMod * BuyPriceMultiplier);
        }
        
        /// <summary>
        /// Check if merchant has item in stock
        /// </summary>
        public bool HasInStock(string itemId)
        {
            var stock = Stock.FirstOrDefault(s => s.ItemId == itemId);
            return stock != null && stock.Quantity > 0;
        }
        
        /// <summary>
        /// Get stock info for an item
        /// </summary>
        public MerchantStock GetStock(string itemId)
        {
            return Stock.FirstOrDefault(s => s.ItemId == itemId);
        }
        
        /// <summary>
        /// Player buys item from merchant
        /// </summary>
        public bool SellToPlayer(string itemId, int quantity, Inventory playerInventory, ref int playerGold)
        {
            var stock = GetStock(itemId);
            if (stock == null || stock.Quantity < quantity) return false;
            
            int totalPrice = GetSellPrice(itemId) * quantity;
            if (playerGold < totalPrice) return false;
            
            // Check if player can carry it
            var testItem = new Item(itemId, ItemQuality.Normal, quantity);
            if (testItem.Weight > playerInventory.FreeWeight) return false;
            
            // Transaction
            playerGold -= totalPrice;
            Gold += totalPrice;
            stock.Quantity -= quantity;
            
            // Add to player inventory
            playerInventory.TryAddItem(itemId, quantity);
            
            System.Diagnostics.Debug.WriteLine($">>> TRADE: Player bought {quantity}x {itemId} for {totalPrice} gold <<<");
            return true;
        }
        
        /// <summary>
        /// Player sells item to merchant
        /// </summary>
        public bool BuyFromPlayer(Item item, int quantity, Inventory playerInventory, ref int playerGold)
        {
            if (item == null) return false;
            
            int pricePerItem = GetBuyPrice(item);
            int totalPrice = pricePerItem * quantity;
            
            // Check merchant can afford
            if (Gold < totalPrice) return false;
            
            // Check player has enough
            if (item.StackCount < quantity) return false;
            
            // Transaction
            playerGold += totalPrice;
            Gold -= totalPrice;
            
            // Remove from player (or reduce stack)
            if (item.StackCount <= quantity)
            {
                playerInventory.RemoveItem(item);
            }
            else
            {
                item.StackCount -= quantity;
            }
            
            // Add to merchant stock (optional - merchants accumulate items)
            var existingStock = Stock.FirstOrDefault(s => s.ItemId == item.ItemDefId);
            if (existingStock != null)
            {
                existingStock.Quantity = Math.Min(existingStock.Quantity + quantity, existingStock.MaxQuantity);
            }
            
            System.Diagnostics.Debug.WriteLine($">>> TRADE: Player sold {quantity}x {item.Name} for {totalPrice} gold <<<");
            return true;
        }
        
        /// <summary>
        /// Restock merchant inventory (call periodically)
        /// </summary>
        public void Restock()
        {
            foreach (var stock in Stock)
            {
                // Slowly restock toward max
                if (stock.Quantity < stock.MaxQuantity)
                {
                    stock.Quantity = Math.Min(stock.Quantity + 1, stock.MaxQuantity);
                }
            }
        }
        
        // ============================================
        // UPDATE
        // ============================================
        
        public void Update(float deltaTime)
        {
            // Simple idle behavior - NPCs mostly stand around for now
            // Future: patrol, day/night schedules, etc.
        }
        
        // ============================================
        // FACTORY METHODS
        // ============================================
        
        /// <summary>
        /// Create a general goods merchant
        /// </summary>
        public static NPCEntity CreateGeneralMerchant(string id, Vector2 position)
        {
            var npc = new NPCEntity(id, NPCType.Merchant, "Trader")
            {
                Position = position,
                Gold = 500,
                Greeting = "Need supplies? I've got what you need."
            };
            
            npc.SetupMerchantStock(new List<MerchantStock>
            {
                // Consumables
                new MerchantStock("food_jerky", 10, 15, 1.3f),
                new MerchantStock("food_canned", 5, 10, 1.3f),
                new MerchantStock("water_purified", 8, 12, 1.2f),
                new MerchantStock("bandage", 10, 15, 1.2f),
                new MerchantStock("medkit", 3, 5, 1.5f),
                
                // Materials
                new MerchantStock("cloth", 20, 30, 1.1f),
                new MerchantStock("leather", 15, 20, 1.2f),
                new MerchantStock("wood", 25, 40, 1.1f),
                new MerchantStock("scrap_metal", 15, 25, 1.2f),
                new MerchantStock("components", 5, 10, 1.5f),
            });
            
            return npc;
        }
        
        /// <summary>
        /// Create a weapons merchant
        /// </summary>
        public static NPCEntity CreateWeaponsMerchant(string id, Vector2 position)
        {
            var npc = new NPCEntity(id, NPCType.Merchant, "Arms Dealer")
            {
                Position = position,
                Gold = 800,
                Greeting = "Looking for firepower? You've come to the right place."
            };
            
            npc.SetupMerchantStock(new List<MerchantStock>
            {
                // Weapons
                new MerchantStock("knife_rusty", 3, 5, 1.2f),
                new MerchantStock("knife_combat", 2, 3, 1.4f),
                new MerchantStock("pipe_club", 3, 5, 1.2f),
                new MerchantStock("machete", 2, 3, 1.4f),
                new MerchantStock("spear_makeshift", 4, 6, 1.1f),
                new MerchantStock("bow_simple", 2, 3, 1.3f),
                
                // Ammo
                new MerchantStock("arrow_basic", 30, 50, 1.1f),
                new MerchantStock("ammo_9mm", 20, 40, 1.3f),
                new MerchantStock("ammo_shells", 15, 25, 1.3f),
                
                // Armor
                new MerchantStock("armor_leather", 2, 3, 1.3f),
                new MerchantStock("helmet_hardhat", 2, 4, 1.2f),
            });
            
            return npc;
        }
        
        /// <summary>
        /// Create a wandering trader with random stock
        /// </summary>
        public static NPCEntity CreateWanderer(string id, Vector2 position)
        {
            var npc = new NPCEntity(id, NPCType.Wanderer, "Wandering Trader")
            {
                Position = position,
                Gold = 200,
                Greeting = "Ah, another survivor. Care to trade?",
                BuyPriceMultiplier = 0.4f,  // Pays less
                SellPriceMultiplier = 1.5f  // Charges more
            };
            
            // Random assortment
            npc.SetupMerchantStock(new List<MerchantStock>
            {
                new MerchantStock("water_dirty", 5, 5, 0.8f),
                new MerchantStock("food_mutfruit", 3, 5, 1.2f),
                new MerchantStock("cloth", 8, 10, 1.0f),
                new MerchantStock("scrap_metal", 5, 8, 1.1f),
            });
            
            return npc;
        }
    }
}
