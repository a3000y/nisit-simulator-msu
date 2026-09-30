using System;
using System.Collections.Generic;
using UnityEngine;

namespace NisitSimulator.Academics.ExamMinigame
{
    // ===== ข้อมูลคลังข้อสอบมินิเกม (ใช้เฉพาะตอน "สอบ" — การเข้าเรียนปกติไม่เปิดมินิเกม) =====
    //   เนื้อหาอยู่ใน ScriptableObject: Resources/ExamBanks/ExamBankDatabase.asset
    //   สร้าง/รีเซ็ตด้วยเมนู Nisit ▸ Build Exam Minigame Banks (ค่าเริ่มต้นใน ExamBankDefaults)
    //   ผูกกับรายวิชาด้วย "รหัสวิชาเดิมในหลักสูตร" (CourseDefinition.code) — ไม่สร้างรหัสใหม่

    public enum ExamQuestionType
    {
        MultipleChoice,   // เลือกคำตอบ
        Ordering,         // เรียงลำดับ (บล็อกข้อความ/โค้ด)
        Matching,         // จับคู่
        FindError,        // หาจุดผิด (เลือกบรรทัด/ขั้นตอนที่ผิด)
    }

    [Serializable]
    public class ExamQuestion
    {
        [Tooltip("รหัสถาวรของข้อ (เซฟอ้างรหัสนี้ ห้ามเปลี่ยนหลังปล่อยเกม) เช่น CS102-Q01")]
        public string id;
        public ExamQuestionType type;
        [TextArea(2, 6)] public string prompt;
        [TextArea(3, 14)]
        [Tooltip("ข้อความ/โค้ดประกอบหลายบรรทัด (แสดงเป็นข้อความเท่านั้น ไม่ประมวลผลโค้ด) — เว้นว่างได้")]
        public string code = "";
        [Tooltip("เลือกคำตอบ = ตัวเลือก · หาจุดผิด = บรรทัด/ขั้นตอนให้เลือก · เรียงลำดับ = บล็อกตามลำดับที่ถูก · จับคู่ = ฝั่งซ้าย")]
        public List<string> items = new List<string>();
        [Tooltip("เฉพาะจับคู่: ฝั่งขวา โดย matches[i] คู่กับ items[i]")]
        public List<string> matches = new List<string>();
        [Tooltip("เลือกคำตอบ/หาจุดผิด: index ของคำตอบที่ถูกใน items")]
        public int correctIndex;
        [Tooltip("เฉพาะเรียงลำดับ: ลำดับอื่นที่ถูกเช่นกัน เป็น index ของ items คั่นด้วยจุลภาค เช่น \"1,0,2,3\"")]
        public List<string> alternateOrders = new List<string>();
        [Tooltip("คะแนนเต็มของข้อ")]
        public float points = 1f;
        [TextArea(1, 4)] [Tooltip("คำใบ้ (อธิบายแนวคิด ห้ามเฉลยตรง ๆ)")]
        public string hint = "";
        [TextArea(1, 6)] [Tooltip("คำอธิบายเฉลย (แสดงหลังส่งข้อสอบเท่านั้น)")]
        public string explanation = "";
    }

    [Serializable]
    public class CourseExamBank
    {
        [Tooltip("รหัสวิชาเดิมในหลักสูตร เช่น CS102")]
        public string courseCode;
        [Tooltip("เพิ่มเลขเมื่อแก้เนื้อหา (เซฟที่สอบค้างจะเก็บเลขชุดที่ใช้ไว้)")]
        public int version = 1;
        [Tooltip("จำนวนข้อต่อการสอบ (0 = ค่าเริ่มต้นของฐานข้อมูล)")]
        public int questionsPerExam = 0;
        [Tooltip("เวลาสอบเป็นวินาทีจริง (0 = ค่าเริ่มต้นของฐานข้อมูล)")]
        public float timeLimitSeconds = 0f;
        public List<ExamQuestion> questions = new List<ExamQuestion>();

        public ExamQuestion Find(string id)
        {
            if (questions == null || string.IsNullOrEmpty(id)) return null;
            foreach (var q in questions) if (q != null && q.id == id) return q;
            return null;
        }
    }

    [CreateAssetMenu(fileName = "ExamBankDatabase", menuName = "Nisit/Exam Bank Database")]
    public class ExamBankDatabase : ScriptableObject
    {
        public const string ResourcePath = "ExamBanks/ExamBankDatabase";

        [Header("ค่าเริ่มต้นต่อการสอบ (ปรับแยกวิชาได้ที่ CourseExamBank)")]
        public int defaultQuestionsPerExam = 5;
        public float defaultTimeLimitSeconds = 180f;

        [Header("คำใบ้ตามความรู้ของวิชาที่สอบ (0–100 = สัดส่วนการเรียนรายวิชา)")]
        [Tooltip("ความรู้ตั้งแต่ค่านี้ได้คำใบ้ 1 ครั้ง")] public float oneHintFrom = 40f;
        [Tooltip("ความรู้ตั้งแต่ค่านี้ได้คำใบ้ 2 ครั้ง")] public float twoHintsFrom = 70f;

