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
        public Enrollment selfStudy;      // นับเป็นอ่านทบทวนให้วิชานี้
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
        {
            Curriculum = curriculum;
            Record = record ?? new AcademicRecord();
        }

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
            string conflict = ConflictWithSelected(oc);
            if (conflict != null) { reason = $"{code} ชนเวลากับ {conflict}"; return false; }
            int after = SelectedCredits() + oc.def.credits;
            if (after > CreditCap) { reason = $"เกินเพดาน {CreditCap} หน่วยกิต (เลือกแล้ว {SelectedCredits()} + {oc.def.credits} = {after})"; return false; }
            return true;
        }

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
                    if (!string.IsNullOrEmpty(building) && s.building != building)
                    {
                        res.wrongBuildingCode = e.code; res.wrongBuildingTarget = s.building;
                        continue;
                    }
                    string key = semDay + ":" + i;
                    int ticks = e.TicksFor(key);
                    if (ticks >= s.MaxTicks) { res.capped = true; continue; }
                    e.AddTick(key);
                    e.progress += quality;
                    res.attended = e; res.session = s; res.tickNo = ticks + 1; res.tickMax = s.MaxTicks;
                    return res;
                }
            }

            // นอกคาบ = อ่านทบทวน → ชดเชยให้วิชาที่ตามหลังที่สุด (มีเพดาน)
            if (res.wrongBuildingCode == null && quality > 0f)
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
        public float RecordExam(bool final, float quizFraction, float bonus, int semDay)
        {
            var list = CurrentEnrollments();
            if (list.Count == 0) return 0f;
            float sum = 0f; int n = 0;
            foreach (var e in list)
            {
                if (e.graded) continue;
                float readiness = StudyRatio(e, semDay);
                float s = Mathf.Clamp01(Curriculum.examQuizWeight * quizFraction + (1f - Curriculum.examQuizWeight) * readiness + bonus);
                if (final) { e.final = s; e.missedFinal = false; } else { e.midterm = s; e.missedMidterm = false; }
                sum += s; n++;
            }
            return n > 0 ? sum / n : 0f;
        }

        public void RecordMissedExam(bool final)
        {
            foreach (var e in CurrentEnrollments())
            {
                if (e.graded) continue;
                if (final && e.final < 0f) { e.final = 0f; e.missedFinal = true; }
                if (!final && e.midterm < 0f) { e.midterm = 0f; e.missedMidterm = true; }
            }
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
