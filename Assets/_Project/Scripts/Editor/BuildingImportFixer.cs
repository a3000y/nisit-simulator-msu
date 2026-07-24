#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace NisitSimulator.EditorTools
{
    // แก้อาการตึก FBX เอียง/ตะแคง (แกนติดมาจาก Blender -90° X)
    // สั่ง "Bake Axis Conversion" ให้ทุกไฟล์ในโฟลเดอร์ Buildings แล้ว reimport
    // ใช้: เมนู  Nisit -> Fix Buildings Import  (ทำครั้งเดียว ก่อนวางตึก)
    public static class BuildingImportFixer
    {
        private const string BuildingsFolder = "Assets/_Project/Art/Models/Buildings";

        [MenuItem("Nisit/Fix Buildings Import (Bake Axis)", false, 2)]
        public static void FixImport()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { BuildingsFolder });
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Nisit", $"ไม่พบโมเดลใน {BuildingsFolder}", "OK");
                return;
            }

            int fixedCount = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null) continue;

                    importer.bakeAxisConversion = true;   // ฝังการแปลงแกนลง mesh -> root หมุนได้ตรงๆ
                    importer.SaveAndReimport();
                    fixedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log($"<color=lime>[Nisit] ✅ แก้ import ตึก {fixedCount} ไฟล์แล้ว (Bake Axis Conversion)\n" +
                      "ต่อไปกด Nisit -> Place Campus Buildings ใหม่อีกครั้ง</color>");
        }
    }
}
#endif
