using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WinForms = System.Windows.Forms;
using MonoGameEditor.ViewModels;

namespace MonoGameEditor.Controls
{
    /// <summary>
    /// WinForms Panel that hosts MonoGame rendering with 3D scene
    /// </summary>
    public class MonoGameControl : WinForms.Panel
    {
        private GraphicsDeviceService? _graphicsService;
        private SpriteBatch? _spriteBatch;
        private bool _initialized;
        
        // Editor components
        private EditorCamera? _camera;
        private GridRenderer? _gridRenderer;
        private OrientationGizmo? _orientationGizmo;
        private GizmoRenderer? _gizmoRenderer;
        private MonoGameEditor.Core.Gizmos.TranslationGizmo? _translationGizmo;
        private MonoGameEditor.Core.Gizmos.RotationGizmo? _rotationGizmo;
        private ProceduralSkybox? _skybox;
        private RenderTarget2D? _hdrRenderTarget;
        private ToneMapRenderer? _toneMapRenderer;
        
        // Floating Toolbar
        private WinForms.Panel? _toolPanel;
        private WinForms.Button? _btnMove;
        private WinForms.Button? _btnRotate;

        // Input state
        private bool _isRightMouseDown;
        private bool _isLeftMouseDown;
        private bool _isMiddleMouseDown;
        private System.Drawing.Point _lastMousePosition;
        private Stopwatch _stopwatch = new Stopwatch();
        private TimeSpan _lastFrameTime;
        
        // Render loop timer
        private WinForms.Timer _renderTimer;

        public GraphicsDevice? GraphicsDevice => _graphicsService?.GraphicsDevice;
        public EditorCamera? Camera => _camera;
        public bool ShowGrid { get; set; } = true;
        private bool _showSkybox = true;
        public bool ShowSkybox 
        { 
            get => _showSkybox; 
            set 
            { 
                if (_showSkybox != value)
                {
                    _showSkybox = value;
                    Invalidate();
                }
            } 
        }

        public MonoGameControl()
        {
            SetStyle(
                WinForms.ControlStyles.UserPaint | 
                WinForms.ControlStyles.AllPaintingInWmPaint | 
                WinForms.ControlStyles.Opaque |
                WinForms.ControlStyles.Selectable,
                true);
            
            AllowDrop = true; // Enable Drag & Drop
            
            // Use Timer for render loop (works better in WPF host)
            _renderTimer = new WinForms.Timer();
            _renderTimer.Interval = 8; // ~120 FPS
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

            _graphicsService = GraphicsDeviceService.AddRef(Handle, Width, Height);
            _spriteBatch = new SpriteBatch(GraphicsDevice!);
            
            _camera = new EditorCamera();
            _camera.UpdateAspectRatio(Width, Height);
            
            _gridRenderer = new GridRenderer();
            _gridRenderer.Initialize(GraphicsDevice!);
            
            _orientationGizmo = new OrientationGizmo();
            _orientationGizmo.Initialize(GraphicsDevice!);
            
            _gizmoRenderer = new GizmoRenderer();
            _gizmoRenderer.Initialize(GraphicsDevice!);

            _translationGizmo = new MonoGameEditor.Core.Gizmos.TranslationGizmo(GraphicsDevice!);
            _rotationGizmo = new MonoGameEditor.Core.Gizmos.RotationGizmo(GraphicsDevice!);

            _skybox = new ProceduralSkybox();
            _skybox.Initialize(GraphicsDevice!);

            _toneMapRenderer = new ToneMapRenderer(GraphicsDevice!);
            ResizeHDRTarget(Width, Height);

            InitializeToolbar();
            if (MainViewModel.Instance != null)
            {
                MainViewModel.Instance.PropertyChanged += OnViewModelPropertyChanged;
                MainViewModel.Instance.FocusRequested += (s, e) => FocusSelection(); // Subscribe to focus requests
                ShowSkybox = MainViewModel.Instance.IsSkyboxVisible;
                UpdateToolbarState();
            }
            
            _initialized = true;
            _stopwatch.Start();
            
            _renderTimer.Start();
        }

        private void InitializeToolbar()
        {
            _toolPanel = new WinForms.Panel
            {
                Location = new System.Drawing.Point(10, 10),
                Size = new System.Drawing.Size(70, 34), // 2 buttons * 32 + margin
                BackColor = System.Drawing.Color.FromArgb(45, 45, 48),
                BorderStyle = WinForms.BorderStyle.None
            };

            _btnMove = CreateToolbarButton("✥", 0, TransformTool.Move);
            _btnRotate = CreateToolbarButton("↻", 34, TransformTool.Rotate); 

            _toolPanel.Controls.Add(_btnMove);
            _toolPanel.Controls.Add(_btnRotate);
            
            this.Controls.Add(_toolPanel);
        }

