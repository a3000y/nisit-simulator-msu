#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace NisitSimulator.EditorTools
{
    // สร้างฟอนต์ไทยสำหรับ TextMeshPro (จากฟอนต์ในเครื่อง) แล้วใส่ให้ทุกข้อความใน HUD
    //   แก้ปัญหา "ตัวอักษรไทยเป็นสี่เหลี่ยม □" เพราะฟอนต์ TMP เริ่มต้นไม่มีภาษาไทย
    // ใช้: เมนู  Nisit -> Fix Thai Font (HUD)   (M3HudBuilder เรียกให้อัตโนมัติด้วย)
    public static class ThaiFontSetup
    {
        private const string FontDir = "Assets/_Project/Fonts";
        private const string TmpAssetPath = FontDir + "/NisitThai SDF.asset";

        // ฟอนต์ไทยในเครื่อง Windows (ไล่หาจากบนลงล่าง)
        private static readonly string[] SystemFonts =
        {
            @"C:\Windows\Fonts\leelawui.ttf",   // Leelawadee UI (สวย ทันสมัย)
            @"C:\Windows\Fonts\tahoma.ttf",     // Tahoma (มีไทย)
            @"C:\Windows\Fonts\leelawad.ttf",   // Leelawadee
            @"C:\Windows\Fonts\upcjl.ttf",      // (สำรอง)
        };

        [MenuItem("Nisit/Fix Thai Font (HUD)", false, 20)]
        public static void FixMenu()
        {
            var font = GetOrCreateThaiFont();
            if (font == null)
            {
                EditorUtility.DisplayDialog("Nisit", "ไม่พบฟอนต์ไทยในเครื่อง (Leelawadee/Tahoma)\nลองติดตั้งฟอนต์ไทยแล้วรันใหม่", "OK");
                return;
            }
            int n = ApplyToHud(font);
            EditorUtility.DisplayDialog("Nisit", $"ใส่ฟอนต์ไทยให้ HUD แล้ว {n} ข้อความ\nกด Ctrl+S แล้ว Play — ตัวอักษรไทยจะขึ้นแล้ว", "OK");
        }

        // ให้ M3HudBuilder เรียกต่อท้าย (ถ้ามีฟอนต์ไทย)
        public static int ApplyToHud()
        {
            var f = GetOrCreateThaiFont();
            return f != null ? ApplyToHud(f) : 0;
        }

        private static int ApplyToHud(TMP_FontAsset font)
        {
            var canvas = GameObject.Find("HUD Canvas");
            if (canvas == null) return 0;
            int n = 0;
            foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true)) { t.font = font; n++; }
            EditorUtility.SetDirty(canvas);
            return n;
        }

        // สร้าง (หรือโหลด) TMP_FontAsset ภาษาไทย เก็บเป็นไฟล์ในโปรเจกต์
        public static TMP_FontAsset GetOrCreateThaiFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpAssetPath);
            if (existing != null) return existing;

            string sys = null;
            foreach (var p in SystemFonts) if (File.Exists(p)) { sys = p; break; }
            if (sys == null) return null;

            if (!Directory.Exists(FontDir)) Directory.CreateDirectory(FontDir);
            string ttfPath = FontDir + "/" + Path.GetFileName(sys);
            if (!File.Exists(ttfPath)) File.Copy(sys, ttfPath, true);
            AssetDatabase.ImportAsset(ttfPath, ImportAssetOptions.ForceSynchronousImport);

            var srcFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (srcFont == null) return null;

            // dynamic = เรนเดอร์ glyph ไทยตามต้องการจากไฟล์ฟอนต์
            var tmp = TMP_FontAsset.CreateFontAsset(srcFont);
            if (tmp == null) return null;
            tmp.name = "NisitThai SDF";

            AssetDatabase.CreateAsset(tmp, TmpAssetPath);
            if (tmp.material != null) { tmp.material.name = "NisitThai Mat"; AssetDatabase.AddObjectToAsset(tmp.material, tmp); }
            if (tmp.atlasTextures != null)
                foreach (var tex in tmp.atlasTextures)
                    if (tex != null) { tex.name = "NisitThai Atlas"; AssetDatabase.AddObjectToAsset(tex, tmp); }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TmpAssetPath);

            Debug.Log($"<color=lime>[Nisit] สร้างฟอนต์ไทย TMP จาก {Path.GetFileName(sys)} แล้ว → {TmpAssetPath}</color>");
            return tmp;
        }
    }
}
#endif
