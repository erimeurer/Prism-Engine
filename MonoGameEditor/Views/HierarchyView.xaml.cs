using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using MonoGameEditor.Core;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using WpfPoint = System.Windows.Point;

namespace MonoGameEditor.Views
{
    public partial class HierarchyView : UserControl
    {
        public HierarchyView()
        {
            InitializeComponent();
        }

        private System.Windows.Point _startPoint;
        private bool _isDragging;

        private void TreeView_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is GameObject go)
            {
                SceneManager.Instance.SelectedObject = go;
            }
            else
            {
                SceneManager.Instance.SelectedObject = null;
            }
        }

        private void TreeView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Only toggle focus if an item is actually clicked, usually handled by checking SelectedValue/SelectedItem
            var tree = sender as System.Windows.Controls.TreeView;
            if (tree?.SelectedItem != null)
            {
                ViewModels.MainViewModel.Instance.RequestFocus();
            }
        }

        private void TreeView_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
        }

        private void TreeView_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && !_isDragging)
            {
                System.Windows.Point pos = e.GetPosition(null);
                if (Math.Abs(pos.X - _startPoint.X) > System.Windows.SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(pos.Y - _startPoint.Y) > System.Windows.SystemParameters.MinimumVerticalDragDistance)
                {
                    StartDrag(sender as System.Windows.Controls.TreeView, e);
                }
            }
        }

        private static GameObject? _internalDragObject;

        private void StartDrag(System.Windows.Controls.TreeView tree, System.Windows.Input.MouseEventArgs e)
        {
            if (tree == null || tree.SelectedItem == null) return;
            
            _isDragging = true;
            _internalDragObject = tree.SelectedItem as GameObject;
            
            // We still need a DataObject to initiate the drag events, but we put a dummy string or keep it simple
            DataObject data = new DataObject("GameObject", "InternalDrag"); 
            System.Windows.DragDrop.DoDragDrop(tree, data, System.Windows.DragDropEffects.Move);
            
            _isDragging = false;
            _internalDragObject = null;
        }

        private void TreeView_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (_internalDragObject == null)
            {
                e.Effects = System.Windows.DragDropEffects.None;
                return;
            }

            // If hovering over an item, it's a "Reparent to item" operation
            // If hovering over empty space, it's a "Unparent to root" operation
            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
        }

        private void TreeView_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (_internalDragObject != null)
            {
                var draggedObject = _internalDragObject;
                
                // Find target
                var tree = sender as System.Windows.Controls.TreeView;
                var targetItem = GetTreeViewItemUnderMouse(tree, e.GetPosition(tree));
                GameObject? targetObject = targetItem?.DataContext as GameObject; // If null, means dropped on root

                SceneManager.Instance.Reparent(draggedObject, targetObject);
            }
        }
        
        private System.Windows.Controls.TreeViewItem? GetTreeViewItemUnderMouse(System.Windows.Controls.TreeView tree, System.Windows.Point point)
        {
            System.Windows.IInputElement element = tree.InputHitTest(point);
            System.Windows.DependencyObject depObj = element as System.Windows.DependencyObject;
            
            while (depObj != null && !(depObj is System.Windows.Controls.TreeViewItem))
            {
                depObj = System.Windows.Media.VisualTreeHelper.GetParent(depObj);
            }
            
            return depObj as System.Windows.Controls.TreeViewItem;
        }

        private void TreeView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                var vm = DataContext as ViewModels.HierarchyViewModel;
                if (vm != null && vm.DeleteObjectCommand.CanExecute(null))
                {
                    vm.DeleteObjectCommand.Execute(null);
                }
            }
        }
    }

    /// <summary>
    /// Converts bool IsActive to text color
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
            {
                return new WpfBrush(WpfColor.FromRgb(220, 220, 220));
            }
            return new WpfBrush(WpfColor.FromRgb(100, 100, 100));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts GameObjectType to icon visual
    /// </summary>
    public class ObjectTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var canvas = new Canvas { Width = 16, Height = 16 };
            
            if (value is GameObjectType type)
            {
                switch (type)
                {
                    case GameObjectType.Camera:
                        // Camera icon - simplified movie camera
                        var cameraBody = new System.Windows.Shapes.Rectangle
                        {
                            Width = 10, Height = 7,
                            Fill = new WpfBrush(WpfColor.FromRgb(100, 149, 237)),
                            RadiusX = 1, RadiusY = 1
                        };
                        Canvas.SetLeft(cameraBody, 1);
                        Canvas.SetTop(cameraBody, 5);
                        canvas.Children.Add(cameraBody);
                        
                        var lens = new System.Windows.Shapes.Polygon
                        {
                            Points = new System.Windows.Media.PointCollection 
                            { 
                                new WpfPoint(11, 6), new WpfPoint(15, 4), 
                                new WpfPoint(15, 11), new WpfPoint(11, 9) 
                            },
                            Fill = new WpfBrush(WpfColor.FromRgb(100, 149, 237))
                        };
                        canvas.Children.Add(lens);
                        break;

                    case GameObjectType.Light:
                        // Light icon - sun/bulb
                        var lightCircle = new System.Windows.Shapes.Ellipse
                        {
                            Width = 8, Height = 8,
                            Fill = new WpfBrush(WpfColor.FromRgb(255, 200, 50))
                        };
                        Canvas.SetLeft(lightCircle, 4);
                        Canvas.SetTop(lightCircle, 4);
                        canvas.Children.Add(lightCircle);
                        
                        // Rays
                        for (int i = 0; i < 8; i++)
                        {
                            double angle = i * Math.PI / 4;
                            var ray = new System.Windows.Shapes.Line
                            {
                                X1 = 8 + Math.Cos(angle) * 5,
                                Y1 = 8 + Math.Sin(angle) * 5,
                                X2 = 8 + Math.Cos(angle) * 7,
                                Y2 = 8 + Math.Sin(angle) * 7,
                                Stroke = new WpfBrush(WpfColor.FromRgb(255, 200, 50)),
                                StrokeThickness = 1
                            };
                            canvas.Children.Add(ray);
                        }
                        break;

                    default:
                        // Default 3D Cube Icon (Isometric)
                        var cubePath = new System.Windows.Shapes.Path
                        {
                            Stroke = new WpfBrush(WpfColor.FromRgb(160, 160, 160)),
                            StrokeThickness = 1,
                            Fill = System.Windows.Media.Brushes.Transparent,
                            // Hexagon + Internal lines to form a cube
                            Data = System.Windows.Media.Geometry.Parse("M 3,4.5 L 8,2 L 13,4.5 L 13,10.5 L 8,13 L 3,10.5 Z M 3,4.5 L 8,7 L 13,4.5 M 8,7 L 8,13")
                        };
                        canvas.Children.Add(cubePath);
                        break;
                }
            }
            
            return canvas;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
