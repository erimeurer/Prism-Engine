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
            if (effect == null)
            {
                MonoGameEditor.ViewModels.ConsoleViewModel.Log("[Material] Apply called with null effect!");
                return;
            }
            
            // Set albedo
            var albedoParam = effect.Parameters["AlbedoColor"];
            if (albedoParam != null)
            {
                albedoParam.SetValue(AlbedoColor.ToVector4());
            }
            else
            {
                MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[Material] ⚠️ AlbedoColor parameter not found in shader");
            }
            
            // Set metallic
            var metallicParam = effect.Parameters["Metallic"];
            if (metallicParam != null)
            {
                metallicParam.SetValue(Metallic);
            }
            else
            {
                MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[Material] ⚠️ Metallic parameter not found");
            }
            
            // Set roughness
            var roughnessParam = effect.Parameters["Roughness"];
            if (roughnessParam != null)
            {
                roughnessParam.SetValue(Roughness);
            }
            else
            {
                MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[Material] ⚠️ Roughness parameter not found");
            }
            
            // Set AO
            var aoParam = effect.Parameters["AO"];
            if (aoParam != null)
            {
                aoParam.SetValue(AmbientOcclusion);
            }
            
            // Set texture maps if available AND set usage flags
            if (AlbedoMap != null)
            {
                effect.Parameters["AlbedoTexture"]?.SetValue(AlbedoMap);
                effect.Parameters["UseAlbedoMap"]?.SetValue(true);
            }
            else
            {
                effect.Parameters["UseAlbedoMap"]?.SetValue(false);
            }
            
            if (MetallicMap != null)
            {
                effect.Parameters["MetallicTexture"]?.SetValue(MetallicMap);
                effect.Parameters["UseMetallicMap"]?.SetValue(true);
            }
            else
            {
                effect.Parameters["UseMetallicMap"]?.SetValue(false);
            }
            
            if (RoughnessMap != null)
            {
                effect.Parameters["RoughnessTexture"]?.SetValue(RoughnessMap);
                effect.Parameters["UseRoughnessMap"]?.SetValue(true);
            }
            else
            {
                effect.Parameters["UseRoughnessMap"]?.SetValue(false);
            }
            
            if (NormalMap != null)
            {
                effect.Parameters["NormalTexture"]?.SetValue(NormalMap);
                effect.Parameters["UseNormalMap"]?.SetValue(true);
            }
            else
            {
                effect.Parameters["UseNormalMap"]?.SetValue(false);
            }
            
            if (AOMap != null)
            {
                effect.Parameters["AOTexture"]?.SetValue(AOMap);
                effect.Parameters["UseAOMap"]?.SetValue(true);
            }
            else
            {
                effect.Parameters["UseAOMap"]?.SetValue(false);
            }

            // Apply Custom Properties (Dynamic execution for custom shaders)
            if (CustomProperties != null && CustomProperties.Count > 0)
            {
                foreach (var kvp in CustomProperties)
                {
                    var param = effect.Parameters[kvp.Key];
                    if (param != null)
                    {
                        try 
                        {
                            ApplyProperty(param, kvp.Value);
                        }
                        catch
                        {
                            // Ignore type mismatches during rendering to prevent crash
                        }
                    }
                }
            }
        }

        private void ApplyProperty(EffectParameter param, object value)
        {
            if (value is float f) param.SetValue(f);
            else if (value is Vector2 v2) param.SetValue(v2);
            else if (value is Vector3 v3) param.SetValue(v3);
            else if (value is Vector4 v4) param.SetValue(v4);
            else if (value is Color c) param.SetValue(c.ToVector4());
            else if (value is bool b) param.SetValue(b);
            else if (value is Texture2D t) param.SetValue(t);
            else if (value is int i) param.SetValue(i);
            else if (value is Matrix m) param.SetValue(m);
        }

        // Dictionary to store dynamic properties (e.g. from Custom Shaders)
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();
    }
}
