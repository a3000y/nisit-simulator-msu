#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.Academics.ExamMinigame;
using NisitSimulator.Interaction;
using NisitSimulator.SaveLoad;
using NisitSimulator.Stats;
using NisitSimulator.Systems;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;

namespace NisitSimulator.DevTools
{
    public enum CheckStatus { Pass, Fail, Skip }

    public class CheckResult
    {
        public string id, name;
        public CheckStatus status;
        public string expected = "", actual = "", error = "";
        public string StatusText => status == CheckStatus.Pass ? "ผ่าน" : status == CheckStatus.Fail ? "ไม่ผ่าน" : "ข้าม";
    }

    // ===== การตรวจอัตโนมัติ \"ในเกม\" (ใช้ฉาก/ระบบจริงทั้งหมด) =====
    //   ทุกข้อตรวจผลลัพธ์ที่เกี่ยวข้อง (คะแนน/จำนวนครั้งที่บันทึก/ไฟล์เซฟ/สถานะ) — ไม่ถือว่าผ่านเพียงเพราะไม่มี exception
    //   ต้องอยู่ในโปรไฟล์ทดสอบ (ข้อที่เปลี่ยนสถานะ) · ข้อที่ต้องการสถานการณ์เฉพาะจะ \"ข้าม\" พร้อมเหตุผล
    public static class DevRuntimeChecks
    {
        public static readonly List<CheckResult> Results = new List<CheckResult>();
        public static bool Running { get; private set; }
        public static string Progress = "";
        public static readonly List<int> SeedsUsed = new List<int>();

        static CheckResult Add(string id, string name, CheckStatus st, string expected, string actual, string error = "")
        {
            Results.RemoveAll(r => r.id == id);
            var r = new CheckResult { id = id, name = name, status = st, expected = expected, actual = actual, error = error };
            Results.Add(r);
            Results.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return r;
        }

        static CheckResult Verdict(string id, string name, bool ok, string expected, string actual) =>
            Add(id, name, ok ? CheckStatus.Pass : CheckStatus.Fail, expected, actual);

        static ExamMinigameController Ctrl => ExamMinigameController.Instance;
        static RegistrationService Svc => CourseRegistrar.Instance != null ? CourseRegistrar.Instance.Service : null;

        public static IEnumerator RunAll(bool includeReload)
        {
            if (Running) yield break;
            Running = true;
            try
            {
                if (!DevProfile.Active)
                {
                    Add("R00", "โปรไฟล์ทดสอบ", CheckStatus.Skip, "อยู่ใน DEV TEST PROFILE", "ยังไม่ได้เข้าโปรไฟล์ทดสอบ — ทุกข้อที่เปลี่ยนสถานะถูกข้าม");
                    yield break;
                }
                Progress = "R01 เซฟจริง"; yield return Guarded("R01", "โปรไฟล์ Dev ไม่เขียนทับเซฟจริง", CheckRealSaveIsolation());
                Progress = "R02 เข้าเรียน"; yield return Guarded("R02", "เข้าเรียนปกติไม่เปิดมินิเกม", CheckAttendanceNoMinigame());
                Progress = "R03 Preview"; yield return Guarded("R03", "Preview ไม่เปลี่ยนคะแนน/หน่วยกิต/รางวัล/เซฟ", CheckPreviewNoSideEffects());
                Progress = "R04 ตรวจคำตอบ"; yield return Guarded("R04", "ตอบผิดทั้งหมด = 0 · ตอบบางข้อ = คะแนนบางส่วน (ตัวตรวจจริง)", CheckGradingModes());
                Progress = "R05 เร่งเวลา"; yield return Guarded("R05", "เร่งเวลาโลก x10 ไม่กระทบตัวจับเวลาสอบ", CheckTimerUnaffectedBySpeed());
                if (includeReload)
                {
                    Progress = "R06 Save/Load ระหว่างสอบ"; yield return Guarded("R06", "Save/Load ระหว่างสอบคงชุดข้อ คำตอบ และเวลา", CheckSaveLoadMidExam());
                }
                else Add("R06", "Save/Load ระหว่างสอบคงชุดข้อ คำตอบ และเวลา", CheckStatus.Skip, "", "ไม่ได้เลือกชุดที่โหลดฉากใหม่");
                Progress = "R07 หมดเวลา"; yield return Guarded("R07", "หมดเวลาสอบ ส่งคำตอบ/บันทึกผลเพียงครั้งเดียว", CheckTimeoutOnce());
                Progress = "R08 ส่งซ้ำ"; yield return Guarded("R08", "กดส่งซ้ำไม่บันทึกคะแนน/ไม่ให้รางวัลซ้ำ", CheckDoubleSubmit());
                Progress = "R09 จำลองเวลา"; yield return Guarded("R09", "จำลองเวลาข้ามวัน: เหตุการณ์วันใหม่ครั้งเดียว ค่าขนมครั้งเดียว", CheckSimulatedDay());
            }
            finally { Running = false; Progress = ""; }
        }

