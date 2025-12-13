using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.Core.Assets;
using MonoGameEditor.Core.Materials;
using MonoGameEditor.Controls;
using MonoGameEditor.ViewModels;

namespace MonoGameEditor.Core.Components
{


    public class ModelRendererComponent : Component
    {
        private class DeviceResources
        {
            public VertexBuffer VertexBuffer { get; set; }
            public IndexBuffer IndexBuffer { get; set; }
            public Effect Effect { get; set; }
        }

        private readonly Dictionary<GraphicsDevice, DeviceResources> _deviceResources = new();
        private string _modelPath;
        private AssetMetadata _metadata;
        private Assets.MeshData _meshData; // For hierarchical models
        
        
        public override string ComponentName => "Model Renderer";

        public string ModelPath
        {
            get => _modelPath;
            set
            {
                if (_modelPath != value)
                {
                    _modelPath = value;
                    OnPropertyChanged(nameof(ModelPath));
                    ConsoleViewModel.Log($"ModelPath set to: {value}");
                    LoadModel();
                }
            }
        }

        // Shadow Properties
        private ShadowMode _castShadows = ShadowMode.On;
        private bool _receiveShadows = true;

        public ShadowMode CastShadows
        {
            get => _castShadows;
            set { _castShadows = value; OnPropertyChanged(nameof(CastShadows)); }
        }

        public bool ReceiveShadows
        {
            get => _receiveShadows;
            set { _receiveShadows = value; OnPropertyChanged(nameof(ReceiveShadows)); }
        }

        // Material Properties
        private Materials.PBRMaterial? _material;
        private string? _materialPath;
        
        // Pending texture paths used to reload textures for different devices
        private Dictionary<string, string> _pendingTexturePaths = new();
        
        // Multi-device texture cache: Device -> (TextureType -> Texture2D)
        private Dictionary<GraphicsDevice, Dictionary<string, Texture2D>> _deviceTextureCache = new();
        
        // Debug
        private HashSet<string> _loggedMismatches = new HashSet<string>();

        public Materials.PBRMaterial? Material
        {
            get
            {
                // CRITICAL FIX: Always ensure we have a material (prevent blue artifacts with BasicEffect)
                if (_material == null)
                {
                    _material = Materials.PBRMaterial.CreateDefault();
                    ConsoleViewModel.Log($"[ModelRenderer] Auto-created default material for {GameObject?.Name ?? "unknown"}");
                }
                return _material;
            }
            set { _material = value; OnPropertyChanged(nameof(Material)); }
        }

        public string? MaterialPath
        {
            get => _materialPath;
            set
            {
                if (_materialPath != value)
                {
                    ConsoleViewModel.Log($"[ModelRenderer] MaterialPath CHANGED: '{_materialPath}' → '{value}'");
                    _materialPath = value;
                    OnPropertyChanged(nameof(MaterialPath));
                    LoadMaterial();
                }
            }
        }

        // For hierarchical meshes - stores the original model file path
        private string _originalModelPath;
        public string OriginalModelPath
        {
            get => _originalModelPath;
            set
            {
                _originalModelPath = value;
                OnPropertyChanged(nameof(OriginalModelPath));
            }
        }

        /// <summary>
        /// Sets mesh data directly (for hierarchical models with multiple meshes)
        /// </summary>
        public void SetMeshData(Assets.MeshData meshData, string originalModelPath = null)
        {
            _meshData = meshData;
            _originalModelPath = originalModelPath; // Store source path
            
            // Clear existing resources to force regeneration on next Draw
            ClearResources();
            
            ConsoleViewModel.Log($"SetMeshData called for mesh '{meshData.Name}' from '{originalModelPath}'");
        }

        private void ClearResources()
        {
            foreach (var resources in _deviceResources.Values)
            {
                resources.VertexBuffer?.Dispose();
                resources.IndexBuffer?.Dispose();
                // Note: We don't dispose the Effect here as it might be shared effectively or managed by ContentManager in a real engine,
                // but here we create them manually so we should probably dispose them or leave them for GC if detached.
                // For safety in this specific architecture, let's treat them as unique per device instance.
            }
            _deviceResources.Clear();
        }

