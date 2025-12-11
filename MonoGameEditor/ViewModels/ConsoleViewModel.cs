using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MonoGameEditor.ViewModels
{
    public class ConsoleViewModel : ToolViewModel
    {
        private static ConsoleViewModel? _instance;
        public static ConsoleViewModel Instance => _instance ??= new ConsoleViewModel();

        public ObservableCollection<string> LogMessages { get; } = new();

        public ConsoleViewModel() : base("Console")
        {
            _instance = this;
        }

        public static void Log(string message)
        {
            Instance.AddLog(message);
        }

        public void AddLog(string message)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            });
        }

        public void Clear()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                LogMessages.Clear();
            });
        }
    }
}
