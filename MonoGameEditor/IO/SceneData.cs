using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGameEditor.Core;

namespace MonoGameEditor.IO
{
    // DTOs (Data Transfer Objects) for serialization to avoid cyclic references
    // and decoupling storage format from runtime classes.

    public class SceneData
    {
        public List<GameObjectData> Objects { get; set; } = new List<GameObjectData>();
    }

    public class GameObjectData
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "GameObject";
        public bool IsActive { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }
        public GameObjectType ObjectType { get; set; }
        public Guid? ParentId { get; set; } // Reference to parent by ID
        
        // Transform (Local)
        public Vector3 LocalPosition { get; set; }
        public Vector3 LocalRotation { get; set; }
        public Vector3 LocalScale { get; set; }

        public List<ComponentData> Components { get; set; } = new List<ComponentData>();
    }

    public class ComponentData
    {
        public string TypeName { get; set; } = "";
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }
}