        // ห่อ coroutine: exception = ไม่ผ่าน พร้อมข้อความ
        static IEnumerator Guarded(string id, string name, IEnumerator body)
        {
            while (true)
            {
                object cur;
                try { if (!body.MoveNext()) break; cur = body.Current; }
                catch (Exception e) { Add(id, name, CheckStatus.Fail, "ทำงานได้โดยไม่เกิดข้อผิดพลาด", "เกิด exception", e.GetType().Name + ": " + e.Message); yield break; }
                yield return cur;
            }
        }

        // ---------- R01 ----------
        static IEnumerator CheckRealSaveIsolation()
        {
            const string id = "R01", name = "โปรไฟล์ Dev ไม่เขียนทับเซฟจริง";
            string realPath = SaveSystem.RealSlotPath(DevProfile.RealSlotAtEntry);
            string before = DevProfile.Fingerprint(realPath);
            DateTime devBefore = File.Exists(DevProfile.ProfilePath) ? File.GetLastWriteTimeUtc(DevProfile.ProfilePath) : DateTime.MinValue;
            int blocked0 = SaveSystem.DevBlockedRealWrites;
            yield return new WaitForSecondsRealtime(0.05f);
            SaveManager.Save();                                   // เส้นทาง autosave จริง
            // จำลองกรณีเลวร้าย: เส้นทางเซฟทดสอบหายไประหว่างทดสอบ → ต้องถูกกัน ไม่ตกไปเขียนช่องจริง
            string keep = SaveSystem.DevPathOverride;
            SaveSystem.DevPathOverride = null;
            try { SaveSystem.Save(new SaveData()); SaveSystem.DeleteSave(); }
            finally { SaveSystem.DevPathOverride = keep; }
            string after = DevProfile.Fingerprint(realPath);
            DateTime devAfter = File.Exists(DevProfile.ProfilePath) ? File.GetLastWriteTimeUtc(DevProfile.ProfilePath) : DateTime.MinValue;
            bool untouched = before == after && DevProfile.RealSaveUntouched(out var d);
            bool devWritten = devAfter > devBefore;
            int blocked = SaveSystem.DevBlockedRealWrites - blocked0;
            Verdict(id, name, untouched && devWritten && blocked == 2,
                "เซฟจริงไม่เปลี่ยนตั้งแต่เข้าโหมดทดสอบ · autosave เขียนลงเซฟทดสอบ · การเขียน/ลบช่องจริงถูกกัน 2 ครั้ง",
                $"เซฟจริงไม่เปลี่ยน={untouched} · เซฟทดสอบถูกเขียน={devWritten} · ถูกกัน {blocked} ครั้ง");
        }

