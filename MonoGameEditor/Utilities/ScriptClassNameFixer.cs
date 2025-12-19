using System;
using System.IO;
using System.Text.RegularExpressions;

namespace MonoGameEditor.Utilities
{
    /// <summary>
    /// Utility to fix class names in script files to match their filenames
    /// </summary>
    public static class ScriptClassNameFixer
    {
        public static void FixAllScriptsInProject()
        {
            string projectPath = Core.ProjectManager.Instance.ProjectPath;
            if (string.IsNullOrEmpty(projectPath))
            {
                Core.Logger.Log("[ScriptFixer] No project loaded");
                return;
            }

            string scriptsFolder = Path.Combine(projectPath, "Assets", "Scripts");
            if (!Directory.Exists(scriptsFolder))
            {
                Core.Logger.Log($"[ScriptFixer] Scripts folder not found: {scriptsFolder}");
                return;
            }

            var scriptFiles = Directory.GetFiles(scriptsFolder, "*.cs", SearchOption.AllDirectories);
            Core.Logger.Log($"[ScriptFixer] Found {scriptFiles.Length} script file(s)");

            int fixedCount = 0;
            foreach (var filePath in scriptFiles)
            {
                if (FixScriptClassName(filePath))
                {
                    fixedCount++;
                }
            }

            Core.Logger.Log($"[ScriptFixer] Fixed {fixedCount} script(s). Restart editor to recompile.");
        }

        private static bool FixScriptClassName(string filePath)
        {
            try
            {
                string expectedClassName = Path.GetFileNameWithoutExtension(filePath);
                string content = File.ReadAllText(filePath);

                // Find current class name
                var classMatch = Regex.Match(content, @"\bclass\s+(\w+)\s*:\s*ScriptComponent");
                if (!classMatch.Success)
                {
                    Core.Logger.Log($"[ScriptFixer] Skipping {Path.GetFileName(filePath)} - not a ScriptComponent");
                    return false;
                }

                string currentClassName = classMatch.Groups[1].Value;
                if (currentClassName == expectedClassName)
                {
                    Core.Logger.Log($"[ScriptFixer] {Path.GetFileName(filePath)} - already correct");
                    return false;
                }

                Core.Logger.Log($"[ScriptFixer] Fixing {Path.GetFileName(filePath)}: {currentClassName} → {expectedClassName}");

                // Replace class declaration
                content = Regex.Replace(
                    content,
                    $@"\bclass\s+{Regex.Escape(currentClassName)}\b",
                    $"class {expectedClassName}");

                // Replace ComponentName if exists
                content = Regex.Replace(
                    content,
                    $@"ComponentName\s*=>\s*""{Regex.Escape(currentClassName)}""",
                    $"ComponentName => \"{expectedClassName}\"");

                // Also try with spaces in ComponentName
                content = Regex.Replace(
                    content,
                    $@"ComponentName\s*=>\s*""New Script""",
                    $"ComponentName => \"{expectedClassName}\"");

                File.WriteAllText(filePath, content);
                return true;
            }
            catch (Exception ex)
            {
                Core.Logger.Log($"[ScriptFixer] Error fixing {Path.GetFileName(filePath)}: {ex.Message}");
                return false;
            }
        }
    }
}
