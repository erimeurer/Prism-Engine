using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using MonoGameEditor.Core;

namespace MonoGameEditor.IO
{
    public static class SceneSerializer
    {
        private static JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true
        };

        public static void SaveScene(string filePath)
        {
            var sceneData = new SceneData();
            
            // Flatten hierarchy for serialization
            // We iterate over all Root objects and their children recursively
            foreach (var rootObj in SceneManager.Instance.RootObjects)
            {
                CaptureObject(rootObj, sceneData);
            }

            string json = JsonSerializer.Serialize(sceneData, _options);
            File.WriteAllText(filePath, json);
        }

        private static void CaptureObject(GameObject go, SceneData data)
        {
            var goData = new GameObjectData
            {
                Id = go.Id,
                Name = go.Name,
                IsActive = go.IsActive,
                IsExpanded = go.IsExpanded,
                IsSelected = go.IsSelected,
                ObjectType = go.ObjectType,
                ParentId = go.Parent?.Id,
                LocalPosition = go.Transform.LocalPosition,
                LocalRotation = go.Transform.LocalRotation,
                LocalScale = go.Transform.LocalScale
            };

            foreach (var component in go.Components)
            {
                var compData = new ComponentData
                {
                    TypeName = component.GetType().FullName ?? "Unknown"
                };

                // Serialize public properties
                var props = component.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var prop in props)
                {
                    // Skip properties we can't read or that are complex types we can't serialize easily
                    if (!prop.CanRead || !prop.CanWrite) continue;
                    
                    // Skip ComponentName and GameObject (runtime-only properties)
                    if (prop.Name == "ComponentName" || prop.Name == "GameObject") continue;

                    try
                    {
                        var value = prop.GetValue(component);
                        if (value != null)
                        {
                            // Convert to string for JSON serialization
                            compData.Properties[prop.Name] = JsonSerializer.Serialize(value, _options);
                        }
                    }
                    catch
                    {
                        // Skip properties that fail to serialize
                    }
                }

                goData.Components.Add(compData);
            }

            data.Objects.Add(goData);

            // Recurse children
            foreach (var child in go.Children)
            {
                CaptureObject(child, data);
            }
        }

        public static void LoadScene(string filePath)
        {
            if (!File.Exists(filePath)) return;

            string json = File.ReadAllText(filePath);
            var sceneData = JsonSerializer.Deserialize<SceneData>(json, _options);

            if (sceneData == null) return;

            // Clear current scene
            SceneManager.Instance.RootObjects.Clear();
            SceneManager.Instance.SelectedObject = null;

            // 1. Recreate Objects from Data
            var idToObjMap = new Dictionary<Guid, GameObject>();
            var parentIds = new Dictionary<Guid, Guid>();

            foreach (var objData in sceneData.Objects)
            {
                // We use a constructor that accepts ID if possible, or we assume ID is generated.
                // Since ID is readonly in GameObject, ensuring persistence requires either reflection or a dedicated constructor/setter.
                // For now, we unfortunately will get NEW IDs unless we refactor GameObject.
                // Let's refactor GameObject slightly to allow setting ID or use Reflection.
                
                var go = new GameObject(objData.Name);
                
                // Hack: Set ID via reflection since it's readonly
                var prop = typeof(GameObject).GetProperty("Id");
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(go, objData.Id);
                }
                else
                {
                    // Backing field?
                    // Or keep new ID? If we keep new ID, parenting links break!
                    // We must preserve ID.
                    System.Reflection.FieldInfo? field = typeof(GameObject).GetField("<Id>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        field.SetValue(go, objData.Id);
                    }
                }

                go.IsActive = objData.IsActive;
                go.IsExpanded = objData.IsExpanded;
                go.ObjectType = objData.ObjectType;
                
                go.Transform.LocalPosition = objData.LocalPosition;
                go.Transform.LocalRotation = objData.LocalRotation;
                go.Transform.LocalScale = objData.LocalScale;

                // Re-add components
                foreach (var compData in objData.Components)
                {
                    // Simple reflection to instantiate component
                    Type? type = Type.GetType(compData.TypeName);
                    if (type != null && Activator.CreateInstance(type) is Component comp)
                    {
                        // Restore properties
                        foreach (var kvp in compData.Properties)
                        {
                            var propInfo = type.GetProperty(kvp.Key);
                            if (propInfo != null && propInfo.CanWrite)
                            {
                                try
                                {
                                    // Deserialize value from JSON
                                    var value = JsonSerializer.Deserialize(kvp.Value, propInfo.PropertyType, _options);
                                    propInfo.SetValue(comp, value);
                                    System.Diagnostics.Debug.WriteLine($"Restored {type.Name}.{kvp.Key} = {value}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to restore property {kvp.Key}: {ex.Message}");
                                }
                            }
                        }

                        go.AddComponent(comp);
                        
                        // Call OnComponentAdded if it exists (for re-initialization after deserialization)
                        var onAddedMethod = type.GetMethod("OnComponentAdded");
                        onAddedMethod?.Invoke(comp, null);
                    }
                }

                idToObjMap[objData.Id] = go;
                
                if (objData.ParentId.HasValue)
                {
                    parentIds[objData.Id] = objData.ParentId.Value;
                }
                else
                {
                    // It's a root object (for now)
                    SceneManager.Instance.RootObjects.Add(go);
                }
            }

            // 2. Restore Hierarchy
            foreach (var kvp in parentIds)
            {
                Guid childId = kvp.Key;
                Guid parentId = kvp.Value;

                if (idToObjMap.TryGetValue(childId, out var child) && 
                    idToObjMap.TryGetValue(parentId, out var parent))
                {
                    // This moves it from RootObjects (if added there) to Parent's children
                    // However, we added strict logic in Reparent/AddChild.
                    // Let's use SceneManager.Instance.Reparent to be safe? 
                    // No, direct manipulation is faster here, but we need to remove from Root if we added it there.
                    
                    // Actually, in the loop above I added to RootObjects ONLY if ParentId is null.
                    // So here I just need to add to parent.
                    parent.AddChild(child);
                }
            }
        }
    }
}
