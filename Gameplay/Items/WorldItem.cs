// Gameplay/Items/WorldItem.cs
// Represents an item dropped in the world that can be picked up

using Microsoft.Xna.Framework;
using MyRPG.Data;

namespace MyRPG.Gameplay.Items
{
    /// <summary>
    /// An item that exists in the game world (dropped loot, placed items)
    /// </summary>
    public class WorldItem
    {
        // The actual item
        public Item Item { get; set; }
        
        // World position
        public Vector2 Position { get; set; }
        
        // Visual properties
        public float BobOffset { get; set; } = 0f;  // For floating animation
        public float SpawnTime { get; set; }         // When item was dropped
        public float LifeTime { get; set; } = -1f;   // -1 = permanent, otherwise despawn timer
        
        // Interaction
        public bool CanPickup { get; set; } = true;
        public float PickupRadius { get; set; } = 48f;  // How close player needs to be
        
        // Unique ID for tracking
        public string Id { get; private set; }
        
        private static int _nextId = 0;
        
        public WorldItem(Item item, Vector2 position)
        {
            Item = item;
            Position = position;
            Id = $"WorldItem_{_nextId++}";
            SpawnTime = 0f; // Will be set by caller if needed
        }
        
        /// <summary>
        /// Create a world item from item definition
        /// </summary>
        public static WorldItem Create(string itemDefId, Vector2 position, int count = 1, ItemQuality quality = ItemQuality.Normal)
        {
            var item = new Item(itemDefId, quality, count);
            return new WorldItem(item, position);
        }
        
        /// <summary>
        /// Get the tile position of this item
        /// </summary>
        public Point GetTilePosition(int tileSize)
        {
            return new Point((int)(Position.X / tileSize), (int)(Position.Y / tileSize));
        }
        
        /// <summary>
        /// Check if player is within pickup range
        /// </summary>
        public bool IsInPickupRange(Vector2 playerPosition)
        {
            return Vector2.Distance(Position, playerPosition) <= PickupRadius;
        }
        
        /// <summary>
        /// Get display text for this item
        /// </summary>
        public string GetDisplayText()
        {
            return Item?.GetDisplayName() ?? "Unknown Item";
        }
    }
}
