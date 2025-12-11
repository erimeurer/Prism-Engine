using System.Windows.Forms.Integration;

namespace MonoGameEditor.Controls
{
    /// <summary>
    /// WPF wrapper for MonoGameControl using WindowsFormsHost
    /// </summary>
    public class MonoGameHost : WindowsFormsHost
    {
        private MonoGameControl? _gameControl;
        
        public MonoGameControl? GameControl => _gameControl;

        public bool ShowGrid
        {
            get => _gameControl?.ShowGrid ?? true;
            set { if (_gameControl != null) _gameControl.ShowGrid = value; }
        }

        public MonoGameHost()
        {
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_gameControl == null)
            {
                _gameControl = new MonoGameControl();
                Child = _gameControl;
            }
        }
    }
}
