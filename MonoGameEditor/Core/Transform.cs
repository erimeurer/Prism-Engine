using System.ComponentModel;
using Microsoft.Xna.Framework;
using Vector3 = Microsoft.Xna.Framework.Vector3;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace MonoGameEditor.Core
{
    /// <summary>
    /// Transform component - Position, Rotation, Scale
    /// </summary>
    public class Transform : INotifyPropertyChanged
    {
        private Vector3 _localPosition = Vector3.Zero;
        private Vector3 _localRotation = Vector3.Zero; 
        private Vector3 _localScale = Vector3.One;
        private bool _isUIExpanded = true;
        
        public GameObject? GameObject { get; set; }

        public bool IsUIExpanded
        {
            get => _isUIExpanded;
            set
            {
                if (_isUIExpanded != value)
                {
                    _isUIExpanded = value;
                    OnPropertyChanged(nameof(IsUIExpanded));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Local Position relative to Parent
        /// </summary>
        public Vector3 LocalPosition
        {
            get => _localPosition;
            set 
            { 
                _localPosition = value; 
                OnPropertyChanged(nameof(LocalPosition));
                OnPropertyChanged(nameof(Position)); // Update World Position reading
                OnPropertyChanged(nameof(PositionX));
                OnPropertyChanged(nameof(PositionY));
                OnPropertyChanged(nameof(PositionZ));
            }
        }

        /// <summary>
        /// World Position
        /// </summary>
        public Vector3 Position
        {
            get
            {
                return WorldMatrix.Translation;
            }
            set
            {
                if (GameObject?.Parent != null)
                {
                    // Convert World TO Local
                    // P_world = P_local * M_parent
                    // P_local = P_world * M_parent_inverse
                    var parentInverse = Matrix.Invert(GameObject.Parent.Transform.WorldMatrix);
                    LocalPosition = Vector3.Transform(value, parentInverse);
                }
                else
                {
                    LocalPosition = value;
                }
                OnPropertyChanged(nameof(Position));
            }
        }

        public float PositionX
        {
            get => _localPosition.X;
            set { var p = _localPosition; p.X = value; LocalPosition = p; }
        }

        public float PositionY
        {
            get => _localPosition.Y;
            set { var p = _localPosition; p.Y = value; LocalPosition = p; }
        }

        public float PositionZ
        {
            get => _localPosition.Z;
            set { var p = _localPosition; p.Z = value; LocalPosition = p; }
        }

        public Vector3 Rotation
        {
            get => LocalRotation;
            set => LocalRotation = value;
        }

        public Vector3 LocalRotation
        {
            get => _localRotation;
            set 
            { 
                _localRotation = value; 
                OnPropertyChanged(nameof(Rotation));
                OnPropertyChanged(nameof(LocalRotation));
                OnPropertyChanged(nameof(RotationX));
                OnPropertyChanged(nameof(RotationY));
                OnPropertyChanged(nameof(RotationZ));
            }
        }

        public float RotationX
        {
            get => _localRotation.X;
            set { var r = _localRotation; r.X = value; LocalRotation = r; }
        }

        public float RotationY
        {
            get => _localRotation.Y;
            set { var r = _localRotation; r.Y = value; LocalRotation = r; }
        }

        public float RotationZ
        {
            get => _localRotation.Z;
            set { var r = _localRotation; r.Z = value; LocalRotation = r; }
        }

        public Vector3 Scale
        {
            get => LocalScale;
            set => LocalScale = value;
        }

        public Vector3 LocalScale
        {
            get => _localScale;
            set 
            { 
                _localScale = value; 
                OnPropertyChanged(nameof(Scale));
                OnPropertyChanged(nameof(LocalScale));
                OnPropertyChanged(nameof(ScaleX));
                OnPropertyChanged(nameof(ScaleY));
                OnPropertyChanged(nameof(ScaleZ));
            }
        }

        public float ScaleX
        {
            get => _localScale.X;
            set { var s = _localScale; s.X = value; LocalScale = s; }
        }

        public float ScaleY
        {
            get => _localScale.Y;
            set { var s = _localScale; s.Y = value; LocalScale = s; }
        }

        public float ScaleZ
        {
            get => _localScale.Z;
            set { var s = _localScale; s.Z = value; LocalScale = s; }
        }

        /// <summary>
        /// Local transformation matrix (without parent)
        /// Used for skeletal animation bone calculations
        /// </summary>
        public Matrix LocalMatrix
        {
            get
            {
                return Matrix.CreateScale(_localScale) *
                       Matrix.CreateRotationX(MathHelper.ToRadians(_localRotation.X)) *
                       Matrix.CreateRotationY(MathHelper.ToRadians(_localRotation.Y)) *
                       Matrix.CreateRotationZ(MathHelper.ToRadians(_localRotation.Z)) *
                       Matrix.CreateTranslation(_localPosition);
            }
        }

        public Matrix WorldMatrix
        {
            get
            {
                var localMat = LocalMatrix;

                if (GameObject?.Parent != null)
                {
                    return localMat * GameObject.Parent.Transform.WorldMatrix;
                }

                return localMat;
            }
        }

        public Vector3 Forward => WorldMatrix.Forward;
        public Vector3 Backward => WorldMatrix.Backward;
        public Vector3 Up => WorldMatrix.Up;
        public Vector3 Down => WorldMatrix.Down;
        public Vector3 Right => WorldMatrix.Right;
        public Vector3 Left => WorldMatrix.Left;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
