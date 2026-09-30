#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.Systems;

namespace NisitSimulator.DevTools
{
    // ===== สร้างประวัติการเรียนสำหรับทดสอบ ด้วยกฎจริงของ RegistrationService (ตรรกะล้วน — EditMode test ได้) =====
    //   ทุกภาค: เปิดภาค → ลงทะเบียนผ่าน Add() (ตรวจ prerequisite/ชนเวลา/เพดาน) → Confirm() → เข้าเรียนผ่าน RecordStudyTick()
    //   ตามตารางจริงทีละคาบ → สอบกลางภาค/ปลายภาคผ่าน RecordCourseExam() (สูตรคะแนนเดิม) → CloseCurrentTerm() (ตัดเกรด/เลื่อนชั้น/ตรวจจบ)
    //   → หน่วยกิตสะสมและ GPA \"คำนวณจากประวัติรายวิชา\" เสมอ ไม่มีการตั้งตัวเลขหรือ flag ผ่านลอย ๆ
    //   คุณภาพ q (0..1) = สัดส่วนการเข้าเรียน = สัดส่วนตอบถูก → คะแนนรวมรายวิชา ≈ 100q (A≥85 · B≥70 · C+≥63 · D≥40 · F<40)
    public class DevHistoryBuilder
    {
        public readonly CurriculumDefinition Curriculum;
        public readonly AcademicRecord Record;
        public readonly RegistrationService Svc;
        public int Cal = 1;          // ปีการศึกษาที่เล่นจริง
        public int NextSem = 0;      // ภาคถัดไปที่จะเปิด (0 ต้น 1 ปลาย 2 ฤดูร้อน)
        public float MinGpa = 2f;
        public int GpaCheckFromYear = 2;
        public string Error;
        public readonly List<string> Log = new List<string>();

        public Func<string, int, int, float> Quality = (code, attempt, classYear) => 0.75f;   // (รหัส, ครั้งที่ลง, ชั้นปีตอนลง) → q
        public Func<CourseDefinition, TermState, bool> TakeElective = (d, t) => false;
        public HashSet<string> SkipRetake = new HashSet<string>();

        public DevHistoryBuilder(CurriculumDefinition c, int creditCapOverride = 0)
        {
            Curriculum = c;
            Record = new AcademicRecord { classYear = 1 };
            Svc = new RegistrationService(c, Record) { CreditCapOverride = creditCapOverride };
        }

        public TermState Term => Record.Current;
        public bool Ok => string.IsNullOrEmpty(Error);

        // วันในปีของวันที่ semDay ในภาค sem (ภาคต้น 1–3 · ภาคปลาย 4–6 · ฤดูร้อน 7–8)
        public static int DayInYear(int sem, int semDay)
        {
            int d = 0;
            for (int i = 0; i < sem; i++) d += AcademicCalendar.SemesterLen(i);
            return d + Mathf.Clamp(semDay, 1, AcademicCalendar.SemesterLen(sem));
        }

        // ---------- วงจรภาค ----------
        public TermState OpenNext()
        {
            var t = Svc.OpenTerm(Cal, NextSem);
            return t;
        }

        // ปิดภาคปัจจุบัน แล้วเลื่อนตัวชี้ภาค (ภาคฤดูร้อน → สิ้นปีการศึกษา: ตรวจกฎ GPA แบบเดียวกับ ProgressionManager)
        public TermCloseResult CloseAndAdvance()
        {
            var res = Svc.CloseCurrentTerm();
            if (res != null && !res.term.isBreak && res.graded.Count > 0)
            {
                var sb = new StringBuilder($"ปิดภาค ปี{res.term.classYear}/{res.term.planSemester} (ปีการศึกษา {Cal}): ");
                foreach (var e in res.graded) sb.Append(e.code).Append('=').Append(e.letter).Append(' ');
                sb.Append($"· GPA สะสม {Svc.Gpa():0.00} · หน่วยกิต {Svc.EarnedCredits()}");
                if (res.promoted) sb.Append($" · เลื่อนเป็นชั้นปี {res.newClassYear}");
                if (res.graduated) sb.Append(" · จบการศึกษา");
                Log.Add(sb.ToString());
            }
            NextSem++;
            if (NextSem > 2)
            {
                if (Svc.HasGpa && Svc.Gpa() < MinGpa && Cal >= GpaCheckFromYear && !Record.graduated)
                    Error = $"สิ้นปีการศึกษา {Cal} GPA {Svc.Gpa():0.00} < {MinGpa:0.00} → เกมจริงจะรีไทร์ก่อนถึงจุดนี้";
                NextSem = 0;
                Cal++;
            }
            return res;
        }

