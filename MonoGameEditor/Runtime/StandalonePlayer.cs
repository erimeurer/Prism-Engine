using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;
using MonoGameEditor.Core.Graphics;

namespace MonoGameEditor.Runtime
{
    public class StandalonePlayer : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch? _spriteBatch;
        
        private ShadowRenderer? _shadowRenderer;
        private ProceduralSkybox? _skybox;
        private ToneMapRenderer? _toneMap;
        private RenderTarget2D? _hdrRenderTarget;

        private ProjectSettings? _settings;
        private CameraComponent? _mainCamera;

        public StandalonePlayer()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            
            // Default window settings
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.GraphicsProfile = GraphicsProfile.HiDef;
            
            // CRITICAL: Ensure window can receive input
            Window.AllowUserResizing = true;
            
            // Subscribe to activation events for debugging
            Activated += (s, e) => Core.Logger.Log("[StandalonePlayer] ✅ Window ACTIVATED - should receive input now!");
            Deactivated += (s, e) => Core.Logger.Log("[StandalonePlayer] ⚠️ Window DEACTIVATED - input will not work!");
            
            Core.Logger.Log("[StandalonePlayer] Constructor finished");
        }

        protected override void Initialize()
        {
            // Initialize logger for Core first
            Core.Logger.Initialize(new RuntimeLogger());
            Core.Logger.Log("[StandalonePlayer] Initialize started");
            
            _graphics.ApplyChanges();

            // Load Project Settings
            string settingsPath = "ProjectSettings.json";
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);
                _settings = System.Text.Json.JsonSerializer.Deserialize<ProjectSettings>(json) ?? new ProjectSettings();
                Window.Title = _settings.ProjectName;
            }

            // Initialize Core Systems
            ProjectManager.Instance.OpenProject(Directory.GetCurrentDirectory());
            ScriptManager.Instance.DiscoverAndCompileScripts();
            
            base.Initialize();
            Core.Logger.Log("[StandalonePlayer] Initialize finished");
        }

        protected override void LoadContent()
        {
            Core.Logger.Log("[StandalonePlayer] LoadContent started");
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            
            // Set shared services for components
            GraphicsManager.GraphicsDevice = GraphicsDevice;
            GraphicsManager.ContentManager = Content;
            
            // Initialize renderers with correct constructors
            _shadowRenderer = new ShadowRenderer(GraphicsDevice, Content);
            
            _skybox = new ProceduralSkybox();
            _skybox.Initialize(GraphicsDevice);
            
            _toneMap = new ToneMapRenderer(GraphicsDevice);
            _toneMap.Initialize(Content);
            
            CreateHDRTarget();

            // Load first scene
            string? sceneToLoad = _settings?.ScenesInBuild?.FirstOrDefault();
            if (!string.IsNullOrEmpty(sceneToLoad) && File.Exists(sceneToLoad))
            {
                Core.Logger.Log($"[StandalonePlayer] Loading scene: {sceneToLoad}");
                // USE SYNCHRONOUS LOAD for standalone to ensure everything is ready before first frame
                // This prevents race conditions with async loading
                SceneManager.Instance.LoadScene(sceneToLoad);
                Core.Logger.Log("[StandalonePlayer] Scene loaded successfully.");
            }
            else
            {
                Core.Logger.Log("[StandalonePlayer] No scene to load or file missing, creating default");
                SceneManager.Instance.CreateDefaultScene();
            }
            
            // Verify scripts
            int scriptCount = ScriptManager.Instance.ScriptAssets.Count;
            Core.Logger.Log($"[StandalonePlayer] Total scripts compiled and registered: {scriptCount}");
            
            Core.Logger.Log("[StandalonePlayer] LoadContent finished");
        }

        private void CreateHDRTarget()
        {
            _hdrRenderTarget?.Dispose();
            // Disabled MSAA (set to 0) for reliability on different hardware
            _hdrRenderTarget = new RenderTarget2D(GraphicsDevice, 
                GraphicsDevice.PresentationParameters.BackBufferWidth, 
                GraphicsDevice.PresentationParameters.BackBufferHeight, 
                false, SurfaceFormat.HalfVector4, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
            Core.Logger.Log($"[StandalonePlayer] Created HDR Target: {GraphicsDevice.PresentationParameters.BackBufferWidth}x{GraphicsDevice.PresentationParameters.BackBufferHeight}");
        }

        protected override void Update(GameTime gameTime)
        {
            // Update Input
            Core.Input.Update();
            
            // Update Physics
            if (PhysicsManager.Instance.Gravity == Vector3.Zero)
            {
                PhysicsManager.Instance.Gravity = new Vector3(0, -9.81f, 0);
            }
            PhysicsManager.Instance.Update(gameTime);
            
            // Update Scripts
            ScriptManager.Instance.UpdateScripts(gameTime);

            // Find main camera
            var oldCamera = _mainCamera;
            _mainCamera = FindMainCamera();
            
            if (_mainCamera != null && oldCamera == null)
            {
                Core.Logger.Log($"[StandalonePlayer] Main Camera found: {_mainCamera.GameObject?.Name ?? "Unnamed"}");
            }
            else if (_mainCamera == null && oldCamera != null)
            {
                Core.Logger.Log("[StandalonePlayer] WARNING: Main Camera LOST!");
            }

            base.Update(gameTime);
        }

        private CameraComponent? FindMainCamera()
        {
            foreach (var obj in SceneManager.Instance.RootObjects)
            {
                var camera = FindCameraRecursive(obj);
                if (camera != null) return camera;
            }
            return null;
        }

        private CameraComponent? FindCameraRecursive(GameObject obj)
        {
            var camera = obj.GetComponent<CameraComponent>();
            if (camera != null && camera.IsMainCamera) return camera;
            
            foreach (var child in obj.Children)
            {
                var found = FindCameraRecursive(child);
                if (found != null) return found;
            }
            return null;
        }

        private LightComponent? FindFirstLight()
        {
            foreach (var obj in SceneManager.Instance.RootObjects)
            {
                var light = FindLightRecursive(obj);
                if (light != null) return light;
            }
            return null;
        }

        private LightComponent? FindLightRecursive(GameObject obj)
        {
            var light = obj.GetComponent<LightComponent>();
            if (light != null) return light;
            
            foreach (var child in obj.Children)
            {
                var found = FindLightRecursive(child);
                if (found != null) return found;
            }
            return null;
        }

        protected override void Draw(GameTime gameTime)
        {
            if (_mainCamera == null)
            {
                // Clear to Purple so we can distinguish from "normal" black
                GraphicsDevice.Clear(Color.Purple);
                return;
            }

            // Log camera position occasionally to verify it's not at NaN or origin unexpectedly
            if (gameTime.TotalGameTime.Seconds % 5 == 0 && gameTime.TotalGameTime.Milliseconds < 20)
            {
                Core.Logger.Log($"[StandalonePlayer] Camera Pos: {_mainCamera.GameObject.Transform.Position}");
            }

            Matrix view = Matrix.Invert(_mainCamera!.GameObject.Transform.WorldMatrix);
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(_mainCamera.FieldOfView), 
                GraphicsDevice.Viewport.AspectRatio, 
                _mainCamera.NearClip, 
                _mainCamera.FarClip);
            
            Vector3 camPos = _mainCamera.GameObject.Transform.Position;

            // 1. Shadow Pass
            Texture2D shadowMap = null;
            Matrix? lightViewProj = null;
            
            var mainLight = FindFirstLight();
            if (mainLight != null && _shadowRenderer != null)
            {
                _shadowRenderer.BeginPass(mainLight, camPos, _mainCamera.GameObject.Transform.Forward);
                foreach (var obj in SceneManager.Instance.RootObjects)
                    RenderShadowsRecursive(obj);
                _shadowRenderer.EndPass();
                
                shadowMap = _shadowRenderer.ShadowMap;
                lightViewProj = _shadowRenderer.LightViewProjection;
            }

            // 2. Main Pass (Directly to Backbuffer for Debugging)
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(_mainCamera.BackgroundColor);

            if (_mainCamera.ClearFlags == CameraClearFlags.Skybox && _skybox != null)
            {
                _skybox.Draw(view, projection, camPos, _mainCamera.FarClip);
            }

            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            foreach (var obj in SceneManager.Instance.RootObjects)
            {
                RenderRecursively(obj, view, projection, shadowMap, lightViewProj);
            }

            // 3. Resolve Pass (Disabled for Debugging)
            /*
            if (_toneMap != null && _hdrRenderTarget != null)
            {
                _toneMap.Draw(_hdrRenderTarget);
            }
            */

            base.Draw(gameTime);
        }

        private void RenderRecursively(GameObject obj, Matrix view, Matrix projection, Texture2D? shadowMap, Matrix? lightViewProj)
        {
            if (!obj.IsActive) return;

            var renderer = obj.GetComponent<ModelRendererComponent>();
            if (renderer != null)
            {
                renderer.Draw(GraphicsDevice, view, projection, obj.Transform.Position, shadowMap, lightViewProj);
            }

            foreach (var child in obj.Children)
                RenderRecursively(child, view, projection, shadowMap, lightViewProj);
        }

        private void RenderShadowsRecursive(GameObject obj)
        {
            if (!obj.IsActive) return;

            var renderer = obj.GetComponent<ModelRendererComponent>();
            if (renderer != null)
                _shadowRenderer?.DrawObject(obj, renderer);

            foreach (var child in obj.Children)
                RenderShadowsRecursive(child);
        }
    }
}
