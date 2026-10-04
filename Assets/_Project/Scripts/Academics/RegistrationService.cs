using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NisitSimulator.Academics
{
    public enum CourseStatus { CanRegister, MissingPrerequisite, Passed, Selected }

    public enum OfferGroup { Plan, Outstanding, Elective }

    // วิชาที่เปิดให้ลงในภาคนี้ (พร้อมตอนเรียนที่ใช้)
    public class OfferedCourse
    {
        public CourseDefinition def;
        public OfferGroup group;
        public bool retakeSection;
        public List<ClassSession> Sessions =>
            retakeSection && def.retakeSessions != null && def.retakeSessions.Count > 0 ? def.retakeSessions : def.sessions;
    }

    public class TermCloseResult
    {
        public TermState term;
        public List<Enrollment> graded = new List<Enrollment>();
        public float termGpa;
        public bool promoted;
        public int newClassYear;
        public bool graduated;
        public bool exhausted;        // เรียนเกินจำนวนภาคสูงสุด → พ้นสภาพ
    }

    public class StudyTickResult
    {
        public Enrollment attended;       // เข้าเรียนวิชานี้
        public ClassSession session;
        public int tickNo, tickMax;
        public bool capped;               // ชั่วโมงของคาบนี้ครบแล้ว
        public string wrongBuildingCode;  // มีคาบอยู่แต่ผิดตึก
        public string wrongBuildingTarget;
        public string wrongRoomCode;      // มีคาบอยู่แต่ผิดห้อง/อยู่นอกห้องเรียน (โหมดห้องเรียน)
        public string wrongRoomTarget;    // roomId ของคาบ
        public int sessionIndex = -1;     // ลำดับคาบของวิชาที่นับ (ใช้กับคีย์ "วัน:ลำดับ")
        public float quality;             // คุณภาพที่นับจริงหลังปรับด้วยเหตุการณ์
        public Enrollment selfStudy;      // นับเป็นอ่านทบทวนให้วิชานี้
    }

    // ปัญหาข้อมูลห้องเรียน (ห้องชน / ไม่มีห้อง / ห้องไม่มีอยู่จริง) — ตรรกะล้วน ใช้ทั้ง Editor และตอนรัน
    public class RoomIssue
    {
        public enum Kind { Conflict, MissingRoom, UnknownRoom }
        public Kind kind;
        public string codeA, codeB;
        public bool retakeA, retakeB;
        public int indexA, indexB;
        public string roomId;
        public int day, startA, endA, startB, endB;

        static string T(int a, int b) => $"{a / 60:00}:{a % 60:00}–{b / 60:00}:{b % 60:00}";
        static string Sec(bool retake, int idx) => (retake ? "ภาคค่ำ" : "ปกติ") + " คาบ " + (idx + 1);
        public override string ToString()
        {
            switch (kind)
            {
                case Kind.Conflict:
                    return $"ห้องชน {roomId} วันที่ {day}: {codeA} ({Sec(retakeA, indexA)} {T(startA, endA)}) ทับ {codeB} ({Sec(retakeB, indexB)} {T(startB, endB)})";
                case Kind.MissingRoom:
                    return $"{codeA} ({Sec(retakeA, indexA)} วันที่ {day} {T(startA, endA)}): ไม่มี roomId";
                default:
                    return $"{codeA} ({Sec(retakeA, indexA)} วันที่ {day} {T(startA, endA)}): ไม่พบห้อง {roomId} ใน ClassroomCatalog";
            }
        }
    }

    // ===== รายงานผลตอนจบเทอม (ตรรกะล้วน — TermResultUI แสดงผล) =====
    public class TermReportRow
    {
        public string code, title;
        public int credits, attempt;
        public float attendance;                 // 0..1 (เข้าเรียน + อ่านทบทวนชดเชย)
        public float midterm = -1f, final = -1f; // 0..1 · -1 = ไม่มีคะแนน
        public bool missedMidterm, missedFinal;
        public float bonusMid, bonusFinal;       // คะแนนพิเศษจากเหตุการณ์ในคาบ
        public float score;                      // 0..100
        public string letter;
        public float point;
        public bool passed;
        public bool retakeSection;
    }

    public class TermReport
    {
        public int serial, calendarYear, semIndex, planSemester, classYear;
        public bool isExtra;
        public List<TermReportRow> rows = new List<TermReportRow>();
        public float termGpa, cumulativeGpa;
        public bool hasGpa;
        public int creditsAttempted, creditsEarnedTerm, creditsEarnedTotal, graduationCredits;
        public bool promoted;
        public int newClassYear;
        public int promotionShortfall;           // > 0 = จบภาค 2 แล้วไม่เลื่อนชั้น ขาดอีกกี่หน่วยกิต
        public bool probation;                   // GPA สะสมต่ำกว่าเกณฑ์
        public float minGpa;
        public bool graduated, exhausted;
        public List<string> retakeCodes = new List<string>();   // ตก → ต้องลงซ้ำ (ภาคค่ำ)
        public string nextStep = "";
        public bool Empty => rows.Count == 0;
    }

    public class GraduationStatus
    {
        public bool eligible;
        public List<string> reasons = new List<string>();
    }

    // ===== ตรรกะลงทะเบียน/เกรด/จบการศึกษา (C# ล้วน ไม่ผูกฉาก — ทดสอบด้วย EditMode test ได้) =====
    public class RegistrationService
    {
        public readonly CurriculumDefinition Curriculum;
        public readonly AcademicRecord Record;
        public int CreditCapOverride;   // > 0 = ใช้แทนค่าในหลักสูตร (ตั้งจาก Inspector ของ CourseRegistrar)

        public RegistrationService(CurriculumDefinition curriculum, AcademicRecord record)
            : this(curriculum, record, null) { }

        // catalog != null → ตรวจห้องชน/ห้องไม่มีอยู่จริงตอนสร้าง: วิชาที่ข้อมูลห้องเสีย log error และลงทะเบียนไม่ได้ (เกมไม่พัง)
        public RegistrationService(CurriculumDefinition curriculum, AcademicRecord record, ClassroomCatalog catalog)
        {
            Curriculum = curriculum;
            Record = record ?? new AcademicRecord();
            Catalog = catalog;
            if (catalog != null && curriculum != null)
            {
                RoomIssues = FindRoomConflicts(curriculum, catalog, requireRooms: false);
                foreach (var i in RoomIssues)
                {
                    Debug.LogError("[Registration] ข้อมูลห้องเรียนผิด — " + i + " → ปิดการลงทะเบียนวิชาที่เกี่ยวข้อง");
                    if (i.codeA != null) BlockedCodes.Add(i.codeA);
                    if (i.codeB != null) BlockedCodes.Add(i.codeB);
                }
            }
        }

        public readonly ClassroomCatalog Catalog;
        public readonly List<RoomIssue> RoomIssues = new List<RoomIssue>();
        public readonly HashSet<string> BlockedCodes = new HashSet<string>();   // วิชาที่ข้อมูลห้องชน/ผิด → ห้ามลง

        // ปรับคุณภาพชั่วโมงเรียนก่อนบันทึก (เหตุการณ์ระหว่างเรียน) — (รหัสวิชา, q) → q ใหม่ · null = ไม่ปรับ
        public System.Func<string, float, float> QualityModifier;

        public const float ClassBonusCap = 0.10f;   // คะแนนพิเศษจากเหตุการณ์ระหว่างเรียน สูงสุดต่อการสอบหนึ่งครั้ง

        public int CreditCap => CreditCapOverride > 0 ? CreditCapOverride : Curriculum.creditCapPerTerm;
        public TermState Term => Record.Current;

        // ============================================================
        // ผลการเรียน (อ่านอย่างเดียว)
        // ============================================================
        public bool IsPassed(string code)
        {
            foreach (var e in Record.enrollments) if (e.code == code && e.graded && e.passed) return true;
            return false;
        }

        public HashSet<string> PassedCodes()
        {
            var set = new HashSet<string>();
            foreach (var e in Record.enrollments) if (e.graded && e.passed) set.Add(e.code);
            return set;
        }

        // หน่วยกิตสะสม = วิชาที่ผ่าน นับรหัสละครั้งเดียว
        public int EarnedCredits()
        {
            int s = 0;
            foreach (var code in PassedCodes()) { var d = Curriculum.Get(code); if (d != null) s += d.credits; }
            return s;
        }

        public int PassedRequiredCredits()
        {
            int s = 0;
            foreach (var code in PassedCodes()) { var d = Curriculum.Get(code); if (d != null && d.IsRequired) s += d.credits; }
            return s;
        }

        public int PassedElectiveCount()
        {
            int n = 0;
            foreach (var code in PassedCodes()) { var d = Curriculum.Get(code); if (d != null && d.IsElective) n++; }
            return n;
        }

        public List<Enrollment> Attempts(string code)
        {
            var l = new List<Enrollment>();
            foreach (var e in Record.enrollments) if (e.code == code) l.Add(e);
            return l;
        }

        // เกรดล่าสุดที่ประกาศแล้วของแต่ละวิชา (ไม่รวมเทียบโอน)
        public Dictionary<string, Enrollment> LatestGraded()
        {
            var map = new Dictionary<string, Enrollment>();
            foreach (var e in Record.enrollments)
            {
                if (!e.graded || e.transfer) continue;
                if (!map.TryGetValue(e.code, out var old) || e.termSerial > old.termSerial ||
                    (e.termSerial == old.termSerial && e.attempt > old.attempt))
                    map[e.code] = e;
            }
            return map;
        }

        public int GpaCredits()
        {
            int c = 0;
            foreach (var kv in LatestGraded()) { var d = Curriculum.Get(kv.Key); if (d != null) c += d.credits; }
            return c;
        }

        public bool HasGpa => GpaCredits() > 0;

        // GPA ถ่วงหน่วยกิต — ยังไม่มีเกรดคืน 0 (ไม่หารศูนย์)
        public float Gpa()
        {
            float sum = 0f; int cr = 0;
            foreach (var kv in LatestGraded())
            {
                var d = Curriculum.Get(kv.Key); if (d == null) continue;
                sum += kv.Value.point * d.credits; cr += d.credits;
            }
            return cr > 0 ? sum / cr : 0f;
        }

        public float TermGpa(int serial)
        {
            float sum = 0f; int cr = 0;
            foreach (var e in Record.enrollments)
            {
                if (e.termSerial != serial || !e.graded || e.transfer) continue;
                var d = Curriculum.Get(e.code); if (d == null) continue;
                sum += e.point * d.credits; cr += d.credits;
            }
            return cr > 0 ? sum / cr : 0f;
        }

        public List<Enrollment> CurrentEnrollments()
        {
            var l = new List<Enrollment>();
            var t = Term;
            if (t == null || t.isBreak || !t.confirmed) return l;
            foreach (var e in Record.enrollments) if (e.termSerial == t.serial && !e.transfer) l.Add(e);
            return l;
        }

        public bool HasActiveEnrollments
        {
            get { var t = Term; return t != null && !t.closed && CurrentEnrollments().Count > 0; }
        }

        // ============================================================
        // วงจรภาคเรียน
        // ============================================================
        public TermState OpenTerm(int calendarYear, int semIndex, bool lateRegistration = false)
        {
            bool isBreak = semIndex >= 2;
            if (!isBreak) Record.regularTermsStarted++;
            var t = new TermState
            {
                serial = Record.regularTermsStarted,
                calendarYear = calendarYear,
                semIndex = semIndex,
                classYear = Record.classYear,
                planSemester = isBreak ? 0 : semIndex + 1,
                isBreak = isBreak,
                isExtra = !isBreak && Record.finishedPlan,
                registrationOpen = !isBreak && !Record.graduated,
                lateRegistration = lateRegistration,
            };
            Record.current = t;
            Record.hasCurrent = true;
            return t;
        }

        // ปิดภาค: ประกาศเกรด → เลื่อนชั้นปี → ตรวจจบ · เรียกซ้ำได้ (ครั้งที่สองไม่ทำอะไร)
        public TermCloseResult CloseCurrentTerm()
        {
            var t = Term;
            if (t == null || t.closed) return null;
            t.closed = true;
            t.registrationOpen = false;
            var res = new TermCloseResult { term = t, newClassYear = Record.classYear };
            if (t.isBreak) return res;

            if (t.confirmed)
                foreach (var e in EnrollmentsOfTerm(t))
                    if (!e.graded) { Grade(e); res.graded.Add(e); }
            res.termGpa = TermGpa(t.serial);

            // เลื่อนชั้นปี: จบภาค 2 ของปีปัจจุบัน + หน่วยกิตถึงเกณฑ์
            if (t.planSemester == 2 && !t.isExtra)
            {
                if (Record.classYear < 4 && EarnedCredits() >= Curriculum.PromotionThreshold(Record.classYear))
                {
                    Record.classYear++;
                    res.promoted = true;
                }
                else if (t.classYear >= 4) Record.finishedPlan = true;
            }
            res.newClassYear = Record.classYear;

            if (!Record.graduated && CheckGraduation().eligible)
            {
                Record.graduated = true;
                res.graduated = true;
            }
            else if (!Record.graduated && Record.regularTermsStarted >= Curriculum.maxRegularTerms)
                res.exhausted = true;
            return res;
        }

        List<Enrollment> EnrollmentsOfTerm(TermState t)
        {
            var l = new List<Enrollment>();
            foreach (var e in Record.enrollments) if (e.termSerial == t.serial && !e.transfer) l.Add(e);
            return l;
        }

        public bool RegistrationWindowOpen(int semDay)
        {
            var t = Term;
            if (t == null || t.isBreak || t.closed || t.confirmed || !t.registrationOpen) return false;
            return t.lateRegistration || semDay <= Mathf.Max(1, Curriculum.registrationDays);
        }

        // หมดช่วงลงทะเบียน → ยืนยันอัตโนมัติ (ถ้าเลือกไว้และผ่านเงื่อนไข) หรือปิด · คืน true ถ้ามีการเปลี่ยนแปลง
        public bool CheckRegistrationDeadline(int semDay, out string message)
        {
            message = null;
            var t = Term;
            if (t == null || t.isBreak || t.closed || t.confirmed || !t.registrationOpen || t.lateRegistration) return false;
            if (semDay <= Mathf.Max(1, Curriculum.registrationDays)) return false;

            if (Curriculum.autoConfirmAtDeadline && t.selected.Count > 0 && ValidateSelection(out _))
            {
                ConfirmInternal();
                message = $"หมดช่วงลงทะเบียน — ระบบยืนยันรายการที่เลือกไว้ให้ ({SelectedCredits()} หน่วยกิต)";
                return true;
            }
            t.registrationOpen = false;
            t.selected.Clear();
            message = "หมดช่วงลงทะเบียนแล้ว — ภาคนี้ไม่ได้ลงทะเบียนเรียน (รอภาคถัดไป)";
            return true;
        }

        // ============================================================
        // รายวิชาที่เปิด
        // ============================================================
        public List<OfferedCourse> Offered()
        {
            var list = new List<OfferedCourse>();
            var t = Term;
            if (t == null || t.isBreak || Record.graduated) return list;
            var added = new HashSet<string>();
            int cy = t.classYear, ps = t.planSemester;

            if (!t.isExtra)
                foreach (var d in Curriculum.PlanCourses(cy, ps))
                    if (added.Add(d.code)) list.Add(new OfferedCourse { def = d, group = OfferGroup.Plan, retakeSection = false });

            // วิชาค้าง: วิชาบังคับจากภาคก่อนหน้าในแผนที่ยังไม่ผ่าน (ตก หรือยังไม่เคยลง) — เปิดทุกภาค
            foreach (var d in Curriculum.courses)
            {
                if (d == null || !d.IsRequired || added.Contains(d.code) || IsPassed(d.code)) continue;
                bool earlier = t.isExtra || d.planYear < cy || (d.planYear == cy && d.planSemester < ps);
                if (!earlier) continue;
                bool regular = t.isExtra && d.planSemester == ps;   // ภาคเพิ่มเติม: ภาคเดียวกับแผนใช้ตอนปกติ
                added.Add(d.code);
                list.Add(new OfferedCourse { def = d, group = OfferGroup.Outstanding, retakeSection = !regular });
            }

            // วิชาเลือก: เปิดทั้งสองภาคตั้งแต่ชั้นปีที่กำหนด
            if (cy >= Curriculum.electivesFromYear || t.isExtra)
                foreach (var d in Curriculum.courses)
                    if (d != null && d.IsElective && added.Add(d.code))
                        list.Add(new OfferedCourse { def = d, group = OfferGroup.Elective, retakeSection = false });
            return list;
        }

        public OfferedCourse FindOffered(string code)
        {
            foreach (var o in Offered()) if (o.def.code == code) return o;
            return null;
        }

        public bool PrerequisitesMet(CourseDefinition d, out string reason)
        {
            reason = null;
            var missing = new List<string>();
            foreach (var p in d.prerequisites) if (!IsPassed(p)) missing.Add(p);
            int earned = EarnedCredits();
            var sb = new StringBuilder();
            if (missing.Count > 0)
            {
                sb.Append("ต้องผ่าน ").Append(string.Join(", ", missing)).Append(" ก่อน");
                var t = Term;
                if (t != null && !t.confirmed)
                    foreach (var m in missing)
                        if (t.selected.Contains(m)) { sb.Append(" (กำลังลง ").Append(m).Append(" อยู่ ยังไม่นับว่าผ่าน)"); break; }
            }
            if (d.minEarnedCredits > 0 && earned < d.minEarnedCredits)
            {
                if (sb.Length > 0) sb.Append(" และ");
                sb.Append($"ต้องมีหน่วยกิตสะสม ≥ {d.minEarnedCredits} (ตอนนี้ {earned})");
            }
            if (sb.Length == 0) return true;
            reason = sb.ToString();
            return false;
        }

        public CourseStatus StatusOf(OfferedCourse oc, out string reason)
        {
            reason = null;
            if (IsPassed(oc.def.code)) { reason = "ผ่านแล้ว ลงซ้ำไม่ได้"; return CourseStatus.Passed; }
            var t = Term;
            if (t != null && (t.selected.Contains(oc.def.code) || IsEnrolledThisTerm(oc.def.code)))
                return CourseStatus.Selected;
            if (!PrerequisitesMet(oc.def, out reason)) return CourseStatus.MissingPrerequisite;
            if (BlockedCodes.Contains(oc.def.code)) { reason = BlockedReason(oc.def.code); return CourseStatus.MissingPrerequisite; }
            // ลงได้ แต่บอกล่วงหน้าถ้าชน/เต็ม
            string conflict = ConflictWithSelected(oc);
            if (conflict != null) reason = $"ชนเวลากับ {conflict}";
            else if (t != null && SelectedCredits() + oc.def.credits > CreditCap) reason = $"เพิ่มแล้วจะเกินเพดาน {CreditCap} หน่วยกิต";
            return CourseStatus.CanRegister;
        }

        bool IsEnrolledThisTerm(string code)
        {
            var t = Term; if (t == null) return false;
            foreach (var e in Record.enrollments) if (e.termSerial == t.serial && e.code == code && !e.transfer) return true;
            return false;
        }

        public int SelectedCredits()
        {
            var t = Term; if (t == null) return 0;
            int s = 0;
            foreach (var c in t.selected) { var d = Curriculum.Get(c); if (d != null) s += d.credits; }
            return s;
        }

        string ConflictWithSelected(OfferedCourse oc)
        {
            var t = Term; if (t == null) return null;
            foreach (var code in t.selected)
            {
                if (code == oc.def.code) continue;
                var other = FindOffered(code);
                if (other == null) continue;
                foreach (var a in oc.Sessions)
                    foreach (var b in other.Sessions)
                        if (a.Overlaps(b)) return $"{code} ({b.ShortText})";
            }
            return null;
        }

        public bool CanAdd(string code, out string reason)
        {
            reason = null;
            var t = Term;
            if (t == null || t.isBreak) { reason = "ตอนนี้เป็นช่วงปิดภาค ยังไม่เปิดลงทะเบียน"; return false; }
            if (Record.graduated) { reason = "จบการศึกษาแล้ว"; return false; }
            if (t.confirmed) { reason = "ยืนยันการลงทะเบียนภาคนี้ไปแล้ว (ล็อกรายการ)"; return false; }
            if (!t.registrationOpen) { reason = "หมดช่วงลงทะเบียนของภาคนี้แล้ว"; return false; }
            var oc = FindOffered(code);
            if (oc == null) { reason = $"{code} ไม่เปิดให้ลงในภาคนี้"; return false; }
            if (t.selected.Contains(code)) { reason = $"เลือก {code} ไว้แล้ว (ห้ามเลือกรหัสซ้ำ)"; return false; }
            if (IsPassed(code)) { reason = $"{code} ผ่านแล้ว ลงซ้ำไม่ได้"; return false; }
            if (!PrerequisitesMet(oc.def, out var pr)) { reason = $"{code}: {pr}"; return false; }
            if (BlockedCodes.Contains(code)) { reason = BlockedReason(code); return false; }
            string conflict = ConflictWithSelected(oc);
            if (conflict != null) { reason = $"{code} ชนเวลากับ {conflict}"; return false; }
            int after = SelectedCredits() + oc.def.credits;
            if (after > CreditCap) { reason = $"เกินเพดาน {CreditCap} หน่วยกิต (เลือกแล้ว {SelectedCredits()} + {oc.def.credits} = {after})"; return false; }
            return true;
        }

        static string BlockedReason(string code) => $"{code}: ข้อมูลห้องเรียนชน/ไม่ถูกต้อง — ปิดการลงทะเบียนวิชานี้ชั่วคราว (แจ้งผู้พัฒนา)";

        public bool Add(string code, out string reason)
        {
            if (!CanAdd(code, out reason)) return false;
            Term.selected.Add(code);
            reason = $"เพิ่ม {code} แล้ว ({SelectedCredits()}/{CreditCap} หน่วยกิต)";
            return true;
        }

        public bool Remove(string code, out string reason)
        {
            var t = Term;
            if (t == null || !t.selected.Contains(code)) { reason = $"ไม่มี {code} ในรายการที่เลือก"; return false; }
            if (t.confirmed) { reason = "ยืนยันแล้ว ถอนรายการไม่ได้"; return false; }
            if (!t.registrationOpen) { reason = "หมดช่วงลงทะเบียนแล้ว"; return false; }
            t.selected.Remove(code);
            reason = $"ถอน {code} แล้ว ({SelectedCredits()}/{CreditCap} หน่วยกิต)";
            return true;
        }

        // ตรวจทุกเงื่อนไขของทั้งชุดก่อนบันทึก
        public bool ValidateSelection(out string reason)
        {
            reason = null;
            var t = Term;
            if (t == null || t.isBreak) { reason = "ยังไม่เปิดภาคเรียน"; return false; }
            if (t.selected.Count == 0) { reason = "ยังไม่ได้เลือกรายวิชา"; return false; }
            var seen = new HashSet<string>();
            var offered = new List<OfferedCourse>();
            foreach (var code in t.selected)
            {
                if (!seen.Add(code)) { reason = $"เลือก {code} ซ้ำ"; return false; }
                var oc = FindOffered(code);
                if (oc == null) { reason = $"{code} ไม่เปิดในภาคนี้"; return false; }
                if (IsPassed(code)) { reason = $"{code} ผ่านแล้ว"; return false; }
                if (!PrerequisitesMet(oc.def, out var pr)) { reason = $"{code}: {pr}"; return false; }
                if (BlockedCodes.Contains(code)) { reason = BlockedReason(code); return false; }
                offered.Add(oc);
            }
            for (int i = 0; i < offered.Count; i++)
                for (int j = i + 1; j < offered.Count; j++)
                    if (CurriculumDefinition.SessionsConflict(offered[i].Sessions, offered[j].Sessions))
                    { reason = $"{offered[i].def.code} ชนเวลากับ {offered[j].def.code}"; return false; }
            if (SelectedCredits() > CreditCap) { reason = $"รวม {SelectedCredits()} หน่วยกิต เกินเพดาน {CreditCap}"; return false; }
            return true;
        }

        public bool Confirm(out string reason)
        {
            var t = Term;
            if (t != null && t.confirmed) { reason = "ยืนยันไปแล้ว — ไม่สร้างรายการซ้ำ"; return false; }
            if (t == null || !t.registrationOpen) { reason = "หมดช่วงลงทะเบียนแล้ว"; return false; }
            if (!ValidateSelection(out reason)) return false;
            ConfirmInternal();
            reason = $"ยืนยันลงทะเบียน {t.selected.Count} วิชา {SelectedCredits()} หน่วยกิต เรียบร้อย";
            return true;
        }

        void ConfirmInternal()
        {
            var t = Term;
            foreach (var code in t.selected)
            {
                if (IsEnrolledThisTerm(code)) continue;   // กันซ้ำ
                var oc = FindOffered(code);
                int attempt = Attempts(code).Count + 1;
                Record.enrollments.Add(new Enrollment
                {
                    code = code, attempt = attempt, termSerial = t.serial, calendarYear = t.calendarYear,
                    semIndex = t.semIndex, classYear = t.classYear, retakeSection = oc != null && oc.retakeSection,
                });
            }
            t.confirmed = true;
            t.registrationOpen = false;
        }

        // อธิบายว่าทำไมไม่มีวิชาให้ลง + ทางแก้ (ห้ามปล่อยหน้าว่าง) · null = มีวิชาให้ลง
        public string ExplainNoOptions(int semDay)
        {
            var t = Term;
            if (Record.graduated) return "คุณผ่านเงื่อนไขจบการศึกษาครบแล้ว";
            if (t == null) return "ยังไม่เริ่มภาคเรียน";
            if (t.isBreak) return "ภาคฤดูร้อนไม่เปิดสอนในหลักสูตรนี้ — รอเปิดลงทะเบียนวันแรกของภาคต้นปีการศึกษาถัดไป";
            if (t.confirmed) return null;
            if (!t.registrationOpen || !RegistrationWindowOpen(semDay))
                return "หมดช่วงลงทะเบียนของภาคนี้แล้ว — ใช้เวลานี้อ่านหนังสือ/ทำงาน แล้วลงทะเบียนวันแรกของภาคถัดไป";
            var offered = Offered();
            int can = 0; var blocked = new List<string>();
            foreach (var o in offered)
            {
                var st = StatusOf(o, out var r);
                if (st == CourseStatus.CanRegister || st == CourseStatus.Selected) can++;
                else if (st == CourseStatus.MissingPrerequisite) blocked.Add($"{o.def.code}: {r}");
            }
            if (can > 0) return null;
            if (offered.Count == 0) return "ไม่มีรายวิชาเปิดสำหรับชั้นปีนี้ — รอภาคถัดไป";
            var sb = new StringBuilder("ภาคนี้ยังไม่มีวิชาที่ลงได้\n");
            if (blocked.Count > 0)
            {
                sb.Append("สาเหตุ: ขาดวิชาบังคับก่อน\n");
                foreach (var b in blocked) sb.Append("- ").Append(b).Append('\n');
                sb.Append("ทางแก้: ลงเรียนวิชาค้างที่เป็นวิชาบังคับก่อนให้ผ่าน (เปิดทุกภาค ตอนภาคค่ำ) หรือรอประกาศผลภาคนี้");
            }
            else sb.Append("ทุกวิชาที่เปิดผ่านแล้ว — รอภาคถัดไป");
            return sb.ToString();
        }

        // ============================================================
        // เข้าเรียน / สอบ / เกรด
        // ============================================================
        public List<ClassSession> SessionsFor(Enrollment e)
        {
            var d = Curriculum.Get(e.code);
            if (d == null) return new List<ClassSession>();
            return e.retakeSection && d.retakeSessions != null && d.retakeSessions.Count > 0 ? d.retakeSessions : d.sessions;
        }

        public float ExpectedHours(Enrollment e, int uptoSemDay = int.MaxValue)
        {
            float h = 0f;
            foreach (var s in SessionsFor(e)) if (s.day <= uptoSemDay) h += s.MaxTicks;
            return h;
        }

        // สัดส่วนการเรียน 0..1 (เข้าเรียนจริง + อ่านเองชดเชยได้บางส่วน)
        public float StudyRatio(Enrollment e, int uptoSemDay = int.MaxValue)
        {
            float total = ExpectedHours(e);
            float req = ExpectedHours(e, uptoSemDay);
            if (req <= 0f) req = total;
            if (req <= 0f) return 1f;
            float self = Mathf.Min(e.selfStudy, Curriculum.selfStudyCap * total);
            return Mathf.Clamp01((e.progress + self) / req);
        }

        // จุดเรียนให้ผลหนึ่งครั้ง (ทุก 60 นาทีเกม) — quality = สัดส่วนความรู้ที่ได้จริงหลังหักความเครียด (0..1)
        public StudyTickResult RecordStudyTick(int semDay, float tickEndMinute, string building, float quality)
            => RecordStudyTick(semDay, tickEndMinute, building, null, quality, false);

        // roomMode = true (เล่นคนเดียว): คาบที่มี roomId นับเฉพาะเมื่อ roomId ของที่นั่งตรงกัน (null = นอกห้องเรียน = ไม่นับ)
        //   คาบที่ไม่มี roomId (หลักสูตรเก่า) ใช้การเช็กตึกแบบเดิม · roomMode = false = พฤติกรรมเดิมทุกประการ (multiplayer)
        public StudyTickResult RecordStudyTick(int semDay, float tickEndMinute, string building, string roomId, float quality, bool roomMode)
        {
            var res = new StudyTickResult();
            var t = Term;
            if (t == null || t.closed || !t.confirmed) return res;
            quality = Mathf.Clamp01(quality);
            float from = tickEndMinute - 60f;

            foreach (var e in CurrentEnrollments())
            {
                var ss = SessionsFor(e);
                for (int i = 0; i < ss.Count; i++)
                {
                    var s = ss[i];
                    if (s.day != semDay) continue;
                    if (!(tickEndMinute > s.startMinute && from < s.endMinute)) continue;   // ช่วงนั่งเรียนทับคาบ
                    if (roomMode && s.HasRoom)
                    {
                        if (roomId != s.roomId) { res.wrongRoomCode = e.code; res.wrongRoomTarget = s.roomId; continue; }
                    }
                    else if (!string.IsNullOrEmpty(building) && s.building != building)
                    {
                        res.wrongBuildingCode = e.code; res.wrongBuildingTarget = s.building;
                        continue;
                    }
                    string key = semDay + ":" + i;
                    int ticks = e.TicksFor(key);
                    if (ticks >= s.MaxTicks) { res.capped = true; continue; }
                    float q = QualityModifier != null ? Mathf.Clamp01(QualityModifier(e.code, quality)) : quality;
                    e.AddTick(key);
                    e.progress += q;
                    res.attended = e; res.session = s; res.tickNo = ticks + 1; res.tickMax = s.MaxTicks;
                    res.sessionIndex = i; res.quality = q;
                    return res;
                }
            }

            // นอกคาบ = อ่านทบทวน → ชดเชยให้วิชาที่ตามหลังที่สุด (มีเพดาน)
            if (res.wrongBuildingCode == null && res.wrongRoomCode == null && quality > 0f)
            {
                Enrollment worst = null; float worstRatio = 2f;
                foreach (var e in CurrentEnrollments())
                {
                    if (e.graded) continue;
                    float cap = Curriculum.selfStudyCap * ExpectedHours(e);
                    if (e.selfStudy >= cap) continue;
                    float r = StudyRatio(e, semDay);
                    if (r < worstRatio) { worstRatio = r; worst = e; }
                }
                if (worst != null)
                {
                    worst.selfStudy += quality * Curriculum.selfStudyShare;
                    res.selfStudy = worst;
                }
            }
            return res;
        }

        // บันทึกผลสอบให้ "ทุกวิชาที่ลงในภาคนี้" — คืนคะแนนสอบเฉลี่ย (0..1)
        //   onlyCodes != null → เฉพาะวิชาที่ระบุ และข้ามวิชาที่มีคะแนนรอบนี้แล้ว (สอบแบบเดิมของวิชาที่ไม่ใช้มินิเกม)
        public float RecordExam(bool final, float quizFraction, float bonus, int semDay, ICollection<string> onlyCodes = null)
        {
            var list = CurrentEnrollments();
            if (list.Count == 0) return 0f;
            float sum = 0f; int n = 0;
            foreach (var e in list)
            {
                if (e.graded) continue;
                if (onlyCodes != null && (!onlyCodes.Contains(e.code) || HasExamScore(e, final))) continue;
                float s = ExamScore(e, quizFraction, bonus, semDay, final);   // สูตรเดิม (ย้ายไปเป็นเมธอดให้มินิเกมใช้ร่วม)
                if (final) { e.final = s; e.missedFinal = false; } else { e.midterm = s; e.missedMidterm = false; }
                sum += s; n++;
            }
            return n > 0 ? sum / n : 0f;
        }

        // ขาดสอบ → 0 เฉพาะวิชาที่ยังไม่มีคะแนนรอบนี้ · คืนจำนวนวิชาที่ถูกบันทึกว่าขาดสอบ
        public int RecordMissedExam(bool final)
        {
            int n = 0;
            foreach (var e in CurrentEnrollments())
            {
                if (e.graded) continue;
                if (final && e.final < 0f) { e.final = 0f; e.missedFinal = true; n++; }
                if (!final && e.midterm < 0f) { e.midterm = 0f; e.missedMidterm = true; n++; }
            }
            return n;
        }

        // ===== สอบรายวิชา (มินิเกมสอบ — ExamMinigameController) =====
        public Enrollment CurrentEnrollment(string code)
        {
            foreach (var e in CurrentEnrollments()) if (e.code == code) return e;
            return null;
        }

        public static bool HasExamScore(Enrollment e, bool final) => e != null && (final ? e.final >= 0f : e.midterm >= 0f);

        // สูตรคะแนนสอบเดิม: ตอบถูก × examQuizWeight + ความพร้อม (การเข้าเรียนรายวิชา) × ส่วนที่เหลือ + โบนัส
        //   + คะแนนพิเศษจากเหตุการณ์ระหว่างเรียนของรอบนั้น (ค่าเริ่มต้น 0 → สูตรเดิมทุกประการ)
        public float ExamScore(Enrollment e, float quizFraction, float bonus, int semDay) =>
            ExamScore(e, quizFraction, bonus, semDay, NextExamIsFinal(e));

        public float ExamScore(Enrollment e, float quizFraction, float bonus, int semDay, bool final) =>
            Mathf.Clamp01(Curriculum.examQuizWeight * quizFraction + (1f - Curriculum.examQuizWeight) * StudyRatio(e, semDay) + bonus + ClassBonus(e, final));

        public static float ClassBonus(Enrollment e, bool final) =>
            e == null ? 0f : Mathf.Clamp(final ? e.classBonusFinal : e.classBonusMid, 0f, ClassBonusCap);

        // การสอบครั้งถัดไปของวิชานี้เป็นปลายภาคไหม (มีคะแนนกลางภาคแล้ว = ปลายภาค)
        public static bool NextExamIsFinal(Enrollment e) => e != null && e.midterm >= 0f;

        // เพิ่มคะแนนพิเศษ (มีเพดาน ClassBonusCap) — คืนค่าที่เพิ่มได้จริง
        public static float AddClassBonus(Enrollment e, bool final, float amount)
        {
            if (e == null || e.graded || amount <= 0f) return 0f;
            float cur = final ? e.classBonusFinal : e.classBonusMid;
            float next = Mathf.Min(ClassBonusCap, cur + amount);
            float added = Mathf.Max(0f, next - cur);
            if (final) e.classBonusFinal = next; else e.classBonusMid = next;
            return added;
        }

        // หาคาบของวิชาที่กำลังเรียน ณ เวลานั้น — คืนลำดับคาบ (-1 = ไม่มี)
        public int SessionIndexAt(Enrollment e, int semDay, float minute)
        {
            var ss = SessionsFor(e);
            for (int i = 0; i < ss.Count; i++)
                if (ss[i].day == semDay && minute >= ss[i].startMinute && minute < ss[i].endMinute) return i;
            return -1;
        }

        public static string MeetingKey(int semDay, int sessionIndex) => semDay + ":" + sessionIndex;

        // เลิกเรียนกะทันหัน (ไฟดับ/คอมค้าง): นับชั่วโมงที่เหลือของคาบนั้นให้เต็ม — คืนจำนวนชั่วโมงที่เพิ่ม
        public int CreditRemainingMeeting(Enrollment e, int semDay, int sessionIndex, float quality)
        {
            if (e == null || e.graded) return 0;
            var ss = SessionsFor(e);
            if (sessionIndex < 0 || sessionIndex >= ss.Count) return 0;
            string key = MeetingKey(semDay, sessionIndex);
            int added = 0;
            while (e.TicksFor(key) < ss[sessionIndex].MaxTicks)
            {
                e.AddTick(key);
                e.progress += Mathf.Clamp01(quality);
                added++;
            }
            return added;
        }

        // บันทึกผลสอบ "หนึ่งวิชา" ด้วยสูตรเดิม — คืน -1 ถ้าบันทึกไม่ได้ (ไม่ได้ลง/ประกาศเกรดแล้ว/มีคะแนนรอบนี้แล้ว = กันบันทึกซ้ำ)
        public float RecordCourseExam(string code, bool final, float quizFraction, float bonus, int semDay)
        {
            var e = CurrentEnrollment(code);
            if (e == null || e.graded || HasExamScore(e, final)) return -1f;
            float s = ExamScore(e, quizFraction, bonus, semDay, final);
            if (final) { e.final = s; e.missedFinal = false; } else { e.midterm = s; e.missedMidterm = false; }
            return s;
        }

        public float ProjectedScore(Enrollment e)
        {
            float ws = Curriculum.studyWeight, wm = Curriculum.midtermWeight, wf = Curriculum.finalWeight;
            float sum = ws + wm + wf; if (sum <= 0f) sum = 1f;
            float s = ws * StudyRatio(e) + wm * Mathf.Max(0f, e.midterm) + wf * Mathf.Max(0f, e.final);
            return 100f * s / sum;
        }

        public void Grade(Enrollment e)
        {
            if (e.graded) return;
            e.score = Mathf.Round(ProjectedScore(e) * 10f) / 10f;
            var g = Curriculum.GradeFor(e.score);
            e.letter = g.letter; e.point = g.point;
            e.passed = g.point + 1e-4f >= Curriculum.passPoint;
            e.graded = true;
        }

        // ============================================================
        // จบการศึกษา (ตรวจรายวิชาบังคับทีละวิชา ไม่ใช่ดูหน่วยกิตรวมอย่างเดียว)
        // ============================================================
        public GraduationStatus CheckGraduation()
        {
            var st = new GraduationStatus();
            var missing = new List<string>();
            var missingIntern = new List<string>();
            var missingProject = new List<string>();
            foreach (var d in Curriculum.courses)
            {
                if (d == null || !d.IsRequired || IsPassed(d.code)) continue;
                missing.Add(d.code);
                if (d.category == CourseCategory.Internship) missingIntern.Add(d.code);
                if (d.category == CourseCategory.Project) missingProject.Add(d.code);
            }
            int req = Curriculum.RequiredCredits;
            if (missing.Count > 0)
            {
                string list = missing.Count <= 8 ? string.Join(", ", missing) : string.Join(", ", missing.GetRange(0, 8)) + $" … (+{missing.Count - 8})";
                st.reasons.Add($"วิชาบังคับผ่าน {PassedRequiredCredits()}/{req} หน่วยกิต — ยังขาด {list}");
            }
            if (missingIntern.Count > 0) st.reasons.Add("ยังไม่ผ่านการฝึกงาน (" + string.Join(", ", missingIntern) + ")");
            if (missingProject.Count > 0) st.reasons.Add("ยังไม่ผ่านโครงงาน (" + string.Join(", ", missingProject) + ")");
            int el = PassedElectiveCount();
            if (el < Curriculum.electivesRequired)
                st.reasons.Add($"วิชาเลือกเฉพาะทางผ่าน {el}/{Curriculum.electivesRequired} วิชา (ต้องไม่ซ้ำกัน)");
            float gpa = Gpa();
            if (!HasGpa) st.reasons.Add("ยังไม่มีเกรดสำหรับคำนวณ GPA");
            else if (gpa + 1e-4f < Curriculum.minGraduationGpa)
                st.reasons.Add($"GPA {gpa:0.00} ต่ำกว่าเกณฑ์ {Curriculum.minGraduationGpa:0.00}");
            st.eligible = st.reasons.Count == 0;
            return st;
        }

        // ============================================================
        // รายงานผลตอนจบเทอม — สร้างจากข้อมูลในเรกคอร์ดของภาคนั้น (ใช้ได้ทั้งตอนปิดภาคและตอนโหลดเซฟที่ยังไม่ได้ดู)
        // ============================================================
        public TermReport BuildTermReport(TermCloseResult r, float minGpa)
        {
            if (r == null || r.term == null) return new TermReport();
            var rep = BuildTermReport(r.term.serial, minGpa, r.promoted, r.exhausted, r.term.isExtra);
            rep.planSemester = r.term.planSemester; rep.classYear = r.term.classYear; rep.calendarYear = r.term.calendarYear; rep.semIndex = r.term.semIndex;
            rep.graduated = r.graduated;
            return rep;
        }

        public TermReport BuildTermReport(int serial, float minGpa, bool promoted, bool exhausted, bool isExtra)
        {
            var rep = new TermReport { serial = serial, minGpa = minGpa, promoted = promoted, exhausted = exhausted, isExtra = isExtra };
            foreach (var e in Record.enrollments)
            {
                if (e.termSerial != serial || e.transfer || !e.graded) continue;
                var d = Curriculum.Get(e.code);
                int cr = d != null ? d.credits : 0;
                rep.rows.Add(new TermReportRow
                {
                    code = e.code, title = d != null ? d.title : e.code, credits = cr, attempt = e.attempt,
                    attendance = StudyRatio(e), midterm = e.midterm, final = e.final,
                    missedMidterm = e.missedMidterm, missedFinal = e.missedFinal,
                    bonusMid = ClassBonus(e, false), bonusFinal = ClassBonus(e, true),
                    score = e.score, letter = e.letter, point = e.point, passed = e.passed, retakeSection = e.retakeSection,
                });
                rep.creditsAttempted += cr;
                if (e.passed) rep.creditsEarnedTerm += cr;
                else if (d != null && d.IsRequired && !IsPassed(e.code)) rep.retakeCodes.Add(e.code);
                rep.calendarYear = e.calendarYear; rep.semIndex = e.semIndex; rep.classYear = e.classYear;
                rep.planSemester = e.semIndex + 1;
            }
            rep.termGpa = TermGpa(serial);
            rep.hasGpa = HasGpa;
            rep.cumulativeGpa = Gpa();
            rep.creditsEarnedTotal = EarnedCredits();
            rep.graduationCredits = Curriculum.GraduationCredits;
            rep.newClassYear = Record.classYear;
            rep.graduated = Record.graduated;
            rep.probation = rep.hasGpa && rep.cumulativeGpa + 1e-4f < minGpa && !rep.graduated;
            if (!promoted && !isExtra && rep.planSemester == 2 && rep.classYear < 4)
                rep.promotionShortfall = Mathf.Max(0, Curriculum.PromotionThreshold(rep.classYear) - rep.creditsEarnedTotal);

            if (rep.graduated) rep.nextStep = "ครบเงื่อนไขจบการศึกษา — ยินดีด้วย!";
            else if (exhausted) rep.nextStep = $"เรียนครบ {Curriculum.maxRegularTerms} ภาคแล้วยังไม่จบ — พ้นสภาพนิสิต";
            else
            {
                string next = rep.planSemester == 1
                    ? "ภาคปลายเปิดลงทะเบียนวันที่ 1 ของภาค (ภายในวันนั้นเท่านั้น)"
                    : "ต่อด้วยปิดภาคฤดูร้อน — ลงทะเบียนภาคต้นปีการศึกษาถัดไปวันที่ 1 ของภาค";
                if (rep.retakeCodes.Count > 0) next = $"ลงเรียนซ้ำ {string.Join(", ", rep.retakeCodes)} (ตอนภาคค่ำ เปิดทุกภาค) · " + next;
                rep.nextStep = next;
            }
            return rep;
        }

        // ============================================================
        // ตรวจห้องเรียน (ตรรกะล้วน) — ห้องเดียวกัน + วันเดียวกัน + เวลาทับ (start < otherEnd && otherStart < end) = ชน
        //   นับเฉพาะคาบที่เปิดในภาคเดียวกันได้: ตอนปกติ = ภาคตามแผน (วิชาเลือกเปิดทั้งสองภาค) · ตอนเรียนซ้ำภาคค่ำ = ทุกภาค
        //   requireRooms = true → คาบที่ไม่มี roomId เป็นปัญหาด้วย · catalog != null → roomId ที่ไม่มีใน catalog เป็นปัญหา
        // ============================================================
        public static List<RoomIssue> FindRoomConflicts(CurriculumDefinition c, ClassroomCatalog catalog, bool requireRooms)
        {
            var issues = new List<RoomIssue>();
            if (c == null || c.courses == null) return issues;
            var all = new List<(CourseDefinition def, bool retake, int idx, ClassSession s, int semMask)>();
            foreach (var d in c.courses)
            {
                if (d == null) continue;
                int planMask = d.planSemester == 1 ? 1 : d.planSemester == 2 ? 2 : 3;
                if (d.sessions != null) for (int i = 0; i < d.sessions.Count; i++) all.Add((d, false, i, d.sessions[i], planMask));
                if (d.retakeSessions != null) for (int i = 0; i < d.retakeSessions.Count; i++) all.Add((d, true, i, d.retakeSessions[i], 3));
            }
            foreach (var a in all)
            {
                if (a.s == null) continue;
                if (!a.s.HasRoom)
                {
                    if (requireRooms) issues.Add(new RoomIssue { kind = RoomIssue.Kind.MissingRoom, codeA = a.def.code, retakeA = a.retake, indexA = a.idx, day = a.s.day, startA = a.s.startMinute, endA = a.s.endMinute });
                }
                else if (catalog != null && !catalog.Exists(a.s.roomId))
                    issues.Add(new RoomIssue { kind = RoomIssue.Kind.UnknownRoom, codeA = a.def.code, retakeA = a.retake, indexA = a.idx, roomId = a.s.roomId, day = a.s.day, startA = a.s.startMinute, endA = a.s.endMinute });
            }
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                {
                    var a = all[i]; var b = all[j];
                    if (a.s == null || b.s == null || !a.s.HasRoom || a.s.roomId != b.s.roomId) continue;
                    if (a.def == b.def && a.retake == b.retake) continue;   // คาบของตอนเดียวกันเอง (เรียนคนละเวลาอยู่แล้ว)
                    if ((a.semMask & b.semMask) == 0) continue;            // คนละภาค ไม่มีทางเปิดพร้อมกัน
                    if (!SessionsOverlap(a.s, b.s)) continue;
                    issues.Add(new RoomIssue
                    {
                        kind = RoomIssue.Kind.Conflict, roomId = a.s.roomId, day = a.s.day,
                        codeA = a.def.code, retakeA = a.retake, indexA = a.idx, startA = a.s.startMinute, endA = a.s.endMinute,
                        codeB = b.def.code, retakeB = b.retake, indexB = b.idx, startB = b.s.startMinute, endB = b.s.endMinute,
                    });
                }
            return issues;
        }

        // ห้องเดียวกันไม่สน — เฉพาะวัน/เวลา (ชนตรงขอบพอดีไม่นับว่าชน)
        public static bool SessionsOverlap(ClassSession a, ClassSession b) =>
            a != null && b != null && a.day == b.day && a.startMinute < b.endMinute && b.startMinute < a.endMinute;

        // ============================================================
        // ย้ายเซฟเก่า: ภาคที่เรียนผ่านมาแล้วตามแผน → เทียบโอน (ได้หน่วยกิต ไม่คิด GPA)
        // ============================================================
        public static AcademicRecord MigrateLegacy(CurriculumDefinition c, int legacyYear, int semIndex)
        {
            var r = new AcademicRecord { migratedFromLegacy = true, classYear = Mathf.Clamp(legacyYear, 1, 4) };
            int completedTerms = (r.classYear - 1) * 2 + Mathf.Clamp(semIndex, 0, 2);   // semIndex 2 = ผ่านทั้งสองภาคแล้ว
            int serial = 0;
            for (int y = 1; y <= 4; y++)
                for (int s = 1; s <= 2; s++)
                {
                    int idx = (y - 1) * 2 + (s - 1);
                    if (idx >= completedTerms) continue;
                    serial++;
                    foreach (var d in c.PlanCourses(y, s))
                        r.enrollments.Add(new Enrollment
                        {
                            code = d.code, attempt = 1, termSerial = serial, calendarYear = y, semIndex = s - 1, classYear = y,
                            graded = true, passed = true, transfer = true, letter = "TR", point = 0f, score = 0f,
                        });
                }
            r.regularTermsStarted = serial;
            if (r.classYear >= 4 && semIndex >= 2) r.finishedPlan = true;
            return r;
        }
    }
}