        // ลงทะเบียนตามนโยบาย: วิชาในแผน + วิชาค้าง (เรียนซ้ำ) + วิชาเลือกที่กำหนด — ทุกวิชาผ่าน Add() กฎปกติ
        public List<string> RegisterDefault()
        {
            var rejected = new List<string>();
            var t = Term;
            if (t == null || t.isBreak) return rejected;
            foreach (var o in Svc.Offered())
            {
                bool want = o.group == OfferGroup.Plan
                         || (o.group == OfferGroup.Outstanding && !SkipRetake.Contains(o.def.code))
                         || (o.group == OfferGroup.Elective && TakeElective(o.def, t));
                if (!want || Svc.IsPassed(o.def.code)) continue;
                if (!Svc.Add(o.def.code, out var why)) rejected.Add(why);
            }
            return rejected;
        }

        public bool Confirm()
        {
            var t = Term;
            if (t == null || t.isBreak || t.selected.Count == 0) return false;
            if (!Svc.Confirm(out var why)) { Log.Add("ยืนยันไม่ได้: " + why); return false; }
            return true;
        }

        float Q(Enrollment e) => Mathf.Clamp01(Quality(e.code, e.attempt, e.classYear));

        // เข้าเรียนตามตาราง (ทุกคาบของวัน fromDay..toDay; วัน toDay นับเฉพาะคาบที่จบก่อน untilMinute) ผ่าน RecordStudyTick
        public int Attend(int fromDay, int toDay, int untilMinute = int.MaxValue)
        {
            int ticks = 0;
            foreach (var e in Svc.CurrentEnrollments())
            {
                var ss = Svc.SessionsFor(e);
                float q = Q(e);
                foreach (var s in ss)
                {
                    if (s.day < fromDay || s.day > toDay) continue;
                    if (s.day == toDay && s.endMinute > untilMinute) continue;
                    for (int k = 1; k <= s.MaxTicks; k++)
                    {
                        var r = Svc.RecordStudyTick(s.day, Mathf.Min(s.startMinute + 60 * k, s.endMinute), s.building, q);
                        if (r.attended != null) ticks++;
                    }
                }
            }
            return ticks;
        }

        // สอบรายวิชา (สูตรเดิม) — ตอบถูกสัดส่วน q
        public void Exam(bool final, int semDay)
        {
            foreach (var e in Svc.CurrentEnrollments())
                Svc.RecordCourseExam(e.code, final, Q(e), 0f, semDay);
        }

        // เรียนครบหนึ่งภาคตามลำดับเวลาจริง: เข้าเรียนวัน 1–2 → สอบกลางภาค (วันกลางภาค) → เรียนวันที่เหลือ → สอบปลายภาค → ปิดภาค
        public TermCloseResult CompleteTerm()
        {
            var t = Term;
            if (t != null && !t.isBreak)
            {
                var rej = RegisterDefault();
                foreach (var r in rej) Log.Add("ลงไม่ได้: " + r);
                if (Confirm())
                {
                    int sem = t.semIndex;
                    int len = AcademicCalendar.SemesterLen(sem);
                    int mid = ExamController.MidtermDay(sem);
                    Attend(1, mid);
                    if (AcademicCalendar.HasMidterm(sem)) Exam(false, mid);
                    Attend(mid + 1, len);
                    Exam(true, len);
                }
            }
            return CloseAndAdvance();
        }

