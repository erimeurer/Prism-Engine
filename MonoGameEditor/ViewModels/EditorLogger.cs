using MonoGameEditor.Core;

namespace MonoGameEditor.ViewModels
{
    /// <summary>
    /// Adapter that bridges ConsoleViewModel to the Core Logger interface
    /// </summary>
    public class EditorLogger : ILogger
    {
        public void Log(string message) => ConsoleViewModel.Log(message);
        public void LogWarning(string message) => ConsoleViewModel.LogWarning(message);
        public void LogError(string message) => ConsoleViewModel.LogError(message);
    }
}