        // ---------- R02 ----------
        static IEnumerator CheckAttendanceNoMinigame()
        {
            const string id = "R02", name = "เข้าเรียนปกติไม่เปิดมินิเกม";
            var ctl = ExamMinigameController.EnsureExists();
            if (ctl.HasSessionInProgress) { Add(id, name, CheckStatus.Skip, "", "มีรอบสอบค้างอยู่"); yield break; }
            if (!CourseRegistrar.Active) { Add(id, name, CheckStatus.Skip, "", "ระบบลงทะเบียนไม่ได้ใช้กับคณะนี้"); yield break; }
            var spot = UnityEngine.Object.FindFirstObjectByType<ActivitySpot>();
            if (spot == null) { Add(id, name, CheckStatus.Skip, "", "ไม่พบจุดเรียน (ActivitySpot) ในฉาก"); yield break; }
            var s0 = ctl.Session; bool room0 = ctl.IsRoomOpen;
            string note = CourseRegistrar.NotifyStudyTick(spot.transform, 10f, 10f);   // เส้นทางเดียวกับนั่งเรียนครบ 60 นาทีเกม
            yield return null; yield return null;
            var ui = UnityEngine.Object.FindFirstObjectByType<ExamMinigameUI>(FindObjectsInactive.Include);
            bool uiVisible = ui != null && ui.IsVisible;
            bool ok = ctl.Session == s0 && !ctl.IsRoomOpen && !room0 && !ExamMinigameController.BlocksWorld && !uiVisible;
            Verdict(id, name, ok, "หลังนับชั่วโมงเรียน: ไม่มีรอบสอบใหม่ · ห้องสอบไม่เปิด · UI มินิเกมไม่แสดง · ไม่ล็อกผู้เล่น",
                $"รอบสอบเดิม={(ctl.Session == s0)} · ห้องสอบเปิด={ctl.IsRoomOpen} · UI แสดง={uiVisible} · ผลการเรียน: {(string.IsNullOrEmpty(note) ? "(นอกคาบ/ไม่นับ)" : note.Trim())}");
        }

        // ---------- R03 ----------
        static string RecordJson() => Svc != null ? JsonUtility.ToJson(Svc.Record) : "";

        static IEnumerator CheckPreviewNoSideEffects()
        {
            const string id = "R03", name = "Preview ไม่เปลี่ยนคะแนน/หน่วยกิต/รางวัล/เซฟ";
            var ctl = ExamMinigameController.EnsureExists();
            if (ctl.HasSessionInProgress) { Add(id, name, CheckStatus.Skip, "", "มีรอบสอบค้างอยู่"); yield break; }
            var codes = DevExamTools.BankCodes();
            if (codes.Count == 0) { Add(id, name, CheckStatus.Skip, "", "ไม่มีคลังข้อสอบ"); yield break; }
            var stats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            string rec0 = RecordJson();
            string st0 = stats != null ? $"{stats.Money}|{stats.Exp}|{stats.Knowledge:0.###}|{stats.Satisfaction:0.###}" : "";
            int rc0 = ExamMinigameController.DevRecordCount, rw0 = ExamMinigameController.DevRewardCount;
            string file0 = DevProfile.Fingerprint(DevProfile.ProfilePath);
            SeedsUsed.Add(24680);
            var s = DevExamTools.BuildPreview(codes[0], null, 3, 60f, 24680, 80f, out var m);
            if (s == null || !DevExamTools.StartPreview(s, out m)) { Add(id, name, CheckStatus.Fail, "เปิด Preview ได้", m); yield break; }
            yield return null;
            int expect = DevExamTools.AutoAnswer(DevExamTools.AnswerMode.AllCorrect, out _);
            DevExamTools.Submit();
            yield return null;
            var ss = ctl.Session;
            string rec1 = RecordJson();
            string st1 = stats != null ? $"{stats.Money}|{stats.Exp}|{stats.Knowledge:0.###}|{stats.Satisfaction:0.###}" : "";
            bool ok = ss != null && ss.submitted && ss.score100 == expect && expect == 100 && ss.recordedExamScore < 0f
                      && rec0 == rec1 && st0 == st1 && rc0 == ExamMinigameController.DevRecordCount && rw0 == ExamMinigameController.DevRewardCount
                      && file0 == DevProfile.Fingerprint(DevProfile.ProfilePath);
            Verdict(id, name, ok,
                "Preview ตอบถูกทั้งหมด = 100/100 แต่ประวัติรายวิชา/สถานะผู้เล่น/จำนวนครั้งบันทึก/ไฟล์เซฟไม่เปลี่ยน",
                $"คะแนน {(ss != null ? ss.score100 : -1)}/100 (คาด {expect}) · บันทึกลงวิชา={(ss != null && ss.recordedExamScore >= 0)} · ประวัติเปลี่ยน={rec0 != rec1} · สถานะเปลี่ยน={st0 != st1} · บันทึกผล +{ExamMinigameController.DevRecordCount - rc0} · รางวัล +{ExamMinigameController.DevRewardCount - rw0} · เซฟเปลี่ยน={file0 != DevProfile.Fingerprint(DevProfile.ProfilePath)}");
            ctl.LeaveExamRoom();
        }

