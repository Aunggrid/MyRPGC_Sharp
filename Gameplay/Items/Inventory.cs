// Gameplay/Items/Inventory.cs
// Inventory management system

using System;
using System.Collections.Generic;
using System.Linq;
using MyRPG.Data;

namespace MyRPG.Gameplay.Items
{
    public class Inventory
    {
        // Storage
        private List<Item> _items = new List<Item>();
        
        // Capacity
        public int MaxSlots { get; set; } = 20;
        public float MaxWeight { get; set; } = 50f;         // kg
        
        // Current stats
        public int UsedSlots => _items.Count;
        public int FreeSlots => MaxSlots - UsedSlots;
        public float CurrentWeight => _items.Sum(i => i.Weight);
        public float FreeWeight => MaxWeight - CurrentWeight;
        public bool IsFull => UsedSlots >= MaxSlots;
        public bool IsOverweight => CurrentWeight > MaxWeight;
        
        // Equipment slots
        private Dictionary<EquipSlot, Item> _equipment = new Dictionary<EquipSlot, Item>();
        
        // Events
        public event Action<Item> OnItemAdded;
        public event Action<Item> OnItemRemoved;
        public event Action<Item, EquipSlot> OnItemEquipped;
        public event Action<Item, EquipSlot> OnItemUnequipped;
        
        public Inventory(int maxSlots = 20, float maxWeight = 50f)
        {
            MaxSlots = maxSlots;
            MaxWeight = maxWeight;
            
            // Initialize equipment slots
            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                if (slot != EquipSlot.None)
                {
                    _equipment[slot] = null;
                }
            }
        }
        
        // ============================================
        // ADD ITEMS
        // ============================================
        
        /// <summary>
        /// Try to add an item. Returns true if successful.
        /// </summary>
        public bool TryAddItem(Item item)
        {
            if (item == null) return false;
            
            // Check weight
            if (CurrentWeight + item.Weight > MaxWeight)
            {
                System.Diagnostics.Debug.WriteLine($">>> Cannot add {item.Name}: Too heavy! <<<");
                return false;
            }
            
            // Try to stack first
            if (item.IsStackable)
            {
                foreach (var existing in _items)
                {
                    if (existing.CanStackWith(item))
                    {
                        int remainder = existing.AddToStack(item.StackCount);
                        if (remainder == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($">>> Stacked {item.Name} (now x{existing.StackCount}) <<<");
                            OnItemAdded?.Invoke(item);
                            return true;
                        }
                        item.StackCount = remainder;
                    }
                }
            }
            
            // Need a new slot
            if (IsFull)
            {
                System.Diagnostics.Debug.WriteLine($">>> Cannot add {item.Name}: Inventory full! <<<");
                return false;
            }
            
            _items.Add(item);
            System.Diagnostics.Debug.WriteLine($">>> Added {item.GetDisplayName()} to inventory <<<");
            OnItemAdded?.Invoke(item);
            return true;
        }
        
        /// <summary>
        /// Add item by definition ID
        /// </summary>
        public bool TryAddItem(string itemDefId, int count = 1, ItemQuality quality = ItemQuality.Normal)
        {
            var item = new Item(itemDefId, quality, count);
            if (item.Definition == null)
            {
                System.Diagnostics.Debug.WriteLine($">>> Unknown item: {itemDefId} <<<");
                return false;
            }
            return TryAddItem(item);
        }
        
        // ============================================
        // REMOVE ITEMS
        // ============================================
        
