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

    /// <summary>
    /// Physics Body component (Rigidbody equivalent)
    /// </summary>
    public class PhysicsBodyComponent : Component
    {
        private float _mass = 1.0f;
        private float _drag = 0.0f;
        private float _angularDrag = 0.05f;
        private bool _useGravity = true;
        private bool _isKinematic = false;

        // Position constraints
        private bool _freezePositionX = false;
        private bool _freezePositionY = false;
        private bool _freezePositionZ = false;

        // Rotation constraints
        private bool _freezeRotationX = false;
        private bool _freezeRotationY = false;
        private bool _freezeRotationZ = false;

        public Vector3 Velocity { get; set; } = Vector3.Zero;
        public Vector3 AngularVelocity { get; set; } = Vector3.Zero;

        public override string ComponentName => "Physics Body";

        public float Mass
        {
            get => _mass;
            set { _mass = Math.Max(0.0001f, value); OnPropertyChanged(nameof(Mass)); }
        }

        public float Drag
        {
            get => _drag;
            set { _drag = Math.Max(0, value); OnPropertyChanged(nameof(Drag)); }
        }

        public float AngularDrag
        {
            get => _angularDrag;
            set { _angularDrag = Math.Max(0, value); OnPropertyChanged(nameof(AngularDrag)); }
        }

        public bool UseGravity
        {
            get => _useGravity;
            set { _useGravity = value; OnPropertyChanged(nameof(UseGravity)); }
        }

        public bool IsKinematic
        {
            get => _isKinematic;
            set { _isKinematic = value; OnPropertyChanged(nameof(IsKinematic)); }
        }

        public bool FreezePositionX
        {
            get => _freezePositionX;
            set { _freezePositionX = value; OnPropertyChanged(nameof(FreezePositionX)); }
        }

        public bool FreezePositionY
        {
            get => _freezePositionY;
            set { _freezePositionY = value; OnPropertyChanged(nameof(FreezePositionY)); }
        }

        public bool FreezePositionZ
        {
            get => _freezePositionZ;
            set { _freezePositionZ = value; OnPropertyChanged(nameof(FreezePositionZ)); }
        }

        public bool FreezeRotationX
        {
            get => _freezeRotationX;
            set { _freezeRotationX = value; OnPropertyChanged(nameof(FreezeRotationX)); }
        }

        public bool FreezeRotationY
        {
            get => _freezeRotationY;
            set { _freezeRotationY = value; OnPropertyChanged(nameof(FreezeRotationY)); }
        }

        public bool FreezeRotationZ
        {
            get => _freezeRotationZ;
            set { _freezeRotationZ = value; OnPropertyChanged(nameof(FreezeRotationZ)); }
        }
    }
}
