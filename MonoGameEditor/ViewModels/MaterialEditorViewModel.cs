using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media;
using MonoGameEditor.Core.Materials;

namespace MonoGameEditor.ViewModels;

/// <summary>
/// ViewModel for editing PBR Material in Inspector
/// </summary>
public class MaterialEditorViewModel : ViewModelBase
{
    private PBRMaterial _material = PBRMaterial.CreateDefault();
    private string _filePath;
    private string? _shaderPath;
    private Core.Shaders.ShaderAsset? _currentShader;
    private Dictionary<string, object?> _customProperties = new();
    private bool _needsCompilation;
    
    
    // Observable collection for UI binding
    public ObservableCollection<ShaderPropertyViewModel> ShaderProperties { get; } = new();
    public ObservableCollection<string> AvailableShaders { get; } = new();
    
    
    public ICommand SaveMaterialCommand => new RelayCommand(_ => SaveMaterial());
    public ICommand CompileShaderCommand => new RelayCommand(_ => CompileShader());
    
    private ICommand? _selectTextureCommand;
    public ICommand SelectTextureCommand => _selectTextureCommand ??= new RelayCommand(param => SelectTexture(param as string));
    
    public MaterialEditorViewModel(string materialPath)
    {
        _filePath = materialPath;
        ConsoleViewModel.Log($"[MaterialEditor] Creating MaterialEditorViewModel for: {materialPath}");
        LoadAvailableShaders();
        ConsoleViewModel.Log($"[MaterialEditor] Available shaders loaded: {AvailableShaders.Count}");
        LoadMaterial();
        ConsoleViewModel.Log($"[MaterialEditor] Material loaded. ShaderPath = {_shaderPath ?? "(null)"}");
        
        // Fallback: if no shader or shader not available, use PBREffect or first available
        if (string.IsNullOrEmpty(_shaderPath) || !AvailableShaders.Contains(_shaderPath))
        {
            if (AvailableShaders.Count > 0)
            {
                // Try to find PBREffect.fx first (editor's built-in PBR shader)
                var pbrShader = AvailableShaders.FirstOrDefault(s => s.Contains("PBREffect.fx"));
                var selectedShader = pbrShader ?? AvailableShaders[0];
                
                var reason = string.IsNullOrEmpty(_shaderPath) ? "No shader configured" : $"Configured shader '{_shaderPath}' not found";
                ConsoleViewModel.Log($"[MaterialEditor] {reason}. Using: {selectedShader}");
                _shaderPath = selectedShader; // Use field directly to avoid trigger during init
            }
            else
            {
                ConsoleViewModel.Log($"[MaterialEditor] No shaders available!");
                _shaderPath = null;
            }
        }
        
        // DEBUG: Show all available shaders
        ConsoleViewModel.Log($"[MaterialEditor] === Available Shaders ({AvailableShaders.Count}) ===");
        for (int i = 0; i < AvailableShaders.Count; i++)
        {
            ConsoleViewModel.Log($"[MaterialEditor]   [{i}] {AvailableShaders[i]}");
        }
        ConsoleViewModel.Log($"[MaterialEditor] === Currently Selected: '{_shaderPath}' ===");
        
        // Start a timer to retry loading shader until GraphicsDevice is available
        StartShaderLoadRetryTimer();
    }
    
    private System.Windows.Threading.DispatcherTimer? _shaderLoadTimer;
    
    private void StartShaderLoadRetryTimer()
    {
        if (string.IsNullOrEmpty(_shaderPath) || ShaderProperties.Count > 0)
            return; // No shader or already loaded
            
        _shaderLoadTimer = new System.Windows.Threading.DispatcherTimer();
        _shaderLoadTimer.Interval = TimeSpan.FromMilliseconds(500);
        _shaderLoadTimer.Tick += (s, e) =>
        {
            var device = Core.GraphicsManager.GraphicsDevice;
            ConsoleViewModel.Log($"[MaterialEditor] Timer tick - Device: {(device != null ? "Available" : "null")}, Properties: {ShaderProperties.Count}");
            
            if (device != null && ShaderProperties.Count == 0)
            {
                ConsoleViewModel.Log($"[MaterialEditor] GraphicsDevice now available - loading shader");
                LoadShader();
                
                if (ShaderProperties.Count > 0 || _shaderLoadTimer == null)
                {
                    ConsoleViewModel.Log($"[MaterialEditor] Shader loaded successfully, stopping timer");
                    _shaderLoadTimer?.Stop();
                    _shaderLoadTimer = null;
                }
            }
        };
        _shaderLoadTimer.Start();
        ConsoleViewModel.Log($"[MaterialEditor] Started shader load retry timer");
    }
    
