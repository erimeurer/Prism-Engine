using System;
using System.IO;
using System.Text.Json;

namespace MonoGameEditor.Core
{
    public class GlobalEditorSettings
    {
        public string LastProjectPath { get; set; } = string.Empty;

        private static string SettingsFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonoGameEditor", "global_settings.json");

        public static GlobalEditorSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<GlobalEditorSettings>(json) ?? new GlobalEditorSettings();
                }
            }
            catch { }
            return new GlobalEditorSettings();
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }
    }
}
