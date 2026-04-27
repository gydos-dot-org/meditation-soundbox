using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MeditativeReader.Config
{
    /// <summary>
    /// User-configurable profile that replaces long command-line arguments.
    /// Stored in %APPDATA%\SndBx\profiles\default.json on Windows.
    /// </summary>
    public class SndBxProfile
    {
        public string VoiceId { get; set; } = "en_US-lessac-medium";
        public string VoicePath { get; set; } = @".\voices\en_US-lessac-medium.onnx";
        public string PiperPath { get; set; } = @".\tools\piper\piper\piper.exe";

        // Frequencies now use double for decimal precision (528.5, 432.07, etc.)
        public List<double> Frequencies { get; set; } = new() { 432.0 };

        public double Bpm { get; set; } = 60.0;
        public string Mode { get; set; } = "ambient";        // ambient, hybrid, rhythmic
        public string Strength { get; set; } = "subtle";     // subtle, noticeable, dominant
        public string Rhythm { get; set; } = "None";
        public string Flavor { get; set; } = "PurePad";

        public bool AutoRecord { get; set; } = true;
        public bool AutoStartMusic { get; set; } = false;
        public string? DefaultTextFile { get; set; }

        // Path where recordings are saved
        public string RecordingDirectory { get; set; } = @".\recordings";
    }

    public static class SndBxConfig
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SndBx", "profiles");

        private static readonly string DefaultPath = Path.Combine(ConfigDir, "default.json");

        public static SndBxProfile Load(string? profileName = null)
        {
            var path = profileName == null
                ? DefaultPath
                : Path.Combine(ConfigDir, $"{profileName}.json");

            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<SndBxProfile>(json) ?? new SndBxProfile();
            }

            // First run: create default
            var profile = new SndBxProfile();
            Save(profile, profileName);
            return profile;
        }

        public static void Save(SndBxProfile profile, string? profileName = null)
        {
            Directory.CreateDirectory(ConfigDir);
            var path = profileName == null
                ? DefaultPath
                : Path.Combine(ConfigDir, $"{profileName}.json");

            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
        }
    }
}
