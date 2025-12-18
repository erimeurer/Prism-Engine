using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace MonoGameEditor.Controls
{
    /// <summary>
    /// Free-fly camera for editor scene navigation
    /// </summary>
    public class EditorCamera
    {
        // Camera transform
        public Vector3 Position { get; set; } = new Vector3(10, 10, 10);
        
        // Rotation angles (in radians)
        private float _yaw = MathHelper.ToRadians(-135); // Looking toward origin from (10,10,10)
        private float _pitch = MathHelper.ToRadians(-30);
        
        // Movement settings
        public float MoveSpeed { get; set; } = 10f;
        public float MouseSensitivity { get; set; } = 0.003f;
        
        // Projection settings
        public float FieldOfView { get; set; } = MathHelper.ToRadians(60);
        public float NearPlane { get; set; } = 0.1f;
        public float FarPlane { get; set; } = 1000f;
        public float AspectRatio { get; set; } = 16f / 9f;

        // Computed vectors
        public Vector3 Forward { get; private set; }
        public Vector3 Right { get; private set; }
        public Vector3 Up { get; private set; } = Vector3.Up;

        public Matrix View { get; private set; }
        public Matrix Projection { get; private set; }

        public EditorCamera()
        {
            UpdateVectors();
        }

        public void UpdateAspectRatio(int width, int height)
        {
            if (height > 0)
            {
                AspectRatio = (float)width / height;
                Projection = Matrix.CreatePerspectiveFieldOfView(FieldOfView, AspectRatio, NearPlane, FarPlane);
            }
        }

        /// <summary>
        /// Rotate camera with mouse delta
        /// </summary>
        public void Rotate(float deltaX, float deltaY)
        {
            _yaw += deltaX * MouseSensitivity;
            _pitch -= deltaY * MouseSensitivity;
            
            // Clamp pitch to avoid gimbal lock
            _pitch = MathHelper.Clamp(_pitch, MathHelper.ToRadians(-89f), MathHelper.ToRadians(89f));
            
            UpdateVectors();
        }

        /// <summary>
        /// Move camera (WASD style)
        /// </summary>
        public void Move(float forward, float right, float up, float deltaTime)
        {
            Vector3 movement = Vector3.Zero;
            
            movement += Forward * forward;
            movement += Right * right;
            movement += Vector3.Up * up;
            
            if (movement.LengthSquared() > 0)
            {
                movement.Normalize();
                Position += movement * MoveSpeed * deltaTime;
            }
            
            UpdateVectors();
        }

        public void RefreshProjection()
        {
            if (AspectRatio > 0)
            {
                Projection = Matrix.CreatePerspectiveFieldOfView(FieldOfView, AspectRatio, NearPlane, FarPlane);
            }
        }

        private void UpdateVectors()
        {
            // Calculate forward vector from yaw and pitch
            Forward = new Vector3(
                (float)(Math.Cos(_pitch) * Math.Cos(_yaw)),
                (float)Math.Sin(_pitch),
                (float)(Math.Cos(_pitch) * Math.Sin(_yaw))
            );
            Forward = Vector3.Normalize(Forward);
            
            // Calculate right and up
            Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.Up));
            Up = Vector3.Normalize(Vector3.Cross(Right, Forward));
            
            // Update view matrix
            View = Matrix.CreateLookAt(Position, Position + Forward, Vector3.Up);
            RefreshProjection();
        }

        // For OrientationGizmo compatibility
        public Vector3 Target => Position + Forward;

        public Ray GetRay(Vector2 mousePosition, Viewport viewport)
        {
            Vector3 nearPoint = viewport.Unproject(new Vector3(mousePosition.X, mousePosition.Y, 0), Projection, View, Matrix.Identity);
            Vector3 farPoint = viewport.Unproject(new Vector3(mousePosition.X, mousePosition.Y, 1), Projection, View, Matrix.Identity);
            Vector3 direction = farPoint - nearPoint;
            direction.Normalize();
            return new Ray(nearPoint, direction);
        }

        public void Pan(float deltaX, float deltaY)
        {
            Vector3 move = -Right * deltaX * MouseSensitivity * MoveSpeed * 2.5f + Up * deltaY * MouseSensitivity * MoveSpeed * 2.5f;
            Position += move;
            UpdateVectors();
        }

        public void Focus(Vector3 targetPosition, float distance = 5.0f)
        {
            Position = targetPosition - Forward * distance;
            UpdateVectors();
        }

        public void Zoom(float delta)
        {
            // Delta is usually +/- 120 per notch. Scale it down.
            // Move along Forward vector
            float speed = MoveSpeed * 0.1f; // Zoom speed multiplier
            Position += Forward * delta * 0.01f * speed;
            UpdateVectors();
        }
    }
}
