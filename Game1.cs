using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using MyRPG.Engine;
using MyRPG.Gameplay.World;
using MyRPG.Gameplay.Entities;
using MyRPG.Gameplay.Systems;

namespace MyRPG
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Camera2D _camera;
        private WorldGrid _world;
        private PlayerEntity _player;

        // --- NEW ASSETS ---
        private Texture2D _pixelTexture;
        private SpriteFont _font; // <--- Store the font here

        // Mouse State for clicking
        private MouseState _prevMouseState;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
        }

        protected override void Initialize()
        {
            _camera = new Camera2D(GraphicsDevice.Viewport);
            _camera.Position = new Vector2(400, 400);

            _world = new WorldGrid(50, 50, GraphicsDevice);

            _player = new PlayerEntity();
            // Spawn at 2,2
            _player.Position = new Vector2(2 * _world.TileSize, 2 * _world.TileSize);
            _player.Speed = 200f;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            // --- LOAD THE FONT ---
            // Make sure you named it "SystemFont" in the MGCB Editor!
            _font = Content.Load<SpriteFont>("SystemFont");
        }

        protected override void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();

            // 1. Camera
            float camSpeed = 500f * deltaTime;
            var kState = Keyboard.GetState();
            if (kState.IsKeyDown(Keys.W)) _camera.Position.Y -= camSpeed;
            if (kState.IsKeyDown(Keys.S)) _camera.Position.Y += camSpeed;
            if (kState.IsKeyDown(Keys.A)) _camera.Position.X -= camSpeed;
            if (kState.IsKeyDown(Keys.D)) _camera.Position.X += camSpeed;
            if (kState.IsKeyDown(Keys.Q)) _camera.Zoom -= 1f * deltaTime;
            if (kState.IsKeyDown(Keys.E)) _camera.Zoom += 1f * deltaTime;

            // 2. Player Movement
            MouseState currentMouse = Mouse.GetState();
            if (currentMouse.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                Vector2 worldPos = _camera.ScreenToWorld(new Vector2(currentMouse.X, currentMouse.Y));
                Point endTile = new Point((int)(worldPos.X / _world.TileSize), (int)(worldPos.Y / _world.TileSize));
                Point startTile = new Point((int)(_player.Position.X / _world.TileSize), (int)(_player.Position.Y / _world.TileSize));

                var path = Pathfinder.FindPath(_world, startTile, endTile);
                if (path != null) _player.CurrentPath = path;
            }
            _prevMouseState = currentMouse;

            // 3. Lightning Test
            if (kState.IsKeyDown(Keys.Space))
            {
                if (_player.StatusEffects.Contains("Wet") && !_player.StatusEffects.Contains("Stunned"))
                {
                    _player.StatusEffects.Add("Stunned");
                }
            }

            _player.Update(deltaTime, _world);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // --- LAYER 1: WORLD (Affected by Camera) ---
            _spriteBatch.Begin(transformMatrix: _camera.GetViewMatrix(), samplerState: SamplerState.PointClamp);

            _world.Draw(_spriteBatch);

            // Draw Player
            Color playerColor = Color.Green;
            if (_player.StatusEffects.Contains("Wet")) playerColor = Color.Cyan;
            if (_player.StatusEffects.Contains("Stunned")) playerColor = Color.Yellow;

            // Center Offset Calculation
            int offset = (_world.TileSize - 32) / 2;
            Rectangle playerRect = new Rectangle((int)_player.Position.X + offset, (int)_player.Position.Y + offset, 32, 32);
            _spriteBatch.Draw(_pixelTexture, playerRect, playerColor);

            // [NEW] FLOATING TEXT (Baldur's Gate Style)
            // We draw this inside the World Batch so it stays attached to the player's head
            if (_player.StatusEffects.Count > 0)
            {
                string statusText = string.Join(" + ", _player.StatusEffects); // "Wet + Stunned"
                Vector2 textSize = _font.MeasureString(statusText);

                // Position text above player's head
                Vector2 textPos = new Vector2(
                    _player.Position.X + offset - (textSize.X / 2) + 16, // Center horizontally
                    _player.Position.Y - 20 // 20 pixels above
                );

                // Draw black shadow for readability
                _spriteBatch.DrawString(_font, statusText, textPos + new Vector2(2, 2), Color.Black);
                // Draw white text
                _spriteBatch.DrawString(_font, statusText, textPos, Color.White);
            }

            _spriteBatch.End();


            // --- LAYER 2: UI (Static Screen) ---
            // Notice: No transformMatrix here! This draws directly to the screen glass.
            _spriteBatch.Begin();

            // Draw a simple "Hunger/Status Bar" at the bottom left
            _spriteBatch.Draw(_pixelTexture, new Rectangle(10, 680, 200, 30), Color.DarkGray); // Background
            _spriteBatch.Draw(_pixelTexture, new Rectangle(12, 682, 196, 26), Color.Green);   // Health Bar

            _spriteBatch.DrawString(_font, "Health: 100%", new Vector2(20, 685), Color.White);
            _spriteBatch.DrawString(_font, "Controls: Click to Move | Space on Water to Shock", new Vector2(10, 10), Color.Yellow);

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}