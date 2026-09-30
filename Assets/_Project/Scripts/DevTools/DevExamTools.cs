#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.Academics.ExamMinigame;
using NisitSimulator.Systems;

namespace NisitSimulator.DevTools
{
    // ===== เครื่องมือทดสอบมินิเกมสอบ (Dev) =====
    //   • สอบจริง: ผ่าน ExamController.TryTakeExam (ตรวจวันสอบ) → ExamMinigameController.StartExam (ตรวจสิทธิ์ CanStart) — ไม่ข้ามเงื่อนไข
    //   • Preview: สร้างรอบด้วย ExamMinigameLogic.CreateSession จากคลังจริง (กรองชนิด/จำนวน/เวลา/seed) แล้วเปิดใน UI จริง
    //       ด้วย DevStartPreview → ไม่บันทึกคะแนน ไม่ให้หน่วยกิต/รางวัล ไม่เขียนเซฟ
    //   • ตอบอัตโนมัติ: เรียก ExamMinigameLogic.Select / AppendOrder / SetMatch (ฟังก์ชันเดียวกับปุ่มใน UI) แล้วส่งด้วย ctl.Submit(false) (ปุ่มส่ง)
    //       ไม่ตั้งคะแนนปลายทางเอง — คะแนนมาจากตัวตรวจคำตอบจริง
    public static class DevExamTools
    {
        public enum AnswerMode { AllCorrect, AllWrong, Partial }

        public static ExamMinigameController Ctrl => ExamMinigameController.Instance;
        public static ExamController Exam => Object.FindFirstObjectByType<ExamController>();
        public static ExamBankDatabase Db => Ctrl != null ? Ctrl.Db : ExamBankDatabase.LoadDefault();

        public static bool OpenRoomNormal(out string msg)
        {
            var ctl = ExamMinigameController.EnsureExists();
            if (ctl.IsRoomOpen) { msg = "อยู่ในห้องสอบแล้ว"; return true; }
            var exam = Exam; var pl = DevTimeTools.Player;
            if (exam == null || pl == null) { msg = "ไม่พบระบบสอบ/ผู้เล่นในฉาก"; return false; }
            exam.TryTakeExam(pl);   // ขั้นตอนเดียวกับกด E ที่ห้องสอบ (ตรวจวันสอบ/สิทธิ์ตามจริง)
            msg = ctl.IsRoomOpen ? "เข้าห้องสอบตามขั้นตอนปกติ" : "ระบบไม่ให้เข้าห้องสอบ — ดูเหตุผลที่แจ้งบนจอ (เช่น วันนี้ไม่ใช่วันสอบ / ไม่ได้ลงทะเบียน)";
            return ctl.IsRoomOpen;
        }

        public static bool StartCourse(string code, int? seed, out string msg)
        {
            if (!OpenRoomNormal(out msg)) return false;
            var ctl = Ctrl;
            if (seed.HasValue) ExamMinigameController.DevNextSeed = seed;
            bool ok = ctl.StartExam(code, out var reason);
            if (!ok) ExamMinigameController.DevNextSeed = null;
            msg = ok ? $"เริ่มสอบ {code} ผ่าน StartExam (ตรวจสิทธิ์จริง){(seed.HasValue ? " · seed " + seed : "")}" : $"สอบ {code} ไม่ได้: {reason}";
            return ok;
        }

        public static List<string> BankCodes()
        {
            var l = new List<string>();
            var db = Db;
            if (db != null) foreach (var b in db.banks) if (b != null && b.questions != null && b.questions.Count > 0) l.Add(b.courseCode);
            return l;
        }

        public static int CountOfType(string code, ExamQuestionType? type)
        {
            var b = Db != null ? Db.Get(code) : null; if (b == null) return 0;
            int n = 0; foreach (var q in b.questions) if (q != null && (!type.HasValue || q.type == type.Value)) n++;
            return n;
        }

