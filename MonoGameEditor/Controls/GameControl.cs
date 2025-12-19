using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WinForms = System.Windows.Forms;
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;
using MonoGameEditor.ViewModels;
using MonoGameEditor.Core.Graphics;
using System.Linq;
using System.Windows.Threading;

namespace MonoGameEditor.Controls
{
    /// <summary>
    /// Game View Control - Renders scene from Main Camera perspective
    /// </summary>
    public class GameControl : WinForms.Panel
    {
        private GraphicsDeviceService? _graphicsService;
        private bool _initialized = false;
        private WinForms.Timer _renderTimer;
        private DateTime _lastFrameTime;

        public ProceduralSkybox? Skybox => _skybox;
        private ProceduralSkybox? _skybox;
        private RenderTarget2D? _hdrRenderTarget;

        private ToneMapRenderer _toneMapRenderer;
        private ShadowRenderer _shadowRenderer;

        // Static ContentManager and GraphicsDevice shared across components
        private static Microsoft.Xna.Framework.Content.ContentManager? _sharedContent;
        public static Microsoft.Xna.Framework.Content.ContentManager? SharedContent 
        {
            get => _sharedContent;
            set
            {
                _sharedContent = value;
                if (value != null) GraphicsManager.ContentManager = value;
            }
        }
        
        private static GraphicsDevice? _sharedGraphicsDevice;
        public static GraphicsDevice? SharedGraphicsDevice
        {
            get => _sharedGraphicsDevice;
            set
            {
                _sharedGraphicsDevice = value;
                if (value != null) GraphicsManager.GraphicsDevice = value;
            }
        }

        public GraphicsDevice? GraphicsDevice => _graphicsService?.GraphicsDevice;

        public GameControl()
        {
            SetStyle(
                WinForms.ControlStyles.UserPaint | 
                WinForms.ControlStyles.AllPaintingInWmPaint | 
                WinForms.ControlStyles.Opaque,
                true);
            
            // CRITICAL: WinForms Panel doesn't receive keyboard events without this!
            TabStop = true; // Allow control to receive focus via Tab
            
            // Give focus when clicked
            Click += (s, e) => Focus();

            // Also try to focus when mouse enters
            MouseEnter += (s, e) => Focus();
            
            _renderTimer = new WinForms.Timer();
            _renderTimer.Interval = 16; // ~60 FPS
            _renderTimer.Tick += RenderTimer_Tick;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) _renderTimer?.Start();
            else _renderTimer?.Stop();
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            if (DesignMode) return;

            ConsoleViewModel.Log($"[GameControl] OnCreateControl: Handle={Handle}");
            _graphicsService = GraphicsDeviceService.AddRef(Handle, Width, Height);
            _sharedGraphicsDevice = _graphicsService.GraphicsDevice; // Store for global access
            
            // Setup Services for ContentManager
            var services = new Microsoft.Xna.Framework.GameServiceContainer();
            services.AddService(typeof(IGraphicsDeviceService), _graphicsService);
            
            var content = new Microsoft.Xna.Framework.Content.ContentManager(services, "Content");
            _sharedContent = content; // Store for use by PBREffectLoader
            GraphicsManager.RegisterContentManager(_graphicsService.GraphicsDevice, content);

            _toneMapRenderer = new ToneMapRenderer(GraphicsDevice!);
            _toneMapRenderer.Initialize(content);
            _shadowRenderer = new ShadowRenderer(GraphicsDevice!, content);
            ResizeHDRTarget(Width, Height, AntialiasingMode.MSAA_8x); // Default to 8x for Game view

            _initialized = true;
            _renderTimer.Start();
        }

        // Keep for compatibility but logic is in OnVisibleChanged
        public void SetRenderEnabled(bool enabled) 
        {
             if (enabled && Visible) _renderTimer.Start();
             else _renderTimer.Stop();
        }
        private void RenderTimer_Tick(object? sender, EventArgs e)
        {
            if (!_initialized || GraphicsDevice == null || !Visible) return;

            // Calculate Delta Time
            var currentTime = DateTime.Now;
            double deltaTime = (currentTime - _lastFrameTime).TotalSeconds;
            _lastFrameTime = currentTime;

            // Cap delta time to avoid huge spikes on tab switching/debugging
            deltaTime = Math.Min(deltaTime, 0.1);

            // Check Play Mode and Run Game Loop
            var vm = ViewModels.MainViewModel.Instance;
            if (vm != null && vm.IsPlaying && !vm.IsPaused)
            {
                UpdateGame(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(deltaTime)));
            }

