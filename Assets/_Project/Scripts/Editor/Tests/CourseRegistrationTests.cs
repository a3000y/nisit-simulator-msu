#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;

namespace NisitSimulator.Tests
{
    // EditMode tests ของระบบลงทะเบียน (Window ▸ General ▸ Test Runner ▸ EditMode)
    //   ทดสอบตรรกะล้วน (RegistrationService) + Save/Load JSON — ไม่ต้องเปิดฉาก
    public class CourseRegistrationTests
    {
        CurriculumDefinition cur;
        RegistrationService svc;

        [SetUp]
        public void SetUp()
        {
            cur = CsCurriculumDefaults.Create();
            svc = new RegistrationService(cur, new AcademicRecord());
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(cur); }

        // ---------- helpers ----------
        List<string> Plan(int y, int s) { var l = new List<string>(); foreach (var d in cur.PlanCourses(y, s)) l.Add(d.code); return l; }

        void Attend(Enrollment e)
        {
            foreach (var s in svc.SessionsFor(e))
                for (int k = 1; k <= s.MaxTicks; k++)
                {
                    var r = svc.RecordStudyTick(s.day, s.startMinute + 60 * k, s.building, 1f);
                    Assert.AreEqual(e.code, r.attended != null ? r.attended.code : null, $"เข้าเรียน {e.code} ว.{s.day} {s.TimeText}");
                }
        }

        // เล่นหนึ่งภาค: ลง → ยืนยัน → เข้าเรียนเฉพาะวิชาที่ attend คืน true → สอบ (quiz) → ปิดภาค
        TermCloseResult PlayTerm(int cal, int sem, IEnumerable<string> codes, System.Func<string, bool> attend, float quiz)
        {
            svc.OpenTerm(cal, sem);
            foreach (var c in codes) Assert.IsTrue(svc.Add(c, out var r), r);
            Assert.IsTrue(svc.Confirm(out var cr), cr);
            foreach (var e in svc.CurrentEnrollments()) if (attend(e.code)) Attend(e);
            svc.RecordExam(false, quiz, 0f, 2);
            svc.RecordExam(true, quiz, 0f, 3);
            return svc.CloseCurrentTerm();
        }

        List<string> PlanWithElective(int y, int s)
        {
            var codes = Plan(y, s + 1);
            if (y == 3 && s == 0) codes.Add("EL301");
            if (y == 3 && s == 1) codes.Add("EL307");
            return codes;
        }

        static Enrollment Graded(string code, int serial, float point, string letter)
            => new Enrollment { code = code, termSerial = serial, graded = true, point = point, letter = letter, passed = point >= 1f };

        // ---------- ข้อมูลหลักสูตร ----------
        [Test]
        public void Curriculum_IsValid_And_Totals120()
        {
            CollectionAssert.IsEmpty(cur.Validate());
            Assert.AreEqual(44, cur.courses.Count);                   // บังคับ 36 + เลือก 8
            Assert.AreEqual(114, cur.RequiredCredits);
            Assert.AreEqual(120, cur.GraduationCredits);
            int[] expect = { 18, 18, 18, 18, 12, 12, 9, 9 };
            int i = 0;
            for (int y = 1; y <= 4; y++)
                for (int s = 1; s <= 2; s++, i++)
                {
                    int c = 0; foreach (var d in cur.PlanCourses(y, s)) c += d.credits;
                    Assert.AreEqual(expect[i], c, $"แผน {y}/{s}");
                }
            // ฝึกงานกับโครงงาน 1 คนละช่วงเวลา
            Assert.IsFalse(CurriculumDefinition.SessionsConflict(cur.Get("CS401").sessions, cur.Get("CS402").sessions));
        }

