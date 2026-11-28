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
        // An explicit APPDATA wins: .NET's known-folder lookup ignores it, which made an isolated test
        // profile impossible. In a normal session APPDATA is already that same real path.
        public static string DataDir
        {
            get
            {
                string root = Environment.GetEnvironmentVariable("APPDATA");
                if (string.IsNullOrEmpty(root))
                    root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(root, Assembly.GetEntryAssembly().GetName().Name);
            }
        }

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
        // Lines of text shown per card in the popup list.
        public int PreviewLines { get; set; } = 10;
        // When false, image clips live only in the Images tab; the All/history list hides them.
        public bool ShowImagesInAll { get; set; } = true;
        public string Theme { get; set; } = "light";
        // Web shell zoom percent; WebView2 ZoomFactor = UiZoom / 100 (80–150, clamped by the host).
        public int UiZoom { get; set; } = 100;
        // "12h" or "24h"; any other value behaves as 24h (normalised on load).
        public string TimeFormat { get; set; } = "24h";
        // No StartOnLogin here on purpose: the Startup-folder shortcut IS the state (Autostart.IsEnabled),
        // and a stored copy would drift the moment the user deletes the shortcut by hand.
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
        // Advisory ceiling for clip.db + its WAL: over it the list asks once per run to clear space.
        // Copies are never refused (owner decision 25 Nov): the limit advises, it does not block.
        public long MaxStorageBytes { get; set; } = 1073741824;

        /// <summary>True when Load() had to create defaults (file missing or corrupt). Never persisted.</summary>
        [JsonIgnore]
        public bool WasNew { get; private set; }

        public class NotificationSettings
        {
            public bool Enabled { get; set; } = true;
            public int DurationMs { get; set; } = 3000;
            /// <summary>Maximum width in DIP; a short clip gets a narrower toast.</summary>
            public int Width { get; set; } = 420;
            /// <summary>Maximum height in DIP; content decides the height below it.</summary>
            public int Height { get; set; } = 160;
            public string Position { get; set; } = "bottom-right";
            public int OffsetX { get; set; } = 16;
            public int OffsetY { get; set; } = 16;
            public bool ShowApp { get; set; } = true;
            public bool ShowTime { get; set; } = true;
            public int MaxLines { get; set; } = 3;
            /// <summary>Toast size 0-10: level N scales the Width/Height caps to (1 + N/10)x.</summary>
            public int Size { get; set; } = 0;
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
                        if (cfg.Notification.Size < 0 || cfg.Notification.Size > 10) cfg.Notification.Size = 0;
                        return cfg;
                    }
                }
            }
            catch { } // unreadable or corrupt config: fresh defaults below, the old file is overwritten by Save()

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
