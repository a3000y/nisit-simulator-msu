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
            // หาไฟล์ตัวละคร (FBX ที่มี mesh)
            string fbxPath = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null && go.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                {
                    fbxPath = p;
                    break;
                }
            }

            if (fbxPath == null)
            {
                EditorUtility.DisplayDialog("Nisit", "ไม่พบไฟล์ตัวละครใน Characters/", "OK");
                return;
            }

            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            // 1) เปิดให้ import วัสดุ
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();

            // 2) แตก texture ที่ฝังใน FBX ออกมาเป็นไฟล์จริง
            string texDir = CharDir + "/Textures";
            if (!Directory.Exists(texDir)) Directory.CreateDirectory(texDir);
            bool extracted = importer.ExtractTextures(texDir);
            AssetDatabase.Refresh();

            // 3) แตกวัสดุออกมาเป็น asset (จะได้ผูกกับ texture ที่แตกแล้ว)
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.SaveAndReimport();
            AssetDatabase.Refresh();

            Debug.Log($"<color=lime>[Nisit] แก้วัสดุตัวละครแล้ว</color> (แตก texture: {extracted})");
            EditorUtility.DisplayDialog("Nisit Simulator",
                extracted
                    ? "แตก texture + ผูกวัสดุเสร็จแล้ว! 🎨\n\nตัวละครควรมีสีผิว/เสื้อผ้าแล้ว\nกด Play ดูได้เลย\n\n(ถ้ายังขาวอยู่ ส่งรูปมาให้ผมดูอีกที)"
                    : "ไฟล์นี้อาจไม่มี texture ฝังมา (โมเดลบางตัวเป็นสีล้วน)\nถ้ายังขาว ลองโหลดตัวละครอื่นที่มีเสื้อผ้าจาก Mixamo", "OK");
        }
    }
}
#endif