        // ---------- ลงทะเบียน ----------
        [Test]
        public void Year1_Register_Succeeds_And_AppearsInSchedule()
        {
            svc.OpenTerm(1, 0);
            Assert.IsTrue(svc.RegistrationWindowOpen(1));
            foreach (var c in Plan(1, 1)) Assert.IsTrue(svc.Add(c, out var r), r);
            Assert.AreEqual(18, svc.SelectedCredits());
            Assert.IsTrue(svc.Confirm(out var msg), msg);
            var ens = svc.CurrentEnrollments();
            Assert.AreEqual(6, ens.Count);
            foreach (var e in ens) Assert.AreEqual(4, svc.SessionsFor(e).Count, e.code);   // ปฏิทินรุ่น 2: วิชาละ 4 ครั้ง/ภาค
            Assert.IsTrue(svc.HasActiveEnrollments);
            Assert.IsFalse(svc.Remove("GE101", out _));             // ล็อกหลังยืนยัน
        }

        [Test]
        public void Blocked_When_Prerequisite_Missing()
        {
            // ภาค 1/1 ตก CS102 → ภาค 1/2 ลง CS103 ไม่ได้ แม้กำลังลง CS102 ซ้ำอยู่
            PlayTerm(1, 0, Plan(1, 1), c => c != "CS102", 0f);
            Assert.IsFalse(svc.IsPassed("CS102"));
            svc.OpenTerm(1, 1);
            Assert.IsTrue(svc.Add("CS102", out var r0), r0);
            Assert.IsFalse(svc.CanAdd("CS103", out var why));
            StringAssert.Contains("CS102", why);
            var st = svc.StatusOf(svc.FindOffered("CS103"), out var reason);
            Assert.AreEqual(CourseStatus.MissingPrerequisite, st);
            StringAssert.Contains("ยังไม่นับว่าผ่าน", reason);
        }

        [Test]
        public void Blocked_When_TimeConflict()
        {
            // ตก GE101 และ CS102 (ตอนเรียนซ้ำภาคค่ำช่องเดียวกัน — ภาคค่ำ 4 ช่อง วิชาบังคับลำดับ 0 กับ 4) → ลงพร้อมกันไม่ได้
            PlayTerm(1, 0, Plan(1, 1), c => c != "GE101" && c != "CS102", 0f);
            svc.OpenTerm(1, 1);
            Assert.IsTrue(svc.Add("GE101", out var r), r);
            Assert.IsFalse(svc.Add("CS102", out var why));
            StringAssert.Contains("ชนเวลา", why);
        }

        [Test]
        public void Blocked_When_Over_CreditCap()
        {
            PlayTerm(1, 0, Plan(1, 1), c => c != "GE101", 0f);
            svc.OpenTerm(1, 1);
            foreach (var c in Plan(1, 2)) Assert.IsTrue(svc.Add(c, out var r), r);
            Assert.AreEqual(18, svc.SelectedCredits());
            Assert.IsFalse(svc.Add("GE101", out var why));
            StringAssert.Contains("เพดาน 18", why);
            svc.CreditCapOverride = 21;                               // เพดานปรับได้
            Assert.IsTrue(svc.Add("GE101", out var ok), ok);
        }

        [Test]
        public void Confirm_Twice_DoesNotDuplicate()
        {
            svc.OpenTerm(1, 0);
            foreach (var c in Plan(1, 1)) svc.Add(c, out _);
            Assert.IsFalse(svc.Add("GE101", out _));                 // เลือกรหัสซ้ำ
            Assert.IsTrue(svc.Confirm(out _));
            Assert.IsFalse(svc.Confirm(out var again));
            StringAssert.Contains("ไม่สร้างรายการซ้ำ", again);
            Assert.AreEqual(6, svc.Record.enrollments.Count);
        }

