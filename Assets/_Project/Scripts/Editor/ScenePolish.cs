#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace NisitSimulator.EditorTools
{
    // แต่งบรรยากาศฉากให้สวย + น่าเล่นขึ้น:
    //   หมอกไกลๆ (ซ่อนขอบแมป + เพิ่มมิติ) · จัดมุมแสงอาทิตย์ · เพิ่มแสงแวดล้อม
    // ใช้: เมนู  Nisit -> Polish Look
    public static class ScenePolish
    {
        [MenuItem("Nisit/Polish Look", false, 20)]
        public static void Polish()
        {
            // ---- หมอก (อิงขนาดพื้น เพื่อซ่อนขอบแมปพอดี) ----
            float worldSize = 120f;
            var ground = GameObject.Find("Ground");
            if (ground != null) worldSize = 10f * Mathf.Max(ground.transform.localScale.x, ground.transform.localScale.z);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.85f);   // ฟ้าอมเทา กลืนขอบฟ้า
            RenderSettings.fogStartDistance = worldSize * 0.45f;
            RenderSettings.fogEndDistance = worldSize * 1.05f;

            // ---- แสงแวดล้อม (เงาไม่ดำสนิท) ----
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.85f, 0.88f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.63f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.42f, 0.4f);

            // ---- แสงอาทิตย์: มุมเฉียงสวย + อุ่นนิดๆ ----
            Light sun = RenderSettings.sun;
            if (sun == null)
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null)
            {
                Undo.RecordObject(sun.transform, "Polish Light");
                Undo.RecordObject(sun, "Polish Light");
                sun.transform.rotation = Quaternion.Euler(48f, 40f, 0f);   // มุมสายๆ เงาทอดสวย
                sun.color = new Color(1f, 0.96f, 0.88f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
            }

            Debug.Log($"<color=lime>[Nisit] ✅ แต่งฉากแล้ว: หมอก {RenderSettings.fogStartDistance:F0}-{RenderSettings.fogEndDistance:F0} + แสงอุ่น + ambient นุ่ม\n" +
                      "ปรับหมอกได้ที่ Window > Rendering > Lighting > Environment</color>");
        }

        [MenuItem("Nisit/Polish Look (ปิดหมอก)", false, 21)]
        public static void FogOff()
        {
            RenderSettings.fog = false;
            Debug.Log("<color=lime>[Nisit] ปิดหมอกแล้ว</color>");
        }
    }
}
#endif