        // ---------- R04 ----------
        static IEnumerator CheckGradingModes()
        {
            const string id = "R04", name = "ตอบผิดทั้งหมด = 0 · ตอบบางข้อ = คะแนนบางส่วน (ตัวตรวจจริง)";
            var ctl = ExamMinigameController.EnsureExists();
            if (ctl.HasSessionInProgress) { Add(id, name, CheckStatus.Skip, "", "มีรอบสอบค้างอยู่"); yield break; }
            var codes = DevExamTools.BankCodes();
            if (codes.Count == 0) { Add(id, name, CheckStatus.Skip, "", "ไม่มีคลังข้อสอบ"); yield break; }
            var parts = new List<string>(); bool ok = true;
            foreach (var mode in new[] { DevExamTools.AnswerMode.AllWrong, DevExamTools.AnswerMode.Partial })
                foreach (ExamQuestionType type in Enum.GetValues(typeof(ExamQuestionType)))
                {
                    string code = null;
                    foreach (var c in codes) if (DevExamTools.CountOfType(c, type) >= 2) { code = c; break; }
                    if (code == null) { parts.Add($"{ExamMinigameLogic.TypeName(type)}: ไม่มีคลัง"); continue; }
                    int seed = 1000 + (int)type;
                    var s = DevExamTools.BuildPreview(code, type, 4, 60f, seed, 0f, out _);
                    if (s == null || !DevExamTools.StartPreview(s, out _)) { ok = false; parts.Add($"{type}: เปิดไม่ได้"); continue; }
                    yield return null;
                    int expect = DevExamTools.AutoAnswer(mode, out _);
                    DevExamTools.Submit();
                    yield return null;
                    int got = ctl.Session != null ? ctl.Session.score100 : -1;
                    bool pass = got == expect && (mode == DevExamTools.AnswerMode.AllWrong ? got == 0 : got > 0 && got < 100);
                    ok &= pass;
                    parts.Add($"{(mode == DevExamTools.AnswerMode.AllWrong ? "ผิดหมด" : "บางข้อ")}/{ExamMinigameLogic.TypeName(type)} {code}: {got} (คาด {expect}){(pass ? "" : " ✗")}");
                    ctl.LeaveExamRoom();
                    yield return null;
                }
            Verdict(id, name, ok, "ผิดหมด = 0 ทุกชนิดข้อสอบ · ตอบครึ่งแรกถูก/ที่เหลือว่าง = ตรงกับคะแนนเต็มของข้อที่ถูก (0 < x < 100)", string.Join(" · ", parts));
        }

        // ---------- R05 ----------
        static IEnumerator CheckTimerUnaffectedBySpeed()
        {
            const string id = "R05", name = "เร่งเวลาโลก x10 ไม่กระทบตัวจับเวลาสอบ";
            var ctl = ExamMinigameController.EnsureExists();
            var clock = DevTimeTools.Clock;
            if (ctl.HasSessionInProgress || clock == null) { Add(id, name, CheckStatus.Skip, "", "มีรอบสอบค้าง/ไม่พบนาฬิกา"); yield break; }
            var codes = DevExamTools.BankCodes();
            if (codes.Count == 0) { Add(id, name, CheckStatus.Skip, "", "ไม่มีคลังข้อสอบ"); yield break; }
            int prevMult = DevTimeTools.SpeedMult;
            var s = DevExamTools.BuildPreview(codes[0], null, 2, 120f, 1357, 0f, out _);
            if (s == null || !DevExamTools.StartPreview(s, out var m)) { Add(id, name, CheckStatus.Fail, "เปิด Preview ได้", "เปิดไม่ได้"); yield break; }
            DevTimeTools.SetSpeed(10, out _);
            yield return null;
            float r0 = ctl.Session.remainingSeconds, c0 = clock.TotalMinutes, t0 = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(1.5f);
            float dt = Time.realtimeSinceStartup - t0;
            float dr = r0 - ctl.Session.remainingSeconds;
            float dc = clock.TotalMinutes - c0;
            DevTimeTools.SetSpeed(prevMult, out _);
            bool ok = Mathf.Abs(dr - dt) <= 0.25f && Mathf.Abs(dc) < 0.01f && Mathf.Approximately(Time.timeScale, 1f);
            Verdict(id, name, ok, $"เวลาสอบลดเท่าเวลาจริง (~{dt:0.00} วิ ±0.25) · นาฬิกาโลกหยุดระหว่างสอบ · Time.timeScale = 1",
                $"เวลาสอบลด {dr:0.00} วิ ใน {dt:0.00} วิจริง · นาฬิกาโลกเดิน {dc:0.00} นาที · timeScale {Time.timeScale}");
            DevExamTools.Submit();
            yield return null;
            ctl.LeaveExamRoom();
        }

