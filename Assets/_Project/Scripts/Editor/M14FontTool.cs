#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

namespace NisitSimulator.EditorTools
{
    // 🔤 สร้างฟอนต์ Mitr SDF (แบบ Dynamic รองรับไทยครบ) + เปลี่ยนฟอนต์ข้อความทั้งเกมในคลิกเดียว
    // ใช้: เมนู  Nisit -> Apply Mitr Font (All UI)
    public static class M14FontTool
    {
        const string MitrTtf   = "Assets/_Project/Art/Fonts/Mitr/Mitr-Medium.ttf";
        const string MitrSdf   = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        const string MenuScene = "Assets/Scenes/Scene1.unity";
        const string GameScene = "Assets/_Project/Scenes/01_Gameplay.unity";

        [MenuItem("Nisit/Apply Mitr Font (All UI)")]
        public static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var font = EnsureMitrSdf();
            if (font == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "ไม่พบไฟล์ Mitr-Medium.ttf\nที่ " + MitrTtf + "\n\nตรวจว่าวางโฟลเดอร์ Mitr ไว้ที่ Assets/_Project/Art/Fonts/Mitr/ แล้วหรือยัง", "ปิด");
                return;
            }

            int a = SwapInScene(MenuScene, font);
            int b = SwapInScene(GameScene, font);

            Debug.Log($"<color=lime>[Nisit] เปลี่ยนฟอนต์ Mitr แล้ว — เมนู {a} จุด / เกม {b} จุด</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                $"เปลี่ยนเป็นฟอนต์ Mitr เรียบร้อย! 🔤\n\n• หน้าเมนู: {a} ข้อความ\n• ในเกม: {b} ข้อความ\n\nฟอนต์ใหม่: Mitr SDF (Dynamic — พิมพ์ไทยได้ครบ)\nกด Play ดูได้เลย", "เยี่ยม!");
        }

        // สร้างฟอนต์ Mitr SDF ถ้ายังไม่มี (Dynamic = เรนเดอร์ไทยตามต้องการ ไม่ต้องเลือกช่วงอักขระ)
        public static TMP_FontAsset EnsureMitrSdf()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MitrSdf);
            if (existing != null) return existing;

            var src = AssetDatabase.LoadAssetAtPath<Font>(MitrTtf);
            if (src == null) return null;

            var fa = TMP_FontAsset.CreateFontAsset(src);   // ค่าเริ่มต้น: Dynamic, SDFAA, 1024x1024
            if (fa == null) return null;
            fa.name = "Mitr SDF";

            AssetDatabase.CreateAsset(fa, MitrSdf);

            // ผูก material + atlas เป็น sub-asset ให้เซฟติดไฟล์
            if (fa.material != null)
            {
                fa.material.name = "Mitr SDF Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            if (fa.atlasTextures != null)
                foreach (var tex in fa.atlasTextures)
                    if (tex != null) { tex.name = "Mitr SDF Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }

            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MitrSdf);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MitrSdf);
        }

        static int SwapInScene(string path, TMP_FontAsset font)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = 0;
            // รวมทั้งที่ปิดอยู่ (โทรศัพท์/ป็อปอัปเหตุการณ์)
            var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in texts)
            {
                t.font = font;
                EditorUtility.SetDirty(t);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return n;
        }
    }
}
#endif
