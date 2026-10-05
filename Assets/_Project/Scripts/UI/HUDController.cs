using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.Systems;

namespace NisitSimulator.UI
{
    // ควบคุมหน้าจอ HUD — ฟัง event จาก PlayerStats / GameClock / ProgressionManager
    // ตัวอ้างอิง UI ถูกเซ็ตอัตโนมัติโดย Editor tool
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        [Header("แถบสถานะ (Image แบบ Filled)")]
        public Image energyFill;
        public Image healthFill;
        public Image hungerFill;
        public Image stressFill;      // แถบความเครียด (เต็ม = แย่ ต่างจากแถบอื่น)
        public Image knowledgeFill;   // แถบความคืบหน้าความรู้ (เทียบเป้าปี)

        [Header("ตัวเลขบนแถบ")]
        public TMP_Text energyText;
        public TMP_Text healthText;
        public TMP_Text hungerText;
        public TMP_Text stressText;

        [Header("ข้อความ")]
        public TMP_Text clockText;
        public TMP_Text dayText;
        public TMP_Text yearText;
        public TMP_Text moneyText;
        public TMP_Text knowledgeText;
        public TMP_Text toastText;    // ข้อความแจ้งเตือนชั่วคราว
        public TMP_Text promptText;   // ป้าย "กด E เพื่อ..." เมื่อเข้าใกล้
        public GameObject promptBg;   // พื้นหลังป้าย (ซ่อนเมื่อไม่มีป้าย)

        private PlayerStats stats;
        private GameClock clock;
        private ProgressionManager progression;
        private float knowledgeTarget = 100f;

        void Awake()
        {
            Instance = this;
            if (toastText != null) toastText.text = "";
        }

        void Start()
        {
            stats = Object.FindFirstObjectByType<PlayerStats>();
            clock = Object.FindFirstObjectByType<GameClock>();
            progression = Object.FindFirstObjectByType<ProgressionManager>();
            if (dayText == null && clockText != null)
            {
                var canvas = clockText.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    var card = GrowthUI.Box(canvas.transform, "WeekdayChip", new Vector2(1, 1), new Vector2(-24, -220), new Vector2(452, 50), GrowthUI.CardCol);
                    dayText = GrowthUI.Text(card.transform, "", Vector2.zero, new Vector2(420, 44), 24, GrowthUI.Ink, TextAlignmentOptions.Center);
                    dayText.font = clockText.font;
                    UIFit.OneLine(dayText, 24, 22);
                }
            }

            if (stats != null)
            {
                stats.OnEnergyChanged    += UpdateEnergy;
                stats.OnHealthChanged    += UpdateHealth;
                stats.OnHungerChanged    += UpdateHunger;
                stats.OnMoneyChanged     += UpdateMoney;
                stats.OnKnowledgeChanged += UpdateKnowledge;
                stats.OnStressChanged    += UpdateStress;
            }
            if (clock != null)
            {
                clock.OnTimeChanged += UpdateClock;
                clock.OnDayChanged  += UpdateDay;
            }
            if (progression != null)
            {
                progression.OnYearChanged += UpdateYear;
                progression.OnDayInYearChanged += UpdateCalendarDay;
                knowledgeTarget = progression.CurrentTarget;
            }

