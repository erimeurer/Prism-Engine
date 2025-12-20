using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using MonoGameEditor.ViewModels;

namespace MonoGameEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                // Update button content if using an icon font that differentiates
                MaximizeButton.Content = "1"; // '1' in Marlett is Maximize
            }
            else
            {
                WindowState = WindowState.Maximized;
                 MaximizeButton.Content = "2"; // '2' in Marlett is Restore
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        public void ShowLoadingOverlay(string message = "Loading...")
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] ShowLoadingOverlay called: {message}");
            Dispatcher.Invoke(() =>
            {
                LoadingStatusText.Text = message;
                LoadingOverlay.Visibility = Visibility.Visible;
                
                // CRITICAL: Hide the main content Grid to prevent 3D rendering over overlay
                // We do this via the ViewModel property to avoid breaking bindings
                MainViewModel.Instance.IsMainContentVisible = false;
                
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Overlay visibility set to Visible");
            });
        }

        public void HideLoadingOverlay()
        {
            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                
                // Restore main content visibility
                MainViewModel.Instance.IsMainContentVisible = true;
            });
        }
    }
}