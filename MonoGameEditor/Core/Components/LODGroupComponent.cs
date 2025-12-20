using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace MonoGameEditor.Core.Components
{
    /// <summary>
    /// Represents a single Level of Detail (LOD)
    /// </summary>
    public class LODLevel : System.ComponentModel.INotifyPropertyChanged
    {
        private GameObject? _targetObject;
        private Guid _targetObjectId;
        private float _distanceThreshold;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [JsonIgnore]
        public GameObject? TargetObject 
        { 
            get => _targetObject; 
            set 
            { 
                _targetObject = value; 
                if (value != null) _targetObjectId = value.Id;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TargetObject))); 
            }
        }

        public Guid TargetObjectId
        {
            get => _targetObjectId;
            set { _targetObjectId = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TargetObjectId))); }
        }
        
        public float DistanceThreshold 
        { 
            get => _distanceThreshold; 
            set { _distanceThreshold = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(DistanceThreshold))); }
        }

        public LODLevel() { } // Required for JSON

        public LODLevel(GameObject? target, float distance)
        {
            TargetObject = target;
            _distanceThreshold = distance;
        }

        public void ResolveReference(Dictionary<Guid, GameObject> idMap)
        {
            if (_targetObjectId != Guid.Empty && idMap.TryGetValue(_targetObjectId, out var go))
            {
                _targetObject = go;
            }
        }
    }

    /// <summary>
    /// Manages geometric complexity based on distance from the camera.
    /// </summary>
    public class LODGroupComponent : Component
    {
        private List<LODLevel> _levels = new List<LODLevel>();
        private int _currentLOD = -1;
        private bool _forceUpdate = true;
        private bool _referencesResolved = false;

        public override string ComponentName => "LOD Group";

        public List<LODLevel> Levels
        {
            get => _levels;
            set { _levels = value; OnPropertyChanged(nameof(Levels)); _forceUpdate = true; _referencesResolved = false; }
        }

        public void AddLevel()
        {
            _levels.Add(new LODLevel(null, 100f));
            OnPropertyChanged(nameof(Levels));
            _forceUpdate = true;
        }

        public void RemoveLevel(LODLevel level)
        {
            if (_levels.Remove(level))
            {
                OnPropertyChanged(nameof(Levels));
                _forceUpdate = true;
            }
        }

        // Called via reflection by SceneSerializer
        public void OnComponentAdded()
        {
            _referencesResolved = false; // Trigger resolution on next update
            _forceUpdate = true;
        }

        public void UpdateLOD(Vector3 cameraPosition)
        {
            if (GameObject == null || _levels.Count == 0) return;

            // Resolve references if not done yet
            if (!_referencesResolved)
            {
                ResolveReferences();
            }

            float distance = Vector3.Distance(cameraPosition, GameObject.Transform.Position);
            
            // Find appropriate LOD level
            int selectedLOD = -1;
            
            // Assume levels are sorted by distance threshold (ascending)
            for (int i = 0; i < _levels.Count; i++)
            {
                if (distance < _levels[i].DistanceThreshold)
                {
                    selectedLOD = i;
                    break;
                }
            }

            if (selectedLOD != _currentLOD || _forceUpdate)
            {
                _currentLOD = selectedLOD;
                _forceUpdate = false;

                for (int i = 0; i < _levels.Count; i++)
                {
                    if (_levels[i].TargetObject != null)
                    {
                        _levels[i].TargetObject.IsActive = (i == _currentLOD);
                    }
                }
            }
        }

        private void ResolveReferences()
        {
            // Build a quick map of the scene hierarchy
            var idMap = new Dictionary<Guid, GameObject>();
            BuildIdMapRecursively(SceneManager.Instance.RootObjects, idMap);

            foreach (var level in _levels)
            {
                level.ResolveReference(idMap);
            }

            _referencesResolved = true;
        }

        private void BuildIdMapRecursively(System.Collections.ObjectModel.ObservableCollection<GameObject> objects, Dictionary<Guid, GameObject> map)
        {
            foreach (var obj in objects)
            {
                map[obj.Id] = obj;
                BuildIdMapRecursively(obj.Children, map);
            }
        }
    }
}
