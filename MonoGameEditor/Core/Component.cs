using System.ComponentModel;

namespace MonoGameEditor.Core
{
    /// <summary>
    /// Base class for all components that can be attached to GameObjects
    /// </summary>
    public abstract class Component : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isEnabled = true;

        /// <summary>
        /// Whether this component is currently enabled and active
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        /// <summary>
        /// The GameObject this component is attached to
        /// </summary>
        public GameObject? GameObject { get; internal set; }

        /// <summary>
        /// Display name of the component
        /// </summary>
        public abstract string ComponentName { get; }

        /// <summary>
        /// Whether this component can be removed
        /// </summary>
        public virtual bool CanRemove => true;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
