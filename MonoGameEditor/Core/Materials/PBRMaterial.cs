using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.ComponentModel;

namespace MonoGameEditor.Core.Materials
{
    /// <summary>
    /// PBR (Physically Based Rendering) Material following Unity's Standard shader
    /// Uses Metallic workflow: Albedo, Metallic, Roughness
    /// </summary>
    public class PBRMaterial : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _name = "Standard";
        private Color _albedoColor = Color.White;
        private float _metallic = 0.0f;
        private float _roughness = 0.5f;
        private float _ao = 1.0f;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }
        
        // Base Properties
        public Color AlbedoColor
        {
            get => _albedoColor;
            set
            {
                if (_albedoColor != value)
                {
                    _albedoColor = value;
                    OnPropertyChanged(nameof(AlbedoColor));
                }
            }
        }

        public float Metallic
        {
            get => _metallic;
            set
            {
                var clamped = MathHelper.Clamp(value, 0f, 1f);
                if (_metallic != clamped)
                {
                    _metallic = clamped;
                    OnPropertyChanged(nameof(Metallic));
                }
            }
        }

        public float Roughness
        {
            get => _roughness;
            set
            {
                var clamped = MathHelper.Clamp(value, 0f, 1f);
                if (_roughness != clamped)
                {
                    _roughness = clamped;
                    OnPropertyChanged(nameof(Roughness));
                    OnPropertyChanged(nameof(Smoothness)); // Notify Smoothness too
                }
            }
        }

        public float AmbientOcclusion
        {
            get => _ao;
            set
            {
                var clamped = MathHelper.Clamp(value, 0f, 1f);
                if (_ao != clamped)
                {
                    _ao = clamped;
                    OnPropertyChanged(nameof(AmbientOcclusion));
                }
            }
        }
        
        // Texture Maps (optional)
        public Texture2D? AlbedoMap { get; set; }
        public Texture2D? MetallicMap { get; set; }
        public Texture2D? RoughnessMap { get; set; }
        public Texture2D? NormalMap { get; set; }
        public Texture2D? AOMap { get; set; }
        
        // Convenience property for Unity-style "Smoothness" (inverse of Roughness)
        public float Smoothness
        {
            get => 1.0f - Roughness;
            set
            {
                Roughness = 1.0f - value; // This will trigger PropertyChanged
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        /// <summary>
        /// Creates a default PBR material (white, non-metallic, medium roughness)
        /// </summary>
        public static PBRMaterial CreateDefault()
        {
            return new PBRMaterial
            {
                Name = "Standard",
                AlbedoColor = new Color(180, 180, 180), // Gray instead of white for visibility
                Metallic = 0.0f,
                Roughness = 0.5f,
                AmbientOcclusion = 1.0f
            };
        }
        
        /// <summary>
        /// Applies this material's properties to a PBR effect
        /// </summary>
        public void Apply(Effect effect)
        {
            // Set albedo
            var albedoParam = effect.Parameters["AlbedoColor"];
            if (albedoParam != null)
                albedoParam.SetValue(AlbedoColor.ToVector4());
            
            // Set metallic
            var metallicParam = effect.Parameters["Metallic"];
            if (metallicParam != null)
                metallicParam.SetValue(Metallic);
            
            // Set roughness
            var roughnessParam = effect.Parameters["Roughness"];
            if (roughnessParam != null)
                roughnessParam.SetValue(Roughness);
            
            // Set AO
            var aoParam = effect.Parameters["AO"];
            if (aoParam != null)
                aoParam.SetValue(AmbientOcclusion);
            
            // Set texture maps if available
            if (AlbedoMap != null)
            {
                var albedoTexParam = effect.Parameters["AlbedoTexture"];
                if (albedoTexParam != null)
                    albedoTexParam.SetValue(AlbedoMap);
            }
            
            if (MetallicMap != null)
            {
                var metallicTexParam = effect.Parameters["MetallicTexture"];
                if (metallicTexParam != null)
                    metallicTexParam.SetValue(MetallicMap);
            }
            
            if (RoughnessMap != null)
            {
                var roughnessTexParam = effect.Parameters["RoughnessTexture"];
                if (roughnessTexParam != null)
                    roughnessTexParam.SetValue(RoughnessMap);
            }
            
            if (NormalMap != null)
            {
                var normalTexParam = effect.Parameters["NormalTexture"];
                if (normalTexParam != null)
                    normalTexParam.SetValue(NormalMap);
            }
            
            if (AOMap != null)
            {
                var aoTexParam = effect.Parameters["AOTexture"];
                if (aoTexParam != null)
                    aoTexParam.SetValue(AOMap);
            }
        }
    }
}
