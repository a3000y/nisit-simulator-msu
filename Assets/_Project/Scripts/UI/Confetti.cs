using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.UI
{
    // คอนเฟตตีฉลอง (ตอนจบการศึกษา) — สร้าง canvas ของตัวเอง โปรยชิ้นสีตกลงมา แล้วลบตัวเองทิ้ง
    public class Confetti : MonoBehaviour
    {
        static readonly Color[] Cols = {
            new Color(1f,0.85f,0.35f), new Color(1f,0.55f,0.6f), new Color(0.55f,0.85f,1f),
            new Color(0.6f,0.95f,0.7f), new Color(0.85f,0.7f,1f), new Color(1f,0.75f,0.45f)
        };

        public static void Burst(int count = 70)
        {
            var go = new GameObject("Confetti");
            var c = go.AddComponent<Confetti>();
            c.count = count;
        }

        public int count = 70;

        void Start()
        {
            var canGo = new GameObject("Confetti Canvas", typeof(Canvas), typeof(CanvasScaler));
            canGo.transform.SetParent(transform, false);
            var canvas = canGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            var sc = canGo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080);

            for (int i = 0; i < count; i++)
            {
                var pg = new GameObject("p", typeof(RectTransform), typeof(Image));
                pg.transform.SetParent(canGo.transform, false);
                var img = pg.GetComponent<Image>(); img.color = Cols[Random.Range(0, Cols.Length)]; img.raycastTarget = false;
                var rt = (RectTransform)pg.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                float s = Random.Range(10f, 20f); rt.sizeDelta = new Vector2(s, s * Random.Range(0.6f, 1.4f));
                rt.anchoredPosition = new Vector2(Random.Range(-940f, 940f), Random.Range(560f, 760f));
                StartCoroutine(Fall(rt, img));
            }
            Destroy(gameObject, 5f);
        }

        IEnumerator Fall(RectTransform rt, Image img)
        {
            float vy = Random.Range(-260f, -160f);
            float vx = Random.Range(-70f, 70f);
            float spin = Random.Range(-220f, 220f);
            float life = Random.Range(3f, 4.2f), e = 0f;
            while (e < life && rt != null)
            {
                float dt = Time.unscaledDeltaTime; e += dt;
                vy -= 220f * dt;   // แรงโน้มถ่วง
                rt.anchoredPosition += new Vector2(vx, vy) * dt;
                rt.Rotate(0f, 0f, spin * dt);
                if (img != null && e > life - 1f) { var c = img.color; c.a = Mathf.Clamp01(life - e); img.color = c; }
                yield return null;
            }
        }
    }
}
