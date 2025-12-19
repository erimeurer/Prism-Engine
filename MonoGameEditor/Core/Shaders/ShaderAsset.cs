using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.Core;

namespace MonoGameEditor.Core.Shaders;

/// <summary>
/// Property type for shader parameters
/// </summary>
public enum ShaderPropertyType
{
    Float,
    Vector2,
    Vector3,
    Vector4,
    Color,
    Texture2D,
    Matrix,
    Bool
}

/// <summary>
/// Represents a shader parameter/property
/// </summary>
public class ShaderProperty
{
    public string Name { get; set; } = "";
    public ShaderPropertyType Type { get; set; }
    public object? DefaultValue { get; set; }
    
    public ShaderProperty(string name, ShaderPropertyType type, object? defaultValue = null)
    {
        Name = name;
        Type = type;
        DefaultValue = defaultValue;
    }
}

/// <summary>
/// Represents a shader asset (.fx file)
/// </summary>
public class ShaderAsset
{
    public string Name { get; set; } = "Shader";
    public string ShaderPath { get; set; } = ""; // Path to .fx or .mgfxo file
    public List<ShaderProperty> Properties { get; private set; } = new();
    
    /// <summary>
    /// Discover properties from a loaded Effect using reflection
    /// </summary>
    public void DiscoverProperties(Effect effect)
    {
        Properties.Clear();

        Logger.Log($"[ShaderAsset] Discovering properties from effect with {effect.Parameters.Count} parameters");
        
        foreach (var param in effect.Parameters)
        {
            Logger.Log($"[ShaderAsset] Param: {param.Name}, Class: {param.ParameterClass}, Type: {param.ParameterType}");
            
            // Skip system parameters
            if (IsSystemParameter(param.Name))
            {
                Logger.Log($"[ShaderAsset]   -> Skipped (system parameter)");
                continue;
            }
                
            var propType = GetPropertyType(param);
            if (propType.HasValue)
            {
                object? defaultValue = null;
                try
                {
                    defaultValue = propType.Value switch
                    {
                        ShaderPropertyType.Float => param.GetValueSingle(),
                        ShaderPropertyType.Bool => param.GetValueBoolean(),
                        ShaderPropertyType.Vector2 => param.GetValueVector2(),
                        ShaderPropertyType.Vector3 => param.GetValueVector3(),
                        ShaderPropertyType.Vector4 or ShaderPropertyType.Color => param.GetValueVector4(),
                        // Textures don't have default values we can read easily here
                        _ => null
                    };
                }
                catch { /* Ignore if fails to read default */ }
                
                Properties.Add(new ShaderProperty(param.Name, propType.Value, defaultValue));
                Logger.Log($"[ShaderAsset]   -> Added as {propType.Value} (Default: {defaultValue})");
            }
            else
            {
                Logger.Log($"[ShaderAsset]   -> Skipped (unsupported type)");
            }
        }
        
        Logger.Log($"[ShaderAsset] Discovered {Properties.Count} properties");
    }
    
    private bool IsSystemParameter(string name)
    {
        // Common system parameters to skip
        var systemParams = new[] { 
            "World", "View", "Projection", "WorldViewProjection",
            "WorldInverseTranspose", "ViewProjection",
            "CameraPosition", "Time"
        };
        return Array.Exists(systemParams, p => p.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    
    private ShaderPropertyType? GetPropertyType(EffectParameter param)
    {
        return param.ParameterClass switch
        {
            EffectParameterClass.Scalar when param.ParameterType == EffectParameterType.Single => ShaderPropertyType.Float,
            EffectParameterClass.Scalar when param.ParameterType == EffectParameterType.Bool => ShaderPropertyType.Bool,
            EffectParameterClass.Vector when param.ColumnCount == 2 => ShaderPropertyType.Vector2,
            EffectParameterClass.Vector when param.ColumnCount == 3 => ShaderPropertyType.Vector3,
            EffectParameterClass.Vector when param.ColumnCount == 4 => ShaderPropertyType.Color,
            EffectParameterClass.Matrix => ShaderPropertyType.Matrix,
            EffectParameterClass.Object when param.ParameterType == EffectParameterType.Texture2D => ShaderPropertyType.Texture2D,
            _ => null
        };
    }
    
    /// <summary>
    /// Create default shader asset for PBR
    /// </summary>
    public static ShaderAsset CreateStandard()
    {
        return new ShaderAsset
        {
            Name = "Standard (PBR)",
            ShaderPath = "Shaders/PBREffect.mgfxo"
        };
    }
    
    /// <summary>
    /// Create unlit shader asset
    /// </summary>
    public static ShaderAsset CreateUnlit()
    {
        return new ShaderAsset
        {
            Name = "Unlit",
            ShaderPath = "Shaders/Unlit.mgfxo"
        };
    }
}
