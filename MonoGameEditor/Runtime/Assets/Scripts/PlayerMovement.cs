using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;

namespace MyGame.Scripts
{
    public class PlayerMovement : ScriptComponent
    {
        public float Speed = 5.0f;

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Vector3 move = Vector3.Zero;

            if (Input.GetKey(Keys.W)) move += transform.Forward;
            if (Input.GetKey(Keys.S)) move += transform.Backward;
            if (Input.GetKey(Keys.A)) move += transform.Left;
            if (Input.GetKey(Keys.D)) move += transform.Right;

            if (move != Vector3.Zero)
            {
                move.Normalize();
                transform.Position += move * Speed * dt;
            }
        }
    }
}