        [Test]
        public void Deadline_AutoConfirms_Or_Closes()
        {
            svc.OpenTerm(1, 0);
            svc.Add("GE101", out _);
            Assert.IsFalse(svc.CheckRegistrationDeadline(1, out _));  // ยังอยู่ในช่วง
            Assert.IsTrue(svc.CheckRegistrationDeadline(2, out var m));
            Assert.IsTrue(svc.Term.confirmed, m);

            var svc2 = new RegistrationService(cur, new AcademicRecord());
            svc2.OpenTerm(1, 0);
            Assert.IsTrue(svc2.CheckRegistrationDeadline(2, out _));
            Assert.IsFalse(svc2.Term.registrationOpen);
            Assert.IsNotNull(svc2.ExplainNoOptions(2));               // มีคำอธิบาย ไม่ปล่อยว่าง
        }

        [Test]
        public void SummerBreak_HasNoCourses_ButExplains()
        {
            svc.OpenTerm(1, 2);
            Assert.IsTrue(svc.Term.isBreak);
            Assert.AreEqual(0, svc.Offered().Count);
            Assert.IsNotNull(svc.ExplainNoOptions(1));
        }

        // ---------- เข้าเรียน ----------
        [Test]
        public void Attendance_ChecksBuilding_And_CapsTicks()
        {
            svc.OpenTerm(1, 0);
            svc.Add("CS101", out _); svc.Confirm(out _);
            var s = cur.Get("CS101").sessions[0];                    // ว.1 15-17 คณะ IT
            var wrong = svc.RecordStudyTick(s.day, s.startMinute + 60, "อาคารเรียน", 1f);
            Assert.AreEqual("CS101", wrong.wrongBuildingCode);
            Assert.IsNull(wrong.attended);
            Assert.IsNotNull(svc.RecordStudyTick(s.day, s.startMinute + 60, s.building, 1f).attended);
            Assert.IsNotNull(svc.RecordStudyTick(s.day, s.startMinute + 120, s.building, 1f).attended);
            Assert.IsTrue(svc.RecordStudyTick(s.day, s.startMinute + 120, s.building, 1f).capped);
            var notReg = cur.Get("GE101").sessions[0];                // ไม่ได้ลงวิชานี้ = ไม่นับ
            Assert.IsNull(svc.RecordStudyTick(notReg.day, notReg.startMinute + 60, notReg.building, 1f).attended);
        }

        // ---------- ตก → ลงซ้ำ → ผ่าน ----------
        [Test]
        public void Fail_Retake_Pass_CreditsCountedOnce()
        {
            PlayTerm(1, 0, Plan(1, 1), c => c != "CS102", 0f);
            Assert.AreEqual(15, svc.EarnedCredits());
            Assert.AreEqual("F", svc.Attempts("CS102")[0].letter);

            var codes = Plan(1, 2); codes.Remove("CS103"); codes.Add("CS102");
            PlayTerm(1, 1, codes, c => true, 1f);
            Assert.IsTrue(svc.IsPassed("CS102"));
            Assert.AreEqual(2, svc.Attempts("CS102").Count);           // เก็บประวัติทุกครั้ง
            Assert.IsTrue(svc.Attempts("CS102")[1].retakeSection);
            Assert.AreEqual(15 + 18, svc.EarnedCredits());              // CS102 นับครั้งเดียว
            Assert.AreEqual("A", svc.LatestGraded()["CS102"].letter);   // GPA ใช้ครั้งล่าสุด

            svc.OpenTerm(2, 0);
            Assert.IsFalse(svc.CanAdd("CS102", out var why));          // ผ่านแล้วลงซ้ำไม่ได้
            Assert.IsNotNull(why);
        }

        [Test]
        public void CloseTerm_IsIdempotent()
        {
            PlayTerm(1, 0, Plan(1, 1), c => true, 1f);
            int credits = svc.EarnedCredits(); float gpa = svc.Gpa(); int n = svc.Record.enrollments.Count;
            Assert.IsNull(svc.CloseCurrentTerm());
            Assert.AreEqual(credits, svc.EarnedCredits());
            Assert.AreEqual(gpa, svc.Gpa());
            Assert.AreEqual(n, svc.Record.enrollments.Count);
        }

