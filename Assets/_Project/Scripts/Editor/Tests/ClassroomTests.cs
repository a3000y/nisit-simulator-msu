#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NisitSimulator.Academics;
using NisitSimulator.SaveLoad;
using NisitSimulator.TimeSystem;

namespace NisitSimulator.Tests
{
    // EditMode tests: ห้องเรียน (A) · เร่งเวลา (B) · เหตุการณ์ระหว่างเรียน (C) — ตรรกะล้วน ไม่ต้องเปิดฉาก
    public class ClassroomTests
    {
        CurriculumDefinition cur;
        ClassroomCatalog cat;

        [SetUp]
        public void SetUp()
        {
            cur = CsCurriculumDefaults.Create();
            cat = ClassroomDefaults.Create();
            GameClock.WarpMultiplier = 1f;
            GameSession.IsMultiplayerGame = false; GameClock.NetworkFollower = false; GameClock.NetworkAuthoritative = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cur); Object.DestroyImmediate(cat);
            GameClock.WarpMultiplier = 1f;
            GameSession.IsMultiplayerGame = false; GameClock.NetworkFollower = false; GameClock.NetworkAuthoritative = false;
        }

        // ---------- helpers ----------
        static CourseDefinition Course(string code, int sem, params ClassSession[] ss) =>
            new CourseDefinition { code = code, title = code, credits = 3, category = CourseCategory.Core, planYear = 1, planSemester = sem, sessions = new List<ClassSession>(ss) };

        static ClassSession S(int day, int sh, int eh, string room) => new ClassSession(day, sh, eh, "คณะ IT", room, room);

        CurriculumDefinition Mini(params CourseDefinition[] cs)
        {
            var c = ScriptableObject.CreateInstance<CurriculumDefinition>();
            c.courses = new List<CourseDefinition>(cs);
            c.gradeScale = new List<GradeStep> { new GradeStep("A", 85f, 4f), new GradeStep("F", 0f, 0f) };
            return c;
        }

        List<RoomIssue> Conflicts(CurriculumDefinition c) =>
            RegistrationService.FindRoomConflicts(c, cat, false).FindAll(i => i.kind == RoomIssue.Kind.Conflict);

        // ======================= A: ห้องเรียน =======================
        [Test]
        public void Room_SameRoomOverlappingTime_Conflicts()
        {
            var c = Mini(Course("X1", 1, S(1, 9, 11, "IT-201")), Course("X2", 1, S(1, 10, 12, "IT-201")));
            var r = Conflicts(c);
            Assert.AreEqual(1, r.Count);
            StringAssert.Contains("IT-201", r[0].ToString());
            StringAssert.Contains("X1", r[0].ToString()); StringAssert.Contains("X2", r[0].ToString());
            Object.DestroyImmediate(c);
        }

        [Test]
        public void Room_BackToBackSameRoom_NoConflict()
        {
            var c = Mini(Course("X1", 1, S(1, 9, 11, "IT-201")), Course("X2", 1, S(1, 11, 13, "IT-201")));
            CollectionAssert.IsEmpty(Conflicts(c));
            Object.DestroyImmediate(c);
        }

        [Test]
        public void Room_DifferentRoomSameTime_NoConflict()
        {
            var c = Mini(Course("X1", 1, S(1, 9, 11, "IT-201")), Course("X2", 1, S(1, 9, 11, "IT-202")));
            CollectionAssert.IsEmpty(Conflicts(c));
            Object.DestroyImmediate(c);
        }

        [Test]
        public void Room_DifferentDay_NoConflict()
        {
            var c = Mini(Course("X1", 1, S(1, 9, 11, "IT-201")), Course("X2", 1, S(2, 9, 11, "IT-201")));
            CollectionAssert.IsEmpty(Conflicts(c));
            Object.DestroyImmediate(c);
        }

        [Test]
        public void Room_DifferentSemester_NoConflict_ButElectiveCountsBoth()
        {
            var c = Mini(Course("X1", 1, S(1, 9, 11, "IT-201")), Course("X2", 2, S(1, 9, 11, "IT-201")));
            CollectionAssert.IsEmpty(Conflicts(c), "ภาค 1 กับภาค 2 ไม่เปิดพร้อมกัน");
            var el = Course("EL", 0, S(1, 9, 11, "IT-201")); el.category = CourseCategory.Elective;
            var c2 = Mini(Course("X1", 1, S(1, 9, 11, "IT-201")), el);
            Assert.AreEqual(1, Conflicts(c2).Count, "วิชาเลือกเปิดทั้งสองภาค → ชน");
            Object.DestroyImmediate(c); Object.DestroyImmediate(c2);
        }