        public async void LoadModel()
        {
             if (string.IsNullOrEmpty(_modelPath))
             {
                 ConsoleViewModel.Log("LoadModel: ModelPath is empty, skipping");
                 return;
             }

             ConsoleViewModel.Log($"LoadModel: Loading {_modelPath}");

             try
             {
                 _metadata = await ModelImporter.LoadMetadataAsync(_modelPath);
                 ClearResources(); // Invalidate resources
             }
             catch (Exception ex)
             {
                 System.Diagnostics.Debug.WriteLine($"Failed to load model: {ex.Message}");
             }
        }

        private string? _loadedShaderPath;

        /// <summary>
        /// Called after component is added to GameObject (useful after deserialization)
        /// </summary>
        public async void OnComponentAdded()
        {
            // If we have OriginalModelPath but no _meshData, reload from file
            if (!string.IsNullOrEmpty(_originalModelPath) && _meshData == null)
            {
                ConsoleViewModel.Log($"Reloading model from: {_originalModelPath}");
                
                // Reload the entire model
                var modelData = await Assets.ModelImporter.LoadModelDataAsync(_originalModelPath);
                if (modelData != null && modelData.Meshes.Count > 0)
                {
                    // Find which mesh we are by GameObject name
                    var myMeshData = modelData.Meshes.FirstOrDefault(m => m.Name == GameObject?.Name);
                    if (myMeshData != null)
                    {
                        _meshData = myMeshData;
                        ClearResources(); // Invalidate resources
                        ConsoleViewModel.Log($"Reloaded mesh '{myMeshData.Name}'");
                    }
                }
            }
        }

        /// <summary>
        /// Load material from file path
        /// </summary>
        /// <summary>
        /// Load material from file path
        /// </summary>
        private void LoadMaterial()
        {
            // Use default material if no material path is set
            if (string.IsNullOrEmpty(_materialPath))
            {
                var defaultPath = Assets.MaterialAssetManager.Instance.GetDefaultMaterialPath();
                ConsoleViewModel.Log($"[ModelRenderer] No material assigned, using default: {defaultPath}");
                _materialPath = defaultPath;
            }
            
            if (string.IsNullOrEmpty(_materialPath))
            {
                Material = null;
                _loadedShaderPath = null;
                return;
            }

            try
            {
                if (!System.IO.File.Exists(_materialPath))
                {
                    ConsoleViewModel.Log($"[ModelRenderer] Material file not found: {_materialPath}, falling back to default");
                    _materialPath = Assets.MaterialAssetManager.Instance.GetDefaultMaterialPath();
                    
                    if (!System.IO.File.Exists(_materialPath))
                    {
                        Material = Materials.PBRMaterial.CreateDefault();
                        return;
                    }
                }

                string json = System.IO.File.ReadAllText(_materialPath);
                var data = System.Text.Json.JsonSerializer.Deserialize<MaterialData>(json);

                if (data == null)
                {
                    Material = Materials.PBRMaterial.CreateDefault();
                    return;
                }

                _loadedShaderPath = data.shaderPath;

                var mat = new Materials.PBRMaterial
                {
                    Name = data.name ?? "Material",
                    Metallic = data.metallic,
                    Roughness = data.roughness,
                    AmbientOcclusion = data.ambientOcclusion
                };

                if (data.albedoColor != null && data.albedoColor.Length >= 3)
                {
                    mat.AlbedoColor = new Color(
                        (byte)(data.albedoColor[0] * 255),
                        (byte)(data.albedoColor[1] * 255),
                        (byte)(data.albedoColor[2] * 255)
                    );
                }
        
        // Store texture paths for lazy loading during Draw
        _pendingTexturePaths.Clear();
        // _texturesNeedLoading = false;
        
        if (!string.IsNullOrEmpty(data.albedoMap))
        {
            _pendingTexturePaths["albedo"] = data.albedoMap;
        }
        
        if (!string.IsNullOrEmpty(data.normalMap))
        {
            _pendingTexturePaths["normal"] = data.normalMap;
        }
        
        if (!string.IsNullOrEmpty(data.metallicMap))
        {
            _pendingTexturePaths["metallic"] = data.metallicMap;
        }
        
        if (!string.IsNullOrEmpty(data.roughnessMap))
        {
            _pendingTexturePaths["roughness"] = data.roughnessMap;
        }
        
        if (!string.IsNullOrEmpty(data.aoMap))
        {
            _pendingTexturePaths["ao"] = data.aoMap;
        }
        
        if (_pendingTexturePaths.Count > 0)
        {
            ConsoleViewModel.Log($"[ModelRenderer] Stored {_pendingTexturePaths.Count} texture paths for lazy loading");
        }
                
                // Load Custom Properties
                if (data.customProperties != null)
                {
                    foreach (var kvp in data.customProperties)
                    {
                        var value = DeserializePropertyValue(kvp.Value);
                        if (value != null)
                        {
                            mat.CustomProperties[kvp.Key] = value;
                        }
                    }
                }

                // If ShaderPath is defined in JSON, we might want to use it to load specific effect
                // But ModelRenderer currently manages Effect separately via device resources.
                // For now, we trust the PBRMaterial properties will be applied to whatever effect is used.
                
                Material = mat;
                ConsoleViewModel.Log($"[ModelRenderer] Loaded Material '{mat.Name}' from {_materialPath}");
            }
            catch (Exception ex)
            {
                ConsoleViewModel.Log($"[ModelRenderer] Failed to load material: {ex.Message}");
                Material = Materials.PBRMaterial.CreateDefault();
            }
        }
        
