using System;
using System.Collections.Generic;
using UnityEngine;

namespace NisitSimulator.Academics
{
    // หมวดรายวิชา — ใช้แยกวิชาบังคับ/วิชาเลือก/ฝึกงาน/โครงงาน ตอนตรวจจบการศึกษา
    public enum CourseCategory { GeneralEducation, Core, Elective, Internship, Project }

    // หนึ่งคาบเรียนในตาราง — "วัน" คือวันที่ในภาคเรียน (ภาคต้น/ปลายยาว 3 วันตาม AcademicCalendar)
    //   เวลาเป็นนาทีนับจากเที่ยงคืนของนาฬิกาเกม (GameClock.TotalMinutes) · building ต้องตรงกับชื่อตึกใน Interiors (Spawn_<ชื่อ>)
    [Serializable]
    public class ClassSession
    {
        [Tooltip("วันที่ในภาคเรียน (1 = วันแรกของภาค)")]
        public int day = 1;
        [Tooltip("เวลาเริ่ม (นาทีนับจาก 00:00) เช่น 540 = 09:00")]
        public int startMinute = 540;
        [Tooltip("เวลาจบ (นาทีนับจาก 00:00) เช่น 660 = 11:00")]
        public int endMinute = 660;
        [Tooltip("ชื่อตึกให้ตรงกับ Spawn_<ชื่อ> ในฉาก เช่น อาคารเรียน / คณะ IT / อาคารบริหาร")]
        public string building = "คณะ IT";
        [Tooltip("ชื่อห้องที่แสดง (ข้อมูลเดิม) — ห้องจริงใช้ roomId")]
        public string room = "";
        [Tooltip("รหัสห้องใน ClassroomCatalog (เช่น IT-201) — เข้าเรียนนับเฉพาะใน ClassroomZone ที่ตรงกัน · ว่าง = ใช้การเช็กตึกแบบเดิม")]
        public string roomId = "";

        public ClassSession() { }
        public ClassSession(int day, int startHour, int endHour, string building, string room)
        {
            this.day = day; startMinute = startHour * 60; endMinute = endHour * 60;
            this.building = building; this.room = room;
        }
        public ClassSession(int day, int startHour, int endHour, string building, string room, string roomId)
            : this(day, startHour, endHour, building, room) { this.roomId = roomId ?? ""; }

        public bool HasRoom => !string.IsNullOrEmpty(roomId);

        public float Hours => Mathf.Max(0, endMinute - startMinute) / 60f;
        public int MaxTicks => Mathf.Max(1, Mathf.CeilToInt(Hours - 0.001f));   // จุดเรียนให้ผลทุก 60 นาทีเกม
        public bool Overlaps(ClassSession o) =>
            o != null && day == o.day && startMinute < o.endMinute && o.startMinute < endMinute;
        public string TimeText => $"{startMinute / 60:00}:{startMinute % 60:00}–{endMinute / 60:00}:{endMinute % 60:00}";
        public string ShortText => $"{NisitSimulator.Systems.AcademicCalendar.ShortTermDayText(day)} {TimeText} {(HasRoom ? roomId : building)}";
    }

    [Serializable]
    public class CourseDefinition
    {
        [Tooltip("รหัสวิชา = ตัวอ้างอิงถาวร (เซฟอ้างรหัสนี้ ห้ามเปลี่ยนหลังปล่อยเกม)")]
        public string code;
        public string title;
        public int credits = 3;
        public CourseCategory category = CourseCategory.Core;
        [Tooltip("ชั้นปีในแผน (0 = วิชาเลือก ไม่ผูกภาค)")]
        public int planYear;
        [Tooltip("ภาคเรียนในแผน 1/2 (0 = วิชาเลือก)")]
        public int planSemester;
        [Tooltip("ต้องผ่านครบทุกวิชา")]
        public List<string> prerequisites = new List<string>();
        [Tooltip("หน่วยกิตสะสม (ที่ประกาศผลแล้ว) ขั้นต่ำก่อนลง")]
        public int minEarnedCredits;
        [Tooltip("ตอนเรียนปกติ (ภาคตามแผน)")]
        public List<ClassSession> sessions = new List<ClassSession>();
        [Tooltip("ตอนเรียนซ้ำ/เก็บตก (ภาคค่ำ) — ใช้เมื่อลงนอกภาคตามแผน กันชนกับวิชาในแผนปัจจุบัน")]
        public List<ClassSession> retakeSessions = new List<ClassSession>();

