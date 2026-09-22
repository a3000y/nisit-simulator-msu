#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NisitSimulator.EditorTools
{
    // แก้ตัวละคร Mixamo ที่เป็นสีขาว — แตก texture ที่ฝังใน FBX ออกมาแล้วผูกเข้าวัสดุ
    // ใช้: เมนู  Nisit -> Fix Character Materials
    public static class FixCharacterMaterials
    {
        const string CharDir = "Assets/_Project/Art/Characters";

        [MenuItem("Nisit/Fix Character Materials")]
        public static void Fix()
        {
            string texDir = CharDir + "/Textures";
            if (!Directory.Exists(texDir)) Directory.CreateDirectory(texDir);

            var log = new System.Text.StringBuilder();
            int count = 0, extractedCount = 0;

            // แก้ "ทุก" โมเดลตัวละคร (ไม่ใช่แค่ตัวแรก) — ข้ามไฟล์อนิเมชัน (@)
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.Contains("@")) continue;                        // ไฟล์ท่าอนิเมชัน ข้าม
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null || go.GetComponentInChildren<SkinnedMeshRenderer>() == null) continue;

                var importer = AssetImporter.GetAtPath(p) as ModelImporter;
                if (importer == null) continue;

                // 1) เปิด import วัสดุ
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                // 2) แตก texture ที่ฝังใน FBX ออกมาเป็นไฟล์จริง
                bool extracted = importer.ExtractTextures(texDir);
                AssetDatabase.Refresh();
                // 3) แตกวัสดุเป็น asset (ผูกกับ texture ที่แตกแล้ว)
                importer.materialLocation = ModelImporterMaterialLocation.External;
                importer.SaveAndReimport();

                log.AppendLine($"{(extracted ? "✓" : "•")} {Path.GetFileName(p)}");
                if (extracted) extractedCount++;
                count++;
            }
            AssetDatabase.Refresh();

            Debug.Log($"<color=lime>[Nisit] แก้วัสดุตัวละคร {count} ไฟล์ (แตก texture {extractedCount})</color>\n" + log);
            EditorUtility.DisplayDialog("Nisit Simulator",
                count == 0
                    ? "ไม่พบไฟล์ตัวละครใน Characters/"
                    : $"แก้วัสดุตัวละคร {count} ตัวเสร็จ! 🎨 (แตก texture {extractedCount})\n\n" + log +
                      "\nควรมีสีผิว/เสื้อผ้าแล้ว → รัน Build Talk NPCs ใหม่ แล้ว Play\n\n(ถ้ายังเทา ส่งรูปมาให้ผมดู)", "OK");
        }
    }
}
#endif
