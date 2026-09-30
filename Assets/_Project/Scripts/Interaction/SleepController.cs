using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Core;
using NisitSimulator.TimeSystem;
using NisitSimulator.Player;
using NisitSimulator.UI;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;
using NisitSimulator.Academics.ExamMinigame;

namespace NisitSimulator.Interaction
{
    // ===== ขั้นตอน "นอนพักจนถึง 07:00" =====
    //   ยืนยัน → (MP: รอทุกคนพร้อม) → เฟดดำ → GameClock.SkipToNextMorning (ข้ามวันได้ครั้งเดียว · ยิง OnDayChanged อย่างมาก 1 ครั้ง)
    //   → ฟื้นสถานะ 1 ครั้ง → ยืนข้างเตียง → แสง/HUD อัปเดตทันที → บันทึกเกม (ระบบเซฟเดิม) → เฟดกลับ
    //   กันกดยืนยันซ้ำด้วยสถานะ (state) — ระหว่างนอนกด E/ยืนยันซ้ำจะถูกเมิน
    //   คาบเรียน/กำหนดส่งที่ถูกข้ามระหว่างนอน: ปล่อยให้ระบบเดิมตัดสิน (ไม่นับว่าเข้าเรียน/ทำภารกิจสำเร็จ)
    public class SleepController : MonoBehaviour
    {
        public enum SleepState { Idle, Confirming, WaitingOthers, Sleeping }

        public static SleepController Instance { get; private set; }
        public static bool IsSleeping => Instance != null && Instance.state != SleepState.Idle;

        [Tooltip("เวลาเฟดดำ/เฟดกลับ (วินาทีจริง)")] public float fadeSeconds = 0.45f;
        [Tooltip("ค้างจอดำก่อนข้ามเวลา (วินาทีจริง)")] public float holdBlackSeconds = 0.35f;

        public SleepState State => state;
        public int CompletedSleeps { get; private set; }   // นับจำนวนการนอนที่สำเร็จ (ทดสอบ/กันซ้ำ)
        public string LastResult { get; private set; } = "";

        SleepState state = SleepState.Idle;
        SleepStation station;
        GameObject sleeper;
        float prevTimeScale = 1f;
        PlayerMovement lockedMove;
        int animBaseHash;

        // UI
        GameObject dialog, waitPanel;
        TMP_Text bodyText, waitText;
        Button okBtn;
        CanvasGroup fade;

        public static SleepController EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("SleepController");
            return go.AddComponent<SleepController>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildUI();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (state == SleepState.Confirming) Time.timeScale = prevTimeScale;
        }

