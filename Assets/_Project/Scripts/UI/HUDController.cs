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
        public Image knowledgeFill;   // แถบความคืบหน้าความรู้ (เทียบเป้าปี)

        [Header("ตัวเลขบนแถบ")]
        public TMP_Text energyText;
        public TMP_Text healthText;
        public TMP_Text hungerText;

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

        void Awake() => Instance = this;

        void Start()
        {
            stats = Object.FindFirstObjectByType<PlayerStats>();
            clock = Object.FindFirstObjectByType<GameClock>();
            progression = Object.FindFirstObjectByType<ProgressionManager>();

            if (stats != null)
            {
                stats.OnEnergyChanged    += UpdateEnergy;
                stats.OnHealthChanged    += UpdateHealth;
                stats.OnHungerChanged    += UpdateHunger;
                stats.OnMoneyChanged     += UpdateMoney;
                stats.OnKnowledgeChanged += UpdateKnowledge;
            }
            if (clock != null)
            {
                clock.OnTimeChanged += UpdateClock;
                clock.OnDayChanged  += UpdateDay;
            }
            if (progression != null)
            {
                progression.OnYearChanged += UpdateYear;
                knowledgeTarget = progression.CurrentTarget;
            }

            RefreshFromStats();
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
            if (promptText != null) promptText.text = msg;
            if (promptBg != null) promptBg.SetActive(has);
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
            }
            if (clock != null)
            {
                clock.OnTimeChanged -= UpdateClock;
                clock.OnDayChanged  -= UpdateDay;
            }
            if (progression != null) progression.OnYearChanged -= UpdateYear;
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
            if (progression != null) UpdateYear(progression.CurrentYear, progression.CurrentTarget);
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
            toastText.text = msg;
            CancelInvoke(nameof(ClearToast));
            Invoke(nameof(ClearToast), 2.5f);
        }

        private void ClearToast() { if (toastText != null) toastText.text = ""; }

        // ---------- callback อัปเดต UI ----------
        private void UpdateEnergy(float cur, float max) { if (energyFill) energyFill.fillAmount = cur / max; if (energyText) energyText.text = $"{cur:0}"; }
        private void UpdateHealth(float cur, float max) { if (healthFill) healthFill.fillAmount = cur / max; if (healthText) healthText.text = $"{cur:0}"; }
        private void UpdateHunger(float cur, float max) { if (hungerFill) hungerFill.fillAmount = cur / max; if (hungerText) hungerText.text = $"{cur:0}"; }

        private void UpdateMoney(int money) { if (moneyText) moneyText.text = $"฿ {money}"; }

        private void UpdateKnowledge(float know)
        {
            if (knowledgeText) knowledgeText.text = $"ความรู้ {know:0}/{knowledgeTarget:0}";
            if (knowledgeFill) knowledgeFill.fillAmount = knowledgeTarget > 0f ? Mathf.Clamp01(know / knowledgeTarget) : 0f;
        }

        private void UpdateClock(int hour, int minute) { if (clockText) clockText.text = clock.GetTimeString(); }
        private void UpdateDay(int day) { if (dayText) dayText.text = $"วันที่ {day}"; }

        private void UpdateYear(int year, float target)
        {
            knowledgeTarget = target;
            if (yearText) yearText.text = $"ปี {year}";
            if (stats != null) UpdateKnowledge(stats.Knowledge); // อัปเดตเป้าใหม่
        }
    }
}
