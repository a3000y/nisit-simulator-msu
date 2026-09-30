#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NisitSimulator.UI;
using NisitSimulator.Stats;
using NisitSimulator.Core;
using NisitSimulator.TimeSystem;
using NisitSimulator.Systems;

namespace NisitSimulator.EditorTools
{
    // สร้างหน้าจอ HUD สไตล์การ์ตูน 3D (ขอบหนา + เงา + มุมมน + ไอคอน) + เชื่อมระบบสถานะ/เวลา
    // ใช้: เมนู  Nisit -> Build M3 HUD
    public static class M3HudBuilder
    {
        // สีธีมการ์ตูน ดำ-ทอง หรูหรา
        static readonly Color Ink       = new Color(0.05f, 0.05f, 0.10f, 1f);     // ขอบเข้ม (การ์ตูนป็อป)
        static readonly Color PanelCol  = new Color(0.14f, 0.15f, 0.27f, 0.96f);  // พื้นแผง (คราม ทึบขึ้น อ่านง่าย)
        static readonly Color SlotCol   = new Color(0.07f, 0.07f, 0.13f, 1f);     // ร่องแถบ (ว่าง)
        static readonly Color Gold      = new Color(0.96f, 0.80f, 0.36f, 1f);     // ทอง (แอกเซนต์หรู)
        static readonly Color OnGold    = new Color(0.13f, 0.11f, 0.18f, 1f);     // ข้อความบนทอง

        const string UIDir = "Assets/_Project/UI";
        static Sprite Round  => GetSprite("round",  64, 20);   // สี่เหลี่ยมมุมมน (9-slice)
        static Sprite Pill   => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_pill2.png") ?? Round;
        static Sprite Gloss  => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_gloss.png");
        static Color Shift(Color c, float d) => new Color(Mathf.Clamp01(c.r + d), Mathf.Clamp01(c.g + d), Mathf.Clamp01(c.b + d), c.a);
        static Sprite Circle => GetSprite("circle", 96, -1);   // วงกลม

        public static bool SuppressDialog = false;   // ปิด popup เมื่อเรียกจาก Rebuild All

        [MenuItem("Nisit/Build M3 HUD")]
        public static void BuildHud()
        {
            var player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Nisit", "ยังไม่มี Player ในฉาก!\nกด Nisit -> Build M1 Scene ก่อน", "OK");
                return;
            }

            DestroyIfExists("Menu Canvas");   // กันพื้นหลังเมนูหลงมาในฉากเกม (ปนกัน)

            // ---- ระบบ ----
            if (player.GetComponent<StatDecay>() == null) player.AddComponent<StatDecay>();
            var gm = GameObject.Find("GameManager");
            if (gm == null) { gm = new GameObject("GameManager"); gm.AddComponent<GameManager>(); }
            if (gm.GetComponent<GameClock>() == null) gm.AddComponent<GameClock>();
            if (gm.GetComponent<GameOverWatcher>() == null) gm.AddComponent<GameOverWatcher>();
            if (gm.GetComponent<ProgressionManager>() == null) gm.AddComponent<ProgressionManager>();
            // 3 นาทีเกมต่อ 1 วินาทีจริง — หนึ่งวันตื่นถึงเข้านอนราว 5 นาทีจริง เล่นจบ 4 ปีได้ในหนึ่งนั่ง
            // อย่าเปลี่ยนกลับเป็น 1 โดยไม่ปรับ StatDecay ด้วย เกมจะกลับไปยาว 19 ชั่วโมงต่อรอบ
            gm.GetComponent<GameClock>().gameMinutesPerRealSecond = 3f;

            // ---- Canvas ----
            DestroyIfExists("HUD Canvas");
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var hud = canvasGo.AddComponent<HUDController>();
            var root = canvasGo.transform;

