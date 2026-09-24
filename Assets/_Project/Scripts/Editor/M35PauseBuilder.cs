#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // ⏸️ สร้างเมนู Pause ในเกม (Esc → เล่นต่อ/กลับเมนู/ออกเกม) — วางในฉากเกม
    //   ใช้: เมนู Nisit -> Build Pause Menu
    public static class M35PauseBuilder
    {
        public static bool SuppressDialog = false;
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string FontPath = "Assets/_Project/Art/Fonts/Mitr SDF.asset";

        [MenuItem("Nisit/Build Pause Menu", false, 34)]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
            var old = GameObject.Find("Pause Canvas");
            if (old != null) Object.DestroyImmediate(old);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var canGo = new GameObject("Pause Canvas");
            var canvas = canGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
            var scaler = canGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            canGo.AddComponent<GraphicRaycaster>();

            // dim เต็มจอ
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canGo.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            // การ์ดกลาง
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(panel.transform, false);
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(460f, 480f);
            Roundify(card, new Color(0.16f, 0.18f, 0.28f, 0.98f));

            var title = MakeText(card.transform, font, "หยุดชั่วคราว", new Vector2(0f, -30f), 40, Color.white);
            title.alignment = TextAlignmentOptions.Center;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(0, 60); trt.anchoredPosition = new Vector2(0, -30);

            var resume   = MakeButton(card.transform, font, "เล่นต่อ", new Vector2(0f, 62f), new Color(0.60f, 0.86f, 0.68f));
            var settings = MakeButton(card.transform, font, "ตั้งค่าเสียง", new Vector2(0f, -12f), new Color(0.80f, 0.72f, 0.96f));
            var menu     = MakeButton(card.transform, font, "กลับเมนูหลัก", new Vector2(0f, -86f), new Color(0.62f, 0.80f, 0.96f));
            var quit     = MakeButton(card.transform, font, "ออกจากเกม", new Vector2(0f, -160f), new Color(0.99f, 0.74f, 0.78f));

            // แผงตั้งค่าเสียง (ซ้อนบนหน้า Pause) — สร้างหลัง panel เพื่อให้อยู่ด้านบน
            var settingsPanel = BuildSettingsPanel(canGo.transform, font);

            var pm = canGo.AddComponent<PauseMenu>();
            pm.panel = panel; pm.resumeButton = resume; pm.menuButton = menu; pm.quitButton = quit;
            pm.settingsButton = settings; pm.settingsPanel = settingsPanel;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] สร้างเมนู Pause แล้ว</color>");
            if (!SuppressDialog)
                EditorUtility.DisplayDialog("Nisit Simulator", "สร้างเมนู Pause แล้ว! ⏸️\n\nกด Esc ในเกม → เล่นต่อ / ตั้งค่าเสียง / กลับเมนูหลัก / ออกเกม", "เยี่ยม!");
        }

        // ---------- แผงตั้งค่าเสียงในเกม ----------
        static GameObject BuildSettingsPanel(Transform canvasRoot, TMP_FontAsset font)
        {
            var dim = new GameObject("Pause Settings", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvasRoot, false);
            var drt = dim.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(dim.transform, false);
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(560f, 640f);
            Roundify(card, new Color(0.16f, 0.18f, 0.28f, 0.99f));

            var title = MakeText(card.transform, font, "ตั้งค่าเสียง", new Vector2(0f, 280f), 36, new Color(1f, 0.9f, 0.5f));
            title.alignment = TextAlignmentOptions.Center;

            var sc = card.AddComponent<SettingsController>();
            sc.panel = dim;
            sc.masterSlider  = LabeledSlider(card.transform, font, "เสียงรวม (Master)", 210f);
            sc.musicSlider   = LabeledSlider(card.transform, font, "เพลง (Music)", 140f);
            sc.sfxSlider     = LabeledSlider(card.transform, font, "เอฟเฟกต์ (SFX)", 70f);
            sc.voiceSlider   = LabeledSlider(card.transform, font, "เสียงพูด (Voice)", 0f);
            sc.ambientSlider = LabeledSlider(card.transform, font, "บรรยากาศ (Ambient)", -70f);
            sc.closeButton   = MakeButton(card.transform, font, "ปิด", new Vector2(0f, -250f), new Color(0.80f, 0.72f, 0.96f));

            dim.SetActive(false);
            return dim;
        }

        static Slider LabeledSlider(Transform parent, TMP_FontAsset font, string label, float y)
        {
            var lbl = MakeText(parent, font, label, new Vector2(0f, y), 22, new Color(0.95f, 0.96f, 0.92f));
            lbl.alignment = TextAlignmentOptions.Center;
            return MakeSlider(parent, new Vector2(0f, y - 36f), new Vector2(460f, 22f));
        }

        static Slider MakeSlider(Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var bg = go.GetComponent<Image>(); bg.color = new Color(0f, 0f, 0f, 0.4f);
            var slider = go.GetComponent<Slider>();

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fart = (RectTransform)fillArea.transform;
            fart.anchorMin = new Vector2(0, 0f); fart.anchorMax = new Vector2(1, 1f);
            fart.offsetMin = new Vector2(4, 4); fart.offsetMax = new Vector2(-4, -4);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            fill.GetComponent<Image>().color = new Color(0.30f, 0.85f, 0.48f);
            ((RectTransform)fill.transform).sizeDelta = new Vector2(10, 0);

            var hsa = new GameObject("Handle Slide Area", typeof(RectTransform));
            hsa.transform.SetParent(go.transform, false);
            var hart = (RectTransform)hsa.transform;
            hart.anchorMin = new Vector2(0, 0); hart.anchorMax = new Vector2(1, 1);
            hart.offsetMin = new Vector2(12, 0); hart.offsetMax = new Vector2(-12, 0);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(hsa.transform, false);
            var hImg = handle.GetComponent<Image>(); hImg.color = Color.white;
            var hrt = (RectTransform)handle.transform; hrt.sizeDelta = new Vector2(26, 0);

            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = hrt;
            slider.targetGraphic = hImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            return slider;
        }

        // การ์ดมุมมน + ขอบ + เงา (เข้าชุดกับเมนู)
        static void Roundify(GameObject go, Color col)
        {
            var img = go.GetComponent<Image>(); img.color = col;
            var round = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_round.png");
            if (round != null) { img.sprite = round; img.type = Image.Type.Sliced; }
            var ol = go.AddComponent<Outline>(); ol.effectColor = new Color(0.10f, 0.12f, 0.22f, 1f); ol.effectDistance = new Vector2(4f, -4f); ol.useGraphicAlpha = false;
            var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.35f); sh.effectDistance = new Vector2(4f, -8f);
        }

        static Button MakeButton(Transform parent, TMP_FontAsset font, string label, Vector2 pos, Color col)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(320f, 60f);
            go.GetComponent<Image>().color = col;
            var t = MakeText(go.transform, font, label, Vector2.zero, 24, new Color(0.14f, 0.16f, 0.26f));
            t.alignment = TextAlignmentOptions.Center;
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero; trt.anchoredPosition = Vector2.zero;
            return go.GetComponent<Button>();
        }

        static TMP_Text MakeText(Transform parent, TMP_FontAsset font, string s, Vector2 pos, float size, Color col)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = s; t.fontSize = size; t.color = col; t.alignment = TextAlignmentOptions.Center;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(400f, 60f);
            return t;
        }
    }
}
#endif
