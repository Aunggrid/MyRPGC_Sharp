using System;
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