        /// <summary>
        /// Ensures textures are loaded for the specific GraphicsDevice and assigned to the Material
        /// </summary>
        private void EnsureTexturesForDevice(GraphicsDevice device)
        {
            if (Material == null || _pendingTexturePaths.Count == 0)
                return;

            // Check if we already have textures for this device
            if (!_deviceTextureCache.ContainsKey(device))
            {
                _deviceTextureCache[device] = new Dictionary<string, Texture2D>();
            }

            var deviceCache = _deviceTextureCache[device];
            // bool materialNeedsUpdate = false;

            // Load missing textures for this device
            foreach (var kvp in _pendingTexturePaths)
            {
                string key = kvp.Key;
                string path = kvp.Value;

                if (!deviceCache.ContainsKey(key) || deviceCache[key].IsDisposed || deviceCache[key].GraphicsDevice != device)
                {
                    // Load texture for this specific device
                    Texture2D texture = null;
                    try
                    {
                        // Note: ContentManager might return a texture bound to a DIFFERENT device if shared.
                        // We must verify the device of the loaded texture.
                        // If ContentManager shares resources, it might be tricky. 
                        // For now, let's try to trust ContentManager or fallback to direct load if device mismatches.

                        // Optimization: Try to find existing texture in other caches to clone? No, copy data? Expensive.
                        // Just load from disk.
                        
                        texture = LoadTextureWithoutContentManager(path, device);
                        
                        if (texture != null)
                        {
                            deviceCache[key] = texture;
                            // ConsoleViewModel.Log($"[ModelRenderer] Loaded {key} for Device {device.GetHashCode()}");
                        }
                    }
                    catch (Exception ex)
                    {
                         // Suppress per-frame error spam, maybe log once
                    }
                }
                
                // If we have a valid texture in cache, ensure it's on the material
                if (deviceCache.ContainsKey(key))
                {
                    var tex = deviceCache[key];
                    
                    // Assign to Material properties based on key
                    // We ALWAYS assign because the previous Draw might have been for a different device
                    switch (key)
                    {
                        case "albedo":
                            if (Material.AlbedoMap != tex) { Material.AlbedoMap = tex; }
                            break;
                        case "normal":
                            if (Material.NormalMap != tex) { Material.NormalMap = tex; }
                            break;
                        case "metallic":
                            if (Material.MetallicMap != tex) { Material.MetallicMap = tex; }
                            break;
                        case "roughness":
                            if (Material.RoughnessMap != tex) { Material.RoughnessMap = tex; }
                            break;
                        case "ao":
                            if (Material.AOMap != tex) { Material.AOMap = tex; }
                            break;
                    }
                }
            }
            
            // if (materialNeedsUpdate) ConsoleViewModel.Log($"[ModelRenderer] Swapped textures for Device {device.GetHashCode()}");
        }