        // หาวิชาที่ \"พร้อมสอบ\" (ตามสิทธิ์จริง) — เปิดห้องสอบผ่านขั้นตอนปกติก่อน
        static string ReadyCourse(out string why)
        {
            why = null;
            if (!DevExamTools.OpenRoomNormal(out why)) return null;
            foreach (var e in Ctrl.BuildEntries()) if (e.status == ExamMinigameController.EntryStatus.Ready) return e.code;
            why = "ไม่มีวิชาที่พร้อมสอบด้วยมินิเกม (ต้องอยู่วันสอบ + ลงทะเบียนวิชาที่มีคลังข้อสอบ เช่น สถานการณ์ S5)";
            Ctrl.LeaveExamRoom();
            return null;
        }

        static Enrollment Enr(string code) => Svc != null ? Svc.CurrentEnrollment(code) : null;

        // ---------- R06 ----------
        static IEnumerator CheckSaveLoadMidExam()
        {
            const string id = "R06", name = "Save/Load ระหว่างสอบคงชุดข้อ คำตอบ และเวลา";
            var ctl = ExamMinigameController.EnsureExists();
            if (ctl.HasSessionInProgress) { Add(id, name, CheckStatus.Skip, "", "มีรอบสอบค้างอยู่"); yield break; }
            string code = ReadyCourse(out var why);
            if (code == null) { Add(id, name, CheckStatus.Skip, "มีวิชาที่พร้อมสอบ", why); yield break; }
            SeedsUsed.Add(4242);
            if (!DevExamTools.StartCourse(code, 4242, out var m)) { Add(id, name, CheckStatus.Fail, "เริ่มสอบได้", m); yield break; }
            yield return null;
            DevExamTools.AutoAnswer(DevExamTools.AnswerMode.Partial, out _);
            string fp0 = DevExamTools.Fingerprint(ctl.Session);
            DevProfile.SaveDev(out _);
            float saved = ctl.Session.remainingSeconds;
            if (!DevProfile.LoadDev(out m)) { Add(id, name, CheckStatus.Fail, "โหลดเซฟทดสอบได้", m); yield break; }
            // รอฉากใหม่ + ตัวคุมสอบคืนรอบค้าง
            float t0 = Time.realtimeSinceStartup;
            yield return null;
            while (Time.realtimeSinceStartup - t0 < 8f)
            {
                var c = ExamMinigameController.Instance;
                if (c != null && c != ctl && c.HasSessionInProgress && c.IsRoomOpen) break;
                yield return null;
            }
            var c2 = ExamMinigameController.Instance;
            var s2 = c2 != null ? c2.Session : null;
            string fp1 = DevExamTools.Fingerprint(s2);
            float after = s2 != null ? s2.remainingSeconds : -1f;
            bool ok = c2 != null && c2 != ctl && s2 != null && s2.InProgress && fp0 == fp1 && after <= saved + 0.01f && after >= saved - 2.5f;
            Verdict(id, name, ok, $"หลังโหลด: วิชา {code} ยังสอบค้าง · ชุดข้อ/ลำดับ/คำตอบ/คำใบ้เหมือนเดิม · เวลาเหลือ ≈ {saved:0.0} วิ (ไม่เพิ่ม)",
                $"ค้างอยู่={(s2 != null && s2.InProgress)} · ข้อมูลรอบเหมือนเดิม={fp0 == fp1} · เวลา {saved:0.0} → {after:0.0} วิ");
        }

