using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyRPG.Engine;

namespace MyRPG.Gameplay.World
{
    public class WorldGrid
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int TileSize { get; private set; } = 64;

        // --- FIX 1: Change 'private' to 'public' and rename '_tiles' to 'Tiles' ---
        public Tile[,] Tiles;

        private Texture2D _pixelTexture;

        public WorldGrid(int width, int height, GraphicsDevice graphics)
        {
            Width = width;
            Height = height;
            Tiles = new Tile[width, height]; // Changed from _tiles to Tiles

            _pixelTexture = new Texture2D(graphics, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            GenerateSimpleMap();
        }

        // --- FIX 2: Add this helper method that Pathfinder.cs is looking for ---
        public bool IsInBounds(Point p)
        {
            return p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
        }

        private void GenerateSimpleMap()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    // Use 'Tiles' instead of '_tiles'
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
                    Color color = Color.SaddleBrown;

                    // Use 'Tiles' instead of '_tiles'
                    if (Tiles[x, y].Type == TileType.Water) color = Color.Blue;
                    if (Tiles[x, y].Type == TileType.StoneWall) color = Color.Gray;

                    // Draw Tile
                    spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X, (int)pos.Y, TileSize, TileSize), color);

                    // Draw Border
                    spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X, (int)pos.Y, TileSize, 1), Color.Black * 0.3f);
                    spriteBatch.Draw(_pixelTexture, new Rectangle((int)pos.X, (int)pos.Y, 1, TileSize), Color.Black * 0.3f);
                }
            }
        }
    }
}