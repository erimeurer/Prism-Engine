using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;

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

        /// <summary>
        /// Serialize current scene to JSON string (for Play mode state restore)
        /// </summary>
        public static string SerializeSceneToString()
        {
            var sceneData = new SceneData();
            
            foreach (var rootObj in SceneManager.Instance.RootObjects)
            {
                CaptureObject(rootObj, sceneData);
            }

            return JsonSerializer.Serialize(sceneData, _options);
        }

        /// <summary>
        /// Deserialize scene from JSON string (for Play mode state restore)
        /// </summary>
        public static void DeserializeSceneFromString(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            var sceneData = JsonSerializer.Deserialize<SceneData>(json, _options);
            if (sceneData == null) return;

            // Set flag to prevent components from reloading assets during deserialization
            _isLoadingScene = true;
            
            try
            {
                // Clear current scene
                SceneManager.Instance.RootObjects.Clear();
                SceneManager.Instance.SelectedObject = null;

                // Rebuild scene from data (reuse LoadScene logic)
                RestoreSceneFromData(sceneData);
            }
            finally
            {
                _isLoadingScene = false;
            }
        }

        /// <summary>
        /// Restore ONLY Transform properties from JSON (for Play mode restore without GPU bugs)
        /// This updates existing GameObjects without destroying/recreating them
        /// </summary>
        public static void RestoreScenePropertiesFromString(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            var sceneData = JsonSerializer.Deserialize<SceneData>(json, _options);
            if (sceneData == null) return;

            // Create a map of ID -> saved transform data
            var transformData = new Dictionary<Guid, (Vector3 pos, Vector3 rot, Vector3 scale)>();
            foreach (var objData in sceneData.Objects)
            {
                transformData[objData.Id] = (
                    objData.LocalPosition,
                    objData.LocalRotation,
                    objData.LocalScale
                );
            }

            // Recursively update transforms of existing GameObjects
            RestoreTransformsRecursive(SceneManager.Instance.RootObjects, transformData);
        }

        private static void RestoreTransformsRecursive(
            System.Collections.ObjectModel.ObservableCollection<GameObject> objects,
            Dictionary<Guid, (Vector3 pos, Vector3 rot, Vector3 scale)> transformData)
        {
            foreach (var obj in objects)
            {
                if (transformData.TryGetValue(obj.Id, out var data))
                {
                    obj.Transform.LocalPosition = data.pos;
                    obj.Transform.LocalRotation = data.rot;
                    obj.Transform.LocalScale = data.scale;
                }
                
                // Recurse into children
                RestoreTransformsRecursive(obj.Children, transformData);
            }
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
                    if (!prop.CanRead || !prop.CanWrite) continue;
                    if (prop.Name == "ComponentName" || prop.Name == "GameObject") continue;

                    try
                    {
                        var value = prop.GetValue(component);
                        if (value != null)
                        {
                            if (typeof(GameObject).IsAssignableFrom(prop.PropertyType))
                            {
                                var targetGo = value as GameObject;
                                compData.Properties[prop.Name] = $"\"GUID:{targetGo?.Id.ToString() ?? "null"}\"";
                            }
                            else if (typeof(Component).IsAssignableFrom(prop.PropertyType))
                            {
                                var targetComp = value as Component;
                                compData.Properties[prop.Name] = $"\"GUID_COMP:{targetComp?.GameObject?.Id.ToString() ?? "null"}:{targetComp?.GetType().FullName ?? "null"}\"";
                            }
                            else
                            {
                                compData.Properties[prop.Name] = JsonSerializer.Serialize(value, _options);
                            }
                        }
                    }
                    catch { }
                }

                // Serialize public fields
                var fields = component.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    try
                    {
                        var value = field.GetValue(component);
                        if (value != null)
                        {
                            if (typeof(GameObject).IsAssignableFrom(field.FieldType))
                            {
                                var targetGo = value as GameObject;
                                compData.Properties[field.Name] = $"\"GUID:{targetGo?.Id.ToString() ?? "null"}\"";
                            }
                            else if (typeof(Component).IsAssignableFrom(field.FieldType))
                            {
                                var targetComp = value as Component;
                                compData.Properties[field.Name] = $"\"GUID_COMP:{targetComp?.GameObject?.Id.ToString() ?? "null"}:{targetComp?.GetType().FullName ?? "null"}\"";
                            }
                            else
                            {
                                compData.Properties[field.Name] = JsonSerializer.Serialize(value, _options);
                            }
                        }
                    }
                    catch { }
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
            // Show loading overlay
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            mainWindow?.ShowLoadingOverlay("Loading scene...");
            
            try
            {
                if (!File.Exists(filePath)) return;

                string json = File.ReadAllText(filePath);
                var sceneData = JsonSerializer.Deserialize<SceneData>(json, _options);

                if (sceneData == null) return;

                // CRITICAL FIX: Clear model cache to prevent bone corruption
                // Cache shares same BoneData objects between Scene and Game tabs
                MonoGameEditor.Core.Assets.ModelImporter.ClearCache();

                // Set flag to prevent components from reloading assets during deserialization
                _isLoadingScene = true;
                
                try
                {
                    // Clear current scene
                    SceneManager.Instance.RootObjects.Clear();
                    SceneManager.Instance.SelectedObject = null;

                    RestoreSceneFromData(sceneData);
                }
                finally
                {
                    // Always reset flag when done loading
                    _isLoadingScene = false;
                }
            }
            finally
            {
                // Hide loading overlay
                mainWindow?.HideLoadingOverlay();
            }
        }

        /// <summary>
        /// Restore scene from SceneData (shared by LoadScene and DeserializeSceneFromString)
        /// </summary>
        private static void RestoreSceneFromData(SceneData sceneData)
        {
            // 1. Recreate Objects from Data
            var idToObjMap = new Dictionary<Guid, GameObject>();
            var parentIds = new Dictionary<Guid, Guid>();
            
            // Track object references for post-load resolution
            var pendingReferences = new List<(object Owner, System.Reflection.MemberInfo Member, string RefData)>();

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
                
                MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[SceneLoader] Loaded {go.Name} (ID: {go.Id}) at position {objData.LocalPosition}");

                // Re-add components
                foreach (var compData in objData.Components)
                {
                    // Try to create instance
                    Component? comp = null;
                    
                    // First try normal Type.GetType
                    Type? type = Type.GetType(compData.TypeName);
                    
                    // If that fails and it looks like a user script, try ScriptManager's compiled assembly
                    if (type == null && !compData.TypeName.StartsWith("MonoGameEditor."))
                    {
                        MonoGameEditor.ViewModels.ConsoleViewModel.LogInfo($"[SceneLoader] Type.GetType failed for '{compData.TypeName}', trying ScriptManager assembly");
                        type = Core.ScriptManager.Instance.GetCompiledType(compData.TypeName);
                    }
                    
                    if (type != null && Activator.CreateInstance(type) is Component c)
                    {
                        comp = c;
                        MonoGameEditor.ViewModels.ConsoleViewModel.LogInfo($"[SceneLoader] Created instance of {type.Name}");
                    }
                    else
                    {
                        MonoGameEditor.ViewModels.ConsoleViewModel.LogWarning($"[SceneLoader] Failed to create component of type '{compData.TypeName}'");
                    }
                    
                    if (comp != null)
                    {
                        var componentType = comp.GetType();
                        
                        // Restore properties and fields
                        foreach (var kvp in compData.Properties)
                        {
                            var propInfo = componentType.GetProperty(kvp.Key);
                            var fieldInfo = componentType.GetField(kvp.Key);
                            
                            Type? memberType = propInfo?.PropertyType ?? fieldInfo?.FieldType;
                            System.Reflection.MemberInfo? member = (System.Reflection.MemberInfo?)propInfo ?? fieldInfo;

                            if (member != null)
                            {
                                // SPECIAL HANDLING: Check if it's a GUID reference
                                string rawJson = kvp.Value.Trim('"');
                                if (rawJson.StartsWith("GUID:"))
                                {
                                    pendingReferences.Add((comp, member, rawJson));
                                }
                                else if (rawJson.StartsWith("GUID_COMP:"))
                                {
                                    pendingReferences.Add((comp, member, rawJson));
                                }
                                else
                                {
                                    try
                                    {
                                        // Deserialize value from JSON
                                        var value = JsonSerializer.Deserialize(kvp.Value, memberType, _options);
                                        if (propInfo != null && propInfo.CanWrite) propInfo.SetValue(comp, value);
                                        else if (fieldInfo != null) fieldInfo.SetValue(comp, value);
                                        
                                        System.Diagnostics.Debug.WriteLine($"Restored {componentType.Name}.{kvp.Key} = {value}");
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Failed to restore property/field {kvp.Key}: {ex.Message}");
                                    }
                                }
                            }
                        }

                        go.AddComponent(comp);
                        
                        // Reset started flag for ScriptComponents
                        if (comp is Core.Components.ScriptComponent script)
                        {
                            script.ResetStartedFlag();
                        }
                        
                        // Log when adding component
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[SceneLoader] Added {componentType.Name} to {go.Name} (ID: {go.Id})");
                        
                        // Call OnComponentAdded if it exists (for re-initialization after deserialization)
                        var onAddedMethod = type.GetMethod("OnComponentAdded");
                        if (onAddedMethod != null)
                        {
                            onAddedMethod.Invoke(comp, null);
                        }
                    }
                    else
                    {
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[SceneLoader] WARNING: Could not create instance of {compData.TypeName}");
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
            
            // 3. Initialize components that need post-load setup
            // CRITICAL: Wait a moment to ensure ALL GameObjects are in the scene before initializing
            System.Threading.Thread.Sleep(100); // Small delay to ensure hierarchy is stable
            
            MonoGameEditor.ViewModels.ConsoleViewModel.LogInfo($"[SceneLoader] Initializing {idToObjMap.Count} objects post-load");
            
            foreach (var obj in idToObjMap.Values)
            {
                foreach (var component in obj.Components)
                {
                    if (component is ModelRendererComponent renderer)
                    {
                        MonoGameEditor.ViewModels.ConsoleViewModel.LogInfo($"[SceneLoader] Reinitializing renderer for {obj.Name}");
                        _ = renderer.InitializeAfterSceneLoad();
                    }
                }
            }
            
            // 4. Resolve Object References
            MonoGameEditor.ViewModels.ConsoleViewModel.LogInfo($"[SceneLoader] Resolving {pendingReferences.Count} object references");
            foreach (var (owner, member, refData) in pendingReferences)
            {
                try
                {
                    if (refData.StartsWith("GUID:"))
                    {
                        string guidStr = refData.Substring(5);
                        if (guidStr != "null" && Guid.TryParse(guidStr, out Guid targetId))
                        {
                            if (idToObjMap.TryGetValue(targetId, out var targetGo))
                            {
                                SetMemberValue(owner, member, targetGo);
                            }
                        }
                    }
                    else if (refData.StartsWith("GUID_COMP:"))
                    {
                        string[] parts = refData.Substring(10).Split(':');
                        if (parts.Length >= 2 && parts[0] != "null" && Guid.TryParse(parts[0], out Guid targetGoId))
                        {
                            if (idToObjMap.TryGetValue(targetGoId, out var targetGo))
                            {
                                string compType = parts[1];
                                var targetComp = targetGo.Components.FirstOrDefault(c => c.GetType().FullName == compType);
                                SetMemberValue(owner, member, targetComp);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MonoGameEditor.ViewModels.ConsoleViewModel.LogWarning($"[SceneLoader] Failed to resolve reference {member.Name}: {ex.Message}");
                }
            }
        }

        private static void SetMemberValue(object owner, System.Reflection.MemberInfo member, object? value)
        {
            if (member is System.Reflection.PropertyInfo prop)
            {
                if (prop.CanWrite) prop.SetValue(owner, value);
            }
            else if (member is System.Reflection.FieldInfo field)
            {
                field.SetValue(owner, value);
            }
        }
    
    // Flag to prevent asset reloading during scene deserialization
    private static bool _isLoadingScene = false;
    
    /// <summary>
    /// Returns true if we're currently loading a scene (used to prevent asset reloading)
    /// </summary>
    public static bool IsLoadingScene => _isLoadingScene;
}
}
