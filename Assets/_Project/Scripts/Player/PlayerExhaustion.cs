using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Core;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;
using NisitSimulator.SaveLoad;
using NisitSimulator.Interaction;
using NisitSimulator.CameraRig;
using NisitSimulator.Systems;
using NisitSimulator.Academics.ExamMinigame;

namespace NisitSimulator.Player
{
    // ===== กติกาหมดแรง (ฟังก์ชันล้วน — ใช้ทั้งในเกมและ EditMode test) =====
    public static class ExhaustionRules
    {
        // เข้าเมื่อพลังงานลงถึงเกณฑ์เข้า (ปกติ 0)
        public static bool ShouldEnter(bool exhausted, float energy, float enterAt)
            => !exhausted && energy <= enterAt + 0.0001f;

        // ออกเมื่อฟื้นถึงเกณฑ์ออก (สัดส่วนของค่าสูงสุด) — แยกจากเกณฑ์เข้า กันสลับไปมาตอนพลังงานใกล้ 0
        public static bool ShouldExit(bool exhausted, float energy, float max, float exitPercent)
            => exhausted && energy >= ExitEnergy(max, exitPercent) - 0.0001f;

        public static float ExitEnergy(float max, float exitPercent) => Mathf.Max(0f, max) * Mathf.Clamp01(exitPercent);
        public static float RecoverAmount(float max, float percent) => Mathf.Max(0f, max) * Mathf.Clamp01(percent);

        // โหลดเซฟ: เซฟบอกว่าหมดแรงและยังไม่ถึงเกณฑ์ออก → ยังหมดแรง · พลังงาน 0 (รวมเซฟเก่าที่ไม่มีธง) → หมดแรง
        public static bool FromSave(bool savedFlag, float energy, float max, float enterAt, float exitPercent)
            => energy <= enterAt + 0.0001f || (savedFlag && energy < ExitEnergy(max, exitPercent) - 0.0001f);
    }

