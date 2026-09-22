#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace NisitSimulator.EditorTools
{
    // 🔤 แก้ฟอนต์ทั้งโปรเจกต์ให้คมชัด — เดิมหลายตัวเบคแบบ SMOOTH (บิตแมป = เบลอเมื่อสเกล)
    //   เปลี่ยนเป็น SDFAA (คมทุกขนาด) + ตั้ง padding/gradient ให้พอดี + ล้าง atlas ให้สร้างใหม่
    //   ใช้: เมนู  Nisit -> Fix Fonts (SDF ให้คมชัด)
    public static class M31FixFonts
    {
        const int SDFAA_HINTED = 4169;   // ค่าเดียวกับ LiberationSans SDF (ฟอนต์มาตรฐาน TMP ที่คมชัด)
        const int PADDING = 9;

        [MenuItem("Nisit/Fix Fonts (SDF ให้คมชัด)")]
        public static void Fix()
        {
            var log = new System.Text.StringBuilder();
            int fixedCount = 0, okCount = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (fa == null) continue;

                var so = new SerializedObject(fa);
                var rm = so.FindProperty("m_AtlasRenderMode");
                if (rm != null && rm.intValue == SDFAA_HINTED)   // เป็น SDF อยู่แล้ว
                {
                    okCount++;
                    log.AppendLine($"• (SDF อยู่แล้ว) {Path.GetFileName(path)}");
                    continue;
                }

                if (rm != null) rm.intValue = SDFAA_HINTED;                 // SMOOTH/บิตแมป → SDFAA
                var pad = so.FindProperty("m_AtlasPadding");
                if (pad != null) pad.intValue = PADDING;
                so.ApplyModifiedProperties();

                fa.ClearFontAssetData(true);   // ล้าง atlas + ตาราง glyph → dynamic จะ re-render เป็น SDF

                // ปรับ material ให้ gradient scale ตรงกับ padding (SDF คมพอดี ไม่ฟุ้ง)
                if (fa.material != null)
                {
                    if (fa.material.HasProperty("_GradientScale")) fa.material.SetFloat("_GradientScale", PADDING + 1);
                    if (fa.material.HasProperty("_Sharpness")) fa.material.SetFloat("_Sharpness", 0f);
                    EditorUtility.SetDirty(fa.material);
                }

                EditorUtility.SetDirty(fa);
                log.AppendLine($"✓ แก้เป็น SDFAA: {Path.GetFileName(path)}");
                fixedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=lime>[Nisit] Fix Fonts: แก้ {fixedCount} · เป็น SDF อยู่แล้ว {okCount}</color>\n" + log);
            EditorUtility.DisplayDialog("Nisit Simulator",
                $"แก้ฟอนต์ให้คมชัดเสร็จ! 🔤\n\nแก้เป็น SDFAA: {fixedCount} ตัว · เป็น SDF อยู่แล้ว: {okCount} ตัว\n\n" + log +
                "\nเดิม SMOOTH (บิตแมป=เบลอ) → SDFAA (คมทุกขนาด)\nกด Play ดูตัวหนังสือใหม่ทั้งเกม\n\n" +
                "ถ้ายังเบลอบางตัว: Window → TextMeshPro → Font Asset Creator → Render Mode = SDFAA → Generate → Save ทับ", "เยี่ยม!");
        }
    }
}
#endif