            // ===== พอร์ตเทรตตัวละคร (ซ้ายบน) + แถบสถานะข้างขวา =====
            Portrait(root);
            hud.energyFill = CartoonBar(root, "Energy", 0, new Color(0.40f, 0.86f, 0.40f), "พลังงาน", out var eVal); hud.energyText = eVal;
            hud.healthFill = CartoonBar(root, "Health", 1, new Color(1.00f, 0.44f, 0.46f), "สุขภาพ",  out var hVal); hud.healthText = hVal;
            hud.hungerFill = CartoonBar(root, "Hunger", 2, new Color(1.00f, 0.73f, 0.24f), "ความอิ่ม", out var uVal); hud.hungerText = uVal;
            // แถบความเครียด — อ่านกลับกับอีกสามแถบ เต็มคือแย่ จึงใช้สีม่วงแดงเป็นสัญญาณเตือน
            hud.stressFill = CartoonBar(root, "Stress", 3, new Color(0.85f, 0.45f, 0.85f), "ความเครียด", out var sVal); hud.stressText = sVal;

            // ===== ข้อมูล (ขวาบน) แบบชิปแยก =====
            // นาฬิกา (ชิปเด่นสุด สีทอง)
            var clockChip = MakeImage(root, "ClockChip", new Vector2(1, 1), new Vector2(-24, -24), new Vector2(292, 68), Round, Gold);
            clockChip.rectTransform.pivot = new Vector2(1, 1);
            Deco(clockChip, 5f);
            hud.clockText = MakeTMP(clockChip.transform, "ClockTxt", "08:00 AM", 46, TextAlignmentOptions.Center, OnGold, FontStyles.Bold);
            var crt = hud.clockText.rectTransform; crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;
            // แถบความรู้ (fill + ข้อความทับ)
            hud.knowledgeFill = KnowledgeBar(root, -102, out var kTxt); hud.knowledgeText = kTxt;
            // ชิปข้อมูล (ไอคอนสี + ข้อความ)
            InfoChip(root, -154, new Color(0.45f, 0.72f, 1f),  out var dTxt); hud.dayText   = dTxt;
            InfoChip(root, -204, new Color(1f, 0.80f, 0.30f),  out var mTxt); hud.moneyText = mTxt;
            InfoChip(root, -254, new Color(0.72f, 0.56f, 0.96f), out var yTxt); hud.yearText  = yTxt;

            // ===== toast (กลางบน) ปรับเป็นสีขาว ขนาดพอเหมาะ =====
            hud.toastText = FloatText(root, "ToastText", new Vector2(0.5f, 1f), new Vector2(0, -140), "", 32, Color.white);

            // ===== prompt (ล่างกลาง) แบบเม็ดยา =====
            var pill = Panel(root, new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(520, 84));
            hud.promptBg = pill.gameObject;
            hud.promptText = FloatText(pill.transform, "PromptText", new Vector2(0.5f, 0.5f), Vector2.zero, "กด E เพื่อ...", 34, Color.white);
            var prt = hud.promptText.rectTransform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
            pill.gameObject.SetActive(false);

            int fontN = ThaiFontSetup.ApplyToHud();
            BuildEndScreen();   // จอจบเกม (ชนะ/แพ้) สไตล์เดียวกัน

