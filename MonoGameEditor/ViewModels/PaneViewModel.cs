namespace MonoGameEditor.ViewModels
{
    public class PaneViewModel : ViewModelBase
    {
        private string _title = string.Empty;
        public string Title 
        { 
            get => _title; 
            set { _title = value; OnPropertyChanged(); } 
        }

        private string _contentId = string.Empty;
        public string ContentId
        {
            get => _contentId;
            set { _contentId = value; OnPropertyChanged(); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        
        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public PaneViewModel(string title)
        {
            Title = title;
            ContentId = title;
        }
    }
}
