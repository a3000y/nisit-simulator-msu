#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // สร้างคู่มือในเกม (Tutorial) — โผล่ครั้งแรก + กด F1 เปิดได้ · สไตล์พาสเทล
    // ใช้: เมนู  Nisit -> Build Tutorial
    public static class M24TutorialBuilder
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        static readonly Color Ink = new Color(0.12f, 0.14f, 0.24f, 1f);
        static readonly Color CardCol = new Color(0.11f, 0.14f, 0.24f, 0.99f);
        static readonly Color Gold = new Color(0.42f, 0.26f, 0.58f);   // หัวข้อการ์ด — ม่วงเข้มเข้าธีมพาสเทล (อ่านง่ายบนพื้นสว่าง)
        public static bool SuppressDialog = false;

        static Sprite Round => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_round.png")
            ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        static Sprite Pill  => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_pill2.png") ?? Round;
        static Sprite Gloss => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_gloss.png");

        [MenuItem("Nisit/Build Tutorial")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            DestroyIfExists("Tutorial Canvas");
            var canvasGo = new GameObject("Tutorial Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var ctrl = canvasGo.AddComponent<TutorialController>();

            var dim = Img(canvasGo.transform, "Panel", Vector2.zero, Vector2.one, Vector2.zero, null, new Color(0.04f, 0.06f, 0.12f, 0.66f));
            dim.rectTransform.offsetMin = Vector2.zero; dim.rectTransform.offsetMax = Vector2.zero;

            var card = Rounded(dim.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 660), CardCol);
            Deco(card);

            Txt(card.transform, "Title", "วิธีเล่น Nisit Simulator", 44, new Vector2(0.5f, 1f), new Vector2(0, -44), new Vector2(760, 60), Gold, TextAlignmentOptions.Center, FontStyles.Bold);

            Txt(card.transform, "Ctrl", "ปุ่มควบคุม", 28, new Vector2(0, 1f), new Vector2(60, -120), new Vector2(700, 40), new Color(0.72f, 0.92f, 0.78f), TextAlignmentOptions.Left, FontStyles.Bold);
            Txt(card.transform, "CtrlBody",
                "WASD — เดิน     Shift — วิ่ง     Space — กระโดด\n" +
                "คลิกซ้าย / E — โต้ตอบ (เข้าเรียน / กิน / ทำงาน / สอบ)\n" +
                "TAB — โทรศัพท์     M — แผนที่     I — กระเป๋า",
                25, new Vector2(0, 1f), new Vector2(60, -160), new Vector2(710, 150), Color.white, TextAlignmentOptions.TopLeft, FontStyles.Normal);

            Txt(card.transform, "Goal", "เป้าหมาย", 28, new Vector2(0, 1f), new Vector2(60, -310), new Vector2(700, 40), new Color(0.80f, 0.85f, 1f), TextAlignmentOptions.Left, FontStyles.Bold);
            Txt(card.transform, "GoalBody",
                "สะสมความรู้ให้ถึงเป้าแต่ละปี แล้วไปสอบที่ตึกคณะ\n" +
                "เลื่อนชั้นปี 1 → 4 จนจบการศึกษา!\n" +
                "อย่าลืมดูแล พลังงาน / สุขภาพ / ความอิ่ม (กินข้าว + นอน)",
                25, new Vector2(0, 1f), new Vector2(60, -350), new Vector2(710, 150), Color.white, TextAlignmentOptions.TopLeft, FontStyles.Normal);

            Txt(card.transform, "Hint", "กด F1 เพื่อเปิดคู่มือนี้อีกครั้งได้ทุกเมื่อ", 20, new Vector2(0.5f, 0f), new Vector2(0, 118), new Vector2(700, 34), new Color(0.75f, 0.78f, 0.86f), TextAlignmentOptions.Center, FontStyles.Italic);

            var start = CandyButton(card.transform, "เริ่มเล่น!", new Vector2(0, 46), new Color(0.60f, 0.86f, 0.68f));

            ctrl.panel = dim.gameObject;
            ctrl.closeButton = start;

            var f = ThaiFontSetup.GetOrCreateThaiFont();
            if (f != null) foreach (var t in canvasGo.GetComponentsInChildren<TMP_Text>(true)) t.font = f;

            dim.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] สร้างคู่มือในเกมเสร็จ!</color>");
            if (!SuppressDialog)
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "สร้างคู่มือในเกมเสร็จแล้ว! 📖\n\n• โผล่อัตโนมัติครั้งแรกที่เล่น\n• กด F1 เปิด/ปิดได้ทุกเมื่อ (ดีตอนนำเสนอ)\n\nกด Play ทดสอบได้เลย", "เยี่ยม!");
        }

        // ---------- helpers ----------
        static Image Img(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Sprite sprite, Color col)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.type = (sprite != null && sprite.border.sqrMagnitude > 0.01f) ? Image.Type.Sliced : Image.Type.Simple; img.color = col;
            var rt = img.rectTransform; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(100, 100);
            return img;
        }

        static Image Rounded(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = Round; img.type = Image.Type.Sliced; img.color = col; img.raycastTarget = false;
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return img;
        }

        static TMP_Text Txt(Transform parent, string name, string text, float size, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, Color col, TextAlignmentOptions align, FontStyles style)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = col; t.fontStyle = style; t.alignment = align; t.enableWordWrapping = true;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return t;
        }

        static Button CandyButton(Transform parent, string label, Vector2 pos, Color col)
        {
            var size = new Vector2(300, 78);
            var sprite = Pill;
            var baseGo = new GameObject(label + "Base", typeof(Image));
            baseGo.transform.SetParent(parent, false);
            var bimg = baseGo.GetComponent<Image>(); bimg.sprite = sprite; bimg.type = Image.Type.Sliced; bimg.color = Shift(col, -0.24f); bimg.raycastTarget = false;
            var brt = bimg.rectTransform; brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f); brt.anchoredPosition = pos + new Vector2(0, -6); brt.sizeDelta = size;

            var go = new GameObject(label + "Btn", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.sprite = sprite; img.type = Image.Type.Sliced; img.color = col;
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
            Deco(img);
            var gl = Gloss;
            if (gl != null)
            {
                var g = new GameObject("Gloss", typeof(Image)); g.transform.SetParent(go.transform, false);
                var gi = g.GetComponent<Image>(); gi.sprite = gl; gi.type = Image.Type.Simple; gi.color = new Color(1f, 1f, 1f, 0.45f); gi.raycastTarget = false;
                var grt = gi.rectTransform; grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f); grt.sizeDelta = new Vector2(size.x * 0.9f, size.y * 0.5f); grt.anchoredPosition = new Vector2(0, size.y * 0.22f);
            }
            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint; btn.targetGraphic = img;
            var cb = btn.colors; cb.normalColor = col; cb.highlightedColor = Shift(col, 0.12f); cb.pressedColor = Shift(col, -0.10f); cb.fadeDuration = 0.1f; btn.colors = cb;
            var t = Txt(go.transform, "Text", label, 30, new Vector2(0.5f, 0.5f), Vector2.zero, size, new Color(0.20f, 0.22f, 0.38f), TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            t.transform.SetAsLastSibling();
            return btn;
        }

        static void Deco(Graphic g)
        {
            var o = g.gameObject.AddComponent<Outline>(); o.effectColor = Ink; o.effectDistance = new Vector2(5, -5); o.useGraphicAlpha = false;
            var s = g.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0f, 0f, 0f, 0.35f); s.effectDistance = new Vector2(4, -6);
        }

        static Color Shift(Color c, float d) => new Color(Mathf.Clamp01(c.r + d), Mathf.Clamp01(c.g + d), Mathf.Clamp01(c.b + d), c.a);

        static void DestroyIfExists(string name) { var go = GameObject.Find(name); if (go != null) Object.DestroyImmediate(go); }
    }
}
#endif
