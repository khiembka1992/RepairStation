using System;
using System.IO;
using Newtonsoft.Json;

namespace AI_AOI.Config
{
    public class SoftwareSettingsData
    {
        public string HOLLY_AOI_REPAIR_AIConnectionString { get; set; }
        public string HOLLY_AOI_REPAIRConnectionString { get; set; }
        public double ImageScale { get; set; }
        public int RectangleThickness { get; set; }
        public double FontSize { get; set; }

        public SoftwareSettingsData Clone()
        {
            return (SoftwareSettingsData)MemberwiseClone();
        }
    }

    public static class SoftwareSettingsManager
    {
        private static readonly object Sync = new object();
        private static SoftwareSettingsData _current;

        public static string ConfigPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "SoftwareSettings.json");

        public static SoftwareSettingsData Current
        {
            get
            {
                EnsureLoaded();
                return _current;
            }
        }

        public static void EnsureLoaded()
        {
            lock (Sync)
            {
                if (_current != null) return;

                _current = LoadOrCreate();
            }
        }

        public static void Save()
        {
            lock (Sync)
            {
                EnsureLoaded();
                SaveInternal(_current);
            }
        }

        public static void Reload()
        {
            lock (Sync)
            {
                _current = LoadOrCreate();
            }
        }

        private static SoftwareSettingsData LoadOrCreate()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var parsed = JsonConvert.DeserializeObject<SoftwareSettingsData>(json);
                    if (parsed != null)
                    {
                        Normalize(parsed);
                        return parsed;
                    }
                }
            }
            catch
            {
                // fall through to defaults
            }

            var defaults = CreateDefaultSettings();
            Normalize(defaults);
            SaveInternal(defaults);
            return defaults;
        }

        private static SoftwareSettingsData CreateDefaultSettings()
        {
            return new SoftwareSettingsData
            {
                HOLLY_AOI_REPAIR_AIConnectionString = string.Empty,
                HOLLY_AOI_REPAIRConnectionString = string.Empty,
                ImageScale = 0.1,
                RectangleThickness = 5,
                FontSize = 100
            };
        }

        private static void Normalize(SoftwareSettingsData data)
        {
            if (data == null) return;
            if (string.IsNullOrWhiteSpace(data.HOLLY_AOI_REPAIR_AIConnectionString))
                data.HOLLY_AOI_REPAIR_AIConnectionString = string.Empty;
            if (string.IsNullOrWhiteSpace(data.HOLLY_AOI_REPAIRConnectionString))
                data.HOLLY_AOI_REPAIRConnectionString = string.Empty;
            if (data.ImageScale <= 0)
                data.ImageScale = 0.1;
            if (data.RectangleThickness <= 0)
                data.RectangleThickness = 1;
            if (data.FontSize <= 0)
                data.FontSize = 12;
        }

        private static void SaveInternal(SoftwareSettingsData data)
        {
            var folder = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
    }
}