        private Texture2D LoadTextureFromPath(Microsoft.Xna.Framework.Content.ContentManager contentManager, string relativePath)
        {
            // Remove file extension if present (ContentManager doesn't need it)
            string assetName = System.IO.Path.ChangeExtension(relativePath, null);
            
            // Try ContentManager first
            try
            {
                return contentManager.Load<Texture2D>(assetName);
            }
            catch
            {
                // ContentManager failed, try direct file loading
                string projectPath = ProjectManager.Instance.ProjectPath ?? "";
                string fullPath = System.IO.Path.Combine(projectPath, relativePath);
                
                if (System.IO.File.Exists(fullPath))
                {
                    using (var stream = System.IO.File.OpenRead(fullPath))
                    {
                        return Texture2D.FromStream(MonoGameControl.SharedGraphicsDevice, stream);
                    }
                }
                else
                {
                    throw new System.IO.FileNotFoundException($"Texture not found: {fullPath}");
                }
            }
        }
        
        private Texture2D LoadTextureWithoutContentManager(string relativePath, GraphicsDevice device = null)
        {
            // Direct file loading without ContentManager
            string projectPath = ProjectManager.Instance.ProjectPath ?? "";
            string fullPath = System.IO.Path.Combine(projectPath, relativePath);
            
            if (System.IO.File.Exists(fullPath))
            {
                using (var stream = System.IO.File.OpenRead(fullPath))
                {
                    // Use provided device or try multiple sources
                    var graphicsDevice = device 
                                      ?? MonoGameControl.SharedGraphicsDevice 
                                      ?? GameControl.SharedGraphicsDevice;
                                      
                    if (graphicsDevice != null)
                    {
                        return Texture2D.FromStream(graphicsDevice, stream);
                    }
                    else
                    {
                        throw new InvalidOperationException("No GraphicsDevice available");
                    }
                }
            }
            else
            {
                throw new System.IO.FileNotFoundException($"Texture not found: {fullPath}");
            }
        }

