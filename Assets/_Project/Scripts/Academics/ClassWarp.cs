using UnityEngine;
using TMPro;
using UnityEngine.UI;
using NisitSimulator.Core;
using NisitSimulator.Interaction;
using NisitSimulator.Player;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;

namespace NisitSimulator.Academics
{
    public enum ClassWarpDecision { Idle, Warp, Suspend, End, Finish }

    // สถานะที่ใช้ตัดสินใจเร่งเวลา (เก็บจากเกมทุกเฟรม แล้วส่งให้ตรรกะล้วน ClassWarpRules)
    public struct ClassWarpInput
    {
        public bool singlePlayer, registrarActive;
        public bool inClass;               // กำลังอยู่ในคาบที่เร่งอยู่ (เริ่มไปแล้ว)
        public bool seated, studySpot;     // นั่งที่นั่งที่ให้ความรู้อยู่
        public bool gameEnded, exhausted, examSuspended, dayChanged;
        public bool paused, windowOpen;    // pause / หน้าต่างอื่น (timeScale 0, โทรศัพท์, แอปลงทะเบียน ฯลฯ)
        public bool hasSession;            // มีคาบกำลังเรียนอยู่ (inClass = คาบเดิมยังไม่เลิก)
        public string sessionRoomId, spotRoomId;
        public bool playerInSessionRoom;   // ผู้เล่นอยู่ใน ClassroomZone ของคาบ
    }

    // ===== ตรรกะล้วนของการเร่งเวลาตอนเข้าเรียน (ทดสอบด้วย EditMode test) =====
    public static class ClassWarpRules
    {
        public static ClassWarpDecision Decide(ClassWarpInput i)
        {
            ClassWarpDecision stop = i.inClass ? ClassWarpDecision.End : ClassWarpDecision.Idle;
            if (!i.singlePlayer || !i.registrarActive) return stop;               // multiplayer = ไม่เร่ง
            if (i.gameEnded || i.dayChanged) return stop;
            if (!i.seated || !i.studySpot) return stop;
            if (i.exhausted || i.examSuspended) return stop;
            if (!i.hasSession) return i.inClass ? ClassWarpDecision.Finish : ClassWarpDecision.Idle;   // นอกคาบ (อ่านทบทวน) ไม่เร่ง / คาบเลิก = จบ
            if (string.IsNullOrEmpty(i.sessionRoomId) || i.spotRoomId != i.sessionRoomId) return stop;   // ห้องผิด ไม่เร่ง
            if (!i.playerInSessionRoom) return stop;                                // ออกจากห้อง
            if (i.paused || i.windowOpen) return ClassWarpDecision.Suspend;         // หยุดชั่วคราว ปิดหน้าต่างแล้วเร่งต่อ
            return ClassWarpDecision.Warp;
        }

        // ตัวคูณที่ทำให้เวลาเดินด้วยอัตราเป้าหมาย แต่ไม่เลยเวลาเลิกคาบในเฟรมเดียว (ไม่ต่ำกว่า 1)
        public static float Multiplier(float baseMinutesPerSecond, float targetMinutesPerSecond, float remainingMinutes, float deltaTime)
        {
            if (baseMinutesPerSecond <= 0f || targetMinutesPerSecond <= baseMinutesPerSecond) return 1f;
            float m = targetMinutesPerSecond / baseMinutesPerSecond;
            if (deltaTime > 0f && remainingMinutes > 0f) m = Mathf.Min(m, remainingMinutes / (baseMinutesPerSecond * deltaTime));
            return Mathf.Max(1f, m);
        }

        // ตัวคูณที่ต้องตั้งตามผลตัดสิน — ทุกกรณีที่ไม่ใช่ Warp กลับเป็น 1 ทันที
        public static float MultiplierFor(ClassWarpDecision d, float warpMultiplier) => d == ClassWarpDecision.Warp ? Mathf.Max(1f, warpMultiplier) : 1f;

        // เร่งจนถึงเวลาเลิกคาบเสมอ (มาสาย = เร่งเฉพาะที่เหลือ)
        public static float WarpEndMinute(ClassSession s) => s != null ? s.endMinute : 0f;
        public static float RemainingMinutes(ClassSession s, float nowMinute) => s == null ? 0f : Mathf.Max(0f, s.endMinute - nowMinute);

        // ชั่วโมงที่จะได้ถ้านั่งตั้งแต่ sitMinute จนเลิกคาบ — tick ทุก 60 นาทีนับจากเวลานั่ง ·
        //   tick นับเมื่อช่วง 60 นาทีของมันทับคาบ และเกิดไม่เกินเวลาเลิก (เร่งหยุดที่เวลาเลิก) → ชั่วโมงที่ไม่ครบไม่นับ
        public static int ExpectedTicks(ClassSession s, float sitMinute, int alreadyTicks = 0, float toleranceMinutes = 1f)
        {
            if (s == null) return 0;
            int n = 0;
            for (int k = 1; k <= 48; k++)
            {
                float t = sitMinute + 60f * k;
                if (t > s.endMinute + toleranceMinutes) break;
                if (t > s.startMinute && t - 60f < s.endMinute) n++;
            }
            return Mathf.Clamp(n, 0, Mathf.Max(0, s.MaxTicks - alreadyTicks));
        }

