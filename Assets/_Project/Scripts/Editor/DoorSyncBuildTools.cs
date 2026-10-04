#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using NisitSimulator.Net;

namespace NisitSimulator.EditorTools
{
    public static class DoorSyncBuildTools
    {
        public static void EnsurePrefab()
        {
            const string path = "Assets/_Project/Resources/Net/LobbyState.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<DoorSyncManager>() == null) root.AddComponent<DoorSyncManager>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void BuildMPTest()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build_MPTest/NisitSimulator.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-result.json"),
                JsonUtility.ToJson(new Result { result = report.summary.result.ToString(),
                    errors = report.summary.totalErrors, warnings = report.summary.totalWarnings,
                    bytes = report.summary.totalSize, seconds = report.summary.totalTime.TotalSeconds }, true));
            if (report.summary.result != BuildResult.Succeeded) Debug.LogError("[MPTest] build failed " + report.summary.result);
        }
        [System.Serializable] class Result { public string result; public int errors, warnings; public ulong bytes; public double seconds; }
    }
}
#endif