        // ---------- ตรวจสถานะที่ห้ามนอน ----------
        public bool CanSleepNow(GameObject who, out string reason)
        {
            reason = "";
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsActive) { reason = "เกมหยุดอยู่"; return false; }
            var mini = ExamMinigameController.Instance;
            if (ExamMinigameController.BlocksWorld || (mini != null && mini.HasSessionInProgress)) { reason = "กำลังสอบอยู่ — นอนไม่ได้"; return false; }
            var exam = FindFirstObjectByType<ExamController>();
            if (exam != null && exam.IsOpen) { reason = "กำลังสอบอยู่ — นอนไม่ได้"; return false; }
            if (Time.timeScale == 0f) { reason = "ปิดหน้าต่างอื่นก่อนนอน"; return false; }
            var act = who != null ? who.GetComponent<PlayerActionController>() : null;
            if (act != null && act.IsBusy) { reason = "กำลังทำกิจกรรมอยู่"; return false; }
            if (FindFirstObjectByType<GameClock>() == null) { reason = "ไม่พบนาฬิกาเกม"; return false; }
            return true;
        }

        // ---------- เริ่ม ----------
        public void RequestSleep(SleepStation st, GameObject who, bool skipConfirm)
        {
            if (state != SleepState.Idle || st == null || who == null) return;   // กำลังยืนยัน/นอนอยู่ → เมิน (กันซ้ำ)
            if (!CanSleepNow(who, out var why)) { HUDController.Toast(why); return; }
            station = st; sleeper = who;
            if (skipConfirm) { state = SleepState.Confirming; prevTimeScale = 1f; Lock(); Confirm(); return; }
            ShowConfirm();
        }

        void ShowConfirm()
        {
            state = SleepState.Confirming;
            Lock();
            prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;   // หยุดโลกระหว่างอ่าน (รูปแบบเดียวกับหน้าต่างอื่นของเกม) → วัน/เวลาที่แสดงตรงกับตอนกดยืนยัน

            var clock = FindFirstObjectByType<GameClock>();
            GameClock.ComputeWake(clock.Day, clock.TotalMinutes, station.wakeHour, out int wakeDay);
            bool tomorrow = wakeDay != clock.Day;
            bodyText.text =
                $"นอนพักจนถึง {station.wakeHour:00}:00\n\n" +
                $"ตอนนี้: วันที่ {clock.Day} เวลา {clock.Hour:00}:{clock.Minute:00} น.\n" +
                $"ตื่น: <b>วันที่ {wakeDay} เวลา {station.wakeHour:00}:00 น.</b> ({(tomorrow ? "พรุ่งนี้" : "วันนี้")})\n\n" +
                "<size=80%>ฟื้นพลังงานและลดความเครียด · คาบเรียน/กำหนดส่งที่เลยไประหว่างนอน จะไม่นับว่าเข้าเรียน</size>";
            okBtn.interactable = true;
            dialog.SetActive(true);
        }

        void Update()
        {
            if (state == SleepState.Confirming && dialog.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Confirm();
                else if (Input.GetKeyDown(KeyCode.Escape)) Cancel();
            }
            else if (state == SleepState.WaitingOthers && Input.GetKeyDown(KeyCode.Escape)) CancelWaiting();
        }

        public void Cancel()
        {
            if (state != SleepState.Confirming) return;
            dialog.SetActive(false);
            Time.timeScale = prevTimeScale;
            Unlock();
            state = SleepState.Idle;
        }

        public void Confirm()
        {
            if (state != SleepState.Confirming) return;   // กดซ้ำ/กดหลังเริ่มนอนแล้ว → เมิน
            okBtn.interactable = false;
            dialog.SetActive(false);
            Time.timeScale = prevTimeScale;

            if (!CanSleepNow(sleeper, out var why)) { HUDController.Toast(why); Unlock(); state = SleepState.Idle; return; }

            // Multiplayer: ต้องรอทุกคนพร้อม Host เป็นผู้ข้ามเวลาโลก (ผู้เล่นแต่ละคนข้ามเวลาเองไม่ได้)
            var sync = NisitSimulator.Net.WorldTimeSync.Instance;
            if (sync != null && sync.IsMultiplayerSession)
            {
                state = SleepState.WaitingOthers;
                waitText.text = "รอผู้เล่นคนอื่นพร้อมนอน...\n<size=75%>กด Esc เพื่อยกเลิก</size>";
                waitPanel.SetActive(true);
                sync.SetSleepReady(true);
                return;
            }
            StartCoroutine(SleepRoutine(true));
        }

        // ---------- Multiplayer ----------
        public void UpdateWaiting(int ready, int total)
        {
            if (state == SleepState.WaitingOthers)
                waitText.text = $"รอผู้เล่นคนอื่นพร้อมนอน... ({ready}/{total})\n<size=75%>กด Esc เพื่อยกเลิก</size>";
        }

        public void CancelWaiting()
        {
            if (state != SleepState.WaitingOthers) return;
            var sync = NisitSimulator.Net.WorldTimeSync.Instance;
            if (sync != null) sync.SetSleepReady(false);
            waitPanel.SetActive(false);
            Unlock();
            state = SleepState.Idle;
        }

        // Host ประกาศว่าทุกคนหลับแล้ว (เวลาโลกถูกข้ามโดย Host และซิงค์มาแล้ว) → ทำส่วนของเครื่องนี้โดยไม่ข้ามนาฬิกาเอง
        public void OnNetworkWake(bool hostSkipsClockHere)
        {
            if (state != SleepState.WaitingOthers) return;
            waitPanel.SetActive(false);
            StartCoroutine(SleepRoutine(hostSkipsClockHere));
        }

        // ---------- ขั้นตอนนอน ----------
        IEnumerator SleepRoutine(bool skipClock)
        {
            state = SleepState.Sleeping;
            Lock();
            SFXManager.Sleep();
            PlaySleepAnim(true);
            yield return Fade(0f, 1f);
            for (float t = 0f; t < holdBlackSeconds; t += Time.unscaledDeltaTime) yield return null;

            var clock = FindFirstObjectByType<GameClock>();
            int dayBefore = clock != null ? clock.Day : 0;
            float minBefore = clock != null ? clock.TotalMinutes : 0f;
            if (skipClock && clock != null) clock.SkipToNextMorning(station.wakeHour);   // ข้ามวันอย่างมาก 1 ครั้ง

            station.ApplyWakeEffects(sleeper);                                           // ฟื้นสถานะ 1 ครั้ง
            PlaySleepAnim(false);
            PlayerSpawnSystem.PlaceInDorm(sleeper, station.WakePoint);                  // ยืนข้างเตียง + กล้องตามทันที
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.ApplyNow();       // แสง/ท้องฟ้าตรงเวลาใหม่ทันที

            SaveManager.Save();   // บันทึกตามระบบเดิม (โหมด Dev = เขียนลงโปรไฟล์ทดสอบ ไม่ทับเซฟจริง)

            CompletedSleeps++;
            LastResult = clock != null
                ? $"นอน วันที่ {dayBefore} {Hhmm(minBefore)} → ตื่น วันที่ {clock.Day} {clock.Hour:00}:{clock.Minute:00}"
                : "นอนแล้ว";
            Debug.Log("[Sleep] " + LastResult);

            Unlock();
            yield return Fade(1f, 0f);
            HUDController.Toast($"หลับสบาย! ตื่น {station.wakeHour:00}:00 น. พลังงานเต็ม ความเครียดลด (แต่หิวแล้ว)");
            state = SleepState.Idle;
        }

        static string Hhmm(float minutes) { int m = Mathf.FloorToInt(minutes); return $"{(m / 60) % 24:00}:{m % 60:00}"; }

        IEnumerator Fade(float from, float to)
        {
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

        void PlaySleepAnim(bool on)
        {
            var anim = sleeper != null ? sleeper.GetComponentInChildren<Animator>() : null;
            if (anim == null || anim.runtimeAnimatorController == null) return;
            if (on)
            {
                animBaseHash = anim.GetCurrentAnimatorStateInfo(0).shortNameHash;
                int h = Animator.StringToHash("Sleeping_A");
                if (anim.HasState(0, h)) anim.CrossFade(h, 0.15f);
            }
            else if (animBaseHash != 0 && anim.HasState(0, animBaseHash)) anim.CrossFade(animBaseHash, 0.1f);
        }

        void Lock()
        {
            if (lockedMove != null) return;
            var mv = sleeper != null ? sleeper.GetComponent<PlayerMovement>() : null;
            if (mv != null && mv.enabled) { mv.enabled = false; lockedMove = mv; }
        }

        void Unlock()
        {
            if (lockedMove != null) lockedMove.enabled = true;
            lockedMove = null;
        }

        // ---------- UI (สร้างด้วยโค้ด ใช้ฟอนต์ไทยเดียวกับ HUD) ----------
        void BuildUI()
        {
            TMP_FontAsset font = null;
            if (HUDController.Instance != null && HUDController.Instance.clockText != null) font = HUDController.Instance.clockText.font;
            if (font == null) { var any = FindFirstObjectByType<TextMeshProUGUI>(); if (any != null) font = any.font; }

            var canGo = new GameObject("Sleep Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canGo.transform.SetParent(transform, false);
            var canvas = canGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 940;
            var scaler = canGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;

            // ---- หน้ายืนยัน ----
            dialog = Panel(canGo.transform, "SleepDialog", new Color(0f, 0f, 0f, 0.45f), Vector2.zero, Vector2.one);
            var card = Panel(dialog.transform, "Card", new Color(0.12f, 0.14f, 0.22f, 0.96f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var crt = (RectTransform)card.transform; crt.sizeDelta = new Vector2(760, 440);
            bodyText = Label(card.transform, "Body", font, 34, new Vector2(40, 130), new Vector2(-40, -30));
            okBtn = Btn(card.transform, "นอนเลย (Enter)", font, new Color(0.36f, 0.62f, 0.95f), new Vector2(0.5f, 0f), new Vector2(-190, 70));
            okBtn.onClick.AddListener(Confirm);
            var cancel = Btn(card.transform, "ยกเลิก (Esc)", font, new Color(0.45f, 0.47f, 0.55f), new Vector2(0.5f, 0f), new Vector2(190, 70));
            cancel.onClick.AddListener(Cancel);
            dialog.SetActive(false);

            // ---- รอผู้เล่นอื่น (Multiplayer) ----
            waitPanel = Panel(canGo.transform, "SleepWait", new Color(0.12f, 0.14f, 0.22f, 0.92f), new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f));
            ((RectTransform)waitPanel.transform).sizeDelta = new Vector2(640, 130);
            waitText = Label(waitPanel.transform, "Text", font, 30, new Vector2(20, 10), new Vector2(-20, -10));
            waitPanel.SetActive(false);

            // ---- จอดำ ----
            var fadeGo = new GameObject("SleepFade", typeof(Canvas), typeof(CanvasGroup));
            fadeGo.transform.SetParent(transform, false);
            var fc = fadeGo.GetComponent<Canvas>(); fc.renderMode = RenderMode.ScreenSpaceOverlay; fc.sortingOrder = 950;
            fade = fadeGo.GetComponent<CanvasGroup>(); fade.alpha = 0f; fade.blocksRaycasts = false; fade.interactable = false;
            var black = Panel(fadeGo.transform, "Black", Color.black, Vector2.zero, Vector2.one);
            black.GetComponent<Image>().raycastTarget = true;
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
            t.fontSize = size; t.color = Color.white; t.alignment = TextAlignmentOptions.Center; t.richText = true; t.raycastTarget = false;
            return t;
        }

        static Button Btn(Transform parent, string label, TMP_FontAsset font, Color c, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = anchor; rt.anchorMax = anchor; rt.sizeDelta = new Vector2(320, 72); rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = c;
            var t = Label(go.transform, "Label", font, 30, Vector2.zero, Vector2.zero);
            t.text = label;
            return go.GetComponent<Button>();
        }
    }
}
