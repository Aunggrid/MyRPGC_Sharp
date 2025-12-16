public class PlayerEntity {
    public Vector2 Position; // Pixel position
    public float Speed = 100f; // Pixels per second
    public List<Point> CurrentPath = new List<Point>();
    public List<string> StatusEffects = new List<string>();

    public void Update(float deltaTime, WorldGrid grid) {
        
        // 1. MOVEMENT LOGIC
        if (CurrentPath != null && CurrentPath.Count > 0) {
            // Get the next target tile center
            Point nextTile = CurrentPath[0];
            Vector2 targetPos = new Vector2(nextTile.X * grid.TileSize, nextTile.Y * grid.TileSize);

            // Move towards it
            Vector2 direction = targetPos - Position;
            direction.Normalize();
            
            // APPLY SPEED MODIFIER (Deep Mechanic!)
            float currentSpeed = Speed;
            if (StatusEffects.Contains("Wet")) currentSpeed *= 0.9f; // Slow down
            
            Position += direction * currentSpeed * deltaTime;

            // Check if we arrived at the tile (distance is small)
            if (Vector2.Distance(Position, targetPos) < 2f) {
                CurrentPath.RemoveAt(0); // Remove this step, go to next
                
                // 2. INTERACTION LOGIC (Trigger when entering a tile)
                CheckTileInteraction(grid, nextTile);
            }
        }
    }

    private void CheckTileInteraction(WorldGrid grid, Point tilePos) {
        Tile tile = grid.Tiles[tilePos.X, tilePos.Y];

        // "Rimworld" Logic: The environment changes the pawn
        if (tile.Type == TileType.Water) {
            if (!StatusEffects.Contains("Wet")) {
                StatusEffects.Add("Wet");
                System.Console.WriteLine("Entered Water! Status: WET applied.");
            }
        }
        else if (tile.Type == TileType.Dirt) {
            // Maybe dry off?
             if (StatusEffects.Contains("Wet")) {
                 StatusEffects.Remove("Wet"); // Simple drying logic
                 System.Console.WriteLine("Back on land. Drying off.");
             }
        }
    }
}