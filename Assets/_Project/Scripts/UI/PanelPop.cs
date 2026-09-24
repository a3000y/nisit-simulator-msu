using System.Collections;
using UnityEngine;

namespace NisitSimulator.UI
{
    // แผงเด้งเข้าแบบนุ่ม ๆ (fade + การ์ดขยายจาก 0.92→1) แทนการตัดจบดิบ ๆ
    //   ใส่บน "แผง dim" ที่มีลูกชื่อ Card · unscaled → ทำงานตอน pause ได้ · ติดอัตโนมัติโดย UIJuiceInstaller
    [DisallowMultipleComponent]
    public class PanelPop : MonoBehaviour
    {
        public float dur = 0.16f;
        CanvasGroup cg;
        Transform card;
        Vector3 cardBase = Vector3.one;

        void Awake()
        {
            cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            card = transform.Find("Card");
            if (card != null) cardBase = card.localScale;
        }

        void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            float e = 0f;
            if (cg != null) cg.alpha = 0f;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / dur);
                float s = 1f - Mathf.Pow(1f - k, 3f);   // ease-out
                if (cg != null) cg.alpha = s;
                if (card != null) card.localScale = Vector3.LerpUnclamped(cardBase * 0.92f, cardBase, s);
                yield return null;
            }
            if (cg != null) cg.alpha = 1f;
            if (card != null) card.localScale = cardBase;
        }
    }
}
