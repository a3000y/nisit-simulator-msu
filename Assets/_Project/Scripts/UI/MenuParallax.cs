using System;
using UnityEngine;

namespace NisitSimulator.UI
{
    // เลื่อนเลเยอร์ UI ตามตำแหน่งเมาส์คนละระดับ → เกิดความลึกแบบ parallax
    // ติดที่ Menu Canvas (ตัว Editor tool เซ็ต layers ให้อัตโนมัติ)
    public class MenuParallax : MonoBehaviour
    {
        [Serializable]
        public class Layer
        {
            public RectTransform target;
            public float amount = 12f;      // ยิ่งมาก = ขยับมาก (เลเยอร์หน้าควรมากกว่าเลเยอร์หลัง)
            [HideInInspector] public Vector2 home;
        }

        public Layer[] layers = new Layer[0];
        public float smooth = 6f;           // ความนุ่มในการตาม

        void Start()
        {
            foreach (var l in layers)
                if (l != null && l.target != null) l.home = l.target.anchoredPosition;
        }

        void Update()
        {
            // ตำแหน่งเมาส์ -1..1 (กลางจอ = 0)
            Vector2 m = new Vector2(
                Input.mousePosition.x / Mathf.Max(1, Screen.width),
                Input.mousePosition.y / Mathf.Max(1, Screen.height)) * 2f - Vector2.one;
            m = Vector2.ClampMagnitude(m, 1f);

            float k = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);   // lerp แบบ frame-rate independent
            foreach (var l in layers)
            {
                if (l == null || l.target == null) continue;
                Vector2 goal = l.home + new Vector2(-m.x, -m.y) * l.amount;
                l.target.anchoredPosition = Vector2.Lerp(l.target.anchoredPosition, goal, k);
            }
        }
    }
}