        // ---------- Preview ----------
        public static ExamSessionState BuildPreview(string code, ExamQuestionType? type, int count, float timeSeconds, int seed, float knowledge, out string msg)
        {
            var db = Db;
            var src = db != null ? db.Get(code) : null;
            if (src == null) { msg = $"ไม่มีคลังข้อสอบของ {code}"; return null; }
            var copy = new CourseExamBank { courseCode = src.courseCode, version = src.version, timeLimitSeconds = Mathf.Max(10f, timeSeconds) };
            foreach (var q in src.questions) if (q != null && (!type.HasValue || q.type == type.Value)) copy.questions.Add(q);
            if (copy.questions.Count == 0) { msg = $"{code} ไม่มีข้อชนิด {ExamMinigameLogic.TypeName(type.Value)}"; return null; }
            copy.questionsPerExam = Mathf.Clamp(count, 1, copy.questions.Count);
            var s = ExamMinigameLogic.CreateSession(db, copy, new System.Random(seed));
            var cur = CurriculumDefinition.LoadDefault();
            var def = cur != null ? cur.Get(code) : null;
            s.courseTitle = "[PREVIEW] " + (def != null ? def.title : code);
            s.roundKey = "PREVIEW-" + seed;
            s.knowledgeAtStart = Mathf.Clamp(knowledge, 0f, 100f);
            s.hintsAllowed = ExamMinigameLogic.HintAllowance(s.knowledgeAtStart, db);
            s.playerId = "dev-preview";
            msg = $"Preview {code}: {s.questions.Count} ข้อ · {s.timeLimit:0} วินาที · seed {seed} · ความรู้ {s.knowledgeAtStart:0} → คำใบ้ {s.hintsAllowed} ครั้ง"
                + (count > copy.questions.Count ? $" (ขอ {count} ข้อ แต่คลังมี {copy.questions.Count})" : "");
            return s;
        }

        public static bool StartPreview(ExamSessionState s, out string msg)
        {
            var ctl = ExamMinigameController.EnsureExists();
            if (!ctl.DevStartPreview(s, out var why)) { msg = "เปิด Preview ไม่ได้: " + why; return false; }
            msg = "เปิด Preview ใน UI จริง — ไม่บันทึกคะแนน/หน่วยกิต/รางวัล";
            return true;
        }

        // ---------- ตอบอัตโนมัติ (ผ่านฟังก์ชันเดียวกับปุ่มของผู้เล่น) — คืนคะแนนที่ \"ควรได้\" 0–100 จากเฉลยในคลัง ----------
        public static int AutoAnswer(AnswerMode mode, out string msg)
        {
            var ctl = Ctrl; var s = ctl != null ? ctl.Session : null;
            if (s == null || !s.InProgress) { msg = "ไม่มีการสอบที่กำลังทำ"; return -1; }
            float expect = 0f, max = 0f;
            int half = (s.questions.Count + 1) / 2;
            for (int i = 0; i < s.questions.Count; i++)
            {
                var st = s.questions[i];
                var q = ctl.QuestionAt(i);
                if (q == null) continue;
                max += st.maxPoints;
                bool correct = mode == AnswerMode.AllCorrect || (mode == AnswerMode.Partial && i < half);
                bool blank = mode == AnswerMode.Partial && i >= half;
                ClearAnswer(s, st, q);
                if (blank) continue;
                if (correct) { AnswerCorrect(s, st, q); expect += st.maxPoints; }
                else AnswerWrong(s, st, q);
            }
            msg = mode == AnswerMode.AllCorrect ? "ตอบถูกทุกข้อ" : mode == AnswerMode.AllWrong ? "ตอบผิดทุกข้อ" : $"ตอบถูก {half} ข้อแรก เว้นว่างที่เหลือ";
            return max > 0f ? Mathf.RoundToInt(100f * expect / max) : 0;
        }

        static void ClearAnswer(ExamSessionState s, ExamQuestionState st, ExamQuestion q)
        {
            switch (q.type)
            {
                case ExamQuestionType.Ordering: ExamMinigameLogic.ClearOrder(s, st); break;
                case ExamQuestionType.Matching: for (int i = 0; i < st.match.Count; i++) ExamMinigameLogic.SetMatch(s, st, i, -1); break;
                default: st.selected = -1; break;   // ผู้เล่นเปลี่ยนคำตอบได้เสมอ — ล้างก่อนเลือกใหม่
            }
        }

        static void AnswerCorrect(ExamSessionState s, ExamQuestionState st, ExamQuestion q)
        {
            int n = q.items != null ? q.items.Count : 0;
            switch (q.type)
            {
                case ExamQuestionType.MultipleChoice:
                case ExamQuestionType.FindError: ExamMinigameLogic.Select(s, st, q.correctIndex); break;
                case ExamQuestionType.Ordering: for (int i = 0; i < n; i++) ExamMinigameLogic.AppendOrder(s, st, i); break;
                case ExamQuestionType.Matching: for (int i = 0; i < n; i++) ExamMinigameLogic.SetMatch(s, st, i, i); break;
            }
        }

