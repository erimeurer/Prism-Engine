using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using MonoGameEditor.Core;

namespace MonoGameEditor.ViewModels
{
    public class ProjectViewModel : ToolViewModel
    {
        public ObservableCollection<DirectoryItemViewModel> Roots { get; } = new ObservableCollection<DirectoryItemViewModel>();

        private DirectoryItemViewModel _rootDirectory;
        public DirectoryItemViewModel RootDirectory
        {
            get => _rootDirectory;
            set 
            { 
                _rootDirectory = value; 
                OnPropertyChanged();
                Roots.Clear();
                if (_rootDirectory != null) Roots.Add(_rootDirectory);
            }
        }

        private DirectoryItemViewModel _selectedDirectory;
        public DirectoryItemViewModel SelectedDirectory
        {
            get => _selectedDirectory;
            set 
            { 
                if (_selectedDirectory != value)
                {
                     _selectedDirectory = value; 
                    OnPropertyChanged();
                    LoadCurrentDirectory();
                    
                    // Sync with TreeView
                    if (_selectedDirectory != null)
                    {
                        _selectedDirectory.IsSelected = true;
                        // Expand parents to ensure visibility
                        var parent = _selectedDirectory.Parent;
                        while (parent != null)
                        {
                            parent.IsExpanded = true;
                            parent = parent.Parent;
                        }
                    }
                }
            }
        }

        public ICommand NavigateUpCommand => new RelayCommand(_ => NavigateUp(), _ => CanNavigateUp());

        private bool CanNavigateUp()
        {
            return SelectedDirectory != null && SelectedDirectory.Parent != null;
        }

        private void NavigateUp()
        {
            if (CanNavigateUp())
            {
                SelectedDirectory = SelectedDirectory.Parent;
            }
        }

        // Items to display in the Right Grid (Files + Subfolders)
        public ObservableCollection<FileSystemItemViewModel> GridItems { get; } = new ObservableCollection<FileSystemItemViewModel>();

        public ProjectViewModel() : base("Project") 
        {
            // Subscribe to ProjectLoaded to refresh
            ProjectManager.Instance.ProjectLoaded += Refresh;
            Refresh();
        }

        public void Refresh()
        {
            var assetsPath = ProjectManager.Instance.AssetsPath;
            if (!string.IsNullOrEmpty(assetsPath) && Directory.Exists(assetsPath))
            {
                RootDirectory = new DirectoryItemViewModel(assetsPath);
                SelectedDirectory = RootDirectory;
            }
            else
            {
                RootDirectory = null;
                GridItems.Clear();
            }
        }

        private void LoadCurrentDirectory()
        {
            GridItems.Clear();
            if (SelectedDirectory == null) return;

            // Refresh sub-items if needed
            SelectedDirectory.LoadChildren();

            foreach (var kid in SelectedDirectory.Children)
            {
                GridItems.Add(kid);
            }
            
            // Should we add files too? DirectoryItemViewModel usually contains both or just dirs?
            // Let's assume DirectoryItemViewModel.Children contains sub-folders.
            // We need files too.
            // Let's load files here manually or add them to DirectoryItemViewModel.
            // Better to load files here for the Grid.
            
            try
            {
                var files = Directory.GetFiles(SelectedDirectory.FullPath);
                foreach (var file in files)
                {
                    GridItems.Add(new FileItemViewModel(file));
                }
            }
            catch {}
        }
        
        // Command to handle double click on Grid Item
        public ICommand OpenItemCommand => new RelayCommand(OpenItem);
        
        private void OpenItem(object param)
        {
            if (param is DirectoryItemViewModel dirVm)
            {
                SelectedDirectory = dirVm;
                // We should also Expand the tree to show this selection if we bound it two-way
                dirVm.IsExpanded = true;
            }
            else if (param is FileItemViewModel fileVm)
            {
                // Handle opening file (e.g. if scene, load it)
                if (fileVm.Name.EndsWith(".world"))
                {
                    MainViewModel.Instance?.OpenScene(fileVm.FullPath);
                }
            }
        }
    }

    public abstract class FileSystemItemViewModel : ViewModelBase
    {
        public string FullPath { get; }
        public string Name { get; }
        public bool IsDirectory { get; }
        
        public FileSystemItemViewModel(string path, bool isDirectory)
        {
            FullPath = path;
            Name = Path.GetFileName(path);
            IsDirectory = isDirectory;
        }
    }

    public class DirectoryItemViewModel : FileSystemItemViewModel
    {
        public ObservableCollection<DirectoryItemViewModel> Children { get; } = new ObservableCollection<DirectoryItemViewModel>();
        public DirectoryItemViewModel Parent { get; }
        
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set 
            { 
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    if (_isExpanded) LoadChildren();
                }
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public DirectoryItemViewModel(string path, DirectoryItemViewModel parent = null) : base(path, true)
        {
            Parent = parent;
            // Add dummy if has subdirs to support lazy loading
            if (HasSubdirectories())
            {
                Children.Add(null); 
            }
        }

        private bool HasSubdirectories()
        {
            try
            {
                return Directory.EnumerateDirectories(FullPath).Any();
            }
            catch
            {
                return false;
            }
        }

        public void LoadChildren()
        {
            // Clear dummy
            if (Children.Count == 1 && Children[0] == null) Children.Clear();
            
            // Don't reload if already loaded (unless forced?)
            if (Children.Count > 0) return;

            try
            {
                var dirs = Directory.GetDirectories(FullPath);
                foreach (var dir in dirs)
                {
                    Children.Add(new DirectoryItemViewModel(dir, this));
                }
            }
            catch { }
        }
    }

    public class FileItemViewModel : FileSystemItemViewModel
    {
        public FileItemViewModel(string path) : base(path, false) { }
    }
}