            Selection.activeGameObject = canvasGo;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"<color=cyan>[Nisit] สร้าง HUD การ์ตูน 3D สำเร็จ! (ใส่ฟอนต์ไทย {fontN} ข้อความ) กด Ctrl+S แล้ว Play</color>");
            if (!SuppressDialog)
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้าง HUD การ์ตูน 3D เสร็จแล้ว!\n\nกด Ctrl+S แล้ว Play\n\n※ ถ้าไทยเป็นสี่เหลี่ยม กด Nisit -> Fix Thai Font (HUD)", "เยี่ยม!");
        }

        // ---------- พอร์ตเทรตตัวละคร (เรนเดอร์โมเดล 3D จริงลงกรอบ + ป้ายอารมณ์) ----------
        static void Portrait(Transform root)
        {
            // เลเยอร์ให้กล้องพอร์ตเทรตเรนเดอร์เฉพาะตัวละคร (พื้นหลังสีเดียว ไม่รกฉาก)
            var player = GameObject.Find("Player");
            int layer = EnsureLayer("Player");
            if (player != null && layer >= 0) SetLayerRecursive(player, layer);
            var mainCam = Camera.main;
            if (mainCam != null && layer >= 0) mainCam.cullingMask |= (1 << layer);   // ให้จอหลักยังเห็นตัวละคร

            // กรอบทอง + Mask วงกลม
            var frame = MakeImage(root, "Portrait", new Vector2(0, 1), new Vector2(22, -16), new Vector2(116, 116), Circle, new Color(1f, 0.82f, 0.34f));
            Deco(frame, 5f);
            var mask = MakeImage(frame.transform, "PMask", Vector2.zero, Vector2.zero, Vector2.zero, Circle, new Color(0.56f, 0.80f, 0.93f));
            var mrt = mask.rectTransform; mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one; mrt.offsetMin = new Vector2(8, 8); mrt.offsetMax = new Vector2(-8, -8);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            // ภาพเรนเดอร์ตัวละคร (RawImage รับ RenderTexture ตอน Play)
            var viewGo = new GameObject("PortraitView", typeof(RawImage));
            viewGo.transform.SetParent(mask.transform, false);
            var raw = viewGo.GetComponent<RawImage>();
            var vrt = raw.rectTransform; vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = vrt.offsetMax = Vector2.zero;

            // กล้องพอร์ตเทรต
            DestroyIfExists("PortraitCamera");
            var camGo = new GameObject("PortraitCamera", typeof(Camera));
            var pcam = camGo.GetComponent<Camera>();
            pcam.cullingMask = layer >= 0 ? (1 << layer) : pcam.cullingMask;
            var pc = camGo.AddComponent<PortraitCam>();
            pc.display = raw;
            if (player != null) pc.target = player.transform;

            // ป้ายอารมณ์เล็ก (มุมขวาล่าง) + MoodFace
            var ink = new Color(0.20f, 0.15f, 0.18f);
            var badge = MakeImage(frame.transform, "MoodBadge", new Vector2(1, 0), new Vector2(-2, 2), new Vector2(40, 40), Circle, new Color(1f, 0.95f, 0.82f));
            badge.rectTransform.pivot = new Vector2(1, 0);
            Deco(badge, 3f);
            var beL = MakeImage(badge.transform, "BEyeL", new Vector2(0.5f, 0.5f), new Vector2(-6, 4), new Vector2(5, 5), Circle, ink);
            var beR = MakeImage(badge.transform, "BEyeR", new Vector2(0.5f, 0.5f), new Vector2(6, 4), new Vector2(5, 5), Circle, ink);
            var bmouth = MakeImage(badge.transform, "BMouth", new Vector2(0.5f, 0.5f), new Vector2(0, -3), new Vector2(22, 14), GetMouth("smile"), ink);
            var mf = frame.gameObject.AddComponent<MoodFace>();
            mf.mouth = bmouth; mf.smile = GetMouth("smile"); mf.flat = GetMouth("flat"); mf.frown = GetMouth("frown");
            mf.leftEye = beL.rectTransform; mf.rightEye = beR.rectTransform;
        }

        // สร้าง/หาเลเยอร์ตามชื่อ (คืน index หรือ -1)
        static int EnsureLayer(string name)
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset.Length == 0) return -1;
            var tm = new SerializedObject(asset[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;
            for (int i = 8; i < layers.arraySize; i++)
            {
                var e = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(e.stringValue)) { e.stringValue = name; tm.ApplyModifiedProperties(); return i; }
            }
            return -1;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        // ---------- แถบสถานะการ์ตูน (ไอคอน + ไฮไลต์ + ร่อง + fill + มันวาว + ตัวเลข) ----------
        static Image CartoonBar(Transform root, string name, int index, Color color, string label, out TMP_Text valueText)
        {
            float y = -22 - index * 52f;

            // ไอคอนวงกลมสี + จุดไฮไลต์ (ดูนูนวาว) — อยู่ขวาของพอร์ตเทรต
            var icon = MakeImage(root, name + "Icon", new Vector2(0, 1), new Vector2(150, y - 1), new Vector2(40, 40), Circle, color);
            Deco(icon, 4f);
            var ihi = MakeImage(icon.transform, "Hi", new Vector2(0.32f, 0.68f), Vector2.zero, new Vector2(13, 13), Circle, new Color(1, 1, 1, 0.55f));

            // ร่องแถบ (พื้นเข้ม มุมมน)
            var slot = MakeImage(root, name + "Slot", new Vector2(0, 1), new Vector2(198, y), new Vector2(240, 40), Round, SlotCol);
            slot.rectTransform.pivot = new Vector2(0, 1);
            Deco(slot, 4f);

            // fill สว่าง (เติมแนวนอน)
            var fill = MakeImage(slot.transform, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, Round, color);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0; fill.fillAmount = 1f;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(5, 5); frt.offsetMax = new Vector2(-5, -5);

            // แถบมันวาว (สีขาวจางครึ่งบน)
            var gloss = MakeImage(slot.transform, "Gloss", Vector2.zero, Vector2.zero, Vector2.zero, Round, new Color(1, 1, 1, 0.22f));
            var grt = gloss.rectTransform;
            grt.anchorMin = new Vector2(0, 0.52f); grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(6, 0); grt.offsetMax = new Vector2(-6, -4);
            // เงาไล่ล่าง (รวมกับ gloss = ไล่เฉดสว่าง→เข้ม)
            var shade = MakeImage(slot.transform, "Shade", Vector2.zero, Vector2.zero, Vector2.zero, Round, new Color(0, 0, 0, 0.20f));
            var srt = shade.rectTransform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = new Vector2(1, 0.42f);
            srt.offsetMin = new Vector2(6, 4); srt.offsetMax = new Vector2(-6, 0);

            // ป้ายซ้าย + ตัวเลขขวา
            var lbl = MakeTMP(slot.transform, name + "Label", label, 20, TextAlignmentOptions.Left, Color.white, FontStyles.Bold);
            var lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(14, 0); lrt.offsetMax = new Vector2(-42, 0);

            valueText = MakeTMP(slot.transform, name + "Val", "100", 20, TextAlignmentOptions.Right, Color.white, FontStyles.Bold);
            var vrt = valueText.rectTransform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(10, 0); vrt.offsetMax = new Vector2(-12, 0);

            return fill;
        }

        // เส้นคั่นในแผง
        // ชิปข้อมูล (ขวาบน) = เม็ดยามุมมน + ไอคอนวงกลม + ข้อความ
        static void InfoChip(Transform root, float y, Color iconCol, out TMP_Text text)
        {
            var chip = MakeImage(root, "InfoChip", new Vector2(1, 1), new Vector2(-24, y), new Vector2(292, 44), Round, PanelCol);
            chip.rectTransform.pivot = new Vector2(1, 1);
            Deco(chip, 4f);
            var dot = MakeImage(chip.transform, "Dot", new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(24, 24), Circle, iconCol);
            MakeImage(dot.transform, "Hi", new Vector2(0.35f, 0.68f), Vector2.zero, new Vector2(8, 8), Circle, new Color(1, 1, 1, 0.5f));
            text = MakeTMP(chip.transform, "Txt", "", 26, TextAlignmentOptions.Left, Color.white, FontStyles.Normal);
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(52, 0); rt.offsetMax = new Vector2(-16, 0);
        }

        // ชิปแถบความรู้ = fill สีฟ้า + มันวาว + ข้อความทับกลาง — คืนค่า fill
        static Image KnowledgeBar(Transform root, float y, out TMP_Text text)
        {
            var chip = MakeImage(root, "KChip", new Vector2(1, 1), new Vector2(-24, y), new Vector2(292, 44), Round, SlotCol);
            chip.rectTransform.pivot = new Vector2(1, 1);
            Deco(chip, 4f);
            var fill = MakeImage(chip.transform, "KFill", Vector2.zero, Vector2.zero, Vector2.zero, Round, new Color(0.42f, 0.68f, 1f));
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 0.1f;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(4, 4); frt.offsetMax = new Vector2(-4, -4);
            var gloss = MakeImage(chip.transform, "KGloss", Vector2.zero, Vector2.zero, Vector2.zero, Round, new Color(1, 1, 1, 0.14f));
            var grt = gloss.rectTransform;
            grt.anchorMin = new Vector2(0, 0.55f); grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(6, 0); grt.offsetMax = new Vector2(-6, -4);
            text = MakeTMP(chip.transform, "KTxt", "ความรู้ 0/80", 22, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return fill;
        }

        // ---------- แผงมุมมน (ขอบหนา + เงา) ----------
        static Image Panel(Transform root, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var p = MakeImage(root, "Panel", anchor, pos, size, Round, PanelCol);
            p.rectTransform.pivot = anchor;
            Deco(p, 5f);
            return p;
        }

        // ข้อความลอย (toast/prompt) ตัวหนาใหญ่
        static TMP_Text FloatText(Transform parent, string name, Vector2 anchor, Vector2 pos, string text, float size, Color color)
        {
            var t = MakeTMP(parent, name, text, size, TextAlignmentOptions.Center, color, FontStyles.Bold);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(760, 70);
            return t;
        }

        // ---------- ตัวช่วยพื้นฐาน ----------
        static Image MakeImage(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = (sprite != null && sprite.border.sqrMagnitude > 0.01f) ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return img;
        }

        static TMP_Text MakeTMP(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = color;
            t.fontStyle = style; t.enableWordWrapping = false;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 44);
            return t;
        }

        // ขอบหนา + เงา (การ์ตูน 3D) — ใช้ได้กับ Image
        static void Deco(Graphic g, float outline)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = new Vector2(outline, -outline);
            o.useGraphicAlpha = false;
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.38f);
            s.effectDistance = new Vector2(4f, -7f);
        }

        // ---------- จอจบเกม (ชนะ/แพ้) สไตล์พรีเมียม ----------
        static void BuildEndScreen()
        {
            DestroyIfExists("End Canvas");
            var canvasGo = new GameObject("End Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;   // อยู่บน HUD
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var ctrl = canvasGo.AddComponent<EndScreenController>();

            // ฉากมืดโปร่ง (root ที่เปิด/ปิด)
            var dim = MakeImage(canvasGo.transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, null, new Color(0f, 0f, 0f, 0.72f));
            var drt = dim.rectTransform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;

            // การ์ดกลางจอ (สูงขึ้นให้พอดีคะแนน 4 บรรทัด)
            var card = MakeImage(dim.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 660), Round, PanelCol);
            Deco(card, 6f);

            var title = CardText(card.transform, "GAME OVER", 252, 72, new Color(0.85f, 0.22f, 0.22f), FontStyles.Bold, false);
            var msg   = CardText(card.transform, "ข้อความ", 150, 30, new Color(0.25f, 0.30f, 0.42f), FontStyles.Normal, true);
            var score = CardText(card.transform, "คะแนนรวม: 0", -25, 30, new Color(0.16f, 0.20f, 0.30f), FontStyles.Bold, false);
            score.rectTransform.sizeDelta = new Vector2(660, 200);   // รองรับ 4 บรรทัด (คณะ/ชั้นปี/GPA/คะแนน)
            score.lineSpacing = 12f;

            var restart = EndButton(card.transform, "เริ่มใหม่", new Vector2(-145, -268), new Color(0.60f, 0.86f, 0.68f));  // มินต์พาสเทล
            var quit    = EndButton(card.transform, "ออก",       new Vector2(145, -268),  new Color(0.99f, 0.74f, 0.78f));  // ชมพูพาสเทล

            ctrl.panel = dim.gameObject;
            ctrl.titleText = title; ctrl.messageText = msg; ctrl.scoreText = score;
            ctrl.restartButton = restart; ctrl.quitButton = quit;

            var f = ThaiFontSetup.GetOrCreateThaiFont();
            if (f != null) foreach (var t in canvasGo.GetComponentsInChildren<TMP_Text>(true)) t.font = f;

            dim.gameObject.SetActive(false);
        }

        static TMP_Text CardText(Transform card, string txt, float y, float size, Color col, FontStyles st, bool wrap)
        {
            var t = MakeTMP(card, "Line", txt, size, TextAlignmentOptions.Center, col, st);
            t.enableWordWrapping = wrap;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(640, wrap ? 130f : size * 1.8f);
            return t;
        }

        // ปุ่มลูกกวาด: ฐานหนา + พิลล์ + เงาวาว + ขอบหนา + ตัวอักษรเข้ม
        static Button EndButton(Transform parent, string label, Vector2 pos, Color col)
        {
            var size = new Vector2(240, 78);
            var sprite = Pill;
            var gl = Gloss;

            var baseGo = new GameObject(label + "Base", typeof(Image));
            baseGo.transform.SetParent(parent, false);
            var bimg = baseGo.GetComponent<Image>();
            bimg.sprite = sprite; bimg.type = Image.Type.Sliced; bimg.color = Shift(col, -0.24f); bimg.raycastTarget = false;
            var brt = bimg.rectTransform; brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f); brt.anchoredPosition = pos + new Vector2(0, -6); brt.sizeDelta = size;

            var go = new GameObject(label + "Btn", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.type = Image.Type.Sliced; img.color = col;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            Deco(img, 5f);

            if (gl != null)
            {
                var g = new GameObject("Gloss", typeof(Image)); g.transform.SetParent(go.transform, false);
                var gi = g.GetComponent<Image>(); gi.sprite = gl; gi.type = Image.Type.Simple; gi.color = new Color(1f, 1f, 1f, 0.42f); gi.raycastTarget = false;
                var grt = gi.rectTransform; grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f); grt.sizeDelta = new Vector2(size.x * 0.9f, size.y * 0.5f); grt.anchoredPosition = new Vector2(0, size.y * 0.22f);
            }

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint; btn.targetGraphic = img;
            var cb = btn.colors; cb.normalColor = col; cb.highlightedColor = Shift(col, 0.12f); cb.pressedColor = Shift(col, -0.10f); cb.fadeDuration = 0.1f; btn.colors = cb;

            var txt = MakeTMP(go.transform, "Text", label, 32, TextAlignmentOptions.Center, new Color(0.20f, 0.22f, 0.38f), FontStyles.Bold);
            var trt = txt.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            txt.transform.SetAsLastSibling();
            return btn;
        }

        static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        // ---------- สร้าง sprite เอง (มุมมนคม / วงกลมเนียน) เก็บเป็น PNG ในโปรเจกต์ ----------
        static Sprite GetSprite(string key, int size, int radius)   // radius < 0 = วงกลม
        {
            string path = UIDir + "/" + key + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            if (!Directory.Exists(UIDir)) Directory.CreateDirectory(UIDir);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = radius < 0 ? CircleAlpha(x + 0.5f, y + 0.5f, size)
                                         : RoundAlpha(x + 0.5f, y + 0.5f, size, radius);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = radius < 0 ? Vector4.zero : new Vector4(radius, radius, radius, radius);
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static float RoundAlpha(float px, float py, int size, int r)
        {
            bool cx = px < r || px > size - r;
            bool cy = py < r || py > size - r;
            if (!(cx && cy)) return 1f;   // ขอบตรง/ด้านใน = ทึบเต็ม
            float dx = px < r ? r - px : px - (size - r);
            float dy = py < r ? r - py : py - (size - r);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(r - d + 0.5f);   // มุมมน + ขอบเนียน 1px
        }

        static float CircleAlpha(float px, float py, int size)
        {
            float c = size * 0.5f, r = c - 0.75f;
            float d = Mathf.Sqrt((px - c) * (px - c) + (py - c) * (py - c));
            return Mathf.Clamp01(r - d + 0.75f);
        }

        // ปาก (smile = โค้งขึ้น / frown = โค้งลง / flat = เส้นตรง)
        static Sprite GetMouth(string kind)
        {
            string path = UIDir + "/mouth_" + kind + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            if (!Directory.Exists(UIDir)) Directory.CreateDirectory(UIDir);
            int size = 40;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, MouthAlpha(x + 0.5f, y + 0.5f, size, kind)));
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static float MouthAlpha(float px, float py, int size, string kind)
        {
            float nx = px / size * 2f - 1f;               // -1..1 (แนวนอน)
            if (Mathf.Abs(nx) > 0.72f) return 0f;          // จำกัดความกว้างปาก
            float mid = size * 0.5f, th = size * 0.11f, amp = size * 0.20f;
            float curve = amp * (1f - nx * nx);
            float line = kind == "smile" ? mid - curve : (kind == "frown" ? mid + curve : mid);
            return Mathf.Clamp01(th - Mathf.Abs(py - line) + 0.5f);
        }
    }
}
#endif
