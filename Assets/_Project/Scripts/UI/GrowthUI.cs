using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NisitSimulator.UI
{
    // ตัวช่วยสร้าง UI ตอนรัน (โทนพาสเทลเดียวกับหน้าอื่น) — ใช้กับเลเวล/เลือกความสามารถ/สรุปวัน
    public static class GrowthUI
    {
        public static readonly Color CardCol = new Color(0.955f, 0.93f, 0.985f, 0.99f);
        public static readonly Color Strip   = new Color(0.82f, 0.78f, 0.94f, 1f);
        public static readonly Color Ink     = new Color(0.30f, 0.25f, 0.46f);
        public static readonly Color Soft    = new Color(0.50f, 0.46f, 0.64f);
        public static readonly Color Title   = new Color(0.42f, 0.26f, 0.58f);
        public static readonly Color Good    = new Color(0.22f, 0.62f, 0.38f);
        public static readonly Color Gold    = new Color(1f, 0.74f, 0.25f);

        static TMP_FontAsset _font;
        static Sprite _round;

        public static TMP_FontAsset Font
        {
            get
            {
                if (_font != null) return _font;
                foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t == null || t.font == null) continue;
                    _font = t.font;
                    if (t.font.name.Contains("Mitr")) break;   // ฟอนต์ไทยหลักของเกม
                }
                return _font;
            }
        }

        public static Sprite Round
        {
            get
            {
                if (_round != null) return _round;
                foreach (var i in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (i != null && i.sprite != null && i.sprite.name.StartsWith("ui_round")) { _round = i.sprite; break; }
                return _round;
            }
        }

        public static Canvas MakeCanvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920, 1080); s.matchWidthOrHeight = 0.5f;
            return c;
        }

        public static Image Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color col, bool shadow = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = col;
            if (Round != null) { img.sprite = Round; img.type = Image.Type.Sliced; }
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            if (shadow)
            {
                var sh = go.AddComponent<Shadow>();
                sh.effectColor = new Color(0.28f, 0.24f, 0.44f, 0.30f); sh.effectDistance = new Vector2(0f, -6f);
            }
            return img;
        }

        public static Image Dim(Transform parent)
        {
            var go = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>(); img.color = new Color(0.16f, 0.13f, 0.26f, 0.55f);
            return img;
        }

        public static TMP_Text Text(Transform parent, string s, Vector2 pos, Vector2 size, float fs, Color col, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = s; t.fontSize = fs; t.color = col; t.alignment = align; t.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return t;
        }

        public static Button Button(Transform parent, string label, Vector2 pos, Vector2 size, Color col, float fs = 24f)
        {
            var img = Box(parent, "Btn_" + label, new Vector2(0.5f, 0.5f), pos, size, col);
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors; cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f); cb.pressedColor = new Color(0.9f, 0.9f, 0.9f); b.colors = cb;
            var t = Text(img.transform, label, Vector2.zero, size, fs, Ink);
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            return b;
        }
    }
}
