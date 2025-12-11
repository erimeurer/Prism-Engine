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
                // Ensure BackBuffer matches control size
                if (GraphicsDevice.PresentationParameters.BackBufferWidth != Width ||
                    GraphicsDevice.PresentationParameters.BackBufferHeight != Height)
                {
                     if (Visible && Width > 0 && Height > 0)
                     {
                         try {
                            GraphicsDevice.PresentationParameters.BackBufferWidth = Width;
                            GraphicsDevice.PresentationParameters.BackBufferHeight = Height;
                            GraphicsDevice.Reset();
                         } catch (Exception ex) {
                            ConsoleViewModel.Log($"Reset failed: {ex.Message}");
                            return;
                         }
                     }
                     else
                     {
                         return;
                     }
                }
                
                // Set Viewport - Clamp to BackBuffer to prevent crash
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
                // 2. Render
                if (mainCamera != null && mainCamera.GameObject != null)
                {
                    GraphicsDevice.Clear(mainCamera.BackgroundColor);

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
                            // Calculate Matrices
                            var transform = mainCamera.GameObject.Transform;
                            float aspectRatio = GraphicsDevice.Viewport.AspectRatio;
                            
                            Matrix view = Matrix.CreateLookAt(transform.Position, transform.Position + transform.Forward, Vector3.Up);
                            Matrix projection = Matrix.CreatePerspectiveFieldOfView(
                                MathHelper.ToRadians(mainCamera.FieldOfView), 
                                aspectRatio, 
                                mainCamera.NearClip, 
                                mainCamera.FarClip);
    
                            // Draw Skybox (first, with Depth disabled internally)
                            _skybox.Draw(view, projection, transform.Position, mainCamera.FarClip);
                        }
                    }
                }
                else
                {
                    // CornflowerBlue = classic XNA color, proves GameControl is rendering independently
                    GraphicsDevice.Clear(Color.CornflowerBlue);
                }
                
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
