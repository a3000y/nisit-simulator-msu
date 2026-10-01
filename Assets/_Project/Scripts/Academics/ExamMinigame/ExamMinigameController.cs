using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Core;
using NisitSimulator.Player;
using NisitSimulator.SaveLoad;
using NisitSimulator.Stats;
using NisitSimulator.Systems;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;

namespace NisitSimulator.Academics.ExamMinigame
{
    // ===== ตัวคุมมินิเกมสอบรายวิชา (เปิดเฉพาะตอนสอบ — การเข้าเรียนปกติไม่เกี่ยว) =====
    //   ไปห้องสอบ (ExamStation กด E) → ExamController.TryTakeExam → OpenExamRoom (เลือกวิชา)
    //   → ตรวจสิทธิ์ → รายละเอียด → ยืนยันเริ่ม (เริ่มจับเวลา) → ทำข้อสอบ → ส่ง → ดูผล → บันทึกคะแนนผ่าน CourseRegistrar (สูตรเกรดเดิม)
    //   • จับเวลาด้วย unscaledDeltaTime (ไม่ใช้ timeScale = 0) · หยุดนาฬิกาเกม/การลดสถานะ "ของผู้เล่นนี้" ระหว่างอยู่ในห้องสอบ
    //   • ล็อกการเดิน + การโต้ตอบในโลก เฉพาะผู้เล่นเครื่องนี้ (Multiplayer: สถานะการเรียนเป็นของแต่ละเครื่องตามระบบเดิม)
    //   • บันทึกลง SaveData.examSession · ถูกสร้างอัตโนมัติ (EnsureExists) ไม่ต้องแก้ฉาก
    public class ExamMinigameController : MonoBehaviour
    {
        public static ExamMinigameController Instance { get; private set; }

        public static ExamMinigameController EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("ExamMinigame");
            return go.AddComponent<ExamMinigameController>();
        }

        // ผู้เล่นเครื่องนี้อยู่ในห้องสอบ (ล็อกเดิน/โต้ตอบ + หยุดนาฬิกาเกม) — PlayerInteraction/GameClock/StatDecay อ่านค่านี้
        public static bool BlocksWorld => Instance != null && Instance.locked;

        public enum EntryStatus { Ready, InProgress, Submitted, NoContent, LegacyAssessment, NotEligible }

        public class CourseEntry
        {
            public string code, title;
            public EntryStatus status;
            public string reason;
            public float examScore = -1f;   // คะแนนสอบรอบนี้ที่บันทึกแล้ว (0..1)
            public int questions;
            public float timeLimit;
        }

        public ExamBankDatabase Db { get; private set; }
        public ExamSessionState Session { get; private set; }
        public bool IsRoomOpen => locked;
        public bool HasSessionInProgress => Session != null && Session.InProgress;
        public bool RoundFinal => roundFinal;
        public int RoundSemester => roundSem;
        public string LastRewardText { get; private set; } = "";

        ExamMinigameUI ui;
        bool locked;
        bool roundFinal;
        int roundSem;
        GameObject player;
        PlayerMovement move;
        PlayerActionController action;
        bool heldByUs;
        System.Random rng = new System.Random();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ===== Dev Testing Panel (Editor / Development Build เท่านั้น) =====
        //   IsPreview      = รอบดูมินิเกม: ใช้ UI/ตรรกะจริง แต่ไม่บันทึกคะแนน ไม่ให้หน่วยกิต/รางวัล ไม่เขียนเซฟ
        //   DevRecordCount = จำนวนครั้งที่ "บันทึกคะแนนลงรายวิชาได้จริง" (ไว้ตรวจว่าส่งซ้ำ/หมดเวลาไม่บันทึกซ้ำ)
        public bool IsPreview { get; private set; }
        public static int DevRecordCount;
        public static int DevRewardCount;
        public static int? DevNextSeed;   // seed ของการสุ่มรอบสอบถัดไป (รอบจริง) — ใช้ครั้งเดียว