        private WinForms.Button CreateToolbarButton(string text, int x, object? tag)
        {
            var btn = new WinForms.Button
            {
                Text = text,
                Location = new System.Drawing.Point(x, 1),
                Size = new System.Drawing.Size(32, 32),
                FlatStyle = WinForms.FlatStyle.Flat,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 12f),
                Cursor = WinForms.Cursors.Hand,
                Tag = tag
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(62, 62, 66);
            if (tag is TransformTool) 
                btn.Click += OnToolbarButtonClick;
                
            return btn;
        }

        private void OnToolbarButtonClick(object? sender, EventArgs e)
        {
            if (sender is WinForms.Button btn && btn.Tag is TransformTool tool)
            {
                if (MainViewModel.Instance != null)
                {
                    MainViewModel.Instance.ActiveTool = tool;
                }
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.ActiveTool))
            {
                if (IsHandleCreated) Invoke(new Action(UpdateToolbarState));
            }
            else if (e.PropertyName == nameof(MainViewModel.IsSkyboxVisible))
            {
                 if (MainViewModel.Instance != null && IsHandleCreated)
                 {
                     ShowSkybox = MainViewModel.Instance.IsSkyboxVisible;
                 }
            }
        }

        private void UpdateToolbarState()
        {
            if (MainViewModel.Instance == null || _btnMove == null || _btnRotate == null) return;
            
            var active = MainViewModel.Instance.ActiveTool;
            
            _btnMove.BackColor = active == TransformTool.Move ? System.Drawing.Color.FromArgb(0, 122, 204) : System.Drawing.Color.Transparent;
            _btnRotate.BackColor = active == TransformTool.Rotate ? System.Drawing.Color.FromArgb(0, 122, 204) : System.Drawing.Color.Transparent;
        }

        public void SetRenderEnabled(bool enabled)
        {
            // Placeholder
        }

        private void RenderTimer_Tick(object? sender, EventArgs e)
        {
            if (!_initialized || !Visible) return;
            
            var currentTime = _stopwatch.Elapsed;
            float deltaTime = (float)(currentTime - _lastFrameTime).TotalSeconds;
            _lastFrameTime = currentTime;
            deltaTime = Math.Min(deltaTime, 0.1f);
            
            ProcessKeyboardInput(deltaTime);
            
            Invalidate(); // Triggers OnPaint
        }

        #region Mouse Input
        
        protected override void OnMouseDown(WinForms.MouseEventArgs e)
        {
            base.OnMouseDown(e);
            
            if (e.Button == WinForms.MouseButtons.Right)
            {
                _isRightMouseDown = true;
                _lastMousePosition = e.Location;
                Capture = true;
                Focus();
            }
            if (e.Button == WinForms.MouseButtons.Middle)
            {
                _isMiddleMouseDown = true;
                _lastMousePosition = e.Location;
                Capture = true;
                Focus();
            }
            if (e.Button == WinForms.MouseButtons.Left)
            {
                _isLeftMouseDown = true;
                UpdateGizmo(e.Location);
                if (_translationGizmo != null) Focus();
            }
        }

        protected override void OnMouseUp(WinForms.MouseEventArgs e)
        {
            base.OnMouseUp(e);
            
            if (e.Button == WinForms.MouseButtons.Right)
            {
                _isRightMouseDown = false;
                if (!_isMiddleMouseDown) Capture = false;
            }
            if (e.Button == WinForms.MouseButtons.Middle)
            {
                _isMiddleMouseDown = false;
                if (!_isRightMouseDown) Capture = false;
            }
            if (e.Button == WinForms.MouseButtons.Left)
            {
                _isLeftMouseDown = false;
                UpdateGizmo(e.Location);
            }
        }

        protected override void OnMouseMove(WinForms.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            
            if (_isRightMouseDown && _camera != null)
            {
                int deltaX = e.X - _lastMousePosition.X;
                int deltaY = e.Y - _lastMousePosition.Y;
                
                if (deltaX != 0 || deltaY != 0)
                {
                    _camera.Rotate(deltaX, deltaY);
                    _lastMousePosition = e.Location;
                }
            }
            
            if (_isMiddleMouseDown && _camera != null)
            {
                int deltaX = e.X - _lastMousePosition.X;
                int deltaY = e.Y - _lastMousePosition.Y;
                
                if (deltaX != 0 || deltaY != 0)
                {
                    _camera.Pan(deltaX, deltaY);
                    _lastMousePosition = e.Location;
                }
            }
            
            UpdateGizmo(e.Location);
        }
        
