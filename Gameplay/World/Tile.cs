using Microsoft.Xna.Framework; // Fixes Vector2/Point errors

namespace MyRPG.Gameplay.World // CHANGED NAMESPACE
{
    public enum TileType
    {
        Dirt,
        Grass,
        Water,
        StoneWall
    }

    public struct Tile
    {
        public TileType Type;
        public bool IsWalkable;
        public float MovementCost;

        public Tile(TileType type)
        {
            Type = type;
            MovementCost = 1.0f;
            IsWalkable = true;

            switch (type)
            {
                case TileType.Water:
                    MovementCost = 2.0f;
                    break;
                case TileType.StoneWall:
                    IsWalkable = false;
                    break;
            }
        }
    }
}