    private void LoadMaterial()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _material = PBRMaterial.CreateDefault();
                // Shader will be set by fallback in constructor
                return;
            }
            
            string json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<MaterialData>(json);
            
            if (data == null)
            {
                _material = PBRMaterial.CreateDefault();
                // Shader will be set by fallback in constructor
                return;
            }
            
            _material = new PBRMaterial
            {
                Name = data.name ?? "Material",
                AlbedoColor = new Microsoft.Xna.Framework.Color(
                    (byte)(data.albedoColor[0] * 255),
                    (byte)(data.albedoColor[1] * 255),
                    (byte)(data.albedoColor[2] * 255)
                ),
                Metallic = data.metallic,
                Roughness = data.roughness,
                AmbientOcclusion = data.ambientOcclusion
            };
            
            // Load texture paths
            _albedoMapPath = data.albedoMap;
            _normalMapPath = data.normalMap;
            _metallicMapPath = data.metallicMap;
            _roughnessMapPath = data.roughnessMap;
            _aoMapPath = data.aoMap;
            _heightMapPath = data.heightMap;
            
            // Load shader path (may be null, will use fallback)
            ShaderPath = data.shaderPath;
            
            // Legacy properties that should now come from shader, not JSON
            var legacyProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Metallic", "Roughness", "AO", "AmbientOcclusion",
                "LightDirection", "LightColor", "LightIntensity",
                "AlbedoColor", "UseShadows", "ShadowStrength", "ShadowBias", "ShadowMap"
            };
            
            // Load custom properties, but SKIP legacy properties that now come from shader
            if (data.customProperties != null)
            {
                foreach (var kvp in data.customProperties)
                {
                    // Skip legacy properties - they will be discovered from shader
                    if (legacyProperties.Contains(kvp.Key))
                    {
                        ConsoleViewModel.Log($"[Material] Skipping legacy property: {kvp.Key} (will use shader value)");
                        continue;
                    }
                    
                    _customProperties[kvp.Key] = DeserializePropertyValue(kvp.Value);
                }
            }
        }
        catch
        {
            _material = PBRMaterial.CreateDefault();
            // Shader will be set by fallback in constructor
        }
    }
    
    private void Material_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // Auto-save on property change (optional - could use Save button instead)
        // SaveMaterial();
    }
    
    public string MaterialName
    {
        get => _material?.Name ?? "Material";
        set
        {
            if (_material != null && _material.Name != value)
            {
                _material.Name = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string? ShaderPath
    {
        get => _shaderPath;
        set
        {
            ConsoleViewModel.Log($"==============================");
            ConsoleViewModel.Log($"🔴 SETTER CALLED! 🔴");
            ConsoleViewModel.Log($"==============================");
            
            // DEBUG: Show detailed info about what value we received
            var valueType = value?.GetType().Name ?? "null";
            var oldValue = _shaderPath ?? "(null)";
            var newValue = value ?? "(null)";
            
            ConsoleViewModel.Log($"[MaterialEditor] ShaderPath setter called:");
            ConsoleViewModel.Log($"[MaterialEditor]   Old: '{oldValue}' (length: {oldValue.Length})");
            ConsoleViewModel.Log($"[MaterialEditor]   New: '{newValue}' (length: {newValue.Length}, type: {valueType})");
            ConsoleViewModel.Log($"[MaterialEditor]   Are Equal? {_shaderPath == value}");
            
            if (_shaderPath != value)
            {
                _shaderPath = value;
                ConsoleViewModel.Log($"[MaterialEditor] ShaderPath changed! Calling OnPropertyChanged()");
                OnPropertyChanged();
                
                ConsoleViewModel.Log($"[MaterialEditor] Calling LoadShader()...");
                LoadShader(); // Reload properties for new shader
                
                ConsoleViewModel.Log($"[MaterialEditor] Calling SaveMaterial()...");
                SaveMaterial(); // CRITICAL FIX: Persist shader change immediately
                
                ConsoleViewModel.Log($"[MaterialEditor] ShaderPath setter complete!");
            }
            else
            {
                ConsoleViewModel.Log($"[MaterialEditor] ShaderPath unchanged, skipping update");
            }
            
            // Update compilation status
            UpdateCompilationStatus();
        }
    }
    
    public bool NeedsCompilation
    {
        get => _needsCompilation;
        set
        {
            if (_needsCompilation != value)
            {
                _needsCompilation = value;
                OnPropertyChanged();
            }
        }
    }
    
    // Public method to reload shader if it wasn't loaded (e.g. GraphicsDevice wasn't ready)
    public void EnsureShaderLoaded()
    {
        // If we have a shader path but no properties, try loading again
        if (!string.IsNullOrEmpty(_shaderPath) && ShaderProperties.Count == 0)
        {
            ConsoleViewModel.Log($"[MaterialEditor] Retrying shader load for: {_shaderPath}");
            LoadShader();
        }
    }
    
    private void LoadAvailableShaders()
    {
        try
        {
            string projectPath = Core.ProjectManager.Instance.ProjectPath ?? "";
            
            // Get editor directory (where the executable is running)
            string editorPath = System.AppDomain.CurrentDomain.BaseDirectory;
            
            // Search in multiple locations:
            // 1. Content\Shaders (built-in editor shaders like PBR) - FIRST PRIORITY
            // 2. Assets folder (user shaders)
            
            List<string> searchPaths = new List<string>
            {
                System.IO.Path.Combine(editorPath, "Content", "Shaders"),  // Editor shaders (built-in)
                System.IO.Path.Combine(projectPath, "Assets")               // User shaders
            };
            
            int totalMgfxo = 0;
            int totalFx = 0;
            
            foreach (var searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath))
                {
                    ConsoleViewModel.Log($"[Material] Skipping non-existent directory: {searchPath}");
                    continue;
                }
                
                ConsoleViewModel.Log($"[Material] Searching for shaders in: {searchPath}");
                
                // Find all shader files recursively (.xnb, .mgfxo, .fx)
                var xnbFiles = Directory.GetFiles(searchPath, "*.xnb", SearchOption.AllDirectories);
                var mgfxoFiles = Directory.GetFiles(searchPath, "*.mgfxo", SearchOption.AllDirectories);
                var fxFiles = Directory.GetFiles(searchPath, "*.fx", SearchOption.AllDirectories);
                
                totalMgfxo += mgfxoFiles.Length;
                totalFx += fxFiles.Length + xnbFiles.Length; // Count xnb as fx
                
                ConsoleViewModel.Log($"[Material] Found {xnbFiles.Length} .xnb, {mgfxoFiles.Length} .mgfxo and {fxFiles.Length} .fx in this directory");
                
                // Determine if this is editor path or project path
                bool isEditorPath = searchPath.Contains(editorPath);
                string basePath = isEditorPath ? editorPath : projectPath;
                
                // Add .xnb shaders (compiled via Content Pipeline)
                // Change extension to .fx for consistency with ShaderPath
                foreach (var fullPath in xnbFiles)
                {
                    var relativePath = System.IO.Path.GetRelativePath(basePath, fullPath)
                        .Replace("\\", "/");
                    
                    // Change .xnb to .fx so it matches ShaderPath format
                    relativePath = System.IO.Path.ChangeExtension(relativePath, ".fx");
                    
                    ConsoleViewModel.Log($"[Material] Adding shader (xnb): {relativePath}");
                    AvailableShaders.Add(relativePath);
                }
                
                // Add compiled shaders (.mgfxo)
                foreach (var fullPath in mgfxoFiles)
                {
                    var relativePath = System.IO.Path.GetRelativePath(basePath, fullPath)
                        .Replace("\\", "/");
                    
                    ConsoleViewModel.Log($"[Material] Adding shader: {relativePath}");
                    AvailableShaders.Add(relativePath);
                }
                
                // Add source shaders (.fx)
                foreach (var fullPath in fxFiles)
                {
                    var relativePath = System.IO.Path.GetRelativePath(basePath, fullPath)
                        .Replace("\\", "/");
                    
                    ConsoleViewModel.Log($"[Material] Adding shader: {relativePath}");
                    AvailableShaders.Add(relativePath);
                }
            }
            
            ConsoleViewModel.Log($"[Material] Total: {totalMgfxo} .mgfxo + {totalFx} .fx = {AvailableShaders.Count} shaders available");
        }
        catch (Exception ex)
        {
            ConsoleViewModel.Log($"[Material] Failed to load shaders: {ex.Message}");
        }
    }
    
    private void LoadShader()
    {
        ShaderProperties.Clear();
        _currentShader = null;
        
        if (string.IsNullOrEmpty(_shaderPath))
            return;
            
        try
        {
            // Get GraphicsDevice from MonoGameControl
            var device = Core.GraphicsManager.GraphicsDevice;
            if (device == null)
            {
                ConsoleViewModel.Log($"[Material] GraphicsDevice not available yet - will retry when available");
                return;
            }
            
            // CRITICAL FIX: Construct absolute path for shader loading
            // ShaderLoader expects either:
            // 1. "Content/..." paths (for built-in shaders)
            // 2. Absolute paths (for user shaders in Assets/)
            string shaderPathToLoad = _shaderPath;
            
            if (!_shaderPath.StartsWith("Content/", StringComparison.OrdinalIgnoreCase))
            {
                // User shader in project Assets folder - construct absolute path
                string projectPath = Core.ProjectManager.Instance.ProjectPath ?? "";
                string editorPath = System.AppDomain.CurrentDomain.BaseDirectory;
                
                // Try project path first (for user shaders in Assets/)
                string absolutePath = System.IO.Path.Combine(projectPath, _shaderPath);
                
                if (System.IO.File.Exists(absolutePath))
                {
                    shaderPathToLoad = absolutePath;
                    ConsoleViewModel.Log($"[Material] Using absolute project path: {shaderPathToLoad}");
                }
                else
                {
                    // Try editor path (for built-in shaders that aren't in Content/)
                    absolutePath = System.IO.Path.Combine(editorPath, _shaderPath);
                    if (System.IO.File.Exists(absolutePath))
                    {
                        shaderPathToLoad = absolutePath;
                        ConsoleViewModel.Log($"[Material] Using absolute editor path: {shaderPathToLoad}");
                    }
                    else
                    {
                        ConsoleViewModel.Log($"[Material] Warning: Shader file not found at project or editor path: {_shaderPath}");
                    }
                }
            }
            
            // Load shader via ShaderLoader
            _currentShader = Core.Shaders.ShaderLoader.LoadShaderAsset(
                device, 
                shaderPathToLoad,
                System.IO.Path.GetFileNameWithoutExtension(_shaderPath)
            );
            
            if (_currentShader == null)
            {
                ConsoleViewModel.Log($"[Material] Failed to load shader: {_shaderPath}");
                ConsoleViewModel.Log($"[Material] Shader selection kept - check if shader needs compilation");
                
                // DON'T fallback to PBREffect automatically!
                // Keep the shader selected so user can see the "Compile Shader" button
                // and compile it manually
                
                return;
            }
            
            ConsoleViewModel.Log($"[Material] Loaded shader: {_shaderPath} with {_currentShader.Properties.Count} properties");
        
        // DON'T clean up texture properties from customProperties!
        // Textures are saved in customProperties and need to be loaded as initialValue
        // Only remove non-texture properties that are re-discovered from shader
        var discoveredPropertyNames = _currentShader.Properties
            .Where(p => p.Type != Core.Shaders.ShaderPropertyType.Texture2D) // Keep textures in customProperties
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        var keysToRemove = _customProperties.Keys.Where(k => discoveredPropertyNames.Contains(k)).ToList();
        
        foreach (var key in keysToRemove)
        {
            ConsoleViewModel.Log($"[Material] Removing customProperty '{key}' (now comes from shader)");
            _customProperties.Remove(key);
        }
        
        // Create ViewModels for each property
        foreach (var prop in _currentShader.Properties)
        {
            // Check if we have a saved value for this property
            object? initialValue = null;
            if (_customProperties.TryGetValue(prop.Name, out var savedValue))
            {
                initialValue = savedValue;
                ConsoleViewModel.Log($"[Material] Loading saved value for '{prop.Name}': {savedValue}");
            }    
                var propVM = new ShaderPropertyViewModel(prop, initialValue);
                
                // Listen for changes to auto-save
                propVM.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ShaderPropertyViewModel.Value))
                    {
                        _customProperties[prop.Name] = propVM.Value;
                        SaveMaterial();
                    }
                };
                
                ShaderProperties.Add(propVM);
            }
            
            // CRITICAL: Populate ShaderProperties with legacy texture values
            PopulateLegacyTextureValues();
        }
        catch (Exception ex)
        {
            ConsoleViewModel.Log($"[Material] Failed to load shader: {ex.Message}");
        }
    }
    
    public System.Windows.Media.Color AlbedoColor
    {
        get
        {
            var c = _material?.AlbedoColor ?? Microsoft.Xna.Framework.Color.White;
            return System.Windows.Media.Color.FromRgb(c.R, c.G, c.B);
        }
        set
        {
            if (_material != null)
            {
                _material.AlbedoColor = new Microsoft.Xna.Framework.Color(value.R, value.G, value.B);
                OnPropertyChanged();
            }
        }
    }
    
    public float Metallic
    {
        get => _material?.Metallic ?? 0f;
        set
        {
            if (_material != null && _material.Metallic != value)
            {
                _material.Metallic = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MetallicPercent));
            }
        }
    }
    
    public int MetallicPercent
    {
        get => (int)(Metallic * 100);
        set => Metallic = value / 100f;
    }
    
    public float Smoothness
    {
        get => _material?.Smoothness ?? 0.5f;
        set
        {
            if (_material != null && _material.Smoothness != value)
            {
                _material.Smoothness = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SmoothnessPercent));
                OnPropertyChanged(nameof(Roughness));
            }
        }
    }
    
    public int SmoothnessPercent
    {
        get => (int)(Smoothness * 100);
        set => Smoothness = value / 100f;
    }
    
    public float Roughness
    {
        get => _material?.Roughness ?? 0.5f;
    }
    
    public float AmbientOcclusion
    {
        get => _material?.AmbientOcclusion ?? 1f;
        set
        {
            if (_material != null && _material.AmbientOcclusion != value)
            {
                _material.AmbientOcclusion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AmbientOcclusionPercent));
            }
        }
    }
    
    public int AmbientOcclusionPercent
    {
        get => (int)(AmbientOcclusion * 100);
        set => AmbientOcclusion = value / 100f;
    }
    
    // Texture Map Paths
    private string? _albedoMapPath;
    public string? AlbedoMapPath
    {
        get => _albedoMapPath;
        set { _albedoMapPath = value; OnPropertyChanged(); SaveMaterial(); }
    }
    
    private string? _normalMapPath;
    public string? NormalMapPath
    {
        get => _normalMapPath;
        set { _normalMapPath = value; OnPropertyChanged(); SaveMaterial(); }
    }
    
    private string? _metallicMapPath;
    public string? MetallicMapPath
    {
        get => _metallicMapPath;
        set { _metallicMapPath = value; OnPropertyChanged(); SaveMaterial(); }
    }
    
    private string? _roughnessMapPath;
    public string? RoughnessMapPath
    {
        get => _roughnessMapPath;
        set { _roughnessMapPath = value; OnPropertyChanged(); SaveMaterial(); }
    }
    
    private string? _aoMapPath;
    public string? AOMapPath
    {
        get => _aoMapPath;
        set { _aoMapPath = value; OnPropertyChanged(); SaveMaterial(); }
    }
    
    private string? _heightMapPath;
    public string? HeightMapPath
    {
        get => _heightMapPath;
        set { _heightMapPath = value; OnPropertyChanged(); SaveMaterial(); }
    }
    
    public ICommand SaveCommand => new RelayCommand(_ => SaveMaterial());
    
    private void SaveMaterial()
    {
        ConsoleViewModel.Log($"[MaterialEditor] SaveMaterial() called for: {_filePath}");
        
        try
        {
            var data = new MaterialData
            {
                name = _material.Name,
                albedoColor = new float[]
                {
                    _material.AlbedoColor.R / 255f,
                    _material.AlbedoColor.G / 255f,
                    _material.AlbedoColor.B / 255f
                },
                metallic = _material.Metallic,
                roughness = _material.Roughness,
                ambientOcclusion = _material.AmbientOcclusion,
                
                // Texture paths
                albedoMap = _albedoMapPath,
                normalMap = _normalMapPath,
                metallicMap = _metallicMapPath,
                roughnessMap = _roughnessMapPath,
                aoMap = _aoMapPath,
                heightMap = _heightMapPath,
                
                // Shader path
                shaderPath = _shaderPath,
                
                // Custom properties
                customProperties = SerializeCustomProperties()
            };
            
            ConsoleViewModel.Log($"[MaterialEditor] Saving - ShaderPath: '{data.shaderPath}', CustomProps: {data.customProperties?.Count ?? 0}");
            
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
            
            ConsoleViewModel.Log($"[MaterialEditor] ✅ Material saved successfully: {_material.Name}");
        }
        catch (Exception ex)
        {
            ConsoleViewModel.Log($"[MaterialEditor] ❌ Failed to save material: {ex.Message}");
        }
    }
    
    private Dictionary<string, JsonElement> SerializeCustomProperties()
    {
        var result = new Dictionary<string, JsonElement>();
        
        // Serialize ONLY texture properties (string values) from ShaderProperties
        // Other properties (floats, vectors, colors) are re-discovered from shader on load
        foreach (var prop in ShaderProperties)
        {
            if (prop.Type == Core.Shaders.ShaderPropertyType.Texture2D && !string.IsNullOrEmpty(prop.TextureValue))
            {
                var jsonValue = JsonSerializer.SerializeToElement(prop.TextureValue);
                result[prop.Name] = jsonValue;
                ConsoleViewModel.Log($"[MaterialEditor] Serializing texture: {prop.Name} = {prop.TextureValue}");
            }
        }
        
        // Legacy: merge any old _customProperties that aren't in ShaderProperties
        foreach (var kvp in _customProperties)
        {
            if (!result.ContainsKey(kvp.Key))
            {
                var jsonValue = JsonSerializer.SerializeToElement(kvp.Value);
                result[kvp.Key] = jsonValue;
            }
        }
        
        return result;
    }
    
    private object? DeserializePropertyValue(JsonElement element)
    {
        // Try to deserialize based on element type
        try
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    return element.GetSingle();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Object:
                    // Try to deserialize as Vector4 (for colors)
                    try
                    {
                        return element.Deserialize<Microsoft.Xna.Framework.Vector4>();
                    }
                    catch
                    {
                        return null;
                    }
                default:
                    return null;
            }
        }
        catch
        {
            return null;
        }
    }
    
    // JSON structure
    private class MaterialData
    {
        public string name { get; set; } = "";
        public float[] albedoColor { get; set; } = new float[3];
        public float metallic { get; set; }
        public float roughness { get; set; }
        public float ambientOcclusion { get; set; }
        
        // Texture paths
        public string? albedoMap { get; set; }
        public string? normalMap { get; set; }
        public string? metallicMap { get; set; }
        public string? roughnessMap { get; set; }
        public string? aoMap { get; set; }
        public string? heightMap { get; set; }
        
        // Shader path
        public string? shaderPath { get; set; }
        
        // Custom shader properties
        public Dictionary<string, JsonElement>? customProperties { get; set; }
    }
    
    private void SelectTexture(string? propertyName)
    {
        ConsoleViewModel.Log($"[MaterialEditor] SelectTexture called with: '{propertyName}'");
        
        if (string.IsNullOrEmpty(propertyName))
        {
            ConsoleViewModel.Log($"[MaterialEditor] ❌ Property name is null or empty");
            return;
        }
            
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Select {propertyName} Texture",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.dds;*.tga|All Files|*.*",
            InitialDirectory = Core.ProjectManager.Instance.ProjectPath
        };
        
        ConsoleViewModel.Log($"[MaterialEditor] Opening file dialog...");
        
        if (dialog.ShowDialog() == true)
        {
            ConsoleViewModel.Log($"[MaterialEditor] File selected: {dialog.FileName}");
            
            // Store relative path if possible
            string projectPath = Core.ProjectManager.Instance.ProjectPath ?? "";
            string texturePath = dialog.FileName;
            
            if (texturePath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
            {
                texturePath = System.IO.Path.GetRelativePath(projectPath, texturePath).Replace("\\", "/");
                ConsoleViewModel.Log($"[MaterialEditor] Converted to relative path: {texturePath}");
            }
            
            ConsoleViewModel.Log($"[MaterialEditor] Current ShaderProperties count: {ShaderProperties.Count}");
            
            // Find shader property and update its value
            var property = ShaderProperties.FirstOrDefault(p => 
                p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            
            if (property != null)
            {
                ConsoleViewModel.Log($"[MaterialEditor] ✅ Found property '{propertyName}', Type: {property.Type}");
                ConsoleViewModel.Log($"[MaterialEditor] Old value: '{property.TextureValue}'");
                
                property.TextureValue = texturePath;
                
                ConsoleViewModel.Log($"[MaterialEditor] New value: '{property.TextureValue}'");
                
                // ALSO update legacy fixed properties for backward compatibility
                UpdateLegacyTextureProperty(propertyName, texturePath);
                
                ConsoleViewModel.Log($"[MaterialEditor] Calling SaveMaterial()...");
                SaveMaterial();
            }
            else
            {
                ConsoleViewModel.Log($"[MaterialEditor] ❌ Property '{propertyName}' not found in ShaderProperties");
                ConsoleViewModel.Log($"[MaterialEditor] Available properties:");
                foreach (var p in ShaderProperties)
                {
                    ConsoleViewModel.Log($"[MaterialEditor]   - {p.Name} ({p.Type})");
                }
            }
        }
        else
        {
            ConsoleViewModel.Log($"[MaterialEditor] File dialog cancelled");
        }
    }
    
    private void UpdateLegacyTextureProperty(string propertyName, string texturePath)
    {
        // Map shader property names to legacy fixed properties
        switch (propertyName)
        {
            case "AlbedoTexture":
                AlbedoMapPath = texturePath;
                ConsoleViewModel.Log($"[MaterialEditor] Updated AlbedoMapPath = {texturePath}");
                break;
            case "NormalTexture":
                NormalMapPath = texturePath;
                ConsoleViewModel.Log($"[MaterialEditor] Updated NormalMapPath = {texturePath}");
                break;
            case "MetallicTexture":
                MetallicMapPath = texturePath;
                ConsoleViewModel.Log($"[MaterialEditor] Updated MetallicMapPath = {texturePath}");
                break;
            case "RoughnessTexture":
                RoughnessMapPath = texturePath;
                ConsoleViewModel.Log($"[MaterialEditor] Updated RoughnessMapPath = {texturePath}");
                break;
            case "AOTexture":
                AOMapPath = texturePath;
                ConsoleViewModel.Log($"[MaterialEditor] Updated AOMapPath = {texturePath}");
                break;
            default:
                // For custom shader textures with different names, save in customProperties
                ConsoleViewModel.Log($"[MaterialEditor] Property '{propertyName}' doesn't match legacy names, will save in customProperties");
                break;
        }
    }
    
    private void PopulateLegacyTextureValues()
    {
        // Map legacy texture paths to ShaderProperties
        var mappings = new Dictionary<string, string?>
        {
            { "AlbedoTexture", _albedoMapPath },
            { "NormalTexture", _normalMapPath },
            { "MetallicTexture", _metallicMapPath },
            { "RoughnessTexture", _roughnessMapPath },
            { "AOTexture", _aoMapPath }
        };
        
        foreach (var mapping in mappings)
        {
            if (!string.IsNullOrEmpty(mapping.Value))
            {
                var prop = ShaderProperties.FirstOrDefault(p => 
                    p.Name.Equals(mapping.Key, StringComparison.OrdinalIgnoreCase));
                    
                if (prop != null)
                {
                    prop.TextureValue = mapping.Value;
                    ConsoleViewModel.Log($"[MaterialEditor] Loaded legacy texture: {mapping.Key} = {mapping.Value}");
                }
            }
        }
    }
    
    private void UpdateCompilationStatus()
    {
        if (string.IsNullOrEmpty(_shaderPath))
        {
            NeedsCompilation = false;
            return;
        }
        
        // Check if shader is .fx and doesn't have corresponding .mgfxo
        if (_shaderPath.EndsWith(".fx", StringComparison.OrdinalIgnoreCase) && 
            !_shaderPath.StartsWith("Content/", StringComparison.OrdinalIgnoreCase))
        {
            string projectPath = Core.ProjectManager.Instance.ProjectPath ?? "";
            string absolutePath = System.IO.Path.Combine(projectPath, _shaderPath);
            string mgfxoPath = System.IO.Path.ChangeExtension(absolutePath, ".mgfxo");
            
            NeedsCompilation = System.IO.File.Exists(absolutePath) && !System.IO.File.Exists(mgfxoPath);
        }
        else
        {
            NeedsCompilation = false;
        }
    }
    
    private void CompileShader()
    {
        if (string.IsNullOrEmpty(_shaderPath))
            return;
            
        try
        {
            string projectPath = Core.ProjectManager.Instance.ProjectPath ?? "";
            string fxPath = System.IO.Path.Combine(projectPath, _shaderPath);
            string mgfxoPath = System.IO.Path.ChangeExtension(fxPath, ".mgfxo");
            
            if (!System.IO.File.Exists(fxPath))
            {
                ConsoleViewModel.Log($"[MaterialEditor] Shader file not found: {fxPath}");
                return;
            }
            
            ConsoleViewModel.Log($"[MaterialEditor] Compiling shader: {fxPath}");
            
            // Call mgfxc compiler
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "mgfxc",
                Arguments = $"\"{fxPath}\" \"{mgfxoPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using (var process = System.Diagnostics.Process.Start(processInfo))
            {
                if (process == null)
                {
                    ConsoleViewModel.Log($"[MaterialEditor] ❌ Failed to start mgfxc compiler. Make sure mgfxc is in PATH.");
                    return;
                }
                
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                if (process.ExitCode == 0)
                {
                    ConsoleViewModel.Log($"[MaterialEditor] ✅ Shader compiled successfully: {mgfxoPath}");
                    
                    if (!string.IsNullOrWhiteSpace(output))
                        ConsoleViewModel.Log($"[mgfxc] {output}");
                    
                    // Reload shader
                    LoadShader();
                    UpdateCompilationStatus();
                }
                else
                {
                    ConsoleViewModel.Log($"[MaterialEditor] ❌ Shader compilation failed (exit code {process.ExitCode})");
                    
                    if (!string.IsNullOrWhiteSpace(error))
                        ConsoleViewModel.Log($"[mgfxc ERROR] {error}");
                    
                    if (!string.IsNullOrWhiteSpace(output))
                        ConsoleViewModel.Log($"[mgfxc] {output}");
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleViewModel.Log($"[MaterialEditor] ❌ Failed to compile shader: {ex.Message}");
        }
    }
}
