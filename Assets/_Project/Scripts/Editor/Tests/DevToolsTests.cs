using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.Academics.ExamMinigame;
using NisitSimulator.DevTools;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Tests
{
    // EditMode tests ของ Dev Testing Panel (Window ▸ General ▸ Test Runner ▸ EditMode)
    //   • สถานการณ์สำเร็จรูปทั้ง 9 ต้องสร้างได้ด้วยกฎจริง และประวัติ/หน่วยกิต/GPA สอดคล้องกัน (คำนวณซ้ำจากประวัติได้ค่าเดิม)
    //   • โปรไฟล์ Dev ไม่เขียน/ลบเซฟจริง
    //   • Preview สร้างจากคลังจริง: seed เดิม = ชุดเดิม · กรองชนิด/จำนวน/เวลา · สิทธิ์คำใบ้ตามความรู้
    //   (พฤติกรรมที่ต้องใช้ฉากจริง — หมดเวลา/ส่งซ้ำ/โหลดระหว่างสอบ/เข้าเรียนไม่เปิดมินิเกม — ตรวจด้วย Dev Panel ▸ ผลทดสอบ ใน Play Mode)
    public class DevToolsTests
    {
        CurriculumDefinition cur;

        [SetUp] public void SetUp() { cur = CsCurriculumDefaults.Create(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(cur); }

        static IEnumerable<string> ScenarioIds() { foreach (var d in DevScenarios.All) yield return d.id; }

        [TestCaseSource(nameof(ScenarioIds))]
        public void Scenario_BuildsWithRealRules_AndMeetsItsCondition(string id)
        {
            var r = DevScenarios.Build(id, cur, 0);
            Assert.IsFalse(r.skipped, $"{id} ถูกข้าม: {r.skipReason}");
            Assert.IsTrue(r.conditionOk, $"{id}: {r.conditionText}");
            Assert.IsNotNull(r.record.Current, "ต้องมีภาคปัจจุบัน");
            Assert.AreEqual(r.calendarYear, r.record.Current.calendarYear, "ปีการศึกษาของเกมต้องตรงกับภาคในประวัติ");
            Assert.AreEqual(r.record.Current.semIndex, NisitSimulator.Systems.AcademicCalendar.SemesterIndex(r.dayInYear), "วันในปีต้องอยู่ในภาคเดียวกับภาคปัจจุบัน");
        }

        [TestCaseSource(nameof(ScenarioIds))]
        public void Scenario_HistoryIsConsistent_NotFlags(string id)
        {
            var r = DevScenarios.Build(id, cur, 0);
            Assume.That(!r.skipped);
            // โหลดประวัติเข้าบริการใหม่ (เหมือนโหลดเซฟ) → หน่วยกิต/GPA ต้องคำนวณได้จากประวัติรายวิชาเท่านั้น
            var json = JsonUtility.ToJson(r.record);
            var svc = new RegistrationService(cur, JsonUtility.FromJson<AcademicRecord>(json));
            var passed = new HashSet<string>();
            int serialMax = 0;
            foreach (var e in svc.Record.enrollments)
            {
                Assert.IsFalse(e.transfer, $"{e.code}: สถานการณ์ต้องไม่ใช้การเทียบโอน");
                serialMax = Mathf.Max(serialMax, e.termSerial);
                if (!e.graded) continue;
                var g = cur.GradeFor(e.score);
                Assert.AreEqual(g.letter, e.letter, $"{e.code}: เกรดต้องมาจากคะแนนรวม");
                Assert.AreEqual(g.point + 1e-4f >= cur.passPoint, e.passed, $"{e.code}: ผ่าน/ไม่ผ่านต้องสอดคล้องกับเกรด");
                if (e.passed) passed.Add(e.code);
                // ลงได้เฉพาะเมื่อผ่านวิชาบังคับก่อนแล้ว ในภาคก่อนหน้า
                foreach (var pre in cur.Get(e.code).prerequisites)
                {
                    bool ok = false;
                    foreach (var p in svc.Record.enrollments) if (p.code == pre && p.graded && p.passed && p.termSerial < e.termSerial) ok = true;
                    Assert.IsTrue(ok, $"{e.code} (ภาค {e.termSerial}) ลงโดยยังไม่ผ่าน {pre}");
                }
            }
            int credits = 0; foreach (var c in passed) credits += cur.Get(c).credits;
            Assert.AreEqual(credits, svc.EarnedCredits(), "หน่วยกิตสะสม = ผลรวมหน่วยกิตของรหัสวิชาที่ผ่าน (นับครั้งเดียว)");
            Assert.AreEqual(serialMax <= svc.Record.regularTermsStarted, true, "ลำดับภาคในประวัติไม่เกินจำนวนภาคที่เปิด");
        }

        [Test]
        public void Scenario_S6_Retake_ThenPass_CreditsCountedOnce()
        {
            var b = new DevHistoryBuilder(cur);
            b.Quality = (code, att, y) => code == "CS101" && att == 1 ? 0.2f : 0.75f;
            Assert.IsTrue(b.AdvanceToTerm(1, 2), b.Error);
            int before = b.Svc.EarnedCredits();
            b.CompleteTerm();   // ภาค 1/2: ลง CS101 ซ้ำ (ภาคค่ำ) + แผน แล้วผ่าน
            var att = b.Svc.Attempts("CS101");
            Assert.AreEqual(2, att.Count);
            Assert.IsFalse(att[0].passed); Assert.IsTrue(att[1].passed); Assert.IsTrue(att[1].retakeSection);
            int cs101 = 0; foreach (var c in b.Svc.PassedCodes()) if (c == "CS101") cs101++;
            Assert.AreEqual(1, cs101);
            Assert.AreEqual(before + 3 + 15, b.Svc.EarnedCredits(), "CS101 นับ 3 หน่วยกิตครั้งเดียว + แผน 1/2 ที่ลงได้ 15 (CS104 ติด prerequisite)");
        }

        [Test]
        public void Builder_Refuses_History_That_Would_Retire_On_Gpa()
        {
            var b = new DevHistoryBuilder(cur) { MinGpa = 2f, GpaCheckFromYear = 2 };
            b.Quality = (code, att, y) => 0.45f;   // D ทุกวิชา → GPA 1.0
            Assert.IsFalse(b.AdvanceToTerm(3, 1));
            StringAssert.Contains("รีไทร์", b.Error);
        }

        [Test]
        public void CustomHistory_ForcedFail_BlocksDependentCourse()
        {
            var r = DevScenarios.BuildCustom(cur, 0, 1, 2, 0.75f, new[] { "CS102" });
            Assert.IsFalse(r.skipped, r.skipReason);
            var svc = new RegistrationService(cur, r.record);
            Assert.IsFalse(svc.CanAdd("CS103", out var why));
            StringAssert.Contains("CS102", why);
            Assert.AreEqual(15, svc.EarnedCredits());
        }

        // ---------- เซฟแยก ----------
        [Test]
        public void DevProfile_Guard_NeverWritesOrDeletesRealSlot()
        {
            string real = SaveSystem.RealSlotPath(GameSession.SaveSlot);
            string fp0 = DevProfile.Fingerprint(real);
            string dir = Path.Combine(Application.persistentDataPath, "dev_test");
            Directory.CreateDirectory(dir);
            string devPath = Path.Combine(dir, "unit_test_dev_save.json");
            bool g = SaveSystem.DevGuard; string o = SaveSystem.DevPathOverride, ro = SaveSystem.DevReadOverride; int blocked0 = SaveSystem.DevBlockedRealWrites;
            try
            {
                SaveSystem.DevGuard = true;
                SaveSystem.DevPathOverride = devPath;
                SaveSystem.DevReadOverride = null;
                SaveSystem.Save(new SaveData { money = 424242 });
                Assert.IsTrue(File.Exists(devPath), "เซฟทดสอบต้องถูกเขียน");
                Assert.AreEqual(424242, SaveSystem.Load().money, "โหลดจากเซฟทดสอบ");

                SaveSystem.DevPathOverride = null;          // กรณีเส้นทางเซฟทดสอบหาย → ต้องไม่ตกไปเขียนช่องจริง
                SaveSystem.Save(new SaveData { money = 1 });
                SaveSystem.DeleteSave();
                SaveSystem.DeleteSlot(GameSession.SaveSlot);
                Assert.AreEqual(blocked0 + 3, SaveSystem.DevBlockedRealWrites, "เขียน 1 + ลบ 2 ต้องถูกกันทั้งหมด");
                Assert.AreEqual(fp0, DevProfile.Fingerprint(real), "ไฟล์เซฟจริงต้องไม่เปลี่ยน");
            }
            finally
            {
                SaveSystem.DevGuard = g; SaveSystem.DevPathOverride = o; SaveSystem.DevReadOverride = ro;
                if (File.Exists(devPath)) File.Delete(devPath);
            }
        }

        [Test]
        public void DevReadOverride_ReadsSnapshot_UntilNextRealSave()
        {
            string dir = Path.Combine(Application.persistentDataPath, "dev_test");
            Directory.CreateDirectory(dir);
            string snap = Path.Combine(dir, "unit_test_snapshot.json");
            bool g = SaveSystem.DevGuard; string o = SaveSystem.DevPathOverride, ro = SaveSystem.DevReadOverride;
            try
            {
                File.WriteAllText(snap, JsonUtility.ToJson(new SaveData { money = 777 }));
                SaveSystem.DevGuard = true;           // ช่วงเปลี่ยนฉากตอนออกจากโหมดทดสอบ
                SaveSystem.DevPathOverride = null;
                SaveSystem.DevReadOverride = snap;
                Assert.AreEqual(777, SaveSystem.Load().money, "ทุกระบบอ่านสถานะเดิมจากสำเนา");
                SaveSystem.Save(new SaveData());      // ยัง guard อยู่ → ต้องไม่เขียนและยังอ่านสำเนา
                Assert.AreEqual(snap, SaveSystem.DevReadOverride);
            }
            finally
            {
                SaveSystem.DevGuard = g; SaveSystem.DevPathOverride = o; SaveSystem.DevReadOverride = ro;
                if (File.Exists(snap)) File.Delete(snap);
            }
        }

        // ---------- Preview ----------
        [Test]
        public void Preview_SameSeed_SameSet_TypeFilter_Count_Time_Hints()
        {
            var db = ExamBankDatabase.LoadDefault();
            string code = null;
            foreach (var b in db.banks) if (b.questions.Count > 0) { code = b.courseCode; break; }
            Assume.That(code != null, "ไม่มีคลังข้อสอบ");
            var a = DevExamTools.BuildPreview(code, null, 4, 90f, 777, 75f, out _);
            var b2 = DevExamTools.BuildPreview(code, null, 4, 90f, 777, 75f, out _);
            Assert.AreEqual(DevExamTools.Fingerprint(a), DevExamTools.Fingerprint(b2), "seed เดิม → ชุดข้อ/ลำดับเดิม");
            Assert.AreEqual(4, a.questions.Count);
            Assert.AreEqual(90f, a.timeLimit); Assert.AreEqual(90f, a.remainingSeconds);
            Assert.AreEqual(2, a.hintsAllowed, "ความรู้ 75 → คำใบ้ 2");
            Assert.AreEqual(0, DevExamTools.BuildPreview(code, null, 4, 90f, 777, 10f, out _).hintsAllowed, "ความรู้ 10 → ไม่มีคำใบ้");
            foreach (ExamQuestionType t in System.Enum.GetValues(typeof(ExamQuestionType)))
            {
                int n = DevExamTools.CountOfType(code, t);
                var s = DevExamTools.BuildPreview(code, t, 99, 60f, 5, 0f, out _);
                if (n == 0) { Assert.IsNull(s); continue; }
                Assert.AreEqual(n, s.questions.Count, "ขอเกินคลัง → ได้เท่าที่มี");
                foreach (var q in s.questions) Assert.AreEqual((int)t, q.type);
            }
        }

        [Test]
        public void Preview_Session_IsNotPersisted_And_GradesWithRealLogic()
        {
            var db = ExamBankDatabase.LoadDefault();
            string code = null;
            foreach (var b in db.banks) if (b.questions.Count > 0) { code = b.courseCode; break; }
            Assume.That(code != null);
            var s = DevExamTools.BuildPreview(code, null, 3, 60f, 42, 0f, out _);
            var bank = db.Get(code);
            // ตอบถูกทุกข้อผ่านฟังก์ชันเดียวกับ UI แล้วส่งด้วยตัวตรวจจริง
            foreach (var st in s.questions)
            {
                var q = bank.Find(st.questionId);
                switch (q.type)
                {
                    case ExamQuestionType.MultipleChoice:
                    case ExamQuestionType.FindError: ExamMinigameLogic.Select(s, st, q.correctIndex); break;
                    case ExamQuestionType.Ordering: for (int i = 0; i < q.items.Count; i++) ExamMinigameLogic.AppendOrder(s, st, i); break;
                    case ExamQuestionType.Matching: for (int i = 0; i < q.items.Count; i++) ExamMinigameLogic.SetMatch(s, st, i, i); break;
                }
            }
            s.started = true;
            ExamMinigameLogic.Submit(s, bank);
            Assert.AreEqual(100, s.score100);
            Assert.IsFalse(s.recorded, "Preview ไม่ถูกบันทึกลงระบบเกรด");
            StringAssert.StartsWith("PREVIEW", s.roundKey);
        }
    }
}
