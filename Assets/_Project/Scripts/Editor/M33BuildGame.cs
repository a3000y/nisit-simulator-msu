#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NisitSimulator.EditorTools
{
    // 🏗️ Build เกมเป็น .exe (Windows 64-bit) ในคลิกเดียว → โฟลเดอร์ Build/ ข้างโปรเจกต์
    //   ใช้: เมนู  Nisit -> ★ Build Game (.exe)
    public static class M33BuildGame
    {
        [MenuItem("Nisit/★ Build Game (.exe)", false, 1)]
        public static void Build()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Nisit", "ไม่มีฉากใน Build Settings\n(File → Build Settings → Add Open Scenes)", "OK");
                return;
            }

            string root = Directory.GetParent(Application.dataPath).FullName;
            string dir = Path.Combine(root, "Build");
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "NisitSimulator.exe");

            Debug.Log("<color=cyan>[Nisit] เริ่ม Build .exe ... (อาจใช้เวลาหลายนาที)</color>");

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var sum = report.summary;

            if (sum.result == BuildResult.Succeeded)
            {
                long mb = (long)(sum.totalSize / (1024 * 1024));
                Debug.Log($"<color=lime>[Nisit] Build สำเร็จ! {mb} MB → {exe}</color>");
                EditorUtility.RevealInFinder(exe);
                EditorUtility.DisplayDialog("Nisit Simulator",
                    $"Build .exe สำเร็จ! 🎉\n\nขนาด ~{mb} MB\nที่: {exe}\n\nเปิดโฟลเดอร์ให้แล้ว → ดับเบิลคลิก NisitSimulator.exe เล่นเต็มจอ FHD ได้เลย", "เยี่ยม!");
            }
            else
            {
                Debug.LogError($"[Nisit] Build ล้มเหลว: {sum.result} · errors {sum.totalErrors}");
                EditorUtility.DisplayDialog("Nisit",
                    $"Build ล้มเหลว: {sum.result}\nerrors: {sum.totalErrors}\n\nดู Console (แดง) แล้วส่งมาให้ผมช่วยแก้", "OK");
            }
        }
    }
}
#endif