            // Always render
            Invalidate();
        }

        private void UpdateGame(GameTime gameTime)
        {
            // CAPTURE FOCUS check
            if (!Focused)
            {
                // In Editor, sometimes we need to stay vigilant about focus
                // but we don't want to steal it if user is typing in another tool
                // if (WinForms.Form.ActiveForm != null) Focus();
            }

            // CRITICAL: Update Input state BEFORE scripts run
            Core.Input.Update();
            
            PhysicsManager.Instance.Update(gameTime);
            ScriptManager.Instance.UpdateScripts(gameTime);
        }

        protected override void OnPaint(WinForms.PaintEventArgs e)
        {
            if (!_initialized || GraphicsDevice == null) return;
            


            try 
            {
                if (GraphicsDevice.PresentationParameters.BackBufferWidth != Width ||
                    GraphicsDevice.PresentationParameters.BackBufferHeight != Height)
                {
                     if (Visible && Width > 0 && Height > 0)
                     {
                         try {
                            // Only reset if dimensions changed to avoid loops
                            GraphicsDevice.PresentationParameters.BackBufferWidth = Width;
                            GraphicsDevice.PresentationParameters.BackBufferHeight = Height;
                            GraphicsDevice.Reset();
                            ConsoleViewModel.Log($"[GameControl] Device Reset to {Width}x{Height}");
                         } catch (Exception ex) {
                            ConsoleViewModel.Log($"Reset failed: {ex.Message}");
                         }
                     }
                }
                
                // Set Viewport
                int safeWidth = Math.Min(Width, GraphicsDevice.PresentationParameters.BackBufferWidth);
                int safeHeight = Math.Min(Height, GraphicsDevice.PresentationParameters.BackBufferHeight);

                if (safeWidth > 0 && safeHeight > 0)
                {
                    GraphicsDevice.Viewport = new Viewport(0, 0, safeWidth, safeHeight);
                }
                else
                {
                    return;
                }
                
                // 1. Find Data (Main Camera only)
                CameraComponent? mainCamera = null;
                var scene = SceneManager.Instance;
    
                // Safe iteration
                var roots = scene.RootObjects;
                for (int i = 0; i < roots.Count; i++)
                {
                    var go = roots[i];
                    var comps = go.Components;
                    for(int j=0; j<comps.Count; j++)
                    {
                        if (comps[j] is CameraComponent cam && cam.IsMainCamera && cam.IsEnabled)
                        {
                            mainCamera = cam;
                            break;
                        }
                    }
                    if (mainCamera != null) break;
                }
                
                if (mainCamera == null)
                {
                    mainCamera = FindMainCameraRecursive(scene.RootObjects);
                }
    
                // Ensure HDR Target is valid
                if (mainCamera != null && (_hdrRenderTarget == null || _hdrRenderTarget.Width != safeWidth || _hdrRenderTarget.Height != safeHeight || _lastAntialiasing != mainCamera.Antialiasing))
                {
                    ResizeHDRTarget(safeWidth, safeHeight, mainCamera?.Antialiasing ?? AntialiasingMode.None);
                    _lastAntialiasing = mainCamera?.Antialiasing ?? AntialiasingMode.None;
                }

                // 2. Render to HDR Target

                // Identify Main Camera
                var cameraComp = FindMainCamera();
                if (cameraComp != null && cameraComp.GameObject != null)
                {
                    float aspectRatio = GraphicsDevice.Viewport.AspectRatio;
                    var view = Matrix.CreateLookAt(cameraComp.GameObject.Transform.Position, 
                        cameraComp.GameObject.Transform.Position + cameraComp.GameObject.Transform.Forward, 
                        cameraComp.GameObject.Transform.Up);
                    
                    var projection = Matrix.CreatePerspectiveFieldOfView(
                        MathHelper.ToRadians(cameraComp.FieldOfView), 
                        aspectRatio, 
                        cameraComp.NearClip, 
                        cameraComp.FarClip);

                    // --- Shadow Pass ---
                    Texture2D shadowMap = null;
                    Matrix? lightViewProj = null;

                    var mainLightObj = FindFirstLight(MonoGameEditor.Core.SceneManager.Instance.RootObjects);
                     if (mainLightObj != null)
                    {
                        var lightComp = mainLightObj.GetComponent<MonoGameEditor.Core.Components.LightComponent>();
                        if (lightComp != null && lightComp.CastShadows && _shadowRenderer != null)
                        {
                            // Update resolution if changed
                            _shadowRenderer.UpdateResolution(lightComp.ShadowResolution);
                            
                            // Render Shadows  
                            _shadowRenderer.BeginPass(lightComp, cameraComp.GameObject.Transform.Position, cameraComp.GameObject.Transform.Forward);
                            
                            // Draw all objects to shadow map
                            foreach (var obj in MonoGameEditor.Core.SceneManager.Instance.RootObjects)
                            {
                                RenderShadowsRecursive(obj);
                            }
                            
                            _shadowRenderer.EndPass();
                            
                            shadowMap = _shadowRenderer.ShadowMap;
                            lightViewProj = _shadowRenderer.LightViewProjection;
                        }
                    }

                    // --- Main Pass ---
                    GraphicsDevice.SetRenderTarget(_hdrRenderTarget);
                    GraphicsDevice.Clear(cameraComp.BackgroundColor);

                    // CRITICAL: Reset states BEFORE Skybox (not after)
                    // Skybox saves/restores previous state, so we need clean state first
                    GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                    GraphicsDevice.BlendState = BlendState.Opaque;
                    GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
                    GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

                    // Render Skybox if needed
                    if (cameraComp.ClearFlags == CameraClearFlags.Skybox)
                    {
                        if (_skybox == null && GraphicsDevice != null) 
                        {
                            _skybox = new ProceduralSkybox();
                            _skybox.Initialize(GraphicsDevice);
                        }
                        
                        if (_skybox != null)
                        {
                            // Draw Skybox (it will manage its own depth state internally)
                            _skybox.Draw(view, projection, cameraComp.GameObject.Transform.Position, cameraComp.FarClip);
                        }
                    }

                    if (MonoGameEditor.Core.SceneManager.Instance != null)
                    {
                        RenderModelsRecursive(MonoGameEditor.Core.SceneManager.Instance.RootObjects, GraphicsDevice, view, projection, cameraComp.GameObject.Transform.Position, shadowMap, lightViewProj);
                    }
                    
                    // --- Direct Blit (No Tone Mapping) ---
                    // Using the same method as Scene view to avoid whitewashed look
                    GraphicsDevice.SetRenderTarget(null);
                    GraphicsDevice.Clear(Color.CornflowerBlue);
                    
                    using (var batch = new SpriteBatch(GraphicsDevice))
                    {
                        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                        batch.Draw(_hdrRenderTarget, GraphicsDevice.Viewport.Bounds, Color.White);
                        batch.End();
                    }

                    // --- DEBUG SHADOW MAP (commented out) ---
                    /*
                    if (_shadowRenderer != null && _shadowRenderer.ShadowMap != null)
                    {
                        using (var batch = new SpriteBatch(GraphicsDevice))
                        {
                            batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
                            // Draw mini-map in top-left
                            batch.Draw(_shadowRenderer.ShadowMap, new Microsoft.Xna.Framework.Rectangle(10, 10, 256, 256), Color.White);
                            batch.End();
                        }
                    }
                    */
                    // ------------------------
                }
                else
                {
                    GraphicsDevice.Clear(Color.Black);
                }
                
                GraphicsDevice.Present();
            }
            catch (Exception ex) 
            {
                ConsoleViewModel.Log($"Paint Error: {ex.Message}");
            }
        }

        private AntialiasingMode _lastAntialiasing;

        private void ResizeHDRTarget(int width, int height, AntialiasingMode antialiasingMode)
        {
            _hdrRenderTarget?.Dispose();
            
            if (width <= 0 || height <= 0) return;

            int multiSampleCount = 0;
            switch (antialiasingMode)
            {
                case AntialiasingMode.MSAA_2x: multiSampleCount = 2; break;
                case AntialiasingMode.MSAA_4x: multiSampleCount = 4; break;
                case AntialiasingMode.MSAA_8x: multiSampleCount = 8; break;
            }

            try 
            {
                _hdrRenderTarget = new RenderTarget2D(GraphicsDevice, width, height, false, 
                    SurfaceFormat.HalfVector4, DepthFormat.Depth24, multiSampleCount, RenderTargetUsage.PreserveContents);
            }
            catch(Exception)
            {
                // Fallback
                _hdrRenderTarget = new RenderTarget2D(GraphicsDevice, width, height, false, 
                    SurfaceFormat.Color, DepthFormat.Depth24, multiSampleCount, RenderTargetUsage.PreserveContents);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if(Visible) 
            {
                _renderTimer?.Start();
                Invalidate();
            }
        }

        private MonoGameEditor.Core.Components.CameraComponent? FindMainCamera()
        {
             if (MonoGameEditor.Core.SceneManager.Instance == null) return null;
             return FindMainCameraRecursive(MonoGameEditor.Core.SceneManager.Instance.RootObjects);
        }

        private MonoGameEditor.Core.Components.CameraComponent? FindMainCameraRecursive(System.Collections.ObjectModel.ObservableCollection<GameObject> nodes)
        {
             for(int i=0; i<nodes.Count; i++)
             {
                 var node = nodes[i];
                 var comps = node.Components;
                 for(int j=0; j<comps.Count; j++)
                 {
                     if(comps[j] is CameraComponent cam && cam.IsMainCamera && cam.IsEnabled) return cam;
                 }
                 
                 var found = FindMainCameraRecursive(node.Children);
                 if(found != null) return found;
             }
              return null;
         }

        /// <summary>
        /// Recursively renders all ModelRendererComponents in the scene
        /// </summary>
        private void RenderModelsRecursive(System.Collections.ObjectModel.ObservableCollection<GameObject> nodes, GraphicsDevice device, Matrix view, Matrix projection, Vector3 camPos, Texture2D shadowMap, Matrix? lightViewProj)
        {
            // Use camPos parameter directly (already correct from caller)
            // Previously: Vector3 cameraPosition = Matrix.Invert(view).Translation; ← BUG!

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!node.IsActive) continue;

                // Render ModelRendererComponent if exists
                var modelRenderer = node.GetComponent<ModelRendererComponent>();
                if (modelRenderer != null && modelRenderer.IsEnabled)
                {
                    // SKIP if ShadowsOnly (invisible to main cam)
                    if (modelRenderer.CastShadows == ShadowMode.ShadowsOnly) continue;
                        
                    modelRenderer.Draw(device, view, projection, camPos, shadowMap, lightViewProj);
                }

                // Recurse to children
                RenderModelsRecursive(node.Children, device, view, projection, camPos, shadowMap, lightViewProj);
            }
        }
        

        private void RenderShadowsRecursive(GameObject node)
        {
            if (!node.IsActive) return;

             var modelRenderer = node.GetComponent<ModelRendererComponent>();
             if (modelRenderer != null && modelRenderer.IsEnabled && _shadowRenderer != null)
             {
                 _shadowRenderer.DrawObject(node, modelRenderer);
             }

             if (node.Children != null)
             {
                 foreach(var child in node.Children)
                 {
                     RenderShadowsRecursive(child);
                 }
             }
        }

        private MonoGameEditor.Core.GameObject FindFirstLight(System.Collections.ObjectModel.ObservableCollection<MonoGameEditor.Core.GameObject> nodes)
        {
             foreach(var node in nodes)
             {
                 if (!node.IsActive) continue;

                 var light = node.Components.FirstOrDefault(c => c is MonoGameEditor.Core.Components.LightComponent lc && lc.IsEnabled && lc.LightType == MonoGameEditor.Core.Components.LightType.Directional);
                 if (light != null) return node;
                 
                 var childResult = FindFirstLight(node.Children);
                 if (childResult != null) return childResult;
             }
             return null;
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _renderTimer?.Stop();
                _renderTimer?.Dispose();
                _hdrRenderTarget?.Dispose();
                _toneMapRenderer?.Dispose();
                _skybox?.Dispose();
                _graphicsService?.Release(Handle);
            }
            base.Dispose(disposing);
        }
    }
}