using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WinForms = System.Windows.Forms;
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;
using MonoGameEditor.ViewModels;
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
        private bool _initialized;
        private WinForms.Timer _renderTimer;
        private ProceduralSkybox? _skybox;
        private RenderTarget2D? _hdrRenderTarget;
        private ToneMapRenderer? _toneMapRenderer;

        public GraphicsDevice? GraphicsDevice => _graphicsService?.GraphicsDevice;

        public GameControl()
        {
            SetStyle(
                WinForms.ControlStyles.UserPaint | 
                WinForms.ControlStyles.AllPaintingInWmPaint | 
                WinForms.ControlStyles.Opaque,
                true);
            
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
            
            _toneMapRenderer = new ToneMapRenderer(GraphicsDevice!);
            ResizeHDRTarget(Width, Height);

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
            
            // Check Play Mode and Run Game Loop
            var vm = ViewModels.MainViewModel.Instance;
            if (vm != null && vm.IsPlaying && !vm.IsPaused)
            {
                UpdateGame();
            }

            // Always render
            Invalidate();
        }

        private void UpdateGame()
        {
            // Placeholder: This is where scripts/physics would update
            // For now we just acknowledge the loop exists.
            // ConsoleViewModel.Log("Game Updating..."); // Throttled log logic would be needed
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
                        if (comps[j] is CameraComponent cam && cam.IsMainCamera)
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
                if (_hdrRenderTarget == null || _hdrRenderTarget.Width != safeWidth || _hdrRenderTarget.Height != safeHeight)
                {
                    ResizeHDRTarget(safeWidth, safeHeight);
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

                    // --- Shadow Pass (DISABLED - Causing dark rendering) ---
                    // TODO: Re-enable after shaders are compiled
                    /*
                    var mainLightObj = FindFirstLight(MonoGameEditor.Core.SceneManager.Instance.RootObjects);
                     if (mainLightObj != null)
                    {
                        var lightComp = mainLightObj.GetComponent<MonoGameEditor.Core.Components.LightComponent>();
                        if (lightComp != null && lightComp.CastShadows && _shadowRenderer != null)
                        {
                            _shadowRenderer.UpdateMatrix(mainLightObj.Transform.Forward, cameraComp.GameObject.Transform.Position);
                            _shadowRenderer.BeginPass();
                            foreach (var obj in MonoGameEditor.Core.SceneManager.Instance.RootObjects)
                            {
                                RenderShadowsRecursive(obj);
                            }
                            _shadowRenderer.EndPass();
                            shadowMap = _shadowRenderer.ShadowMap;
                            lightViewProj = _shadowRenderer.LightViewProjection;
                        }
                    }
                    */
                    
                    Texture2D? shadowMap = null;
                    Matrix? lightViewProj = null;

                    // --- Main Pass ---
                    GraphicsDevice.SetRenderTarget(_hdrRenderTarget);
                    GraphicsDevice.Clear(cameraComp.BackgroundColor);

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
                            // Draw Skybox (first, with Depth disabled internally)
                            _skybox.Draw(view, projection, cameraComp.GameObject.Transform.Position, cameraComp.FarClip);
                        }
                    }

                    // Reset states
                    GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                    GraphicsDevice.BlendState = BlendState.Opaque;
                    GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise; // Cull Back faces
                    GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

                    if (MonoGameEditor.Core.SceneManager.Instance != null)
                    {
                        RenderModelsRecursive(MonoGameEditor.Core.SceneManager.Instance.RootObjects, GraphicsDevice, view, projection, cameraComp.GameObject.Transform.Position);
                    }
                    
                    // --- Tone Map ---
                    GraphicsDevice.SetRenderTarget(null);
                    if (_toneMapRenderer != null && _hdrRenderTarget != null)
                        _toneMapRenderer.Draw(_hdrRenderTarget);
                    else
                    {
                         // Fallback
                         using (var batch = new SpriteBatch(GraphicsDevice))
                         {
                             batch.Begin();
                             batch.Draw(_hdrRenderTarget, GraphicsDevice.Viewport.Bounds, Color.White);
                             batch.End();
                         }
                    }
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

        private void ResizeHDRTarget(int width, int height)
        {
            _hdrRenderTarget?.Dispose();
            
            if (width <= 0 || height <= 0) return;

            try 
            {
                _hdrRenderTarget = new RenderTarget2D(GraphicsDevice, width, height, false, 
                    SurfaceFormat.HalfVector4, DepthFormat.Depth24);
            }
            catch(Exception)
            {
                // Fallback
                _hdrRenderTarget = new RenderTarget2D(GraphicsDevice, width, height, false, 
                    SurfaceFormat.Color, DepthFormat.Depth24);
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
                     if(comps[j] is CameraComponent cam && cam.IsMainCamera) return cam;
                 }
                 
                 var found = FindMainCameraRecursive(node.Children);
                 if(found != null) return found;
             }
              return null;
         }

        /// <summary>
        /// Recursively renders all ModelRendererComponents in the scene
        /// </summary>
        private void RenderModelsRecursive(System.Collections.ObjectModel.ObservableCollection<GameObject> nodes, GraphicsDevice device, Matrix view, Matrix projection, Vector3 camPos)
        {
            Vector3 cameraPosition = Matrix.Invert(view).Translation;

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!node.IsActive) continue;

                // Render ModelRendererComponent if exists
                var modelRenderer = node.GetComponent<ModelRendererComponent>();
                if (modelRenderer != null)
                {
                    // Debug log (throttled)
                    if (DateTime.Now.Millisecond < 5)
                        ConsoleViewModel.Log($"[GameView] Drawing {node.Name}");
                        
                    modelRenderer.Draw(device, view, projection, cameraPosition);
                }

                // Recurse to children
                RenderModelsRecursive(node.Children, device, view, projection, cameraPosition);
            }
        }
        

        private MonoGameEditor.Core.GameObject FindFirstLight(System.Collections.ObjectModel.ObservableCollection<MonoGameEditor.Core.GameObject> nodes)
        {
             foreach(var node in nodes)
             {
                 var light = node.Components.FirstOrDefault(c => c is MonoGameEditor.Core.Components.LightComponent && ((MonoGameEditor.Core.Components.LightComponent)c).LightType == MonoGameEditor.Core.Components.LightType.Directional);
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