        public bool DevStartPreview(ExamSessionState s, out string reason)
        {
            reason = null;
            if (s == null || s.questions == null || s.questions.Count == 0) { reason = "ไม่มีข้อสอบในชุด Preview"; return false; }
            if (HasSessionInProgress) { reason = $"มีการสอบ {Session.courseCode} ค้างอยู่ — ส่งหรือเลิกสอบก่อน"; return false; }
            if (Session != null && Session.submitted && !locked) Session = null;
            IsPreview = true;
            s.started = true;
            Session = s;
            roundFinal = s.isFinal; roundSem = s.semIndex;
            if (!locked) Lock(GameObject.Find("Player"));
            EnsureUI().ShowExam();
            return true;
        }
#endif

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            GameClock.Suspended = false;
            Db = ExamBankDatabase.LoadDefault();
        }

        void Start()
        {
            ui = ExamMinigameUI.EnsureExists(this);
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; GameClock.Suspended = false; }
        }

        CourseRegistrar Reg => CourseRegistrar.Instance;
        RegistrationService Svc => Reg != null ? Reg.Service : null;
        ExamController Exam => Object.FindFirstObjectByType<ExamController>();
        ProgressionManager Prog => Object.FindFirstObjectByType<ProgressionManager>();

        // ============================================================
        // ห้องสอบ (เปิดจาก ExamController.TryTakeExam)
        // ============================================================
        public void OpenExamRoom(bool final, int sem, GameObject interactor)
        {
            if (HasSessionInProgress) { ResumeWindow(); return; }
            roundFinal = final; roundSem = sem;
            Lock(interactor);
            EnsureUI().ShowCourseList();
        }

        public void LeaveExamRoom()
        {
            if (HasSessionInProgress) { EnsureUI().ShowExam(); return; }   // ยังสอบค้าง → ออกไม่ได้ (ต้องส่งหรือเลิกสอบ)
            if (Session != null && Session.submitted) Session = null;       // ดูผลแล้ว
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Session == null) IsPreview = false;
#endif
            Release();
            EnsureUI().HideAll();
        }

        ExamMinigameUI EnsureUI()
        {
            if (ui == null) ui = ExamMinigameUI.EnsureExists(this);
            return ui;
        }

        // ---------- รายการวิชาในห้องสอบ ----------
        public List<CourseEntry> BuildEntries()
        {
            var list = new List<CourseEntry>();
            var svc = Svc;
            if (svc == null) return list;
            foreach (var e in svc.CurrentEnrollments())
            {
                var def = svc.Curriculum.Get(e.code);
                var bank = Db.Get(e.code);
                var entry = new CourseEntry
                {
                    code = e.code,
                    title = def != null ? def.title : e.code,
                    questions = Db.QuestionsFor(bank),
                    timeLimit = Db.TimeFor(bank),
                };
                if (RegistrationService.HasExamScore(e, roundFinal)) entry.examScore = roundFinal ? e.final : e.midterm;
                if (Session != null && Session.InProgress && Session.courseCode == e.code) { entry.status = EntryStatus.InProgress; entry.reason = "กำลังสอบอยู่ — กดเพื่อทำต่อ"; }
                else if (entry.examScore >= 0f) { entry.status = EntryStatus.Submitted; entry.reason = (roundFinal ? e.missedFinal : e.missedMidterm) ? "ขาดสอบรอบนี้" : "ส่งข้อสอบรอบนี้แล้ว"; }
                else if (Db.UsesLegacyAssessment(def)) { entry.status = EntryStatus.LegacyAssessment; entry.reason = "วิชาโครงงาน/ฝึกงาน — ประเมินด้วยระบบเดิม (ไม่ใช้มินิเกม)"; }
                else if (!Db.HasContent(e.code)) { entry.status = EntryStatus.NoContent; entry.reason = "ข้อสอบมินิเกมของวิชานี้ยังไม่พร้อม — ใช้การสอบแบบเดิมของระบบ"; }
                else if (!CanStart(e.code, out var why)) { entry.status = EntryStatus.NotEligible; entry.reason = why; }
                else { entry.status = EntryStatus.Ready; entry.reason = "พร้อมสอบ"; }
                list.Add(entry);
            }
            return list;
        }

        // ---------- ตรวจสิทธิ์สอบ (เหตุผลภาษาไทย) ----------
        public bool CanStart(string code, out string reason)
        {
            reason = null;
            var reg = Reg; var svc = Svc;
            if (reg == null || !reg.IsActive || svc == null) { reason = "ระบบลงทะเบียนเรียนไม่ได้ใช้กับคณะนี้ — ใช้การสอบแบบเดิม"; return false; }
            var exam = Exam;
            var prog = Prog;
            int dayInYear = prog != null ? prog.DayInYear : 1;
            int sem = AcademicCalendar.SemesterIndex(dayInYear);
            int semDay = AcademicCalendar.SemesterDay(dayInYear);
            if (exam == null || !exam.HasPendingExam || exam.PendingIsFinal != roundFinal || exam.PendingSemester != roundSem || sem != roundSem)
            {
                reason = sem >= 2 ? "ภาคฤดูร้อนไม่มีการสอบ"
                       : $"ไม่อยู่ในช่วงสอบ — กลางภาควันที่ {ExamController.MidtermDay(sem)} · ปลายภาควันที่ {ExamController.FinalDay(sem)} ของภาค (วันนี้วันที่ {semDay})";
                return false;
            }
            var e = svc.CurrentEnrollment(code);
            if (e == null) { reason = $"ไม่ได้ลงทะเบียน {code} ในภาคเรียนปัจจุบัน"; return false; }
            if (e.graded) { reason = "ประกาศเกรดวิชานี้แล้ว"; return false; }
            if (RegistrationService.HasExamScore(e, roundFinal)) { reason = "ส่งข้อสอบวิชานี้ของรอบนี้แล้ว — สอบซ้ำไม่ได้ (ถ้าไม่ผ่านให้ลงทะเบียนเรียนซ้ำตามระบบเดิม)"; return false; }
            if (Session != null && Session.InProgress && Session.courseCode != code) { reason = $"มีการสอบวิชา {Session.courseCode} ค้างอยู่ — ส่งหรือเลิกสอบวิชานั้นก่อน"; return false; }
            var def = svc.Curriculum.Get(code);
            if (Db.UsesLegacyAssessment(def)) { reason = "วิชานี้ประเมินด้วยระบบเดิม"; return false; }
            if (!Db.HasContent(code) || Db.QuestionsFor(Db.Get(code)) <= 0) { reason = "ข้อสอบมินิเกมของวิชานี้ยังไม่พร้อม"; return false; }
            if (Db.minStudyRatioToSit > 0f && svc.StudyRatio(e, semDay) + 1e-4f < Db.minStudyRatioToSit)
            {
                reason = $"เวลาเรียนไม่ถึงเกณฑ์ขั้นต่ำ {Db.minStudyRatioToSit * 100:0}% (ตอนนี้ {svc.StudyRatio(e, semDay) * 100:0}%)";
                return false;
            }
            return true;
        }

        // ความรู้ของวิชาที่สอบ 0–100 = สัดส่วนการเรียนรายวิชาเดิม (เข้าเรียน + อ่านทบทวน) — ไม่ใช้ความรู้วิชาอื่น
        public float CourseKnowledge(string code)
        {
            var svc = Svc; if (svc == null) return 0f;
            var e = svc.CurrentEnrollment(code); if (e == null) return 0f;
            return Mathf.Clamp(svc.StudyRatio(e, Reg.SemDay) * 100f, 0f, 100f);
        }

        public int HintsFor(string code) => ExamMinigameLogic.HintAllowance(CourseKnowledge(code), Db);

        // ============================================================
        // เริ่มสอบ — เริ่มจับเวลาเมื่อกดยืนยันเท่านั้น
        // ============================================================
        public bool StartExam(string code, out string reason)
        {
            if (!CanStart(code, out reason)) return false;
            var svc = Svc; var e = svc.CurrentEnrollment(code); var def = svc.Curriculum.Get(code);
            var bank = Db.Get(code);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevNextSeed.HasValue) { rng = new System.Random(DevNextSeed.Value); DevNextSeed = null; }
