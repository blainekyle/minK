using System;
using System.IO;
using System.Text.Json;
using mink.Models;

namespace mink.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;

        public SettingsService()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "minK",
                "settings.json"))
        {
        }

        public SettingsService(string settingsFilePath)
        {
            _settingsFilePath = settingsFilePath;
        }

        public string SettingsFilePath => _settingsFilePath;

        public AppSettings Load()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        loaded.Ollama ??= new OllamaConfig();
                        return loaded;
                    }
                }
                catch
                {
                    // Fallback to default settings if deserialization fails
                }
            }

            var settings = new AppSettings();
            Save(settings);
            return settings;
        }

        public void Save(AppSettings settings)
        {
            string? dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
    }
}
