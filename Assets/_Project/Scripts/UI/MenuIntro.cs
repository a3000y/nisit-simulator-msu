using UnityEngine;

namespace NisitSimulator.UI
{
    // แอนิเมชันตอนเปิดเมนู: โลโก้เด้งเข้า + องค์ประกอบค่อย ๆ จางขึ้นไล่กัน (ดูมีโปรดักชัน)
    // ใช้ CanvasGroup ปรับ alpha (ไม่ชนกับ parallax ที่ขยับตำแหน่ง หรือ hover ที่ปรับสเกลปุ่ม)
    public class MenuIntro : MonoBehaviour
    {
        [System.Serializable]
        public class Item { public CanvasGroup group; public float delay; }

        public Item[] items;
        public float duration = 0.45f;
        public RectTransform logo;      // โลโก้ที่จะเด้งเข้า (สเกล)
        public float logoFrom = 0.82f;

        private float time;

        void OnEnable()
        {
            time = 0f;
            if (items != null)
                foreach (var it in items)
                    if (it != null && it.group != null) it.group.alpha = 0f;
            if (logo != null) logo.localScale = Vector3.one * logoFrom;
        }

        void Update()
        {
            time += Time.unscaledDeltaTime;

            if (items != null)
                foreach (var it in items)
                {
                    if (it == null || it.group == null) continue;
                    it.group.alpha = Mathf.Clamp01((time - it.delay) / duration);
                }

            if (logo != null)
            {
                if (time <= duration)
                {
                    float p = time / duration;
                    logo.localScale = Vector3.one * Mathf.Lerp(logoFrom, 1f, EaseOutBack(p));
                }
                else
                {
                    // หายใจเบา ๆ หลังเข้าเสร็จ
                    float b = 1f + 0.012f * Mathf.Sin((time - duration) * 1.6f);
                    logo.localScale = Vector3.one * b;
                }
            }
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = 2.70158f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }
    }
}