        // วินาทีจริงโดยประมาณที่ใช้เร่งช่วงที่เหลือ
        public static float RealSecondsFor(float remainingMinutes, float targetMinutesPerSecond) =>
            targetMinutesPerSecond > 0f ? remainingMinutes / targetMinutesPerSecond : 0f;
    }

    // ===== ตัวควบคุมการเร่งเวลาตอนเข้าเรียน (เล่นคนเดียว) =====
    //   เริ่มเมื่อ: นั่งโต๊ะเรียนใน ClassroomZone ของคาบที่กำลังเรียน (หรือเข้าผ่านตู้เข้าเรียน/ClassStation ซึ่งพามานั่งให้)
    //   จบเมื่อ: ถึงเวลาเลิกคาบ (ลุกเอง + toast) · ลุกเอง (E) · หมดแรง · ห้องสอบ · ข้ามวัน · ออกจากห้อง · จบเกม · โหลด/Destroy
    //   หยุดชั่วคราว (ตัวคูณ = 1) เมื่อ pause / เปิดหน้าต่างอื่น / เหตุการณ์ระหว่างเรียน แล้วเร่งต่อเองเมื่อปิด
    //   สร้างอัตโนมัติโดย GameplayBootstrap — ไม่ต้องแก้ฉาก
    public class ClassWarpController : MonoBehaviour
    {
        public static ClassWarpController Instance { get; private set; }

        [Header("ความเร็วตอนเรียน")]
        [Tooltip("นาทีเกมต่อวินาทีจริงระหว่างเรียน (35 → คาบ 2 ชม. ใช้เวลาจริงราว 3.4 วินาที) · ตัวคูณ = ค่านี้ ÷ GameClock.gameMinutesPerRealSecond")]
        public float classGameMinutesPerSecond = 35f;
        [Tooltip("ยอมให้ชั่วโมงสุดท้ายขาดได้กี่นาทีเกมตอนเลิกคาบ (กันเศษทศนิยม/ลำดับ Update)")]
        public float finishToleranceMinutes = 1f;
        [Range(0f, 0.6f)] public float dimAlpha = 0.22f;

        public bool InClass { get; private set; }
        public Enrollment Enrollment { get; private set; }
        public ClassSession Session { get; private set; }
        public int SessionIndex { get; private set; }
        public int SemDay { get; private set; }
        public string RoomId { get; private set; }
        public ActivitySpot Spot => spot;
        public ClassWarpDecision LastDecision { get; private set; }
        public static bool IsWarping => GameClock.WarpMultiplier > 1.001f;

        ActivitySpot spot; GameObject who;
        GameClock clock; PhoneController phone;
        bool finishing, dayChanged;
        float beginRealTime;   // เวลาจริงตอนเริ่มเร่ง (ไว้รายงานว่าคาบใช้เวลาจริงกี่วินาที)
        float sitMinute;

        GameObject dimRoot, bannerRoot; TMP_Text bannerText; Image dimImg;

        public static ClassWarpController EnsureExists()
        {
            if (Instance != null) return Instance;
            return new GameObject("ClassWarpController").AddComponent<ClassWarpController>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            GameClock.WarpMultiplier = 1f;
        }

        void Start()
        {
            clock = Object.FindFirstObjectByType<GameClock>();
            phone = Object.FindFirstObjectByType<PhoneController>(FindObjectsInactive.Include);
            if (clock != null) clock.OnDayChanged += OnDay;
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += OnState;
        }

        void OnDestroy()
        {
            GameClock.WarpMultiplier = 1f;
            if (clock != null) clock.OnDayChanged -= OnDay;
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= OnState;
            if (Instance == this) Instance = null;
        }

        void OnDisable() { GameClock.WarpMultiplier = 1f; }

        void OnDay(int d) { if (InClass || spot != null) dayChanged = true; GameClock.WarpMultiplier = 1f; }
        void OnState(GameState s) { if (s == GameState.GameOver || s == GameState.Win || s == GameState.Paused) GameClock.WarpMultiplier = 1f; }

        // ---------- แจ้งจาก ActivitySpot ----------
        public static void NotifySeated(ActivitySpot s, GameObject who) { if (Instance != null) Instance.OnSeated(s, who); }
        public static void NotifyStood(ActivitySpot s) { if (Instance != null) Instance.OnStood(s); }