        public void FocusSelection(float distance = 5f)
        {
            var selected = MainViewModel.Instance?.Inspector?.SelectedObject as MonoGameEditor.Core.GameObject;
            if (selected != null && _camera != null)
            {
                _camera.Focus(selected.Transform.Position, distance);
            }
        }

        protected override void OnMouseWheel(WinForms.MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_camera != null)
            {
                _camera.Zoom(e.Delta);
                Invalidate();
            }
        }

        #endregion

        #region Keyboard Input

        protected override bool IsInputKey(WinForms.Keys keyData)
        {
            switch (keyData)
            {
                case WinForms.Keys.W:
                case WinForms.Keys.A:
                case WinForms.Keys.S:
                case WinForms.Keys.D:
                case WinForms.Keys.E:
                case WinForms.Keys.Q:
                case WinForms.Keys.W | WinForms.Keys.Shift:
                case WinForms.Keys.A | WinForms.Keys.Shift:
                case WinForms.Keys.S | WinForms.Keys.Shift:
                case WinForms.Keys.D | WinForms.Keys.Shift:
                case WinForms.Keys.E | WinForms.Keys.Shift:
                case WinForms.Keys.Q | WinForms.Keys.Shift:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        private void ProcessKeyboardInput(float deltaTime)
        {
            if (!_isRightMouseDown || _camera == null) return;
            
            float forward = 0, right = 0, up = 0;
            
            if (IsKeyDown(WinForms.Keys.W)) forward += 1;
            if (IsKeyDown(WinForms.Keys.S)) forward -= 1;
            if (IsKeyDown(WinForms.Keys.D)) right += 1;
            if (IsKeyDown(WinForms.Keys.A)) right -= 1;
            if (IsKeyDown(WinForms.Keys.E)) up += 1;
            if (IsKeyDown(WinForms.Keys.Q)) up -= 1;
            
            float speed = IsKeyDown(WinForms.Keys.ShiftKey) ? 2f : 1f;
            
            _camera.Move(forward * speed, right * speed, up * speed, deltaTime);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private bool IsKeyDown(WinForms.Keys key)
        {
            return (GetAsyncKeyState((int)key) & 0x8000) != 0;
        }

        #endregion

        private void UpdateGizmo(System.Drawing.Point mousePos)
        {
            var vm = MainViewModel.Instance;
            if (vm == null || _camera == null || _translationGizmo == null) return;

            if (vm.ActiveTool == TransformTool.Move)
            {
                var selectedObj = vm.Inspector.SelectedObject as MonoGameEditor.Core.GameObject;
                if (selectedObj != null)
                {
                    Vector2 mouseVec = new Vector2(mousePos.X, mousePos.Y);
                    Viewport vp = new Viewport(0, 0, Width, Height);
                    
                    Ray ray = _camera.GetRay(mouseVec, vp);
                    _translationGizmo.Update(ray, _isLeftMouseDown, selectedObj.Transform);
                }
            }
            else if (vm.ActiveTool == TransformTool.Rotate)
            {
                var selectedObj = vm.Inspector.SelectedObject as MonoGameEditor.Core.GameObject;
                if (selectedObj != null)
                {
                    Vector2 mouseVec = new Vector2(mousePos.X, mousePos.Y);
                    Viewport vp = new Viewport(0, 0, Width, Height);
                    
                    Ray ray = _camera.GetRay(mouseVec, vp);
                    _rotationGizmo?.Update(ray, _isLeftMouseDown, selectedObj.Transform);
                }
            }
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
                            GraphicsDevice.PresentationParameters.BackBufferWidth = Width;
                            GraphicsDevice.PresentationParameters.BackBufferHeight = Height;
                            GraphicsDevice.Reset();
                            _camera?.UpdateAspectRatio(Width, Height);
                            ConsoleViewModel.Log($"[MonoGameControl] Device Reset to {Width}x{Height}");
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


                // --- Shadow Pass (DISABLED - Causing dark rendering) ---
                // TODO: Re-enable after shaders are compiled
                /*
                bool shadowsEnabled = false;
                Texture2D shadowMap = null;
                Matrix? lightViewProj = null;
                
                MonoGameEditor.Core.GameObject mainLightObj = null;
                if (Core.SceneManager.Instance != null && Core.SceneManager.Instance.RootObjects.Any())
                {
                    mainLightObj = FindFirstLight(Core.SceneManager.Instance.RootObjects);
                }
                
                if (mainLightObj != null)
                {
                    var lightComp = mainLightObj.GetComponent<MonoGameEditor.Core.Components.LightComponent>();
                    if (lightComp != null && lightComp.CastShadows && _shadowRenderer != null)
                    {
                        _shadowRenderer.UpdateMatrix(mainLightObj.Transform.Forward, _camera.Position);
                        _shadowRenderer.BeginPass();
                        
                        if (Core.SceneManager.Instance != null)
                        {
                            foreach (var obj in Core.SceneManager.Instance.RootObjects)
                            {
                                RenderShadowsRecursive(obj);
                            }
                        }
                        
                        shadowsEnabled = true;
                        shadowMap = _shadowRenderer.ShadowMap;
                        lightViewProj = _shadowRenderer.LightViewProjection;
                    }
                }
                */
                
                Texture2D shadowMap = null;
                Matrix? lightViewProj = null;



            // --- Main Pass (HDR) ---
            GraphicsDevice.SetRenderTarget(_hdrRenderTarget);
            GraphicsDevice.Clear(Color.CornflowerBlue); // Scene background
            
            // Draw Skybox first
            if (ShowSkybox && _skybox != null)
            {
                _skybox.Draw(_camera.View, _camera.Projection, _camera.Position, _camera.FarPlane);
            }
            
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            // Draw Scene
            if (Core.SceneManager.Instance != null)
            {
                foreach (var obj in Core.SceneManager.Instance.RootObjects)
                {
                    RenderRecursively(obj);
                }
            }
                // Ensure HDR Target is valid
                if (_hdrRenderTarget == null || _hdrRenderTarget.Width != safeWidth || _hdrRenderTarget.Height != safeHeight)
                {
                    ResizeHDRTarget(safeWidth, safeHeight);
                }

                // Grid Drawing
                if (ShowGrid && _camera != null && _gridRenderer != null)
                {
                    _camera.UpdateAspectRatio(Width, Height);
                    _gridRenderer.Draw(GraphicsDevice, _camera);
                }
                
                // Models are rendered in HDR pass above.
                // Gizmos are rendered here on top of ToneMapped output.
                
                // THEN render gizmos on top (disable depth test for on-top rendering)
                var previousDepthState = GraphicsDevice.DepthStencilState;
                GraphicsDevice.DepthStencilState = DepthStencilState.None; // Disable depth test
                
                if (_camera != null && _gizmoRenderer != null)
                {
                    _gizmoRenderer.Draw(GraphicsDevice, _camera);
                }
                
                if (_camera != null && _orientationGizmo != null)
                {
                    _orientationGizmo.Draw(GraphicsDevice, _camera);
                }

                var vm = MainViewModel.Instance;
                if (vm != null && _camera != null)
                {
                     var selectedObj = vm.Inspector.SelectedObject as MonoGameEditor.Core.GameObject;
                     if (selectedObj != null)
                     {
                         bool useLocal = vm.GizmoOrientationMode == ViewModels.GizmoOrientationMode.Local;
                         
                         float yaw = MathHelper.ToRadians(selectedObj.Transform.Rotation.Y);
                         float pitch = MathHelper.ToRadians(selectedObj.Transform.Rotation.X);
                         float roll = MathHelper.ToRadians(selectedObj.Transform.Rotation.Z);
                         Quaternion objRotation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
                         
                         if (vm.ActiveTool == TransformTool.Move)
                             _translationGizmo?.Draw(_camera, selectedObj.Transform.Position, objRotation, useLocal);
                         else if (vm.ActiveTool == TransformTool.Rotate)
                             _rotationGizmo?.Draw(_camera, selectedObj.Transform.Position);
                     }
                }
                
                // Restore depth state
                GraphicsDevice.DepthStencilState = previousDepthState;
                
                // 2. Tone Mapping Pass (HDR -> Backbuffer)
                GraphicsDevice.SetRenderTarget(null); // Back to screen
                // GraphicsDevice.Clear is usually not needed as we draw full screen quad, but good practice if letterboxing
                
                if (_toneMapRenderer != null && _hdrRenderTarget != null)
                {
                    _toneMapRenderer.Draw(_hdrRenderTarget);
                }
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


                // Implicit Present
                GraphicsDevice.Present();
            }
            catch (Exception ex)
            {
                ConsoleViewModel.Log($"Render error: {ex.Message}");
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

        private void RenderRecursively(MonoGameEditor.Core.GameObject node)
        {
            var modelRenderer = node.Components.FirstOrDefault(c => c is MonoGameEditor.Core.Components.ModelRendererComponent) as MonoGameEditor.Core.Components.ModelRendererComponent;
            modelRenderer?.Draw(GraphicsDevice, _camera.View, _camera.Projection, _camera.Position);

            foreach (var child in node.Children)
            {
                RenderRecursively(child);
            }
        }

        protected override void OnDragEnter(WinForms.DragEventArgs drgevent)
        {
            base.OnDragEnter(drgevent);
            if (drgevent.Data.GetDataPresent(WinForms.DataFormats.FileDrop))
                drgevent.Effect = WinForms.DragDropEffects.Copy;
            else
                drgevent.Effect = WinForms.DragDropEffects.None;
        }

        protected override async void OnDragDrop(WinForms.DragEventArgs drgevent)
        {
            base.OnDragDrop(drgevent);
            string[] files = (string[])drgevent.Data.GetData(WinForms.DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                string file = files[0];
                string ext = System.IO.Path.GetExtension(file).ToLower();
                if (ext == ".obj" || ext == ".fbx" || ext == ".gltf" || ext == ".blend")
                {
                    ConsoleViewModel.Log($"[MonoGameControl] DragDrop received file: {file}");
                    
                    // Load model data with mesh hierarchy
                    var modelData = await MonoGameEditor.Core.Assets.ModelImporter.LoadModelDataAsync(file);
                    
                    if (modelData == null || modelData.Meshes.Count == 0)
                    {
                        ConsoleViewModel.Log($"[MonoGameControl] Failed to load model: {file} (Data is null or empty)");
                        return;
                    }

                    // Create root GameObject
                    var rootGO = new MonoGameEditor.Core.GameObject 
                    { 
                        Name = modelData.Name 
                    };
                    
                    // Position in front of camera
                    if (_camera != null)
                    {
                        var targetPos = _camera.Position + _camera.Forward * 5f;
                        rootGO.Transform.Position = targetPos;
                    }

                    // Create child GameObject for each mesh
                    foreach (var meshData in modelData.Meshes)
                    {
                        var childGO = new MonoGameEditor.Core.GameObject
                        {
                            Name = meshData.Name
                        };

                        // Add ModelRendererComponent with mesh-specific data
                        var renderer = new MonoGameEditor.Core.Components.ModelRendererComponent();
                        renderer.SetMeshData(meshData, file); // Pass original file path
                        childGO.AddComponent(renderer);

                        // Add as child
                        rootGO.AddChild(childGO);
                    }

                    Core.SceneManager.Instance.RootObjects.Add(rootGO);
                    
                    ConsoleViewModel.Log($"[MonoGameControl] Successfully created '{rootGO.Name}' with {modelData.Meshes.Count} mesh(es)");
                    
                    if (MainViewModel.Instance != null)
                        MainViewModel.Instance.Inspector.SelectedObject = rootGO;
                }
                else
                {
                    ConsoleViewModel.Log($"[MonoGameControl] Ignored dropped file with extension: {ext}");
                }
            }
        }

        private void ResizeHDRTarget(int width, int height)
        {
            _hdrRenderTarget?.Dispose();
            
            if (width <= 0 || height <= 0) return;

            try 
            {
                 // Use HalfVector4 for HDR precision (16-bit float per channel)
                 // Preserves values > 1.0 for Tone Mapping
                _hdrRenderTarget = new RenderTarget2D(GraphicsDevice, width, height, false, 
                    SurfaceFormat.HalfVector4, DepthFormat.Depth24);
                
                ConsoleViewModel.Log($"[MonoGameControl] Created HDR Target {width}x{height} (HalfVector4)");
            }
            catch(Exception ex)
            {
                ConsoleViewModel.Log($"[MonoGameControl] Failed to create HDR target: {ex.Message}. Fallback to Color.");
                // Fallback to standard Color if HalfVector4 not supported
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
                _camera?.UpdateAspectRatio(Width, Height);
                Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (MainViewModel.Instance != null)
                    {
                        MainViewModel.Instance.PropertyChanged -= OnViewModelPropertyChanged;
                    }
                    _btnMove?.Dispose();
                    _btnRotate?.Dispose();
                    _toolPanel?.Dispose();

                    _renderTimer?.Stop();
                    _renderTimer?.Dispose();
                    _orientationGizmo?.Dispose();
                    _skybox?.Dispose();
                    _gridRenderer?.Dispose();
                    _gridRenderer?.Dispose();
                    _hdrRenderTarget?.Dispose();
                    _toneMapRenderer?.Dispose();
                    _spriteBatch?.Dispose();
                    _graphicsService?.Release(Handle);
                }
                base.Dispose(disposing);
            }
            catch { }
        }
    }
}
