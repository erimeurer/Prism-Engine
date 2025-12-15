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
