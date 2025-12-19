// Stub implementation for Runtime build
// The full implementation is in MonoGameEditor.ViewModels but Runtime doesn't need it
#if RUNTIME_BUILD
namespace MonoGameEditor.ViewModels
{
    public class ViewModelBase
    {
        protected void OnPropertyChanged(string propertyName = "") { }
    }
    
    public class ShaderPropertyViewModel
    {
        public ShaderPropertyViewModel(object prop, object value) 
        { 
            Value = value;
        }
        
        public object Value { get; set; }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}

namespace MonoGameEditor.Core.Components
{
    public class MaterialSlotViewModel : MonoGameEditor.ViewModels.ViewModelBase
    {
        public int Index { get; set; }
        public string MeshName { get; set; } = string.Empty;
        private string _materialPath = string.Empty;
        
        public string MaterialPath
        {
            get => _materialPath;
            set
            {
                if (_materialPath != value)
                {
                    _materialPath = value;
                    OnPropertyChanged();
                    MaterialPathChanged?.Invoke(this, value);
                }
            }
        }
        
        public event System.EventHandler<string>? MaterialPathChanged;
    }
}

namespace MonoGameEditor.ViewModels
{
    /// <summary>
    /// Stub for ConsoleViewModel - does nothing in runtime
    /// </summary>
    public static class ConsoleViewModel
    {
        public static void Log(string message) { }
        public static void LogWarning(string message) { }
        public static void LogError(string message) { }
    }
    
    /// <summary>
    /// Stub for EditorLogger - does nothing in runtime
    /// </summary>
    public class EditorLogger
    {
        public static EditorLogger Instance { get; } = new EditorLogger();
        public void Log(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { }
    }
}
#endif
