using System;
using System.IO;
using Newtonsoft.Json;
using NLog;

namespace AI_AOI.Config
{
    public class SoftwareSettingsData
    {
        public string HOLLY_AOI_REPAIR_AIConnectionString { get; set; }
        public string HOLLY_AOI_REPAIRConnectionString { get; set; }
        public double ImageScale { get; set; }
        public int RectangleThickness { get; set; }
        public double FontSize { get; set; }
        public string HistoryDataRootPath { get; set; }
        public string ImageDataRootPath { get; set; }
        public string OffsetRootPath { get; set; }
        public string OffsetNgRootPath { get; set; }
        public string OffsetOkRootPath { get; set; }
        public string ShopfloorExportRootPath { get; set; }
        public int RepeatedComponentLockCount { get; set; }
        public string RepeatedComponentUnlockPassword { get; set; }
        public string AlarmTypesRedText { get; set; }

        public SoftwareSettingsData Clone()
        {
            return (SoftwareSettingsData)MemberwiseClone();
        }
    }

    public static class SoftwareSettingsManager
    {
        private static readonly Logger Logger = LogManager.GetLogger("debug");
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
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load software settings from {0}. Falling back to defaults.", ConfigPath);
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
                FontSize = 100,
                HistoryDataRootPath = @"E:\HLAOI_HISTORYDATA",
                ImageDataRootPath = @"E:\HLAOI_IMAGEDATA",
                OffsetRootPath = @"E:\HLAOI_OFFSET",
                OffsetNgRootPath = @"E:\HLAOI_OFFSET_NG",
                OffsetOkRootPath = @"E:\HLAOI_OFFSET_OK",
                ShopfloorExportRootPath = @"E:\HLAOI_SHOPFLOOR_EXPORTS_FOXCONN_VN",
                RepeatedComponentLockCount = 8,
                RepeatedComponentUnlockPassword = "1",
                AlarmTypesRedText = "Missing,Wrong Part,Polarity,Tombstone,Bridge"
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
            if (string.IsNullOrWhiteSpace(data.HistoryDataRootPath))
                data.HistoryDataRootPath = @"E:\HLAOI_HISTORYDATA";
            if (string.IsNullOrWhiteSpace(data.ImageDataRootPath))
                data.ImageDataRootPath = @"E:\HLAOI_IMAGEDATA";
            if (string.IsNullOrWhiteSpace(data.OffsetRootPath))
                data.OffsetRootPath = @"E:\HLAOI_OFFSET";
            if (string.IsNullOrWhiteSpace(data.OffsetNgRootPath))
                data.OffsetNgRootPath = @"E:\HLAOI_OFFSET_NG";
            if (string.IsNullOrWhiteSpace(data.OffsetOkRootPath))
                data.OffsetOkRootPath = @"E:\HLAOI_OFFSET_OK";
            if (string.IsNullOrWhiteSpace(data.ShopfloorExportRootPath))
                data.ShopfloorExportRootPath = @"E:\HLAOI_SHOPFLOOR_EXPORTS_FOXCONN_VN";
            if (data.RepeatedComponentLockCount <= 0)
                data.RepeatedComponentLockCount = 8;
            if (string.IsNullOrWhiteSpace(data.RepeatedComponentUnlockPassword))
                data.RepeatedComponentUnlockPassword = "1";
            if (string.IsNullOrWhiteSpace(data.AlarmTypesRedText))
                data.AlarmTypesRedText = "Missing,Wrong Part,Polarity,Tombstone,Bridge";
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
