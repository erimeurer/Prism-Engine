using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using MonoGameEditor.Models;
using System.IO;

namespace MonoGameEditor.ViewModels
{
    public class ConsoleViewModel : ToolViewModel, INotifyPropertyChanged
    {
        private static ConsoleViewModel? _instance;
        public static ConsoleViewModel Instance => _instance ??= new ConsoleViewModel();

        private ObservableCollection<LogMessage> _allMessages = new();
        private int _selectedTabIndex = 0;
        
        // Auto-save logs to project root (accessible, not in bin)
        private static readonly string _logFilePath = Path.Combine(
            Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? "",
            "..", "..", "..",
            $"DebugLogs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        );
        private static StreamWriter? _logWriter;

        public ObservableCollection<LogMessage> AllMessages 
        { 
            get => _allMessages;
            set
            {
                _allMessages = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<LogMessage> DisplayedMessages { get; } = new();

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                _selectedTabIndex = value;
                OnPropertyChanged();
                UpdateDisplayedMessages();
            }
        }

        public int InfoCount => AllMessages.Count(m => m.Type == LogType.Info);
        public int WarningCount => AllMessages.Count(m => m.Type == LogType.Warning);
        public int ErrorCount => AllMessages.Count(m => m.Type == LogType.Error);
        public int AllCount => AllMessages.Count;

        public bool HasErrors => ErrorCount > 0;
        public string StatusText => HasErrors ? $"Compilation Failed ({ErrorCount} error(s))" : "Ready";
        public string StatusColor => HasErrors ? "#FF4444" : "#4EC9B0";

        public ConsoleViewModel() : base("Console")
        {
            try
            {
                _logWriter = new StreamWriter(_logFilePath, append: true) { AutoFlush = true };
                _logWriter.WriteLine($"=== Prism Engine Debug Log Started: {DateTime.Now} ===");
            }
            catch { }
        }

        public static void Log(string message)
        {
            Instance.AddMessage(message, LogType.Info);
        }

        public static void LogInfo(string message)
        {
            Instance.AddMessage(message, LogType.Info);
        }

        public static void LogWarning(string message)
        {
            Instance.AddMessage(message, LogType.Warning);
        }

        public static void LogError(string message)
        {
            Instance.AddMessage(message, LogType.Error);
        }

        private void AddMessage(string message, LogType type)
        {
            // Write to file immediately
            try
            {
                var prefix = type switch
                {
                    LogType.Warning => "[⚠]",
                    LogType.Error => "[❌]",
                    _ => "[ℹ]"
                };
                _logWriter?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {prefix} {message}");
            }
            catch { }
            
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var logMsg = new LogMessage(message, type);

                AllMessages.Add(logMsg);

                if (ShouldDisplay(logMsg))
                {
                    DisplayedMessages.Add(logMsg);
                }
                
                // Update counts and status bar
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(WarningCount));
                OnPropertyChanged(nameof(InfoCount));
                OnPropertyChanged(nameof(AllCount));
                OnPropertyChanged(nameof(HasErrors));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            });
        }

        private bool ShouldDisplay(LogMessage msg)
        {
            return SelectedTabIndex switch
            {
                1 => msg.Type == LogType.Info,
                2 => msg.Type == LogType.Warning,
                3 => msg.Type == LogType.Error,
                _ => true
            };
        }

        public void Clear()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                AllMessages.Clear();
                DisplayedMessages.Clear();
                UpdateDisplayedMessages();
                
                // Update counts and status bar
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(WarningCount));
                OnPropertyChanged(nameof(InfoCount));
                OnPropertyChanged(nameof(AllCount));
                OnPropertyChanged(nameof(HasErrors));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            });
        }

        public void ClearErrors()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var errors = AllMessages.Where(m => m.Type == LogType.Error).ToList();
                foreach (var error in errors)
                {
                    AllMessages.Remove(error);
                }
                UpdateDisplayedMessages();
                
                // Update counts and status bar
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(WarningCount));
                OnPropertyChanged(nameof(InfoCount));
                OnPropertyChanged(nameof(AllCount));
                OnPropertyChanged(nameof(HasErrors));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            });
        }

        private void UpdateDisplayedMessages()
        {
            DisplayedMessages.Clear();

            var filtered = SelectedTabIndex switch
            {
                1 => AllMessages.Where(m => m.Type == LogType.Info),
                2 => AllMessages.Where(m => m.Type == LogType.Warning),
                3 => AllMessages.Where(m => m.Type == LogType.Error),
                _ => AllMessages
            };

            foreach (var msg in filtered)
            {
                DisplayedMessages.Add(msg);
            }
        }
        
        public void CopyToClipboard()
        {
            try
            {
                var logs = string.Join(Environment.NewLine, AllMessages.Select(m => $"{m.Icon} {m.FormattedMessage}"));
                System.Windows.Clipboard.SetText(logs);
                LogInfo("📋 Logs copiados para clipboard!");
            }
            catch (Exception ex)
            {
                LogError($"Erro ao copiar: {ex.Message}");
            }
        }

        public new event PropertyChangedEventHandler? PropertyChanged;
        protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
