#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using NisitSimulator.Academics;

namespace NisitSimulator.EditorTools
{
    // 🎓 สร้าง/รีเซ็ตข้อมูลหลักสูตรวิทยาการคอมพิวเตอร์ (ScriptableObject ใน Resources) + ตรวจความถูกต้อง
    //   ใช้: เมนู Nisit -> Build CS Curriculum (ลงทะเบียนเรียน)
    //   ระบบลงทะเบียนสร้างตัวเองตอนรัน (GameplayBootstrap) — ไม่ต้องแก้ฉาก/Prefab
    public static class M43CurriculumBuilder
    {
        public const string Dir = "Assets/_Project/Resources/Curricula";
        public const string AssetPath = Dir + "/CS_Curriculum.asset";

        [MenuItem("Nisit/Build CS Curriculum (ลงทะเบียนเรียน)")]
        public static void BuildMenu()
        {
            bool exists = AssetDatabase.LoadAssetAtPath<CurriculumDefinition>(AssetPath) != null;
            if (exists && !EditorUtility.DisplayDialog("Nisit Simulator",
                    "มีหลักสูตรอยู่แล้ว — รีเซ็ตเป็นค่าเริ่มต้นหรือไม่?\n(ค่าที่แก้ใน Inspector จะหาย)", "รีเซ็ต", "ยกเลิก"))
                return;
            var c = Build();
            var errs = c.Validate();
            EditorUtility.DisplayDialog("Nisit Simulator",
                $"สร้างหลักสูตรแล้ว: {c.courses.Count} วิชา · บังคับ {c.RequiredCredits} + เลือก → จบ {c.GraduationCredits} หน่วยกิต\n" +
                (errs.Count == 0 ? "ตรวจข้อมูลผ่าน (แผนทุกภาคไม่ชนเวลา ไม่เกินเพดาน)" : "พบปัญหา:\n" + string.Join("\n", errs)),
                "เยี่ยม!");
            Selection.activeObject = c;
        }

        // สร้าง (หรือเขียนทับ) asset จากค่าเริ่มต้น — คืน asset ที่บันทึกแล้ว
        public static CurriculumDefinition Build()
        {
            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
            var c = AssetDatabase.LoadAssetAtPath<CurriculumDefinition>(AssetPath);
            if (c == null)
            {
                c = ScriptableObject.CreateInstance<CurriculumDefinition>();
                CsCurriculumDefaults.Fill(c);
                AssetDatabase.CreateAsset(c, AssetPath);
            }
            else CsCurriculumDefaults.Fill(c);
            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=lime>[Nisit] หลักสูตร CS: {c.courses.Count} วิชา → {AssetPath}</color>");
            return c;
        }

        [MenuItem("Nisit/Validate CS Curriculum")]
        public static void ValidateMenu()
        {
            var c = AssetDatabase.LoadAssetAtPath<CurriculumDefinition>(AssetPath);
            if (c == null) { EditorUtility.DisplayDialog("Nisit Simulator", "ยังไม่มี asset — กด Build CS Curriculum ก่อน", "ตกลง"); return; }
            var errs = c.Validate();
            // ห้องเรียน: ทุกคาบต้องมี roomId ที่มีอยู่จริง + ห้องเดียวกัน วันเดียวกัน เวลาทับ (ภาคเดียวกัน) ห้ามเด็ดขาด
            var catalog = AssetDatabase.LoadAssetAtPath<ClassroomCatalog>(M47ClassroomBuilder.CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("[Curriculum] ยังไม่มี ClassroomCatalog.asset — ตรวจกับห้องค่าเริ่มต้นในโค้ด (รัน Nisit ▸ Classrooms ▸ Setup Classroom Zones)");
                catalog = ClassroomDefaults.Create();
            }
            foreach (var e in catalog.Validate()) errs.Add("[ห้อง] " + e);
            var issues = RegistrationService.FindRoomConflicts(c, catalog, requireRooms: true);
            foreach (var i in issues) errs.Add("[ห้อง] " + i);
            // ห้องที่หลักสูตรใช้ต้องมี ClassroomZone ในฉากที่เปิดอยู่ (ถ้าเปิด 01_Gameplay)
            if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path.EndsWith("01_Gameplay.unity"))
            {
                var zones = new HashSet<string>();
                foreach (var z in Object.FindObjectsByType<NisitSimulator.Interaction.ClassroomZone>(FindObjectsInactive.Include, FindObjectsSortMode.None)) zones.Add(z.roomId);
                var used = new HashSet<string>();
                foreach (var d in c.courses)
                {
                    if (d == null) continue;
                    foreach (var s in d.sessions) if (s.HasRoom) used.Add(s.roomId);
                    foreach (var s in d.retakeSessions) if (s.HasRoom) used.Add(s.roomId);
                }
                foreach (var id in used) if (!zones.Contains(id)) errs.Add($"[ห้อง] {id}: ไม่มี ClassroomZone ในฉาก 01_Gameplay");
            }
            if (errs.Count == 0) Debug.Log($"<color=lime>[Nisit] หลักสูตรผ่านการตรวจ (รวมห้องเรียน: ไม่มีห้องชน · roomId มีอยู่จริงทุกคาบ)</color>");
            else foreach (var e in errs) Debug.LogError("[Curriculum] " + e);
        }
    }
}
#endif
