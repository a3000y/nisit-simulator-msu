#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.DevTools
{
    // Command-line safety is independent of the optional in-game DevProfile.
    public static class MPTestProfile
    {
        public static readonly string DirectoryPath = Argument("-mptest");
        public static readonly string Id = Argument("-mptest-id") ?? "X";
        public static bool Enabled => !string.IsNullOrEmpty(DirectoryPath);
        public static string SavePath => Enabled ? Path.Combine(DirectoryPath, Id + "_save.json") : null;
        static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == key) return args[i + 1];
            return null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Boot()
        {
            if (!Enabled) return;
            Directory.CreateDirectory(DirectoryPath);
            Enforce();
        }
        public static void Enforce()
        {
            if (!Enabled) return;
            SaveSystem.DevGuard = true;
            SaveSystem.DevPathOverride = SavePath;
            SaveSystem.DevReadOverride = null;
        }
    }
}
#endif
