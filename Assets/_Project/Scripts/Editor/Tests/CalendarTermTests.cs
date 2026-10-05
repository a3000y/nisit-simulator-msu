#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;

namespace NisitSimulator.Tests
{
    // EditMode tests: ปฏิทินรุ่น 2 (10/10/3) · ตารางเรียน 4 ครั้ง/ภาค · แปลงเซฟเก่า · นาฬิกา 20 นาที/วัน · รายงานผลเทอม
    public class CalendarTermTests
    {
        CurriculumDefinition cur;
        ClassroomCatalog cat;

        [SetUp] public void SetUp() { cur = CsCurriculumDefaults.Create(); cat = ClassroomDefaults.Create(); GameClock.WarpMultiplier = 1f; }
        [TearDown] public void TearDown() { Object.DestroyImmediate(cur); Object.DestroyImmediate(cat); GameClock.WarpMultiplier = 1f; }

        // ---------- ปฏิทิน ----------
        [Test]
        public void Calendar_31Days_TermsAndExamDays()
        {
            Assert.AreEqual(31, AcademicCalendar.TotalDays);
            Assert.AreEqual(14, AcademicCalendar.SemesterLen(0));
            Assert.AreEqual(14, AcademicCalendar.SemesterLen(1));
            Assert.AreEqual(3, AcademicCalendar.SemesterLen(2));
            for (int d = 1; d <= 31; d++)
            {
                int sem = d <= 14 ? 0 : d <= 28 ? 1 : 2;
                int sd = d <= 14 ? d : d <= 28 ? d - 14 : d - 28;
                Assert.AreEqual(sem, AcademicCalendar.SemesterIndex(d), "วัน " + d);
                Assert.AreEqual(sd, AcademicCalendar.SemesterDay(d), "วัน " + d);
                Assert.AreEqual(d, AcademicCalendar.DayInYear(sem, sd));
            }
            Assert.AreEqual(6, ExamController.MidtermDay(0)); Assert.AreEqual(13, ExamController.FinalDay(0));
            Assert.AreEqual(6, ExamController.MidtermDay(1)); Assert.AreEqual(13, ExamController.FinalDay(1));
            Assert.IsTrue(AcademicCalendar.HasMidterm(0)); Assert.IsTrue(AcademicCalendar.HasMidterm(1));
            Assert.IsFalse(AcademicCalendar.HasMidterm(2), "ฤดูร้อนไม่มีสอบกลางภาค");
            // 12 เดือนกระจายทั้งปี
            Assert.AreEqual("มิถุนายน", AcademicCalendar.MonthName(1));
            Assert.AreEqual("พฤษภาคม", AcademicCalendar.MonthName(31));
        }

        [Test]
        public void Progression_DaysPerYear_ComesFromCalendar()
        {
            var go = new GameObject("prog");
            var p = go.AddComponent<ProgressionManager>();
            p.daysPerYear = 8;   // ค่าในฉากเก่า
            typeof(ProgressionManager).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(p, null);
            Assert.AreEqual(AcademicCalendar.TotalDays, p.daysPerYear);
            Assert.AreEqual(new[] { 1150f, 2450f, 3900f, 5450f }, p.knowledgeTargets);
            Object.DestroyImmediate(go);
        }

        // ---------- ตารางเรียน ----------
        [Test]
        public void Schedule_FourMeetings_NoExamDayClasses_NoConflicts_FreePeriodEveryDay()
        {
            int mid = ExamController.MidtermDay(0), fin = ExamController.FinalDay(0);
            foreach (var d in cur.courses)
            {
                Assert.AreEqual(4, d.sessions.Count, d.code + " ตอนปกติ 4 ครั้ง");
                if (d.IsRequired) Assert.AreEqual(4, d.retakeSessions.Count, d.code + " ภาคค่ำ 4 ครั้ง");
                foreach (var s in d.sessions) { Assert.AreNotEqual(mid, s.day); Assert.AreNotEqual(fin, s.day); Assert.That(s.day, Is.InRange(2, 12)); }
                foreach (var s in d.retakeSessions) { Assert.AreNotEqual(mid, s.day); Assert.AreNotEqual(fin, s.day); }
                // 2 ครั้งก่อนสอบกลางภาค 2 ครั้งหลัง
                Assert.AreEqual(2, d.sessions.FindAll(s => s.day < mid).Count, d.code);
            }
            CollectionAssert.IsEmpty(cur.Validate());
            var issues = RegistrationService.FindRoomConflicts(cur, cat, true);
            CollectionAssert.IsEmpty(issues, string.Join("\n", issues.ConvertAll(i => i.ToString())));

            // แผนภาคที่มี 6 วิชา: ทุกวันเรียนมีคาบว่างอย่างน้อย 1 คาบ (09–17)
            var plan = cur.PlanCourses(1, 1);
            for (int day = 1; day <= 12; day++)
            {
                if (day == mid) continue;
                int used = 0;
                foreach (var c in plan) foreach (var s in c.sessions) if (s.day == day && s.startMinute >= 540 && s.endMinute <= 1020) used += (s.endMinute - s.startMinute) / 120;
                Assert.Less(used, 4, $"วันที่ {day} ต้องเหลือคาบว่าง");
            }
            // ชั่วโมงเรียนต่อวิชา = 8 ชม. (ฝึกงาน/โครงงาน 2 = 16 ชม.)
            var svc = new RegistrationService(cur, new AcademicRecord());
            Assert.AreEqual(8f, svc.ExpectedHours(new Enrollment { code = "CS102" }));
            Assert.AreEqual(16f, svc.ExpectedHours(new Enrollment { code = "CS401" }));
        }

