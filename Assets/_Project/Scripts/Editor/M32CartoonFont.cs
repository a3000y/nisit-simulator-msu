#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace NisitSimulator.EditorTools
{
    // 🎨 เปลี่ยนฟอนต์ทั้งเกมเป็น Pattaya (การ์ตูนหนากลม) แบบ SDFAA (คมชัดทุกขนาด)
    //   ต้องวางไฟล์ Pattaya-Regular.ttf ใน Assets/_Project/Art/Fonts/ ก่อน (โหลดจาก Google Fonts)
    //   ใช้: เมนู  Nisit -> Apply Cartoon Font (Pattaya)
    public static class M32CartoonFont
    {
        const string SdfPath   = "Assets/_Project/Art/Fonts/Pattaya SDF.asset";
        const string FontDir   = "Assets/_Project/Art/Fonts";
        const string MenuScene = "Assets/Scenes/Scene1.unity";
        const string GameScene = "Assets/_Project/Scenes/01_Gameplay.unity";

        [MenuItem("Nisit/Apply Cartoon Font (Pattaya)")]
        public static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var font = EnsureSdf();
            if (font == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "ไม่พบไฟล์ Pattaya (.ttf)\n\nโหลดจาก:  fonts.google.com/specimen/Pattaya\nแตกไฟล์ → วาง Pattaya-Regular.ttf ใน:\n" + FontDir + "/\nแล้วรันเมนูนี้อีกครั้ง", "ปิด");
                return;
            }

            int a = SwapInScene(MenuScene, font);
            int b = SwapInScene(GameScene, font);
            SetTMPDefault(font);

            Debug.Log($"<color=lime>[Nisit] เปลี่ยนฟอนต์ Pattaya — เมนู {a} / เกม {b}</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                $"เปลี่ยนเป็นฟอนต์ Pattaya (การ์ตูน) แล้ว! 🎨\n\n• เมนู {a} · ในเกม {b} ข้อความ\n• SDFAA คมชัดทุกขนาด\n• ตั้งเป็นฟอนต์เริ่มต้นแล้ว\n\nกด Play ดูได้เลย\n(ถ้ารัน Rebuild UI ใหม่ทีหลัง ให้รันเมนูนี้ซ้ำ)", "เยี่ยม!");
        }

        // สร้าง Pattaya SDF (SDFAA ชัดเจน + Dynamic รองรับไทยครบ) ถ้ายังไม่มี
        static TMP_FontAsset EnsureSdf()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfPath);
            if (existing != null) return existing;

            string ttf = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { FontDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(p).ToLower().Contains("pattaya")) { ttf = p; break; }
            }
            if (ttf == null) return null;

            var src = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (src == null) return null;

            // สำคัญ: ระบุ SDFAA_HINTED ชัดเจน (ไม่ใช่ SMOOTH ที่เบลอ) · Dynamic = ไทยครบ
            var fa = TMP_FontAsset.CreateFontAsset(src, 90, 9,
                GlyphRenderMode.SDFAA_HINTED, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fa == null) return null;

            fa.name = "Pattaya SDF";
            AssetDatabase.CreateAsset(fa, SdfPath);
            if (fa.material != null) { fa.material.name = "Pattaya SDF Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
            if (fa.atlasTextures != null)
                foreach (var tex in fa.atlasTextures)
                    if (tex != null) { tex.name = "Pattaya Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }

            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(SdfPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfPath);
        }

        static int SwapInScene(string path, TMP_FontAsset font)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = 0;
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { t.font = font; EditorUtility.SetDirty(t); n++; }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return n;
        }

        static void SetTMPDefault(TMP_FontAsset font)
        {
            var settings = TMP_Settings.instance;
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var p = so.FindProperty("m_defaultFontAsset");
            if (p != null) { p.objectReferenceValue = font; so.ApplyModifiedProperties(); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets(); }
        }
    }
}
#endif
