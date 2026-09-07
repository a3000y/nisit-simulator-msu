#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NisitSimulator.EditorTools
{
    // 🧍 แปลงโมเดลตัวละครทุกตัวในโฟลเดอร์ Characters เป็น Humanoid (สร้าง Avatar ของตัวเอง)
    //   โมเดล Mixamo ที่โหลดมามักเป็น Generic → ต้องเป็น Humanoid ถึงจะเล่นท่า Waving/Talking/เดิน ที่ใช้ร่วมกันได้
    //   ใช้: เมนู Nisit -> Setup NPC Models (Humanoid)  แล้วค่อย Build Talk NPCs
    public static class M28SetupNPCModels
    {
        public static bool SuppressDialog = false;

        const string CharDir = "Assets/_Project/Art/Characters";

        [MenuItem("Nisit/Setup NPC Models (Humanoid)", false, 32)]
        public static void Setup()
        {
            var log = new System.Text.StringBuilder();
            int done = 0, skip = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { CharDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("@")) continue;                    // ไฟล์ท่าอนิเมชัน ข้าม
                if (!path.ToLower().EndsWith(".fbx")) continue;

                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;

                if (imp.animationType == ModelImporterAnimationType.Human)
                {
                    log.AppendLine("• (Humanoid อยู่แล้ว) " + Path.GetFileName(path));
                    skip++; continue;
                }

                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;   // สร้าง Avatar จากโครงของตัวเอง
                imp.SaveAndReimport();
                log.AppendLine("✓ แปลงเป็น Humanoid: " + Path.GetFileName(path));
                done++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"<color=lime>[Nisit] ตั้ง Humanoid {done} ตัว (ข้ามที่เป็นอยู่แล้ว {skip})</color>\n" + log);
            if (!SuppressDialog)
                EditorUtility.DisplayDialog("Nisit Simulator",
                    $"ตั้งค่าโมเดลเป็น Humanoid เสร็จ! 🧍\n\nแปลงใหม่ {done} ตัว · มีอยู่แล้ว {skip} ตัว\n\n" + log +
                    "\nต่อไป: Nisit → Build Talk NPCs (หรือ ★ Rebuild All)", "เยี่ยม!");
        }
    }
}
#endif
