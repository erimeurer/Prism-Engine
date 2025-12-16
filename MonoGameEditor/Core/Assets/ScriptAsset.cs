using System;

namespace MonoGameEditor.Core.Assets
{
    /// <summary>
    /// Represents metadata for a discovered script asset
    /// </summary>
    public class ScriptAsset
    {
        /// <summary>
        /// Name of the script (class name)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Full path to the script file
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Compiled type (null if compilation failed)
        /// </summary>
        public Type? CompiledType { get; set; }

        /// <summary>
        /// Whether the script compiled successfully
        /// </summary>
        public bool IsCompiled => CompiledType != null;

        /// <summary>
        /// Compilation error message (if any)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Full type name including namespace
        /// </summary>
        public string? FullTypeName => CompiledType?.FullName;
    }
}
