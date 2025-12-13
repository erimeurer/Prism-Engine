using System;
using System.ComponentModel;
using System.Windows.Input;

namespace MonoGameEditor.ViewModels;

/// <summary>
/// ViewModel for a single shader property (for UI binding)
/// </summary>
public class ShaderPropertyViewModel : ViewModelBase
{
    private readonly Core.Shaders.ShaderProperty _property;
    private object? _value;
    
    public ShaderPropertyViewModel(Core.Shaders.ShaderProperty property, object? initialValue = null)
    {
        _property = property;
        _value = initialValue ?? property.DefaultValue;
        
        // Ensure not null to prevent binding issues
        if (_value == null)
        {
            _value = property.Type switch
            {
                Core.Shaders.ShaderPropertyType.Float => 0f,
                Core.Shaders.ShaderPropertyType.Bool => false,
                Core.Shaders.ShaderPropertyType.Vector2 => Microsoft.Xna.Framework.Vector2.Zero,
                Core.Shaders.ShaderPropertyType.Vector3 => Microsoft.Xna.Framework.Vector3.Zero,
                Core.Shaders.ShaderPropertyType.Vector4 or Core.Shaders.ShaderPropertyType.Color => Microsoft.Xna.Framework.Vector4.One,
                Core.Shaders.ShaderPropertyType.Texture2D => "",
                _ => null
            };
        }
        
        // Command to pick color
        PickColorCommand = new RelayCommand(_ => PickColor());
    }
    
    public string Name => _property.Name;
    public Core.Shaders.ShaderPropertyType Type => _property.Type;
    
    public object? Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FloatValue));
                OnPropertyChanged(nameof(ColorValue));
                OnPropertyChanged(nameof(TextureValue));
                OnPropertyChanged(nameof(BoolValue));
            }
        }
    }
    
    // Type-specific accessors for easier binding
    public float FloatValue
    {
        get => Value is float f ? f : 0f;
        set => Value = value;
    }
    
    public System.Windows.Media.Color ColorValue
    {
        get
        {
            if (Value is Microsoft.Xna.Framework.Vector4 v4)
                return System.Windows.Media.Color.FromArgb(
                    (byte)(v4.W * 255),
                    (byte)(v4.X * 255),
                    (byte)(v4.Y * 255),
                    (byte)(v4.Z * 255)
                );
            return System.Windows.Media.Colors.White;
        }
        set
        {
            Value = new Microsoft.Xna.Framework.Vector4(
                value.R / 255f,
                value.G / 255f,
                value.B / 255f,
                value.A / 255f
            );
        }
    }
    
    public string TextureValue
    {
        get => Value as string ?? "";
        set
        {
            ConsoleViewModel.Log($"[ShaderPropertyVM] TextureValue setter for '{Name}': OLD='{Value}', NEW='{value}'");
            Value = value;
        }
    }
    
    public string Vector2Value
    {
        get
        {
            if (Value is Microsoft.Xna.Framework.Vector2 v2)
                return $"{v2.X:F2}, {v2.Y:F2}";
            return "0, 0";
        }
        set
        {
            var parts = value.Split(',');
            if (parts.Length == 2 && 
                float.TryParse(parts[0], out float x) && 
                float.TryParse(parts[1], out float y))
            {
                Value = new Microsoft.Xna.Framework.Vector2(x, y);
            }
        }
    }
    
    public string Vector3Value
    {
        get
        {
            if (Value is Microsoft.Xna.Framework.Vector3 v3)
                return $"{v3.X:F2}, {v3.Y:F2}, {v3.Z:F2}";
            return "0, 0, 0";
        }
        set
        {
            var parts = value.Split(',');
            if (parts.Length == 3 && 
                float.TryParse(parts[0], out float x) && 
                float.TryParse(parts[1], out float y) &&
                float.TryParse(parts[2], out float z))
            {
                Value = new Microsoft.Xna.Framework.Vector3(x, y, z);
            }
        }
    }
    
    public bool BoolValue
    {
        get => Value is bool b && b;
        set => Value = value;
    }
    
    public ICommand PickColorCommand { get; }
    
    private void PickColor()
    {
        using (var dialog = new System.Windows.Forms.ColorDialog())
        {
            // Convert MediaColor to System.Drawing.Color
            var mediaColor = ColorValue;
            dialog.Color = System.Drawing.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Convert back to MediaColor
                ColorValue = System.Windows.Media.Color.FromArgb(
                    dialog.Color.A, 
                    dialog.Color.R, 
                    dialog.Color.G, 
                    dialog.Color.B
                );
            }
        }
    }
}
