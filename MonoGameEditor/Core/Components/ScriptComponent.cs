using Microsoft.Xna.Framework;

namespace MonoGameEditor.Core.Components
{
    /// <summary>
    /// Base class for all user scripts. Similar to Unity's MonoBehaviour.
    /// </summary>
    public abstract class ScriptComponent : Component
    {
        private bool _started = false;

        /// <summary>
        /// Called once when the script is first initialized
        /// </summary>
        public virtual void Start() { }

        /// <summary>
        /// Called every frame
        /// </summary>
        /// <param name="gameTime">Current game time</param>
        public virtual void Update(GameTime gameTime) { }

        /// <summary>
        /// Internal method to ensure Start() is only called once
        /// </summary>
        internal void InternalUpdate(GameTime gameTime)
        {
            if (!_started)
            {
                Start();
                _started = true;
            }
            Update(gameTime);
        }

        /// <summary>
        /// Helper property to easily access the transform
        /// </summary>
        protected Transform? transform => GameObject?.Transform;

        /// <summary>
        /// Helper method to get a component from the same GameObject
        /// </summary>
        protected T? GetComponent<T>() where T : Component
        {
            return GameObject?.GetComponent<T>();
        }

        /// <summary>
        /// Helper method to find a GameObject by name in the scene
        /// </summary>
        protected GameObject? FindGameObject(string name)
        {
            return FindGameObjectRecursive(SceneManager.Instance.RootObjects, name);
        }

        private GameObject? FindGameObjectRecursive(System.Collections.ObjectModel.ObservableCollection<GameObject> objects, string name)
        {
            foreach (var obj in objects)
            {
                if (obj.Name == name)
                    return obj;

                var found = FindGameObjectRecursive(obj.Children, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Reset the started flag when deserializing
        /// </summary>
        internal void ResetStartedFlag()
        {
            _started = false;
        }

        /// <summary>
        /// Get all public editable properties for this script
        /// </summary>
        public System.Collections.Generic.List<System.Reflection.PropertyInfo> GetEditableProperties()
        {
            var props = new System.Collections.Generic.List<System.Reflection.PropertyInfo>();
            var type = GetType();
            
            // Get all public instance properties
            foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                // Exclude inherited properties from Component and ScriptComponent
                if (prop.DeclaringType == typeof(Component) || prop.DeclaringType == typeof(ScriptComponent))
                    continue;
                    
                // Only include properties with both getter and setter
                if (prop.CanRead && prop.CanWrite)
                {
                    props.Add(prop);
                }
            }
            
            return props;
        }
    }
}