        private object DeserializePropertyValue(System.Text.Json.JsonElement element)
        {
            try
            {
                switch (element.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.Number:
                        return element.GetSingle();
                    case System.Text.Json.JsonValueKind.True:
                    case System.Text.Json.JsonValueKind.False:
                        return element.GetBoolean();
                    case System.Text.Json.JsonValueKind.String:
                        return element.GetString();
                    case System.Text.Json.JsonValueKind.Object:
                        // Enable fields for Vector serialization
                        var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
                        try { return System.Text.Json.JsonSerializer.Deserialize<Vector4>(element.GetRawText(), options); } catch { return null; }
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        // Private DTO for JSON deserialization
        private class MaterialData
        {
            public string? name { get; set; }
            public float[]? albedoColor { get; set; }
            public float metallic { get; set; }
            public float roughness { get; set; }
            public float ambientOcclusion { get; set; }
            public string? albedoMap { get; set; }
            public string? normalMap { get; set; }
            public string? metallicMap { get; set; }
            public string? roughnessMap { get; set; }
            public string? aoMap { get; set; }
            public string? heightMap { get; set; }
            public string? shaderPath { get; set; }
            public Dictionary<string, System.Text.Json.JsonElement>? customProperties { get; set; }
        }


        public void Draw(GraphicsDevice device, Matrix view, Matrix projection, Vector3 cameraPosition, 
            Texture2D shadowMap = null, Matrix? lightViewProj = null)
        {
            // Check for disposed device
            if (device == null || device.IsDisposed) return;

            // Use _meshData if set (hierarchical), otherwise use _metadata (legacy single-mesh)
            var hasData = _meshData != null || (_metadata != null && _metadata.PreviewVertices.Count > 0);
            if (!hasData) return;

            // Get or create resources for this specific device
            if (!_deviceResources.TryGetValue(device, out var resources))
            {
                resources = CreateResourcesForDevice(device);
                if (resources == null) return; // Failed to create
                _deviceResources[device] = resources;
            }
            
            
            // RETRY LOGIC: If we have BasicEffect, try to upgrade to PBR
            var sharedContent = MonoGameEditor.Controls.GameControl.SharedContent;
            var ownContent = MonoGameEditor.Controls.MonoGameControl.OwnContentManager;
            var isBasicEffect = resources.Effect is BasicEffect;
            
            // Determine which ContentManager to use
            var contentToUse = sharedContent ?? ownContent;
            
            if (isBasicEffect && contentToUse != null)
            {
                try
                {
                    // Use PBREffectLoader with the ContentManager
                    var pbrEffect = PBREffectLoader.Load(device, contentToUse);
                    if (pbrEffect != null)
                    {
                        // Upgrade to PBR Effect!
                        resources.Effect = pbrEffect;
                        ConsoleViewModel.Log($"[ModelRenderer] ✅ Upgraded {GameObject?.Name} to PBR!");
                    }
                }
                catch (Exception ex)
                {
                    ConsoleViewModel.Log($"[ModelRenderer] PBR upgrade failed: {ex.Message}");
                }
            }
            
            // Double check validity (in case of device loss/reset handling if manual)
            if (resources.VertexBuffer == null || resources.VertexBuffer.IsDisposed ||
                resources.Effect == null || resources.Effect.IsDisposed)
            {
                _deviceResources.Remove(device);
                return;
            }

            // Ensure textures are loaded for THIS device (handles multi-device caching)
            EnsureTexturesForDevice(device);
            
            // Note: EnsureTexturesForDevice handles assigning correct textures to Material
            // before we apply the effect.


            // Set matrices (works for both BasicEffect and custom Effect)
            Matrix world = GameObject != null ? GameObject.Transform.WorldMatrix : Matrix.Identity;
            
            // Check if it's a BasicEffect (easier API) or generic Effect (parameter-based)
            if (resources.Effect is BasicEffect basicEffect)
            {
                // Ensure Material is not null - use default if needed
                var materialToUse = Material ?? Materials.PBRMaterial.CreateDefault();
                
                basicEffect.World = world;
                basicEffect.View = view;
                basicEffect.Projection = projection;
                
                // Update light every frame (for real-time changes)
                var sceneLight = FindSceneLight();
                if (sceneLight != null)
                {
                    if (DateTime.Now.Millisecond < 10)
                        ConsoleViewModel.Log($"[ModelRenderer] Scene light found: {sceneLight.Name}");
                    
                    var lightComp = sceneLight.GetComponent<LightComponent>();
                    basicEffect.DirectionalLight0.Enabled = true;

                    // Calculate Light Color: BaseColor * Kelvin * Intensity
                    var baseColor = lightComp.Color.ToVector3(); // 0-1 (sRGB approx)
                    // Linearize manually
                    baseColor = new Vector3(
                        (float)Math.Pow(baseColor.X, 2.2),
                        (float)Math.Pow(baseColor.Y, 2.2),
                        (float)Math.Pow(baseColor.Z, 2.2));
                    
                    var tempColor = KelvinToRGB(lightComp.Temperature);
                    var finalColor = baseColor * tempColor * lightComp.Intensity;
                    
                    if (DateTime.Now.Millisecond < 10)
                        ConsoleViewModel.Log($"[ModelRenderer] Light intensity={lightComp.Intensity}, finalColor=({finalColor.X:F2},{finalColor.Y:F2},{finalColor.Z:F2})");
                    
                    basicEffect.DirectionalLight0.DiffuseColor = finalColor; // Can exceed 1.0 in HDR
                    
                    // Specular usually follows light color but can be styled
                    basicEffect.DirectionalLight0.SpecularColor = finalColor; 

                    // Indirect/Ambient
                    var ambientM = lightComp.IndirectMultiplier;
                    basicEffect.AmbientLightColor = new Vector3(lightComp.AmbientIntensity) * ambientM; // User controlled ambient

                    // Direction
                    var lightDir = sceneLight.Transform.Forward;
                    basicEffect.DirectionalLight0.Direction = Vector3.Normalize(lightDir);
                }
                else
                {
                    // Fallback if no light found
                    if (DateTime.Now.Millisecond < 10)
                        ConsoleViewModel.Log("[ModelRenderer] No scene light found, using fallback lighting");
                    basicEffect.DirectionalLight0.Enabled = true;
                    basicEffect.DirectionalLight0.DiffuseColor = new Vector3(0.8f);
                    basicEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.5f, -1f, -0.5f));
                    basicEffect.AmbientLightColor = new Vector3(0.15f); // Fallback ambient increased
                }
                
                // Apply material color (Linearize albedo)
                var albedo = materialToUse.AlbedoColor.ToVector3();
                albedo = new Vector3(
                    (float)Math.Pow(albedo.X, 2.2),
                    (float)Math.Pow(albedo.Y, 2.2),
                    (float)Math.Pow(albedo.Z, 2.2));
                
                
                basicEffect.DiffuseColor = albedo;
                
                // For metallic simulation with BasicEffect, adjust specular
                if (materialToUse.Metallic > 0.5f)
                {
                    basicEffect.SpecularColor = albedo * materialToUse.Metallic;
                    basicEffect.SpecularPower = (1.0f - materialToUse.Roughness) * 128f;
                }
                else
                {
                    basicEffect.SpecularColor = Vector3.One * 0.04f; // Dielectric F0
                    basicEffect.SpecularPower = (1.0f - materialToUse.Roughness) * 32f;
                }
            }
            else
            {
                // Ensure Material is not null - use default if needed
                var materialToUse = Material ?? Materials.PBRMaterial.CreateDefault();
                
                // Generic Effect - use Parameters
                resources.Effect.Parameters["World"]?.SetValue(world);
                resources.Effect.Parameters["View"]?.SetValue(view);
                resources.Effect.Parameters["Projection"]?.SetValue(projection);
                resources.Effect.Parameters["CameraPosition"]?.SetValue(cameraPosition);
                
                // Handle Light Params if PBR shader supports them
                var sceneLight = FindSceneLight();
                if (sceneLight != null) 
                {
                     var lightComp = sceneLight.GetComponent<LightComponent>();
                     var baseColor = lightComp.Color.ToVector3();
                     baseColor = new Vector3(
                        (float)Math.Pow(baseColor.X, 2.2),
                        (float)Math.Pow(baseColor.Y, 2.2),
                        (float)Math.Pow(baseColor.Z, 2.2));
                        
                     var tempColor = KelvinToRGB(lightComp.Temperature);
                     var finalColor = baseColor * tempColor * lightComp.Intensity;
                     
                            // 4. Shadow Parameters
                            bool castShadowsGlobal = lightComp.CastShadows;
                            bool receiveShadowsLocal = ReceiveShadows;
                            
                            // Enable shadows ONLY if: Light casts them, Object receives them, and ShadowMap is provided
                            if (castShadowsGlobal && receiveShadowsLocal && shadowMap != null && lightViewProj.HasValue)
                            {
                                resources.Effect.Parameters["UseShadows"]?.SetValue(true);
                                resources.Effect.Parameters["ShadowMap"]?.SetValue(shadowMap);
                                resources.Effect.Parameters["LightViewProjection"]?.SetValue(lightViewProj.Value);
                                resources.Effect.Parameters["ShadowQuality"]?.SetValue((int)lightComp.Quality);
                                resources.Effect.Parameters["ShadowStrength"]?.SetValue(lightComp.ShadowStrength);
                                resources.Effect.Parameters["ShadowBias"]?.SetValue(lightComp.ShadowBias);
                            }
                            else
                            {
                                resources.Effect.Parameters["UseShadows"]?.SetValue(false);
                            }


                     resources.Effect.Parameters["LightDirection"]?.SetValue(sceneLight.Transform.Forward);
                     resources.Effect.Parameters["LightColor"]?.SetValue(finalColor);
                     resources.Effect.Parameters["LightIntensity"]?.SetValue(lightComp.Intensity); // CRITICAL FIX: Was never set!
                     resources.Effect.Parameters["IndirectMultiplier"]?.SetValue(lightComp.IndirectMultiplier);
                }
                
                // Set these if not set by light block (safeguard)
                if (sceneLight == null)
                    resources.Effect.Parameters["UseShadows"]?.SetValue(false);

                // Apply material
                materialToUse.Apply(resources.Effect);
            }

            // Enable backface culling to hide back faces
            var previousRasterizerState = device.RasterizerState;
            device.RasterizerState = RasterizerState.CullClockwise;

            foreach (var pass in resources.Effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.SetVertexBuffer(resources.VertexBuffer);
                device.Indices = resources.IndexBuffer;
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, resources.IndexBuffer.IndexCount / 3);
            }

