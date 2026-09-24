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
        const string FontSdf = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        static TMP_FontAsset thai;
        static Sprite ui, round, pill, gloss, circle;
        static Sprite LoadUI(string f) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/" + f);

        public static bool SuppressDialog = false;   // ปิด popup เมื่อเรียกจาก Rebuild All

        [MenuItem("Nisit/Build M4 Menu")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            thai = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdf);
            ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            round  = LoadUI("ui_round.png"); pill = LoadUI("ui_pill2.png");
            gloss  = LoadUI("ui_gloss.png"); circle = LoadUI("ui_circle.png");   // สไปรต์ลูกกวาด (จาก Polish)

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

            // ===== 3) Build Settings (เมนูเป็นฉากแรก) — ใส่ล็อบบี้ด้วยถ้ามี (กันเขียนทับตอน rebuild) =====
            const string LobbyPath = "Assets/_Project/Scenes/02_Lobby.unity";
            var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuPath, true),
            };
            if (System.IO.File.Exists(LobbyPath)) buildScenes.Add(new EditorBuildSettingsScene(LobbyPath, true));
            buildScenes.Add(new EditorBuildSettingsScene(GameplayPath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();

            // ===== 4) ต่อ Polish + พื้นหลัง + หน้าเลือกคณะ อัตโนมัติ (กดครั้งเดียวจบ ไม่ต้องไล่กด) =====
            MenuPolish.SuppressDialog = true;
            M11FacultyBuilder.SuppressDialog = true;
            try
            {
                MenuPolish.Run();                  // ปุ่มลูกกวาด + โลโก้เงานูน + parallax/intro
                MenuPolish.DayCampusBackground();  // พื้นหลังมหาลัยกลางวันสดใส
                M11FacultyBuilder.Build();         // หน้าเลือกคณะ (สไตล์เดียวกัน อยู่บนสุด)
            }
            catch (System.Exception e) { Debug.LogWarning("[Nisit] ต่อเมนูอัตโนมัติมีปัญหา: " + e.Message); }
            finally
            {
                MenuPolish.SuppressDialog = false;
                M11FacultyBuilder.SuppressDialog = false;
            }
            EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);   // กลับมาที่ฉากเมนูให้ผู้ใช้พร้อม Play

            Debug.Log("<color=lime>[Nisit] สร้างเมนู M4 + Polish + พื้นหลัง + คณะ เสร็จครบ!</color>");
            if (!SuppressDialog)
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างเมนูครบทุกอย่างในคลิกเดียว! 🎬✨\n\n✓ ปุ่มลูกกวาดเงาวาว 5 ปุ่ม\n✓ พื้นหลังมหาลัยสดใส\n✓ หน้าเลือกคณะ (สไตล์เดียวกัน)\n✓ เสียง 5 ระดับ\n\nกด Play ที่ฉากเมนู (Scene1) ได้เลย", "เยี่ยม!");
        }

        static void BuildMenuUI()
        {
            // พื้นหลังกล้อง
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.20f, 0.30f); }

            // ลบ Menu Canvas เก่า + Canvas ของฉากเกมที่หลงเข้ามาในฉากเมนู (กันปนกัน)
            string[] strayInMenu = { "Menu Canvas", "HUD Canvas", "End Canvas", "Exam Canvas",
                "Inventory Canvas", "Shop Canvas", "Cafeteria Canvas", "Quest Canvas",
                "Event Canvas", "Minimap Canvas", "Phone Canvas", "Tutorial Canvas" };
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (System.Array.IndexOf(strayInMenu, c.name) >= 0) Object.DestroyImmediate(c.gameObject);

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

            // ปุ่ม (5 ปุ่ม)
            var mc = canvasGo.AddComponent<MainMenuController>();
            mc.newGameButton     = MakeButton(root, "เล่นคนเดียว", new Vector2(0, 78),   new Color(0.60f, 0.86f, 0.68f));
            mc.multiplayerButton = MakeButton(root, "เล่นหลายคน", new Vector2(0, -12),  new Color(0.99f, 0.82f, 0.62f));
            mc.continueButton    = MakeButton(root, "เล่นต่อ",     new Vector2(0, -102), new Color(0.62f, 0.80f, 0.96f));
            mc.settingsButton    = MakeButton(root, "ตั้งค่า",      new Vector2(0, -192), new Color(0.80f, 0.72f, 0.96f));
            mc.quitButton        = MakeButton(root, "ออก",         new Vector2(0, -282), new Color(0.99f, 0.74f, 0.78f));

            // ===== แผงเลือกช่องบันทึก (popup — เด้งตอนกดเล่นคนเดียว/เล่นต่อ) =====
            var slDim = MakeImage(root, "SlotPanel", Vector2.zero, Vector2.one, new Color(0.04f, 0.06f, 0.12f, 0.62f));
            slDim.rectTransform.offsetMin = Vector2.zero; slDim.rectTransform.offsetMax = Vector2.zero;
            var slCard = MakeRounded(slDim.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 600), new Color(0.11f, 0.14f, 0.24f, 0.99f));
            Deco(slCard);
            MakeText(slCard.transform, "SlTitle", "เลือกช่องบันทึก", 42, new Vector2(0.5f, 1f), new Vector2(0, -46), new Vector2(560, 56), new Color(1f, 0.9f, 0.5f), FontStyles.Bold);
            var slotBtns = new Button[3];
            for (int i = 0; i < 3; i++)
                slotBtns[i] = MakeButton(slCard.transform, "ช่อง " + (i + 1), new Vector2(0f, 120f - i * 118f), new Color(0.72f, 0.82f, 0.72f));
            var slClose = MakeButton(slCard.transform, "ยกเลิก", new Vector2(0, 30), new Color(0.86f, 0.80f, 0.88f), 0.5f, 0f);
            mc.slotButtons = slotBtns;
            mc.slotPanel = slDim.gameObject;
            mc.slotCloseButton = slClose;
            slDim.gameObject.SetActive(false);

            // ===== แผงตั้งค่าเสียง (dim เต็มจอ + การ์ดกลาง) =====
            var sDim = MakeImage(root, "SettingsPanel", Vector2.zero, Vector2.one, new Color(0.04f, 0.06f, 0.12f, 0.62f));
            sDim.rectTransform.offsetMin = Vector2.zero; sDim.rectTransform.offsetMax = Vector2.zero;
            var sCard = MakeRounded(sDim.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660, 820), new Color(0.11f, 0.14f, 0.24f, 0.99f));
            Deco(sCard);
            MakeText(sCard.transform, "STitle", "ตั้งค่า", 46, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(600, 60), new Color(1f, 0.9f, 0.5f), FontStyles.Bold);

            var sc = sCard.gameObject.AddComponent<SettingsController>();
            sc.panel = sDim.gameObject;
            sc.masterSlider  = LabeledSlider(sCard.transform, "เสียงรวม (Master)", -108);
            sc.musicSlider   = LabeledSlider(sCard.transform, "เพลง (Music)", -196);
            sc.sfxSlider     = LabeledSlider(sCard.transform, "เอฟเฟกต์ (SFX)", -284);
            sc.voiceSlider   = LabeledSlider(sCard.transform, "เสียงพูด (Voice)", -372);
            sc.ambientSlider = LabeledSlider(sCard.transform, "บรรยากาศ (Ambient)", -460);
            // กราฟิก (ปุ่มวน — คลิกเพื่อเปลี่ยนคุณภาพ/สลับเต็มจอ)
            sc.qualityButton    = MakeButton(sCard.transform, "คุณภาพ", new Vector2(0, -556), new Color(0.62f, 0.80f, 0.96f), 0.5f, 1f);
            sc.fullscreenButton = MakeButton(sCard.transform, "เต็มจอ", new Vector2(0, -648), new Color(0.62f, 0.80f, 0.96f), 0.5f, 1f);
            sc.closeButton   = MakeButton(sCard.transform, "ปิด", new Vector2(0, 28), new Color(0.80f, 0.72f, 0.96f), 0.5f, 0f);

            mc.settingsPanel = sDim.gameObject;
            sDim.gameObject.SetActive(false);

            // ===== ป็อปอัป "กำลังพัฒนา" (dim + การ์ด) =====
            var csDim = MakeImage(root, "ComingSoonPanel", Vector2.zero, Vector2.one, new Color(0.04f, 0.06f, 0.12f, 0.62f));
            csDim.rectTransform.offsetMin = Vector2.zero; csDim.rectTransform.offsetMax = Vector2.zero;
            var csCard = MakeRounded(csDim.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680, 360), new Color(0.11f, 0.14f, 0.24f, 0.99f));
            Deco(csCard);
            MakeText(csCard.transform, "CSTitle", "กำลังพัฒนา", 46, new Vector2(0.5f, 1f), new Vector2(0, -48), new Vector2(640, 62), new Color(1f, 0.9f, 0.5f), FontStyles.Bold);
            var csBody = MakeText(csCard.transform, "CSBody", "โหมดเล่นหลายคน (Multiplayer)\nจะเปิดให้เล่นในเวอร์ชันถัดไป", 27, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(600, 110), new Color(0.92f, 0.94f, 0.88f), FontStyles.Normal);
            csBody.enableWordWrapping = true; csBody.alignment = TextAlignmentOptions.Center;
            var csClose = MakeButton(csCard.transform, "เข้าใจแล้ว", new Vector2(0, 30), new Color(0.80f, 0.72f, 0.96f), 0.5f, 0f);
            mc.comingSoonPanel = csDim.gameObject;
            mc.comingSoonCloseButton = csClose;
            csDim.gameObject.SetActive(false);

            // ===== ปุ่ม "เกี่ยวกับ / ผู้จัดทำ" + แผงเครดิต =====
            mc.creditsButton = MakeButton(root, "เกี่ยวกับ / ผู้จัดทำ", new Vector2(0, -372), new Color(0.72f, 0.78f, 0.92f));
            mc.creditsButton.transform.localScale = Vector3.one * 0.8f;   // เล็กกว่าปุ่มหลัก

            var crDim = MakeImage(root, "CreditsPanel", Vector2.zero, Vector2.one, new Color(0.04f, 0.06f, 0.12f, 0.66f));
            crDim.rectTransform.offsetMin = Vector2.zero; crDim.rectTransform.offsetMax = Vector2.zero;
            var crCard = MakeRounded(crDim.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 760), new Color(0.11f, 0.14f, 0.24f, 0.99f));
            Deco(crCard);
            MakeText(crCard.transform, "CRTitle", "เกี่ยวกับเกม", 46, new Vector2(0.5f, 1f), new Vector2(0, -44), new Vector2(660, 62), new Color(1f, 0.9f, 0.5f), FontStyles.Bold);

            // แก้ชื่อผู้จัดทำ/อาจารย์ได้ที่ข้อความนี้ (ส่วนในวงเล็บ [ ])
            string crBodyText =
                "<b>Nisit Simulator</b>   ·   จำลองชีวิตนิสิต\n\n" +
                "ปริญญานิพนธ์\n" +
                "สาขาเทคโนโลยีสารสนเทศ\n" +
                "มหาวิทยาลัยมหาสารคาม\n\n" +
                "<b>ผู้จัดทำ</b>\n[ใส่ชื่อ-สกุล / รหัสนิสิต]\n\n" +
                "<b>อาจารย์ที่ปรึกษา</b>\n[ใส่ชื่ออาจารย์ที่ปรึกษา]\n\n" +
                "<size=80%><color=#B9C2D6>พัฒนาด้วย Unity\nโมเดล/แอนิเมชัน: Mixamo, Quaternius · UI: Kenney</color></size>";
            var crBody = MakeText(crCard.transform, "CRBody", crBodyText, 26, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(620, 540), new Color(0.94f, 0.96f, 0.92f), FontStyles.Normal);
            crBody.enableWordWrapping = true; crBody.alignment = TextAlignmentOptions.Top;

            var crClose = MakeButton(crCard.transform, "ปิด", new Vector2(0, 30), new Color(0.80f, 0.72f, 0.96f), 0.5f, 0f);
            mc.creditsPanel = crDim.gameObject;
            mc.creditsCloseButton = crClose;
            crDim.gameObject.SetActive(false);
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
            var size = new Vector2(360, 76);
            var sprite = pill ?? ui;
            var anchor = new Vector2(ax, ay);

            // ฐานเงาเข้ม (หลังปุ่ม)
            var baseGo = new GameObject(label + "Base", typeof(Image));
            baseGo.transform.SetParent(parent, false);
            var bimg = baseGo.GetComponent<Image>();
            bimg.sprite = sprite; bimg.type = Image.Type.Sliced; bimg.color = Shift(col, -0.26f); bimg.raycastTarget = false;
            var brt = bimg.rectTransform; brt.anchorMin = brt.anchorMax = brt.pivot = anchor; brt.anchoredPosition = pos + new Vector2(0, -7); brt.sizeDelta = size;

            var go = new GameObject(label + "Btn", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.type = Image.Type.Sliced; img.color = col;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            Deco(img);

            if (gloss != null)
            {
                var gGo = new GameObject("Gloss", typeof(Image));
                gGo.transform.SetParent(go.transform, false);
                var g = gGo.GetComponent<Image>();
                g.sprite = gloss; g.type = Image.Type.Simple; g.color = new Color(1f, 1f, 1f, 0.5f); g.raycastTarget = false;
                var grt = g.rectTransform; grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
                grt.sizeDelta = new Vector2(size.x * 0.9f, size.y * 0.55f); grt.anchoredPosition = new Vector2(0, size.y * 0.2f);
            }

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint; btn.targetGraphic = img;
            var cb = btn.colors; cb.normalColor = col; cb.highlightedColor = Shift(col, 0.12f); cb.pressedColor = Shift(col, -0.10f); cb.fadeDuration = 0.1f; btn.colors = cb;

            var txt = MakeText(go.transform, "Text", label, 34, new Vector2(0.5f, 0.5f), Vector2.zero, size, new Color(0.20f, 0.22f, 0.38f), FontStyles.Bold);
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            txt.transform.SetAsLastSibling();
            return btn;
        }

        static Slider LabeledSlider(Transform parent, string label, float y)
        {
            MakeText(parent, label + "Lbl", label, 26, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(560, 36), new Color(0.95f, 0.96f, 0.92f), FontStyles.Bold);
            return MakeSlider(parent, new Vector2(0, y - 46), new Vector2(540, 26));
        }

        static Slider MakeSlider(Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var bg = go.GetComponent<Image>(); bg.sprite = round ?? ui; bg.type = Image.Type.Sliced; bg.color = new Color(0f, 0f, 0f, 0.40f);
            var slider = go.GetComponent<Slider>();

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fart = (RectTransform)fillArea.transform;
            fart.anchorMin = new Vector2(0, 0f); fart.anchorMax = new Vector2(1, 1f);
            fart.offsetMin = new Vector2(4, 4); fart.offsetMax = new Vector2(-4, -4);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fImg = fill.GetComponent<Image>(); fImg.sprite = round ?? ui; fImg.type = Image.Type.Sliced; fImg.color = new Color(0.30f, 0.85f, 0.48f);
            ((RectTransform)fill.transform).sizeDelta = new Vector2(10, 0);

            var hsa = new GameObject("Handle Slide Area", typeof(RectTransform));
            hsa.transform.SetParent(go.transform, false);
            var hart = (RectTransform)hsa.transform;
            hart.anchorMin = new Vector2(0, 0); hart.anchorMax = new Vector2(1, 1);
            hart.offsetMin = new Vector2(14, 0); hart.offsetMax = new Vector2(-14, 0);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(hsa.transform, false);
            var hImg = handle.GetComponent<Image>(); hImg.sprite = circle ?? ui; hImg.color = Color.white;
            var hrt = (RectTransform)handle.transform; hrt.sizeDelta = new Vector2(32, 0);

            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = hrt;
            slider.targetGraphic = hImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            return slider;
        }

        // ---------- helper สไตล์ลูกกวาด ----------
        static Image MakeRounded(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = round ?? ui; img.type = Image.Type.Sliced; img.color = col; img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return img;
        }

        static void Deco(Graphic g)
        {
            var o = g.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.12f, 0.14f, 0.24f, 1f); o.effectDistance = new Vector2(5, -5); o.useGraphicAlpha = false;
            var s = g.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0f, 0f, 0f, 0.35f); s.effectDistance = new Vector2(3, -5);
        }

        static Color Shift(Color c, float d) => new Color(Mathf.Clamp01(c.r + d), Mathf.Clamp01(c.g + d), Mathf.Clamp01(c.b + d), c.a);
    }
}
#endif
