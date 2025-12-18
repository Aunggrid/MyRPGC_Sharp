// Gameplay/World/Tile.cs
// Represents a single tile in the world grid

using MyRPG.Data;

namespace MyRPG.Gameplay.World
{
    public class Tile
    {
        public TileType Type { get; set; }
        public bool IsWalkable { get; set; }
        public bool BlocksVision { get; set; }
        public float MovementCost { get; set; }
        
        public Tile(TileType type)
        {
            Type = type;
            
            // Set default properties based on type
            switch (type)
            {
                case TileType.Grass:
                case TileType.Dirt:
                case TileType.Sand:
                    IsWalkable = true;
                    BlocksVision = false;
                    MovementCost = 1.0f;
                    break;
                    
                case TileType.Stone:
                    IsWalkable = true;
                    BlocksVision = false;
                    MovementCost = 1.0f;
                    break;
                    
                case TileType.Water:
                    IsWalkable = true;  // Player can wade through
                    BlocksVision = false;
                    MovementCost = 2.0f;  // Slower in water
                    break;
                    
                case TileType.DeepWater:
                    IsWalkable = false;  // Can't walk in deep water
                    BlocksVision = false;
                    MovementCost = 5.0f;
                    break;
                    
                case TileType.StoneWall:
                    IsWalkable = false;
                    BlocksVision = true;
                    MovementCost = 999f;
                    break;
                    
                default:
                    IsWalkable = true;
                    BlocksVision = false;
                    MovementCost = 1.0f;
                    break;
            }
        }
    }
}
