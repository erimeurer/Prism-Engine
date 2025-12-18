using System;
using Microsoft.Xna.Framework;

namespace MonoGameEditor.Core.Components
{
    /// <summary>
    /// Base class for all collider components
    /// </summary>
    public abstract class ColliderComponent : Component
    {
        private bool _isTrigger = false;
        private Vector3 _center = Vector3.Zero;

        public bool IsTrigger
        {
            get => _isTrigger;
            set { _isTrigger = value; OnPropertyChanged(nameof(IsTrigger)); }
        }

        public Vector3 Center
        {
            get => _center;
            set 
            { 
                _center = value; 
                OnPropertyChanged(nameof(Center)); 
                OnPropertyChanged(nameof(CenterX));
                OnPropertyChanged(nameof(CenterY));
                OnPropertyChanged(nameof(CenterZ));
            }
        }

        public float CenterX
        {
            get => _center.X;
            set { var c = _center; c.X = value; Center = c; }
        }

        public float CenterY
        {
            get => _center.Y;
            set { var c = _center; c.Y = value; Center = c; }
        }

        public float CenterZ
        {
            get => _center.Z;
            set { var c = _center; c.Z = value; Center = c; }
        }
    }

    /// <summary>
    /// Box-shaped collider
    /// </summary>
    public class BoxColliderComponent : ColliderComponent
    {
        private Vector3 _size = Vector3.One;
        public override string ComponentName => "Box Collider";

        public Vector3 Size
        {
            get => _size;
            set 
            { 
                _size = value; 
                OnPropertyChanged(nameof(Size)); 
                OnPropertyChanged(nameof(SizeX));
                OnPropertyChanged(nameof(SizeY));
                OnPropertyChanged(nameof(SizeZ));
            }
        }

        public float SizeX
        {
            get => _size.X;
            set { var s = _size; s.X = value; Size = s; }
        }

        public float SizeY
        {
            get => _size.Y;
            set { var s = _size; s.Y = value; Size = s; }
        }

        public float SizeZ
        {
            get => _size.Z;
            set { var s = _size; s.Z = value; Size = s; }
        }
    }

    /// <summary>
    /// Sphere-shaped collider
    /// </summary>
    public class SphereColliderComponent : ColliderComponent
    {
        private float _radius = 0.5f;
        public override string ComponentName => "Sphere Collider";

        public float Radius
        {
            get => _radius;
            set { _radius = value; OnPropertyChanged(nameof(Radius)); }
        }
    }

    /// <summary>
    /// Direction options for capsule collider
    /// </summary>
    public enum CapsuleDirection
    {
        X_Axis = 0,
        Y_Axis = 1,
        Z_Axis = 2
    }

    /// <summary>
    /// Capsule-shaped collider
    /// </summary>
    public class CapsuleColliderComponent : ColliderComponent
    {
        private float _radius = 0.5f;
        private float _height = 2.0f;
        private CapsuleDirection _direction = CapsuleDirection.Y_Axis;

        public override string ComponentName => "Capsule Collider";

        public float Radius
        {
            get => _radius;
            set { _radius = value; OnPropertyChanged(nameof(Radius)); }
        }

        public float Height
        {
            get => _height;
            set { _height = value; OnPropertyChanged(nameof(Height)); }
        }

        public CapsuleDirection Direction
        {
            get => _direction;
            set { _direction = value; OnPropertyChanged(nameof(Direction)); }
        }
    }
}