        static void AnswerWrong(ExamSessionState s, ExamQuestionState st, ExamQuestion q)
        {
            int n = q.items != null ? q.items.Count : 0;
            switch (q.type)
            {
                case ExamQuestionType.MultipleChoice:
                case ExamQuestionType.FindError:
                    for (int i = 0; i < n; i++) if (i != q.correctIndex && !st.eliminated.Contains(i)) { ExamMinigameLogic.Select(s, st, i); break; }
                    break;
                case ExamQuestionType.Ordering:
                    // หมุนลำดับจนไม่มีบล็อกไหนตรงตำแหน่งในทุกลำดับที่ถูก (รวมลำดับทางเลือก)
                    for (int k = 1; k < Mathf.Max(2, n); k++)
                    {
                        ExamMinigameLogic.ClearOrder(s, st);
                        for (int i = 0; i < n; i++) ExamMinigameLogic.AppendOrder(s, st, (i + k) % n);
                        if (ExamMinigameLogic.Grade(q, st) <= 0f) break;
                    }
                    break;
                case ExamQuestionType.Matching:
                    for (int i = 0; i < n; i++) ExamMinigameLogic.SetMatch(s, st, i, (i + 1) % n);
                    break;
            }
        }

        // ส่งข้อสอบผ่าน UI จริง (ปุ่ม \"ส่งข้อสอบ\" → กล่องยืนยัน → ยืนยัน) · ถ้า UI ไม่แสดงอยู่ ใช้เมธอดเดียวกับที่ปุ่มยืนยันเรียก
        public static void Submit()
        {
            var c = Ctrl; if (c == null || c.Session == null) return;
            var ui = Object.FindFirstObjectByType<ExamMinigameUI>();
            bool was = c.Session.submitted;
            if (!was && ui != null && ui.IsVisible) { ui.AskSubmit(); ui.PressConfirm(); }
            if (was || (c.Session != null && !c.Session.submitted)) c.Submit(false);   // ส่งซ้ำหลังส่งแล้ว = เรียกเมธอดส่งอีกครั้ง (ต้องไม่มีผล)
        }

        public static bool SimulateTimeout(out string msg)
        {
            var c = Ctrl;
            if (c == null || !c.HasSessionInProgress) { msg = "ไม่มีการสอบที่กำลังทำ"; return false; }
            c.Session.remainingSeconds = 0.01f;   // ให้ Update ของตัวคุมสอบตัดสินหมดเวลาเอง (เส้นทางส่งอัตโนมัติจริง)
            msg = "ตั้งเวลาคงเหลือ 0.01 วินาที → ตัวจับเวลาจริงจะส่งอัตโนมัติในเฟรมถัดไป";
            return true;
        }

        public static string SessionSummary()
        {
            var c = Ctrl; var s = c != null ? c.Session : null;
            if (s == null) return "ไม่มีรอบสอบ";
            return $"{(c.IsPreview ? "[PREVIEW] " : "")}{s.courseCode} · {(s.isFinal ? "ปลายภาค" : "กลางภาค")} · {s.questions.Count} ข้อ · ตอบแล้ว {ExamMinigameLogic.AnsweredCount(s)}\n" +
                   $"เวลาเหลือ {s.remainingSeconds:0.0}/{s.timeLimit:0} วิ · คำใบ้ {s.hintsUsed}/{s.hintsAllowed} (ความรู้ตอนเริ่ม {s.knowledgeAtStart:0})\n" +
                   $"สถานะ: {(s.submitted ? (s.autoSubmitted ? "ส่งแล้ว (หมดเวลา)" : s.quit ? "ส่งแล้ว (เลิกสอบ)" : "ส่งแล้ว") : s.started ? "กำลังทำ" : "ยังไม่เริ่ม")} · บันทึกผล={s.recorded} · คะแนนมินิเกม {s.score100}/100 ({s.earned:0.##}/{s.maxPoints:0.##})" +
                   $" · คะแนนสอบที่บันทึกลงวิชา {(s.recordedExamScore >= 0 ? (s.recordedExamScore * 100).ToString("0.0") : "-")}";
        }

        public static string Fingerprint(ExamSessionState s)
        {
            if (s == null) return "null";
            var sb = new System.Text.StringBuilder(s.courseCode + "|" + s.roundKey + "|" + s.hintsUsed + "/" + s.hintsAllowed + "|");
            foreach (var q in s.questions)
                sb.Append(q.questionId).Append('[').Append(string.Join(",", q.displayOrder)).Append("]s").Append(q.selected)
                  .Append("o").Append(string.Join(",", q.order)).Append("m").Append(string.Join(",", q.match)).Append(q.hintUsed ? "H" : "").Append(';');
            return sb.ToString();
        }
    }
}
#endif