        // เล่นตามแผนจนภาคถัดไปที่จะเปิดคือ ชั้นปี year ภาค planSem แล้ว \"เปิด\" ภาคนั้น (ช่วงลงทะเบียน)
        public bool AdvanceToTerm(int year, int planSem)
        {
            for (int guard = 0; guard < 64 && Ok; guard++)
            {
                if (NextSem == 2) { OpenNext(); CloseAndAdvance(); continue; }
                if (!Record.finishedPlan && Record.classYear == year && NextSem + 1 == planSem) { OpenNext(); return true; }
                if (Record.graduated) { Error = "จบการศึกษาไปก่อนถึงภาคเป้าหมาย"; return false; }
                OpenNext();
                CompleteTerm();
            }
            if (Ok) Error = $"ไปไม่ถึงชั้นปี {year} ภาค {planSem} (หน่วยกิตไม่พอเลื่อนชั้น?)";
            return false;
        }

        // เล่นจนถึงภาคเรียนเพิ่มเติมภาคแรก (เรียนครบแผน 4 ปีแล้วยังไม่จบ) แล้วเปิดภาคนั้น
        public bool AdvanceToExtraTerm()
        {
            for (int guard = 0; guard < 64 && Ok; guard++)
            {
                if (NextSem == 2) { OpenNext(); CloseAndAdvance(); continue; }
                if (Record.graduated) { Error = "จบการศึกษาแล้ว — ไม่มีภาคเพิ่มเติม"; return false; }
                if (Record.finishedPlan) { OpenNext(); return true; }
                OpenNext();
                CompleteTerm();
            }
            if (Ok) Error = "ไปไม่ถึงภาคเรียนเพิ่มเติม";
            return false;
        }

        public AcademicRecord CloneRecord() => JsonUtility.FromJson<AcademicRecord>(JsonUtility.ToJson(Record));
    }

    // ผลของสถานการณ์หนึ่ง — ตำแหน่งเวลาในเกม + ประวัติ + ผลตรวจเงื่อนไขที่ตั้งใจสร้าง
    public class DevScenarioResult
    {
        public string id, title, description;
        public bool skipped;
        public string skipReason;
        public AcademicRecord record;
        public int calendarYear = 1, dayInYear = 1;
        public float minutes = 8 * 60;
        public bool conditionOk;                  // เงื่อนไขที่สถานการณ์ตั้งใจสร้างเป็นจริง (ตรวจด้วยกฎจริง)
        public string conditionText = "";         // คาดหวัง / ที่ได้
        public string hint = "";                  // วิธีทดสอบต่อ
        public List<string> log = new List<string>();
    }

    public static class DevScenarios
    {
        public class Def { public string id, title, description; public Func<CurriculumDefinition, int, DevScenarioResult> build; }

        public static readonly Def[] All =
        {
            new Def { id = "S1", title = "ปี 1 ภาคเรียน 1 เริ่มใหม่", description = "ประวัติว่าง เปิดช่วงลงทะเบียนภาค 1/1 วันแรก 08:00", build = FreshY1S1 },
            new Def { id = "S2", title = "ขาดวิชาบังคับก่อน", description = "ภาค 1/1 ตก CS102 → ภาค 1/2 ลง CS103 ไม่ได้ (ต้องผ่าน CS102)", build = MissingPrereq },
            new Def { id = "S3", title = "หน่วยกิตเกินเพดาน", description = "ตก GE101 · ภาค 1/2 เลือกแผนครบ 18 หน่วยกิตแล้ว → ลองเพิ่ม GE101 จะเกินเพดาน", build = OverCap },
            new Def { id = "S4", title = "ตารางเรียนชนกัน", description = "ตก GE101 + CS101 (ตอนเรียนซ้ำเวลาเดียวกัน) · เลือก GE101 ไว้ → ลองเพิ่ม CS101 จะชนเวลา", build = TimeConflict },
            new Def { id = "S5", title = "พร้อมเข้าสอบ", description = "ภาค 1/1 ลงทะเบียนครบ เข้าเรียนวัน 1 + เช้าวัน 2 · วันสอบกลางภาค 12:00", build = ReadyForExam },
            new Def { id = "S6", title = "สอบตกและพร้อมลงเรียนซ้ำ", description = "ภาค 1/1 ได้ F วิชา CS101 → ภาค 1/2 เปิดลงทะเบียน CS101 เป็นวิชาค้าง (ตอนเรียนซ้ำภาคค่ำ)", build = FailedRetake },
            new Def { id = "S7", title = "เรียนครบตามแผนจนพร้อมจบ", description = "ผ่านทุกภาคถึง 4/1 + วิชาเลือก 2 วิชา · ภาค 4/2 เรียน/สอบครบแล้ว วันสอบปลายภาค 20:00 — ข้ามไปวันถัดไป (จำลองเวลา) = ประกาศผลและจบการศึกษา", build = ReadyToGraduate },
            new Def { id = "S8", title = "หน่วยกิตถึงเกณฑ์แต่ขาดวิชาบังคับ", description = "ตก CS404 แต่มีวิชาเลือก 3 วิชา → หน่วยกิตสะสม ≥ 120 แต่ยังจบไม่ได้ · อยู่ภาคเรียนเพิ่มเติม", build = CreditsButMissingRequired },
            new Def { id = "S9", title = "ผ่านวิชาครบแต่ GPA ต่ำกว่าเกณฑ์", description = "ปี 1–2 ได้ C+ · ปี 3–4 ได้ D → ผ่านครบทุกเงื่อนไขยกเว้น GPA < 2.00 · อยู่ช่วงปิดภาคฤดูร้อนปี 4", build = PassedAllLowGpa },
        };