        [Test]
        public void Room_UnknownRoomId_IsError_AndMissingRoomWhenRequired()
        {
            var c = Mini(Course("X1", 1, S(1, 9, 11, "IT-999")), Course("X2", 1, new ClassSession(1, 13, 15, "คณะ IT", "")));
            var all = RegistrationService.FindRoomConflicts(c, cat, true);
            Assert.IsTrue(all.Exists(i => i.kind == RoomIssue.Kind.UnknownRoom && i.roomId == "IT-999"));
            Assert.IsTrue(all.Exists(i => i.kind == RoomIssue.Kind.MissingRoom && i.codeA == "X2"));
            Object.DestroyImmediate(c);
        }

        [Test]
        public void Room_DefaultCurriculum_EverySessionHasExistingRoom_NoConflicts()
        {
            var issues = RegistrationService.FindRoomConflicts(cur, cat, true);
            CollectionAssert.IsEmpty(issues, string.Join("\n", issues.ConvertAll(i => i.ToString())));
            // ใช้ห้องร่วมกันจริง (หลายวิชาต่อห้อง คนละเวลา)
            var perRoom = new Dictionary<string, HashSet<string>>();
            foreach (var d in cur.courses)
                foreach (var s in d.sessions) { if (!perRoom.ContainsKey(s.roomId)) perRoom[s.roomId] = new HashSet<string>(); perRoom[s.roomId].Add(d.code); }
            Assert.GreaterOrEqual(perRoom["IT-201"].Count, 4);
            Assert.GreaterOrEqual(perRoom["GE-101"].Count, 8);
            // ประเภทห้องตามประเภทวิชา
            Assert.AreEqual(ClassroomType.ComputerLab, cat.Get(cur.Get("CS102").sessions[0].roomId).type);
            Assert.AreEqual(ClassroomType.Lecture, cat.Get(cur.Get("GE101").sessions[0].roomId).type);
            Assert.AreEqual(ClassroomType.Office, cat.Get(cur.Get("CS401").sessions[0].roomId).type);
            Assert.AreEqual(ClassroomType.Seminar, cat.Get(cur.Get("CS404").sessions[0].roomId).type);
            // ภาคค่ำมีห้องและผ่านกฎเดียวกัน
            foreach (var d in cur.courses) foreach (var s in d.retakeSessions) Assert.IsTrue(cat.Exists(s.roomId), d.code + " ภาคค่ำ");
            CollectionAssert.IsEmpty(cur.Validate());
        }

        [Test]
        public void Room_RuntimeConflict_LogsError_AndBlocksRegistration_NoCrash()
        {
            var a = Course("X1", 1, S(1, 9, 11, "IT-201")); a.retakeSessions = new List<ClassSession> { S(1, 18, 20, "IT-202") };
            var b = Course("X2", 1, S(1, 10, 12, "IT-201")); b.retakeSessions = new List<ClassSession> { S(1, 20, 22, "IT-202") };
            var ok = Course("X3", 1, S(2, 9, 11, "IT-203")); ok.retakeSessions = new List<ClassSession> { S(2, 18, 20, "IT-203") };
            var c = Mini(a, b, ok);
            c.creditCapPerTerm = 18;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ข้อมูลห้องเรียนผิด"));
            var svc = new RegistrationService(c, new AcademicRecord(), cat);
            svc.OpenTerm(1, 0);
            Assert.IsFalse(svc.Add("X1", out var r1)); StringAssert.Contains("ห้องเรียน", r1);
            Assert.IsFalse(svc.Add("X2", out _));
            Assert.IsTrue(svc.Add("X3", out var r3), r3);
            Object.DestroyImmediate(c);
        }

