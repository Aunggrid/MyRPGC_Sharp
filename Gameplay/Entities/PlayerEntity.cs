using Microsoft.Xna.Framework;
using System.Collections.Generic;
using MyRPG.Gameplay.World;

namespace MyRPG.Gameplay.Entities
{
    public class PlayerEntity
    {
        public Vector2 Position;
        public float Speed = 200f; // Faster speed for better testing
        public List<Point> CurrentPath = new List<Point>();
        public List<string> StatusEffects = new List<string>();

        public void Update(float deltaTime, WorldGrid grid)
        {

            // 1. MOVEMENT LOGIC
            if (CurrentPath != null && CurrentPath.Count > 0)
            {
                Point nextTile = CurrentPath[0];
                Vector2 targetPos = new Vector2(nextTile.X * grid.TileSize, nextTile.Y * grid.TileSize);

                Vector2 direction = targetPos - Position;
                if (direction != Vector2.Zero) direction.Normalize();

                // Deep Mechanic: Wet = Slower
                float currentSpeed = Speed;
                if (StatusEffects.Contains("Wet")) currentSpeed *= 0.5f; // Slow down by 50%

                Position += direction * currentSpeed * deltaTime;

                if (Vector2.Distance(Position, targetPos) < 4f)
                { // Increased "snap" distance
                    Position = targetPos; // Snap to exact grid
                    CurrentPath.RemoveAt(0);
                    CheckTileInteraction(grid, nextTile);
                }
            }
        }

        private void CheckTileInteraction(WorldGrid grid, Point tilePos)
        {
            Tile tile = grid.Tiles[tilePos.X, tilePos.Y];

            // FIX: Use 'System.Diagnostics.Debug' so it shows in Visual Studio!
            if (tile.Type == TileType.Water)
            {
                if (!StatusEffects.Contains("Wet"))
                {
                    StatusEffects.Add("Wet");
                    System.Diagnostics.Debug.WriteLine(">>> ENTERED WATER! STATUS: WET <<<");
                }
            }
            else if (tile.Type == TileType.Dirt)
            {
                if (StatusEffects.Contains("Wet"))
                {
                    StatusEffects.Remove("Wet");
                    System.Diagnostics.Debug.WriteLine(">>> BACK ON LAND. DRYING OFF. <<<");
                }
            }
        }
    }
}