        void OnSeated(ActivitySpot s, GameObject w)
        {
            if (s == null || !s.IsStudySpot) return;
            spot = s; who = w; dayChanged = false;
            sitMinute = clock != null ? clock.TotalMinutes : 0f;
            if (!ClassroomRules.IsSinglePlayer) return;
            // นั่งผิดห้องระหว่างคาบ → บอกทันทีว่าวิชานี้เรียนห้องไหน
            var reg = CourseRegistrar.Instance;
            if (reg != null && reg.IsActive && reg.TryGetOngoingSession(out var e, out var ss, out _) && ss.HasRoom)
            {
                string here = ClassroomZone.RoomOfSpot(s.transform);
                if (here != ss.roomId)
                    HUDController.Toast($"คาบ {e.code} เรียนที่ {CourseRegistrar.RoomText(ss)} — {(here != null ? "ห้องนี้" : "ที่นี่")}ไม่นับเข้าเรียน");
            }
        }

        void OnStood(ActivitySpot s)
        {
            if (s != spot) return;
            if (InClass && !finishing && Enrollment != null && Session != null)
            {
                int t = Enrollment.TicksFor(RegistrationService.MeetingKey(SemDay, SessionIndex));
                HUDController.Toast($"ลุกก่อนเลิกคาบ — {Enrollment.code} เรียนครบ {t}/{Session.MaxTicks} ชม. (ชั่วโมงที่ไม่ครบไม่นับ)");
            }
            ResetClass();
            spot = null; who = null;
        }

        // ---------- ทุกเฟรม ----------
        void Update()
        {
            if (spot == null && !InClass) { SetMultiplier(1f); ShowUI(false); return; }

            var input = Gather(out var reg, out var e, out var s, out int idx);
            var d = ClassWarpRules.Decide(input);
            LastDecision = d;
            switch (d)
            {
                case ClassWarpDecision.Warp:
                    if (!InClass) BeginClass(reg, e, s, idx);
                    float now = clock != null ? clock.TotalMinutes : 0f;
                    float baseRate = clock != null ? clock.gameMinutesPerRealSecond : 1f;
                    SetMultiplier(ClassWarpRules.MultiplierFor(d, ClassWarpRules.Multiplier(baseRate, classGameMinutesPerSecond, ClassWarpRules.RemainingMinutes(Session, now), Time.deltaTime)));
                    ShowUI(true);
                    ClassEventSystem.Instance?.Tick(this, now);
                    break;
                case ClassWarpDecision.Suspend:
                    SetMultiplier(1f);
                    ShowUI(false);
                    break;
                case ClassWarpDecision.Finish:
                    FinishClass();
                    break;
                case ClassWarpDecision.End:
                    SetMultiplier(1f); ShowUI(false);
                    ResetClass();
                    break;
                default:
                    SetMultiplier(1f); ShowUI(false);
                    break;
            }
        }

        ClassWarpInput Gather(out CourseRegistrar reg, out Enrollment e, out ClassSession s, out int idx)
        {
            reg = CourseRegistrar.Instance;
            e = null; s = null; idx = -1;
            var gm = GameManager.Instance;
            var i = new ClassWarpInput
            {
                singlePlayer = ClassroomRules.IsSinglePlayer,
                registrarActive = reg != null && reg.IsActive,
                inClass = InClass,
                seated = spot != null && spot.IsSeated,
                studySpot = spot != null && spot.IsStudySpot,
                gameEnded = gm != null && (gm.State == GameState.GameOver || gm.State == GameState.Win),
                paused = gm != null && gm.State == GameState.Paused,
                dayChanged = dayChanged,
                examSuspended = GameClock.Suspended,
            };
            if (who != null && who.TryGetComponent<PlayerExhaustion>(out var ex)) i.exhausted = ex.IsExhausted;
            if (i.seated && !i.examSuspended) i.examSuspended = PlayerExhaustion.ExamActive();
            i.windowOpen = Time.timeScale == 0f || (phone != null && phone.IsOpen) || RegistrationUI.IsOpen
                           || SleepController.IsSleeping || DaySummaryUI.IsShowing || ClassEventUI.IsOpen || TermResultUI.IsOpen;

            if (InClass)
            {
                float now = clock != null ? clock.TotalMinutes : 0f;
                e = Enrollment; s = Session; idx = SessionIndex;
                i.hasSession = reg != null && reg.SemDay == SemDay && now < Session.endMinute && now >= Session.startMinute;
            }
            else if (i.registrarActive && i.seated && reg.TryGetOngoingSession(out e, out s, out idx))
                i.hasSession = true;

            if (s != null)
            {
                i.sessionRoomId = s.roomId;
                i.spotRoomId = spot != null ? ClassroomZone.RoomOfSpot(spot.transform) : null;
                var z = ClassroomZone.Find(s.roomId);
                i.playerInSessionRoom = z != null && who != null && z.Contains(who.transform.position);
            }
            return i;
        }