        [Test]
        public void Room_PlayerSchedule_TimeConflict_StillBlocked()
        {
            // กฎเดิม: สองวิชาที่เวลาชนกันลงพร้อมกันไม่ได้ (แม้คนละห้อง)
            var a = Course("X1", 1, S(1, 9, 11, "IT-201"));
            var b = Course("X2", 1, S(1, 10, 12, "IT-202"));
            var c = Mini(a, b); c.creditCapPerTerm = 18;
            var svc = new RegistrationService(c, new AcademicRecord(), cat);
            svc.OpenTerm(1, 0);
            Assert.IsTrue(svc.Add("X1", out _));
            Assert.IsFalse(svc.Add("X2", out var why)); StringAssert.Contains("ชนเวลา", why);
            Object.DestroyImmediate(c);
        }

        RegistrationService Enrolled(out Enrollment e, out ClassSession s, string code = "CS102")
        {
            var svc = new RegistrationService(cur, new AcademicRecord(), cat);
            svc.OpenTerm(1, 0);
            foreach (var d in cur.PlanCourses(1, 1)) Assert.IsTrue(svc.Add(d.code, out var r), r);
            Assert.IsTrue(svc.Confirm(out var cr), cr);
            e = svc.CurrentEnrollment(code);
            s = svc.SessionsFor(e)[0];
            return svc;
        }

        [Test]
        public void Attendance_CorrectRoomCounts_WrongRoomAndOutsideDoNot()
        {
            var svc = Enrolled(out var e, out var s);
            Assert.AreEqual("IT-201", s.roomId);
            var wrong = svc.RecordStudyTick(s.day, s.startMinute + 60, "คณะ IT", "IT-202", 1f, true);
            Assert.IsNull(wrong.attended); Assert.AreEqual("CS102", wrong.wrongRoomCode); Assert.AreEqual("IT-201", wrong.wrongRoomTarget);
            Assert.IsNull(wrong.selfStudy, "ห้องผิดระหว่างคาบไม่นับเป็นอ่านทบทวน");
            var outside = svc.RecordStudyTick(s.day, s.startMinute + 60, "คณะ IT", null, 1f, true);
            Assert.IsNull(outside.attended); Assert.AreEqual("CS102", outside.wrongRoomCode);
            var ok = svc.RecordStudyTick(s.day, s.startMinute + 60, "คณะ IT", "IT-201", 1f, true);
            Assert.AreEqual("CS102", ok.attended.code); Assert.AreEqual(0, ok.sessionIndex);
            // multiplayer (roomMode=false) = เช็กตึกแบบเดิม ห้องไม่มีผล
            var mp = svc.RecordStudyTick(s.day, s.startMinute + 120, "คณะ IT", "IT-999", 1f, false);
            Assert.AreEqual("CS102", mp.attended.code);
        }

        [Test]
        public void OldSave_WithoutRoomFields_LoadsAndGetsRoomsFromCurriculum()
        {
            // JSON ของเซฟก่อนมีระบบห้อง (ไม่มี roomId/classBonus/classEventKeys)
            string json = "{\"version\":1,\"classYear\":1,\"regularTermsStarted\":1,\"hasCurrent\":true," +
                          "\"current\":{\"serial\":1,\"calendarYear\":1,\"semIndex\":0,\"classYear\":1,\"planSemester\":1,\"confirmed\":true,\"selected\":[\"CS102\"]}," +
                          "\"enrollments\":[{\"code\":\"CS102\",\"attempt\":1,\"termSerial\":1,\"calendarYear\":1,\"semIndex\":0,\"classYear\":1,\"retakeSection\":false,\"progress\":1.0,\"midterm\":-1,\"final\":-1}]}";
            var rec = JsonUtility.FromJson<AcademicRecord>(json);
            var svc = new RegistrationService(cur, rec, cat);
            var e = svc.CurrentEnrollment("CS102");
            Assert.IsNotNull(e);
            Assert.AreEqual("IT-201", svc.SessionsFor(e)[0].roomId);
            Assert.AreEqual(0f, e.classBonusMid); Assert.AreEqual(0f, e.classBonusFinal);
            Assert.IsNotNull(e.classEventKeys);
            // เซฟไม่เก็บ roomId ซ้ำ
            StringAssert.DoesNotContain("IT-201", JsonUtility.ToJson(rec));
        }

