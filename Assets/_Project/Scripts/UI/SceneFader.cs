using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace NisitSimulator.UI
{
    // เฟดดำ→ใส ทุกครั้งที่โหลดฉาก (เข้า/ออกลื่นขึ้น ไม่ตัดวับ) — persistent, สร้างเองตอนเปิดแอป
    public class SceneFader : MonoBehaviour
    {
        static SceneFader _i;
        CanvasGroup cg;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_i != null) return;
            var go = new GameObject("SceneFader");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<SceneFader>();
            _i.Build();
            SceneManager.sceneLoaded += (s, m) => _i.FadeIn();
            _i.FadeIn();
        }

        void Build()
        {
            var canGo = new GameObject("Fade Canvas", typeof(Canvas), typeof(CanvasGroup));
            canGo.transform.SetParent(transform, false);
            var canvas = canGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 999;
            cg = canGo.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false;

            var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(canGo.transform, false);
            var rt = (RectTransform)img.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var i = img.GetComponent<Image>(); i.color = Color.black; i.raycastTarget = false;
        }

        void FadeIn()
        {
            StopAllCoroutines();
            StartCoroutine(Fade());
        }

        IEnumerator Fade()
        {
            float e = 0f, dur = 0.4f;
            cg.alpha = 1f;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                cg.alpha = 1f - Mathf.Clamp01(e / dur);
                yield return null;
            }
            cg.alpha = 0f;
        }
    }
}
