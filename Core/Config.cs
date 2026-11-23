using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clip.Core
{
    public class Config
    {
        // Rename hook: the data folder follows the assembly name, not a hardcoded product name.
        public static string DataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Assembly.GetEntryAssembly().GetName().Name);

        static string FilePath => Path.Combine(DataDir, "config.json");

        static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            // Keep "+" literal in "Alt+V" so the file stays hand-editable.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public string Hotkey { get; set; } = "Alt+V";
        public string HotkeyPlain { get; set; } = "Ctrl+Shift+V";
        public int MaxItems { get; set; } = 200;
        public string Theme { get; set; } = "dark";
        // "12h" or "24h"; any other value behaves as 24h (normalised on load).
        public string TimeFormat { get; set; } = "24h";
        public bool StartOnLogin { get; set; } = true;
        public NotificationSettings Notification { get; set; } = new NotificationSettings();
        public List<string> IgnoreFormats { get; set; } = new List<string>
        {
            "Clipboard Viewer Ignore",
            "ExcludeClipboardContentFromMonitorProcessing"
        };
        public int MinTextLength { get; set; } = 3;
        public long MaxImageBytes { get; set; } = 5242880;
        // Per-item budget for the stored all-formats blobs (see ClipboardFormats.Snapshot).
        public long MaxItemBytes { get; set; } = 10485760;

        /// <summary>True when Load() had to create defaults (file missing or corrupt). Never persisted.</summary>
        [JsonIgnore]
        public bool WasNew { get; private set; }

        public class NotificationSettings
        {
            public bool Enabled { get; set; } = true;
            public int DurationMs { get; set; } = 3000;
            public int Width { get; set; } = 420;
            public int Height { get; set; } = 160;
            public string Position { get; set; } = "bottom-right";
            public int OffsetX { get; set; } = 16;
            public int OffsetY { get; set; } = 16;
            public bool ShowApp { get; set; } = true;
            public bool ShowTime { get; set; } = true;
            public int MaxLines { get; set; } = 3;
        }

        public static Config Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath), Options);
                    if (cfg != null)
                    {
                        // Hand-edited nulls would crash callers; fall back to the defaults.
                        cfg.Notification ??= new NotificationSettings();
                        cfg.IgnoreFormats ??= new Config().IgnoreFormats;
                        if (cfg.TimeFormat != "12h" && cfg.TimeFormat != "24h") cfg.TimeFormat = "24h";
                        return cfg;
                    }
                }
            }
            catch { }

            var fresh = new Config();
            fresh.WasNew = true;
            fresh.Save();
            return fresh;
        }

        public void Save()
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
    }
}