        // ======================= B: เร่งเวลา =======================
        static ClassWarpInput Ok(bool inClass = false) => new ClassWarpInput
        {
            singlePlayer = true, registrarActive = true, inClass = inClass, seated = true, studySpot = true,
            hasSession = true, sessionRoomId = "IT-201", spotRoomId = "IT-201", playerInSessionRoom = true,
        };

        [Test]
        public void Warp_EndTime_OnTimeAndLate()
        {
            var s = new ClassSession(1, 9, 11, "คณะ IT", "", "IT-201");
            Assert.AreEqual(660f, ClassWarpRules.WarpEndMinute(s));
            Assert.AreEqual(120f, ClassWarpRules.RemainingMinutes(s, 540f));
            Assert.AreEqual(50f, ClassWarpRules.RemainingMinutes(s, 610f), "มาสาย = เร่งแค่ช่วงที่เหลือ");
            Assert.AreEqual(2, ClassWarpRules.ExpectedTicks(s, 540f), "ตรงเวลา ครบ 2 ชม.");
            Assert.AreEqual(2, ClassWarpRules.ExpectedTicks(s, 530f), "นั่งก่อนเริ่ม 10 นาที ครบ 2 ชม.");
            Assert.AreEqual(1, ClassWarpRules.ExpectedTicks(s, 550f), "สาย 10 นาที → ชั่วโมงที่ไม่ครบไม่นับ");
            Assert.AreEqual(0, ClassWarpRules.ExpectedTicks(s, 610f));
            // ~3–4 วินาทีจริงต่อคาบ 2 ชม. ที่ค่าเริ่มต้น 35 นาทีเกม/วินาที
            float sec = ClassWarpRules.RealSecondsFor(120f, 35f);
            Assert.That(sec, Is.InRange(3f, 4f));
        }

        [Test]
        public void Warp_Multiplier_ClampsToEnd_AndNeverBelowOne()
        {
            Assert.AreEqual(35f / 3f, ClassWarpRules.Multiplier(3f, 35f, 120f, 0.016f), 1e-3f);
            Assert.AreEqual(1f, ClassWarpRules.Multiplier(3f, 35f, 0.01f, 0.5f), 1e-4f, "เหลือนิดเดียว → ไม่เร่งเกินเวลาเลิก");
            Assert.AreEqual(1f, ClassWarpRules.Multiplier(0f, 35f, 120f, 0.016f));
        }

        [Test]
        public void Warp_Decide_WarpsOnlyInCorrectRoomDuringClass_SinglePlayer()
        {
            Assert.AreEqual(ClassWarpDecision.Warp, ClassWarpRules.Decide(Ok()));
            var i = Ok(); i.spotRoomId = "IT-202"; Assert.AreEqual(ClassWarpDecision.Idle, ClassWarpRules.Decide(i), "ห้องผิด ไม่เร่ง");
            i = Ok(); i.spotRoomId = null; Assert.AreEqual(ClassWarpDecision.Idle, ClassWarpRules.Decide(i), "นอกห้องเรียน ไม่เร่ง");
            i = Ok(); i.hasSession = false; Assert.AreEqual(ClassWarpDecision.Idle, ClassWarpRules.Decide(i), "นอกคาบ (อ่านทบทวน) ไม่เร่ง");
            i = Ok(); i.singlePlayer = false; Assert.AreEqual(ClassWarpDecision.Idle, ClassWarpRules.Decide(i), "multiplayer ไม่เร่ง");
            i = Ok(); i.studySpot = false; Assert.AreEqual(ClassWarpDecision.Idle, ClassWarpRules.Decide(i));
        }

