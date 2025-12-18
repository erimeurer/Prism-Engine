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
            if (!IsEnabled) return;

            try
            {
                if (!_started)
                {
                    Start();
                    _started = true;
                }
                Update(gameTime);
            } catch (System.Exception ex)
            {
                System.Console.WriteLine($"Exception in ScriptComponent '{GetType().Name}': {ex}");
            }
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
        /// Get all public editable members (properties and fields) for this script
        /// </summary>
        public System.Collections.Generic.List<System.Reflection.MemberInfo> GetEditableMembers()
        {
            var members = new System.Collections.Generic.List<System.Reflection.MemberInfo>();
            var type = GetType();
            
            // Get all public instance members
            var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            
            // Get properties
            foreach (var prop in type.GetProperties(bindingFlags))
            {
                if (prop.DeclaringType == typeof(Component) || prop.DeclaringType == typeof(ScriptComponent))
                    continue;
                    
                if (prop.CanRead && prop.CanWrite)
                    members.Add(prop);
            }
            
            // Get fields
            foreach (var field in type.GetFields(bindingFlags))
            {
                if (field.DeclaringType == typeof(Component) || field.DeclaringType == typeof(ScriptComponent))
                    continue;
                    
                members.Add(field);
            }
            
            return members;
        }
    }
}
