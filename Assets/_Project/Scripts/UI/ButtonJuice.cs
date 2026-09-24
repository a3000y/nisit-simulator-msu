using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NisitSimulator.UI
{
    // ให้ปุ่มมีชีวิต: hover = ยืดขึ้นนิด, กด = หดลง, นุ่ม ๆ (unscaled → ทำงานตอน pause ด้วย)
    //   ติดอัตโนมัติโดย UIJuiceInstaller (เฉพาะปุ่มใหญ่ ไม่ชนกับปุ่มที่ไฮไลต์ด้วย scale เอง)
    public class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hover = 1.05f, press = 0.92f, speed = 16f;

        Transform t;
        Vector3 baseScale = Vector3.one;
        bool hovering, pressing;
        Coroutine co;

        void Awake() { t = transform; baseScale = t.localScale; }

        void OnDisable()
        {
            if (co != null) { StopCoroutine(co); co = null; }
            hovering = pressing = false;
            if (t != null) t.localScale = baseScale;
        }

        public void OnPointerEnter(PointerEventData e) { if (!hovering && !pressing) baseScale = t.localScale; hovering = true; Kick(); }
        public void OnPointerExit(PointerEventData e)  { hovering = false; pressing = false; Kick(); }
        public void OnPointerDown(PointerEventData e)  { pressing = true; Kick(); }
        public void OnPointerUp(PointerEventData e)    { pressing = false; Kick(); }

        void Kick() { if (co == null) co = StartCoroutine(Run()); }

        IEnumerator Run()
        {
            while (true)
            {
                float m = pressing ? press : (hovering ? hover : 1f);
                Vector3 target = baseScale * m;
                t.localScale = Vector3.Lerp(t.localScale, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
                if ((t.localScale - target).sqrMagnitude < 1e-6f)
                {
                    t.localScale = target;
                    if (!hovering && !pressing) { co = null; yield break; }   // นิ่งแล้ว + ไม่โดนอยู่ → หยุด (ไม่กวน scale เดิม)
                }
                yield return null;
            }
        }
    }
}