        [Test]
        public void Warp_EveryStopCase_EndsOrSuspends_AndResetsMultiplier()
        {
            void Check(string name, ClassWarpInput inp, ClassWarpDecision expect)
            {
                var d = ClassWarpRules.Decide(inp);
                Assert.AreEqual(expect, d, name);
                Assert.AreEqual(1f, ClassWarpRules.MultiplierFor(d, 11f), name + ": ตัวคูณต้องกลับเป็น 1");
            }
            var i = Ok(true); i.exhausted = true; Check("หมดแรง", i, ClassWarpDecision.End);
            i = Ok(true); i.paused = true; Check("pause", i, ClassWarpDecision.Suspend);
            i = Ok(true); i.windowOpen = true; Check("เปิดหน้าต่างอื่น", i, ClassWarpDecision.Suspend);
            i = Ok(true); i.dayChanged = true; Check("ข้ามวัน", i, ClassWarpDecision.End);
            i = Ok(true); i.examSuspended = true; Check("เข้าห้องสอบ", i, ClassWarpDecision.End);
            i = Ok(true); i.playerInSessionRoom = false; Check("ออกจากห้อง", i, ClassWarpDecision.End);
            i = Ok(true); i.gameEnded = true; Check("จบเกม", i, ClassWarpDecision.End);
            i = Ok(true); i.seated = false; Check("ลุกเอง", i, ClassWarpDecision.End);
            i = Ok(true); i.hasSession = false; Check("เลิกคาบ", i, ClassWarpDecision.Finish);
            i = Ok(true); i.singlePlayer = false; Check("เปลี่ยนเป็น multiplayer", i, ClassWarpDecision.End);
            Assert.AreEqual(11f, ClassWarpRules.MultiplierFor(ClassWarpDecision.Warp, 11f));
        }

        [Test]
        public void Warp_ClockUsesMultiplier_AndMultiplayerFlagDisablesRooms()
        {
            var go = new GameObject("clock");
            var c = go.AddComponent<GameClock>();
            c.gameMinutesPerRealSecond = 3f;
            GameClock.WarpMultiplier = 5f;
            Assert.AreEqual(15f, c.EffectiveMinutesPerSecond, 1e-4f);
            GameClock.WarpMultiplier = 1f;
            Assert.AreEqual(3f, c.EffectiveMinutesPerSecond, 1e-4f);
            Object.DestroyImmediate(go);
            Assert.IsTrue(ClassroomRules.IsSinglePlayer);
            GameSession.IsMultiplayerGame = true; Assert.IsFalse(ClassroomRules.IsSinglePlayer);
            GameSession.IsMultiplayerGame = false; GameClock.NetworkFollower = true; Assert.IsFalse(ClassroomRules.IsSinglePlayer);
            GameClock.NetworkFollower = false; GameClock.NetworkAuthoritative = true; Assert.IsFalse(ClassroomRules.IsSinglePlayer);
        }

        // ======================= C: เหตุการณ์ระหว่างเรียน =======================
        [Test]
        public void Event_QualityModifiers_AffectNextTickOnly()
        {
            var t = new ClassEventLogic.Tuning();
            var called = ClassEventLogic.CalledOn(true, t);
            Assert.AreEqual(0.8f, ClassEventLogic.ApplyQuality(0.5f, called.qualityMul, called.qualityAdd), 1e-4f);
            Assert.Greater(called.knowledge, 0f);
            var wrong = ClassEventLogic.CalledOn(false, t);
            Assert.AreEqual(5f, wrong.stress); Assert.AreEqual(0f, wrong.qualityAdd);
            var nap = ClassEventLogic.Drowsy(true, t);
            Assert.AreEqual(0.4f, ClassEventLogic.ApplyQuality(0.8f, nap.qualityMul, nap.qualityAdd), 1e-4f);
            Assert.AreEqual(4f, nap.energy);
            Assert.AreEqual(-6f, ClassEventLogic.Drowsy(false, t).energy);
            var chat = ClassEventLogic.FriendNote(true, t);
            Assert.AreEqual(0.6f, ClassEventLogic.ApplyQuality(0.8f, chat.qualityMul, chat.qualityAdd), 1e-4f);
            Assert.AreEqual(5, chat.relation);
            Assert.AreEqual(1f, ClassEventLogic.ApplyQuality(0.9f, 1f, 0.3f), "q ไม่เกิน 1");

            // ผ่าน RecordStudyTick (q ของชั่วโมงถัดไปเท่านั้น)
            var svc = Enrolled(out var e, out var s);
            bool pending = true;
            svc.QualityModifier = (code, q) => { if (!pending || code != "CS102") return q; pending = false; return ClassEventLogic.ApplyQuality(q, 1f, 0.3f); };
            var r1 = svc.RecordStudyTick(s.day, s.startMinute + 60, "คณะ IT", "IT-201", 0.5f, true);
            Assert.AreEqual(0.8f, r1.quality, 1e-4f);
            var r2 = svc.RecordStudyTick(s.day, s.startMinute + 120, "คณะ IT", "IT-201", 0.5f, true);
            Assert.AreEqual(0.5f, r2.quality, 1e-4f);
            Assert.AreEqual(1.3f, e.progress, 1e-4f);
        }

