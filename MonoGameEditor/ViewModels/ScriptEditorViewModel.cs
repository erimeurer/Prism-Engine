using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MonoGameEditor.ViewModels
{
    public class ScriptEditorViewModel : DocumentViewModel
    {
        private string _scriptPath = string.Empty;
        private string _scriptName = string.Empty;
        private bool _isDirty = false;
        private int _currentLine = 1;
        private int _currentColumn = 1;

        public string ScriptPath
        {
            get => _scriptPath;
            set { _scriptPath = value; OnPropertyChanged(); }
        }

        public string ScriptName
        {
            get => _scriptName;
            set { _scriptName = value; OnPropertyChanged(); }
        }

        public bool IsDirty
        {
            get => _isDirty;
            set { _isDirty = value; OnPropertyChanged(); }
        }

        public int CurrentLine
        {
            get => _currentLine;
            set { _currentLine = value; OnPropertyChanged(); }
        }

        public int CurrentColumn
        {
            get => _currentColumn;
            set { _currentColumn = value; OnPropertyChanged(); }
        }

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }

        public ScriptEditorViewModel() : base("Script")
        {
            SaveCommand = new RelayCommand(_ => Save(), _ => !string.IsNullOrEmpty(_scriptPath));
            CloseCommand = new RelayCommand(_ => Close());
        }

        private void Save()
        {
            // Save will be handled by the View
        }

        private void Close()
        {
            // Close will be handled by the View
        }
    }
}