            // Restore previous state
            device.RasterizerState = previousRasterizerState;
        }

        private Vector3 KelvinToRGB(float k)
        {
            // Simple approximation for ~1000K to ~40000K
            // Algorithm based on Tanner Helland's method
            var color = new Vector3();
            k = MathHelper.Clamp(k, 1000, 40000) / 100f;
            
            // Red
            if (k <= 66) color.X = 1f;
            else
            {
                var t = k - 60f;
                color.X = 329.698727446f * (float)Math.Pow(t, -0.1332047592f);
                color.X /= 255f;
            }
            
            // Green
            if (k <= 66)
            {
                var t = k;
                color.Y = 99.4708025861f * (float)Math.Log(t) - 161.1195681661f;
                color.Y /= 255f;
            }
            else
            {
                var t = k - 60f;
                color.Y = 288.1221695283f * (float)Math.Pow(t, -0.0755148492f);
                color.Y /= 255f;
            }
            
            // Blue
            if (k >= 66) color.Z = 1f;
            else if (k <= 19) color.Z = 0f;
            else
            {
                var t = k - 10f;
                color.Z = 138.5177312231f * (float)Math.Log(t) - 305.0447927307f;
                color.Z /= 255f;
            }
            
            return Vector3.Clamp(color, Vector3.Zero, Vector3.One);
        }

