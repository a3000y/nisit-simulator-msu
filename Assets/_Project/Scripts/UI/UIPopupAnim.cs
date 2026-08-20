using UnityEngine;

namespace NisitSimulator.UI
{
    // แอนิเมชันเด้งเข้าเวลาป็อปอัปโผล่: การ์ด scale จากเล็ก→ปกติ (ease-out-back) + จอจางเข้า
    // ใช้ unscaledTime เพราะป็อปอัป (สอบ/เหตุการณ์) หยุดเวลาเกม (timeScale = 0)
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPopupAnim : MonoBehaviour
    {
        public RectTransform scaleTarget;   // การ์ดที่จะเด้ง (ปล่อยว่าง = ตัวเอง)
        public float from = 0.82f;
        public float duration = 0.28f;

        private CanvasGroup cg;
        private float t;

        void Awake() { cg = GetComponent<CanvasGroup>(); }

        void OnEnable()
        {
            t = 0f;
            if (cg != null) cg.alpha = 0f;
            var s = scaleTarget != null ? scaleTarget : (RectTransform)transform;
            if (s != null) s.localScale = Vector3.one * from;
        }

        void Update()
        {
            if (t >= duration) return;
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            if (cg != null) cg.alpha = p;
            var s = scaleTarget != null ? scaleTarget : (RectTransform)transform;
            if (s != null) s.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, EaseOutBack(p));
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = 2.70158f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }
    }
}