        public static Def Find(string id) { foreach (var d in All) if (d.id == id) return d; return null; }

        static DevHistoryBuilder New(CurriculumDefinition c, int cap)
        {
            var b = new DevHistoryBuilder(c, cap);
            var prog = UnityEngine.Object.FindFirstObjectByType<ProgressionManager>();
            if (prog != null) { b.MinGpa = prog.minGpa; b.GpaCheckFromYear = prog.gpaCheckFromYear; }
            return b;
        }

        static DevScenarioResult Result(Def d, DevHistoryBuilder b, int semDay, float minutes)
        {
            var r = new DevScenarioResult { id = d.id, title = d.title, description = d.description, record = b.Record, log = b.Log };
            if (!b.Ok) { r.skipped = true; r.skipReason = b.Error; return r; }
            var t = b.Term;
            r.calendarYear = t != null ? t.calendarYear : b.Cal;
            r.dayInYear = DevHistoryBuilder.DayInYear(t != null ? t.semIndex : 0, semDay);
            r.minutes = minutes;
            return r;
        }

        static DevScenarioResult Check(DevScenarioResult r, bool ok, string expected, string actual)
        {
            if (r.skipped) return r;
            r.conditionOk = ok;
            r.conditionText = $"คาดหวัง: {expected}\nที่ได้: {actual}";
            if (!ok) { r.skipped = true; r.skipReason = "สร้างเงื่อนไขไม่สำเร็จด้วยกฎจริง — " + actual; }
            return r;
        }

        public static DevScenarioResult Build(string id, CurriculumDefinition c, int cap)
        {
            var d = Find(id);
            if (d == null) return new DevScenarioResult { id = id, skipped = true, skipReason = "ไม่พบสถานการณ์" };
            if (c == null) return new DevScenarioResult { id = id, title = d.title, skipped = true, skipReason = "ไม่พบหลักสูตร" };
            try { return d.build(c, cap); }
            catch (Exception e) { return new DevScenarioResult { id = id, title = d.title, skipped = true, skipReason = "ข้อผิดพลาด: " + e.Message }; }
        }

        // ---------- สถานการณ์ ----------
        static DevScenarioResult FreshY1S1(CurriculumDefinition c, int cap)
        {
            var b = New(c, cap);
            b.AdvanceToTerm(1, 1);
            var r = Result(All[0], b, 1, 8 * 60);
            bool ok = b.Ok && b.Record.enrollments.Count == 0 && b.Svc.RegistrationWindowOpen(1) && b.Svc.Offered().Count > 0;
            r.hint = "เปิดแท็บการเรียน → เลือกวิชาแผน 1/1 → ยืนยัน (กฎปกติ)";
            return Check(r, ok, "ประวัติว่าง · ช่วงลงทะเบียนเปิด · มีวิชาเปิด", $"ประวัติ {b.Record.enrollments.Count} รายการ · เปิดลงทะเบียน={b.Svc.RegistrationWindowOpen(1)} · วิชาเปิด {b.Svc.Offered().Count}");
        }

