using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.UI
{
    // ประกายแสงลอยขึ้นช้า ๆ ในฉากเมนู (ambient) ให้ดูมีระดับ/พรีเมียม
    // สร้างจุดแสงตอนเล่น แล้วลอยขึ้น-แกว่ง-กะพริบ วนไปเรื่อย ๆ
    public class MenuSparkles : MonoBehaviour
    {
        public Sprite sprite;
        public int count = 16;
        public Color tint = new Color(1f, 0.95f, 0.72f, 1f);
        public float minSize = 6f, maxSize = 18f;
        public float minSpeed = 10f, maxSpeed = 34f;

        // ครึ่งขนาดพื้นที่ (อิง reference resolution 1920x1080)
        const float HX = 980f, HY = 560f;

        private class Mote { public RectTransform rt; public Image img; public float speed, phase, amp; }
        private Mote[] motes;

        void Start()
        {
            motes = new Mote[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("mote", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                float s = Random.Range(minSize, maxSize);
                rt.sizeDelta = new Vector2(s, s);
                rt.anchoredPosition = new Vector2(Random.Range(-HX, HX), Random.Range(-HY, HY));

                var img = go.GetComponent<Image>();
                img.sprite = sprite; img.color = tint; img.raycastTarget = false;

                motes[i] = new Mote {
                    rt = rt, img = img,
                    speed = Random.Range(minSpeed, maxSpeed),
                    phase = Random.Range(0f, 6.2832f),
                    amp = Random.Range(8f, 34f)
                };
            }
        }

        void Update()
        {
            if (motes == null) return;
            float dt = Time.unscaledDeltaTime, tt = Time.unscaledTime;
            foreach (var m in motes)
            {
                var p = m.rt.anchoredPosition;
                p.y += m.speed * dt;
                p.x += Mathf.Sin(tt * 0.8f + m.phase) * m.amp * dt;
                if (p.y > HY) { p.y = -HY; p.x = Random.Range(-HX, HX); }
                m.rt.anchoredPosition = p;

                var c = m.img.color;
                c.a = 0.30f + 0.35f * Mathf.Sin(tt * 1.5f + m.phase);
                m.img.color = c;
            }
        }
    }
}
