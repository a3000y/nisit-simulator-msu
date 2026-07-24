#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NisitSimulator.EditorTools
{
    // เปิด Loop Time ให้ทุกแอนิเมชันในโฟลเดอร์ Characters (แก้ท่าเดิน/วิ่งค้าง)
    // ใช้: เมนู  Nisit -> Fix Animation Loops
    public static class FixAnimationLoops
    {
        const string CharDir = "Assets/_Project/Art/Characters";

        [MenuItem("Nisit/Fix Animation Loops")]
        public static void Fix()
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                // ดึงรายการ clip (ถ้ายังไม่เคยแก้ ใช้ค่า default)
                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0) continue;

                bool changed = false;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (!clips[i].loopTime)
                    {
                        clips[i].loopTime = true;   // เปิดวนซ้ำ
                        changed = true;
                        count++;
                    }
                }

                if (changed)
                {
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
            }

            Debug.Log($"<color=lime>[Nisit] เปิด Loop Time แล้ว {count} แอนิเมชัน</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                $"เปิด Loop Time ให้ {count} แอนิเมชันแล้ว! 🔁\n\nกด Play — ท่าเดิน/วิ่งจะวนต่อเนื่องไม่ค้างแล้ว\n\n(ถ้ายังค้าง แสดงว่าเป็นเรื่อง avatar ส่งรูปมาให้ผมดูต่อ)", "OK");
        }
    }
}
#endif