        static DevScenarioResult MissingPrereq(CurriculumDefinition c, int cap)
        {
            var b = New(c, cap);
            b.Quality = (code, att, y) => code == "CS102" ? 0.2f : 0.75f;
            b.AdvanceToTerm(1, 2);
            var r = Result(All[1], b, 1, 8 * 60);
            if (r.skipped) return r;
            bool can = b.Svc.CanAdd("CS103", out var why);
            r.hint = "ลองเพิ่ม CS103 (กฎปกติ) → ต้องถูกปฏิเสธพร้อมเหตุผล";
            return Check(r, !can && why != null && why.Contains("CS102"), "CS103 ลงไม่ได้ เหตุผลอ้าง CS102", can ? "ลงได้" : why);
        }

        static DevScenarioResult OverCap(CurriculumDefinition c, int cap)
        {
            var b = New(c, cap);
            b.Quality = (code, att, y) => code == "GE101" ? 0.2f : 0.75f;
            b.AdvanceToTerm(1, 2);
            var r = Result(All[2], b, 1, 8 * 60);
            if (r.skipped) return r;
            foreach (var o in b.Svc.Offered()) if (o.group == OfferGroup.Plan) b.Svc.Add(o.def.code, out _);
            bool can = b.Svc.CanAdd("GE101", out var why);
            r.hint = $"เลือกไว้ {b.Svc.SelectedCredits()}/{b.Svc.CreditCap} หน่วยกิต → ลองเพิ่ม GE101";
            return Check(r, !can && why != null && why.Contains("เพดาน"), "GE101 ถูกปฏิเสธเพราะเกินเพดาน", can ? "ลงได้" : why);
        }

        static DevScenarioResult TimeConflict(CurriculumDefinition c, int cap)
        {
            var b = New(c, cap);
            b.Quality = (code, att, y) => code == "GE101" || code == "CS101" ? 0.2f : 0.75f;
            b.AdvanceToTerm(1, 2);
            var r = Result(All[3], b, 1, 8 * 60);
            if (r.skipped) return r;
            b.Svc.Add("GE101", out _);
            bool can = b.Svc.CanAdd("CS101", out var why);
            r.hint = "เลือก GE101 ไว้แล้ว → ลองเพิ่ม CS101";
            return Check(r, !can && why != null && why.Contains("ชนเวลา"), "CS101 ถูกปฏิเสธเพราะชนเวลากับ GE101", can ? "ลงได้" : why);
        }

        static DevScenarioResult ReadyForExam(CurriculumDefinition c, int cap)
        {
            var b = New(c, cap);
            b.Quality = (code, att, y) => 0.9f;
            b.AdvanceToTerm(1, 1);
            if (b.Ok)
            {
                foreach (var rj in b.RegisterDefault()) b.Log.Add("ลงไม่ได้: " + rj);
                b.Confirm();
                b.Attend(1, 2, 12 * 60);
            }
            int mid = ExamController.MidtermDay(0);
            var r = Result(All[4], b, mid, 12 * 60);
            if (r.skipped) return r;
            int n = b.Svc.CurrentEnrollments().Count;
            bool noScore = true; foreach (var e in b.Svc.CurrentEnrollments()) if (RegistrationService.HasExamScore(e, false)) noScore = false;
            r.hint = "เดินไปห้องสอบ (หรือแท็บเวลา ▸ วาร์ปจุดสอบ) กด E หรือแท็บการสอบ ▸ เข้าห้องสอบผ่านขั้นตอนปกติ";
            return Check(r, n > 0 && noScore && b.Term.confirmed, "ลงทะเบียนแล้ว มีวิชาที่ยังไม่มีคะแนนกลางภาค อยู่วันสอบกลางภาค", $"ลง {n} วิชา · ยืนยัน={b.Term.confirmed} · ยังไม่มีคะแนน={noScore} · วันในภาค {mid}");
        }

