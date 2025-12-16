using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using MonoGameEditor.ViewModels;
using JsonDocument = System.Text.Json.JsonDocument;

namespace MonoGameEditor.Views
{
    public partial class ScriptEditorView : UserControl
    {
        private ScriptEditorViewModel? _viewModel;
        private bool _isInitialized = false;

        public ScriptEditorView()
        {
            InitializeComponent();
            Loaded += ScriptEditorView_Loaded;
            DataContextChanged += ScriptEditorView_DataContextChanged;
        }

        private void ScriptEditorView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ScriptEditorViewModel vm && !string.IsNullOrEmpty(vm.ScriptPath))
            {
                _viewModel = vm;
                if (_isInitialized)
                {
                    LoadScriptContent(vm.ScriptPath);
                }
            }
        }

        private async void ScriptEditorView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) return;

            try
            {
                ConsoleViewModel.LogInfo("[ScriptEditor] Initializing WebView2...");
                await webView.EnsureCoreWebView2Async();
                
                // Load Monaco Editor HTML
                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "MonacoEditor.html");
                
                ConsoleViewModel.LogInfo($"[ScriptEditor] Looking for Monaco HTML at: {htmlPath}");
                
                if (File.Exists(htmlPath))
                {
                    ConsoleViewModel.LogInfo("[ScriptEditor] Monaco HTML found, navigating...");
                    webView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                }
                else
                {
                    ConsoleViewModel.LogError("[ScriptEditor] MonacoEditor.html not found at: " + htmlPath);
                    return;
                }

                // Listen for messages from JavaScript
                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

                _isInitialized = true;
                
                // If ViewModel already set, load the script
                if (_viewModel != null && !string.IsNullOrEmpty(_viewModel.ScriptPath))
                {
                    ConsoleViewModel.LogInfo($"[ScriptEditor] ViewModel ready, will load script after Monaco ready: {_viewModel.ScriptPath}");
                }
            }
            catch (Exception ex)
            {
                ConsoleViewModel.LogError($"[ScriptEditor] Failed to initialize WebView2: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to initialize code editor: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.WebMessageAsJson;
                ConsoleViewModel.LogInfo($"[ScriptEditor] Received message from Monaco: {message}");
                var json = System.Text.Json.JsonDocument.Parse(message);
                var root = json.RootElement;

                if (root.TryGetProperty("type", out var typeElement))
                {
                    string type = typeElement.GetString() ?? "";

                    switch (type)
                    {
                        case "ready":
                            ConsoleViewModel.LogInfo("[ScriptEditor] Monaco editor is ready!");
                            // Editor is ready, load script content
                            if (_viewModel != null && !string.IsNullOrEmpty(_viewModel.ScriptPath))
                            {
                                ConsoleViewModel.LogInfo($"[ScriptEditor] Loading script: {_viewModel.ScriptPath}");
                                LoadScriptContent(_viewModel.ScriptPath);
                            }
                            break;

                        case "contentChanged":
                            if (_viewModel != null)
                            {
                                _viewModel.IsDirty = true;
                            }
                            break;

                        case "cursorChanged":
                            if (_viewModel != null && root.TryGetProperty("line", out var line) && root.TryGetProperty("column", out var column))
                            {
                                _viewModel.CurrentLine = line.GetInt32();
                                _viewModel.CurrentColumn = column.GetInt32();
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleViewModel.LogError($"[ScriptEditor] Error processing web message: {ex.Message}");
            }
        }

        public void LoadScript(string scriptPath)
        {
            if (DataContext is ScriptEditorViewModel vm)
            {
                _viewModel = vm;
                _viewModel.ScriptPath = scriptPath;
                _viewModel.ScriptName = Path.GetFileName(scriptPath);

                if (_isInitialized)
                {
                    LoadScriptContent(scriptPath);
                }
            }
        }

        private async void LoadScriptContent(string scriptPath)
        {
            try
            {
                ConsoleViewModel.LogInfo($"[ScriptEditor] LoadScriptContent called for: {scriptPath}");
                
                if (!File.Exists(scriptPath))
                {
                    ConsoleViewModel.LogError($"[ScriptEditor] Script file not found: {scriptPath}");
                    return;
                }

                string content = await File.ReadAllTextAsync(scriptPath);
                ConsoleViewModel.LogInfo($"[ScriptEditor] Read {content.Length} characters from file");
                
                // Escape for JavaScript - use JSON serialization for better escaping
                string escapedContent = System.Text.Json.JsonSerializer.Serialize(content);
                
                string script = $"loadCode({escapedContent})";
                ConsoleViewModel.LogInfo($"[ScriptEditor] Executing script: {script.Substring(0, Math.Min(100, script.Length))}...");
                
                await webView.ExecuteScriptAsync(script);
                
                if (_viewModel != null)
                {
                    _viewModel.IsDirty = false;
                }
                
                ConsoleViewModel.LogInfo("[ScriptEditor] Script loaded successfully!");
            }
            catch (Exception ex)
            {
                ConsoleViewModel.LogError($"[ScriptEditor] Failed to load script: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public async void SaveScript()
        {
            try
            {
                if (_viewModel == null || string.IsNullOrEmpty(_viewModel.ScriptPath))
                    return;

                string result = await webView.ExecuteScriptAsync("getCode()");
                
                // Monaco now returns base64 encoded - remove JSON quotes first
                if (result.StartsWith("\"") && result.EndsWith("\""))
                {
                    result = result.Substring(1, result.Length - 2);
                }
                
                // Decode from base64
                byte[] data = Convert.FromBase64String(result);
                string content = System.Text.Encoding.UTF8.GetString(data);
                
                await File.WriteAllTextAsync(_viewModel.ScriptPath, content);
                
                _viewModel.IsDirty = false;
                ConsoleViewModel.LogInfo($"[ScriptEditor] Saved: {Path.GetFileName(_viewModel.ScriptPath)}");
                
                // Clear old errors before recompiling
                ConsoleViewModel.Instance.ClearErrors();
                
                // Auto-recompile scripts after saving
                ConsoleViewModel.LogInfo("[ScriptEditor] Recompiling scripts...");
                Core.ScriptManager.Instance.DiscoverAndCompileScripts();
            }
            catch (Exception ex)
            {
                ConsoleViewModel.LogError($"[ScriptEditor] Failed to save script: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to save script: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel?.IsDirty == true)
            {
                var result = System.Windows.MessageBox.Show(
                    "You have unsaved changes. Do you want to save before closing?",
                    "Unsaved Changes",
                    System.Windows.MessageBoxButton.YesNoCancel,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    SaveScript();
                }
                else if (result == System.Windows.MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            // Close the window/tab containing this control
            Window.GetWindow(this)?.Close();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SaveScript();
                e.Handled = true;
            }
        }
    }
}
