using MonoGameEditor.Core;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MonoGameEditor.ViewModels
{
    public class HierarchyViewModel : ToolViewModel
    {
        public ObservableCollection<GameObject> RootObjects => SceneManager.Instance.RootObjects;

        public GameObject? SelectedObject
        {
            get => SceneManager.Instance.SelectedObject;
            set
            {
                SceneManager.Instance.SelectedObject = value;
                OnPropertyChanged(nameof(SelectedObject));
            }
        }

        public ICommand CreateObjectCommand { get; }
        public ICommand DeleteObjectCommand { get; }

        public HierarchyViewModel() : base("Hierarchy")
        {
            CreateObjectCommand = new RelayCommand(_ => CreateNewObject());
            DeleteObjectCommand = new RelayCommand(_ => DeleteSelectedObject(), _ => SelectedObject != null);
        }

        private void CreateNewObject()
        {
            var newObj = SceneManager.Instance.CreateGameObject("New GameObject", SelectedObject);
            SelectedObject = newObj;
        }

        private void DeleteSelectedObject()
        {
            if (SelectedObject != null)
            {
                SceneManager.Instance.DeleteGameObject(SelectedObject);
                SelectedObject = null;
            }
        }
    }

    public class InspectorViewModel : ToolViewModel
    {
        private object? _selectedObject;
        public object? SelectedObject
        {
            get => _selectedObject;
            set { _selectedObject = value; OnPropertyChanged(); }
        }

        public InspectorViewModel() : base("Inspector") 
        {
            // Subscribe to selection changes
            SceneManager.Instance.SelectionChanged += () => 
            {
                SelectedObject = SceneManager.Instance.SelectedObject;
            };
        }
    }





    public class SceneViewModel : DocumentViewModel
    {
        public SceneViewModel() : base("Scene") { }
    }

    public class GameViewModel : DocumentViewModel
    {
        public GameViewModel() : base("Game") { }
    }
}