        static DevScenarioResult FailedRetake(CurriculumDefinition c, int cap)
        {
            var b = New(c, cap);
            b.Quality = (code, att, y) => code == "CS101" && att == 1 ? 0.2f : 0.75f;
            b.AdvanceToTerm(1, 2);
            var r = Result(All[5], b, 1, 8 * 60);
            if (r.skipped) return r;
            var oc = b.Svc.FindOffered("CS101");
            var att1 = b.Svc.Attempts("CS101");
            bool failed = att1.Count == 1 && att1[0].graded && !att1[0].passed;
            bool can = b.Svc.CanAdd("CS101", out var why);
            r.hint = "เพิ่ม CS101 (ตอนเรียนซ้ำ) → ยืนยัน → เรียน/สอบให้ผ่าน แล้วตรวจว่าหน่วยกิตไม่นับซ้ำ";
            return Check(r, failed && oc != null && oc.group == OfferGroup.Outstanding && oc.retakeSection && can,
                "CS101 ตก 1 ครั้ง · เปิดเป็นวิชาค้างตอนเรียนซ้ำ · ลงได้",
                $"ตก={failed} ({(att1.Count > 0 ? att1[0].letter : "-")}) · กลุ่ม={(oc != null ? oc.group.ToString() : "ไม่เปิด")} · ภาคค่ำ={(oc != null && oc.retakeSection)} · ลงได้={can}{(can ? "" : " (" + why + ")")}");
        }

        static DevScenarioResult ReadyToGraduate(CurriculumDefinition c, int cap)
        {
            var b = New(c, cap);
            b.Quality = (code, att, y) => 0.75f;
            b.TakeElective = (d, t) => t.classYear == 3 && t.planSemester == 1 && (d.code == "EL301" || d.code == "EL302");
            b.AdvanceToTerm(4, 2);
            if (b.Ok)
            {
                foreach (var rj in b.RegisterDefault()) b.Log.Add("ลงไม่ได้: " + rj);
                b.Confirm();
                int sem = b.Term.semIndex, len = AcademicCalendar.SemesterLen(sem), mid = ExamController.MidtermDay(sem);
                b.Attend(1, mid); b.Exam(false, mid); b.Attend(mid + 1, len); b.Exam(true, len);
            }
            var r = Result(All[6], b, AcademicCalendar.SemesterLen(1), 20 * 60);
            if (r.skipped) return r;
            // ตรวจล่วงหน้าบนสำเนา: ปิดภาคนี้ด้วยกฎจริงแล้วต้องจบการศึกษา
            var clone = new RegistrationService(c, b.CloneRecord()) { CreditCapOverride = cap };
            var res = clone.CloseCurrentTerm();
            var gs = clone.CheckGraduation();
            r.hint = "แท็บเวลา ▸ จำลองเวลาเดินผ่าน ▸ ไปวันถัดไป → ประกาศผลภาค → จบการศึกษา (หน้าจบเกม)";
            return Check(r, res != null && res.graduated && gs.eligible,
                "ปิดภาคนี้แล้วผ่านเงื่อนไขจบ", res != null && res.graduated ? $"จบ · หน่วยกิต {clone.EarnedCredits()} · GPA {clone.Gpa():0.00}" : "ยังไม่จบ: " + string.Join(" / ", gs.reasons));
        }

        static DevScenarioResult CreditsButMissingRequired(CurriculumDefinition c, int cap)
        {
            var b = New(c, cap);
            b.Quality = (code, att, y) => code == "CS404" ? 0.2f : 0.75f;
            b.SkipRetake.Add("CS404");
            b.TakeElective = (d, t) => t.classYear == 3 && ((t.planSemester == 1 && (d.code == "EL301" || d.code == "EL302")) || (t.planSemester == 2 && d.code == "EL303"));
            b.AdvanceToExtraTerm();
            var r = Result(All[7], b, 1, 8 * 60);
            if (r.skipped) return r;
            var gs = b.Svc.CheckGraduation();
            int earned = b.Svc.EarnedCredits();
            bool missing404 = false; foreach (var s in gs.reasons) if (s.Contains("CS404")) missing404 = true;
            r.hint = "แท็บการเรียน ▸ เงื่อนไขจบที่ยังขาด → ต้องขึ้น CS404 แม้หน่วยกิต ≥ " + c.GraduationCredits;
            return Check(r, earned >= c.GraduationCredits && !gs.eligible && missing404 && !b.Record.graduated,
                $"หน่วยกิต ≥ {c.GraduationCredits} · ยังไม่จบ · ขาด CS404", $"หน่วยกิต {earned} · จบ={gs.eligible} · เหตุผล: {string.Join(" / ", gs.reasons)}");
        }