        [Test]
        public void Event_Bonus_GoesToCorrectExam_WithCap()
        {
            var t = new ClassEventLogic.Tuning();
            var svc = Enrolled(out var e, out var s);
            var quiz = ClassEventLogic.PopQuiz(2, 3, RegistrationService.NextExamIsFinal(e), t);
            Assert.IsFalse(quiz.bonusFinal, "ยังไม่สอบกลางภาค → โบนัสเข้ากลางภาค");
            Assert.AreEqual(0.04f, quiz.bonus, 1e-4f);
            RegistrationService.AddClassBonus(e, quiz.bonusFinal, quiz.bonus);
            var hint = ClassEventLogic.ExamHint(true, t);
            Assert.IsTrue(hint.bonusFinal);
            RegistrationService.AddClassBonus(e, true, hint.bonus);

            float baseMid = svc.ExamScore(e, 0.5f, 0f, 2, false) - 0.04f;
            float mid = svc.RecordCourseExam("CS102", false, 0.5f, 0f, 2);
            Assert.AreEqual(baseMid + 0.04f, mid, 1e-4f, "กลางภาคได้โบนัสจากควิซ");
            Assert.IsTrue(RegistrationService.NextExamIsFinal(e));
            float withHint = svc.ExamScore(e, 0.5f, 0f, 3, true);
            float noHint = svc.ExamScore(e, 0.5f, 0f, 3, true) - RegistrationService.ClassBonus(e, true);
            Assert.AreEqual(0.03f, withHint - noHint, 1e-4f);
            // เพดาน
            RegistrationService.AddClassBonus(e, true, 1f);
            Assert.AreEqual(RegistrationService.ClassBonusCap, e.classBonusFinal, 1e-4f);
            // ไม่มีโบนัส = สูตรเดิมทุกประการ
            var other = svc.CurrentEnrollment("GE101");
            Assert.AreEqual(Mathf.Clamp01(cur.examQuizWeight * 0.7f + (1f - cur.examQuizWeight) * svc.StudyRatio(other, 2)), svc.ExamScore(other, 0.7f, 0f, 2), 1e-5f);
        }

        [Test]
        public void Event_AtMostOnePerMeeting_AndWithinWindow()
        {
            var t = new ClassEventLogic.Tuning { chance = 1f };
            var e = new Enrollment { code = "CS102" };
            var s = new ClassSession(1, 9, 11, "คณะ IT", "", "IT-201");
            string key = RegistrationService.MeetingKey(1, 0);
            float m = ClassEventLogic.PlanOrRoll(e, key, s, 540f, new System.Random(1), t);
            Assert.That(m, Is.InRange(570f, 630f), "ช่วง 25–75% ของคาบ");
            Assert.AreEqual(m, ClassEventLogic.PlanOrRoll(e, key, s, 540f, new System.Random(99), t), "ลุกแล้วนั่งใหม่ไม่สุ่มใหม่");
            Assert.IsFalse(ClassEventLogic.ShouldFire(e, key, m - 1f));
            Assert.IsTrue(ClassEventLogic.ShouldFire(e, key, m));
            ClassEventLogic.MarkDone(e, key);
            Assert.IsFalse(ClassEventLogic.ShouldFire(e, key, 650f));
            Assert.AreEqual(-1f, ClassEventLogic.PlanOrRoll(e, key, s, 600f, new System.Random(2), t));
            Assert.AreEqual(1, ClassEventLogic.FiredCount(e, key));
            Assert.AreEqual(1, e.classEventKeys.Count);

            // โอกาส 0 = ไม่เกิด · มาสายเลยช่วง = ไม่เกิด
            var e2 = new Enrollment { code = "X" };
            Assert.AreEqual(-1f, ClassEventLogic.PlanOrRoll(e2, key, s, 540f, new System.Random(3), new ClassEventLogic.Tuning { chance = 0f }));
            var e3 = new Enrollment { code = "Y" };
            Assert.AreEqual(-1f, ClassEventLogic.PlanOrRoll(e3, key, s, 640f, new System.Random(3), t));
            // ความถี่ราว 40%
            int hit = 0; var rng = new System.Random(7);
            for (int k = 0; k < 2000; k++) { var ek = new Enrollment(); if (ClassEventLogic.PlanOrRoll(ek, key, s, 540f, rng, new ClassEventLogic.Tuning()) >= 0f) hit++; }
            Assert.That(hit / 2000f, Is.InRange(0.36f, 0.44f));
        }