        // ---------- เซฟเก่า ----------
        [Test]
        public void Migration_MapsEveryLegacyDay()
        {
            int[] expect = { 1, 6, 13, 15, 20, 27, 29, 31 };   // วันเดิม 1..8 → วันใหม่
            for (int old = 1; old <= 8; old++)
                Assert.AreEqual(expect[old - 1], CalendarMigration.MapLegacyDay(old), "วันเดิม " + old);
        }

        [Test]
        public void Migration_KeepsProgress_ClearsMeetingKeys_RunsOnce()
        {
            var rec = new AcademicRecord { hasCurrent = true, current = new TermState { serial = 3, semIndex = 1, planSemester = 2, confirmed = true } };
            var e = new Enrollment { code = "CS103", termSerial = 3, progress = 2.5f, midterm = 0.7f, classBonusFinal = 0.03f };
            e.AddTick("1:0"); ClassEventLogic.SetState(e, "1:0", ClassEventLogic.Done);
            var old = new Enrollment { code = "CS101", termSerial = 1, graded = true, passed = true };
            old.AddTick("1:0");
            rec.enrollments.Add(e); rec.enrollments.Add(old);
            var d = new SaveData { dayInYear = 5, hasAcademicRecord = true, academic = rec };
            Assert.AreEqual(0, d.calendarVersion);
            Assert.IsTrue(CalendarMigration.Upgrade(d));
            Assert.AreEqual(20, d.dayInYear);
            Assert.AreEqual(2.5f, e.progress); Assert.AreEqual(0.7f, e.midterm); Assert.AreEqual(0.03f, e.classBonusFinal);
            Assert.AreEqual(0, e.meetingKeys.Count); Assert.AreEqual(0, e.classEventKeys.Count);
            Assert.AreEqual(1, old.meetingKeys.Count, "ภาคที่ประกาศผลแล้วไม่แตะ");
            Assert.IsFalse(CalendarMigration.Upgrade(d), "แปลงครั้งเดียว");
            Assert.AreEqual(20, d.dayInYear);
            // เซฟใหม่ไม่ถูกแปลง
            var fresh = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { dayInYear = 7, calendarVersion = AcademicCalendar.Version }));
            Assert.IsFalse(CalendarMigration.Upgrade(fresh)); Assert.AreEqual(7, fresh.dayInYear);
        }

        // ---------- นาฬิกา ----------
        [Test]
        public void Clock_OneDayIs20Minutes_AndClassWarpStillSeconds()
        {
            var go = new GameObject("clock");
            var c = go.AddComponent<GameClock>();
            Assert.AreEqual(1.2f, c.gameMinutesPerRealSecond, 1e-4f);
            Assert.AreEqual(1200f, 1440f / c.gameMinutesPerRealSecond, 0.01f, "24 ชม. เกม = 1200 วินาทีจริง");
            float m = ClassWarpRules.Multiplier(c.gameMinutesPerRealSecond, 35f, 120f, 0.016f);
            Assert.AreEqual(35f / 1.2f, m, 1e-3f);
            Assert.That(120f / (c.gameMinutesPerRealSecond * m), Is.InRange(3f, 4f), "คาบ 2 ชม. ≈ 3–4 วินาทีจริง");
            Object.DestroyImmediate(go);
        }

        // ---------- รายงานผลเทอม ----------
        RegistrationService PlayTerm(out TermCloseResult res, System.Func<string, bool> attend, float quiz)
        {
            var svc = new RegistrationService(cur, new AcademicRecord());
            svc.OpenTerm(1, 0);
            foreach (var d in cur.PlanCourses(1, 1)) Assert.IsTrue(svc.Add(d.code, out var r), r);
            Assert.IsTrue(svc.Confirm(out var cr), cr);
            foreach (var e in svc.CurrentEnrollments())
                if (attend(e.code))
                    foreach (var s in svc.SessionsFor(e))
                        for (int k = 1; k <= s.MaxTicks; k++) svc.RecordStudyTick(s.day, s.startMinute + 60 * k, s.building, 1f);
            svc.RecordExam(false, quiz, 0f, ExamController.MidtermDay(0));
            svc.RecordExam(true, quiz, 0f, ExamController.FinalDay(0));
            res = svc.CloseCurrentTerm();
            return svc;
        }

        [Test]
        public void Report_GpaCreditsAndRetakes()
        {
            var svc = PlayTerm(out var res, code => code != "CS102", 1f);
            var rep = svc.BuildTermReport(res, 2f);
            Assert.AreEqual(6, rep.rows.Count);
            Assert.AreEqual(18, rep.creditsAttempted);
            var cs102 = rep.rows.Find(r => r.code == "CS102");
            Assert.AreEqual(0f, cs102.attendance, 1e-4f);
            int earned = 0; foreach (var r in rep.rows) if (r.passed) earned += r.credits;
            Assert.AreEqual(earned, rep.creditsEarnedTerm);
            Assert.AreEqual(svc.EarnedCredits(), rep.creditsEarnedTotal);
            Assert.AreEqual(svc.TermGpa(res.term.serial), rep.termGpa, 1e-4f);
            Assert.AreEqual(svc.Gpa(), rep.cumulativeGpa, 1e-4f);
            Assert.AreEqual(120, rep.graduationCredits);
            if (!cs102.passed) CollectionAssert.Contains(rep.retakeCodes, "CS102");
            Assert.IsFalse(rep.promoted);
            Assert.AreEqual(0, rep.promotionShortfall, "ภาค 1 ยังไม่ตัดสินเลื่อนชั้น");
            StringAssert.Contains("ภาคปลาย", rep.nextStep);
            StringAssert.Contains("CS102", TermResultUI.TableText(rep));
        }

        [Test]
        public void Report_ProbationAndPromotionShortfall()
        {
            var svc = PlayTerm(out var res, _ => false, 0f);   // ไม่เข้าเรียน + สอบ 0 → ตกหมด
            var rep = svc.BuildTermReport(res, 2f);
            Assert.IsTrue(rep.probation);
            StringAssert.Contains("ติดโปร", TermResultUI.StatusText(rep));
            Assert.AreEqual(6, rep.retakeCodes.Count);
            // ภาค 2 ไม่ผ่าน → ไม่เลื่อนชั้น ขาด 24 หน่วยกิต
            svc.OpenTerm(1, 1);
            foreach (var d in cur.PlanCourses(1, 2)) svc.Add(d.code, out _);
            foreach (var d in cur.PlanCourses(1, 1)) svc.Add(d.code, out _);
            svc.Confirm(out _);
            var res2 = svc.CloseCurrentTerm();
            var rep2 = svc.BuildTermReport(res2, 2f);
            Assert.IsFalse(rep2.promoted);
            Assert.AreEqual(24, rep2.promotionShortfall);
            StringAssert.Contains("ยังไม่เลื่อนชั้น", TermResultUI.StatusText(rep2));
        }

        [Test]
        public void Report_ShownOnce_PendingSurvivesSaveLoad()
        {
            var svc = PlayTerm(out var res, _ => true, 1f);
            var rec = svc.Record;
            rec.pendingReportSerial = res.term.serial;   // CourseRegistrar ตั้งตอนปิดภาค
            Assert.IsTrue(rec.HasPendingReport);
            var back = JsonUtility.FromJson<AcademicRecord>(JsonUtility.ToJson(rec));
            Assert.IsTrue(back.HasPendingReport, "ปิดเกมก่อนกดตกลง → โหลดแล้วยังค้าง");
            var svc2 = new RegistrationService(cur, back);
            var rep = svc2.BuildTermReport(back.pendingReportSerial, 2f, false, false, false);
            Assert.AreEqual(6, rep.rows.Count, "สร้างรายงานจากเซฟได้");
            // กดตกลง
            back.lastReportedSerial = back.pendingReportSerial; back.pendingReportSerial = 0;
            Assert.IsFalse(JsonUtility.FromJson<AcademicRecord>(JsonUtility.ToJson(back)).HasPendingReport);
            // เซฟเก่าไม่มีฟิลด์ = ไม่แสดง
            Assert.IsFalse(JsonUtility.FromJson<AcademicRecord>("{\"classYear\":1}").HasPendingReport);
        }
    }
}
#endif
