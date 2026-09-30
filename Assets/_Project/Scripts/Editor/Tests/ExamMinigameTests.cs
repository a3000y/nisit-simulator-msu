#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.Academics.ExamMinigame;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Tests
{
    // EditMode tests ของมินิเกมสอบ (Window ▸ General ▸ Test Runner ▸ EditMode)
    //   ตรรกะล้วน: คลังข้อสอบ · สุ่มรอบ · ตรวจคำตอบ 4 แบบ · คำใบ้ · ส่งครั้งเดียว · Save/Load JSON · บันทึกคะแนนรายวิชาครั้งเดียว
    public class ExamMinigameTests
    {
        ExamBankDatabase db;
        CurriculumDefinition cur;

        [SetUp] public void SetUp() { db = ExamBankDefaults.Create(); cur = CsCurriculumDefaults.Create(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(db); Object.DestroyImmediate(cur); }

        ExamQuestion FirstOf(string code, ExamQuestionType t)
        {
            foreach (var q in db.Get(code).questions) if (q.type == t) return q;
            Assert.Fail($"ไม่มีข้อชนิด {t} ใน {code}");
            return null;
        }

        static ExamSessionState SessionFor(ExamQuestion q, int seed)
        {
            var bank = new CourseExamBank { courseCode = "T", questionsPerExam = 1, questions = new List<ExamQuestion> { q } };
            var tmp = ScriptableObject.CreateInstance<ExamBankDatabase>();
            var s = ExamMinigameLogic.CreateSession(tmp, bank, new System.Random(seed));
            Object.DestroyImmediate(tmp);
            s.started = true;
            return s;
        }

        // ---------- คลังข้อสอบ ----------
        [Test]
        public void Banks_Valid_MappedToRealCourses_AtLeast10Each()
        {
            CollectionAssert.IsEmpty(db.Validate(cur));
            var expect = new Dictionary<string, string>
            {
                { "CS102", "การเขียนโปรแกรมเบื้องต้น" }, { "CS202", "ระบบฐานข้อมูล" },
                { "CS207", "เครือข่ายคอมพิวเตอร์" }, { "GE102", "ภาษาอังกฤษพื้นฐาน" },
            };
            foreach (var kv in expect)
            {
                Assert.AreEqual(kv.Value, cur.Get(kv.Key).title, "รหัสวิชาต้องตรงหลักสูตรเดิม");
                Assert.GreaterOrEqual(db.Get(kv.Key).questions.Count, 10, kv.Key);
            }
            var types = new HashSet<ExamQuestionType>();
            foreach (var b in db.banks) foreach (var q in b.questions) types.Add(q.type);
            Assert.AreEqual(4, types.Count, "ใช้ครบทั้ง 4 รูปแบบ");
            // วิชาโครงงาน/ฝึกงานใช้ระบบเดิม · วิชาที่ไม่มีคลัง = ไม่พร้อม (ไม่เอาข้อวิชาอื่นมาแทน)
            Assert.IsTrue(db.UsesLegacyAssessment(cur.Get("CS401")));
            Assert.IsTrue(db.UsesLegacyAssessment(cur.Get("CS403")));
            Assert.IsFalse(db.HasContent("GE101"));
            Assert.IsNull(db.Get("GE101"));
        }

        [Test]
        public void CreateSession_Defaults_5Questions_180s_NoDuplicates()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var s = ExamMinigameLogic.CreateSession(db, db.Get("CS102"), new System.Random(seed));
                Assert.AreEqual(5, s.questions.Count);
                Assert.AreEqual(180f, s.timeLimit);
                Assert.AreEqual(180f, s.remainingSeconds);
                var ids = new HashSet<string>();
                foreach (var q in s.questions) Assert.IsTrue(ids.Add(q.questionId), "ข้อซ้ำในรอบเดียว");
            }
        }

        // ---------- ตรวจคำตอบ ----------
        [Test]
        public void MultipleChoice_Score_Independent_Of_Shuffle()
        {
            var q = FirstOf("CS202", ExamQuestionType.MultipleChoice);
            for (int seed = 0; seed < 25; seed++)
            {
                var s = SessionFor(q, seed);
                var st = s.questions[0];
                ExamMinigameLogic.Select(s, st, q.correctIndex);
                Assert.AreEqual(q.points, ExamMinigameLogic.Grade(q, st), 1e-4f, $"seed {seed} order {string.Join(",", st.displayOrder)}");
                ExamMinigameLogic.Select(s, st, (q.correctIndex + 1) % q.items.Count);
                Assert.AreEqual(0f, ExamMinigameLogic.Grade(q, st));
            }
        }

        [Test]
        public void FindError_Correct_And_Wrong()
        {
            var q = FirstOf("CS102", ExamQuestionType.FindError);
            var s = SessionFor(q, 1); var st = s.questions[0];
            Assert.AreEqual(0f, ExamMinigameLogic.Grade(q, st), "ไม่ตอบ = 0");
            ExamMinigameLogic.Select(s, st, q.correctIndex);
            Assert.AreEqual(q.points, ExamMinigameLogic.Grade(q, st), 1e-4f);
            ExamMinigameLogic.Select(s, st, q.correctIndex == 0 ? 1 : 0);
            Assert.AreEqual(0f, ExamMinigameLogic.Grade(q, st));
        }

        [Test]
        public void Ordering_Full_Alternate_Partial_Empty()
        {
            var q = db.Get("CS102").Find("CS102-O01");   // มีลำดับทางเลือก "1,0,2,3,4,5"
            var s = SessionFor(q, 3); var st = s.questions[0];
            Assert.AreEqual(0f, ExamMinigameLogic.Grade(q, st));
            for (int i = 0; i < q.items.Count; i++) ExamMinigameLogic.AppendOrder(s, st, i);
            Assert.AreEqual(q.points, ExamMinigameLogic.Grade(q, st), 1e-4f);
            ExamMinigameLogic.ClearOrder(s, st);
            foreach (var i in new[] { 1, 0, 2, 3, 4, 5 }) ExamMinigameLogic.AppendOrder(s, st, i);
            Assert.AreEqual(q.points, ExamMinigameLogic.Grade(q, st), 1e-4f, "ลำดับทางเลือกต้องได้เต็ม");
            ExamMinigameLogic.ClearOrder(s, st);
            foreach (var i in new[] { 0, 1, 2, 4, 3, 5 }) ExamMinigameLogic.AppendOrder(s, st, i);
            Assert.AreEqual(q.points * 4f / 6f, ExamMinigameLogic.Grade(q, st), 1e-4f, "ถูกตำแหน่ง 4/6");
            ExamMinigameLogic.MoveOrderUp(s, st, 4);   // สลับกลับ → ถูกทั้งหมด
            Assert.AreEqual(q.points, ExamMinigameLogic.Grade(q, st), 1e-4f);
            ExamMinigameLogic.AppendOrder(s, st, 0);   // ใส่ซ้ำไม่ได้
            Assert.AreEqual(6, st.order.Count);
        }

        [Test]
        public void Matching_PartialCredit_PerPair()
        {
            var q = FirstOf("CS207", ExamQuestionType.Matching);   // 4 คู่
            var s = SessionFor(q, 5); var st = s.questions[0];
            int n = q.items.Count;
            ExamMinigameLogic.SetMatch(s, st, 0, 0);
            ExamMinigameLogic.SetMatch(s, st, 1, 1);
            Assert.AreEqual(q.points * 2f / n, ExamMinigameLogic.Grade(q, st), 1e-4f);
            ExamMinigameLogic.SetMatch(s, st, 2, 1);   // ย้ายคำตอบขวา 1 ไปคู่ใหม่ → ซ้าย 1 ว่าง
            Assert.AreEqual(-1, st.match[1]);
            Assert.AreEqual(q.points * 1f / n, ExamMinigameLogic.Grade(q, st), 1e-4f);
            for (int i = 0; i < n; i++) ExamMinigameLogic.SetMatch(s, st, i, i);
            Assert.AreEqual(q.points, ExamMinigameLogic.Grade(q, st), 1e-4f);
        }

        [Test]
        public void Answers_Kept_When_Changing_Question()
        {
            var bank = db.Get("GE102");
            var s = ExamMinigameLogic.CreateSession(db, bank, new System.Random(7));
            var q0 = bank.Find(s.questions[0].questionId);
            if (q0.type == ExamQuestionType.MultipleChoice) ExamMinigameLogic.Select(s, s.questions[0], 0);
            else ExamMinigameLogic.SetMatch(s, s.questions[0], 0, 0);
            s.currentIndex = 1; s.currentIndex = 0;   // เปลี่ยนข้อไปมา (UI อ่าน/เขียน state ใน session เท่านั้น)
            Assert.IsTrue(ExamMinigameLogic.IsAnswered(s.questions[0]));
            Assert.IsFalse(ExamMinigameLogic.IsAnswered(s.questions[1]));
        }

        // ---------- คำใบ้ ----------
        [Test]
        public void HintAllowance_Thresholds()
        {
            Assert.AreEqual(0, ExamMinigameLogic.HintAllowance(0f, db));
            Assert.AreEqual(0, ExamMinigameLogic.HintAllowance(39.9f, db));
            Assert.AreEqual(1, ExamMinigameLogic.HintAllowance(40f, db));
            Assert.AreEqual(1, ExamMinigameLogic.HintAllowance(69.9f, db));
            Assert.AreEqual(2, ExamMinigameLogic.HintAllowance(70f, db));
            Assert.AreEqual(2, ExamMinigameLogic.HintAllowance(100f, db));
        }

        [Test]
        public void Hint_UsedOncePerQuestion_RespectsAllowance_NoPenalty()
        {
            var bank = db.Get("CS202");
            var s = ExamMinigameLogic.CreateSession(db, bank, new System.Random(11));
            s.started = true; s.hintsAllowed = 1;
            var q0 = bank.Find(s.questions[0].questionId);
            Assert.IsTrue(ExamMinigameLogic.UseHint(s, s.questions[0], q0, new System.Random(1), out var r0), r0);
            Assert.AreEqual(1, s.hintsUsed);
            Assert.IsFalse(ExamMinigameLogic.UseHint(s, s.questions[0], q0, new System.Random(1), out _), "ข้อเดิมใช้ซ้ำไม่ได้");
            Assert.AreEqual(1, s.hintsUsed, "กดซ้ำต้องไม่เสียสิทธิ์");
            var q1 = bank.Find(s.questions[1].questionId);
            Assert.IsFalse(ExamMinigameLogic.UseHint(s, s.questions[1], q1, new System.Random(1), out _), "เกินสิทธิ์");
            // คำใบ้ไม่เฉลยทั้งหมด (MC เหลือ ≥ 2 ตัวเลือก) และไม่หักคะแนน
            var mc = FirstOf("CS202", ExamQuestionType.MultipleChoice);
            var s2 = SessionFor(mc, 2); s2.hintsAllowed = 2;
            Assert.IsTrue(ExamMinigameLogic.UseHint(s2, s2.questions[0], mc, new System.Random(3), out _));
            Assert.LessOrEqual(s2.questions[0].eliminated.Count, mc.items.Count - 2);
            Assert.IsFalse(s2.questions[0].eliminated.Contains(mc.correctIndex));
            ExamMinigameLogic.Select(s2, s2.questions[0], mc.correctIndex);
            Assert.AreEqual(mc.points, ExamMinigameLogic.Grade(mc, s2.questions[0]), 1e-4f);
            // หาจุดผิด: ไม่ตัดบรรทัดที่ผิดจริง และเหลือให้เลือก ≥ 2
            var fe = FirstOf("CS102", ExamQuestionType.FindError);
            var s4 = SessionFor(fe, 6); s4.hintsAllowed = 1;
            Assert.IsTrue(ExamMinigameLogic.UseHint(s4, s4.questions[0], fe, new System.Random(2), out _));
            Assert.IsFalse(s4.questions[0].eliminated.Contains(fe.correctIndex));
            Assert.GreaterOrEqual(fe.items.Count - s4.questions[0].eliminated.Count, 2);
            // ไม่มีสิทธิ์ (ความรู้ < 40)
            var s3 = SessionFor(mc, 4); s3.hintsAllowed = 0;
            Assert.IsFalse(ExamMinigameLogic.UseHint(s3, s3.questions[0], mc, null, out var r3));
            StringAssert.Contains("40", r3);
        }

        // ---------- ส่งข้อสอบ ----------
        [Test]
        public void Submit_Idempotent_And_Score100()
        {
            var bank = db.Get("CS207");
            var s = ExamMinigameLogic.CreateSession(db, bank, new System.Random(21));
            s.started = true;
            var st = s.questions[0]; var q = bank.Find(st.questionId);
            switch (q.type)
            {
                case ExamQuestionType.MultipleChoice: ExamMinigameLogic.Select(s, st, q.correctIndex); break;
                case ExamQuestionType.Ordering: for (int i = 0; i < q.items.Count; i++) ExamMinigameLogic.AppendOrder(s, st, i); break;
                case ExamQuestionType.Matching: for (int i = 0; i < q.items.Count; i++) ExamMinigameLogic.SetMatch(s, st, i, i); break;
            }
            ExamMinigameLogic.Submit(s, bank);
            Assert.IsTrue(s.submitted);
            Assert.AreEqual(20, s.score100);   // ถูก 1/5 ข้อ (ข้อละ 1 คะแนน) · ข้อที่ไม่ตอบ = 0
            float before = s.earned;
            ExamMinigameLogic.Select(s, s.questions[1], 0);   // แก้หลังส่งไม่ได้
            ExamMinigameLogic.Submit(s, bank);
            Assert.AreEqual(before, s.earned);
            Assert.AreEqual(20, s.score100);
        }

        // ---------- Save / Load ----------
        [Test]
        public void SaveLoad_Json_RoundTrip_KeepsQuestionsOrderAnswersTimeHints()
        {
            var bank = db.Get("CS102");
            var s = ExamMinigameLogic.CreateSession(db, bank, new System.Random(99));
            s.started = true; s.remainingSeconds = 97.5f; s.hintsAllowed = 2;
            var q0 = bank.Find(s.questions[0].questionId);
            ExamMinigameLogic.UseHint(s, s.questions[0], q0, new System.Random(1), out _);
            if (q0.type == ExamQuestionType.Ordering) ExamMinigameLogic.AppendOrder(s, s.questions[0], s.questions[0].displayOrder[0]);
            else ExamMinigameLogic.Select(s, s.questions[0], q0.correctIndex);

            var d = new SaveData { hasExamSession = true, examSession = s };
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d));
            Assert.IsTrue(back.hasExamSession);
            var b = back.examSession;
            Assert.AreEqual(s.questions.Count, b.questions.Count);
            for (int i = 0; i < s.questions.Count; i++)
            {
                Assert.AreEqual(s.questions[i].questionId, b.questions[i].questionId);
                CollectionAssert.AreEqual(s.questions[i].displayOrder, b.questions[i].displayOrder);
                CollectionAssert.AreEqual(s.questions[i].order, b.questions[i].order);
                CollectionAssert.AreEqual(s.questions[i].match, b.questions[i].match);
                CollectionAssert.AreEqual(s.questions[i].eliminated, b.questions[i].eliminated);
                Assert.AreEqual(s.questions[i].selected, b.questions[i].selected);
                Assert.AreEqual(s.questions[i].hintUsed, b.questions[i].hintUsed);
            }
            Assert.AreEqual(97.5f, b.remainingSeconds);
            Assert.AreEqual(1, b.hintsUsed);
            Assert.AreEqual(2, b.hintsAllowed);
            Assert.IsTrue(b.InProgress);
        }

        [Test]
        public void OldSave_Without_ExamSession_Loads()
        {
            var old = JsonUtility.FromJson<SaveData>("{\"energy\":50,\"currentYear\":1,\"dayInYear\":2}");
            Assert.IsFalse(old.hasExamSession);
            Assert.IsNotNull(old.examSession);
            Assert.IsFalse(old.examSession.started);
        }

        // ---------- เชื่อมระบบเกรดเดิม ----------
        RegistrationService EnrolledY1()
        {
            var svc = new RegistrationService(cur, new AcademicRecord());
            svc.OpenTerm(1, 0);
            foreach (var d in cur.PlanCourses(1, 1)) Assert.IsTrue(svc.Add(d.code, out var r), r);
            Assert.IsTrue(svc.Confirm(out var m), m);
            return svc;
        }

        [Test]
        public void RecordCourseExam_OnlyOnce_SameFormulaAsLegacy()
        {
            var svc = EnrolledY1();
            float s1 = svc.RecordCourseExam("CS102", false, 0.8f, 0f, 2);
            Assert.GreaterOrEqual(s1, 0f);
            var e = svc.CurrentEnrollment("CS102");
            Assert.AreEqual(Mathf.Clamp01(cur.examQuizWeight * 0.8f + (1f - cur.examQuizWeight) * svc.StudyRatio(e, 2)), e.midterm, 1e-4f, "สูตรเดิม");
            Assert.AreEqual(-1f, svc.RecordCourseExam("CS102", false, 1f, 0f, 2), "ห้ามบันทึกซ้ำ");
            Assert.AreEqual(s1, e.midterm, 1e-4f);
            Assert.AreEqual(-1f, svc.RecordCourseExam("CS999", false, 1f, 0f, 2), "วิชาที่ไม่ได้ลง");
            // ขาดสอบ → เฉพาะวิชาที่ยังไม่ได้สอบ
            int missed = svc.RecordMissedExam(false);
            Assert.AreEqual(5, missed);
            Assert.AreEqual(s1, e.midterm, 1e-4f);
            Assert.IsFalse(e.missedMidterm);
            // ปิดภาค → เกรดคำนวณจากสูตรเดิมครั้งเดียว
            svc.RecordCourseExam("CS102", true, 1f, 0f, 3);
            var res = svc.CloseCurrentTerm();
            Assert.IsTrue(e.graded);
            Assert.AreEqual(Mathf.Round(svc.ProjectedScore(e) * 10f) / 10f, e.score, 1e-3f);
            Assert.IsNull(svc.CloseCurrentTerm(), "ปิดภาคซ้ำไม่ให้เกรด/หน่วยกิตซ้ำ");
            Assert.AreEqual(6, res.graded.Count);
        }

        [Test]
        public void LegacySubset_RecordsOnlyGivenCourses()
        {
            var svc = EnrolledY1();
            svc.RecordCourseExam("CS102", true, 1f, 0f, 3);
            float before = svc.CurrentEnrollment("CS102").final;
            svc.RecordExam(true, 0.5f, 0f, 3, new List<string> { "GE101", "CS102" });
            Assert.AreEqual(before, svc.CurrentEnrollment("CS102").final, 1e-4f, "วิชาที่สอบมินิเกมแล้วไม่ถูกทับ");
            Assert.GreaterOrEqual(svc.CurrentEnrollment("GE101").final, 0f);
            Assert.Less(svc.CurrentEnrollment("MA101").final, 0f, "วิชาที่ไม่ได้ระบุไม่ถูกบันทึก");
        }
    }
}
#endif
