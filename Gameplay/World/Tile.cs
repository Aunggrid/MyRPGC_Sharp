// Gameplay/World/Tile.cs
// Individual tile data with terrain properties including vision blocking for LOS

using MyRPG.Data;

namespace MyRPG.Gameplay.World
{
    public class Tile
    {
        public TileType Type { get; set; }
        
        public Tile(TileType type)
        {
            Type = type;
        }
        
        /// <summary>
        /// Can entities walk on this tile?
        /// </summary>
        public bool IsWalkable => Type switch
        {
            TileType.Grass => true,
            TileType.Dirt => true,
            TileType.Stone => true,
            TileType.Sand => true,
            TileType.Water => false,        // Shallow water - blocks movement
            TileType.DeepWater => false,    // Deep water - blocks movement
            TileType.StoneWall => false,    // Walls block movement
            _ => true
        };
        
        /// <summary>
        /// Does this tile block line of sight for ranged attacks?
        /// This is the KEY property for the LOS system!
        /// </summary>
        public bool BlocksVision => Type switch
        {
            TileType.StoneWall => true,     // Walls block vision
            _ => false                       // Most terrain doesn't block vision
        };
        
        /// <summary>
        /// Movement cost multiplier (1.0 = normal, higher = slower)
        /// </summary>
        public float MovementCost => Type switch
        {
            TileType.Grass => 1.0f,
            TileType.Dirt => 1.0f,
            TileType.Stone => 1.0f,
            TileType.Sand => 1.3f,          // Sand slows movement
            TileType.Water => 2.0f,         // If somehow walkable, very slow
            TileType.DeepWater => 3.0f,
            TileType.StoneWall => 999f,     // Impassable
            _ => 1.0f
        };
        
        /// <summary>
        /// Does this tile provide cover for ranged attacks?
        /// Returns cover value (0 = none, 0.5 = half, 1.0 = full)
        /// </summary>
        public float CoverValue => Type switch
        {
            TileType.StoneWall => 1.0f,     // Full cover (if adjacent)
            _ => 0.0f
        };
        
        /// <summary>
        /// Status effect applied when standing on this tile
        /// </summary>
        public StatusEffectType? TileEffect => Type switch
        {
            TileType.Water => StatusEffectType.Wet,
            TileType.DeepWater => StatusEffectType.Wet,
            _ => null
        };
    }
}
