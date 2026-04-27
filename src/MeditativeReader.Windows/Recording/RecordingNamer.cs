using System;

namespace MeditativeReader.Recording
{
    public static class RecordingNamer
    {
        /// <summary>
        /// Format: yYYYYmMMdDD_aHHMM_SndBx_FILENAME.wav
        /// Example: y2026m04d27_p0423_SndBx_focus.wav
        /// </summary>
        public static string GenerateFilename(string? userSuffix = null)
        {
            var now = DateTime.Now;
            var amPm = now.Hour >= 12 ? "p" : "a";
            var hour12 = now.Hour > 12 ? now.Hour - 12 : (now.Hour == 0 ? 12 : now.Hour);

            var suffix = string.IsNullOrWhiteSpace(userSuffix) ? "default" : userSuffix;

            // yYYYYmMMdDD_aHHMM
            return $"y{now:yyyy}m{now:MM}d{now:dd}_{amPm}{hour12:00}{now:mm}_SndBx_{suffix}.wav";
        }
    }
}
