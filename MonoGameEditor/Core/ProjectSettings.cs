using System;

namespace MonoGameEditor.Core
{
    public class ProjectSettings
    {
        public string ProjectName { get; set; } = "Untitled Project";
        public string Version { get; set; } = "1.0.0";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconPath { get; set; } = "";
        public string LastOpenedScene { get; set; } = "";
        public System.Collections.Generic.List<string> ScenesInBuild { get; set; } = new System.Collections.Generic.List<string>();
    }
}
