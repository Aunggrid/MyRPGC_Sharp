using Microsoft.Xna.Framework;          // <--- Fixes Vector2 error
using Microsoft.Xna.Framework.Graphics; // <--- Fixes SpriteBatch error
using Microsoft.Xna.Framework.Input;    // <--- Fixes Keyboard error
using System.Collections.Generic;       // <--- Fixes List<> error

// --- THESE ARE THE CRITICAL CONNECTIONS ---
using MyRPG.Engine;           // <--- Connects to Engine/Camera2D.cs
using MyRPG.Gameplay.World;   // <--- Connects to Gameplay/World/WorldGrid.cs

namespace MyRPG
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        // Custom Engine Classes
        private Camera2D _camera;
        private WorldGrid _world;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // Setup Camera
            _camera = new Camera2D(GraphicsDevice.Viewport);
            _camera.Position = new Vector2(400, 400);

            // Setup World
            _world = new WorldGrid(50, 50, GraphicsDevice);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();

            // Camera Movement Controls (WASD)
            float camSpeed = 500f * (float)gameTime.ElapsedGameTime.TotalSeconds;
            var kState = Keyboard.GetState();

            if (kState.IsKeyDown(Keys.W)) _camera.Position.Y -= camSpeed;
            if (kState.IsKeyDown(Keys.S)) _camera.Position.Y += camSpeed;
            if (kState.IsKeyDown(Keys.A)) _camera.Position.X -= camSpeed;
            if (kState.IsKeyDown(Keys.D)) _camera.Position.X += camSpeed;

            // Zoom Controls (Q/E)
            if (kState.IsKeyDown(Keys.Q)) _camera.Zoom -= 1f * (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (kState.IsKeyDown(Keys.E)) _camera.Zoom += 1f * (float)gameTime.ElapsedGameTime.TotalSeconds;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // Draw everything through the Camera
            _spriteBatch.Begin(transformMatrix: _camera.GetViewMatrix());

            _world.Draw(_spriteBatch);

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}