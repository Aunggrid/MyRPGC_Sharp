using System;
using Microsoft.Xna.Framework;          // <--- Fixes Vector2 error
using Microsoft.Xna.Framework.Graphics; // <--- Fixes Texture2D error
using MyRPG.Engine;                     // <--- Fixes Camera access

namespace MyRPG.Gameplay.World            // <--- Matches your folder "Gameplay/World"
{
    public class WorldGrid
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int TileSize { get; private set; } = 64;

        private Tile[,] _tiles;
        private Texture2D _pixelTexture;

        public WorldGrid(int width, int height, GraphicsDevice graphics)
        {
            Width = width;
            Height = height;
            _tiles = new Tile[width, height];

            // Create a white square for drawing
            _pixelTexture = new Texture2D(graphics, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            GenerateSimpleMap();
        }

        private void GenerateSimpleMap()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    _tiles[x, y] = new Tile(TileType.Dirt);

                    // Create a river
                    if (x > 10 && x < 15) _tiles[x, y] = new Tile(TileType.Water);
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
                    Color color = Color.SaddleBrown; // Default Dirt

                    // Color logic based on TileType
                    if (_tiles[x, y].Type == TileType.Water) color = Color.Blue;
                    if (_tiles[x, y].Type == TileType.StoneWall) color = Color.Gray;

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