        [Test]
        public void Event_BonusAndPlan_SaveLoadRoundTrip()
        {
            var rec = new AcademicRecord();
            var e = new Enrollment { code = "CS102", classBonusMid = 0.04f, classBonusFinal = 0.03f };
            ClassEventLogic.SetState(e, "1:0", ClassEventLogic.Done);
            ClassEventLogic.SetState(e, "2:1", "600");
            rec.enrollments.Add(e);
            var d = new SaveData { hasAcademicRecord = true, academic = rec };
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d));
            var b = back.academic.enrollments[0];
            Assert.AreEqual(0.04f, b.classBonusMid, 1e-5f);
            Assert.AreEqual(0.03f, b.classBonusFinal, 1e-5f);
            Assert.AreEqual(ClassEventLogic.Done, ClassEventLogic.GetState(b, "1:0"));
            Assert.IsTrue(ClassEventLogic.ShouldFire(b, "2:1", 600f));
        }

        [Test]
        public void Event_Outage_CreditsRemainingHoursFull()
        {
            var svc = Enrolled(out var e, out var s);
            svc.RecordStudyTick(s.day, s.startMinute + 60, "คณะ IT", "IT-201", 0.5f, true);
            var o = ClassEventLogic.Outage(ClassroomType.ComputerLab);
            Assert.IsTrue(o.endClass); StringAssert.Contains("คอมค้าง", o.summary);
            StringAssert.Contains("ไฟดับ", ClassEventLogic.Outage(ClassroomType.Lecture).summary);
            int added = svc.CreditRemainingMeeting(e, s.day, 0, 1f);
            Assert.AreEqual(1, added);
            Assert.AreEqual(s.MaxTicks, e.TicksFor(RegistrationService.MeetingKey(s.day, 0)));
            Assert.AreEqual(1.5f, e.progress, 1e-4f);
            Assert.AreEqual(0, svc.CreditRemainingMeeting(e, s.day, 0, 1f), "ครบแล้วไม่เพิ่ม");
        }

        [Test]
        public void Event_Weights_DependOnEnergyRoomAndContent()
        {
            float wNormal = ClassEventLogic.Weight(ClassEventKind.Drowsy, 80f, ClassroomType.Lecture, true, true, true);
            float wTired = ClassEventLogic.Weight(ClassEventKind.Drowsy, 10f, ClassroomType.Lecture, true, true, true);
            Assert.Greater(wTired, wNormal * 2f, "พลังงานต่ำ → ง่วงบ่อยขึ้น");
            Assert.AreEqual(0f, ClassEventLogic.Weight(ClassEventKind.CalledOn, 80f, ClassroomType.Lecture, false, false, true), "ไม่มีคลังข้อสอบ = ไม่มีเรียกตอบ");
            Assert.AreEqual(0f, ClassEventLogic.Weight(ClassEventKind.FriendNote, 80f, ClassroomType.Lecture, true, true, false));
            Assert.Greater(ClassEventLogic.Weight(ClassEventKind.Outage, 80f, ClassroomType.ComputerLab, true, true, true),
                           ClassEventLogic.Weight(ClassEventKind.Outage, 80f, ClassroomType.Lecture, true, true, true), "แล็บคอมค้างบ่อยกว่า");
            var rng = new System.Random(5);
            for (int k = 0; k < 500; k++)
            {
                var kind = ClassEventLogic.Pick(rng, 50f, ClassroomType.Lecture, false, false, false);
                Assert.AreNotEqual(ClassEventKind.CalledOn, kind); Assert.AreNotEqual(ClassEventKind.PopQuiz, kind); Assert.AreNotEqual(ClassEventKind.FriendNote, kind);
            }
        }
    }
}
#endif
