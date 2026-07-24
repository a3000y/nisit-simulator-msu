#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace NisitSimulator.EditorTools
{
    // ปรับขนาดตัวละคร (Player) ให้ได้สัดส่วนที่ดีเทียบกับตึก KayKit
    // วัดความสูงตึกจริง แล้วตั้งตัวละคร ~Ratio เท่าของตึก
    // ใช้: เมนู  Nisit -> Balance Player Scale
    public static class PlayerScaleBalancer
    {
        private const string CityFolder = "Assets/_Project/Art/Models/KayKit_City";
        private const float GlobalScale = 2f;   // ให้ตรงกับ CampusBuilder
        private const float Ratio = 0.4f;       // ตัวละครสูง = Ratio เท่าของ "ตึกเตี้ยสุด" (เตี้ยกว่าประตู) — 0.3 เตี้ยลง / 0.55 สูงขึ้น

        [MenuItem("Nisit/Balance Player Scale", false, 3)]
        public static void Balance()
        {
            var player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Nisit", "ไม่พบ \"Player\" ในฉาก\n(สร้างฉากด้วย Nisit -> Setup M1 Scene ก่อน)", "OK");
                return;
            }

            // ---- วัดความสูง "ตึกเตี้ยที่สุด" จากโมเดล KayKit_City ----
            float minH = float.MaxValue; int n = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { CityFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (!System.IO.Path.GetFileName(path).StartsWith("building_")) continue;
                if (path.Contains("withoutBase")) continue;
                var m = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (m == null) continue;
                float hh = MeasureHeight(m);
                if (hh > 0.01f) { minH = Mathf.Min(minH, hh); n++; }
            }
            if (n == 0)
            {
                EditorUtility.DisplayDialog("Nisit", "ไม่พบตึก building_* ใน KayKit_City", "OK");
                return;
            }
            float shortestBuildingH = minH * GlobalScale;    // ตึกเตี้ยสุด (world)
            float targetH = shortestBuildingH * Ratio;       // ตัวละคร ~ Ratio ของตึกเตี้ยสุด

            // ---- ตั้งขนาดตัวละคร ----
            player.transform.localScale = Vector3.one;           // รีเซ็ตก่อนวัด
            float nativeH = MeasureHeightInstance(player);
            if (nativeH < 0.01f) nativeH = 2f;
            float scale = targetH / nativeH;
            Undo.RecordObject(player.transform, "Balance Player Scale");
            player.transform.localScale = Vector3.one * scale;

            // ยกให้เท้าแตะพื้น
            var b = MeasureBoundsInstance(player);
            var pos = player.transform.position;
            player.transform.position = new Vector3(pos.x, pos.y - b.min.y, pos.z);

            // ปรับความเร็วเดินตามขนาด (ให้ความรู้สึกเท่าเดิม)
            var mv = player.GetComponent("PlayerMovement");
            if (mv != null)
            {
                var so = new SerializedObject(mv);
                var walk = so.FindProperty("walkSpeed");
                var run = so.FindProperty("runSpeed");
                if (walk != null) walk.floatValue = 4f * Mathf.Clamp(scale, 0.5f, 3f);
                if (run != null) run.floatValue = 7f * Mathf.Clamp(scale, 0.5f, 3f);
                so.ApplyModifiedProperties();
            }

            Debug.Log($"<color=lime>[Nisit] ✅ ปรับสเกลตัวละครแล้ว\n" +
                      $"ตึกเตี้ยสุด ~{shortestBuildingH:F1} | ตัวละครตั้งไว้ ~{targetH:F1} ({Ratio*100:F0}% ของตึกเตี้ยสุด) | scale x{scale:F2}\n" +
                      $"อยากให้ตัวเตี้ย/สูงกว่านี้: แก้ Ratio ในไฟล์ PlayerScaleBalancer.cs แล้วกดใหม่</color>");
            Selection.activeGameObject = player;
        }

        private static float MeasureHeight(GameObject prefab)
        {
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tmp.transform.localScale = Vector3.one; tmp.transform.rotation = Quaternion.identity;
            float h = MeasureBoundsInstance(tmp).size.y;
            Object.DestroyImmediate(tmp);
            return h;
        }

        private static float MeasureHeightInstance(GameObject go) => MeasureBoundsInstance(go).size.y;

        private static Bounds MeasureBoundsInstance(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
    }
}
#endif