        [Header("สิทธิ์สอบ")]
        [Tooltip("สัดส่วนการเรียนขั้นต่ำก่อนเข้าสอบ 0–1 (0 = ไม่มีเงื่อนไข · ระบบเดิมไม่มีเกณฑ์นี้ จึงปิดไว้)")]
        [Range(0f, 1f)] public float minStudyRatioToSit = 0f;
        [Tooltip("หมวดวิชาที่ใช้การประเมินแบบเดิม (ไม่บังคับเป็นมินิเกม)")]
        public List<CourseCategory> legacyCategories = new List<CourseCategory> { CourseCategory.Project, CourseCategory.Internship };

        public List<CourseExamBank> banks = new List<CourseExamBank>();

        public CourseExamBank Get(string courseCode)
        {
            if (banks == null || string.IsNullOrEmpty(courseCode)) return null;
            foreach (var b in banks) if (b != null && b.courseCode == courseCode) return b;
            return null;
        }

        public bool HasContent(string courseCode)
        {
            var b = Get(courseCode);
            return b != null && b.questions != null && b.questions.Count > 0;
        }

        public bool UsesLegacyAssessment(CourseDefinition def) =>
            def != null && legacyCategories != null && legacyCategories.Contains(def.category);

        public int QuestionsFor(CourseExamBank b)
        {
            int n = b != null && b.questionsPerExam > 0 ? b.questionsPerExam : defaultQuestionsPerExam;
            int have = b != null && b.questions != null ? b.questions.Count : 0;
            return Mathf.Clamp(n, 0, have);
        }

        public float TimeFor(CourseExamBank b) =>
            b != null && b.timeLimitSeconds > 0f ? b.timeLimitSeconds : Mathf.Max(10f, defaultTimeLimitSeconds);

        // ตรวจความถูกต้องของข้อมูล (Editor tool + เทสต์) — คืนรายการปัญหา (ว่าง = ผ่าน)
        public List<string> Validate(CurriculumDefinition curriculum = null)
        {
            var errs = new List<string>();
            var ids = new HashSet<string>();
            var codes = new HashSet<string>();
            foreach (var b in banks)
            {
                if (b == null || string.IsNullOrEmpty(b.courseCode)) { errs.Add("มีคลังที่ไม่มีรหัสวิชา"); continue; }
                if (!codes.Add(b.courseCode)) errs.Add($"คลังวิชา {b.courseCode} ซ้ำ");
                if (curriculum != null && curriculum.Get(b.courseCode) == null) errs.Add($"{b.courseCode}: ไม่พบรหัสวิชานี้ในหลักสูตร");
                foreach (var q in b.questions)
                {
                    if (q == null || string.IsNullOrEmpty(q.id)) { errs.Add($"{b.courseCode}: มีข้อที่ไม่มีรหัส"); continue; }
                    if (!ids.Add(q.id)) errs.Add($"รหัสข้อซ้ำ {q.id}");
                    if (string.IsNullOrEmpty(q.prompt)) errs.Add($"{q.id}: ไม่มีคำถาม");
                    if (q.points <= 0f) errs.Add($"{q.id}: คะแนนเต็มต้องมากกว่า 0");
                    int n = q.items != null ? q.items.Count : 0;
                    switch (q.type)
                    {
                        case ExamQuestionType.MultipleChoice:
                        case ExamQuestionType.FindError:
                            if (n < 2) errs.Add($"{q.id}: ต้องมีตัวเลือกอย่างน้อย 2");
                            if (q.correctIndex < 0 || q.correctIndex >= n) errs.Add($"{q.id}: correctIndex เกินช่วง");
                            break;
                        case ExamQuestionType.Ordering:
                            if (n < 2) errs.Add($"{q.id}: ต้องมีบล็อกอย่างน้อย 2");
                            if (q.alternateOrders != null)
                                foreach (var alt in q.alternateOrders)
                                    if (ExamMinigameLogic.ParseOrder(alt, n) == null) errs.Add($"{q.id}: ลำดับทางเลือก \"{alt}\" ไม่ถูกต้อง");
                            break;
                        case ExamQuestionType.Matching:
                            if (n < 2) errs.Add($"{q.id}: ต้องมีคู่อย่างน้อย 2");
                            if (q.matches == null || q.matches.Count != n) errs.Add($"{q.id}: จำนวนฝั่งขวาไม่เท่าฝั่งซ้าย");
                            break;
                    }
                }
            }
            return errs;
        }

        static ExamBankDatabase _cache;
        // โหลดจาก Resources · ไม่มี asset → ใช้ค่าเริ่มต้นจากโค้ด (เหมือน CurriculumDefinition.LoadDefault)
        public static ExamBankDatabase LoadDefault()
        {
            if (_cache != null) return _cache;
            _cache = Resources.Load<ExamBankDatabase>(ResourcePath);
            if (_cache == null)
            {
                Debug.LogWarning("[ExamBank] ไม่พบ Resources/" + ResourcePath + " — ใช้คลังข้อสอบค่าเริ่มต้นจากโค้ด (กด Nisit ▸ Build Exam Minigame Banks เพื่อสร้าง asset ที่แก้ใน Inspector ได้)");
                _cache = ExamBankDefaults.Create();
            }
            return _cache;
        }
    }
}