        void BeginClass(CourseRegistrar reg, Enrollment e, ClassSession s, int idx)
        {
            InClass = true; Enrollment = e; Session = s; SessionIndex = idx; SemDay = reg.SemDay; RoomId = s.roomId;
            beginRealTime = Time.realtimeSinceStartup;
            finishing = false;
            float now = clock != null ? clock.TotalMinutes : 0f;
            Debug.Log($"[ClassWarp] เริ่มเร่งเวลา {e.code} @ {s.roomId} {Hhmm(now)} → {Hhmm(s.endMinute)}");
            ClassEventSystem.Instance?.OnClassStarted(this, now);
        }

        void FinishClass()
        {
            SetMultiplier(1f); ShowUI(false);
            if (spot != null)
            {
                finishing = true;
                spot.TryGrantPendingTick(finishToleranceMinutes);   // ชั่วโมงสุดท้ายที่ครบพอดีเวลาเลิก
                int t = Enrollment != null ? Enrollment.TicksFor(RegistrationService.MeetingKey(SemDay, SessionIndex)) : 0;
                int max = Session != null ? Session.MaxTicks : 0;
                string code = Enrollment != null ? Enrollment.code : "";
                spot.ForceStand();
                HUDController.Toast($"เลิกคาบ {code} — เรียนครบ {t}/{max} ชม.");
                Debug.Log($"[ClassWarp] จบคาบ {code} · ใช้เวลาจริง {Time.realtimeSinceStartup - beginRealTime:0.0} วินาที (รวมช่วงหยุดถ้ามีเหตุการณ์/หน้าต่าง)");
            }
            ResetClass();
            spot = null; who = null;
        }

        // เลิกเรียนกะทันหัน (เหตุการณ์ไฟดับ/คอมค้าง) — ลุกจากที่นั่งพร้อมข้อความ
        public void EndClassEarly(string toast)
        {
            SetMultiplier(1f); ShowUI(false);
            finishing = true;
            if (spot != null) spot.ForceStand(toast); else if (!string.IsNullOrEmpty(toast)) HUDController.Toast(toast);
            ResetClass();
            spot = null; who = null;
        }

        void ResetClass()
        {
            SetMultiplier(1f); ShowUI(false);
            InClass = false; Enrollment = null; Session = null; SessionIndex = -1; RoomId = null;
            finishing = false; dayChanged = false;
        }

        static void SetMultiplier(float m) { GameClock.WarpMultiplier = Mathf.Max(1f, m); }

        static string Hhmm(float minutes) { int m = Mathf.FloorToInt(minutes); return $"{(m / 60) % 24:00}:{m % 60:00}"; }

        // ---------- UI: แถบ "กำลังเรียน" + จอมืดลงเล็กน้อย ----------
        void ShowUI(bool on)
        {
            if (!on) { if (dimRoot != null) { dimRoot.SetActive(false); bannerRoot.SetActive(false); } return; }
            if (dimRoot == null) BuildUI();
            dimRoot.SetActive(true); bannerRoot.SetActive(true);
            dimImg.color = new Color(0.08f, 0.06f, 0.16f, dimAlpha);
            float now = clock != null ? clock.TotalMinutes : 0f;
            int t = Enrollment != null ? Enrollment.TicksFor(RegistrationService.MeetingKey(SemDay, SessionIndex)) : 0;
            bannerText.text = $"<b>กำลังเรียน {Enrollment?.code} · {RoomId} · {Hhmm(now)} → {Hhmm(Session != null ? Session.endMinute : now)}</b>" +
                              $"\n<size=75%>นับแล้ว {t}/{(Session != null ? Session.MaxTicks : 0)} ชม. · กด E ลุกก่อนได้ (ชั่วโมงที่ไม่ครบไม่นับ)</size>";
        }

        void BuildUI()
        {
            var dimCanvas = GrowthUI.MakeCanvas(transform, "ClassWarp Dim", -5);   // ใต้ HUD (order 0)
            dimRoot = dimCanvas.gameObject;
            dimImg = GrowthUI.Dim(dimCanvas.transform);
            dimImg.raycastTarget = false;
            var c = GrowthUI.MakeCanvas(transform, "ClassWarp Banner", 40);
            bannerRoot = c.gameObject;
            var box = GrowthUI.Box(c.transform, "Banner", new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(860f, 96f), GrowthUI.CardCol);
            box.raycastTarget = false;
            bannerText = GrowthUI.Text(box.transform, "", Vector2.zero, new Vector2(820f, 88f), 26, GrowthUI.Ink);
            dimRoot.SetActive(false); bannerRoot.SetActive(false);
        }
    }
}