        public bool IsElective => category == CourseCategory.Elective;
        public bool IsRequired => !IsElective;
        public string PlanLabel => planYear > 0 ? $"{planYear}/{planSemester}" : "เลือก";
    }

    [Serializable]
    public class GradeStep
    {
        public string letter;
        [Tooltip("คะแนนรวมขั้นต่ำ (0-100)")]
        public float minScore;
        public float point;
        public GradeStep() { }
        public GradeStep(string l, float min, float p) { letter = l; minScore = min; point = p; }
    }

    // หลักสูตร (data) — แยกจากตรรกะ · เก็บใน Resources/Curricula ให้โหลดตอนรันได้
    //   สร้าง/รีเซ็ตด้วย Editor tool: Nisit -> Build CS Curriculum (ลงทะเบียนเรียน)
    [CreateAssetMenu(fileName = "CS_Curriculum", menuName = "Nisit/Curriculum")]
    public class CurriculumDefinition : ScriptableObject
    {
        public const string ResourcePath = "Curricula/CS_Curriculum";

        public string programName = "หลักสูตรวิทยาการคอมพิวเตอร์ (สมมติ)";
        [Tooltip("คณะใน FacultyCatalog ที่ใช้หลักสูตรนี้ (0 = เทคโนโลยีสารสนเทศ ซึ่งเป็นคณะสายคอมพิวเตอร์ในเกม)")]
        public int[] facultyIndices = { 0 };

        [Header("การลงทะเบียน")]
        public int creditCapPerTerm = 18;
        [Tooltip("เปิดลงทะเบียน/เพิ่ม-ถอนได้กี่วันแรกของภาค")]
        public int registrationDays = 1;
        [Tooltip("หมดช่วงลงทะเบียนแล้วยังไม่กดยืนยัน → ยืนยันรายการที่เลือกไว้ให้อัตโนมัติ (ถ้าผ่านทุกเงื่อนไข)")]
        public bool autoConfirmAtDeadline = false;
        [Tooltip("วิชาเลือกเปิดตั้งแต่ชั้นปีนี้")]
        public int electivesFromYear = 3;

        [Header("เลื่อนชั้นปี (หน่วยกิตผ่านสะสมขั้นต่ำเพื่อขึ้นปี 2,3,4)")]
        public int[] promotionCredits = { 24, 54, 84 };

        [Header("จบการศึกษา")]
        public int electivesRequired = 2;
        public float minGraduationGpa = 2.00f;
        [Tooltip("จำนวนภาคปกติสูงสุด (รวมภาคเพิ่มเติม) ก่อนพ้นสภาพ — 16 = 8 ปี")]
        public int maxRegularTerms = 16;

        [Header("เกรด (เรียงจากสูงไปต่ำ)")]
        [Tooltip("แต้มขั้นต่ำที่นับว่าผ่าน (D = 1.0)")]
        public float passPoint = 1f;
        public List<GradeStep> gradeScale = new List<GradeStep>();

        [Header("สัดส่วนคะแนนรายวิชา")]
        public float studyWeight = 0.30f;
        public float midtermWeight = 0.30f;
        public float finalWeight = 0.40f;
        [Tooltip("คะแนนสอบ = ตอบถูก × ค่านี้ + ความพร้อม (การเข้าเรียน) × ส่วนที่เหลือ — ตรงกับสูตรเดิมของ ExamController")]
        public float examQuizWeight = 0.60f;
        [Tooltip("อ่านหนังสือนอกคาบ → นับเป็นชั่วโมงเรียนของวิชาที่ตามหลังสุดกี่เท่า")]
        public float selfStudyShare = 0.5f;
        [Tooltip("อ่านเองชดเชยได้สูงสุดกี่ส่วนของชั่วโมงเรียนทั้งหมด")]
        public float selfStudyCap = 0.4f;

        public List<CourseDefinition> courses = new List<CourseDefinition>();

        [NonSerialized] Dictionary<string, CourseDefinition> _map;

        void OnValidate() { _map = null; }

        public CourseDefinition Get(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            if (_map == null || _map.Count != (courses != null ? courses.Count : 0))
            {
                _map = new Dictionary<string, CourseDefinition>();
                if (courses != null)
                    foreach (var c in courses)
                        if (c != null && !string.IsNullOrEmpty(c.code) && !_map.ContainsKey(c.code)) _map.Add(c.code, c);
            }
            _map.TryGetValue(code, out var d);
            return d;
        }

        public bool AppliesToFaculty(int facultyIndex)
        {
            if (facultyIndices == null) return false;
            foreach (var f in facultyIndices) if (f == facultyIndex) return true;
            return false;
        }