#endif
            var s = ExamMinigameLogic.CreateSession(Db, bank, rng);
            if (s.questions.Count == 0) { reason = "ข้อสอบมินิเกมของวิชานี้ยังไม่พร้อม"; return false; }
            var t = svc.Record.Current;
            s.playerId = PlayerId();
            s.courseTitle = def != null ? def.title : code;
            s.calendarYear = t != null ? t.calendarYear : 0;
            s.semIndex = roundSem;
            s.termSerial = e.termSerial;
            s.attempt = e.attempt;
            s.isFinal = roundFinal;
            s.roundKey = ExamSessionState.MakeRoundKey(s.calendarYear, s.semIndex, s.isFinal, code, s.termSerial);
            s.knowledgeAtStart = CourseKnowledge(code);
            s.hintsAllowed = ExamMinigameLogic.HintAllowance(s.knowledgeAtStart, Db);
            s.started = true;
            Session = s;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            IsPreview = false;   // รอบจริง
#endif
            SFXManager.Page();
            SaveManager.Save();   // จำชุดข้อ/ลำดับทันที (ปิดเกมกลางคันแล้วโหลด = ชุดเดิม)
            EnsureUI().ShowExam();
            return true;
        }

        static string PlayerId()
        {
            string name = string.IsNullOrEmpty(GameSession.PlayerName) ? "local" : GameSession.PlayerName;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && (nm.IsClient || nm.IsServer)) name += "#" + nm.LocalClientId;
            return name;
        }

        public ExamQuestion QuestionAt(int i)
        {
            if (Session == null || i < 0 || i >= Session.questions.Count) return null;
            var bank = Db.Get(Session.courseCode);
            var q = bank != null ? bank.Find(Session.questions[i].questionId) : null;
            return q != null && (int)q.type == Session.questions[i].type ? q : null;
        }

        public bool UseHint(int i, out string reason)
        {
            reason = null;
            if (Session == null || i < 0 || i >= Session.questions.Count) return false;
            bool ok = ExamMinigameLogic.UseHint(Session, Session.questions[i], QuestionAt(i), rng, out reason);
            if (ok) SFXManager.Page();
            return ok;
        }

        // ============================================================
        // จับเวลา + ล็อกผู้เล่น
        // ============================================================
        void Update()
        {
            GameClock.Suspended = locked;
            if (Session == null || !Session.InProgress) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.State == GameState.Paused) return;            // pause จริงของเกม = หยุดนับ
            if (gm != null && !gm.IsActive) { Submit(true); return; }          // เกมจบกลางคัน → ส่งที่มี
            Session.remainingSeconds -= Time.unscaledDeltaTime;                  // ไม่ขึ้นกับ timeScale
            if (Session.remainingSeconds <= 0f)
            {
                Session.remainingSeconds = 0f;
                Submit(true);                                                    // หมดเวลา → ส่งอัตโนมัติ (ครั้งเดียว)
            }
        }

        void LateUpdate()
        {
            // แผงอื่น (โทรศัพท์/กระเป๋า) อาจคืน move.enabled ตอนปิด → ล็อกซ้ำทุกเฟรมระหว่างอยู่ในห้องสอบ
            if (locked && move != null && move.enabled) move.enabled = false;
        }

        void Lock(GameObject interactor)
        {
            player = interactor != null ? interactor : GameObject.Find("Player");
            if (player != null)
            {
                move = player.GetComponent<PlayerMovement>();
                action = player.GetComponent<PlayerActionController>();
                if (action == null) action = player.AddComponent<PlayerActionController>();
                if (!action.IsBusy) { action.BeginHold("Sitting"); heldByUs = true; }
            }
            locked = true;
            GameClock.Suspended = true;
            if (move != null) move.enabled = false;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            HUDController.Prompt("");
        }

        void Release()
        {
            locked = false;
            GameClock.Suspended = false;
            if (heldByUs && action != null) action.EndHold();
            heldByUs = false;
            if (move != null) move.enabled = true;
        }

        // ============================================================
        // ส่งข้อสอบ / เลิกสอบ / บันทึกคะแนน (ครั้งเดียวต่อรอบ)
        // ============================================================
        public void Submit(bool auto)
        {
            var s = Session;
            if (s == null || !s.started || s.submitted) return;   // กดส่งซ้ำ/หมดเวลาซ้ำ → ไม่ทำอะไร
            s.autoSubmitted = auto;
            ExamMinigameLogic.Submit(s, Db.Get(s.courseCode));
            RecordResult();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsPreview)
