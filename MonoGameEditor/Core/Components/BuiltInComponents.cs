using Microsoft.Xna.Framework;

namespace MonoGameEditor.Core.Components
{
    /// <summary>
    /// Camera component - makes this GameObject act as a camera
    /// </summary>
    public class CameraComponent : Component
    {
        private float _fieldOfView = 60f;
        private float _nearClip = 0.1f;
        private float _farClip = 1000f;
        private bool _isMainCamera = true;
        private Color _backgroundColor = Color.CornflowerBlue;
        private CameraClearFlags _clearFlags = CameraClearFlags.Skybox;

        public override string ComponentName => "Camera";

        public CameraClearFlags ClearFlags
        {
             get => _clearFlags;
             set { _clearFlags = value; OnPropertyChanged(nameof(ClearFlags)); }
        }

        public float FieldOfView
        {
            get => _fieldOfView;
            set { _fieldOfView = MathHelper.Clamp(value, 1f, 179f); OnPropertyChanged(nameof(FieldOfView)); }
        }

        public float NearClip
        {
            get => _nearClip;
            set { _nearClip = MathHelper.Max(0.01f, value); OnPropertyChanged(nameof(NearClip)); }
        }

        public float FarClip
        {
            get => _farClip;
            set { _farClip = MathHelper.Max(_nearClip + 1f, value); OnPropertyChanged(nameof(FarClip)); }
        }

        public bool IsMainCamera
        {
            get => _isMainCamera;
            set 
            {
                if (_isMainCamera != value)
                {
                    _isMainCamera = value;
                    OnPropertyChanged(nameof(IsMainCamera));
                    
                    // If this became main, uncheck others. 
                    // Note: This naive approach requires access to scene, 
                    // but Component shouldn't know global state directly.
                    // Ideally handled by SceneManager, but for now we'll
                    // use a static event to notify others.
                    if (_isMainCamera)
                        NotifyMainCameraChanged(this);
                }
            }
        }
        
        public static event Action<CameraComponent>? MainCameraChanged;
        
        private static void NotifyMainCameraChanged(CameraComponent newMain)
        {
            MainCameraChanged?.Invoke(newMain);
        }
        
        public CameraComponent()
        {
            MainCameraChanged += OnMainCameraChanged;
        }

        private void OnMainCameraChanged(CameraComponent newMain)
        {
            if (newMain != this && IsMainCamera)
            {
                IsMainCamera = false;
            }
        }

        public Color BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; OnPropertyChanged(nameof(BackgroundColor)); }
        }

    }

    /// <summary>
    /// Light component - makes this GameObject act as a light source
    /// </summary>
    public class LightComponent : Component
    {
        private LightType _lightType = LightType.Directional;
        private Color _color = Color.White;
        private float _intensity = 1f;
        private float _range = 10f;
        private float _spotAngle = 30f;
        private bool _castShadows = true;
        private float _temperature = 6500f; // Kelvin
        private float _indirectMultiplier = 1f;

        public override string ComponentName => "Light";

        public LightType LightType
        {
            get => _lightType;
            set { _lightType = value; OnPropertyChanged(nameof(LightType)); }
        }

        public Color Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(nameof(Color)); }
        }

        public float Intensity
        {
            get => _intensity;
            set { _intensity = MathHelper.Max(0f, value); OnPropertyChanged(nameof(Intensity)); }
        }
        
        /// <summary>
        /// Color temperature in Kelvin (default 6500K).
        /// Used to tint the light color physically.
        /// </summary>
        public float Temperature
        {
            get => _temperature;
            set { _temperature = MathHelper.Clamp(value, 1000f, 20000f); OnPropertyChanged(nameof(Temperature)); }
        }

        /// <summary>
        /// Multiplier for ambient/indirect contribution of this light.
        /// </summary>
        public float IndirectMultiplier
        {
            get => _indirectMultiplier;
            set { _indirectMultiplier = MathHelper.Max(0f, value); OnPropertyChanged(nameof(IndirectMultiplier)); }
        }

        public float AmbientIntensity { get; set; } = 0.5f;

        public float Range
        {
            get => _range;
            set { _range = MathHelper.Max(0f, value); OnPropertyChanged(nameof(Range)); }
        }

        public float SpotAngle
        {
            get => _spotAngle;
            set { _spotAngle = MathHelper.Clamp(value, 1f, 179f); OnPropertyChanged(nameof(SpotAngle)); }
        }

        public bool CastShadows
        {
            get => _castShadows;
            set { _castShadows = value; OnPropertyChanged(nameof(CastShadows)); }
        }
    }

    public enum LightType
    {
        Directional,
        Point,
        Spot
    }

    public enum CameraClearFlags
    {
        Skybox,
        SolidColor
    }
}
