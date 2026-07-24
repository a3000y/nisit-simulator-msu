#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NisitSimulator.Core;
using NisitSimulator.SaveLoad;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // สร้างเมนูหลัก (ฉาก 1) + ตั้งค่าเสียง + ต่อระบบ Save/Load + ตั้ง Build Settings
    // ใช้: เมนู  Nisit -> Build M4 Menu
    public static class M4MenuBuilder
    {
        const string MenuPath = "Assets/Scenes/Scene1.unity";
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string FontSdf = "Assets/_Project/Art/Fonts/LeelawadeeUI SDF.asset";
        static TMP_FontAsset thai;
        static Sprite ui;

        [MenuItem("Nisit/Build M4 Menu")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            thai = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdf);
            ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // ===== 1) ฉากเกม: ใส่ GameplayBootstrap ที่ GameManager =====
            var gp = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null && gm.GetComponent<GameplayBootstrap>() == null)
                gm.gameObject.AddComponent<GameplayBootstrap>();
            EditorSceneManager.MarkSceneDirty(gp);
            EditorSceneManager.SaveScene(gp);

            // ===== 2) ฉากเมนู =====
            var menu = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
            BuildMenuUI();
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);

            // ===== 3) Build Settings (เมนูเป็นฉากแรก) =====
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(GameplayPath, true),
            };

            Debug.Log("<color=lime>[Nisit] สร้างเมนู M4 เสร็จ!</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างเมนูหลัก (M4) เสร็จแล้ว! 🎬\n\nตอนนี้อยู่ที่ฉากเมนู (Scene1)\nกด Play เพื่อทดสอบ:\n- เล่นคนเดียว → เข้าเกม\n- ตั้งค่า → ปรับเสียง\n- จบเกมแล้วกด 'ออก' → กลับเมนู\n- เล่นต่อ → โหลดเซฟล่าสุด\n\n(เซฟอัตโนมัติทุกวันในเกม)", "เยี่ยม!");
        }

        static void BuildMenuUI()
        {
            // พื้นหลังกล้อง
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.20f, 0.30f); }

            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.name == "Menu Canvas") Object.DestroyImmediate(c.gameObject);

            var canvasGo = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var root = canvasGo.transform;

            // พื้นหลังไล่สี (แผงทึบ)
            var bg = MakeImage(root, "BG", Vector2.zero, Vector2.one, new Color(0.13f, 0.17f, 0.26f, 1f));
            bg.rectTransform.offsetMin = Vector2.zero; bg.rectTransform.offsetMax = Vector2.zero;

            // ชื่อเกม
            MakeText(root, "Title", "NISIT SIMULATOR", 96, new Vector2(0.5f, 1f), new Vector2(0, -160),
                 new Vector2(1400, 140), new Color(1f, 0.86f, 0.42f), FontStyles.Bold);
            MakeText(root, "Sub", "จำลองชีวิตนิสิต", 40, new Vector2(0.5f, 1f), new Vector2(0, -280),
                 new Vector2(1000, 60), Color.white, FontStyles.Normal);

            // ปุ่ม
            var mc = canvasGo.AddComponent<MainMenuController>();
            mc.newGameButton  = MakeButton(root, "เล่นคนเดียว", new Vector2(0, 40),   new Color(0.30f, 0.62f, 0.38f));
            mc.continueButton = MakeButton(root, "เล่นต่อ",     new Vector2(0, -50),  new Color(0.30f, 0.52f, 0.70f));
            mc.settingsButton = MakeButton(root, "ตั้งค่า",      new Vector2(0, -140), new Color(0.45f, 0.42f, 0.58f));
            mc.quitButton     = MakeButton(root, "ออก",         new Vector2(0, -230), new Color(0.62f, 0.34f, 0.34f));

            // ===== แผงตั้งค่าเสียง =====
            var panel = MakeImage(root, "SettingsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.15f, 0.18f, 0.28f, 0.98f));
            panel.rectTransform.sizeDelta = new Vector2(700, 560);
            MakeText(panel.transform, "STitle", "ตั้งค่าเสียง", 48, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(600, 60), new Color(1f, 0.86f, 0.42f), FontStyles.Bold);

            var sc = panel.gameObject.AddComponent<SettingsController>();
            sc.panel = panel.gameObject;
            sc.masterSlider = LabeledSlider(panel.transform, "เสียงรวม (Master)", -60);
            sc.musicSlider  = LabeledSlider(panel.transform, "เพลง (Music)", -160);
            sc.sfxSlider    = LabeledSlider(panel.transform, "เอฟเฟกต์ (SFX)", -260);
            sc.closeButton  = MakeButton(panel.transform, "ปิด", new Vector2(0, 120 - 560 / 2f + 30), new Color(0.5f, 0.4f, 0.4f), 0.5f, 0f);
            // ปุ่มปิดจัดที่ล่างแผง
            var crt = sc.closeButton.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0, 40);

            mc.settingsPanel = panel.gameObject;
            panel.gameObject.SetActive(false);
        }

        // ---------- helpers ----------
        static Image MakeImage(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color col)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = ui; img.type = Image.Type.Sliced; img.color = col;
            var rt = img.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(100, 100);
            return img;
        }

        static TMP_Text MakeText(Transform parent, string name, string text, float size, Vector2 anchor,
            Vector2 pos, Vector2 sizeDelta, Color col, FontStyles style)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = col; t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            if (thai != null) t.font = thai;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return t;
        }

        static Button MakeButton(Transform parent, string label, Vector2 pos, Color col, float ax = 0.5f, float ay = 0.5f)
        {
            var go = new GameObject(label + "Btn", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = ui; img.type = Image.Type.Sliced; img.color = col;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(360, 76);

            var txt = MakeText(go.transform, "Text", label, 34, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, 76), Color.white, FontStyles.Bold);
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        static Slider LabeledSlider(Transform parent, string label, float y)
        {
            MakeText(parent, label + "Lbl", label, 28, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(600, 40), Color.white, FontStyles.Normal);
            return MakeSlider(parent, new Vector2(0, y - 44), new Vector2(560, 30));
        }

        static Slider MakeSlider(Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var bg = go.GetComponent<Image>(); bg.sprite = ui; bg.type = Image.Type.Sliced; bg.color = new Color(0, 0, 0, 0.5f);
            var slider = go.GetComponent<Slider>();

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fart = (RectTransform)fillArea.transform;
            fart.anchorMin = new Vector2(0, 0.25f); fart.anchorMax = new Vector2(1, 0.75f);
            fart.offsetMin = new Vector2(8, 0); fart.offsetMax = new Vector2(-8, 0);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fImg = fill.GetComponent<Image>(); fImg.sprite = ui; fImg.type = Image.Type.Sliced; fImg.color = new Color(0.45f, 0.8f, 0.45f);
            ((RectTransform)fill.transform).sizeDelta = new Vector2(10, 0);

            var hsa = new GameObject("Handle Slide Area", typeof(RectTransform));
            hsa.transform.SetParent(go.transform, false);
            var hart = (RectTransform)hsa.transform;
            hart.anchorMin = new Vector2(0, 0); hart.anchorMax = new Vector2(1, 1);
            hart.offsetMin = new Vector2(12, 0); hart.offsetMax = new Vector2(-12, 0);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(hsa.transform, false);
            var hImg = handle.GetComponent<Image>(); hImg.sprite = ui; hImg.color = Color.white;
            ((RectTransform)handle.transform).sizeDelta = new Vector2(26, 0);

            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = (RectTransform)handle.transform;
            slider.targetGraphic = hImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            return slider;
        }
    }
}
#endif
