using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MyRPG.Engine
{
    public class Camera2D
    {
        public Vector2 Position;
        public float Zoom { get; set; } = 1.0f;
        public float Rotation { get; set; } = 0.0f;

        private Viewport _viewport;

        public Camera2D(Viewport viewport)
        {
            _viewport = viewport;
            Position = Vector2.Zero;
        }

        // The "Math" that tells the SpriteBatch where to draw
        public Matrix GetViewMatrix()
        {
            // FIX: Round the position to integers to prevent "shimmering"
            Vector2 roundedPos = new Vector2((int)Position.X, (int)Position.Y);

            return Matrix.CreateTranslation(new Vector3(-roundedPos, 0.0f)) *
                   Matrix.CreateRotationZ(Rotation) *
                   Matrix.CreateScale(new Vector3(Zoom, Zoom, 1.0f)) *
                   Matrix.CreateTranslation(new Vector3(_viewport.Width * 0.5f, _viewport.Height * 0.5f, 0.0f));
        }

        // Helper: Convert Mouse Click (Screen) -> Game World (Grid)
        public Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return Vector2.Transform(screenPosition, Matrix.Invert(GetViewMatrix()));
        }
    }
}