using System.Collections;
using UnityEngine;
using TMPro;
using NisitSimulator.Stats;

namespace NisitSimulator.UI
{
    // เด้งตัวเลขลอย +เงิน/+ความรู้/+EXP เวลาได้รางวัล — สร้างเองตอนรัน (GameplayBootstrap เรียก EnsureExists)
    public class FloatingTextSpawner : MonoBehaviour
    {
        static FloatingTextSpawner _i;
        public static void EnsureExists()
        {
            if (_i == null) { var go = new GameObject("FloatingTextSpawner"); _i = go.AddComponent<FloatingTextSpawner>(); }
        }

        PlayerStats stats;
        Transform canvasT;
        TMP_FontAsset font;
        int lastMoney, lastExp;
        float lastKnow;
        bool primed;

        void Awake()
        {
            if (_i != null && _i != this) { Destroy(gameObject); return; }
            _i = this;
            font = Object.FindFirstObjectByType<TMP_Text>()?.font;

            var canGo = new GameObject("FloatingText Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            canGo.transform.SetParent(transform, false);
            var canvas = canGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 85;
            var sc = canGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            sc.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080);
            canvasT = canGo.transform;
        }

        void Update()
        {
            if (stats == null) { stats = Object.FindFirstObjectByType<PlayerStats>(); if (stats == null) return; }
            if (!primed) { lastMoney = stats.Money; lastExp = stats.Exp; lastKnow = stats.Knowledge; primed = true; return; }

            if (stats.Money > lastMoney) Spawn($"+{stats.Money - lastMoney}฿", new Color(1f, 0.85f, 0.3f));
            if (stats.Exp > lastExp) Spawn($"+{stats.Exp - lastExp} EXP", new Color(0.75f, 0.85f, 1f));
            if (stats.Knowledge - lastKnow >= 1f) Spawn($"+{Mathf.RoundToInt(stats.Knowledge - lastKnow)} ความรู้", new Color(0.62f, 0.95f, 0.72f));

            lastMoney = stats.Money; lastExp = stats.Exp; lastKnow = stats.Knowledge;
        }

        void Spawn(string text, Color col)
        {
            var go = new GameObject("Float", typeof(RectTransform));
            go.transform.SetParent(canvasT, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = 40; t.color = col; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
            t.outlineWidth = 0.2f; t.outlineColor = new Color32(20, 22, 38, 255);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 60);
            rt.anchoredPosition = new Vector2(Random.Range(-140f, 140f), -60f);
            StartCoroutine(Float(rt, t));
        }

        IEnumerator Float(RectTransform rt, TMP_Text t)
        {
            float e = 0f, dur = 1.1f;
            Vector2 start = rt.anchoredPosition;
            while (e < dur && rt != null)
            {
                e += Time.unscaledDeltaTime;
                float k = e / dur;
                rt.anchoredPosition = start + new Vector2(0f, 110f * k);
                if (t != null) { var c = t.color; c.a = 1f - Mathf.Clamp01((k - 0.4f) / 0.6f); t.color = c; }
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }
    }
}
