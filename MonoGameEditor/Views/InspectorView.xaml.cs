#nullable disable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Input;
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;

namespace MonoGameEditor.Views
{
    public partial class InspectorView : UserControl, INotifyPropertyChanged
    {
        private System.Windows.Point _dragStartPoint;
        public event PropertyChangedEventHandler? PropertyChanged;

        // Draggable label state
        private bool _isDraggingLabel = false;
        private System.Windows.Point _labelDragStartPoint;
        private double _initialLabelValue;
        private TextBox _associatedTextBox;
        private double _accumulatedDelta = 0;

        private ObservableCollection<string> _availableMaterials = new();
        public ObservableCollection<string> AvailableMaterials
        {
            get => _availableMaterials;
            set
            {
                _availableMaterials = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailableMaterials)));
            }
        }

        public InspectorView()
        {
            InitializeComponent();
            // DON'T set DataContext here - it breaks existing bindings from GameObject/Components
            
            // Initialize material discovery
            RefreshMaterialList();
            
            // Listen to project load events to refresh material list
            Core.ProjectManager.Instance.ProjectLoaded += () => RefreshMaterialList();
        }

        private void RefreshMaterialList()
        {
            Core.Assets.MaterialAssetManager.Instance.RefreshMaterialCache();
            var materials = Core.Assets.MaterialAssetManager.Instance.GetAvailableMaterials();
            
            AvailableMaterials.Clear();
            foreach (var mat in materials)
            {
                AvailableMaterials.Add(mat);
            }
            
            // Always add default material
            var defaultMat = Core.Assets.MaterialAssetManager.Instance.GetDefaultMaterialPath();
            if (!AvailableMaterials.Contains(defaultMat))
            {
                AvailableMaterials.Insert(0, defaultMat);
            }
        }

        private void AddComponent_Click(object sender, RoutedEventArgs e)
        {
            var selectedObject = SceneManager.Instance.SelectedObject;
            if (selectedObject == null) return;

            // Show context menu with component options
            var menu = new ContextMenu();
            
            var cameraItem = new MenuItem { Header = "Camera" };
            cameraItem.Click += (s, args) => {
                if (selectedObject.GetComponent<CameraComponent>() == null)
                {
                    selectedObject.AddComponent(new CameraComponent());
                    selectedObject.ObjectType = GameObjectType.Camera;
                }
            };
            
            var lightItem = new MenuItem { Header = "Light" };
            lightItem.Click += (s, args) => {
                if (selectedObject.GetComponent<LightComponent>() == null)
                {
                    selectedObject.AddComponent(new LightComponent());
                    selectedObject.ObjectType = GameObjectType.Light;
                }
            };
            
            menu.Items.Add(cameraItem);
            menu.Items.Add(lightItem);

            // Add Physics submenu
            var physicsItem = new MenuItem { Header = "Physics" };
            
            var boxColliderItem = new MenuItem { Header = "Box Collider" };
            boxColliderItem.Click += (s, args) => selectedObject.AddComponent(new BoxColliderComponent());
            
            var sphereColliderItem = new MenuItem { Header = "Sphere Collider" };
            sphereColliderItem.Click += (s, args) => selectedObject.AddComponent(new SphereColliderComponent());
            
            var capsuleColliderItem = new MenuItem { Header = "Capsule Collider" };
            capsuleColliderItem.Click += (s, args) => selectedObject.AddComponent(new CapsuleColliderComponent());
            
            var physicsBodyItem = new MenuItem { Header = "Physics Body" };
            physicsBodyItem.Click += (s, args) => selectedObject.AddComponent(new PhysicsBodyComponent());
            
            physicsItem.Items.Add(physicsBodyItem);
            physicsItem.Items.Add(new Separator());
            physicsItem.Items.Add(boxColliderItem);
            physicsItem.Items.Add(sphereColliderItem);
            physicsItem.Items.Add(capsuleColliderItem);
            menu.Items.Add(physicsItem);
            
            // Add Scripts submenu
            var scriptAssets = ScriptManager.Instance.ScriptAssets;
            if (scriptAssets.Count > 0)
            {
                menu.Items.Add(new Separator());
                var scriptsItem = new MenuItem { Header = "Script" };
                
                foreach (var scriptAsset in scriptAssets.Values)
                {
                    var scriptMenuItem = new MenuItem { Header = scriptAsset.Name };
                    var scriptName = scriptAsset.Name; // Capture for closure
                    scriptMenuItem.Click += (s, args) => {
                        var script = ScriptManager.Instance.CreateScriptInstance(scriptName);
                        if (script != null)
                        {
                            selectedObject.AddComponent(script);
                        }
                    };
                    scriptsItem.Items.Add(scriptMenuItem);
                }
                
                menu.Items.Add(scriptsItem);
            }
            
            menu.IsOpen = true;
        }

        private void RemoveComponent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Core.Component component)
            {
                var selectedObject = SceneManager.Instance.SelectedObject;
                if (selectedObject != null && component.CanRemove)
                {
                    selectedObject.RemoveComponent(component);
                    
                    // Reset object type if removing camera/light
                    if (component is CameraComponent)
                        selectedObject.ObjectType = GameObjectType.Default;
                    else if (component is LightComponent)
                        selectedObject.ObjectType = GameObjectType.Default;
                }
            }
        }

        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CameraComponent camera)
            {
                using (var dialog = new System.Windows.Forms.ColorDialog())
                {
                    dialog.Color = System.Drawing.Color.FromArgb(camera.BackgroundColor.A, camera.BackgroundColor.R, camera.BackgroundColor.G, camera.BackgroundColor.B);
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        camera.BackgroundColor = new Microsoft.Xna.Framework.Color(dialog.Color.R, dialog.Color.G, dialog.Color.B, dialog.Color.A);
                    }
                }
            }
        }

        private void ComponentHeader_DragSource_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }



        #region Draggable Labels
        private void NumericLabel_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement label && label.Tag is TextBox tb)
            {
                _associatedTextBox = tb;
                // Try parsing current value, if fail use 0
                if (!double.TryParse(tb.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out _initialLabelValue))
                {
                    if (!double.TryParse(tb.Text, out _initialLabelValue))
                        _initialLabelValue = 0;
                }

                _isDraggingLabel = true;
                _labelDragStartPoint = e.GetPosition(this);
                _accumulatedDelta = 0;
                label.CaptureMouse();
                Mouse.OverrideCursor = Cursors.SizeWE;
                e.Handled = true;
            }
        }

        private void NumericLabel_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDraggingLabel && _associatedTextBox != null)
            {
                var currentPoint = e.GetPosition(this);
                double delta = currentPoint.X - _labelDragStartPoint.X;
                
                // Sensitivity
                double multiplier = 0.05;
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) multiplier = 0.5;
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) multiplier = 0.005;
                
                double newValue = _initialLabelValue + (delta * multiplier);
                
                // Update the textbox text
                _associatedTextBox.Text = newValue.ToString("F2", CultureInfo.InvariantCulture);
                
                // Force binding update
                var binding = BindingOperations.GetBindingExpression(_associatedTextBox, TextBox.TextProperty);
                binding?.UpdateSource();

                // Special case for script fields that might not use bindings
                // (Handled by the LostFocus/TextChanged logic if we add it, but for now bindings cover most)
                
                e.Handled = true;
            }
        }

        private void NumericLabel_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isDraggingLabel)
            {
                _isDraggingLabel = false;
                if (sender is FrameworkElement label) label.ReleaseMouseCapture();
                Mouse.OverrideCursor = null;
                _associatedTextBox = null;
                e.Handled = true;
            }
        }
        #endregion

        private void ComponentHeader_DragSource_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                System.Windows.Point currentPosition = e.GetPosition(null);
                if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is FrameworkElement element && element.DataContext is Core.Component component)
                    {
                        var data = new System.Windows.DataObject();
                        data.SetData("Component", component);
                        // Also include GameObject for backwards compatibility or if the field specifically wants the GO
                        if (component.GameObject != null)
                            data.SetData("GameObject", component.GameObject);

                        System.Windows.DragDrop.DoDragDrop(element, data, System.Windows.DragDropEffects.Link);
                    }
                }
            }
        }

        private void MaterialEditor_Loaded(object sender, RoutedEventArgs e)
        {
            // When Material Editor UI is loaded, ensure shader is loaded (GraphicsDevice may be ready now)
            if (sender is FrameworkElement element && element.DataContext is ViewModels.MaterialEditorViewModel materialEditor)
            {
                materialEditor.EnsureShaderLoaded();
            }
        }

        private void Material_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    if (System.IO.Path.GetExtension(filePath).ToLower() == ".mat")
                    {
                        // Get the component from the DataContext of the Border (sender)
                        if (sender is FrameworkElement element && element.DataContext is ModelRendererComponent component)
                        {
                             // Try to make relative to project path if possible
                             string projectPath = Core.ProjectManager.Instance.ProjectPath;
                             string finalPath = filePath;
                             
                             if (!string.IsNullOrEmpty(projectPath) && filePath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
                             {
                                 // Make relative
                                 finalPath = System.IO.Path.GetRelativePath(projectPath, filePath);
                             }
                             
                             // Update property
                             component.MaterialPath = finalPath;
                        }
                    }
                }
            }
        }

        private void Inspector_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files != null && files.Length > 0 && System.IO.Path.GetExtension(files[0]).ToLower() == ".cs")
                {
                    e.Effects = System.Windows.DragDropEffects.Copy;
                    if (sender is System.Windows.Controls.ScrollViewer scrollViewer)
                    {
                        scrollViewer.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0x3E, 0x42));
                    }
                }
                else
                {
                    e.Effects = System.Windows.DragDropEffects.None;
                }
            }
            e.Handled = true;
        }

        private void Inspector_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is System.Windows.Controls.ScrollViewer scrollViewer)
            {
                scrollViewer.Background = System.Windows.Media.Brushes.Transparent;
            }
        }

        private void Script_Drop(object sender, System.Windows.DragEventArgs e)
        {
            MonoGameEditor.ViewModels.ConsoleViewModel.Log("[Inspector] Script_Drop called");
            
            // Reset background
            if (sender is System.Windows.Controls.ScrollViewer scrollViewer)
            {
                scrollViewer.Background = System.Windows.Media.Brushes.Transparent;
            }

            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[Inspector] Files dropped: {files?.Length ?? 0}");
                
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[Inspector] File path: {filePath}");
                    
                    if (System.IO.Path.GetExtension(filePath).ToLower() == ".cs")
                    {
                        // Get script name without extension
                        string scriptName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[Inspector] Script name: {scriptName}");
                        
                        // Check available scripts
                        var availableScripts = ScriptManager.Instance.ScriptAssets;
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[Inspector] Available scripts count: {availableScripts.Count}");
                        foreach (var kvp in availableScripts)
                        {
                            MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[Inspector] - {kvp.Key}");
                        }
                        
                        // Try to instantiate the script
                        var script = ScriptManager.Instance.CreateScriptInstance(scriptName);
                        if (script != null)
                        {
                            var selectedObject = SceneManager.Instance.SelectedObject;
                            if (selectedObject != null)
                            {
                                selectedObject.AddComponent(script);
                                MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[Inspector] ✓ Added script: {scriptName}");
                            }
                            else
                            {
                                MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[Inspector] ✗ No object selected");
                            }
                        }
                        else
                        {
                            MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[Inspector] ✗ Failed to create script: {scriptName}. Script may not be compiled.");
                        }
                    }
                }
            }
            else
            {
                MonoGameEditor.ViewModels.ConsoleViewModel.Log("[Inspector] No FileDrop data present");
            }
        }

        private void ScriptComponent_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement border && border.Tag is Core.Components.ScriptComponent script)
            {
                // Find the StackPanel to add properties to
                var panel = border.FindName("ScriptPropertiesPanel") as StackPanel;
                if (panel != null)
                {
                    // Clear existing (except the label)
                    while (panel.Children.Count > 1)
                    {
                        panel.Children.RemoveAt(panel.Children.Count - 1);
                    }

                    // Get editable members (properties and fields)
                    var members = script.GetEditableMembers();
                    
                    if (members.Count > 0)
                    {
                        // Add a title "Script"
                        var title = new System.Windows.Controls.TextBlock
                        {
                            Text = "Script",
                            FontStyle = FontStyles.Italic,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88)),
                            Margin = new Thickness(0, 5, 0, 5)
                        };
                        panel.Children.Add(title);
                        // Add a separator
                        panel.Children.Add(new System.Windows.Controls.Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0x3E, 0x3E)), Margin = new Thickness(0, 0, 0, 5) });
                    }

                    foreach (var member in members)
                    {
                        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 4) };
                        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(90) });
                        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        // Label
                        var label = new System.Windows.Controls.TextBlock
                        {
                            Text = member.Name,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA)),
                            FontSize = 11,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        System.Windows.Controls.Grid.SetColumn(label, 0);
                        grid.Children.Add(label);

                        // Input control based on type
                        FrameworkElement control = null;
                        
                        var propInfo = member as System.Reflection.PropertyInfo;
                        var fieldInfo = member as System.Reflection.FieldInfo;
                        Type memberType = propInfo?.PropertyType ?? fieldInfo?.FieldType;

                        if (memberType == typeof(float) || memberType == typeof(double))
                        {
                            var textBox = new System.Windows.Controls.TextBox
                            {
                                Width = 80,
                                Height = 22,
                                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x37)),
                                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDD, 0xDD, 0xDD)),
                                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55)),
                                BorderThickness = new Thickness(1),
                                Padding = new Thickness(4, 2, 4, 2),
                                VerticalContentAlignment = VerticalAlignment.Center
                            };
                            
                            if (propInfo != null) {
                                var binding = new System.Windows.Data.Binding(member.Name) { Source = script, Mode = System.Windows.Data.BindingMode.TwoWay, StringFormat = "{0:F2}" };
                                textBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, binding);
                            } else {
                                textBox.Text = string.Format("{0:F2}", fieldInfo.GetValue(script));
                                textBox.LostFocus += (s, ev) => {
                                    if (memberType == typeof(float) && float.TryParse(textBox.Text, out float fVal)) fieldInfo.SetValue(script, fVal);
                                    else if (memberType == typeof(double) && double.TryParse(textBox.Text, out double dVal)) fieldInfo.SetValue(script, dVal);
                                };
                            }
                            
                            // Enable draggable label
                            label.Tag = textBox;
                            label.Cursor = Cursors.SizeWE;
                            label.PreviewMouseLeftButtonDown += NumericLabel_PreviewMouseLeftButtonDown;
                            label.PreviewMouseMove += NumericLabel_PreviewMouseMove;
                            label.PreviewMouseLeftButtonUp += NumericLabel_PreviewMouseLeftButtonUp;

                            control = textBox;
                        }
                        else if (memberType == typeof(int))
                        {
                            var textBox = new System.Windows.Controls.TextBox
                            {
                                Width = 80,
                                Height = 22,
                                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x37)),
                                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDD, 0xDD, 0xDD)),
                                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55)),
                                BorderThickness = new Thickness(1),
                                Padding = new Thickness(4, 2, 4, 2),
                                VerticalContentAlignment = VerticalAlignment.Center
                            };
                            if (propInfo != null) {
                                var binding = new System.Windows.Data.Binding(member.Name) { Source = script, Mode = System.Windows.Data.BindingMode.TwoWay };
                                textBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, binding);
                            } else {
                                textBox.Text = fieldInfo.GetValue(script)?.ToString();
                                textBox.LostFocus += (s, ev) => {
                                    if (int.TryParse(textBox.Text, out int val)) fieldInfo.SetValue(script, val);
                                };
                            }

                            // Enable draggable label
                            label.Tag = textBox;
                            label.Cursor = Cursors.SizeWE;
                            label.PreviewMouseLeftButtonDown += NumericLabel_PreviewMouseLeftButtonDown;
                            label.PreviewMouseMove += NumericLabel_PreviewMouseMove;
                            label.PreviewMouseLeftButtonUp += NumericLabel_PreviewMouseLeftButtonUp;

                            control = textBox;
                        }
                        else if (memberType == typeof(bool))
                        {
                            var checkBox = new System.Windows.Controls.CheckBox { VerticalAlignment = VerticalAlignment.Center };
                            if (propInfo != null) {
                                var binding = new System.Windows.Data.Binding(member.Name) { Source = script, Mode = System.Windows.Data.BindingMode.TwoWay };
                                checkBox.SetBinding(System.Windows.Controls.CheckBox.IsCheckedProperty, binding);
                            } else {
                                checkBox.IsChecked = (bool)fieldInfo.GetValue(script);
                                checkBox.Checked += (s, ev) => fieldInfo.SetValue(script, true);
                                checkBox.Unchecked += (s, ev) => fieldInfo.SetValue(script, false);
                            }
                            control = checkBox;
                        }
                        else if (memberType == typeof(string))
                        {
                            var textBox = new System.Windows.Controls.TextBox
                            {
                                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x37)),
                                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDD, 0xDD, 0xDD)),
                                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55)),
                                BorderThickness = new Thickness(1),
                                Padding = new Thickness(4, 2, 4, 2)
                            };
                            if (propInfo != null) {
                                var binding = new System.Windows.Data.Binding(member.Name) { Source = script, Mode = System.Windows.Data.BindingMode.TwoWay };
                                textBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, binding);
                            } else {
                                textBox.Text = fieldInfo.GetValue(script)?.ToString();
                                textBox.LostFocus += (s, ev) => fieldInfo.SetValue(script, textBox.Text);
                            }
                            control = textBox;
                        }
                        else if (memberType == typeof(Microsoft.Xna.Framework.Vector3))
                        {
                            var textBox = new System.Windows.Controls.TextBox
                            {
                                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x37)),
                                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDD, 0xDD, 0xDD)),
                                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55)),
                                BorderThickness = new Thickness(1),
                                Padding = new Thickness(4, 2, 4, 2),
                                ToolTip = "Format: X,Y,Z"
                            };
                            if (propInfo != null) {
                                var binding = new System.Windows.Data.Binding(member.Name) { Source = script, Mode = System.Windows.Data.BindingMode.TwoWay };
                                textBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, binding);
                            } else {
                                var vec = (Microsoft.Xna.Framework.Vector3)fieldInfo.GetValue(script);
                                textBox.Text = $"{vec.X},{vec.Y},{vec.Z}";
                                textBox.LostFocus += (s, ev) => {
                                    try {
                                        string[] parts = textBox.Text.Split(',');
                                        if (parts.Length == 3) {
                                            var newVec = new Microsoft.Xna.Framework.Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
                                            fieldInfo.SetValue(script, newVec);
                                        }
                                    } catch { }
                                };
                            }
                            control = textBox;
                        }
                        else if (typeof(GameObject).IsAssignableFrom(memberType) || typeof(MonoGameEditor.Core.Component).IsAssignableFrom(memberType))
                        {
                            // Reference field (GameObject or Component)
                            var borderRef = new System.Windows.Controls.Border
                            {
                                Height = 22,
                                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x37)),
                                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(2),
                                AllowDrop = true,
                                Tag = new { Script = script, Member = member }
                            };

                            var textBlock = new System.Windows.Controls.TextBlock
                            {
                                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDD, 0xDD, 0xDD)),
                                FontSize = 10,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = new Thickness(6, 0, 6, 0),
                                TextTrimming = TextTrimming.CharacterEllipsis
                            };

                            // Multi-binding or dynamic update for the text
                            var val = propInfo != null ? propInfo.GetValue(script) : fieldInfo.GetValue(script);
                            if (val == null)
                            {
                                textBlock.Text = $"None ({memberType.Name})";
                                textBlock.FontStyle = FontStyles.Italic;
                                textBlock.Opacity = 0.5;
                            }
                            else
                            {
                                string name = "Unknown";
                                if (val is GameObject go) name = go.Name;
                                else if (val is MonoGameEditor.Core.Component c) name = $"{c.GameObject?.Name} ({c.GetType().Name})";
                                textBlock.Text = name;
                            }

                            var innerGrid = new System.Windows.Controls.Grid();
                            innerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            innerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(20) });

                            System.Windows.Controls.Grid.SetColumn(textBlock, 0);
                            innerGrid.Children.Add(textBlock);

                            var clearButton = new System.Windows.Controls.Button
                            {
                                Content = "✕",
                                FontSize = 9,
                                Width = 16,
                                Height = 16,
                                Background = System.Windows.Media.Brushes.Transparent,
                                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88)),
                                BorderThickness = new Thickness(0),
                                Cursor = Cursors.Hand,
                                ToolTip = "Clear Reference",
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = new Thickness(0, 0, 2, 0)
                            };
                            System.Windows.Controls.Grid.SetColumn(clearButton, 1);
                            innerGrid.Children.Add(clearButton);

                            borderRef.Child = innerGrid;

                            clearButton.Click += (s, ev) => {
                                try {
                                    if (propInfo != null) propInfo.SetValue(script, null);
                                    else fieldInfo.SetValue(script, null);

                                    textBlock.Text = $"None ({memberType.Name})";
                                    textBlock.FontStyle = FontStyles.Italic;
                                    textBlock.Opacity = 0.5;
                                } catch (Exception ex) {
                                    ViewModels.ConsoleViewModel.LogError($"[Inspector] Failed to clear reference: {ex.Message}");
                                }
                            };

                            // Drag and Drop handlers for the reference field
                            borderRef.DragEnter += (s, ev) => {
                                if (ev.Data.GetDataPresent("GameObject") || ev.Data.GetDataPresent("Component")) {
                                    ev.Effects = System.Windows.DragDropEffects.Link;
                                    borderRef.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x4C));
                                } else {
                                    ev.Effects = System.Windows.DragDropEffects.None;
                                }
                                ev.Handled = true;
                            };
                            borderRef.DragLeave += (s, ev) => {
                                borderRef.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x37));
                            };
                            borderRef.Drop += (s, ev) => {
                                borderRef.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x37));
                                
                                object droppedObject = null;
                                // Add try-catch around data retrieval to prevent COMException in hybrid WPF/WinForms environments
                                if (ev.Data.GetDataPresent("Component"))
                                {
                                    try {
                                        var droppedComp = ev.Data.GetData("Component") as Core.Component;
                                        if (droppedComp != null)
                                        {
                                            if (memberType.IsAssignableFrom(droppedComp.GetType()))
                                            {
                                                droppedObject = droppedComp;
                                            }
                                            else if (typeof(GameObject).IsAssignableFrom(memberType))
                                            {
                                                droppedObject = droppedComp.GameObject;
                                            }
                                        }
                                    } catch (Exception ex) {
                                        ViewModels.ConsoleViewModel.LogWarning($"[Inspector] Data transfer error (Component): {ex.Message}");
                                    }
                                }
                                
                                if (droppedObject == null && ev.Data.GetDataPresent("GameObject"))
                                {
                                    try {
                                        var droppedGo = ev.Data.GetData("GameObject") as GameObject;
                                        if (droppedGo != null)
                                        {
                                            if (typeof(GameObject).IsAssignableFrom(memberType)) {
                                                droppedObject = droppedGo;
                                            } else if (typeof(MonoGameEditor.Core.Component).IsAssignableFrom(memberType)) {
                                                droppedObject = droppedGo.GetComponent(memberType);
                                            }
                                        }
                                    } catch (Exception ex) {
                                        ViewModels.ConsoleViewModel.LogWarning($"[Inspector] Data transfer error (GameObject): {ex.Message}");
                                    }
                                }

                                if (droppedObject != null)
                                {
                                    try {
                                        if (propInfo != null) propInfo.SetValue(script, droppedObject);
                                        else fieldInfo.SetValue(script, droppedObject);
                                        
                                        // UI Update
                                        string newName = "None";
                                        if (droppedObject is GameObject go) newName = go.Name;
                                        else if (droppedObject is Core.Component c) newName = $"{c.GameObject?.Name} ({c.GetType().Name})";

                                        textBlock.Text = newName;
                                        textBlock.FontStyle = FontStyles.Normal;
                                        textBlock.Opacity = 1.0;
                                    } catch (Exception ex) {
                                        ViewModels.ConsoleViewModel.LogError($"[Inspector] Failed to set reference: {ex.Message}");
                                    }
                                }
                                else if (ev.Data.GetDataPresent("GameObject") || ev.Data.GetDataPresent("Component"))
                                {
                                    ViewModels.ConsoleViewModel.LogWarning($"[Inspector] Dropped item is not compatible with {memberType.Name}");
                                }
                                ev.Handled = true;
                            };

                            control = borderRef;
                        }

                        if (control != null)
                        {
                            System.Windows.Controls.Grid.SetColumn(control, 1);
                            grid.Children.Add(control);
                            panel.Children.Add(grid);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Converts file path to just filename
    /// </summary>
    public class FilePathToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                return System.IO.Path.GetFileName(path);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts null to Visibility
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            bool invert = parameter?.ToString() == "Invert";
            if (invert) isNull = !isNull;
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class XnaColorToWpfColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Microsoft.Xna.Framework.Color xnaColor)
            {
                return System.Windows.Media.Color.FromRgb(xnaColor.R, xnaColor.G, xnaColor.B);
            }
            return System.Windows.Media.Colors.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Markup extension to get enum values for ComboBox
    /// </summary>
    public class EnumValuesExtension : MarkupExtension
    {
        private Type _enumType;

        public EnumValuesExtension(Type enumType)
        {
            _enumType = enumType;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Enum.GetValues(_enumType);
        }
    }
}