        /// <summary>
        /// Remove a specific item instance
        /// </summary>
        public bool RemoveItem(Item item)
        {
            if (_items.Remove(item))
            {
                OnItemRemoved?.Invoke(item);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Remove item by ID (removes first match)
        /// </summary>
        public Item RemoveItem(string itemDefId, int count = 1)
        {
            var item = _items.FirstOrDefault(i => i.ItemDefId == itemDefId);
            if (item == null) return null;
            
            if (item.StackCount <= count)
            {
                _items.Remove(item);
                OnItemRemoved?.Invoke(item);
                return item;
            }
            else
            {
                var removed = item.Split(count);
                OnItemRemoved?.Invoke(removed);
                return removed;
            }
        }
        
        /// <summary>
        /// Check if inventory has at least X of an item
        /// </summary>
        public bool HasItem(string itemDefId, int count = 1)
        {
            int total = _items.Where(i => i.ItemDefId == itemDefId).Sum(i => i.StackCount);
            return total >= count;
        }
        
        /// <summary>
        /// Get total count of an item type
        /// </summary>
        public int GetItemCount(string itemDefId)
        {
            return _items.Where(i => i.ItemDefId == itemDefId).Sum(i => i.StackCount);
        }
        
        /// <summary>
        /// Clear all items and equipment
        /// </summary>
        public void Clear()
        {
            _items.Clear();
            foreach (var slot in _equipment.Keys.ToList())
            {
                _equipment[slot] = null;
            }
        }
        
        // ============================================
        // EQUIPMENT
        // ============================================
        
        /// <summary>
        /// Equip an item to its appropriate slot
        /// </summary>
        public bool EquipItem(Item item)
        {
            if (item == null) return false;
            if (item.Definition == null) return false;
            
            EquipSlot slot = item.Definition.EquipSlot;
            if (slot == EquipSlot.None)
            {
                System.Diagnostics.Debug.WriteLine($">>> {item.Name} cannot be equipped <<<");
                return false;
            }
            
            // Unequip current item in slot
            if (_equipment[slot] != null)
            {
                UnequipSlot(slot);
            }
            
            // Remove from inventory and equip
            _items.Remove(item);
            _equipment[slot] = item;
            
            System.Diagnostics.Debug.WriteLine($">>> Equipped {item.Name} to {slot} <<<");
            OnItemEquipped?.Invoke(item, slot);
            return true;
        }
        
        /// <summary>
        /// Unequip item from slot, return to inventory
        /// </summary>
        public bool UnequipSlot(EquipSlot slot)
        {
            if (slot == EquipSlot.None) return false;
            if (_equipment[slot] == null) return false;
            
            var item = _equipment[slot];
            
            // Check if inventory has room
            if (IsFull)
            {
                System.Diagnostics.Debug.WriteLine($">>> Cannot unequip {item.Name}: Inventory full! <<<");
                return false;
            }
            
            _equipment[slot] = null;
            _items.Add(item);
            
            System.Diagnostics.Debug.WriteLine($">>> Unequipped {item.Name} from {slot} <<<");
            OnItemUnequipped?.Invoke(item, slot);
            return true;
        }
        
        /// <summary>
        /// Get equipped item in slot
        /// </summary>
        public Item GetEquipped(EquipSlot slot)
        {
            return _equipment.TryGetValue(slot, out var item) ? item : null;
        }
        
        /// <summary>
        /// Get all equipped items
        /// </summary>
        public Dictionary<EquipSlot, Item> GetAllEquipped()
        {
            return new Dictionary<EquipSlot, Item>(_equipment);
        }
        
        /// <summary>
        /// Calculate total armor from equipment
        /// </summary>
        public float GetTotalArmor()
        {
            float total = 0f;
            foreach (var item in _equipment.Values)
            {
                if (item != null)
                {
                    total += item.GetEffectiveArmor();
                }
            }
            return total;
        }
        
        /// <summary>
        /// Get the currently equipped weapon
        /// </summary>
        public Item GetWeapon()
        {
            // Check main hand first, then two-hand
            var mainHand = GetEquipped(EquipSlot.MainHand);
            if (mainHand != null && mainHand.Category == ItemCategory.Weapon)
            {
                return mainHand;
            }
            
            var twoHand = GetEquipped(EquipSlot.TwoHand);
            if (twoHand != null && twoHand.Category == ItemCategory.Weapon)
            {
                return twoHand;
            }
            
            return null;
        }
        
        // ============================================
        // QUERIES
        // ============================================
        
        public List<Item> GetAllItems()
        {
            return new List<Item>(_items);
        }
        
        public List<Item> GetItemsByCategory(ItemCategory category)
        {
            return _items.Where(i => i.Category == category).ToList();
        }
        
        public Item GetItem(string itemId)
        {
            return _items.FirstOrDefault(i => i.Id == itemId);
        }
        
        public Item GetItemByDefId(string itemDefId)
        {
            return _items.FirstOrDefault(i => i.ItemDefId == itemDefId);
        }
        
        // ============================================
        // USE CONSUMABLES
        // ============================================
        
        /// <summary>
        /// Use a consumable item. Returns effects to be applied.
        /// </summary>
        public ConsumableEffects UseConsumable(Item item)
        {
            if (item == null) return null;
            if (item.Category != ItemCategory.Consumable) return null;
            if (!_items.Contains(item)) return null;
            
            var def = item.Definition;
            var effects = new ConsumableEffects
            {
                HungerRestore = def.HungerRestore,
                ThirstRestore = def.ThirstRestore,
                HealthRestore = def.HealthRestore,
                RadiationRemove = def.RadiationRemove,
                AppliesEffect = def.AppliesEffect,
                EffectDuration = def.EffectDuration
            };
            
            // Remove one from stack
            if (item.StackCount <= 1)
            {
                _items.Remove(item);
            }
            else
            {
                item.StackCount--;
            }
            
            System.Diagnostics.Debug.WriteLine($">>> Used {item.Name} <<<");
            OnItemRemoved?.Invoke(item);
            
            return effects;
        }
        
        /// <summary>
        /// Use consumable by definition ID
        /// </summary>
        public ConsumableEffects UseConsumable(string itemDefId)
        {
            var item = GetItemByDefId(itemDefId);
            return UseConsumable(item);
        }
        
        // ============================================
        // AMMO
        // ============================================
        
        /// <summary>
        /// Check if have ammo for weapon
        /// </summary>
        public bool HasAmmoFor(Item weapon)
        {
            if (weapon?.Definition?.RequiresAmmo == null) return true;
            return HasItem(weapon.Definition.RequiresAmmo, weapon.Definition.AmmoPerShot);
        }
        
        /// <summary>
        /// Consume ammo for weapon shot
        /// </summary>
        public bool ConsumeAmmo(Item weapon)
        {
            if (weapon?.Definition?.RequiresAmmo == null) return true;
            
            string ammoId = weapon.Definition.RequiresAmmo;
            int needed = weapon.Definition.AmmoPerShot;
            
            if (!HasItem(ammoId, needed)) return false;
            
            RemoveItem(ammoId, needed);
            return true;
        }
        
        // ============================================
        // SORTING
        // ============================================
        
        public void SortByCategory()
        {
            _items = _items.OrderBy(i => i.Category).ThenBy(i => i.Name).ToList();
        }
        
        public void SortByName()
        {
            _items = _items.OrderBy(i => i.Name).ToList();
        }
        
        public void SortByValue()
        {
            _items = _items.OrderByDescending(i => i.Value).ToList();
        }
        
        public void SortByWeight()
        {
            _items = _items.OrderByDescending(i => i.Weight).ToList();
        }
        
        // ============================================
        // DEBUG
        // ============================================
        
        public string GetInventoryReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== INVENTORY ===");
            report.AppendLine($"Slots: {UsedSlots}/{MaxSlots}");
            report.AppendLine($"Weight: {CurrentWeight:F1}/{MaxWeight:F1} kg");
            report.AppendLine();
            
            // Group by category
            foreach (ItemCategory cat in Enum.GetValues(typeof(ItemCategory)))
            {
                var items = GetItemsByCategory(cat);
                if (items.Count == 0) continue;
                
                report.AppendLine($"--- {cat} ---");
                foreach (var item in items)
                {
                    report.AppendLine($"  {item.GetDisplayName()} ({item.Weight:F1}kg, ${item.Value})");
                }
            }
            
            report.AppendLine();
            report.AppendLine("--- EQUIPPED ---");
            foreach (var kvp in _equipment)
            {
                if (kvp.Value != null)
                {
                    report.AppendLine($"  {kvp.Key}: {kvp.Value.GetDisplayName()}");
                }
            }
            
            return report.ToString();
        }
    }
    
    // ============================================
    // CONSUMABLE EFFECTS (returned when using item)
    // ============================================
    
    public class ConsumableEffects
    {
        public float HungerRestore { get; set; }
        public float ThirstRestore { get; set; }
        public float HealthRestore { get; set; }
        public float RadiationRemove { get; set; }
        public StatusEffectType? AppliesEffect { get; set; }
        public float EffectDuration { get; set; }
    }
}
