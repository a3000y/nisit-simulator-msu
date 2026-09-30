#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.UI;
using NisitSimulator.Systems;

namespace NisitSimulator.EditorTools
{
    // 🧑‍🎨 สร้างหน้าแต่งตัวละคร (เลือกแบบ/เพศ + สี + ชื่อ) พร้อมพรีวิว 3D หมุนได้ ในเมนูหลัก
    //   ต้องมี CharacterCatalog ใน Resources ก่อน (สร้างโดย Nisit -> Setup Multiplayer)
    //   ใช้: เมนู Nisit -> Build Character Creator  (อยู่ใน ★ Rebuild All ด้วย)
    public static class M37CharacterCreator
    {
        public static bool SuppressDialog = false;
        const string MenuPath = "Assets/Scenes/Scene1.unity";
        const string FontPath = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        static readonly Vector3 StagePos = new Vector3(1000f, 0f, 1000f);

        [MenuItem("Nisit/Build Character Creator", false, 36)]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);

            var mc = Object.FindFirstObjectByType<MainMenuController>();
            if (mc == null)
            {
                if (!SuppressDialog) EditorUtility.DisplayDialog("Nisit", "ไม่พบเมนูหลัก — กด Build M4 Menu ก่อน", "OK");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var cat = Resources.Load<CharacterCatalog>("CharacterCatalog");
            if (cat == null && !SuppressDialog)
                Debug.LogWarning("[Nisit] ยังไม่มี CharacterCatalog ใน Resources — กด Setup Multiplayer ก่อน (หน้าแต่งตัวจะยังว่าง)");

            // ลบของเก่า (กันซ้ำตอน rebuild)
            var oldStage = GameObject.Find("CharPreviewStage"); if (oldStage != null) Object.DestroyImmediate(oldStage);
            var oldPanel = FindChild(mc.transform, "Character Panel"); if (oldPanel != null) Object.DestroyImmediate(oldPanel);

            // ===== เวที 3D (นอก Canvas, วางไกล ๆ ไม่ให้ชนฉากเมนู) =====
            var stage = new GameObject("CharPreviewStage");
            stage.transform.position = StagePos;

            var modelRoot = new GameObject("ModelRoot").transform;
            modelRoot.SetParent(stage.transform, false);
            modelRoot.localPosition = Vector3.zero;

            var key = new GameObject("Key Light").AddComponent<Light>();
            key.transform.SetParent(stage.transform, false);
            key.transform.localPosition = new Vector3(1.2f, 2.4f, 2.0f);
            key.type = LightType.Point; key.intensity = 9f; key.range = 30f; key.color = new Color(1f, 0.97f, 0.9f);

            var fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.transform.SetParent(stage.transform, false);
            fill.transform.localPosition = new Vector3(-1.4f, 1.6f, 1.8f);
            fill.type = LightType.Point; fill.intensity = 4.5f; fill.range = 30f; fill.color = new Color(0.85f, 0.9f, 1f);

            var camGo = new GameObject("CharPreviewCam");
            camGo.transform.SetParent(stage.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0.98f, 3.3f);   // เผื่อหมวก/ผมยาวไม่ให้หัวตกขอบ
            var cam = camGo.AddComponent<Camera>();
            cam.transform.LookAt(stage.transform.position + new Vector3(0f, 0.90f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.83f, 0.95f);   // พื้นพรีวิวพาสเทล
            cam.fieldOfView = 30f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 12f;
            cam.enabled = false;   // controller เปิดตอนโชว์หน้า + ต่อ RenderTexture

            EnsureAccessoryCatalog();

            // ===== แผงแต่งตัว (ใน Menu Canvas) — กริดแบบ mockup =====
            var root = mc.transform;
            var dim = MakeImage(root, "Character Panel", Vector2.zero, Vector2.one, new Color(0.16f, 0.14f, 0.26f, 0.28f));
            dim.rectTransform.offsetMin = Vector2.zero; dim.rectTransform.offsetMax = Vector2.zero;

            var card = MakeCard(dim.transform, new Vector2(1520f, 884f), new Color(0.13f, 0.16f, 0.26f, 0.99f));
            var lblCol = Color.white;
            var titleCol = new Color(0.42f, 0.26f, 0.58f);

            // แถบหัว + ชื่อหน้า
            var header = MakeImage(card.transform, "Header", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.82f, 0.78f, 0.94f, 1f));
            var hround = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_round.png");
            if (hround != null) { header.sprite = hround; header.type = Image.Type.Sliced; }
            header.raycastTarget = false;
            header.rectTransform.anchoredPosition = new Vector2(0, 372f); header.rectTransform.sizeDelta = new Vector2(720f, 104f);
            MakeText(card.transform, font, "สร้างนิสิตใหม่", new Vector2(0f, 374f), new Vector2(1200, 60), 46, titleCol, TextAlignmentOptions.Center);

            // ชื่อผู้เล่น (กลางบน)
            MakeText(card.transform, font, "ชื่อผู้เล่น", new Vector2(0f, 278f), new Vector2(420, 26), 22, lblCol, TextAlignmentOptions.Center);
            var nameInput = MakeInput(card.transform, font, "ใส่ชื่อ...", new Vector2(0f, 238f), new Vector2(420f, 48f));

            // กรอบพรีวิว (เฟรมมนขาว + เงา) — ให้ตัวละครดูเป็นภาพในกรอบ
            var pframe = new GameObject("PreviewFrame", typeof(RectTransform), typeof(Image));
            pframe.transform.SetParent(card.transform, false);
            var pfImg = pframe.GetComponent<Image>(); pfImg.color = new Color(0.995f, 0.99f, 1f, 1f); pfImg.raycastTarget = false;
            var pfRound = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_round.png");
            if (pfRound != null) { pfImg.sprite = pfRound; pfImg.type = Image.Type.Sliced; }
            var pfrt = pfImg.rectTransform; pfrt.anchorMin = pfrt.anchorMax = pfrt.pivot = new Vector2(0.5f, 0.5f);
            pfrt.anchoredPosition = new Vector2(0f, -30f); pfrt.sizeDelta = new Vector2(438f, 500f);
            var pfOl = pframe.AddComponent<UnityEngine.UI.Outline>(); pfOl.effectColor = new Color(0.6f, 0.55f, 0.78f, 0.5f); pfOl.effectDistance = new Vector2(2f, -2f); pfOl.useGraphicAlpha = false;
            var pfSh = pframe.AddComponent<UnityEngine.UI.Shadow>(); pfSh.effectColor = new Color(0.28f, 0.24f, 0.44f, 0.28f); pfSh.effectDistance = new Vector2(0f, -8f);

            // พรีวิว 3D (กลาง)
            var raw = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            raw.transform.SetParent(card.transform, false);
            var rrt = (RectTransform)raw.transform; rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(0f, -30f); rrt.sizeDelta = new Vector2(400f, 464f);
            var rawImg = raw.GetComponent<RawImage>(); rawImg.color = Color.white;

            // ----- คอลัมน์ซ้าย: เพศ / ชุด / ทรงผม / สีผม -----
            const float LX = -500f, RX = 500f, CW = 470f;
            var genderBtns = new Button[2];
            genderBtns[0] = MakeButton(card.transform, font, "ชาย", new Vector2(LX - 118f, 290f), new Vector2(226f, 50f), new Color(0.62f, 0.78f, 0.98f), 24);
            genderBtns[1] = MakeButton(card.transform, font, "หญิง", new Vector2(LX + 118f, 290f), new Vector2(226f, 50f), new Color(0.99f, 0.72f, 0.84f), 24);
            foreach (var gb in genderBtns) AddRing(gb.transform, 226f);
            var pModel = MakePanel(card.transform, font, "ชุด", new Vector2(LX, 125f), new Vector2(CW, 250f));
            var modelBtns = Grid(pModel, 250f, 10, 5, new Vector2(80f, 86f), new Vector2(10f, 8f), true, font);
            var pHair = MakePanel(card.transform, font, "ทรงผม", new Vector2(LX, -109f), new Vector2(CW, 194f));
            var hairBtns = Grid(pHair, 194f, 16, 8, new Vector2(50f, 56f), new Vector2(6f, 6f), true, font);
            var pHairCol = MakePanel(card.transform, font, "สีผม", new Vector2(LX, -280f), new Vector2(CW, 124f));
            var hairColBtns = SwatchRow(pHairCol, 124f, 8, 8, 42f, 12f);

            // ----- คอลัมน์ขวา: โทนสีชุด / สีผิว / หมวก / แว่นตา + หนวด -----
            var pColor = MakePanel(card.transform, font, "โทนสีชุด", new Vector2(RX, 223f), new Vector2(CW, 190f));
            var colorBtns = SwatchRow(pColor, 190f, 12, 6, 48f, 16f);
            var pSkin = MakePanel(card.transform, font, "สีผิว", new Vector2(RX, 54f), new Vector2(CW, 124f));
            var skinBtns = SwatchRow(pSkin, 124f, 5, 5, 44f, 18f);
            var pHat = MakePanel(card.transform, font, "หมวก", new Vector2(RX, -87f), new Vector2(CW, 134f));
            var hatBtns = Grid(pHat, 134f, 8, 8, new Vector2(50f, 56f), new Vector2(6f, 6f), true, font);
            var pGlass = MakePanel(card.transform, font, "แว่นตา", new Vector2(RX - 160f, -233f), new Vector2(150f, 134f));
            var glassBtns = Grid(pGlass, 134f, 2, 2, new Vector2(50f, 56f), new Vector2(8f, 6f), true, font);
            var pBeard = MakePanel(card.transform, font, "หนวด/เครา", new Vector2(RX + 80f, -233f), new Vector2(310f, 134f));
            var beardBtns = Grid(pBeard, 134f, 5, 5, new Vector2(50f, 56f), new Vector2(6f, 6f), true, font);

            // ลำดับต้องตรงกับ AccessoryCatalog (Setup Synty Characters): ทรงผม / หมวก / แว่นตา / หนวด-เครา
            var accSlots = new CharacterCreatorController.AccessorySlotUI[] {
                new CharacterCreatorController.AccessorySlotUI { buttons = hairBtns },
                new CharacterCreatorController.AccessorySlotUI { buttons = hatBtns },
                new CharacterCreatorController.AccessorySlotUI { buttons = glassBtns },
                new CharacterCreatorController.AccessorySlotUI { buttons = beardBtns },
            };

            // ----- ระดับความยาก (กลาง ใต้พรีวิว) -----
            MakeText(card.transform, font, "ระดับความยาก", new Vector2(0f, -304f), new Vector2(420, 26), 20, lblCol, TextAlignmentOptions.Center);
            var diffBtns = new Button[3];
            string[] diffNames = { "ง่าย", "ปกติ", "ยาก" };
            Color[] diffCols = { new Color(0.60f, 0.86f, 0.68f), new Color(0.62f, 0.80f, 0.96f), new Color(0.99f, 0.66f, 0.62f) };
            for (int i = 0; i < 3; i++)
                diffBtns[i] = MakeButton(card.transform, font, diffNames[i], new Vector2((i - 1) * 132f, -338f), new Vector2(122f, 42f), diffCols[i], 20);

            // ----- ปุ่มล่าง: สุ่ม / เริ่มเล่น / ย้อนกลับ -----
            var randomBtn = MakeButton(card.transform, font, "สุ่ม", new Vector2(-470f, -406f), new Vector2(300f, 62f), new Color(0.99f, 0.86f, 0.6f), 26);
            var confirm   = MakeButton(card.transform, font, "เริ่มเล่น", new Vector2(0f, -406f), new Vector2(360f, 66f), new Color(0.60f, 0.86f, 0.68f), 28);
            var back      = MakeButton(card.transform, font, "ย้อนกลับ", new Vector2(470f, -406f), new Vector2(300f, 62f), new Color(0.86f, 0.80f, 0.88f), 26);

            // ===== ต่อ controller + เมนู =====
            var cc = card.gameObject.AddComponent<CharacterCreatorController>();
            cc.nameInput = nameInput; cc.modelButtons = modelBtns; cc.colorButtons = colorBtns;
            cc.hairColorButtons = hairColBtns; cc.skinButtons = skinBtns;
            cc.genderButtons = genderBtns;
            cc.hairOnlyPanels = new[] { pHair.gameObject, pHairCol.gameObject, pBeard.gameObject };
            cc.accessorySlots = accSlots; cc.difficultyButtons = diffBtns; cc.randomButton = randomBtn;
            cc.previewCamera = cam; cc.previewRoot = modelRoot; cc.previewImage = rawImg;

            mc.characterPanel = dim.gameObject;
            mc.characterConfirmButton = confirm;
            mc.characterBackButton = back;
            dim.gameObject.SetActive(false);

            EditorUtility.SetDirty(mc);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] สร้างหน้าแต่งตัวละคร (พรีวิว 3D) แล้ว</color>");
            if (!SuppressDialog)
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "สร้างหน้าแต่งตัวละครแล้ว! 🧑‍🎨\n\nกด 'เล่นคนเดียว' → เลือกชุด/ทรงผม/สีผม/สีผิว/โทนสีชุด + หมวก/แว่น/หนวด (พรีวิว 3D หมุนได้) → เริ่มเล่น\n\n* ตัวละคร Synty: Nisit ▸ Setup Synty Characters", "เยี่ยม!");
        }

        // สร้างโครง AccessoryCatalog เริ่มต้น (4 ช่องว่าง) ถ้ายังไม่มี — ผู้ใช้ลาก prop ใส่ options[] เอง
        static void EnsureAccessoryCatalog()
        {
            const string path = "Assets/_Project/Resources/AccessoryCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<AccessoryCatalog>(path) != null) return;   // มีแล้ว → ไม่ทับของที่ใส่ไว้

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
                AssetDatabase.CreateFolder("Assets/_Project", "Resources");

            var cat = ScriptableObject.CreateInstance<AccessoryCatalog>();
            cat.slots = new[]
            {
                new AccessoryCatalog.Slot { slotName = "หมวก",      bone = HumanBodyBones.Head,      posOffset = new Vector3(0f, 0.12f, 0f),    eulerOffset = Vector3.zero,             scale = 1f, options = new GameObject[0], labels = new string[0] },
                new AccessoryCatalog.Slot { slotName = "แว่นตา",    bone = HumanBodyBones.Head,      posOffset = new Vector3(0f, 0.04f, 0.09f), eulerOffset = Vector3.zero,             scale = 1f, options = new GameObject[0], labels = new string[0] },
                new AccessoryCatalog.Slot { slotName = "กระเป๋าเป้", bone = HumanBodyBones.Spine,     posOffset = new Vector3(0f, 0.10f, -0.12f),eulerOffset = new Vector3(0f, 180f, 0f),scale = 1f, options = new GameObject[0], labels = new string[0] },
                new AccessoryCatalog.Slot { slotName = "ของถือ",    bone = HumanBodyBones.RightHand, posOffset = Vector3.zero,                  eulerOffset = Vector3.zero,             scale = 1f, options = new GameObject[0], labels = new string[0] },
            };
            AssetDatabase.CreateAsset(cat, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[Nisit] สร้าง AccessoryCatalog เริ่มต้น (4 ช่องว่าง) — ลากโมเดล prop ใส่ options[] ของแต่ละช่องใน Inspector");
        }

        // ---------- helpers ----------
        static GameObject FindChild(Transform parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        static Image MakeImage(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = col;
            var rt = img.rectTransform; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        static RectTransform MakeCard(Transform parent, Vector2 size, Color col)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = col;
            var round = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_round.png");
            if (round != null) { img.sprite = round; img.type = Image.Type.Sliced; }
            var ol = go.AddComponent<UnityEngine.UI.Outline>(); ol.effectColor = new Color(0.10f, 0.12f, 0.22f, 1f); ol.effectDistance = new Vector2(4f, -4f); ol.useGraphicAlpha = false;
            var sh = go.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.35f); sh.effectDistance = new Vector2(4f, -8f);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size;
            return rt;
        }

        static TMP_Text MakeText(Transform parent, TMP_FontAsset font, string s, Vector2 pos, Vector2 size, float fs, Color col, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = s; t.fontSize = fs; t.color = col; t.alignment = align;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return t;
        }

        static Button MakeButton(Transform parent, TMP_FontAsset font, string label, Vector2 pos, Vector2 size, Color col, float fs)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = col;
            var t = MakeText(go.transform, font, label, Vector2.zero, size, fs, new Color(0.15f, 0.17f, 0.28f), TextAlignmentOptions.Center);
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }

        static Button MakeSwatch(Transform parent, Vector2 pos, float d)
        {
            var go = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(d, d);
            var img = go.GetComponent<Image>(); img.color = Color.white;
            var round = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_round.png");
            if (round != null) { img.sprite = round; img.type = Image.Type.Sliced; }
            var ol = go.AddComponent<UnityEngine.UI.Outline>(); ol.effectColor = new Color(1f, 1f, 1f, 0.9f); ol.effectDistance = new Vector2(2f, -2f); ol.useGraphicAlpha = false;
            var sh = go.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0.25f, 0.22f, 0.4f, 0.28f); sh.effectDistance = new Vector2(0f, -3f);
            return go.GetComponent<Button>();
        }

        // แผงหมวด (กล่องพาสเทลมีแถบหัว + เงา = มีมิติ) — คืน transform ไว้วางช่องข้างใน
        static RectTransform MakePanel(Transform parent, TMP_FontAsset font, string title, Vector2 pos, Vector2 size)
        {
            var round = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_round.png");

            var go = new GameObject("Panel_" + title, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = new Color(0.905f, 0.875f, 0.975f, 1f);   // ตัวแผงอ่อนนุ่ม
            if (round != null) { img.sprite = round; img.type = Image.Type.Sliced; }
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var sh = go.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0.30f, 0.26f, 0.46f, 0.22f); sh.effectDistance = new Vector2(0f, -6f);

            // แถบหัวแผง (พิลล์เข้มขึ้นเล็กน้อย ให้เป็นชั้น)
            var strip = new GameObject("Head", typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(go.transform, false);
            var simg = strip.GetComponent<Image>(); simg.color = new Color(0.78f, 0.71f, 0.93f, 1f); simg.raycastTarget = false;
            if (round != null) { simg.sprite = round; simg.type = Image.Type.Sliced; }
            var srt = simg.rectTransform; srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, -9f); srt.sizeDelta = new Vector2(size.x - 26f, 42f);
            MakeText(strip.transform, font, title, Vector2.zero, new Vector2(size.x - 34f, 40f), 21, new Color(0.30f, 0.25f, 0.46f), TextAlignmentOptions.Center);
            return rt;
        }

        // ช่องเลือกแบบ "รูป" (image slot) — ผู้ใช้เอา sprite มาใส่ทีหลังได้ · caption ล่างให้ controller เขียนชื่อ
        static Button MakeSlot(Transform parent, TMP_FontAsset font, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>(); img.color = new Color(0.99f, 0.99f, 1f);
            var round = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_round.png");
            if (round != null) { img.sprite = round; img.type = Image.Type.Sliced; }
            var ol = go.AddComponent<UnityEngine.UI.Outline>(); ol.effectColor = new Color(0.55f, 0.5f, 0.72f, 0.55f); ol.effectDistance = new Vector2(2f, -2f); ol.useGraphicAlpha = false;
            var sh = go.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0.25f, 0.22f, 0.4f, 0.22f); sh.effectDistance = new Vector2(0f, -3f);
            var cap = MakeText(go.transform, font, "", new Vector2(0, -size.y / 2f + 13f), new Vector2(size.x - 6f, 24f), 14, new Color(0.30f, 0.25f, 0.46f), TextAlignmentOptions.Center);
            cap.raycastTarget = false; cap.enableWordWrapping = false; cap.overflowMode = TextOverflowModes.Ellipsis;
            return go.GetComponent<Button>();
        }

        // แถวช่องรูป n ช่อง (สำหรับของแต่ง: ไม่ใส่ + option) — เว้นระยะเท่ากันอัตโนมัติตามจำนวน
        static Button[] MakeSlotRow(Transform panel, TMP_FontAsset font, int n)
        {
            var arr = new Button[n];
            float slotW = n <= 4 ? 82f : 56f;
            float gap = 8f;
            float total = n * slotW + (n - 1) * gap;
            float x0 = -total / 2f + slotW / 2f;
            for (int i = 0; i < n; i++)
                arr[i] = MakeSlot(panel, font, new Vector2(x0 + i * (slotW + gap), -16f), new Vector2(slotW, slotW * 0.95f));
            return arr;
        }

        // กริดช่องรูป (ไอคอน + ชื่อเล็ก ๆ ด้านล่าง + วงเลือก) ใต้แถบหัวแผง · เรียงซ้าย→ขวา บน→ล่าง จัดกลาง
        static Button[] Grid(RectTransform panel, float panelH, int n, int cols, Vector2 cell, Vector2 gap, bool caption, TMP_FontAsset font)
        {
            var arr = new Button[n];
            int rows = Mathf.CeilToInt(n / (float)cols);
            float totalW = cols * cell.x + (cols - 1) * gap.x;
            float top = panelH / 2f - 60f;
            for (int i = 0; i < n; i++)
            {
                int r = i / cols, c = i % cols;
                int inRow = Mathf.Min(cols, n - r * cols);
                float rowW = inRow * cell.x + (inRow - 1) * gap.x;
                float x = -rowW / 2f + cell.x / 2f + c * (cell.x + gap.x);
                float y = top - cell.y / 2f - r * (cell.y + gap.y);
                arr[i] = MakeIconSlot(panel, font, new Vector2(x, y), cell, caption);
            }
            return arr;
        }

        static Button[] SwatchRow(RectTransform panel, float panelH, int n, int cols, float d, float gap)
        {
            var arr = new Button[n];
            float top = panelH / 2f - 62f;
            for (int i = 0; i < n; i++)
            {
                int r = i / cols, c = i % cols;
                int inRow = Mathf.Min(cols, n - r * cols);
                float rowW = inRow * d + (inRow - 1) * gap;
                arr[i] = MakeSwatch(panel, new Vector2(-rowW / 2f + d / 2f + c * (d + gap), top - d / 2f - r * (d + gap)), d);
                AddRing(arr[i].transform, d);
            }
            return arr;
        }

        static Button MakeIconSlot(Transform parent, TMP_FontAsset font, Vector2 pos, Vector2 size, bool caption)
        {
            var round = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_round.png");
            var go = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>(); img.color = new Color(0.99f, 0.99f, 1f);
            if (round != null) { img.sprite = round; img.type = Image.Type.Sliced; }
            var sh = go.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0.25f, 0.22f, 0.4f, 0.22f); sh.effectDistance = new Vector2(0f, -3f);
            // ไอคอน (ซ่อนจนกว่า controller จะใส่รูป)
            var ic = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            ic.transform.SetParent(go.transform, false);
            var icImg = ic.GetComponent<Image>(); icImg.raycastTarget = false; icImg.preserveAspect = true; icImg.enabled = false;
            var irt = icImg.rectTransform; irt.anchorMin = new Vector2(0f, caption ? 0.18f : 0f); irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(3f, 1f); irt.offsetMax = new Vector2(-3f, -3f);

            if (caption)
            {
                float fs = size.x >= 70f ? 14f : 10f;
                var cap = MakeText(go.transform, font, "", Vector2.zero, Vector2.zero, fs, new Color(0.30f, 0.25f, 0.46f), TextAlignmentOptions.Bottom);
                var crt = cap.rectTransform; crt.anchorMin = Vector2.zero; crt.anchorMax = new Vector2(1f, 1f); crt.offsetMin = new Vector2(1f, 1f); crt.offsetMax = new Vector2(-1f, -1f);
                cap.raycastTarget = false; cap.enableWordWrapping = false; cap.overflowMode = TextOverflowModes.Ellipsis;
                cap.enableAutoSizing = true; cap.fontSizeMin = 8f; cap.fontSizeMax = fs;
            }
            AddRing(go.transform, Mathf.Max(size.x, size.y));
            return go.GetComponent<Button>();
        }

        // กรอบไฮไลต์ตอนเลือก (4 แถบรอบปุ่ม ไม่ทับสี/ไอคอน) — controller เปิด/ปิดลูกชื่อ Selected
        static void AddRing(Transform t, float size)
        {
            var ring = new GameObject("Selected", typeof(RectTransform));
            ring.transform.SetParent(t, false);
            var rt = (RectTransform)ring.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-5f, -5f); rt.offsetMax = new Vector2(5f, 5f);
            var gold = new Color(1f, 0.70f, 0.18f, 1f);
            const float th = 4f;
            Bar(ring.transform, gold, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -th), Vector2.zero);   // บน
            Bar(ring.transform, gold, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, th));    // ล่าง
            Bar(ring.transform, gold, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(th, 0f));    // ซ้าย
            Bar(ring.transform, gold, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-th, 0f), Vector2.zero);   // ขวา
            ring.SetActive(false);
        }

        static void Bar(Transform parent, Color col, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>(); im.color = col; im.raycastTarget = false;
            var rt = im.rectTransform; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        }

        static TMP_InputField MakeInput(Transform parent, TMP_FontAsset font, string placeholder, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);
            var input = go.GetComponent<TMP_InputField>();

            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(go.transform, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = new Vector2(14, 7); art.offsetMax = new Vector2(-14, -7);

            var ph = new GameObject("Placeholder", typeof(RectTransform)); ph.transform.SetParent(area.transform, false);
            var pht = ph.AddComponent<TextMeshProUGUI>(); if (font != null) pht.font = font;
            pht.text = placeholder; pht.fontSize = 26; pht.color = new Color(0.35f, 0.35f, 0.4f, 0.6f); pht.alignment = TextAlignmentOptions.Left;
            var phrt = (RectTransform)ph.transform; phrt.anchorMin = Vector2.zero; phrt.anchorMax = Vector2.one; phrt.offsetMin = phrt.offsetMax = Vector2.zero;

            var txt = new GameObject("Text", typeof(RectTransform)); txt.transform.SetParent(area.transform, false);
            var tt = txt.AddComponent<TextMeshProUGUI>(); if (font != null) tt.font = font;
            tt.fontSize = 26; tt.color = new Color(0.12f, 0.13f, 0.18f); tt.alignment = TextAlignmentOptions.Left;
            var ttrt = (RectTransform)txt.transform; ttrt.anchorMin = Vector2.zero; ttrt.anchorMax = Vector2.one; ttrt.offsetMin = ttrt.offsetMax = Vector2.zero;

            input.textViewport = art; input.textComponent = tt; input.placeholder = pht;
            input.fontAsset = font; input.pointSize = 26; input.characterLimit = 16;
            return input;
        }
    }
}
#endif