    // ===== สถานะ "หมดแรง" (แทน Game Over เมื่อพลังงานเป็น 0) =====
    //   ใส่ไว้ที่ Player คู่กับ PlayerStats (GameplayBootstrap เติมให้เองถ้าในฉากไม่มี) — ค่าทั้งหมดปรับได้ใน Inspector
    //   ผล: เดินช้าลง (ตัวคูณแยก ไม่เขียนทับอาการป่วย/ความสามารถ) · ห้ามวิ่ง/กระโดด · ห้ามเริ่มกิจกรรมที่ใช้พลังงาน
    //   ยังใช้ได้: โทรศัพท์ กระเป๋า กิน ซื้อของ พัก นอน (ระบบเหล่านั้นไม่ถูกแตะ)
    //   ฟื้น: พักตรงนี้ / กลับหอพักเพื่อพักฟื้น / กินหรือใช้ไอเทม / นอน (ทุกทางผ่าน PlayerStats.ChangeEnergy → ออกจากสถานะเมื่อถึงเกณฑ์)
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerExhaustion : MonoBehaviour
    {
        public enum RestKind { Here, Dorm }
        public enum RestState { Idle, Resting }

        public static PlayerExhaustion Local { get; private set; }

        [Header("เกณฑ์เข้า/ออก")]
        [Tooltip("พลังงานเท่านี้หรือต่ำกว่า = หมดแรง")] public float enterAtEnergy = 0f;
        [Range(0.01f, 1f)] [Tooltip("ฟื้นถึงสัดส่วนนี้ของพลังงานสูงสุด = หายหมดแรง (ต้องมากกว่าเกณฑ์เข้า)")] public float exitAtEnergyPercent = 0.15f;

        [Header("ผลของสถานะหมดแรง")]
        [Range(0.1f, 1f)] [Tooltip("ตัวคูณความเร็วเดินขณะหมดแรง (คูณกับตัวคูณอื่นของ PlayerMovement ไม่เขียนทับ)")] public float moveSpeedMultiplier = 0.6f;

        [Header("พักตรงนี้")]
        [Tooltip("นาทีในเกม")] public float restHereMinutes = 30f;
        [Range(0f, 1f)] public float restHereRecoverPercent = 0.15f;

        [Header("กลับหอพักเพื่อพักฟื้น")]
        [Tooltip("นาทีในเกม")] public float dormRestMinutes = 60f;
        [Range(0f, 1f)] public float dormRestRecoverPercent = 0.20f;

        [Header("ค่าใช้จ่าย (เวอร์ชันนี้ = ฟรี กันติดสถานะตอนไม่มีเงิน)")]
        public int restHereCost = 0;
        public int dormRestCost = 0;

        [Header("อื่น ๆ")]
        public KeyCode restMenuKey = KeyCode.K;
        [Tooltip("เวลาเฟดดำ (วินาทีจริง) ตอนพักคนเดียว")] public float fadeSeconds = 0.35f;
        [Tooltip("ระยะห่างขั้นต่ำระหว่างข้อความ 'วิ่ง/กระโดดไม่ได้' (วินาทีจริง)")] public float blockedHintCooldown = 2.5f;

        public const string MsgEnter = "หมดแรงแล้ว พักก่อนหรือกลับหอพัก";
        public const string MsgExit = "พักเพียงพอแล้ว";

        public bool IsExhausted { get; private set; }
        public RestState State => restState;
        public bool IsResting => restState != RestState.Idle;
        public int EnterCount { get; private set; }      // จำนวนครั้งที่เข้าสถานะ (ทดสอบ: ต้องเพิ่มครั้งเดียวต่อการหมดแรง)
        public int CompletedRests { get; private set; }  // จำนวนการพักที่ให้ผลแล้ว (ทดสอบ: กดซ้ำต้องไม่เพิ่ม)
        public string LastRestResult { get; private set; } = "";

        // ตัวคูณที่ PlayerMovement อ่าน (อ่านค่า ไม่สะสม → ไม่ลดซ้ำทุกเฟรม)
        public float MoveSpeedMultiplier => IsExhausted ? Mathf.Clamp(moveSpeedMultiplier, 0.1f, 1f) : 1f;
        public bool CanRun => !IsExhausted;
        public bool CanJump => !IsExhausted;

        PlayerStats stats;
        bool bound;
        bool panelPending;
        float lastHintTime = -99f;
        RestState restState = RestState.Idle;
        PlayerActionController holdAction;
        float mpRestProgress, mpRestTarget;
        bool mpCancel;

        // UI
        GameObject canvasGo, panel, chip;
        TMP_Text chipText, bodyText;
        Button hereBtn, dormBtn, closeBtn;
        CanvasGroup fade;
        Image fadeImage;

        // สีตามธีมพาสเทลของเกม (PastelTheme) — กำหนดเอง ไม่พึ่งการแปลงสีตอนโหลดฉาก
        static readonly Color CardCol = new Color(0.955f, 0.93f, 0.985f, 0.97f);
        static readonly Color TextDark = new Color(0.30f, 0.25f, 0.46f, 1f);
        static readonly Color ChipCol = new Color(1.00f, 0.80f, 0.78f, 0.95f);

        public static PlayerExhaustion EnsureOn(GameObject player)
        {
            if (player == null) return null;
            var ex = player.GetComponent<PlayerExhaustion>();
            if (ex == null) ex = player.AddComponent<PlayerExhaustion>();
            ex.Bind();
            return ex;
        }

        void Awake() { Bind(); }

        // ผูกกับ PlayerStats (เรียกซ้ำได้ — EditMode test เรียกเองเพราะ Awake ไม่ทำงานนอก Play)
        public void Bind()
        {
            if (bound) return;
            stats = GetComponent<PlayerStats>();
            if (stats == null) return;
            stats.OnEnergyChanged += OnEnergyChanged;
            bound = true;
            if (gameObject.name == "Player" || Local == null) Local = this;
        }

        void OnDestroy()
        {
            if (stats != null) stats.OnEnergyChanged -= OnEnergyChanged;
            if (Local == this) Local = null;
            if (canvasGo != null) Destroy(canvasGo);
        }

        // ---------- เข้า/ออกสถานะ ----------
        void OnEnergyChanged(float energy, float max) => Evaluate(energy, max);

        public void Evaluate(float energy, float max)
        {
            if (ExhaustionRules.ShouldEnter(IsExhausted, energy, enterAtEnergy)) Enter(true);
            else if (ExhaustionRules.ShouldExit(IsExhausted, energy, max, exitAtEnergyPercent)) Exit();
        }

        void Enter(bool announce)
        {
            if (IsExhausted) return;   // เข้าได้ครั้งเดียวจนกว่าจะออก
            IsExhausted = true;
            EnterCount++;
            Debug.Log("[Exhaustion] หมดแรง (พลังงาน " + (stats != null ? stats.Energy.ToString("0.#") : "?") + ")");
            if (announce) panelPending = true;   // แสดงข้อความ/ทางเลือกเมื่อไม่มีสอบหรือหน้าต่างอื่นบังอยู่
            RefreshUI();
        }

        void Exit()
        {
            if (!IsExhausted) return;
            IsExhausted = false;
            panelPending = false;
            if (panel != null) panel.SetActive(false);
            Debug.Log("[Exhaustion] หายหมดแรง (พลังงาน " + (stats != null ? stats.Energy.ToString("0.#") : "?") + ")");
            if (Application.isPlaying && restState == RestState.Idle) HUDController.Toast(MsgExit);   // ฟื้นจากการพัก → AfterRestToast แจ้งแทน (ไม่ซ้ำ)
            RefreshUI();
        }

        // โหลดเซฟ — คำนวณคืนจากธงในเซฟ + พลังงาน (ไม่ให้ผลฟื้นพลัง/ข้ามเวลาใด ๆ)
        public void RestoreFromSave(bool savedFlag)
        {
            if (stats == null) Bind();
            if (stats == null) return;
            bool ex = ExhaustionRules.FromSave(savedFlag, stats.Energy, stats.maxEnergy, enterAtEnergy, exitAtEnergyPercent);
            if (ex && !IsExhausted) Enter(true);
            else if (!ex && IsExhausted) { IsExhausted = false; RefreshUI(); }
        }

        // ---------- ใช้โดยจุดกิจกรรม ----------
        // true = เริ่มได้ · false = ปฏิเสธพร้อมเหตุผล (ตรวจ "ก่อน" หักค่าใช้จ่าย/ให้รางวัล)
        public static bool CanStartEnergyActivity(GameObject who, float energyNeeded, string activity, out string reason)
        {
            reason = "";
            if (who == null) return true;
            var ex = who.GetComponent<PlayerExhaustion>();
            if (ex != null && ex.IsExhausted)
            {
                reason = $"หมดแรงอยู่ — {activity}ไม่ได้ ต้องพักก่อน (กด {ex.restMenuKey} ดูวิธีพัก)";
                return false;
            }
            var s = who.GetComponent<PlayerStats>();
            if (s != null && energyNeeded > 0f && s.Energy < energyNeeded)
            {
                reason = $"พลังงานไม่พอ{activity} (ต้องใช้ {energyNeeded:0} มี {s.Energy:0}) ไปพักหรือกินอะไรก่อน";
                return false;
            }
            return true;
        }

        public void NotifyBlocked(string what)
        {
            if (Time.unscaledTime - lastHintTime < blockedHintCooldown) return;
            lastHintTime = Time.unscaledTime;
            HUDController.Toast($"หมดแรงอยู่ — {what}ไม่ได้ (กด {restMenuKey} เพื่อพัก)");
        }

        // ---------- ห้ามแสดง/ห้ามพักเมื่อ ----------
        // สอบอยู่ (มินิเกม/แบบเดิม) · กำลังนอน · หน้าต่างหยุดเวลาอื่นเปิดอยู่ · เกมหยุด/จบ
        public static bool ExamActive()
        {
            var mini = ExamMinigameController.Instance;
            if (ExamMinigameController.BlocksWorld || (mini != null && mini.HasSessionInProgress)) return true;
            var exam = FindFirstObjectByType<ExamController>();
            return exam != null && exam.IsOpen;
        }

        bool UiBlocked()
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsActive) return true;
            if (ExamActive() || SleepController.IsSleeping) return true;
            if (Time.timeScale == 0f) return true;
            return false;
        }

        public bool CanRestNow(RestKind kind, out string reason)
        {
            reason = "";
            if (!IsExhausted) { reason = "ยังไม่หมดแรง"; return false; }
            if (restState != RestState.Idle) { reason = "กำลังพักอยู่"; return false; }
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsActive) { reason = "เกมหยุดอยู่"; return false; }
            if (ExamActive()) { reason = "กำลังสอบอยู่ — ทำข้อสอบให้เสร็จก่อนค่อยพัก"; return false; }
            if (SleepController.IsSleeping) { reason = "กำลังนอนอยู่"; return false; }
            if (Time.timeScale == 0f) { reason = "ปิดหน้าต่างอื่นก่อนพัก"; return false; }
            var act = GetComponent<PlayerActionController>();
            if (act != null && act.IsBusy) { reason = "กำลังทำกิจกรรมอยู่"; return false; }
            var sit = GetComponent<PlayerActivity>();
            if (sit != null && sit.IsBusy) { reason = "ลุกจากที่นั่งก่อน"; return false; }
            if (FindFirstObjectByType<GameClock>() == null) { reason = "ไม่พบนาฬิกาเกม"; return false; }
            int cost = kind == RestKind.Here ? restHereCost : dormRestCost;
            if (cost > 0 && stats != null && stats.Money < cost) { reason = $"เงินไม่พอ (ต้องใช้ {cost}฿)"; return false; }
            if (kind == RestKind.Dorm && !TryGetDormPoint(out _, out _, out var why)) { reason = why + " — ใช้ \"พักตรงนี้\" แทนได้"; return false; }
            return true;
        }

        // จุดหอพักจากระบบเดิม (DormSpawnPoint + ช่องของผู้เล่นใน MP) — ตรวจว่าปลอดภัยก่อน ห้ามวาร์ปไปจุดที่ใช้ไม่ได้
        public bool TryGetDormPoint(out DormSpawnPoint dorm, out Transform point, out string why)
        {
            why = ""; point = null;
            dorm = DormSpawnPoint.Main;
            if (dorm == null) { why = "ไม่พบจุดหอพักในฉาก"; return false; }
            point = dorm.GetSlot(PlayerSpawnSystem.NetworkSlotIndex());
            if (point == null) { why = "จุดหอพักใช้งานไม่ได้"; return false; }
            var pos = PlayerSpawnSystem.GroundSnap(gameObject, point.position);
            if (!PlayerSpawnSystem.IsUsable(gameObject, pos, out var w)) { why = "จุดหอพักไม่ปลอดภัย (" + w + ")"; return false; }
            return true;
        }

        static bool IsMultiplayer()
        {
            var sync = NisitSimulator.Net.WorldTimeSync.Instance;
            return sync != null && sync.IsMultiplayerSession;
        }

        // ---------- พัก ----------
        public bool RequestRest(RestKind kind)
        {
            if (!CanRestNow(kind, out var why)) { HUDController.Toast(why); return false; }
            int cost = kind == RestKind.Here ? restHereCost : dormRestCost;
            if (cost > 0 && (stats == null || !stats.TrySpendMoney(cost))) { HUDController.Toast("เงินไม่พอ"); return false; }
            restState = RestState.Resting;   // ล็อกทันที → กดซ้ำ/เปิดพักชุดที่สองจะถูกปฏิเสธ
            if (panel != null) panel.SetActive(false);
            panelPending = false;
            RefreshUI();
            if (IsMultiplayer()) StartCoroutine(RestTimedRoutine(kind));
            else StartCoroutine(RestSkipRoutine(kind));
            return true;
        }

        public void CancelRest() { if (restState == RestState.Resting && IsMultiplayer()) mpCancel = true; }

        // เล่นคนเดียว: ข้ามเวลาด้วยนาฬิกาเดิม (GameClock.AdvanceMinutes → ยิงเหตุการณ์ราย นาที/วัน ตามกฎเดิม)
        IEnumerator RestSkipRoutine(RestKind kind)
        {
            float minutes = kind == RestKind.Here ? restHereMinutes : dormRestMinutes;
            float pct = kind == RestKind.Here ? restHereRecoverPercent : dormRestRecoverPercent;
            BeginHold();
            yield return Fade(0f, 1f);

            var clock = FindFirstObjectByType<GameClock>();
            int d0 = clock != null ? clock.Day : 0; int m0 = clock != null ? Mathf.FloorToInt(clock.TotalMinutes) : 0;
            if (clock != null && minutes > 0f) clock.AdvanceMinutes(minutes);            // ข้ามคาบ/กำหนดส่ง = ระบบเดิมตัดสิน (ไม่นับว่าทำสำเร็จ)
            var decay = GetComponent<StatDecay>();
            if (decay != null && minutes > 0f) decay.Tick(minutes);                         // ความหิว/เครียดเดินตามเวลาที่ข้ามไป (สมดุลเดิม)
            ApplyRecovery(pct);                                                              // ฟื้นครั้งเดียว

            EndHold();
            if (kind == RestKind.Dorm && TryGetDormPoint(out var dorm, out var point, out _))
                PlayerSpawnSystem.PlaceInDorm(gameObject, point, dorm);                     // จุดหอพักเดิม + สถานะอยู่ในอาคาร
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.ApplyNow();        // แสงตรงเวลาใหม่
            IsometricCameraRig.SnapAll();                                                    // กล้องตามทันที

            LastRestResult = (kind == RestKind.Here ? "พักตรงนี้" : "กลับหอพัก") +
                (clock != null ? $" วันที่ {d0} {m0 / 60 % 24:00}:{m0 % 60:00} → วันที่ {clock.Day} {clock.Hour:00}:{clock.Minute:00}" : "");
            Debug.Log("[Exhaustion] " + LastRestResult);
            SaveManager.Save();                                                              // บันทึกหลังให้ผลแล้ว → โหลดใหม่ไม่ได้ผลซ้ำ

            yield return Fade(1f, 0f);
            restState = RestState.Idle;
            RefreshUI();
            AfterRestToast(pct);
        }

        // Multiplayer: ไม่ข้ามเวลาโลก — นั่งพักจนนาฬิกาโลกเดินครบตามนาที แล้วฟื้นครั้งเดียว (ยกเลิกได้ = ไม่ได้ผล)
        IEnumerator RestTimedRoutine(RestKind kind)
        {
            float minutes = kind == RestKind.Here ? restHereMinutes : dormRestMinutes;
            float pct = kind == RestKind.Here ? restHereRecoverPercent : dormRestRecoverPercent;
            if (kind == RestKind.Dorm && TryGetDormPoint(out var dorm, out var point, out _))
            {
                PlayerSpawnSystem.PlaceInDorm(gameObject, point, dorm);
                IsometricCameraRig.SnapAll();
            }
            BeginHold();
            mpCancel = false; mpRestProgress = 0f; mpRestTarget = Mathf.Max(0f, minutes);
            var clock = FindFirstObjectByType<GameClock>();
            float last = Abs(clock);
            while (mpRestProgress < mpRestTarget && !mpCancel)
            {
                if (ExamActive()) { mpCancel = true; break; }
                float now = Abs(clock);
                if (now > last) mpRestProgress += now - last;   // ย้อนเวลา (resync) ไม่นับ
                last = now;
                RefreshUI();
                yield return null;
            }
            EndHold();
            if (mpCancel) { HUDController.Toast("ยกเลิกการพัก (ไม่ได้ฟื้นพลังงาน)"); LastRestResult = "ยกเลิก"; }
            else { ApplyRecovery(pct); LastRestResult = (kind == RestKind.Here ? "พักตรงนี้" : "กลับหอพัก") + $" (MP) {minutes:0} นาทีโลก"; }
            restState = RestState.Idle;
            RefreshUI();
            if (!mpCancel) AfterRestToast(pct);
        }

        static float Abs(GameClock c) => c != null ? c.Day * 1440f + c.TotalMinutes : Time.time;

        void ApplyRecovery(float pct)
        {
            if (stats == null) return;
            CompletedRests++;
            stats.ChangeEnergy(ExhaustionRules.RecoverAmount(stats.maxEnergy, pct));   // ผ่าน ChangeEnergy → ออกจากสถานะเองถ้าถึงเกณฑ์
            GameplayEvents.Raise(GameplayEvents.Relax);
        }

        void AfterRestToast(float pct)
        {
            if (IsExhausted) HUDController.Toast($"พักแล้ว พลังงาน +{pct * 100f:0}% — ยังเหนื่อยอยู่ พักต่อ กินอาหาร หรือไปนอน");
            else HUDController.Toast($"{MsgExit} (พลังงาน +{pct * 100f:0}%)");
        }

        void BeginHold()
        {
            holdAction = GetComponent<PlayerActionController>() ?? gameObject.AddComponent<PlayerActionController>();
            holdAction.BeginHold("Sitting");
        }

        void EndHold()
        {
            if (holdAction != null) holdAction.EndHold();
            holdAction = null;
        }

        IEnumerator Fade(float from, float to)
        {
            EnsureUI();
            fadeImage.color = Color.black;   // PastelTheme แปลงรูปสีเข้มทึบเป็นพาสเทล → ตั้งกลับเป็นจอดำทุกครั้ง
            fade.gameObject.SetActive(true);
            fade.blocksRaycasts = true;
            for (float e = 0f; e < fadeSeconds; e += Time.unscaledDeltaTime)
            {
                fade.alpha = Mathf.Lerp(from, to, e / fadeSeconds);
                yield return null;
            }
            fade.alpha = to;
            if (to <= 0f) { fade.blocksRaycasts = false; fade.gameObject.SetActive(false); }
        }

        // ---------- Update: แสดงทางเลือกเมื่อพร้อม + ปุ่มลัด ----------
        void Update()
        {
            if (!Application.isPlaying) return;
            if (Local != this) return;
            bool blocked = UiBlocked();

            if (IsExhausted && panelPending && !blocked && restState == RestState.Idle)
            {
                panelPending = false;
                HUDController.Toast(MsgEnter);
                ShowPanel(true);
            }
            if (blocked && panel != null && panel.activeSelf) { panel.SetActive(false); if (IsExhausted) panelPending = true; }   // สอบ/หน้าต่างอื่นเปิด → ซ่อน แล้วค่อยแสดงใหม่

            if (!blocked && Input.GetKeyDown(restMenuKey))
            {
                if (restState == RestState.Resting) CancelRest();
                else if (IsExhausted) ShowPanel(panel == null || !panel.activeSelf);
            }
            if (chip != null) chip.SetActive((IsExhausted || restState != RestState.Idle) && !ExamActive() && !SleepController.IsSleeping);
            if (panel != null && panel.activeSelf) UpdatePanelButtons();
        }

        void ShowPanel(bool show)
        {
            EnsureUI();
            panel.SetActive(show && IsExhausted);
            if (show) UpdatePanelButtons();
        }

        void UpdatePanelButtons()
        {
            hereBtn.interactable = CanRestNow(RestKind.Here, out _);
            bool dormOk = CanRestNow(RestKind.Dorm, out var dormWhy);
            dormBtn.interactable = dormOk;
            string dormLine = dormOk ? "" : $"\n<color=#FFB26B><size=80%>กลับหอไม่ได้: {dormWhy}</size></color>";
            bool mp = IsMultiplayer();
            bodyText.text =
                $"<b>{MsgEnter}</b>\n" +
                $"<size=80%>เดินช้าลง · วิ่ง/กระโดดไม่ได้ · เรียน/ทำงาน/ออกกำลังกายไม่ได้\nยังเปิดโทรศัพท์ กระเป๋า กินอาหาร ซื้อของ และนอนได้ตามปกติ" +
                (mp ? "\n(ผู้เล่นหลายคน: พักตามเวลาโลกที่เดินอยู่ ไม่ข้ามเวลาของทุกคน)" : "") + "</size>" + dormLine;
        }

        void RefreshUI()
        {
            if (!Application.isPlaying) return;
            EnsureUI();
            if (restState == RestState.Resting && IsMultiplayer())
                chipText.text = $"กำลังพัก... {mpRestProgress:0}/{mpRestTarget:0} นาที  <size=80%>(กด {restMenuKey} เพื่อยกเลิก)</size>";
            else if (restState == RestState.Resting)
                chipText.text = "กำลังพัก...";
            else if (IsExhausted)
                chipText.text = $"หมดแรง! เดินช้า · วิ่ง/กระโดดไม่ได้  <size=80%>(กด {restMenuKey} เพื่อพัก)</size>";
        }

        // ---------- UI (สร้างด้วยโค้ด ฟอนต์ไทยเดียวกับ HUD — แบบเดียวกับ SleepController) ----------
        void EnsureUI()
        {
            if (canvasGo != null) return;
            TMP_FontAsset font = null;
            if (HUDController.Instance != null && HUDController.Instance.clockText != null) font = HUDController.Instance.clockText.font;
            if (font == null) { var any = FindFirstObjectByType<TextMeshProUGUI>(); if (any != null) font = any.font; }

            canvasGo = new GameObject("Exhaustion Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 930;   // ใต้หน้านอน (940/950)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f; NisitSimulator.UI.UIFit.Scaler(scaler);   // Expand: ทั้งหน้าอยู่ในจอทุกสัดส่วน

            chip = Panel(canvasGo.transform, "ExhaustedChip", ChipCol, new Vector2(0.5f, 0.9f), new Vector2(0.5f, 0.9f));
            ((RectTransform)chip.transform).sizeDelta = new Vector2(820, 64);
            chip.GetComponent<Image>().raycastTarget = false;
            chipText = Label(chip.transform, "Text", font, 28, new Vector2(12, 4), new Vector2(-12, -4));
            chip.SetActive(false);

            panel = Panel(canvasGo.transform, "ExhaustedPanel", CardCol, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            ((RectTransform)panel.transform).sizeDelta = new Vector2(820, 470);
            bodyText = Label(panel.transform, "Body", font, 32, new Vector2(30, 200), new Vector2(-30, -20));
            hereBtn = Btn(panel.transform, $"พักตรงนี้\n<size=70%>{restHereMinutes:0} นาที · +{restHereRecoverPercent * 100f:0}% · ฟรี</size>", font,
                          new Color(0.36f, 0.62f, 0.95f), new Vector2(0.5f, 0f), new Vector2(-200, 135));
            hereBtn.onClick.AddListener(() => RequestRest(RestKind.Here));
            dormBtn = Btn(panel.transform, $"กลับหอพักเพื่อพักฟื้น\n<size=70%>{dormRestMinutes:0} นาที · +{dormRestRecoverPercent * 100f:0}% · ฟรี</size>", font,
                          new Color(0.40f, 0.72f, 0.50f), new Vector2(0.5f, 0f), new Vector2(200, 135));
            dormBtn.onClick.AddListener(() => RequestRest(RestKind.Dorm));
            closeBtn = Btn(panel.transform, $"ปิด (กินอาหาร / นอนแทน) [{restMenuKey}]", font, new Color(0.45f, 0.47f, 0.55f), new Vector2(0.5f, 0f), new Vector2(0, 45));
            ((RectTransform)closeBtn.transform).sizeDelta = new Vector2(560, 60);
            closeBtn.onClick.AddListener(() => panel.SetActive(false));
            panel.SetActive(false);

            var fadeGo = new GameObject("ExhaustionFade", typeof(RectTransform), typeof(CanvasGroup));
            fadeGo.transform.SetParent(canvasGo.transform, false);
            var frt = (RectTransform)fadeGo.transform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            fade = fadeGo.GetComponent<CanvasGroup>(); fade.alpha = 0f; fade.blocksRaycasts = false; fade.interactable = false;
            var black = Panel(fadeGo.transform, "Black", Color.black, Vector2.zero, Vector2.one);
            fadeImage = black.GetComponent<Image>();
            fadeImage.raycastTarget = true;
            fadeGo.SetActive(false);
        }

        static GameObject Panel(Transform parent, string name, Color c, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = c;
            return go;
        }

        static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, float size, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = offMin; rt.offsetMax = offMax;
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = TextDark; t.alignment = TextAlignmentOptions.Center; t.richText = true; t.raycastTarget = false;
            return t;
        }

        static Button Btn(Transform parent, string label, TMP_FontAsset font, Color c, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = anchor; rt.anchorMax = anchor; rt.sizeDelta = new Vector2(360, 96); rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = c;
            var t = Label(go.transform, "Label", font, 30, Vector2.zero, Vector2.zero);
            t.text = label;
            return go.GetComponent<Button>();
        }
    }
}
