#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using NisitSimulator.Systems;

namespace NisitSimulator.EditorTools
{
    // 🦴 ซ่อมบั๊ก "ตัวละครแบบอื่นไม่เดิน/ไม่มีท่าทาง" — ตั้งโมเดลทุกตัวใน CharacterCatalog เป็น Humanoid
    //   (Humanoid = มี avatar → ใช้ controller ร่วม (NisitCharacter) รีทาร์เก็ตแอนิเมชันได้)
    //   ใช้: Nisit ▸ Fix Character Rigs (Humanoid)  แล้ว Build Character Creator + Play
    public static class M40FixCharacterRigs
    {
        [MenuItem("Nisit/Fix Character Rigs (Humanoid)", false, 38)]
        public static void Fix()
        {
            var cat = Resources.Load<CharacterCatalog>("CharacterCatalog");
            if (cat == null || cat.models == null || cat.models.Length == 0)
            {
                EditorUtility.DisplayDialog("Nisit Simulator", "ไม่พบ CharacterCatalog หรือยังไม่มีโมเดล", "โอเค");
                return;
            }

            int fixedCount = 0, already = 0, skipped = 0;
            var report = new StringBuilder("[Nisit] ผลตั้ง Humanoid:\n");

            foreach (var m in cat.models)
            {
                if (m == null) { skipped++; continue; }
                string path = AssetDatabase.GetAssetPath(m);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null)
                {
                    skipped++;
                    report.AppendLine($"⚠ ข้าม (ไม่ใช่ไฟล์โมเดล FBX): {path}");
                    continue;
                }

                if (imp.animationType != ModelImporterAnimationType.Human)
                {
                    imp.animationType = ModelImporterAnimationType.Human;
                    imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    imp.SaveAndReimport();
                    fixedCount++;
                    report.AppendLine($"✔ ตั้ง Humanoid: {System.IO.Path.GetFileName(path)}");
                }
                else
                {
                    already++;
                    report.AppendLine($"• เป็น Humanoid อยู่แล้ว: {System.IO.Path.GetFileName(path)}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());

            EditorUtility.DisplayDialog("Nisit Simulator",
                $"ตั้ง Humanoid ให้โมเดลใหม่ {fixedCount} ตัว\n(เป็นอยู่แล้ว {already} · ข้าม {skipped})\n\n" +
                (skipped > 0 ? "⚠ ตัวที่ข้าม = ไม่ใช่ไฟล์ FBX (เป็น .prefab) ต้องตั้ง Humanoid ที่ไฟล์ FBX ต้นทางเอง\n\n" : "") +
                "ต่อไป: กด Build Character Creator (เผื่อ) แล้ว Play → ลองเลือกแบบ 2-5 ดูว่าเดิน/มีท่าทางไหม", "เยี่ยม!");
        }
    }
}
#endif
