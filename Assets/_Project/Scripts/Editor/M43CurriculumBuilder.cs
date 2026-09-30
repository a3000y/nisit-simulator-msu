#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
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
            if (errs.Count == 0) Debug.Log("<color=lime>[Nisit] หลักสูตรผ่านการตรวจ</color>");
            else foreach (var e in errs) Debug.LogError("[Curriculum] " + e);
        }
    }
}
#endif
