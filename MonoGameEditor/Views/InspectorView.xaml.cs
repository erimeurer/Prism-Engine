#nullable disable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;

namespace MonoGameEditor.Views
{
    public partial class InspectorView : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

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

                    // Get editable properties
                    var properties = script.GetEditableProperties();
                    
                    if (properties.Count > 0)
                    {
                        // Add a separator
                        panel.Children.Add(new System.Windows.Controls.Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0x3E, 0x3E)), Margin = new Thickness(0, 5, 0, 5) });
                    }

                    foreach (var prop in properties)
                    {
                        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 4) };
                        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(90) });
                        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        // Label
                        var label = new System.Windows.Controls.TextBlock
                        {
                            Text = prop.Name,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA)),
                            FontSize = 11,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        System.Windows.Controls.Grid.SetColumn(label, 0);
                        grid.Children.Add(label);

                        // Input control based on type
                        FrameworkElement control = null;
                        var propType = prop.PropertyType;

                        if (propType == typeof(float) || propType == typeof(double))
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
                            var binding = new System.Windows.Data.Binding(prop.Name) { Source = script, Mode = System.Windows.Data.BindingMode.TwoWay, StringFormat = "{0:F2}" };
                            textBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, binding);
                            control = textBox;
                        }
                        else if (propType == typeof(int))
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
                            var binding = new System.Windows.Data.Binding(prop.Name) { Source = script, Mode = System.Windows.Data.BindingMode.TwoWay };
                            textBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, binding);
                            control = textBox;
                        }
                        else if (propType == typeof(bool))
                        {
                            var checkBox = new System.Windows.Controls.CheckBox { VerticalAlignment = VerticalAlignment.Center };
                            var binding = new System.Windows.Data.Binding(prop.Name) { Source = script, Mode = System.Windows.Data.BindingMode.TwoWay };
                            checkBox.SetBinding(System.Windows.Controls.CheckBox.IsCheckedProperty, binding);
                            control = checkBox;
                        }
                        else if (propType == typeof(string))
                        {
                            var textBox = new System.Windows.Controls.TextBox
                            {
                                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x37)),
                                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDD, 0xDD, 0xDD)),
                                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55)),
                                BorderThickness = new Thickness(1),
                                Padding = new Thickness(4, 2, 4, 2)
                            };
                            var binding = new System.Windows.Data.Binding(prop.Name) { Source = script, Mode = System.Windows.Data.BindingMode.TwoWay };
                            textBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, binding);
                            control = textBox;
                        }
                        else if (propType == typeof(Microsoft.Xna.Framework.Vector3))
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
                            var binding = new System.Windows.Data.Binding(prop.Name) { Source = script, Mode = System.Windows.Data.BindingMode.TwoWay };
                            textBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, binding);
                            control = textBox;
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
