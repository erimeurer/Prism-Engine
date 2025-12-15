using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MonoGameEditor.Core;

namespace MonoGameEditor.ViewModels
{
    public class ProjectViewModel : ToolViewModel
    {
        public ObservableCollection<DirectoryItemViewModel> Roots { get; } = new ObservableCollection<DirectoryItemViewModel>();

        private DirectoryItemViewModel? _rootDirectory;
        public DirectoryItemViewModel? RootDirectory
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
                    // Skip .meta files
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;
                        
                    GridItems.Add(new FileItemViewModel(file));
                }
            }
            catch {}
        }
        
        private object _selectedGridItem;
        public object SelectedGridItem
        {
            get => _selectedGridItem;
            set
            {
                if (_selectedGridItem != value)
                {
                    _selectedGridItem = value;
                    OnPropertyChanged();

                    if (_selectedGridItem is FileItemViewModel fileVm)
                    {
                        SelectedFile = fileVm;
                    }
                    else
                    {
                        SelectedFile = null;
                    }
                    
                    if (_selectedGridItem is DirectoryItemViewModel dirVm)
                    {
                         // Optional: Sync tree selection? 
                         // Usually identifying a folder in grid doesn't select it in tree until entered.
                    }
                }
            }
        }

        private FileItemViewModel? _selectedFile;
        public FileItemViewModel? SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (_selectedFile != value)
                {
                    _selectedFile = value;
                    OnPropertyChanged();
                    // Notify MainViewModel (via event or direct access if needed, 
                    // but ideally MainViewModel subscribes to us)
                }
            }
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
                SelectedFile = null; // Clear file selection
            }
            else if (param is FileItemViewModel fileVm)
            {
                SelectedFile = fileVm;

                // Handle opening file (e.g. if scene, load it)
                if (fileVm.Name.EndsWith(".world"))
                {
                    MainViewModel.Instance?.OpenScene(fileVm.FullPath);
                }
            }
        }

        public ICommand CreateFolderCommand => new RelayCommand(CreateNewFolder);

        private void CreateNewFolder(object param)
        {
            try
            {
                string parentPath = null;
                DirectoryItemViewModel targetVm = null;

                if (param is DirectoryItemViewModel dirVm)
                {
                    targetVm = dirVm;
                    parentPath = dirVm.FullPath;
                }
                else if (SelectedDirectory != null)
                {
                    targetVm = SelectedDirectory;
                    parentPath = SelectedDirectory.FullPath;
                }

                if (string.IsNullOrEmpty(parentPath)) return;

                // Generate unique name
                string baseName = "New Folder";
                string newPath = Path.Combine(parentPath, baseName);
                int counter = 1;
                while (Directory.Exists(newPath))
                {
                    newPath = Path.Combine(parentPath, $"{baseName} {counter}");
                    counter++;
                }

                Directory.CreateDirectory(newPath);

                // Refresh logic
                // If we created it in the currently viewed directory, refresh the grid
                if (SelectedDirectory != null && SelectedDirectory.FullPath == parentPath)
                {
                    // Force reload of children for the selected directory to update tree + grid
                    SelectedDirectory.Children.Clear(); // Force reload
                    SelectedDirectory.LoadChildren();
                    LoadCurrentDirectory();

                    // Find the new item and put it in rename mode
                    var newItem = GridItems.FirstOrDefault(x => x.FullPath == newPath);
                    if (newItem != null)
                    {
                        newItem.IsRenaming = true;
                    }
                }
                else if (targetVm != null)
                {
                    // We created it in a tree node that might be expanded but not selected
                    targetVm.Children.Clear(); // Force reload
                    targetVm.LoadChildren();
                    
                    var newItem = targetVm.Children.FirstOrDefault(x => x.FullPath == newPath);
                    if (newItem != null)
                    {
                        newItem.IsRenaming = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating folder: {ex.Message}");
            }
        }

        public ICommand CreateMaterialCommand => new RelayCommand(CreateNewMaterial);

        private void CreateNewMaterial(object param)
        {
            try
            {
                string parentPath = null;

                if (param is DirectoryItemViewModel dirVm)
                {
                    parentPath = dirVm.FullPath;
                }
                else if (SelectedDirectory != null)
                {
                    parentPath = SelectedDirectory.FullPath;
                }

                if (string.IsNullOrEmpty(parentPath)) return;

                // Generate unique name
                string baseName = "NewMaterial";
                string newPath = Path.Combine(parentPath, $"{baseName}.mat");
                int counter = 1;
                while (File.Exists(newPath))
                {
                    newPath = Path.Combine(parentPath, $"{baseName}{counter}.mat");
                    counter++;
                }

                // Create default material
                var material = Core.Materials.PBRMaterial.CreateDefault();
                material.Name = Path.GetFileNameWithoutExtension(newPath);

                // Save to JSON (shader will be set by MaterialEditorViewModel fallback)
                string json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    name = material.Name,
                    albedoColor = new float[] { material.AlbedoColor.R / 255f, material.AlbedoColor.G / 255f, material.AlbedoColor.B / 255f },
                    metallic = material.Metallic,
                    roughness = material.Roughness,
                    ambientOcclusion = material.AmbientOcclusion
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(newPath, json);

                // Refresh directory view
                if (SelectedDirectory != null && SelectedDirectory.FullPath == parentPath)
                {
                    LoadCurrentDirectory();

                    // Find and rename the new file
                    var newItem = GridItems.OfType<FileItemViewModel>().FirstOrDefault(x => x.FullPath == newPath);
                    if (newItem != null)
                    {
                        newItem.IsRenaming = true;
                    }
                }

                Console.WriteLine($"Created material: {newPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating material: {ex.Message}");
            }
        }

        public ICommand CreateShaderCommand => new RelayCommand(CreateNewShader);

        private void CreateNewShader(object param)
        {
            try
            {
                string parentPath = null;

                if (param is DirectoryItemViewModel dirVm)
                {
                    parentPath = dirVm.FullPath;
                }
                else if (SelectedDirectory != null)
                {
                    parentPath = SelectedDirectory.FullPath;
                }

                if (string.IsNullOrEmpty(parentPath)) return;

                // Generate unique name
                string baseName = "NewShader";
                string newPath = Path.Combine(parentPath, $"{baseName}.fx");
                int counter = 1;
                while (File.Exists(newPath))
                {
                    newPath = Path.Combine(parentPath, $"{baseName}{counter}.fx");
                    counter++;
                }

                // Create shader template
                string template = @"#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0
	#define PS_SHADERMODEL ps_4_0
#endif

// Transformation matrices
float4x4 World;
float4x4 View;
float4x4 Projection;

// Custom properties - Change these!
float4 MainColor = float4(1, 1, 1, 1);

struct VertexShaderInput
{
    float4 Position : POSITION0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;
    
    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
    
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    return MainColor;
}

technique BasicColorDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
";

                File.WriteAllText(newPath, template);

                // Refresh directory view
                if (SelectedDirectory != null && SelectedDirectory.FullPath == parentPath)
                {
                    LoadCurrentDirectory();

                    // Find and rename the new file
                    var newItem = GridItems.OfType<FileItemViewModel>().FirstOrDefault(x => x.FullPath == newPath);
                    if (newItem != null)
                    {
                        newItem.IsRenaming = true;
                    }
                }

                Console.WriteLine($"Created shader: {newPath}");
                Console.WriteLine("Note: Compile shader with MGCB or mgfxc tool to use in materials");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating shader: {ex.Message}");
            }
        }
    }

    public abstract class FileSystemItemViewModel : ViewModelBase
    {
        public string FullPath { get; set; } // Settable for rename
        public string Name { get; set; } // Settable for rename
        public bool IsDirectory { get; }
        
        private bool _isRenaming;
        public bool IsRenaming
        {
            get => _isRenaming;
            set
            {
                if (_isRenaming != value)
                {
                    _isRenaming = value;
                    OnPropertyChanged();
                    if (_isRenaming)
                    {
                        RenameText = Name;
                    }
                }
            }
        }

        private string _renameText;
        public string RenameText
        {
            get => _renameText;
            set { _renameText = value; OnPropertyChanged(); }
        }

        public ICommand RenameCommand => new RelayCommand(_ => ExecuteRename());
        public ICommand StartRenameCommand => new RelayCommand(_ => IsRenaming = true);
        public ICommand CancelRenameCommand => new RelayCommand(_ => CancelRename());

        public FileSystemItemViewModel(string path, bool isDirectory)
        {
            FullPath = path;
            Name = Path.GetFileName(path);
            IsDirectory = isDirectory;
        }

        private void ExecuteRename()
        {
            if (string.IsNullOrWhiteSpace(RenameText) || RenameText == Name)
            {
                IsRenaming = false;
                return;
            }

            try
            {
                string parentDir = Path.GetDirectoryName(FullPath);
                string renamedText = RenameText;
                
                // Auto-add .mat extension for material files if missing
                if (!IsDirectory && Path.GetExtension(FullPath).Equals(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    if (!renamedText.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    {
                        renamedText += ".mat";
                    }
                }
                
                string newPath = Path.Combine(parentDir, renamedText);

                if (IsDirectory)
                {
                    if (Directory.Exists(newPath)) 
                    {
                        // Handle collision or warn? For now just cancel
                        IsRenaming = false;
                        return;
                    }
                    Directory.Move(FullPath, newPath);
                }
                else
                {
                    if (File.Exists(newPath))
                    {
                        IsRenaming = false;
                        return;
                    }
                    File.Move(FullPath, newPath);
                }

                FullPath = newPath;
                Name = renamedText;
                RenameText = renamedText;  // Update rename text to include extension
                OnPropertyChanged(nameof(FullPath));
                OnPropertyChanged(nameof(Name));
                IsRenaming = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renaming: {ex.Message}");
                IsRenaming = false;
            }
        }

        private void CancelRename()
        {
            IsRenaming = false;
            RenameText = Name;
        }
    }

    public class DirectoryItemViewModel : FileSystemItemViewModel
    {
        public ObservableCollection<DirectoryItemViewModel?> Children { get; } = new ObservableCollection<DirectoryItemViewModel?>();
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
        private MonoGameEditor.Core.Assets.AssetMetadata _metadata;
        public MonoGameEditor.Core.Assets.AssetMetadata Metadata
        {
            get => _metadata;
            private set { _metadata = value; OnPropertyChanged(); }
        }

        private string _infoText;
        public string InfoText
        {
            get => _infoText;
            set { _infoText = value; OnPropertyChanged(); }
        }
        
        // Placeholder for thumbnail
        private object _thumbnail; // Using object to be flexible (ImageSource or string path)
        public object Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(); }
        }

        public FileItemViewModel(string path) : base(path, false) 
        {
            UpdateInfo();
            LoadMetadataAsync();
        }

        private void UpdateInfo()
        {
            try 
            {
               var fi = new FileInfo(FullPath);
               long sizeVal = fi.Length;
               string suffix = "B";
               if (sizeVal > 1024) { sizeVal /= 1024; suffix = "KB"; }
               if (sizeVal > 1024) { sizeVal /= 1024; suffix = "MB"; }
               InfoText = $"{sizeVal} {suffix}";
            }
            catch { InfoText = "Unknown"; }
        }

        private byte[] _thumbnailPixels;

        private async void LoadMetadataAsync()
        {
            var ext = Path.GetExtension(FullPath).ToLowerInvariant();
            if (ext == ".obj" || ext == ".fbx" || ext == ".gltf" || ext == ".glb" || ext == ".blend")
            {
                // Use robust Assimp importer
                Metadata = await MonoGameEditor.Core.Assets.ModelImporter.LoadMetadataAsync(FullPath);
                
                if (Metadata != null && Metadata.PreviewVertices != null && Metadata.PreviewVertices.Count > 0)
                {
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        // Render pixels in background
                        _thumbnailPixels = MonoGameEditor.Core.Assets.ThumbnailRenderer.RenderPixels(Metadata, 128, 128);
                    });

                    // Create Bitmap on UI Thread
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (_thumbnailPixels != null)
                        {
                            Thumbnail = MonoGameEditor.Core.Assets.ThumbnailRenderer.CreateBitmap(_thumbnailPixels, 128, 128);
                        }
                    });
                }
            }
        }
    }
}
