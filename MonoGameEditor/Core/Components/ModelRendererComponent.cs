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
        
        // PBR Material
        private PBRMaterial _material;
        public PBRMaterial Material
        {
            get => _material ??= PBRMaterial.CreateDefault();
            set => _material = value;
        }

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

        public void Draw(GraphicsDevice device, Matrix view, Matrix projection, Vector3 cameraPosition)
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
            
            // Double check validity (in case of device loss/reset handling if manual)
            if (resources.VertexBuffer == null || resources.VertexBuffer.IsDisposed ||
                resources.Effect == null || resources.Effect.IsDisposed)
            {
                _deviceResources.Remove(device);
                return;
            }

            // Set matrices (works for both BasicEffect and custom Effect)
            Matrix world = GameObject != null ? GameObject.Transform.WorldMatrix : Matrix.Identity;
            
            // Check if it's a BasicEffect (easier API) or generic Effect (parameter-based)
            if (resources.Effect is BasicEffect basicEffect)
            {
                basicEffect.World = world;
                basicEffect.View = view;
                basicEffect.Projection = projection;
                
                // Update light every frame (for real-time changes)
                var sceneLight = FindSceneLight();
                if (sceneLight != null)
                {
                    var lightComp = sceneLight.GetComponent<LightComponent>();
                    basicEffect.DirectionalLight0.Enabled = true;
                    basicEffect.DirectionalLight0.DiffuseColor = lightComp.Color.ToVector3() * lightComp.Intensity;
                    basicEffect.DirectionalLight0.SpecularColor = lightComp.Color.ToVector3() * (lightComp.Intensity * 0.5f);
                    
                    // Get light direction from Transform forward vector
                    var lightDir = sceneLight.Transform.Forward;
                    basicEffect.DirectionalLight0.Direction = Vector3.Normalize(lightDir);
                    
                    // Softer ambient
                    basicEffect.AmbientLightColor = new Vector3(0.15f, 0.15f, 0.15f);
                }
                else
                {
                    // Fallback if no light found
                    basicEffect.DirectionalLight0.Enabled = true;
                    basicEffect.DirectionalLight0.DiffuseColor = new Vector3(0.8f, 0.8f, 0.8f);
                    basicEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.5f, -1f, -0.5f));
                    basicEffect.DirectionalLight0.SpecularColor = new Vector3(0.3f, 0.3f, 0.3f);
                    basicEffect.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.2f);
                }
                
                // Apply material color
                var albedo = Material.AlbedoColor.ToVector3();
                basicEffect.DiffuseColor = albedo;
                
                // For metallic simulation with BasicEffect, adjust specular
                if (Material.Metallic > 0.5f)
                {
                    basicEffect.SpecularColor = albedo * Material.Metallic;
                    basicEffect.SpecularPower = (1.0f - Material.Roughness) * 128f;
                }
                else
                {
                    basicEffect.SpecularColor = Vector3.One * 0.2f;
                    basicEffect.SpecularPower = (1.0f - Material.Roughness) * 32f;
                }
            }
            else
            {
                // Generic Effect - use Parameters
                resources.Effect.Parameters["World"]?.SetValue(world);
                resources.Effect.Parameters["View"]?.SetValue(view);
                resources.Effect.Parameters["Projection"]?.SetValue(projection);
                resources.Effect.Parameters["CameraPosition"]?.SetValue(cameraPosition);
                
                // Apply material
                Material.Apply(resources.Effect);
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

        private DeviceResources CreateResourcesForDevice(GraphicsDevice device)
        {
            List<System.Numerics.Vector3> vertices;
            List<System.Numerics.Vector3> normals;
            List<int> indices;

            if (_meshData != null)
            {
                vertices = _meshData.Vertices;
                normals = _meshData.Normals;
                indices = _meshData.Indices;
            }
            else
            {
                vertices = _metadata.PreviewVertices;
                normals = new List<System.Numerics.Vector3>(); // Legacy mode: generate fake normals
                for (int i = 0; i < vertices.Count; i++)
                    normals.Add(new System.Numerics.Vector3(0, 1, 0));
                indices = _metadata.PreviewIndices;
            }
            
            var uniqueVerts = new VertexPositionNormalTexture[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                var n = normals[i];
                uniqueVerts[i] = new VertexPositionNormalTexture(
                    new Vector3(v.X, v.Y, v.Z),
                    new Vector3(-n.X, -n.Y, -n.Z), // Invert normals to fix orientation
                    Vector2.Zero
                );
            }
            
            var resources = new DeviceResources();
            
            try 
            {
                resources.VertexBuffer = new VertexBuffer(device, typeof(VertexPositionNormalTexture), uniqueVerts.Length, BufferUsage.WriteOnly);
                resources.VertexBuffer.SetData(uniqueVerts);

                resources.IndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.WriteOnly);
                resources.IndexBuffer.SetData(indices.ToArray());

                // Try to load PBR shader
                var pbrEffect = PBREffectLoader.Load(device);
                if (pbrEffect != null)
                {
                    resources.Effect = pbrEffect;
                }
                else
                {
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
            catch (Exception ex)
            {
                ConsoleViewModel.Log($"Failed to create resources for device: {ex.Message}");
                return null;
            }

            return resources;
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
