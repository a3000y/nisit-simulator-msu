using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.EditorTools
{
    // ===== Nisit ▸ UI ▸ Apply Readability Fixes =====
    //   รันซ้ำได้ (แก้เฉพาะที่ยังไม่ได้แก้) · ไม่ลบ/เปลี่ยนชื่อ GameObject หรือ asset
    //   1) ฟอนต์สำรอง: ใส่ Mitr SDF เป็น fallback ของ NisitThai SDF และ Pattaya SDF (มี > → • ที่สองฟอนต์นั้นไม่มี)
    //   2) ข้อความ UI (TextMeshProUGUI) ที่ใช้ Pattaya SDF ในฉาก → Mitr SDF (Pattaya เป็นฟอนต์ตกแต่ง อ่านยากเมื่อตัวเล็ก)
    //      ป้ายในโลก 3 มิติ (TextMeshPro ไม่ใช่ UGUI เช่น ShopSign) คงเดิม
    //   3) CanvasScaler ทุกตัวในฉาก: Scale With Screen Size 1920×1080 แบบ Expand → ทั้งหน้าอยู่ในจอเสมอทุกสัดส่วนจอ
    public static class M49UIReadability
    {
        const string MitrPath = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        const string PattayaPath = "Assets/_Project/Art/Fonts/Pattaya SDF.asset";
        const string NisitPath = "Assets/_Project/Fonts/NisitThai SDF.asset";

        [MenuItem("Nisit/UI/Apply Readability Fixes")]
        public static void Apply()
        {
            var mitr = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MitrPath);
            var pattaya = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PattayaPath);
            var nisit = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NisitPath);
            if (mitr == null) { Debug.LogError("[Nisit] ไม่พบ " + MitrPath); return; }

            int fb = 0, fonts = 0, scalers = 0;
            foreach (var f in new[] { nisit, pattaya })
            {
                if (f == null) continue;
                if (f.fallbackFontAssetTable == null) f.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (f.fallbackFontAssetTable.Contains(mitr)) continue;
                f.fallbackFontAssetTable.Add(mitr);
                EditorUtility.SetDirty(f);
                fb++;
            }

            if (pattaya != null)
                foreach (var t in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t.font != pattaya) continue;
                    Undo.RecordObject(t, "UI Readability");
                    t.font = mitr;
                    t.fontSharedMaterial = mitr.material;
                    t.fontStyle &= ~FontStyles.Italic;
                    EditorUtility.SetDirty(t);
                    if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    fonts++;
                }

            foreach (var s in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && s.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand
                    && s.referenceResolution == new Vector2(1920f, 1080f)) continue;
                if (s.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;   // world/constant canvases ไม่แตะ
                Undo.RecordObject(s, "UI Readability");
                NisitSimulator.UI.UIFit.Scaler(s);
                EditorUtility.SetDirty(s);
                if (PrefabUtility.IsPartOfPrefabInstance(s)) PrefabUtility.RecordPrefabInstancePropertyModifications(s);
                scalers++;
            }

            if (fb > 0) AssetDatabase.SaveAssets();
            if (fonts + scalers > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Nisit] UI readability: ฟอนต์สำรอง {fb} · ข้อความ Pattaya→Mitr {fonts} · CanvasScaler→Expand {scalers}" +
                      (fb + fonts + scalers == 0 ? " (แก้ครบแล้ว)" : " — อย่าลืมเซฟฉาก"));
        }
    }
}
