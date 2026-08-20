using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.Systems
{
    // เอฟเฟกต์อากาศตามฤดู: ฤดูฝน = ฝนตก, ฤดูหนาว = เกล็ด/ละอองลอย, ฤดูร้อน = ละอองแดดอุ่นลอยขึ้น
    // สร้างอนุภาคเป็น UI แล้วเลื่อน-วน (ใช้ unscaledTime เผื่อป็อปอัปหยุดเวลา)
    public class WeatherOverlay : MonoBehaviour
    {
        public Sprite sprite;
        const float HX = 980f, HY = 560f;

        private class P { public RectTransform rt; public float vy, sway, phase; }
        private P[] parts = new P[0];
        private ProgressionManager prog;
        private int mode = -1;

        void Start()
        {
            prog = Object.FindFirstObjectByType<ProgressionManager>();
            if (prog != null)
            {
                prog.OnDayInYearChanged += OnDay;
                SetMode(AcademicCalendar.SemesterIndex(prog.DayInYear));
            }
        }

        void OnDestroy() { if (prog != null) prog.OnDayInYearChanged -= OnDay; }
        void OnDay(int dayInYear, int daysPerYear) => SetMode(AcademicCalendar.SemesterIndex(dayInYear));

        void SetMode(int m)
        {
            if (m == mode) return;
            mode = m;
            Rebuild();
        }

        void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

            int count; Color col; Vector2 size; float vMin, vMax, swayAmp; int dir;
            switch (mode)
            {
                case 0:  count = 60; col = new Color(0.62f, 0.76f, 0.96f, 0.5f);  size = new Vector2(4, 42);   vMin = 900; vMax = 1300; dir = -1; swayAmp = 8;  break;   // ฝน
                case 1:  count = 34; col = new Color(0.90f, 0.94f, 1.00f, 0.6f);  size = new Vector2(11, 11);  vMin = 110; vMax = 240;  dir = -1; swayAmp = 45; break;   // หนาว
                default: count = 18; col = new Color(1.00f, 0.85f, 0.5f, 0.35f);  size = new Vector2(13, 13);  vMin = 40;  vMax = 90;   dir = 1;  swayAmp = 30; break;   // ร้อน
            }

            parts = new P[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("wx", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = size;
                rt.anchoredPosition = new Vector2(Random.Range(-HX, HX), Random.Range(-HY, HY));

                var img = go.GetComponent<Image>();
                img.sprite = sprite; img.color = col; img.raycastTarget = false;

                parts[i] = new P { rt = rt, vy = dir * Random.Range(vMin, vMax), sway = swayAmp, phase = Random.Range(0f, 6.2832f) };
            }
        }

        void Update()
        {
            if (parts == null) return;
            float dt = Time.unscaledDeltaTime, tt = Time.unscaledTime;
            foreach (var p in parts)
            {
                if (p == null || p.rt == null) continue;
                var pos = p.rt.anchoredPosition;
                pos.y += p.vy * dt;
                pos.x += Mathf.Sin(tt * 1.4f + p.phase) * p.sway * dt;
                if (p.vy < 0 && pos.y < -HY) { pos.y = HY; pos.x = Random.Range(-HX, HX); }
                else if (p.vy > 0 && pos.y > HY) { pos.y = -HY; pos.x = Random.Range(-HX, HX); }
                p.rt.anchoredPosition = pos;
            }
        }
    }
}
