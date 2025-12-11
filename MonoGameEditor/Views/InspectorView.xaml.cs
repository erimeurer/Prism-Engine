using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;

namespace MonoGameEditor.Views
{
    public partial class InspectorView : UserControl
    {
        public InspectorView()
        {
            InitializeComponent();
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
            if (sender is Button button && button.Tag is Component component)
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
