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

            // ===== ตัวละคร 3D โชว์ด้านขวา (สไตล์ cinematic) =====
            // ลบเวทีตัวละครเก่าทั้งหมดก่อน (กันซ้ำจากการ rebuild หลายรอบ → ไฟซ้อนกันจนสว่างโอเวอร์)
            foreach (var oldStage in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (oldStage != null && oldStage.name == "MenuCharStage") Object.DestroyImmediate(oldStage.gameObject);

            var pStage = new GameObject("MenuCharStage"); pStage.transform.position = new Vector3(800f, 0f, 800f);
            var pRoot = new GameObject("ModelRoot").transform; pRoot.SetParent(pStage.transform, false); pRoot.localPosition = Vector3.zero;
            var pKey = new GameObject("Key").AddComponent<Light>(); pKey.transform.SetParent(pStage.transform, false); pKey.transform.localPosition = new Vector3(1.2f, 2.4f, 2f); pKey.type = LightType.Point; pKey.intensity = 3.2f; pKey.range = 24f; pKey.color = new Color(1f, 0.97f, 0.9f);
            var pFill = new GameObject("Fill").AddComponent<Light>(); pFill.transform.SetParent(pStage.transform, false); pFill.transform.localPosition = new Vector3(-1.4f, 1.6f, 1.8f); pFill.type = LightType.Point; pFill.intensity = 1.3f; pFill.range = 24f; pFill.color = new Color(0.85f, 0.9f, 1f);
            var pCamGo = new GameObject("MenuCharCam"); pCamGo.transform.SetParent(pStage.transform, false); pCamGo.transform.localPosition = new Vector3(0f, 1.0f, 3.0f);
            var pCam = pCamGo.AddComponent<Camera>(); pCam.transform.LookAt(pStage.transform.position + new Vector3(0f, 0.95f, 0f));
            pCam.clearFlags = CameraClearFlags.SolidColor; pCam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // โปร่งใส → เห็นฉากหลัง
            pCam.fieldOfView = 30f; pCam.nearClipPlane = 0.1f; pCam.farClipPlane = 12f; pCam.enabled = false;
            var praw = new GameObject("MenuCharPreview", typeof(RectTransform), typeof(RawImage));
            praw.transform.SetParent(root, false);
            var prrt = (RectTransform)praw.transform; prrt.anchorMin = prrt.anchorMax = prrt.pivot = new Vector2(0.5f, 0.5f);
            prrt.anchoredPosition = new Vector2(440f, -30f); prrt.sizeDelta = new Vector2(560f, 800f);
            var prawImg = praw.GetComponent<RawImage>(); prawImg.color = Color.white; prawImg.raycastTarget = false;
            var mcp = praw.AddComponent<MenuCharacterPreview>(); mcp.cam = pCam; mcp.root = pRoot; mcp.image = prawImg;

            // ===== แผงเลือกช่องบันทึก (popup — เด้งตอนกดเล่นคนเดียว/เล่นต่อ) =====
            var slDim = MakeImage(root, "SlotPanel", Vector2.zero, Vector2.one, new Color(0.04f, 0.06f, 0.12f, 0.62f));
            slDim.rectTransform.offsetMin = Vector2.zero; slDim.rectTransform.offsetMax = Vector2.zero;
            var slCard = MakeRounded(slDim.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 600), new Color(0.11f, 0.14f, 0.24f, 0.99f));
            Deco(slCard);
            MakeText(slCard.transform, "SlTitle", "เลือกช่องบันทึก", 42, new Vector2(0.5f, 1f), new Vector2(0, -46), new Vector2(560, 56), new Color(0.42f, 0.26f, 0.58f), FontStyles.Bold);
            var slotBtns = new Button[3];
            for (int i = 0; i < 3; i++)
                slotBtns[i] = MakeButton(slCard.transform, "ช่อง " + (i + 1), new Vector2(0f, 120f - i * 118f), new Color(0.72f, 0.82f, 0.72f));
            var slClose = MakeButton(slCard.transform, "ยกเลิก", new Vector2(0, 30), new Color(0.86f, 0.80f, 0.88f), 0.5f, 0f);
            mc.slotButtons = slotBtns;
            mc.slotPanel = slDim.gameObject;
            mc.slotCloseButton = slClose;
            slDim.gameObject.SetActive(false);

            // ===== แผงตั้งค่า (แท็บ เสียง/กราฟิก) =====
            var sDim = MakeImage(root, "SettingsPanel", Vector2.zero, Vector2.one, new Color(0.04f, 0.06f, 0.12f, 0.62f));
            sDim.rectTransform.offsetMin = Vector2.zero; sDim.rectTransform.offsetMax = Vector2.zero;

            // เงานุ่มใต้การ์ด (ทำให้การ์ดลอยเด่น มีมิติ) — วางก่อนการ์ดจะได้อยู่ด้านหลัง
            MakeRounded(sDim.transform, "CardShadow", new Vector2(0.5f, 0.5f), new Vector2(7, -28), new Vector2(796, 896), new Color(0.04f, 0.03f, 0.09f, 0.34f));

            // การ์ดใหญ่ขึ้น (กว้าง+สูง) + หัวข้อมีที่ว่างด้านบนเยอะ (ไม้โท/สระไม่โดนตัด)
            var sCard = MakeRounded(sDim.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(780, 880), new Color(0.11f, 0.14f, 0.24f, 0.99f));
            Deco(sCard);

            // แถบหัวการ์ด (โทนพีเรียงเกิลอ่อน = แยกชั้นหัว/เนื้อหา ให้มีมิติ)
            MakeRounded(sCard.transform, "HeaderBand", new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(740, 236), new Color(0.80f, 0.76f, 0.93f, 1f));
            // เส้นแบ่งบางใต้แถบหัว
            var sDiv = MakeRounded(sCard.transform, "HeaderDiv", new Vector2(0.5f, 1f), new Vector2(0, -246), new Vector2(700, 3), new Color(1f, 1f, 1f, 0.55f));
            sDiv.raycastTarget = false;

            MakeText(sCard.transform, "STitle", "ตั้งค่า", 48, new Vector2(0.5f, 1f), new Vector2(0, -62), new Vector2(640, 96), new Color(0.40f, 0.24f, 0.56f), FontStyles.Bold);

            var sc = sCard.gameObject.AddComponent<SettingsController>();
            sc.panel = sDim.gameObject;

            // แท็บ (บนแถบหัว)
            sc.audioTab    = MakeButton(sCard.transform, "เสียง", new Vector2(-162, -166), new Color(0.62f, 0.80f, 0.96f), 0.5f, 1f, 306f, 72f);
            sc.graphicsTab = MakeButton(sCard.transform, "กราฟิก", new Vector2(162, -166), new Color(0.80f, 0.72f, 0.96f), 0.5f, 1f, 306f, 72f);

            // กลุ่ม "เสียง"
            var audioGrp = MakeSettingsGroup(sCard.transform, "AudioGroup");
            sc.masterSlider  = LabeledSlider(audioGrp, "เสียงรวม (Master)", -300);
            sc.musicSlider   = LabeledSlider(audioGrp, "เพลง (Music)", -378);
            sc.sfxSlider     = LabeledSlider(audioGrp, "เอฟเฟกต์ (SFX)", -456);
            sc.voiceSlider   = LabeledSlider(audioGrp, "เสียงพูด (Voice)", -534);
            sc.ambientSlider = LabeledSlider(audioGrp, "บรรยากาศ (Ambient)", -612);
            sc.audioGroup = audioGrp.gameObject;

            // กลุ่ม "กราฟิก" — ตัวเลือกแบบเกมจริง: ◀ ค่า ▶ (คุณภาพ/ความละเอียด) + สวิตช์เปิด-ปิด (เต็มจอ)
            var gfxGrp = MakeSettingsGroup(sCard.transform, "GraphicsGroup");
            var qSel = MakeSelector(gfxGrp, "คุณภาพกราฟิก", -298);
            sc.qualityPrev = qSel.prev; sc.qualityNext = qSel.next; sc.qualityValue = qSel.val;
            var rSel = MakeSelector(gfxGrp, "ความละเอียด", -456);
            sc.resPrev = rSel.prev; sc.resNext = rSel.next; sc.resValue = rSel.val;
            var fsw = MakeToggle(gfxGrp, "โหมดเต็มจอ", -614);
            sc.fullscreenToggle = fsw.toggle; sc.fullscreenKnob = fsw.knob; sc.fullscreenTrack = fsw.track; sc.fullscreenValue = fsw.val; sc.knobOnX = fsw.onX;
            sc.graphicsGroup = gfxGrp.gameObject;

            sc.closeButton   = MakeButton(sCard.transform, "ปิด", new Vector2(0, 36), new Color(0.80f, 0.72f, 0.96f), 0.5f, 0f);

            mc.settingsPanel = sDim.gameObject;
            sDim.gameObject.SetActive(false);

            // ===== ป็อปอัป "กำลังพัฒนา" (dim + การ์ด) =====
            var csDim = MakeImage(root, "ComingSoonPanel", Vector2.zero, Vector2.one, new Color(0.04f, 0.06f, 0.12f, 0.62f));
            csDim.rectTransform.offsetMin = Vector2.zero; csDim.rectTransform.offsetMax = Vector2.zero;
            var csCard = MakeRounded(csDim.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680, 360), new Color(0.11f, 0.14f, 0.24f, 0.99f));
            Deco(csCard);
            MakeText(csCard.transform, "CSTitle", "กำลังพัฒนา", 46, new Vector2(0.5f, 1f), new Vector2(0, -48), new Vector2(640, 62), new Color(0.44f, 0.28f, 0.60f), FontStyles.Bold);
            var csBody = MakeText(csCard.transform, "CSBody", "โหมดเล่นหลายคน (Multiplayer)\nจะเปิดให้เล่นในเวอร์ชันถัดไป", 27, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(600, 110), new Color(0.92f, 0.94f, 0.88f), FontStyles.Normal);
            csBody.enableWordWrapping = true; csBody.alignment = TextAlignmentOptions.Center;
            var csClose = MakeButton(csCard.transform, "เข้าใจแล้ว", new Vector2(0, 30), new Color(0.80f, 0.72f, 0.96f), 0.5f, 0f);
            mc.comingSoonPanel = csDim.gameObject;
            mc.comingSoonCloseButton = csClose;
            csDim.gameObject.SetActive(false);

            // (เอาปุ่ม "เกี่ยวกับ / ผู้จัดทำ" ออกตามที่ผู้ใช้ขอ)
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

        static Button MakeButton(Transform parent, string label, Vector2 pos, Color col, float ax = 0.5f, float ay = 0.5f, float width = 360f, float height = 76f)
        {
            var size = new Vector2(width, height);
            var sprite = pill ?? ui;
            var anchor = new Vector2(ax, ay);

            // ฐานเงาเข้ม (หลังปุ่ม)
            var baseGo = new GameObject(label + "BtnBase", typeof(Image));   // ชื่อให้ตรงกับที่ MenuPolish หา (…BtnBase) จะได้ย้ายตามปุ่มไปซ้าย
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
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(14, 0); trt.offsetMax = new Vector2(-14, 0);   // เว้นขอบซ้าย-ขวา กันชิดขอบ
            txt.enableWordWrapping = false;                                            // บรรทัดเดียวเสมอ (กันตกบรรทัด เช่น "1920×1080")
            txt.enableAutoSizing = true; txt.fontSizeMin = 20; txt.fontSizeMax = 34;   // ยาวเกินให้ย่อลงพอดี
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
            var bg = go.GetComponent<Image>(); bg.sprite = round ?? ui; bg.type = Image.Type.Sliced; bg.color = new Color(0.05f, 0.06f, 0.12f, 0.7f);
            var bgOl = go.AddComponent<Outline>(); bgOl.effectColor = new Color(0f, 0f, 0f, 0.5f); bgOl.effectDistance = new Vector2(0f, -2f); bgOl.useGraphicAlpha = false;
            var slider = go.GetComponent<Slider>();

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fart = (RectTransform)fillArea.transform;
            fart.anchorMin = new Vector2(0, 0f); fart.anchorMax = new Vector2(1, 1f);
            fart.offsetMin = new Vector2(4, 4); fart.offsetMax = new Vector2(-4, -4);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fImg = fill.GetComponent<Image>(); fImg.sprite = round ?? ui; fImg.type = Image.Type.Sliced; fImg.color = new Color(0.36f, 0.88f, 0.54f);
            ((RectTransform)fill.transform).sizeDelta = new Vector2(10, 0);
            // แสงวาวบน fill
            if (gloss != null)
            {
                var fg = new GameObject("FillGloss", typeof(RectTransform), typeof(Image));
                fg.transform.SetParent(fill.transform, false);
                var fgi = fg.GetComponent<Image>(); fgi.sprite = gloss; fgi.color = new Color(1f, 1f, 1f, 0.35f); fgi.raycastTarget = false;
                var fgrt = (RectTransform)fg.transform; fgrt.anchorMin = new Vector2(0, 0.5f); fgrt.anchorMax = Vector2.one; fgrt.offsetMin = Vector2.zero; fgrt.offsetMax = Vector2.zero;
            }

            var hsa = new GameObject("Handle Slide Area", typeof(RectTransform));
            hsa.transform.SetParent(go.transform, false);
            var hart = (RectTransform)hsa.transform;
            hart.anchorMin = new Vector2(0, 0); hart.anchorMax = new Vector2(1, 1);
            hart.offsetMin = new Vector2(16, 0); hart.offsetMax = new Vector2(-16, 0);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(hsa.transform, false);
            var hImg = handle.GetComponent<Image>(); hImg.sprite = circle ?? ui; hImg.color = Color.white;
            var hSh = handle.AddComponent<Shadow>(); hSh.effectColor = new Color(0f, 0f, 0f, 0.4f); hSh.effectDistance = new Vector2(0f, -3f);
            var hOl = handle.AddComponent<Outline>(); hOl.effectColor = new Color(0.30f, 0.72f, 0.46f, 0.9f); hOl.effectDistance = new Vector2(1.5f, -1.5f); hOl.useGraphicAlpha = false;
            var hrt = (RectTransform)handle.transform; hrt.sizeDelta = new Vector2(40, 0);

            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = hrt;
            slider.targetGraphic = hImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            return slider;
        }

        // แถวตัวเลือกแบบเกม: หัวข้อ + ◀ [กล่องค่า] ▶  (คืนปุ่มซ้าย/ขวา + ข้อความค่า)
        static (Button prev, Button next, TMP_Text val) MakeSelector(Transform parent, string title, float y)
        {
            MakeText(parent, title + "Head", title, 27, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(560, 36),
                new Color(0.34f, 0.28f, 0.50f), FontStyles.Bold);
            float ly = y - 54;                                   // บรรทัดตัวเลือกอยู่ใต้หัวข้อ
            var prev = MakeButton(parent, "<", new Vector2(-274, ly), new Color(0.62f, 0.80f, 0.96f), 0.5f, 1f, 84f, 84f);
            var next = MakeButton(parent, ">", new Vector2(274, ly), new Color(0.62f, 0.80f, 0.96f), 0.5f, 1f, 84f, 84f);

            // กล่องค่าตรงกลาง (ขาวนวล มีกรอบ + เงาใน = มีมิติ) — ธีมพาสเทลไม่แตะ (สว่างอยู่แล้ว)
            var box = MakeRounded(parent, title + "Box", new Vector2(0.5f, 1f), new Vector2(0, ly), new Vector2(392, 84), new Color(0.99f, 0.99f, 1f, 1f));
            var bOl = box.gameObject.AddComponent<Outline>(); bOl.effectColor = new Color(0.55f, 0.5f, 0.72f, 0.95f); bOl.effectDistance = new Vector2(2.5f, -2.5f); bOl.useGraphicAlpha = false;
            var bSh = box.gameObject.AddComponent<Shadow>(); bSh.effectColor = new Color(0.12f, 0.10f, 0.22f, 0.22f); bSh.effectDistance = new Vector2(0, -4);
            var val = MakeText(box.transform, "Val", "-", 32, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, 66), new Color(0.30f, 0.25f, 0.46f), FontStyles.Bold);
            val.rectTransform.anchorMin = Vector2.zero; val.rectTransform.anchorMax = Vector2.one; val.rectTransform.offsetMin = new Vector2(10, 0); val.rectTransform.offsetMax = new Vector2(-10, 0);
            val.enableWordWrapping = false; val.enableAutoSizing = true; val.fontSizeMin = 18; val.fontSizeMax = 32;
            return (prev, next, val);
        }

        // สวิตช์ เปิด/ปิด: หัวข้อ + ราง + ลูกบิดเลื่อน + คำว่า เปิด/ปิด (คืนปุ่มราง, ลูกบิด, รูปราง, ข้อความ, ระยะเลื่อน)
        static (Button toggle, RectTransform knob, Image track, TMP_Text val, float onX) MakeToggle(Transform parent, string title, float y)
        {
            MakeText(parent, title + "Head", title, 27, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(560, 36),
                new Color(0.34f, 0.28f, 0.50f), FontStyles.Bold);
            float ly = y - 52;
            // ราง (เป็นปุ่มกดสลับ) — กว้าง <160 กันตัวติดตั้ง juice ไปแต่งทับ
            var trackGo = new GameObject(title + "Track", typeof(Image), typeof(Button));
            trackGo.transform.SetParent(parent, false);
            var track = trackGo.GetComponent<Image>();
            track.sprite = pill ?? ui; track.type = Image.Type.Sliced; track.color = new Color(0.55f, 0.55f, 0.62f);
            var trt = track.rectTransform; trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 1f); trt.anchoredPosition = new Vector2(-90, ly); trt.sizeDelta = new Vector2(150, 64);
            var trOl = trackGo.AddComponent<Outline>(); trOl.effectColor = new Color(0f, 0f, 0f, 0.25f); trOl.effectDistance = new Vector2(0, -2); trOl.useGraphicAlpha = false;
            var toggle = trackGo.GetComponent<Button>(); toggle.transition = Selectable.Transition.None;

            var knobGo = new GameObject("Knob", typeof(Image));
            knobGo.transform.SetParent(trackGo.transform, false);
            var knobImg = knobGo.GetComponent<Image>(); knobImg.sprite = circle ?? ui; knobImg.color = Color.white; knobImg.raycastTarget = false;
            var krt = knobImg.rectTransform; krt.anchorMin = krt.anchorMax = krt.pivot = new Vector2(0.5f, 0.5f); krt.sizeDelta = new Vector2(52, 52); krt.anchoredPosition = new Vector2(-45, 0);
            var kSh = knobGo.AddComponent<Shadow>(); kSh.effectColor = new Color(0f, 0f, 0f, 0.35f); kSh.effectDistance = new Vector2(0, -2);

            var val = MakeText(parent, title + "Val", "ปิด", 30, new Vector2(0.5f, 1f), new Vector2(150, ly), new Vector2(180, 64), new Color(0.34f, 0.28f, 0.50f), FontStyles.Bold);
            return (toggle, krt, track, val, 45f);
        }

        // กลุ่มว่างเต็มการ์ด (ไว้สลับแท็บ)
        static Transform MakeSettingsGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go.transform;
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