        // ---------- วิชาเลือก EL301 → EL307 ----------
        [Test]
        public void Elective_Chain_EL301_Then_EL307()
        {
            for (int y = 1; y <= 2; y++)
                for (int s = 0; s < 2; s++)
                    PlayTerm(y, s, Plan(y, s + 1), c => true, 1f);
            svc.OpenTerm(3, 0);
            Assert.AreEqual(3, svc.Term.classYear);
            Assert.AreEqual(CourseStatus.MissingPrerequisite, svc.StatusOf(svc.FindOffered("EL307"), out _));
            Assert.AreEqual(CourseStatus.CanRegister, svc.StatusOf(svc.FindOffered("EL301"), out _));
            foreach (var c in Plan(3, 1)) svc.Add(c, out _);
            Assert.IsTrue(svc.Add("EL301", out var r), r);
            svc.Confirm(out _);
            foreach (var e in svc.CurrentEnrollments()) Attend(e);
            svc.RecordExam(false, 1f, 0f, 2); svc.RecordExam(true, 1f, 0f, 3);
            svc.CloseCurrentTerm();

            svc.OpenTerm(3, 1);
            Assert.AreEqual(CourseStatus.Passed, svc.StatusOf(svc.FindOffered("EL301"), out _));
            Assert.AreEqual(CourseStatus.CanRegister, svc.StatusOf(svc.FindOffered("EL307"), out _));
            Assert.IsTrue(svc.Add("EL307", out var r2), r2);
        }

        // ---------- จำลองครบ 8 ภาค ----------
        [Test]
        public void FullPlan_8Terms_Gives120_And_Graduates()
        {
            int[] expectYearAfter = { 1, 2, 2, 3, 3, 4, 4, 4 };
            int k = 0;
            TermCloseResult last = null;
            for (int y = 1; y <= 4; y++)
                for (int s = 0; s < 2; s++, k++)
                {
                    Assert.AreEqual(y, svc.Record.classYear, $"ชั้นปีก่อนภาค {y}/{s + 1}");
                    last = PlayTerm(y, s, PlanWithElective(y, s), c => true, 1f);
                    Assert.AreEqual(expectYearAfter[k], svc.Record.classYear, $"ชั้นปีหลังภาค {y}/{s + 1}");
                    if (k < 7) Assert.IsFalse(last.graduated, $"ยังไม่ควรจบหลังภาค {y}/{s + 1}");
                }
            Assert.AreEqual(120, svc.EarnedCredits());
            Assert.AreEqual(8, svc.Record.regularTermsStarted);
            Assert.AreEqual(4f, svc.Gpa(), 1e-4);
            Assert.IsTrue(last.graduated);
            Assert.IsTrue(svc.CheckGraduation().eligible);
        }

        [Test]
        public void HeldBack_When_CreditsBelow24()
        {
            // ปี 1 ภาค 1 ผ่านแค่ 3 วิชา (9) + ภาค 2 ลงได้ 3 วิชา (9) = 18 < 24 → ยังเป็นชั้นปี 1
            PlayTerm(1, 0, Plan(1, 1), c => c == "GE101" || c == "GE103" || c == "CS101", 0f);
            var r = PlayTerm(1, 1, new[] { "GE105", "GE106", "CS104" }, c => true, 0f);
            Assert.AreEqual(18, svc.EarnedCredits());
            Assert.IsFalse(r.promoted);
            Assert.AreEqual(1, svc.Record.classYear);
            svc.OpenTerm(2, 0);                                       // ปีการศึกษาที่ 2 แต่ยังชั้นปี 1
            Assert.AreEqual(1, svc.Term.classYear);
            Assert.AreEqual(3, svc.Term.serial);
            Assert.AreEqual(OfferGroup.Plan, svc.FindOffered("CS102").group);
        }

