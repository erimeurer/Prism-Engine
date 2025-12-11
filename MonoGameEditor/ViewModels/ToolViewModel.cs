namespace MonoGameEditor.ViewModels
{
    public class ToolViewModel : PaneViewModel
    {
        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public ToolViewModel(string title) : base(title)
        {
        }
    }

    public class DocumentViewModel : PaneViewModel
    {
        public DocumentViewModel(string title) : base(title)
        {
        }
    }
}