        private DeviceResources CreateResourcesForDevice(GraphicsDevice device)
        {
            // Guard: If data isn't loaded yet, we can't create resources
            if (_meshData == null && _metadata == null)
            {
                // ConsoleViewModel.Log($"[ModelRenderer] Skip Resource Creation: No data for {GameObject?.Name}");
                return null;
            }

            List<System.Numerics.Vector3> vertices;
            List<System.Numerics.Vector3> normals;
            List<System.Numerics.Vector2> texCoords;
            List<int> indices;

            if (_meshData != null)
            {
                vertices = _meshData.Vertices;
                normals = _meshData.Normals;
                texCoords = _meshData.TexCoords;
                indices = _meshData.Indices;
            }
            else
            {
                vertices = _metadata.PreviewVertices;
                normals = new List<System.Numerics.Vector3>(); // Legacy mode: generate fake normals
                texCoords = new List<System.Numerics.Vector2>();
                for (int i = 0; i < vertices.Count; i++)
                {
                    normals.Add(new System.Numerics.Vector3(0, 1, 0));
                    texCoords.Add(System.Numerics.Vector2.Zero);
                }
                indices = _metadata.PreviewIndices;
            }
            
            var uniqueVerts = new VertexPositionNormalTexture[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                var n = normals[i];
                var uv = (i < texCoords.Count) ? texCoords[i] : System.Numerics.Vector2.Zero;
                
                uniqueVerts[i] = new VertexPositionNormalTexture(
                    new Vector3(v.X, v.Y, v.Z),
                    new Vector3(n.X, n.Y, n.Z),
                    new Vector2(uv.X, uv.Y)
                );
            }
            
            var resources = new DeviceResources();
            
            try 
            {
                resources.VertexBuffer = new VertexBuffer(device, typeof(VertexPositionNormalTexture), uniqueVerts.Length, BufferUsage.WriteOnly);
                resources.VertexBuffer.SetData(uniqueVerts);

                resources.IndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.WriteOnly);
                resources.IndexBuffer.SetData(indices.ToArray());

                ConsoleViewModel.Log($"[ModelRenderer] CreateResourcesForDevice for {GameObject?.Name}");
                
                // Try to load Custom Shader if defined
                Effect? loadedEffect = null;
                if (!string.IsNullOrEmpty(_loadedShaderPath))
                {
                     ConsoleViewModel.Log($"[ModelRenderer] Attempting custom shader: {_loadedShaderPath}");
                     try 
                     {
                        // Logic to load shader from path. 
                        // If it's a file path to .mgfxo, we can load bytes.
                        // If it's a Content path, we use ContentManager.
                        
                        // Simplistic approach: Check if .mgfxo exists at path
                        if (System.IO.File.Exists(_loadedShaderPath))
                        {
                            var bytes = System.IO.File.ReadAllBytes(_loadedShaderPath);
                            loadedEffect = new Effect(device, bytes);
                            ConsoleViewModel.Log($"[ModelRenderer] ✅ Loaded Custom Shader: {_loadedShaderPath}");
                        }
                        else
                        {
                             ConsoleViewModel.Log($"[ModelRenderer] Custom shader file not found: {_loadedShaderPath}");
                             // Try ContentManager if it looks like a content path
                             var content = MonoGameEditor.Controls.GameControl.SharedContent ?? MonoGameEditor.Controls.MonoGameControl.OwnContentManager;
                             if (content != null)
                             {
                                 // Strip extension and folder if needed purely for Content.Load, but strictly strictly relies on correct pathing
                                 // For now, let's assume it failed file check.
                                 ConsoleViewModel.Log($"[ModelRenderer] ContentManager available but no content loading attempted");
                             }
                        }
                     }
                     catch (Exception ex)
                     {
                         ConsoleViewModel.Log($"[ModelRenderer] ❌ Failed to load custom shader '{_loadedShaderPath}': {ex.Message}");
                     }
                }

                // Fallback to PBR or Basic
                if (loadedEffect != null)
                {
                    resources.Effect = loadedEffect;
                    ConsoleViewModel.Log($"[ModelRenderer] Using custom shader for {GameObject?.Name}");
                }
                else
                {
                    ConsoleViewModel.Log($"[ModelRenderer] Attempting PBR shader load...");
                    
                    // CRITICAL FIX: Pass ContentManager explicitly!
                    // Try GameControl.SharedContent first (for Game View), then MonoGameControl (for Scene View)
                    var contentMgr = MonoGameEditor.Controls.GameControl.SharedContent 
                                  ?? MonoGameEditor.Controls.MonoGameControl.OwnContentManager;
                    
                    var pbrEffect = PBREffectLoader.Load(device, contentMgr);
                    if (pbrEffect != null)
                    {
                        resources.Effect = pbrEffect;
                        ConsoleViewModel.Log($"[ModelRenderer] ✅ Using PBR Effect for {GameObject?.Name}");
                    }
                    else
                    {
                        ConsoleViewModel.Log($"[ModelRenderer] ⚠️ PBR failed, using BasicEffect for {GameObject?.Name}");
                        // Fallback to BasicEffect
                        var initialEffect = new BasicEffect(device);
                        
                        // Configure lighting manually
                        initialEffect.LightingEnabled = true;
                        initialEffect.PreferPerPixelLighting = true;
                        initialEffect.DirectionalLight1.Enabled = false;
                        initialEffect.DirectionalLight2.Enabled = false;
                        
                        resources.Effect = initialEffect;
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleViewModel.Log($"Failed to create resources for device: {ex.Message}");
                return null;
            }

            return resources;
        }

        /// <summary>
        /// Draws the model using a custom effect (e.g. Shadow Depth shader)
        /// overrideWorld: Optional custom matrix
        /// </summary>
        public void DrawWithCustomEffect(Effect customEffect, Matrix lightViewProj)
        {
            // MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[ShadowDebug] Request Draw {GameObject.Name}");

            var device = customEffect.GraphicsDevice;
            
            // Re-add resource creation in case this pass runs first!
            if (!_deviceResources.ContainsKey(device))
            {
                // MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[ShadowDebug] Creating resources for {GameObject.Name}");
                CreateResourcesForDevice(device);
            }

            if (!_deviceResources.TryGetValue(device, out var resources)) 
            {
                 // MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[ShadowDebug] {GameObject.Name} No Resources Found!");
                 return;
            }
            
            if (resources.VertexBuffer == null || resources.IndexBuffer == null)
            {
                 // MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[ShadowDebug] {GameObject.Name} Buffers are null!");
                 return;
            }

            Matrix world = GameObject != null ? GameObject.Transform.WorldMatrix : Matrix.Identity;

            // Apply global params
            customEffect.Parameters["World"]?.SetValue(world);
            customEffect.Parameters["LightViewProjection"]?.SetValue(lightViewProj);
            
            // Draw
            foreach (var pass in customEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.SetVertexBuffer(resources.VertexBuffer);
                device.Indices = resources.IndexBuffer;
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, resources.IndexBuffer.IndexCount / 3);
            }
        }

        /// <summary>
        /// Finds the first directional light in the scene
        /// </summary>
        private GameObject FindSceneLight()
        {
            var scene = SceneManager.Instance;
            if (scene == null) return null;

            foreach (var root in scene.RootObjects)
            {
                var light = FindLightRecursive(root);
                if (light != null) return light;
            }
            return null;
        }

        private GameObject FindLightRecursive(GameObject node)
        {
            // Check if this GameObject has a LightComponent
            var lightComp = node.GetComponent<LightComponent>();
            if (lightComp != null && lightComp.LightType == LightType.Directional)
                return node;

            // Check children
            foreach (var child in node.Children)
            {
                var light = FindLightRecursive(child);
                if (light != null) return light;
            }
            return null;
        }
    }
}
