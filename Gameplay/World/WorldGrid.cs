using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyRPG.Engine;
using MyRPG.Data;

namespace MyRPG.Gameplay.World
{
    public class WorldGrid
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int TileSize { get; private set; } = 64;

        public Tile[,] Tiles;

        private Texture2D _pixelTexture;

        public WorldGrid(int width, int height, GraphicsDevice graphics)
        {
            Width = width;
            Height = height;
            Tiles = new Tile[width, height];

            _pixelTexture = new Texture2D(graphics, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            GenerateSimpleMap();
        }

        public bool IsInBounds(Point p)
        {
            return p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
        }
        
        public Tile GetTile(int x, int y)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
                return Tiles[x, y];
            return new Tile(TileType.StoneWall);  // Out of bounds = wall
        }
        
        public void SetTile(int x, int y, TileType type)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                Tiles[x, y] = new Tile(type);
            }
        }
        
        // ============================================
        // LINE OF SIGHT SYSTEM
        // ============================================
        
        /// <summary>
        /// Check if there is clear line of sight between two tile positions.
        /// Uses Bresenham's line algorithm. Checks BOTH terrain AND structures.
        /// </summary>
        public bool HasLineOfSight(Point from, Point to)
        {
            // Same tile = always visible
            if (from == to) return true;
            
            // Adjacent tiles = always visible (melee range)
            int dx = Math.Abs(to.X - from.X);
            int dy = Math.Abs(to.Y - from.Y);
            if (dx <= 1 && dy <= 1) return true;
            
            // Use Bresenham's line algorithm
            return TraceLine(from, to);
        }
        
        /// <summary>
        /// Check line of sight from world positions (pixels)
        /// </summary>
        public bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            Point fromTile = new Point((int)(from.X / TileSize), (int)(from.Y / TileSize));
            Point toTile = new Point((int)(to.X / TileSize), (int)(to.Y / TileSize));
            return HasLineOfSight(fromTile, toTile);
        }
        
        /// <summary>
        /// Bresenham's line algorithm to trace path between two points.
        /// Returns true if path is clear, false if blocked.
        /// </summary>
        private bool TraceLine(Point from, Point to)
        {
            int x0 = from.X;
            int y0 = from.Y;
            int x1 = to.X;
            int y1 = to.Y;
            
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            
            while (true)
            {
                // Check current tile (skip start and end tiles)
                if ((x0 != from.X || y0 != from.Y) && (x0 != to.X || y0 != to.Y))
                {
                    // Check bounds
                    if (x0 < 0 || x0 >= Width || y0 < 0 || y0 >= Height)
                        return false;
                    
                    // Check if terrain blocks vision
                    if (Tiles[x0, y0].BlocksVision)
                        return false;
                    
                    // Check if structure blocks vision
                    if (GameServices.IsInitialized && GameServices.Building != null)
                    {
                        var structure = GameServices.Building.GetStructureAt(new Point(x0, y0));
                        if (structure != null && structure.BlocksVision)
                            return false;
                    }
                }
                
                // Reached target
                if (x0 == x1 && y0 == y1) break;
                
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Get all tiles visible from a position within a given range.
        /// Useful for fog of war, enemy detection, etc.
        /// </summary>
        public List<Point> GetVisibleTiles(Point from, int range)
        {
            var visible = new List<Point>();
            
            for (int x = from.X - range; x <= from.X + range; x++)
            {
                for (int y = from.Y - range; y <= from.Y + range; y++)
                {
                    if (!IsInBounds(new Point(x, y))) continue;
                    
                    // Check if within range (Chebyshev distance)
                    int dist = Math.Max(Math.Abs(x - from.X), Math.Abs(y - from.Y));
                    if (dist > range) continue;
                    
                    // Check line of sight
                    if (HasLineOfSight(from, new Point(x, y)))
                    {
                        visible.Add(new Point(x, y));
                    }
                }
            }
            
            return visible;
        }
        
        /// <summary>
        /// Check if a target has cover from an attacker.
        /// Returns cover value (0 = no cover, 0.5 = half cover, 1.0 = full cover)
        /// Cover is determined by structures/terrain adjacent to the target on the attacker's side.
        /// </summary>
        public float GetCoverValue(Point attacker, Point target)
        {
            // Direction from attacker to target
            int dx = Math.Sign(target.X - attacker.X);
            int dy = Math.Sign(target.Y - attacker.Y);
            
            float maxCover = 0f;
            
            // Check tiles on the attacker's side of the target
            // For diagonal attacks, check both adjacent cardinal directions
            var coverPositions = new List<Point>();
            
            if (dx != 0) coverPositions.Add(new Point(target.X - dx, target.Y));
            if (dy != 0) coverPositions.Add(new Point(target.X, target.Y - dy));
            if (dx != 0 && dy != 0) coverPositions.Add(new Point(target.X - dx, target.Y - dy));
            
            foreach (var coverPos in coverPositions)
            {
                if (!IsInBounds(coverPos)) continue;
                
                // Check terrain cover
                float tileCover = Tiles[coverPos.X, coverPos.Y].CoverValue;
                maxCover = Math.Max(maxCover, tileCover);
                
                // Check structure cover
                if (GameServices.IsInitialized && GameServices.Building != null)
                {
                    var structure = GameServices.Building.GetStructureAt(coverPos);
                    if (structure != null && structure.IsFunctional)
                    {
                        maxCover = Math.Max(maxCover, structure.Definition.CoverValue);
                    }
                }
            }
            
            return Math.Clamp(maxCover, 0f, 1f);
        }

        private void GenerateSimpleMap()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Tiles[x, y] = new Tile(TileType.Dirt);

                    // Create a river
                    if (x > 10 && x < 15) Tiles[x, y] = new Tile(TileType.Water);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Vector2 pos = new Vector2(x * TileSize, y * TileSize);
                    
                    Color color = Tiles[x, y].Type switch
                    {
                        TileType.Grass => Color.DarkGreen,
                        TileType.Dirt => Color.SaddleBrown,
                        TileType.Stone => Color.DarkGray,
                        TileType.Sand => Color.SandyBrown,
                        TileType.Water => Color.Blue,
                        TileType.DeepWater => Color.DarkBlue,
                        TileType.StoneWall => Color.Gray,
                        _ => Color.SaddleBrown
                    };

                    // Draw Tile (slightly smaller to create grid effect)
                    int borderSize = 1;
                    Rectangle tileRect = new Rectangle(
                        (int)pos.X + borderSize, 
                        (int)pos.Y + borderSize, 
                        TileSize - borderSize * 2, 
                        TileSize - borderSize * 2
                    );
                    
                    // Draw background (grid lines)
                    spriteBatch.Draw(_pixelTexture, 
                        new Rectangle((int)pos.X, (int)pos.Y, TileSize, TileSize), 
                        Color.Black * 0.4f);
                    
                    // Draw tile on top
                    spriteBatch.Draw(_pixelTexture, tileRect, color);
                }
            }
        }
    }
}
