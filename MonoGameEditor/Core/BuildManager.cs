using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Xna.Framework;
using MonoGameEditor.Core;

namespace MonoGameEditor.Core
{
    public class BuildManager
    {
        private static BuildManager? _instance;
        public static BuildManager Instance => _instance ??= new BuildManager();

        public void BuildProject(string outputPath)
        {
            try
            {
                Logger.Log($"[Build] Starting professional build to: {outputPath}");

                // Get the directory where the editor executable is running from
                string editorDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // Try to find PrismPlayer.csproj relative to the editor
                string playerProject = Path.Combine(editorDir, "Runtime", "PrismPlayer.csproj");
                
                // If not found (running from bin), go up to the project root
                if (!File.Exists(playerProject))
                {
                    // Navigate up from bin/Debug/net8.0-windows to project root
                    string projectRoot = Path.GetFullPath(Path.Combine(editorDir, "..", "..", ".."));
                    playerProject = Path.Combine(projectRoot, "Runtime", "PrismPlayer.csproj");
                }

                if (!File.Exists(playerProject))
                {
                    throw new Exception($"Could not find player project template. Searched at: {playerProject}");
                }

                Logger.Log($"[Build] Using Player Template: {playerProject}");

                // 1. Prepare Output Directory
                if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

                // 2. Run Dotnet Publish
                // We use -c Release for optimized build
                // Using --self-contained true to ensure all dependencies are included
                string command = $"publish \"{playerProject}\" -c Release -o \"{outputPath}\" -r win-x64 --self-contained true -p:DefineConstants=RUNTIME_BUILD";
                Logger.Log($"[Build] Running command: dotnet {command}");

                var processInfo = new System.Diagnostics.ProcessStartInfo("dotnet", command)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    // Log output even if successful for debugging
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        Logger.Log($"[Build] Output: {output}");
                    }

                    if (process.ExitCode != 0)
                    {
                        Logger.LogError($"[Build] ❌ Dotnet publish failed with exit code {process.ExitCode}");
                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            Logger.LogError($"[Build] Error: {error}");
                        }
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            Logger.LogError($"[Build] Output: {output}");
                        }
                        return;
                    }
                    Logger.Log("[Build] ✅ Dotnet publish completed successfully.");
                }

                // 3. Copy User Assets to the build output
                string assetsPath = ProjectManager.Instance.AssetsPath;
                if (Directory.Exists(assetsPath))
                {
                    string targetAssets = Path.Combine(outputPath, "Assets");
                    CopyDirectory(assetsPath, targetAssets);
                    Logger.Log("[Build] User assets merged.");
                }

                // 4. Copy Project Settings
                string settingsPath = Path.Combine(ProjectManager.Instance.ProjectPath, "ProjectSettings.json");
                if (File.Exists(settingsPath))
                {
                    File.Copy(settingsPath, Path.Combine(outputPath, "ProjectSettings.json"), true);
                }

                Logger.Log($"[Build] ✅ SUCCESS! Game built at: {outputPath}");
                Logger.Log($"[Build] Run {Path.Combine(outputPath, "PrismPlayer.exe")} to play!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Build] ❌ Build failed: {ex.Message}");
            }
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        private void CompileScriptsToDll(string[] scriptFiles, string outputDllPath)
        {
            var syntaxTrees = scriptFiles.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f)).ToList();

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Vector3).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
                MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
                MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location)
            };

            var compilation = CSharpCompilation.Create(
                "DynamicScripts",
                syntaxTrees: syntaxTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var result = compilation.Emit(outputDllPath);

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (var diagnostic in failures)
                {
                    Logger.LogError($"{diagnostic.Id}: {diagnostic.GetMessage()}");
                }
                throw new Exception("Script compilation failed.");
            }
        }
    }
}
