using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MonoGameEditor.Core
{
    /// <summary>
    /// Type of GameObject for icon display
    /// </summary>
    public enum GameObjectType
    {
        Default,
        Camera,
        Light,
        Mesh,
        Empty
    }

    /// <summary>
    /// Base class for all objects in the scene hierarchy (like Unity's GameObject)
    /// </summary>
    public class GameObject : INotifyPropertyChanged
    {
        private string _name = "GameObject";
        private bool _isActive = true;
        private bool _isExpanded = false;
        private bool _isSelected;
        private GameObject? _parent;
        private GameObjectType _objectType = GameObjectType.Default;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Guid Id { get; private set; } = Guid.NewGuid();
        
        public GameObject(string name = "GameObject")
        {
            _name = name;
            Transform.GameObject = this;
            Components.CollectionChanged += Components_CollectionChanged;
        }

        public GameObject(string name, Guid id) : this(name)
        {
            Id = id;
        }
        
        /// <summary>
        /// Transform component (Position, Rotation, Scale)
        /// </summary>
        public Transform Transform { get; } = new Transform();

        /// <summary>
        /// Components attached to this GameObject
        /// </summary>
        public ObservableCollection<Component> Components { get; } = new ObservableCollection<Component>();

        public void AddComponent(Component component)
        {
            component.GameObject = this;
            Components.Add(component);
            OnPropertyChanged(nameof(Components));
        }

        public void RemoveComponent(Component component)
        {
            if (component.CanRemove && Components.Remove(component))
            {
                component.GameObject = null;
                OnPropertyChanged(nameof(Components));
            }
        }

        public T? GetComponent<T>() where T : Component
        {
            foreach (var c in Components)
                if (c is T comp) return comp;
            return null;
        }

        public List<T> GetComponents<T>() where T : Component
        {
            var list = new List<T>();
            foreach (var c in Components)
                if (c is T comp) list.Add(comp);
            return list;
        }

        public Component? GetComponent(Type type)
        {
            foreach (var c in Components)
                if (type.IsAssignableFrom(c.GetType())) return c;
            return null;
        }

        public List<Component> GetComponents(Type type)
        {
            var list = new List<Component>();
            foreach (var c in Components)
                if (type.IsAssignableFrom(c.GetType())) list.Add(c);
            return list;
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public GameObjectType ObjectType
        {
            get => _objectType;
            set { _objectType = value; OnPropertyChanged(nameof(ObjectType)); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public GameObject? Parent
        {
            get => _parent;
            private set { _parent = value; OnPropertyChanged(nameof(Parent)); }
        }

        public ObservableCollection<GameObject> Children { get; } = new ObservableCollection<GameObject>();

        // Default constructor moved up to handle Id initialization correctly

        private void Components_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateObjectType();
        }

        private void UpdateObjectType()
        {
            // Determine type based on components
            var newType = GameObjectType.Default;

            bool hasCamera = false;
            bool hasLight = false;
            bool hasMesh = false;

            foreach (var comp in Components)
            {
                if (comp is Components.CameraComponent) hasCamera = true;
                else if (comp is Components.LightComponent) hasLight = true;
                // else if (comp is Components.MeshRenderer) hasMesh = true; // Assuming we have MeshRenderer
            }

            if (hasCamera) newType = GameObjectType.Camera;
            else if (hasLight) newType = GameObjectType.Light;
            else if (hasMesh) newType = GameObjectType.Mesh;
            else newType = GameObjectType.Default; // Default should look like a Cube

            if (ObjectType != newType)
            {
                ObjectType = newType;
            }
        }

        public void AddChild(GameObject child)
        {
            if (child.Parent != null)
            {
                child.Parent.Children.Remove(child);
            }
            else
            {
                // If it was a root object, remove it from the root list
                SceneManager.Instance.RootObjects.Remove(child);
            }
            child.Parent = this;
            Children.Add(child);
        }

        public void RemoveChild(GameObject child)
        {
            if (Children.Remove(child))
            {
                child.Parent = null;
            }
        }

        public void SetParent(GameObject? newParent)
        {
            if (Parent != null)
            {
                Parent.Children.Remove(this);
            }
            
            Parent = newParent;
            
            if (newParent != null)
            {
                newParent.Children.Add(this);
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
