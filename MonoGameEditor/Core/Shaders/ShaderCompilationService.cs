using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MonoGameEditor.Core;

namespace MonoGameEditor.Core.Shaders;

/// <summary>
/// Unity-style automatic shader compilation service
/// Monitors .fx files and auto-compiles them to .mgfxo
/// </summary>
public class ShaderCompilationService
{
    private readonly string _projectPath;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly HashSet<string> _compilingShaders = new();
    
    public ShaderCompilationService(string projectPath)
    {
        _projectPath = projectPath;
    }
    
    /// <summary>
    /// Start monitoring for shader changes
    /// </summary>
    public void StartMonitoring()
    {
        Logger.Log("[ShaderCompiler] Starting automatic shader compilation service...");
        
        // Compile all existing shaders on startup
        CompileAllShadersAsync();
        
        // Watch Content/Shaders folder (built-in shaders)
        var contentShadersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Shaders");
        if (Directory.Exists(contentShadersPath))
        {
            WatchDirectory(contentShadersPath);
        }
        
        // Watch project Assets folder (user shaders)
        if (!string.IsNullOrEmpty(_projectPath))
        {
            var assetsPath = Path.Combine(_projectPath, "Assets");
            if (Directory.Exists(assetsPath))
            {
                WatchDirectory(assetsPath);
            }
        }
    }
    
    private void WatchDirectory(string path)
    {
        var watcher = new FileSystemWatcher(path)
        {
            Filter = "*.fx",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        
        watcher.Changed += OnShaderFileChanged;
        watcher.Created += OnShaderFileChanged;
        watcher.Renamed += OnShaderFileRenamed;
        
        _watchers.Add(watcher);
        Logger.Log($"[ShaderCompiler] Watching: {path}");
    }
    
    private void OnShaderFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: ignore if already compiling this shader
        lock (_compilingShaders)
        {
            if (_compilingShaders.Contains(e.FullPath))
                return;
            _compilingShaders.Add(e.FullPath);
        }
        
        // Small delay to ensure file is fully written
        Task.Delay(100).ContinueWith(_ => CompileShaderAsync(e.FullPath));
    }
    
    private void OnShaderFileRenamed(object sender, RenamedEventArgs e)
    {
        CompileShaderAsync(e.FullPath);
    }
    
    /// <summary>
    /// Compile all .fx shaders in project and Content folders
    /// </summary>
    private async void CompileAllShadersAsync()
    {
        await Task.Run(() =>
        {
            var shadersToCompile = new List<string>();
            
            // Find all .fx files in Content/Shaders
            var contentShadersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Shaders");
            if (Directory.Exists(contentShadersPath))
            {
                shadersToCompile.AddRange(Directory.GetFiles(contentShadersPath, "*.fx", SearchOption.AllDirectories));
            }
            
            // Find all .fx files in project Assets
            if (!string.IsNullOrEmpty(_projectPath))
            {
                var assetsPath = Path.Combine(_projectPath, "Assets");
                if (Directory.Exists(assetsPath))
                {
                    shadersToCompile.AddRange(Directory.GetFiles(assetsPath, "*.fx", SearchOption.AllDirectories));
                }
            }
            
            if (shadersToCompile.Count > 0)
            {
                Logger.Log($"[ShaderCompiler] Found {shadersToCompile.Count} shader(s) to compile");
                
                foreach (var shaderPath in shadersToCompile)
                {
                    var mgfxoPath = Path.ChangeExtension(shaderPath, ".mgfxo");
                    
                    // Only compile if .mgfxo doesn't exist or is older than .fx
                    if (!File.Exists(mgfxoPath) || 
                        File.GetLastWriteTime(shaderPath) > File.GetLastWriteTime(mgfxoPath))
                    {
                        CompileShader(shaderPath);
                    }
                }
            }
        });
    }
    
    private async void CompileShaderAsync(string fxPath)
    {
        await Task.Run(() => CompileShader(fxPath));
        
        // Remove from compiling set
        lock (_compilingShaders)
        {
            _compilingShaders.Remove(fxPath);
        }
    }
    
    private void CompileShader(string fxPath)
    {
        try
        {
            var mgfxoPath = Path.ChangeExtension(fxPath, ".mgfxo");
            var shaderName = Path.GetFileName(fxPath);
            
            Logger.Log($"[ShaderCompiler] Compiling: {shaderName}");
            
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "mgfxc",
                Arguments = $"\"{fxPath}\" \"{mgfxoPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using (var process = System.Diagnostics.Process.Start(processInfo))
            {
                if (process == null)
                {
                    Logger.Log($"[ShaderCompiler] ❌ Failed to start mgfxc. Make sure it's in PATH.");

                    return;
                }
                
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                if (process.ExitCode == 0)
                {
                    Logger.Log($"[ShaderCompiler] ✅ {shaderName} compiled successfully");
                }
                else
                {
                    Logger.Log($"[ShaderCompiler] ❌ {shaderName} compilation failed (exit code {process.ExitCode})");
                    
                    if (!string.IsNullOrWhiteSpace(error))
                        Logger.Log($"[mgfxc ERROR] {error}");
                    
                    if (!string.IsNullOrWhiteSpace(output))
                        Logger.Log($"[mgfxc] {output}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"[ShaderCompiler] ❌ Exception: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Stop monitoring and cleanup
    /// </summary>
    public void StopMonitoring()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
        
        Logger.Log("[ShaderCompiler] Stopped monitoring");
    }
}