            RefreshFromStats();
            if (clock != null) UpdateClock(clock.Hour, clock.Minute);
            UpdateCalendarDay(progression != null ? progression.DayInYear : 1, AcademicCalendar.TotalDays);
            StyleBarTexts();
            StressStatusHUD.Attach(this);   // ป้ายช่วงความเครียดใต้แถบ + แจ้งเตือนเมื่อข้ามช่วง (สร้างตอนรัน)
            SetupToastStyle();
            if (toastText != null) toastText.text = "";
            SetPrompt("");
        }

        // ---------- ป้าย "กด E เพื่อ..." (เรียกจาก PlayerInteraction) ----------
        public static void Prompt(string msg)
        {
            if (Instance != null) Instance.SetPrompt(msg);
        }

        private void SetPrompt(string msg)
        {
            bool has = !string.IsNullOrEmpty(msg);
            bool changed = promptText != null && promptText.text != msg;
            if (promptText != null) promptText.text = msg;
            if (promptBg != null) promptBg.SetActive(has);
            if (!has || !changed || promptText == null || promptBg == null) return;

            // ป้ายโต้ตอบยาวใช้กรอบตามข้อความแทนความกว้างคงที่ 520
            if (promptText.transform.parent != promptBg.transform)
                promptText.transform.SetParent(promptBg.transform, false);
            var rt = promptText.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(24f, 12f); rt.offsetMax = new Vector2(-24f, -12f);
            promptText.fontSize = 28f; promptText.enableAutoSizing = false;
            promptText.textWrappingMode = TextWrappingModes.Normal;
            promptText.overflowMode = TextOverflowModes.Overflow;
            promptText.alignment = TextAlignmentOptions.Center;
            float width = UIFit.ClampWidth(promptText.GetPreferredValues(msg, 99999f, 0f).x, 24f, 520f, 1100f);
            float height = promptText.GetPreferredValues(msg, width - 48f, 0f).y;
            ((RectTransform)promptBg.transform).sizeDelta = new Vector2(width, Mathf.Max(64f, height + 24f));
        }

        void OnDestroy()
        {
            if (stats != null)
            {
                stats.OnEnergyChanged    -= UpdateEnergy;
                stats.OnHealthChanged    -= UpdateHealth;
                stats.OnHungerChanged    -= UpdateHunger;
                stats.OnMoneyChanged     -= UpdateMoney;
                stats.OnKnowledgeChanged -= UpdateKnowledge;
                stats.OnStressChanged    -= UpdateStress;
            }
            if (clock != null)
            {
                clock.OnTimeChanged -= UpdateClock;
                clock.OnDayChanged  -= UpdateDay;
            }
            if (progression != null) { progression.OnYearChanged -= UpdateYear; progression.OnDayInYearChanged -= UpdateCalendarDay; }
            if (Instance == this) Instance = null;
        }

        private void RefreshFromStats()
        {
            if (stats == null) return;
            UpdateEnergy(stats.Energy, stats.maxEnergy);
            UpdateHealth(stats.Health, stats.maxHealth);
            UpdateHunger(stats.Hunger, stats.maxHunger);
            UpdateMoney(stats.Money);
            UpdateKnowledge(stats.Knowledge);
            UpdateYear(progression != null ? progression.CurrentYear : 1,
                progression != null ? progression.CurrentTarget : knowledgeTarget);
        }

        [Header("Toast Card (พื้นหลังแจ้งเตือน)")]
        public GameObject toastBg;

        void SetupToastStyle()
        {
            if (toastText == null) return;

            toastText.color = new Color(0.27f, 0.22f, 0.42f);   // หมึกม่วงเข้ม บนการ์ดสีอ่อน (โทนเดียวกับป้าย "กด E")
            toastText.fontSize = ToastFont;
            toastText.fontStyle = FontStyles.Bold;
            toastText.alignment = TextAlignmentOptions.Center;

            if (toastBg == null)
            {
                var bgObj = new GameObject("ToastBg", typeof(RectTransform), typeof(Image));
                bgObj.transform.SetParent(toastText.transform.parent, false);
                bgObj.transform.SetSiblingIndex(toastText.transform.GetSiblingIndex());

                var bgImg = bgObj.GetComponent<Image>();
                bgImg.color = new Color(0.12f, 0.14f, 0.22f, 0.88f); // สีกรมท่าเข้มหรูหรา

                var rt = bgObj.GetComponent<RectTransform>();
                rt.anchorMin = toastText.rectTransform.anchorMin;
                rt.anchorMax = toastText.rectTransform.anchorMax;
                rt.pivot = toastText.rectTransform.pivot;
                rt.anchoredPosition = toastText.rectTransform.anchoredPosition;
                rt.sizeDelta = new Vector2(580, 68);

                toastText.transform.SetParent(bgObj.transform, true);
                var trt = toastText.rectTransform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;

                toastBg = bgObj;
            }

            if (toastBg != null)
            {
                var bgImg = toastBg.GetComponent<Image>();
                if (bgImg != null) bgImg.color = new Color(0.965f, 0.945f, 0.99f, 0.97f);
                if (toastBg.GetComponent<Outline>() == null && toastBg.GetComponent<Shadow>() == null)
                {
                    var ol = toastBg.AddComponent<Outline>();
                    ol.effectColor = new Color(0.16f, 0.13f, 0.26f, 0.9f); ol.effectDistance = new Vector2(3f, -3f);
                }
                toastBg.SetActive(false);
            }
        }

        // ---------- แสดงข้อความแจ้งเตือน (เรียกจากที่ไหนก็ได้) ----------
        public static void Toast(string msg)
        {
            if (Instance != null) Instance.ShowToast(msg);
            NisitSimulator.Core.SFXManager.Notify();
            Debug.Log($"[Toast] {msg}");
        }

        private void ShowToast(string msg)
        {
            if (toastText == null) return;
            bool has = !string.IsNullOrEmpty(msg);
            toastText.text = msg;
            if (!toastText.gameObject.activeSelf) toastText.gameObject.SetActive(true);   // กันกล่องแจ้งเตือนว่าง (ข้อความถูกปิดไว้ในฉาก)
            if (has) FitToast(msg);
            if (toastBg != null) toastBg.SetActive(has);
            CancelInvoke(nameof(ClearToast));
            // ข้อความยาวอยู่นานขึ้นเล็กน้อย (อ่านทัน)
            if (has) Invoke(nameof(ClearToast), Mathf.Clamp(2.5f + msg.Length / 40f, 2.5f, 5f));
        }

        // ===== กล่องแจ้งเตือนกว้างตามข้อความ (ไม่ล้นกรอบ) =====
        //   กว้าง 420–1000 (หน่วยอ้างอิง 1920×1080) · ยาวกว่านั้นขึ้นบรรทัดใหม่ สูงสุด 2 บรรทัด (เกินนั้นย่อตัวอักษร)
        public const float ToastMinW = 420f, ToastMaxW = 1000f, ToastPadX = 30f, ToastPadY = 14f;
        public const float ToastFont = 28f;

        void FitToast(string msg)
        {
            if (toastBg == null) return;
            var bgRt = (RectTransform)toastBg.transform;
            if (toastText.transform.parent != toastBg.transform)
            {
                // ฉากเดิม: ข้อความไม่ได้อยู่ในกล่อง → ย้ายเข้าไปให้ขยายพร้อมกัน (ตอนรันเท่านั้น ไม่แก้ฉาก)
                toastText.transform.SetParent(toastBg.transform, false);
            }
            var trt = toastText.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f);
            trt.offsetMin = new Vector2(ToastPadX, ToastPadY); trt.offsetMax = new Vector2(-ToastPadX, -ToastPadY);
            toastText.textWrappingMode = TextWrappingModes.Normal;
            toastText.overflowMode = TextOverflowModes.Overflow;
            toastText.enableAutoSizing = false;
            toastText.fontSize = ToastFont;
            toastText.alignment = TextAlignmentOptions.Center;

            float innerMax = ToastMaxW - ToastPadX * 2f;
            Vector2 one = toastText.GetPreferredValues(msg, 99999f, 0f);
            float w = UIFit.ClampWidth(one.x, ToastPadX, ToastMinW, ToastMaxW);
            int lines = UIFit.LineCount(one.x, innerMax);
            if (lines > 2)
            {
                // ยาวมาก → ย่อตัวอักษรให้อยู่ใน 2 บรรทัด (ไม่ต่ำกว่า 20)
                toastText.fontSize = Mathf.Max(20f, ToastFont * 2f * innerMax / Mathf.Max(1f, one.x));
                lines = 2;
            }
            float h = toastText.GetPreferredValues(msg, Mathf.Min(innerMax, w - ToastPadX * 2f), 0f).y;
            bgRt.sizeDelta = new Vector2(w, Mathf.Max(64f, h + ToastPadY * 2f));
        }

        // ชื่อ/ตัวเลขบนแถบสถานะ: ขอบสีอ่อนรอบตัวอักษร → อ่านได้ทั้งตอนแถบเต็ม (สีเข้ม) และแถบว่าง (พื้นอ่อน)
        void StyleBarTexts()
        {
            foreach (var fill in new[] { energyFill, healthFill, hungerFill, stressFill })
            {
                if (fill == null || fill.transform.parent == null) continue;
                foreach (var t in fill.transform.parent.GetComponentsInChildren<TMP_Text>(true))
                {
                    UIFit.Outline(t, new Color(1f, 1f, 1f, 0.85f), 0.22f);
                    t.fontStyle |= FontStyles.Bold;
                }
            }
        }

        public void ClearToast()
        {
            if (toastText != null) toastText.text = "";
            if (toastBg != null) toastBg.SetActive(false);
        }

        // แถบวิ่งนุ่ม: เก็บค่าเป้า แล้วค่อย ๆ ไล่ใน Update (ไม่กระตุก)
        private float energyT = 1f, healthT = 1f, hungerT = 1f, knowT = -1f, stressT = 0f;

        void Update()
        {
            float k = 1f - Mathf.Exp(-10f * Time.deltaTime);
            if (energyFill) energyFill.fillAmount = Mathf.Lerp(energyFill.fillAmount, energyT, k);
            if (healthFill) healthFill.fillAmount = Mathf.Lerp(healthFill.fillAmount, healthT, k);
            if (hungerFill) hungerFill.fillAmount = Mathf.Lerp(hungerFill.fillAmount, hungerT, k);
            if (stressFill) stressFill.fillAmount = Mathf.Lerp(stressFill.fillAmount, stressT, k);
            if (knowledgeFill && knowT >= 0f) knowledgeFill.fillAmount = Mathf.Lerp(knowledgeFill.fillAmount, knowT, k);
        }

        // ---------- callback อัปเดต UI ----------
        private void UpdateEnergy(float cur, float max) { energyT = max > 0 ? cur / max : 0f; if (energyText) energyText.text = $"{cur:0}"; }
        private void UpdateHealth(float cur, float max) { healthT = max > 0 ? cur / max : 0f; if (healthText) healthText.text = $"{cur:0}"; }
        private void UpdateHunger(float cur, float max) { hungerT = max > 0 ? cur / max : 0f; if (hungerText) hungerText.text = $"{cur:0}"; }
        // ความเครียดอ่านกลับกับแถบอื่น — แถบเต็มคือแย่ จึงไม่กลับด้านสัดส่วน
        private void UpdateStress(float cur, float max) { stressT = max > 0 ? cur / max : 0f; if (stressText) stressText.text = $"{cur:0}"; }

        private void UpdateMoney(int money) { if (moneyText) moneyText.text = $"฿ {money}"; }

        private void UpdateKnowledge(float know)
        {
            if (knowledgeText) knowledgeText.text = $"EXP {know:0}/{knowledgeTarget:0}";
            knowT = knowledgeTarget > 0f ? Mathf.Clamp01(know / knowledgeTarget) : 0f;
        }

        private void UpdateClock(int hour, int minute) { if (clockText) clockText.text = clock.GetTimeString(); }
        private void UpdateCalendarDay(int day, int total) => UpdateDay(day);
        private void UpdateDay(int day) { if (dayText) dayText.text = AcademicCalendar.DateText(progression != null ? progression.DayInYear : 1); }

        private void UpdateYear(int year, float target)
        {
            knowledgeTarget = target;
            if (yearText) yearText.text = $"ปี {year}";
            if (stats != null) UpdateKnowledge(stats.Knowledge); // อัปเดตเป้าใหม่
        }
    }
}