        // ---------- R07 ----------
        static IEnumerator CheckTimeoutOnce()
        {
            const string id = "R07", name = "หมดเวลาสอบ ส่งคำตอบ/บันทึกผลเพียงครั้งเดียว";
            var ctl = ExamMinigameController.EnsureExists();
            string code;
            if (ctl.HasSessionInProgress && !ctl.IsPreview) code = ctl.Session.courseCode;   // ใช้รอบค้างจาก R06 ได้
            else
            {
                code = ReadyCourse(out var why);
                if (code == null) { Add(id, name, CheckStatus.Skip, "มีวิชาที่พร้อมสอบ", why); yield break; }
                SeedsUsed.Add(777);
                if (!DevExamTools.StartCourse(code, 777, out var m)) { Add(id, name, CheckStatus.Fail, "เริ่มสอบได้", m); yield break; }
            }
            yield return null;
            int rc0 = ExamMinigameController.DevRecordCount, rw0 = ExamMinigameController.DevRewardCount;
            var s = ctl.Session;
            DevExamTools.SimulateTimeout(out _);
            float t0 = Time.realtimeSinceStartup;
            while (!s.submitted && Time.realtimeSinceStartup - t0 < 3f) yield return null;
            float firstScore = Enr(code) != null ? Enr(code).midterm : -2f;
            yield return new WaitForSecondsRealtime(0.3f);
            ctl.Submit(true); ctl.ForceSubmitIfActive(); ctl.Submit(false);   // หมดเวลาซ้ำ/ข้ามวัน/กดส่งหลังหมดเวลา
            yield return null;
            var e = Enr(code);
            float sc = e != null ? (s.isFinal ? e.final : e.midterm) : -2f;
            bool ok = s.submitted && s.autoSubmitted && ExamMinigameController.DevRecordCount - rc0 == 1 && ExamMinigameController.DevRewardCount - rw0 == 1
                      && sc >= 0f && Mathf.Approximately(sc, s.recordedExamScore) && Mathf.Approximately(sc, s.isFinal ? sc : firstScore);
            Verdict(id, name, ok, "ส่งอัตโนมัติ 1 ครั้ง · บันทึกคะแนนลงวิชา 1 ครั้ง · ให้รางวัล 1 ครั้ง · คะแนนไม่เปลี่ยนเมื่อสั่งส่งซ้ำ",
                $"ส่งแล้ว={s.submitted} อัตโนมัติ={s.autoSubmitted} · บันทึก +{ExamMinigameController.DevRecordCount - rc0} · รางวัล +{ExamMinigameController.DevRewardCount - rw0} · คะแนนวิชา {sc * 100:0.0} (ที่บันทึก {s.recordedExamScore * 100:0.0}) · คะแนนมินิเกม {s.score100}");
            ctl.LeaveExamRoom();
        }

        // ---------- R08 ----------
        static IEnumerator CheckDoubleSubmit()
        {
            const string id = "R08", name = "กดส่งซ้ำไม่บันทึกคะแนน/ไม่ให้รางวัลซ้ำ";
            var ctl = ExamMinigameController.EnsureExists();
            if (ctl.HasSessionInProgress) { Add(id, name, CheckStatus.Skip, "", "มีรอบสอบค้างอยู่"); yield break; }
            string code = ReadyCourse(out var why);
            if (code == null) { Add(id, name, CheckStatus.Skip, "มีวิชาที่พร้อมสอบอีกวิชา", why); yield break; }
            SeedsUsed.Add(999);
            if (!DevExamTools.StartCourse(code, 999, out var m)) { Add(id, name, CheckStatus.Fail, "เริ่มสอบได้", m); yield break; }
            yield return null;
            var stats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            int expect = DevExamTools.AutoAnswer(DevExamTools.AnswerMode.AllCorrect, out _);
            int rc0 = ExamMinigameController.DevRecordCount, rw0 = ExamMinigameController.DevRewardCount;
            var s = ctl.Session;
            DevExamTools.Submit();
            yield return null;
            var e = Enr(code);
            float sc1 = e != null ? (s.isFinal ? e.final : e.midterm) : -2f;
            int exp1 = stats != null ? stats.Exp : 0; int money1 = stats != null ? stats.Money : 0;
            DevExamTools.Submit(); DevExamTools.Submit();
            yield return null;
            float sc2 = e != null ? (s.isFinal ? e.final : e.midterm) : -2f;
            int exp2 = stats != null ? stats.Exp : 0; int money2 = stats != null ? stats.Money : 0;
            bool ok = s.submitted && !s.autoSubmitted && s.score100 == expect && expect == 100
                      && ExamMinigameController.DevRecordCount - rc0 == 1 && ExamMinigameController.DevRewardCount - rw0 == 1
                      && sc1 >= 0f && Mathf.Approximately(sc1, sc2) && exp1 == exp2 && money1 == money2;
            Verdict(id, name, ok, "ตอบถูกทั้งหมด (ผ่านปุ่มคำตอบ) = 100 · กดส่ง 3 ครั้ง บันทึก 1 ครั้ง รางวัล 1 ครั้ง · คะแนนวิชา/EXP/เงินไม่เปลี่ยนหลังครั้งแรก",
                $"คะแนน {s.score100}/100 · บันทึก +{ExamMinigameController.DevRecordCount - rc0} · รางวัล +{ExamMinigameController.DevRewardCount - rw0} · คะแนนวิชา {sc1 * 100:0.0} → {sc2 * 100:0.0} · EXP {exp1} → {exp2} · เงิน {money1} → {money2}");
            ctl.LeaveExamRoom();
        }