        public int RequiredCredits
        {
            get { int s = 0; foreach (var c in courses) if (c != null && c.IsRequired) s += c.credits; return s; }
        }

        public int GraduationCredits
        {
            get
            {
                int elective = 0, n = 0;
                var list = new List<int>();
                foreach (var c in courses) if (c != null && c.IsElective) list.Add(c.credits);
                list.Sort();
                for (int i = 0; i < list.Count && n < electivesRequired; i++, n++) elective += list[i];
                return RequiredCredits + elective;
            }
        }

        public List<CourseDefinition> PlanCourses(int year, int sem)
        {
            var list = new List<CourseDefinition>();
            foreach (var c in courses) if (c != null && c.planYear == year && c.planSemester == sem) list.Add(c);
            return list;
        }

        public GradeStep GradeFor(float score)
        {
            GradeStep best = null;
            foreach (var g in gradeScale)
                if (score + 1e-4f >= g.minScore && (best == null || g.minScore > best.minScore)) best = g;
            if (best != null) return best;
            // ไม่มีขั้นไหนรับ = ต่ำสุด
            GradeStep low = null;
            foreach (var g in gradeScale) if (low == null || g.minScore < low.minScore) low = g;
            return low ?? new GradeStep("F", 0f, 0f);
        }

        public int PromotionThreshold(int fromYear)
        {
            int i = fromYear - 1;
            if (promotionCredits == null || i < 0 || i >= promotionCredits.Length) return int.MaxValue;
            return promotionCredits[i];
        }

        // ตรวจความถูกต้องของข้อมูลหลักสูตร (ใช้ใน Editor tool + เทสต์) — คืนรายการปัญหา (ว่าง = ผ่าน)
        public List<string> Validate()
        {
            var errs = new List<string>();
            var seen = new HashSet<string>();
            foreach (var c in courses)
            {
                if (c == null || string.IsNullOrEmpty(c.code)) { errs.Add("มีรายวิชาที่ไม่มีรหัส"); continue; }
                if (!seen.Add(c.code)) errs.Add($"รหัสซ้ำ {c.code}");
                foreach (var p in c.prerequisites) if (Get(p) == null) errs.Add($"{c.code}: ไม่พบวิชาบังคับก่อน {p}");
                if (c.sessions == null || c.sessions.Count == 0) errs.Add($"{c.code}: ไม่มีตารางเรียน");
                if (c.IsRequired && (c.retakeSessions == null || c.retakeSessions.Count == 0)) errs.Add($"{c.code}: ไม่มีตอนเรียนซ้ำ");
            }
            // แผนแต่ละภาคต้องไม่ชนกันเอง และไม่เกินเพดาน
            for (int y = 1; y <= 4; y++)
                for (int s = 1; s <= 2; s++)
                {
                    var plan = PlanCourses(y, s);
                    int cr = 0; foreach (var c in plan) cr += c.credits;
                    if (cr > creditCapPerTerm) errs.Add($"แผน {y}/{s} รวม {cr} หน่วยกิต เกินเพดาน {creditCapPerTerm}");
                    for (int i = 0; i < plan.Count; i++)
                        for (int j = i + 1; j < plan.Count; j++)
                            if (SessionsConflict(plan[i].sessions, plan[j].sessions))
                                errs.Add($"แผน {y}/{s}: {plan[i].code} ชนเวลากับ {plan[j].code}");
                }
            return errs;
        }

        public static bool SessionsConflict(List<ClassSession> a, List<ClassSession> b)
        {
            if (a == null || b == null) return false;
            foreach (var x in a) foreach (var y in b) if (x.Overlaps(y)) return true;
            return false;
        }

        static CurriculumDefinition _cache;
        // โหลดจาก Resources · ถ้าไม่มี asset (ยังไม่กด Build) ใช้ค่าเริ่มต้นจากโค้ดแทน พร้อมเตือน
        public static CurriculumDefinition LoadDefault()
        {
            if (_cache != null) return _cache;
            _cache = Resources.Load<CurriculumDefinition>(ResourcePath);
            if (_cache == null)
            {
                Debug.LogWarning("[Curriculum] ไม่พบ Resources/" + ResourcePath + " — ใช้หลักสูตรค่าเริ่มต้นจากโค้ด (กด Nisit ▸ Build CS Curriculum เพื่อสร้าง asset ที่แก้ใน Inspector ได้)");
                _cache = CsCurriculumDefaults.Create();
            }
            return _cache;
        }
    }
}
