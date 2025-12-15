using System;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;

namespace MonoGameEditor.Core
{
    /// <summary>
    /// Manages the current scene and its GameObjects
    /// </summary>
    public class SceneManager
    {
        private static SceneManager? _instance;
        public static SceneManager Instance => _instance ??= new SceneManager();

        public event Action? SelectionChanged;

        /// <summary>
        /// Root-level GameObjects in the scene
        /// </summary>
        public ObservableCollection<GameObject> RootObjects { get; } = new ObservableCollection<GameObject>();

        /// <summary>
        /// Currently selected GameObject
        /// </summary>
        private GameObject? _selectedObject;
        public GameObject? SelectedObject
        {
            get => _selectedObject;
            set
            {
                if (_selectedObject == value) return;

                if (_selectedObject != null)
                    _selectedObject.IsSelected = false;
                
                _selectedObject = value;
                
                if (_selectedObject != null)
                    _selectedObject.IsSelected = true;
                
                SelectionChanged?.Invoke();
            }
        }

        public SceneManager()
        {
            CreateDefaultScene();
        }

        public void CreateDefaultScene()
        {
            RootObjects.Clear();
            SelectedObject = null;

            var mainCamera = new GameObject("Main Camera") { ObjectType = GameObjectType.Camera };
            mainCamera.Transform.Position = new Microsoft.Xna.Framework.Vector3(0, 5, 12);
            mainCamera.Transform.Rotation = new Microsoft.Xna.Framework.Vector3(-15, 0, 0); // Look down towards origin
            mainCamera.AddComponent(new Components.CameraComponent());
            
            // Sun (Directional Light)
            var directionalLight = new GameObject("Directional Light") { ObjectType = GameObjectType.Light };
            directionalLight.Transform.Position = new Microsoft.Xna.Framework.Vector3(0, 50, 0); // High up like the sun
            directionalLight.Transform.Rotation = new Microsoft.Xna.Framework.Vector3(50, -30, 0); // Angled down
            
            var lightComponent = new Components.LightComponent
            {
                LightType = Components.LightType.Directional,
                Color = new Color(255, 244, 214), // Warm sunlight (slightly yellow/orange)
                Intensity = 1.2f, // Bright like the sun
                CastShadows = true
            };
            directionalLight.AddComponent(lightComponent);
            
            RootObjects.Add(mainCamera);
            RootObjects.Add(directionalLight);
        }

        public GameObject CreateGameObject(string name = "GameObject", GameObject? parent = null)
        {
            var go = new GameObject(name);
            
            if (parent != null)
            {
                parent.AddChild(go);
            }
            else
            {
                RootObjects.Add(go);
            }
            
            return go;
        }

        public void DeleteGameObject(GameObject go)
        {
            if (go.Parent != null)
            {
                go.Parent.RemoveChild(go);
            }
            else
            {
                RootObjects.Remove(go);
            }
        }

        public void Reparent(GameObject child, GameObject? newParent)
        {
            if (child == newParent) return; // Cannot parent to self
            
            // Check for circular dependency (cannot parent to own child)
            var current = newParent;
            while (current != null)
            {
                if (current == child) return;
                current = current.Parent;
            }

            // 1. Capture current World Transform
            Microsoft.Xna.Framework.Matrix oldWorldMatrix = child.Transform.WorldMatrix;

            // Remove from old parent
            if (child.Parent != null)
            {
                child.Parent.RemoveChild(child);
            }
            else
            {
                RootObjects.Remove(child);
            }

            // Add to new parent
            if (newParent != null)
            {
                newParent.AddChild(child);
                // Don't force expand - let user control tree state
            }
            else
            {
                RootObjects.Add(child);
                child.SetParent(null); 
            }

            // 2. Calculate new Local Transform to preserve World Transform
            Microsoft.Xna.Framework.Matrix newLocalMatrix = oldWorldMatrix;
            if (newParent != null)
            {
                Microsoft.Xna.Framework.Matrix parentWorldInverse = Microsoft.Xna.Framework.Matrix.Invert(newParent.Transform.WorldMatrix);
                newLocalMatrix = oldWorldMatrix * parentWorldInverse;
            }

            // 3. Apply Translation directly (Simplest step)
            // We use Decompose to be safe about scale/rotation affecting position in matrix
            Microsoft.Xna.Framework.Vector3 scale;
            Microsoft.Xna.Framework.Quaternion rot;
            Microsoft.Xna.Framework.Vector3 pos;

            if (newLocalMatrix.Decompose(out scale, out rot, out pos))
            {
                child.Transform.LocalPosition = pos;
                child.Transform.LocalScale = scale;
                child.Transform.Rotation = ToEulerAngles(rot);
            }
        }

        private Microsoft.Xna.Framework.Vector3 ToEulerAngles(Microsoft.Xna.Framework.Quaternion q)
        {
            // Conversion from Quaternion to Euler (Degrees)
            // MonoGame Quaternion is X, Y, Z, W
            // Logic based on standard XNA quaternion to euler conversion
            float x, y, z;

            // X-axis (Pitch)
            double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            x = (float)Math.Atan2(sinr_cosp, cosr_cosp);

            // Y-axis (Yaw)
            double sinp = 2 * (q.W * q.Y - q.Z * q.X);
            // Manual clamp/copysign replacement
            if (sinp >= 1) y = (float)(Math.PI / 2);
            else if (sinp <= -1) y = (float)(-Math.PI / 2);
            else y = (float)Math.Asin(sinp);

            // Z-axis (Roll)
            double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            z = (float)Math.Atan2(siny_cosp, cosy_cosp);

            return new Microsoft.Xna.Framework.Vector3(MathHelper.ToDegrees(x), MathHelper.ToDegrees(y), MathHelper.ToDegrees(z));
        }
    }
}