        // ---------- R09 ----------
        static IEnumerator CheckSimulatedDay()
        {
            const string id = "R09", name = "จำลองเวลาข้ามวัน: เหตุการณ์วันใหม่ครั้งเดียว ค่าขนมครั้งเดียว";
            var clock = DevTimeTools.Clock; var prog = DevTimeTools.Prog;
            var stats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            var allowance = UnityEngine.Object.FindFirstObjectByType<DailyAllowance>();
            var ctl = ExamMinigameController.Instance;
            if (clock == null || prog == null || stats == null) { Add(id, name, CheckStatus.Skip, "", "ไม่พบนาฬิกา/ความคืบหน้า/สถานะผู้เล่น"); yield break; }
            if (ctl != null && ctl.HasSessionInProgress) { Add(id, name, CheckStatus.Skip, "", "มีรอบสอบค้างอยู่"); yield break; }
            var gm = NisitSimulator.Core.GameManager.Instance;
            if (Time.timeScale == 0f || (gm != null && !gm.IsActive)) { Add(id, name, CheckStatus.Skip, "", "มีหน้าต่างหยุดเวลาเปิดอยู่"); yield break; }
            // เตรียมสภาพแวดล้อม: เติมพลังงาน/อิ่ม/สุขภาพ ก่อนเดินเวลา (กันตัวละครหมดแรง/ตายกลางทางตามกฎจริง ซึ่งจะทำให้ตรวจเรื่องข้ามวันไม่ได้)
            stats.LoadState(stats.maxEnergy, stats.maxHealth, stats.maxHunger, stats.Knowledge, stats.Satisfaction, stats.Money, stats.Exp, stats.Stress);
            int days = 0; Action<int> h = d => days++;
            clock.OnDayChanged += h;
            int day0 = clock.Day, diy0 = prog.DayInYear, cal0 = prog.CalendarYear, money0 = stats.Money;
            float target = DevTimeTools.MinutesUntil(30, false);   // ถึง 00:30 ของวันถัดไป
            if (target < 60f) target += 24f * 60f;
            DevTimeTools.StartSimulation(DevPanel.Instance, target, "ตรวจ R09: ถึง 00:30 วันถัดไป", null, out _);
            float t0 = Time.realtimeSinceStartup;
            while (DevTimeTools.Simulating && Time.realtimeSinceStartup - t0 < 20f)
            {
                if (DaySummaryUI.IsShowing)   // ปิดหน้าสรุปวันเหมือนผู้เล่นกด Space
                {
                    var ui = UnityEngine.Object.FindFirstObjectByType<DaySummaryUI>();
                    var mi = typeof(DaySummaryUI).GetMethod("Close", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (ui != null && mi != null) mi.Invoke(ui, null);
                }
                yield return null;
            }
            clock.OnDayChanged -= h;
            int perDay = allowance != null ? allowance.perDay : 0;
            int moneyDelta = stats.Money - money0;
            bool yearRolled = prog.CalendarYear != cal0;
            bool dayOk = yearRolled ? prog.DayInYear == 1 : prog.DayInYear == diy0 + 1;
            bool reached = clock.Hour == 0 && clock.Minute >= 29 && clock.Minute <= 31;
            bool alive = gm == null || gm.IsActive || Time.timeScale == 0f;
            bool ok = !DevTimeTools.Simulating && reached && alive && days == 1 && clock.Day == day0 + 1 && dayOk && (allowance == null || moneyDelta == perDay);
            Verdict(id, name, ok, $"(เติมสถานะก่อนเริ่ม) ถึง 00:30 วันถัดไปโดยเกมยังดำเนินอยู่ · OnDayChanged 1 ครั้ง · วันเกม +1 · วันในปี +1 · เงิน +{perDay} (ค่าขนม 1 ครั้ง)",
                $"ถึงเวลาเป้าหมาย={reached} · เกมยังดำเนินอยู่={alive} ({(gm != null ? gm.State.ToString() : "-")}) · OnDayChanged {days} ครั้ง · วันเกม {day0} → {clock.Day} · วันในปี {diy0} → {prog.DayInYear} · เงิน {(moneyDelta >= 0 ? "+" : "")}{moneyDelta} · เวลา {clock.GetTimeString()}"
                + (moneyDelta != perDay ? " (เงินต่างจากค่าขนม: อาจมีรายรับ/รายจ่ายอื่นในวันเดียวกัน เช่น เหตุการณ์สุ่ม — ตรวจ log)" : ""));
        }

        // ---------- รายงาน ----------
        public static string Report()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Nisit Simulator — Dev Test Report ===");
            sb.AppendLine("เวลา: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("Unity: " + Application.unityVersion + " · Build: " + (Application.isEditor ? "Editor" : Debug.isDebugBuild ? "Development Build" : "Release") + " · แพลตฟอร์ม: " + Application.platform);
            sb.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            sb.AppendLine("โปรไฟล์: " + (DevProfile.Active ? DevProfile.ProfileName : "เกมปกติ (ไม่อยู่ในโปรไฟล์ทดสอบ)"));
            sb.AppendLine("สถานการณ์: " + (string.IsNullOrEmpty(DevProfile.LastScenario) ? "-" : DevProfile.LastScenario));
            sb.AppendLine("Random seed: " + (SeedsUsed.Count > 0 ? string.Join(", ", SeedsUsed) : "-") + (DevPanel.LastPreviewSeed.HasValue ? " · Preview ล่าสุด " + DevPanel.LastPreviewSeed : ""));
            var reg = CourseRegistrar.Instance; var prog = DevTimeTools.Prog;
            if (reg != null && reg.Service != null)
            {
                var t = reg.Record.Current;
                sb.AppendLine($"การเรียน: ชั้นปี {reg.ClassYear} · {(t == null ? "-" : t.isBreak ? "ปิดภาคฤดูร้อน" : t.isExtra ? "ภาคเพิ่มเติม" : "ภาค " + t.planSemester)} · หน่วยกิต {reg.Service.EarnedCredits()} · GPA {(reg.Service.HasGpa ? reg.Service.Gpa().ToString("0.00") : "-")}");
            }
            if (prog != null) sb.AppendLine($"ปฏิทิน: ปีการศึกษา {prog.CalendarYear} วัน {prog.DayInYear}/{prog.daysPerYear}");
            int p = 0, f = 0, s = 0;
            foreach (var r in Results) { if (r.status == CheckStatus.Pass) p++; else if (r.status == CheckStatus.Fail) f++; else s++; }
            sb.AppendLine($"ผลตรวจในเกม: ผ่าน {p} · ไม่ผ่าน {f} · ข้าม {s}");
            foreach (var r in Results)
            {
                sb.AppendLine($"[{r.StatusText}] {r.id} {r.name}");
                if (!string.IsNullOrEmpty(r.expected)) sb.AppendLine("    คาดหวัง: " + r.expected);
                if (!string.IsNullOrEmpty(r.actual)) sb.AppendLine("    ผลจริง: " + r.actual);
                if (!string.IsNullOrEmpty(r.error)) sb.AppendLine("    ข้อผิดพลาด: " + r.error);
            }
            if (DevPanel.LastScenarioResult != null)
            {
                var sr = DevPanel.LastScenarioResult;
                sb.AppendLine($"สถานการณ์ล่าสุด [{(sr.skipped ? "ข้าม" : "สร้างได้")}] {sr.title}");
                if (sr.skipped) sb.AppendLine("    เหตุผล: " + sr.skipReason);
                else sb.AppendLine("    " + sr.conditionText.Replace("\n", "\n    "));
            }
            sb.AppendLine("(รายงานนี้ไม่รวม path ไฟล์ ชื่อผู้ใช้เครื่อง IP หรือรหัสห้อง)");
            return sb.ToString();
        }
    }
}
#endif