#endif
            SaveManager.Save();                                   // บันทึกทันที กันโหลดเซฟแล้วสอบซ้ำ
            if (auto) HUDController.Toast($"หมดเวลา! ส่งข้อสอบ {s.courseCode} อัตโนมัติแล้ว");
            if (locked) EnsureUI().ShowResult();
        }

        public void QuitExam()
        {
            if (Session == null || !Session.InProgress) return;
            Session.quit = true;
            Submit(false);
        }

        // ส่งคะแนนเข้าสู่ระบบเกรดเดิม — recorded กันซ้ำ + RegistrationService ไม่รับคะแนนซ้ำของรอบเดียวกัน
        void RecordResult()
        {
            var s = Session;
            if (s == null || !s.submitted || s.recorded) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsPreview) { s.recordedExamScore = -1f; LastRewardText = "โหมด PREVIEW — ไม่บันทึกคะแนน ไม่ให้หน่วยกิต/รางวัล"; return; }
#endif
            var reg = Reg;
            float quiz = ExamMinigameLogic.QuizFraction(s);
            float examScore = -1f;
            if (reg != null && reg.IsActive)
            {
                var e = reg.Service.CurrentEnrollment(s.courseCode);
                bool sameRound = e != null && e.termSerial == s.termSerial;
                if (sameRound) examScore = reg.RecordCourseExam(s.courseCode, s.isFinal, quiz, Perks.ExamBonus);
            }
            s.recorded = true;
            s.recordedExamScore = examScore;
            if (examScore < 0f) { LastRewardText = "ไม่ได้บันทึกคะแนน (รอบสอบนี้ปิดไปแล้วหรือมีคะแนนอยู่แล้ว)"; return; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevRecordCount++;
#endif

            GiveRewards(examScore, s.isFinal);
            GameplayEvents.Raise(GameplayEvents.Exam);
            if (examScore >= 0.55f) SFXManager.Success(); else SFXManager.Error();
            var exam = Exam;
            if (exam != null) exam.RefreshCourseRoundCompletion();
        }

        // รางวัลตามตารางเดิมของ ExamController แบ่งตามจำนวนวิชาที่สอบในรอบ (สอบครบทุกวิชา ≈ รางวัลสอบหนึ่งครั้งแบบเดิม)
        void GiveRewards(float examScore, bool final)
        {
            ExamController.RewardFor(examScore, final, out _, out _, out float k, out int exp, out int money, out float sat);
            int n = Mathf.Max(1, Svc != null ? Svc.CurrentEnrollments().Count : 1);
            float share = 1f / n;
            k *= share; exp = Mathf.Max(1, Mathf.RoundToInt(exp * share)); money = Mathf.RoundToInt(money * share); sat *= share;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevRewardCount++;
#endif
            var stats = Object.FindFirstObjectByType<PlayerStats>();
            if (stats != null)
            {
                stats.ChangeKnowledge(k);
                stats.AddExp(exp);
                stats.ChangeSatisfaction(sat);
                if (money > 0) stats.ChangeMoney(money);
            }
            LastRewardText = $"EXP +{Mathf.RoundToInt(k) + exp}" + (money > 0 ? $"   ทุน +{money}฿" : "") + (Mathf.Abs(sat) >= 0.5f ? $"   ความพึงพอใจ {(sat >= 0 ? "+" : "")}{sat:0}" : "");
        }

        // เรียกจาก ExamController.ResolveMissedExam (ข้ามวัน) — ส่งคำตอบที่มีอยู่
        public void ForceSubmitIfActive()
        {
            if (Session != null && Session.InProgress) Submit(true);
        }

        // ============================================================
        // หน้าต่าง
        // ============================================================
        public void HideWindow() { if (HasSessionInProgress) EnsureUI().ShowMiniChip(); }

        public void ResumeWindow()
        {
            if (!HasSessionInProgress) return;
            if (!locked) Lock(GameObject.Find("Player"));
            roundFinal = Session.isFinal; roundSem = Session.semIndex;
            EnsureUI().ShowExam();
        }

        // วิชาที่ไม่ใช้มินิเกม → ข้อสอบแบบเดิมของ ExamController (เฉพาะวิชาที่เลือก) แล้วกลับหน้ารายการ
        public void StartLegacyExam(List<string> codes)
        {
            var exam = Exam;
            if (exam == null || codes == null || codes.Count == 0) { HUDController.Toast("ไม่พบระบบสอบแบบเดิมในฉาก"); return; }
            EnsureUI().HideAll();
            exam.BeginLegacyForCourses(codes, roundFinal, roundSem, () =>
            {
                if (locked) EnsureUI().ShowCourseList();
            });
        }

        // ============================================================
        // Save / Load
        // ============================================================
        public void CollectSave(SaveData d)
        {
            if (Session == null || !Session.started) { d.hasExamSession = false; return; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsPreview) { d.hasExamSession = false; return; }   // Preview ไม่ถูกเซฟ
#endif
            d.hasExamSession = true;
            d.examSession = Session;
        }

        // เรียกจาก SaveManager.ApplyIfPending หลังคืนหลักสูตร/วัน/เวลา — ไม่สุ่มข้อใหม่ ไม่คืนคำใบ้ ไม่เพิ่มเวลา
        public void RestoreFrom(SaveData d)
        {
            Session = null;
            if (d == null || !d.hasExamSession || d.examSession == null || !d.examSession.started) return;
            var s = d.examSession;
            Session = s;
            if (s.submitted)
            {
                if (!s.recorded) RecordResult();   // ส่งแล้วแต่ยังไม่บันทึก (ปิดเกมจังหวะนั้น) → บันทึกครั้งเดียว
                Session = null;                    // จบรอบแล้ว ไม่ต้องเปิดหน้าผลซ้ำ
                return;
            }
            // ยังสอบค้าง: รอบยังใช้ได้ → กลับไปทำต่อ · ไม่ใช่แล้ว (ประกาศเกรด/มีคะแนน/เวลาหมด) → ส่งคำตอบที่มี
            roundFinal = s.isFinal; roundSem = s.semIndex;
            var svc = Svc;
            var e = svc != null ? svc.CurrentEnrollment(s.courseCode) : null;
            bool valid = e != null && e.termSerial == s.termSerial && !e.graded && !RegistrationService.HasExamScore(e, s.isFinal) && s.remainingSeconds > 0f;
            if (!valid) { Submit(true); Session = null; return; }
            StartCoroutine(ResumeNextFrame());
        }

        System.Collections.IEnumerator ResumeNextFrame()
        {
            yield return null;   // รอ UI/ผู้เล่น Start ก่อน
            if (HasSessionInProgress)
            {
                ResumeWindow();
                HUDController.Toast($"กลับมาสอบ {Session.courseCode} ต่อ — เหลือเวลา {Mathf.CeilToInt(Session.remainingSeconds)} วินาที");
            }
        }
    }
}
