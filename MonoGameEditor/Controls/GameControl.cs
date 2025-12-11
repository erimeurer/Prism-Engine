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
    
                // 2. Render
                if (mainCamera != null && mainCamera.GameObject != null)
                {
                    GraphicsDevice.Clear(mainCamera.BackgroundColor);

                    // Calculate Matrices once
                    var transform = mainCamera.GameObject.Transform;
                    float aspectRatio = GraphicsDevice.Viewport.AspectRatio;

                    // Update Viewport to match this control
                    GraphicsDevice.Viewport = new Viewport(0, 0, Width, Height);

                    // Debug log every 60 frames
                    if (DateTime.Now.Millisecond < 20) 
                    {
                        ConsoleViewModel.Log($"[GameView] CamPos: {transform.Position} Dir: {transform.Forward} Viewport: {Width}x{Height}");
                    }
                    
                    Matrix view = Matrix.CreateLookAt(transform.Position, transform.Position + transform.Forward, Vector3.Up);
                    Matrix projection = Matrix.CreatePerspectiveFieldOfView(
                        MathHelper.ToRadians(mainCamera.FieldOfView), 
                        aspectRatio, 
                        mainCamera.NearClip, 
                        mainCamera.FarClip);

                    // Render Skybox if needed
                    if (mainCamera.ClearFlags == CameraClearFlags.Skybox)
                    {
                        if (_skybox == null && GraphicsDevice != null) 
                        {
                            _skybox = new ProceduralSkybox();
                            _skybox.Initialize(GraphicsDevice);
                        }
                        
                        if (_skybox != null)
                        {
                            // Draw Skybox (first, with Depth disabled internally)
                            _skybox.Draw(view, projection, transform.Position, mainCamera.FarClip);
                        }
                    }

                    // Reset Render States for 3D Models
                    GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                    GraphicsDevice.BlendState = BlendState.Opaque;
                    GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

                    // Render 3D Models
                    RenderModelsRecursive(scene.RootObjects, GraphicsDevice, view, projection);
                }
                else
                {
                    // CornflowerBlue = classic XNA color, proves GameControl is rendering independently
                    GraphicsDevice.Clear(Color.CornflowerBlue);
                }
                
                // Implicit Present
                GraphicsDevice.Present();
            }
            catch (Exception ex)
            {
                ConsoleViewModel.Log($"Paint Error: {ex.Message}");
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

        private CameraComponent? FindMainCameraRecursive(System.Collections.ObjectModel.ObservableCollection<GameObject> nodes)
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
        private void RenderModelsRecursive(System.Collections.ObjectModel.ObservableCollection<GameObject> nodes, GraphicsDevice device, Matrix view, Matrix projection)
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
                RenderModelsRecursive(node.Children, device, view, projection);
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _renderTimer?.Stop();
                _renderTimer?.Dispose();
                _graphicsService?.Release(Handle);
            }
            base.Dispose(disposing);
        }
    }
}
