using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Interaction;
using NisitSimulator.Stats;

namespace NisitSimulator.EditorTools
{
    // ===== Nisit ▸ Stress ▸ Apply Stress Tuning =====
    //   ปรับค่าความเครียดของของในฉากที่เปิดอยู่ให้ตรงระบบความเครียดใหม่ — รันซ้ำได้ (แก้เฉพาะค่าที่ยังเป็นค่าเดิม ไม่ทับค่าที่ปรับเองใน Inspector)
    //   • ที่นั่งเรียน (เรียน/เรียนคอมพิวเตอร์/เรียนสัมมนา) +3 → +4/ชม.
    //   • ม้านั่ง "นั่งพัก" 0 → -2/ชม. · โรงอาหาร "กินข้าว" 0 → -2
    //   • เตียง SleepStation -35 → -25
    //   • StatDecay.stressFallPerMinute 0.01 → 0.006
    //   ไม่ลบ/เปลี่ยนชื่อ GameObject ใด ๆ
    public static class M48StressTuning
    {
        [MenuItem("Nisit/Stress/Apply Stress Tuning")]
        public static void Apply()
        {
            int study = 0, bench = 0, meal = 0, beds = 0, decay = 0;
            foreach (var spot in Object.FindObjectsByType<ActivitySpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = spot.activityName ?? "";
                bool isStudy = n == "เรียน" || n == "เรียนคอมพิวเตอร์" || n == "เรียนสัมมนา";
                if (isStudy && Mathf.Approximately(spot.stressChange, 3f)) { Set(spot, () => spot.stressChange = StressBands.ClassPerHour); study++; }
                else if (n == "นั่งพัก" && Mathf.Approximately(spot.stressChange, 0f)) { Set(spot, () => spot.stressChange = StressBands.BenchPerHour); bench++; }
                else if (n == "กินข้าว" && Mathf.Approximately(spot.stressChange, 0f)) { Set(spot, () => spot.stressChange = StressBands.CafeteriaMeal); meal++; }
            }
            foreach (var bed in Object.FindObjectsByType<SleepStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Mathf.Approximately(bed.stressChange, -35f)) { Set(bed, () => bed.stressChange = StressBands.SleepStressChange); beds++; }
            foreach (var d in Object.FindObjectsByType<StatDecay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Mathf.Approximately(d.stressFallPerMinute, 0.01f)) { Set(d, () => d.stressFallPerMinute = StressBands.NaturalFallPerMinute); decay++; }

            int total = study + bench + meal + beds + decay;
            if (total > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Nisit] Stress tuning: ที่นั่งเรียน {study} · ม้านั่ง {bench} · โรงอาหาร {meal} · เตียง {beds} · StatDecay {decay}" +
                      (total == 0 ? " (ปรับครบแล้ว — ไม่มีอะไรเปลี่ยน)" : " — อย่าลืมเซฟฉาก"));
        }

        static void Set(Object o, System.Action change)
        {
            Undo.RecordObject(o, "Stress Tuning");
            change();
            EditorUtility.SetDirty(o);
            if (PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
        }
    }
}
