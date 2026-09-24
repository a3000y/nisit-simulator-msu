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
            key.type = LightType.Point; key.intensity = 14f; key.range = 30f; key.color = new Color(1f, 0.97f, 0.9f);

            var fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.transform.SetParent(stage.transform, false);
            fill.transform.localPosition = new Vector3(-1.4f, 1.6f, 1.8f);
            fill.type = LightType.Point; fill.intensity = 7f; fill.range = 30f; fill.color = new Color(0.85f, 0.9f, 1f);

            var camGo = new GameObject("CharPreviewCam");
            camGo.transform.SetParent(stage.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.0f, 2.7f);
            var cam = camGo.AddComponent<Camera>();
            cam.transform.LookAt(stage.transform.position + new Vector3(0f, 0.95f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.20f, 0.24f, 0.34f);
            cam.fieldOfView = 30f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 12f;
            cam.enabled = false;   // controller เปิดตอนโชว์หน้า + ต่อ RenderTexture

            EnsureAccessoryCatalog();

            // ===== แผงแต่งตัว (ใน Menu Canvas) =====
            var root = mc.transform;
            var dim = MakeImage(root, "Character Panel", Vector2.zero, Vector2.one, new Color(0.05f, 0.07f, 0.13f, 0.75f));
            dim.rectTransform.offsetMin = Vector2.zero; dim.rectTransform.offsetMax = Vector2.zero;

            var card = MakeCard(dim.transform, new Vector2(1260f, 830f), new Color(0.13f, 0.16f, 0.26f, 0.99f));

            MakeText(card.transform, font, "แต่งตัวละคร", new Vector2(0f, 380f), new Vector2(900, 60), 44, new Color(1f, 0.9f, 0.5f), TextAlignmentOptions.Center);

            // พรีวิว (ซ้าย)
            var raw = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            raw.transform.SetParent(card.transform, false);
            var rrt = (RectTransform)raw.transform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(-395f, -10f); rrt.sizeDelta = new Vector2(420f, 630f);
            var rawImg = raw.GetComponent<RawImage>(); rawImg.color = Color.white;

            float rx = 155f;   // จุดกึ่งกลางคอลัมน์ขวา
            var lblCol = Color.white;

            // ชื่อ
            MakeText(card.transform, font, "ชื่อผู้เล่น", new Vector2(rx, 328f), new Vector2(470, 32), 25, lblCol, TextAlignmentOptions.Left);
            var nameInput = MakeInput(card.transform, font, "ใส่ชื่อ...", new Vector2(rx, 290f), new Vector2(470f, 50f));

            // แบบตัวละคร (สูงสุด 6 ปุ่ม, 3 ต่อแถว)
            MakeText(card.transform, font, "แบบตัวละคร / เพศ", new Vector2(rx, 244f), new Vector2(470, 30), 25, lblCol, TextAlignmentOptions.Left);
            var modelBtns = new Button[6];
            for (int i = 0; i < 6; i++)
            {
                int col = i % 3, rowi = i / 3;
                float x = rx + (col - 1) * 156f;
                float y = 204f - rowi * 54f;
                modelBtns[i] = MakeButton(card.transform, font, "แบบ " + (i + 1), new Vector2(x, y), new Vector2(140f, 46f), new Color(0.62f, 0.80f, 0.96f), 21);
            }

            // สี (8)
            MakeText(card.transform, font, "สีชุด", new Vector2(rx, 100f), new Vector2(470, 30), 25, lblCol, TextAlignmentOptions.Left);
            var colorBtns = new Button[8];
            for (int i = 0; i < 8; i++)
                colorBtns[i] = MakeSwatch(card.transform, new Vector2(rx + (i - 3.5f) * 46f, 62f), 40f);

            // ของแต่ง (4 ช่อง: หมวก/แว่น/เป้/ของถือ) — แต่ละช่อง 4 ปุ่ม (ไม่ใส่ + 3 แบบ)
            string[] slotNames = { "หมวก", "แว่นตา", "กระเป๋าเป้", "ของถือ" };
            var accSlots = new CharacterCreatorController.AccessorySlotUI[slotNames.Length];
            for (int s = 0; s < slotNames.Length; s++)
            {
                float labelY = 18f - s * 76f;
                float btnY = labelY - 34f;
                MakeText(card.transform, font, slotNames[s], new Vector2(rx, labelY), new Vector2(470, 28), 23, lblCol, TextAlignmentOptions.Left);
                var btns = new Button[4];
                for (int b = 0; b < 4; b++)
                {
                    float x = rx + (b - 1.5f) * 116f;
                    btns[b] = MakeButton(card.transform, font, "-", new Vector2(x, btnY), new Vector2(108f, 40f), new Color(0.86f, 0.82f, 0.72f), 19);
                }
                accSlots[s] = new CharacterCreatorController.AccessorySlotUI { buttons = btns };
            }

            // ระดับความยาก (ง่าย/ปกติ/ยาก)
            MakeText(card.transform, font, "ระดับความยาก", new Vector2(rx, -286f), new Vector2(470, 28), 23, lblCol, TextAlignmentOptions.Left);
            var diffBtns = new Button[3];
            string[] diffNames = { "ง่าย", "ปกติ", "ยาก" };
            Color[] diffCols = { new Color(0.60f, 0.86f, 0.68f), new Color(0.62f, 0.80f, 0.96f), new Color(0.99f, 0.66f, 0.62f) };
            for (int i = 0; i < 3; i++)
                diffBtns[i] = MakeButton(card.transform, font, diffNames[i], new Vector2(rx + (i - 1) * 150f, -324f), new Vector2(140f, 42f), diffCols[i], 21);

            // ปุ่มยืนยัน / ย้อนกลับ
            var back = MakeButton(card.transform, font, "ย้อนกลับ", new Vector2(-395f, -378f), new Vector2(300f, 60f), new Color(0.86f, 0.80f, 0.88f), 26);
            var confirm = MakeButton(card.transform, font, "เริ่มเล่น", new Vector2(255f, -378f), new Vector2(330f, 62f), new Color(0.60f, 0.86f, 0.68f), 28);

            // ===== ต่อ controller + เมนู =====
            var cc = card.gameObject.AddComponent<CharacterCreatorController>();
            cc.nameInput = nameInput; cc.modelButtons = modelBtns; cc.colorButtons = colorBtns;
            cc.accessorySlots = accSlots;
            cc.difficultyButtons = diffBtns;
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
                    "สร้างหน้าแต่งตัวละครแล้ว! 🧑‍🎨\n\nกด 'เล่นคนเดียว' → เลือกแบบ/สี/ชื่อ + หมวก/แว่น/เป้/ของถือ (พรีวิว 3D หมุนได้) → เริ่มเล่น\n\n* แบบตัวละคร: มาจาก CharacterCatalog (Setup Multiplayer)\n* ของแต่ง: ลากโมเดล prop ใส่ options[] ใน Assets/_Project/Resources/AccessoryCatalog แล้วกดสร้างซ้ำ", "เยี่ยม!");
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
            go.GetComponent<Image>().color = col;
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
            go.GetComponent<Image>().color = Color.white;
            return go.GetComponent<Button>();
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