        [Test]
        public void ExtraTerm_Opens_When_Year4_Done_But_NotGraduated()
        {
            // สอบได้ 0 ทุกครั้ง: วิชาที่เข้าเรียนได้ C, CS404 ไม่เข้าเรียนเลย = F
            for (int y = 1; y <= 4; y++)
                for (int s = 0; s < 2; s++)
                    PlayTerm(y, s, PlanWithElective(y, s), c => c != "CS404", 0f);
            Assert.IsFalse(svc.IsPassed("CS404"));
            Assert.IsFalse(svc.Record.graduated);
            Assert.IsTrue(svc.Record.finishedPlan);
            Assert.AreEqual(117, svc.EarnedCredits());
            svc.OpenTerm(5, 0);
            Assert.IsTrue(svc.Term.isExtra);
            var oc = svc.FindOffered("CS404");
            Assert.IsNotNull(oc);
            Assert.IsTrue(svc.Add("CS404", out var r), r);
        }

        // ---------- เงื่อนไขจบ ----------
        [Test]
        public void EnoughCredits_But_MissingRequired_DoesNotGraduate()
        {
            foreach (var d in cur.courses)
            {
                if (d.code == "CS308") continue;
                if (d.IsElective && d.code != "EL301" && d.code != "EL302" && d.code != "EL303" && d.code != "EL304") continue;
                svc.Record.enrollments.Add(Graded(d.code, 1, 4f, "A"));
            }
            Assert.GreaterOrEqual(svc.EarnedCredits(), 120);
            var g = svc.CheckGraduation();
            Assert.IsFalse(g.eligible);
            StringAssert.Contains("CS308", string.Join("|", g.reasons));
        }

        [Test]
        public void LowGpa_DoesNotGraduate()
        {
            foreach (var d in cur.courses)
            {
                if (d.IsElective && d.code != "EL301" && d.code != "EL307") continue;
                svc.Record.enrollments.Add(Graded(d.code, 1, 1f, "D"));
            }
            Assert.AreEqual(120, svc.EarnedCredits());
            var g = svc.CheckGraduation();
            Assert.IsFalse(g.eligible);
            StringAssert.Contains("GPA", string.Join("|", g.reasons));
        }

        [Test]
        public void Gpa_NoGrades_IsZero_NoDivideByZero()
        {
            Assert.IsFalse(svc.HasGpa);
            Assert.AreEqual(0f, svc.Gpa());
            Assert.AreEqual(0f, svc.TermGpa(1));
        }

        [Test]
        public void Gpa_IsCreditWeighted_UsingLatestAttempt()
        {
            svc.Record.enrollments.Add(Graded("CS401", 1, 4f, "A"));   // 6 หน่วยกิต
            svc.Record.enrollments.Add(Graded("GE101", 1, 1f, "D"));   // 3 หน่วยกิต
            Assert.AreEqual((4f * 6 + 1f * 3) / 9f, svc.Gpa(), 1e-4);
            svc.Record.enrollments.Add(Graded("MA101", 1, 0f, "F"));
            var a = Graded("MA101", 2, 4f, "A"); a.attempt = 2; svc.Record.enrollments.Add(a);
            Assert.AreEqual((4f * 6 + 1f * 3 + 4f * 3) / 12f, svc.Gpa(), 1e-4);
        }

