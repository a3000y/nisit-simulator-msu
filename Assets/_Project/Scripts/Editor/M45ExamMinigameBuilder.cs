#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.Academics.ExamMinigame;

namespace NisitSimulator.EditorTools
{
    // คลังข้อสอบมินิเกม → Resources/ExamBanks/ExamBankDatabase.asset (แก้เนื้อหาใน Inspector ได้)
    //   Nisit ▸ Build Exam Minigame Banks        สร้าง asset ถ้ายังไม่มี / เติมคลังวิชาที่ขาดจากค่าเริ่มต้น (ไม่ทับเนื้อหาที่แก้ไว้)
    //   Nisit ▸ Reset Exam Minigame Banks        รีเซ็ตเป็นค่าเริ่มต้นทั้งหมด
    //   Nisit ▸ Report Exam Bank Coverage        รายงานวิชาในหลักสูตรที่ยังไม่มีคลังข้อสอบ
    public static class M45ExamMinigameBuilder
    {
        const string Dir = "Assets/_Project/Resources/ExamBanks";
        const string AssetPath = Dir + "/ExamBankDatabase.asset";

        [MenuItem("Nisit/Build Exam Minigame Banks (คลังข้อสอบมินิเกม)")]
        public static void Build()
        {
            EnsureFolder();
            var db = AssetDatabase.LoadAssetAtPath<ExamBankDatabase>(AssetPath);
            bool created = false;
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<ExamBankDatabase>();
                ExamBankDefaults.Fill(db);
                AssetDatabase.CreateAsset(db, AssetPath);
                created = true;
            }
            else
            {
                var defaults = ExamBankDefaults.Create();
                int added = 0;
                foreach (var b in defaults.banks)
                    if (db.Get(b.courseCode) == null) { db.banks.Add(b); added++; }
                Object.DestroyImmediate(defaults);
                if (added > 0) EditorUtility.SetDirty(db);
                Debug.Log($"[ExamBank] เติมคลังวิชาที่ขาด {added} วิชา (เนื้อหาเดิมไม่ถูกแก้)");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=lime>[ExamBank] {(created ? "สร้าง" : "อัปเดต")} {AssetPath} แล้ว</color>\n" + Report(db));
        }

        [MenuItem("Nisit/Reset Exam Minigame Banks (ค่าเริ่มต้น)")]
        public static void ResetToDefaults()
        {
            EnsureFolder();
            var db = AssetDatabase.LoadAssetAtPath<ExamBankDatabase>(AssetPath);
            if (db == null) { Build(); return; }
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Nisit Simulator", "รีเซ็ตคลังข้อสอบมินิเกมเป็นค่าเริ่มต้น? เนื้อหาที่แก้ไว้จะหายทั้งหมด", "รีเซ็ต", "ยกเลิก")) return;
            ExamBankDefaults.Fill(db);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log("[ExamBank] รีเซ็ตเป็นค่าเริ่มต้นแล้ว\n" + Report(db));
        }

        [MenuItem("Nisit/Report Exam Bank Coverage (วิชาที่ยังไม่มีข้อสอบ)")]
        public static void LogReport()
        {
            var db = AssetDatabase.LoadAssetAtPath<ExamBankDatabase>(AssetPath);
            if (db == null) db = ExamBankDefaults.Create();
            Debug.Log(Report(db));
        }

        public static string Report(ExamBankDatabase db)
        {
            var cur = CurriculumDefinition.LoadDefault();
            var sb = new StringBuilder();
            var errs = db.Validate(cur);
            sb.Append(errs.Count == 0 ? "ตรวจข้อมูลผ่าน\n" : "พบปัญหา:\n- " + string.Join("\n- ", errs) + "\n");
            foreach (var b in db.banks)
            {
                var d = cur.Get(b.courseCode);
                sb.Append($"มีคลัง: {b.courseCode} {(d != null ? d.title : "(ไม่พบในหลักสูตร)")} — {b.questions.Count} ข้อ\n");
            }
            var missing = new List<string>();
            var legacy = new List<string>();
            foreach (var c in cur.courses)
            {
                if (c == null || db.HasContent(c.code)) continue;
                if (db.UsesLegacyAssessment(c)) legacy.Add($"{c.code} {c.title}");
                else missing.Add($"{c.code} {c.title}");
            }
            sb.Append($"\nยังไม่มีคลังข้อสอบ ({missing.Count} วิชา — สอบด้วยระบบเดิมจนกว่าจะเพิ่ม):\n");
            foreach (var m in missing) sb.Append("  - ").Append(m).Append('\n');
            sb.Append($"\nประเมินด้วยระบบเดิม (โครงงาน/ฝึกงาน {legacy.Count} วิชา):\n");
            foreach (var m in legacy) sb.Append("  - ").Append(m).Append('\n');
            return sb.ToString();
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources")) AssetDatabase.CreateFolder("Assets/_Project", "Resources");
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project/Resources", "ExamBanks");
        }
    }
}
#endif
