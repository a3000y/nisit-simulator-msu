using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Stats;

namespace NisitSimulator.UI
{
    // ===== ป้ายสถานะความเครียดใต้แถบเครียดบน HUD + แจ้งเตือนเมื่อข้ามช่วง =====
    //   สร้างตอนรัน (HUDController.Start ใส่ให้) — ไม่แก้ฉาก
    //   ป้าย: จุดสี + "ตึงตัว · ความรู้ +5%" · สีตามช่วง (เขียว/เหลือง/ส้ม/แดง) · เครียดจัด = กะพริบเบา ๆ · แถบเครียดเปลี่ยนสีตามช่วง
    //   แจ้งเตือน: ขึ้น/ลงช่วง (hysteresis 3 แต้ม) · ต้องคงอยู่ ≥ 2.5 วินาที · ห่างกันอย่างน้อย 45 วินาทีจริง
    //              ไม่แจ้งตอนเพิ่งเข้าเกม/โหลดเซฟ หรือตอนหน้าต่างหยุดเวลา/กำลังนอน (เลื่อนไปแจ้งทีหลัง)
    public class StressStatusHUD : MonoBehaviour
    {
        public const float ToastCooldown = 45f;
        public const float SettleSeconds = 2.5f;
        public const float StartupQuietSeconds = 2f;

        static readonly Color BgCol = new Color(0.12f, 0.12f, 0.22f, 0.82f);   // พื้นเข้มทึบพอ ตัวอักษรสีอ่านชัด

        HUDController hud;
        PlayerStats stats;
        RectTransform root;
        Image bg, dot;
        TMP_Text label;

        StressBand shown = StressBand.Calm;      // ช่วงที่แสดงอยู่ (มี hysteresis)
        StressBand announced = StressBand.Calm;  // ช่วงที่แจ้งเตือนไปล่าสุด
        bool initialized;
        float startTime, changedAt = -999f, lastToastAt = -999f;
        float stress;

        public static StressStatusHUD Instance { get; private set; }
        public StressBand ShownBand => shown;
        public string LabelText => label != null ? label.text : "";
        public static string LastToast { get; private set; }

        public static StressStatusHUD Attach(HUDController h)
        {
            if (h == null || h.stressFill == null) return null;
            var s = h.GetComponent<StressStatusHUD>();
            if (s == null) s = h.gameObject.AddComponent<StressStatusHUD>();
            return s;
        }

        void Awake() { Instance = this; hud = GetComponent<HUDController>(); startTime = Time.unscaledTime; }

        void Start()
        {
            stats = Object.FindFirstObjectByType<PlayerStats>();
            if (stats != null) stats.OnStressChanged += OnStress;
            Build();
            if (stats != null) OnStress(stats.Stress, stats.maxStress);
        }

        void OnDestroy()
        {
            if (stats != null) stats.OnStressChanged -= OnStress;
            if (Instance == this) Instance = null;
        }

        void OnStress(float cur, float max)
        {
            stress = cur;
            var b = initialized ? StressBands.BandWithHysteresis(shown, cur) : StressBands.BandOf(cur);
            if (b != shown) { shown = b; changedAt = Time.unscaledTime; }
            Refresh();
        }

        void Build()
        {
            if (hud == null || hud.stressFill == null) return;
            var slot = hud.stressFill.transform.parent as RectTransform;   // StressSlot
            var canvas = slot != null ? slot.parent : null;
            if (canvas == null) return;

            var go = new GameObject("StressStatus", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas, false);
            root = (RectTransform)go.transform;
            root.anchorMin = slot.anchorMin; root.anchorMax = slot.anchorMax; root.pivot = new Vector2(0f, 1f);
            // ใต้แถบเครียด ชิดซ้ายเท่าแถบ
            root.anchoredPosition = slot.anchoredPosition + new Vector2(0f, -slot.sizeDelta.y - 4f);
            root.sizeDelta = new Vector2(slot.sizeDelta.x, 32f);
            bg = go.GetComponent<Image>();
            bg.color = BgCol;
            bg.raycastTarget = false;
            if (GrowthUI.Round != null) { bg.sprite = GrowthUI.Round; bg.type = Image.Type.Sliced; }

            var d = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            d.transform.SetParent(root, false);
            var drt = (RectTransform)d.transform;
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = new Vector2(14f, 0f);
            drt.sizeDelta = new Vector2(12f, 12f);
            drt.localRotation = Quaternion.Euler(0f, 0f, 45f);   // ข้าวหลามตัด
            dot = d.GetComponent<Image>();
            dot.raycastTarget = false;

            var t = new GameObject("Text", typeof(RectTransform));
            t.transform.SetParent(root, false);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(26f, 0f); trt.offsetMax = new Vector2(-6f, 0f);
            label = t.AddComponent<TextMeshProUGUI>();
            if (hud.stressText != null && hud.stressText.font != null) label.font = hud.stressText.font;
            UIFit.OneLine(label, 21f, 16f);
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            Refresh();
        }

        void Refresh()
        {
            if (label == null) return;
            var col = StressBands.ColorOf(shown);
            label.text = $"{StressBands.NameOf(shown)} · {StressBands.EffectText(stress)}";
            label.color = Color.Lerp(col, Color.white, 0.45f);
            dot.color = col;
            if (hud != null && hud.stressFill != null) hud.stressFill.color = col;
        }

        static bool WindowBlocking()
        {
            return Time.timeScale == 0f || DaySummaryUI.IsShowing || TermResultUI.IsOpen
                   || NisitSimulator.Interaction.SleepController.IsSleeping;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            // ช่วงเข้าเกม/โหลดเซฟ: ตั้งค่าเริ่มเงียบ ๆ ไม่แจ้งเตือน
            if (!initialized)
            {
                if (now - startTime < StartupQuietSeconds) { announced = shown; return; }
                initialized = true;
                announced = shown;
            }

            // เครียดจัด → กะพริบเบา ๆ
            if (dot != null)
            {
                float s = shown == StressBand.Severe ? 1f + 0.25f * Mathf.Sin(now * 5f) : 1f;
                dot.rectTransform.localScale = new Vector3(s, s, 1f);
                if (bg != null) bg.color = shown == StressBand.Severe
                    ? new Color(0.45f, 0.08f, 0.12f, 0.45f + 0.2f * (0.5f + 0.5f * Mathf.Sin(now * 5f)))
                    : BgCol;
            }

            if (shown == announced) return;
            if (now - changedAt < SettleSeconds) return;            // รอให้ค่านิ่ง (และให้ข้อความตื่นนอน/อื่น ๆ แสดงก่อน)
            if (now - lastToastAt < ToastCooldown) return;
            if (WindowBlocking()) return;
            LastToast = StressBands.CrossingMessage(announced, shown, stress);
            announced = shown;
            lastToastAt = now;
            HUDController.Toast(LastToast);
        }
    }
}