        // ---------- Save / Load ----------
        [Test]
        public void SaveLoad_RoundTrip_KeepsState()
        {
            PlayTerm(1, 0, Plan(1, 1), c => c != "MA101", 0.5f);
            svc.OpenTerm(1, 1);
            foreach (var c in Plan(1, 2)) svc.Add(c, out _);
            svc.Confirm(out _);
            var e0 = svc.CurrentEnrollments()[0];
            var s0 = svc.SessionsFor(e0)[0];
            svc.RecordStudyTick(s0.day, s0.startMinute + 60, s0.building, 0.7f);
            svc.RecordExam(false, 0.66f, 0f, 2);

            var data = new SaveData { currentYear = 1, dayInYear = 5, calendarYear = 1, money = 42 };
            data.hasAcademicRecord = true; data.academic = svc.Record;
            string json = JsonUtility.ToJson(data);
            var back = JsonUtility.FromJson<SaveData>(json);
            var svc2 = new RegistrationService(cur, back.academic);

            Assert.IsTrue(back.hasAcademicRecord);
            Assert.AreEqual(42, back.money);
            Assert.AreEqual(svc.EarnedCredits(), svc2.EarnedCredits());
            Assert.AreEqual(svc.Gpa(), svc2.Gpa(), 1e-5);
            Assert.AreEqual(svc.Record.classYear, svc2.Record.classYear);
            Assert.AreEqual(svc.Term.serial, svc2.Term.serial);
            Assert.IsTrue(svc2.Term.confirmed);
            Assert.AreEqual(svc.CurrentEnrollments().Count, svc2.CurrentEnrollments().Count);
            var e1 = svc2.CurrentEnrollments()[0];
            Assert.AreEqual(e0.progress, e1.progress, 1e-5);
            Assert.AreEqual(e0.midterm, e1.midterm, 1e-5);
            Assert.AreEqual(1, e1.TicksFor(s0.day + ":0"));
            Assert.AreEqual(svc.Attempts("MA101").Count, svc2.Attempts("MA101").Count);
            Assert.IsFalse(svc2.Confirm(out _));                     // ยืนยันซ้ำหลังโหลดก็ไม่ซ้ำ
            Assert.AreEqual(svc.Record.enrollments.Count, svc2.Record.enrollments.Count);
        }

        [Test]
        public void OldSave_WithoutAcademicFields_Loads_And_Migrates()
        {
            // เซฟรุ่นก่อนมีระบบลงทะเบียน: ไม่มี calendarYear / hasAcademicRecord / academic
            string oldJson = "{\"energy\":80,\"money\":123,\"knowledge\":500,\"currentYear\":2,\"dayInYear\":5,\"facultyIndex\":0,\"gradePoints\":[3.0,2.0]}";
            var d = JsonUtility.FromJson<SaveData>(oldJson);
            Assert.AreEqual(123, d.money);
            Assert.AreEqual(2, d.currentYear);
            Assert.AreEqual(0, d.calendarYear);
            Assert.IsFalse(d.hasAcademicRecord);
            Assert.AreEqual(2, d.gradePoints.Count);                  // ความคืบหน้าเดิมยังอยู่

            // ปฏิทินรุ่น 2: เซฟเก่า (ปีละ 8 วัน) แปลงวันก่อน — วันที่ 5 เดิม = ภาคปลายวันที่ 2 (สอบกลางภาค) → วันที่ 15 (ภาคปลายวันที่ 5)
            Assert.IsTrue(CalendarMigration.Upgrade(d));
            Assert.AreEqual(15, d.dayInYear);
            int sem = AcademicCalendar.SemesterIndex(d.dayInYear);   // ภาคปลาย
            Assert.AreEqual(1, sem);
            var rec = RegistrationService.MigrateLegacy(cur, d.currentYear, sem);
            var s2 = new RegistrationService(cur, rec);
            Assert.AreEqual(2, rec.classYear);
            Assert.AreEqual(54, s2.EarnedCredits());                  // เทียบโอน 1/1 1/2 2/1
            Assert.IsFalse(s2.HasGpa);                                // เทียบโอนไม่คิด GPA
            s2.OpenTerm(2, sem, lateRegistration: true);
            Assert.AreEqual(2, s2.Term.planSemester);
            Assert.AreEqual(4, s2.Term.serial);
            Assert.IsTrue(s2.RegistrationWindowOpen(3));              // ย้ายเซฟ = ลงได้ถึงสิ้นภาค
            Assert.IsTrue(s2.Add("CS205", out var r), r);             // วิชาบังคับก่อนผ่านจากการเทียบโอน
        }
    }
}
#endif
