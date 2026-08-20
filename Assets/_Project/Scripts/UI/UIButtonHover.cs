using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NisitSimulator.UI
{
    // ปุ่มพรีเมียม: ยกตัว/ย่อตัว + แสงวิ่งกวาดผ่านตอนชี้เมาส์ (+ เรืองแสงถ้ามี glow)
    public class UIButtonHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.06f;
        public float pressScale = 0.97f;
        public float speed = 12f;

        [Header("แสงเรือง (ถ้ามี)")]
        public Graphic glow;
        public float glowRest = 0.16f;
        public float glowHover = 0.60f;

        [Header("แสงวิ่งกวาด (ถ้ามี)")]
        public RectTransform shine;
        public float shineTravel = 240f;
        public float shineDuration = 0.45f;
        public float shinePeak = 0.55f;

        private RectTransform rt;
        private Image shineImg;
        private float target = 1f;
        private bool hovering;
        private float shineT = 999f;   // >duration = ไม่แสดง

        void Awake()
        {
            rt = GetComponent<RectTransform>();
            if (shine != null) shineImg = shine.GetComponent<Image>();
        }

        void OnEnable()
        {
            target = 1f; hovering = false; shineT = 999f;
            if (rt) rt.localScale = Vector3.one;
            SetGlow(glowRest);
            SetShineAlpha(0f);
        }

        void Update()
        {
            float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
            if (rt != null) rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * target, k);

            if (glow != null)
            {
                var col = glow.color;
                col.a = Mathf.Lerp(col.a, hovering ? glowHover : glowRest, k);
                glow.color = col;
            }

            if (shine != null && shineT <= shineDuration)
            {
                shineT += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(shineT / shineDuration);
                var sp = shine.anchoredPosition;
                sp.x = Mathf.Lerp(-shineTravel, shineTravel, p);
                shine.anchoredPosition = sp;
                SetShineAlpha(Mathf.Sin(p * Mathf.PI) * shinePeak);
                if (p >= 1f) SetShineAlpha(0f);
            }
        }

        void SetGlow(float a) { if (glow != null) { var c = glow.color; c.a = a; glow.color = c; } }
        void SetShineAlpha(float a) { if (shineImg != null) { var c = shineImg.color; c.a = a; shineImg.color = c; } }

        public void OnPointerEnter(PointerEventData e) { hovering = true; target = hoverScale; shineT = 0f; }
        public void OnPointerExit(PointerEventData e)  { hovering = false; target = 1f; }
        public void OnPointerDown(PointerEventData e)   { target = pressScale; }
        public void OnPointerUp(PointerEventData e)     { target = hovering ? hoverScale : 1f; }
    }
}