        static DevScenarioResult PassedAllLowGpa(CurriculumDefinition c, int cap)
        {
            var b = New(c, cap);
            b.Quality = (code, att, y) => y <= 2 ? 0.66f : 0.45f;
            b.TakeElective = (d, t) => t.classYear == 3 && t.planSemester == 1 && (d.code == "EL301" || d.code == "EL302");
            b.AdvanceToTerm(4, 2);
            if (b.Ok) b.CompleteTerm();                          // ภาค 4/2 (เปิดอยู่แล้ว) เรียนจนประกาศผล
            if (b.Ok) b.OpenNext();                               // เปิดภาคฤดูร้อน (ยังไม่สิ้นปีการศึกษา)
            var r = Result(All[8], b, 1, 8 * 60);
            if (r.skipped) return r;
            var gs = b.Svc.CheckGraduation();
            bool onlyGpa = gs.reasons.Count == 1 && gs.reasons[0].Contains("GPA");
            r.hint = "แท็บการเรียน ▸ เงื่อนไขจบ: ขาดเฉพาะ GPA · ถ้าเดินเวลาจนสิ้นปีการศึกษา กฎ GPA จะรีไทร์";
            return Check(r, !gs.eligible && onlyGpa && b.Svc.PassedRequiredCredits() == c.RequiredCredits,
                "วิชาบังคับครบ · วิชาเลือกครบ · ขาดเฉพาะ GPA", $"วิชาบังคับ {b.Svc.PassedRequiredCredits()}/{c.RequiredCredits} · วิชาเลือก {b.Svc.PassedElectiveCount()} · GPA {b.Svc.Gpa():0.00} · เหตุผล: {string.Join(" / ", gs.reasons)}");
        }

        // ---------- สร้างประวัติแบบกำหนดเอง (แท็บการเรียน) ----------
        //   เรียนผ่านทุกภาคก่อนหน้าด้วยคุณภาพ quality ยกเว้นวิชาใน forcedFail (ได้ F และไม่ลงซ้ำ) แล้วเปิดช่วงลงทะเบียนภาค year/sem
        public static DevScenarioResult BuildCustom(CurriculumDefinition c, int cap, int year, int sem, float quality, ICollection<string> forcedFail)
        {
            var b = New(c, cap);
            var fail = new HashSet<string>(forcedFail ?? new string[0]);
            b.Quality = (code, att, y) => fail.Contains(code) ? 0.2f : quality;
            foreach (var f in fail) b.SkipRetake.Add(f);
            b.TakeElective = (d, t) => t.classYear == 3 && t.planSemester == 1 && (d.code == "EL301" || d.code == "EL302") && !fail.Contains(d.code);
            b.AdvanceToTerm(year, sem);
            var r = Result(new Def { id = "CUSTOM", title = $"ประวัติกำหนดเอง → ปี {year} ภาค {sem}", description = "" }, b, 1, 8 * 60);
            r.title = $"ประวัติกำหนดเอง → ปี {year} ภาค {sem}" + (fail.Count > 0 ? " (ตก: " + string.Join(",", fail) + ")" : "");
            if (r.skipped) return r;
            return Check(r, b.Svc.RegistrationWindowOpen(1) && b.Term.classYear == year && b.Term.planSemester == sem,
                $"เปิดลงทะเบียนชั้นปี {year} ภาค {sem}", $"ชั้นปี {b.Term.classYear} ภาค {b.Term.planSemester} · เปิด={b.Svc.RegistrationWindowOpen(1)} · หน่วยกิต {b.Svc.EarnedCredits()} · GPA {b.Svc.Gpa():0.00}");
        }
    }
}
#endif
