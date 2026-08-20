#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NisitSimulator.Systems;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // สร้างหน้า "เลือกคณะ" ในเมนูหลัก + ต่อกับ MainMenuController (กด New Game → เลือกคณะ → เริ่มเกม)
    // ใช้: เมนู  Nisit -> Build Faculty Select
    public static class M11FacultyBuilder
    {
        const string MenuPath = "Assets/Scenes/Scene1.unity";
        const string FontSdf = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        static TMP_FontAsset thai;
        static Sprite ui;
        // สไปรต์ชุดเดียวกับเมนูหลัก (สร้างโดย Polish Menu Layout) — มีก็สวยแบบลูกกวาด, ไม่มีก็ fallback
        static Sprite pill, gloss, round;
        static Sprite LoadUI(string f) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/" + f);
        public static bool SuppressDialog = false;   // ให้ Build M4 Menu เรียกต่อได้แบบไม่เด้ง popup

        [MenuItem("Nisit/Build Faculty Select")]
        public static void Build()
        {
            if (EditorSceneManager.GetActiveScene().path != MenuPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
            }

            var canvasGo = GameObject.Find("Menu Canvas");
            if (canvasGo == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator", "ยังไม่พบ Menu Canvas\nกด Nisit ▸ Build M4 Menu ก่อน", "โอเค");
                return;
            }
            var mc = canvasGo.GetComponent<MainMenuController>();
            if (mc == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator", "ไม่พบ MainMenuController บน Menu Canvas", "โอเค");
                return;
            }

            thai = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdf);
            ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            pill  = LoadUI("ui_pill2.png");   // ปุ่มพิลล์ (จาก Polish Menu Layout)
            gloss = LoadUI("ui_gloss.png");   // เงาวาวลูกกวาด
            round = LoadUI("ui_round.png");   // การ์ดมุมโค้ง

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var old = canvasGo.transform.Find("FacultyPanel");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // แผงคลุมจอแบบโปร่ง (หรี่ฉากหลังนิดๆ ให้มหาลัยยังเห็น)
            var panel = MakeImage(canvasGo.transform, "FacultyPanel", Vector2.zero, Vector2.one, new Color(0.05f, 0.08f, 0.14f, 0.55f));
            panel.rectTransform.offsetMin = Vector2.zero; panel.rectTransform.offsetMax = Vector2.zero;

            // การ์ดกลางทึบ (บังปุ่มเมนูข้างหลัง + รวมเนื้อหาให้เป็นระเบียบ) — ขนาดพอดีเนื้อหา
            var card = MakeRounded(panel.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0, 24), new Vector2(720, 700), new Color(0.11f, 0.14f, 0.24f, 0.98f));
            Deco(card);

            MakeText(panel.transform, "Title", "เลือกคณะของคุณ", 54, new Vector2(0.5f, 0.5f), new Vector2(0, 286),
                new Vector2(640, 76), new Color(1f, 0.9f, 0.5f), FontStyles.Bold);
            MakeText(panel.transform, "Sub", "คณะจะกำหนดแนวข้อสอบของคุณ", 27, new Vector2(0.5f, 0.5f), new Vector2(0, 233),
                new Vector2(640, 42), new Color(0.9f, 0.92f, 0.86f), FontStyles.Normal);

            // สีปุ่มแต่ละคณะ (โทนพาสเทลเข้าชุดเมนูหลัก)
            Color[] cols = {
                new Color(0.62f, 0.80f, 0.96f),  // IT ฟ้าพาสเทล
                new Color(0.99f, 0.82f, 0.62f),  // บริหาร พีช
                new Color(0.60f, 0.86f, 0.68f),  // วิทย์ มินต์
                new Color(0.80f, 0.72f, 0.96f),  // นิเทศ ลาเวนเดอร์
            };

            int n = FacultyCatalog.Count;
            float startY = 132f, gap = 88f;   // กระชับให้พอดีการ์ด (4 คณะ + ปุ่มกลับ)
            for (int i = 0; i < n; i++)
            {
                var btn = MakeButton(panel.transform, FacultyCatalog.NameOf(i),
                    new Vector2(0, startY - i * gap), cols[i % cols.Length]);
                btn.gameObject.AddComponent<FacultyButton>().index = i;   // ต่อสายตอน runtime
            }

            // ปุ่มกลับ
            var back = MakeButton(panel.transform, "ย้อนกลับ", new Vector2(0, startY - n * gap - 12f), new Color(0.74f, 0.72f, 0.80f));
            back.gameObject.AddComponent<FacultyButton>().index = -1;

            mc.facultyPanel = panel.gameObject;
            panel.gameObject.SetActive(false);
            panel.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=lime>[Nisit] สร้างหน้าเลือกคณะเสร็จ!</color>");
            if (!SuppressDialog)
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างหน้าเลือกคณะเสร็จแล้ว! 🎓\n\nกด 'เล่นคนเดียว' → เลือกคณะ (IT/บริหาร/วิทย์/นิเทศ) → เข้าเกม\nข้อสอบจะเปลี่ยนตามคณะที่เลือก\n\n(รองรับ multiplayer ในอนาคต: คณะเป็นค่าต่อผู้เล่น)", "เยี่ยม!");
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

        // ปุ่มลูกกวาด: ฐานเงาเข้ม + ปุ่มพิลล์ + เงาวาวด้านบน + tint hover (เข้าชุดเมนูหลัก)
        static Button MakeButton(Transform parent, string label, Vector2 pos, Color col)
        {
            var size = new Vector2(520, 76);
            var sprite = pill ?? ui;

            // ฐานเงาเข้ม (หลังปุ่ม เยื้องลง)
            var baseGo = new GameObject(label + "Base", typeof(Image));
            baseGo.transform.SetParent(parent, false);
            var bimg = baseGo.GetComponent<Image>();
            bimg.sprite = sprite; bimg.type = Image.Type.Sliced; bimg.color = Shift(col, -0.28f); bimg.raycastTarget = false;
            var brt = bimg.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = pos + new Vector2(0, -8); brt.sizeDelta = size;

            // ตัวปุ่มจริง
            var go = new GameObject(label + "Btn", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.type = Image.Type.Sliced; img.color = col;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            Deco(img);

            // เงาวาวด้านบน (candy gloss)
            if (gloss != null)
            {
                var gGo = new GameObject("Gloss", typeof(Image));
                gGo.transform.SetParent(go.transform, false);
                var g = gGo.GetComponent<Image>();
                g.sprite = gloss; g.type = Image.Type.Simple; g.color = new Color(1f, 1f, 1f, 0.5f); g.raycastTarget = false;
                var grt = g.rectTransform;
                grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
                grt.sizeDelta = new Vector2(size.x * 0.9f, size.y * 0.55f);
                grt.anchoredPosition = new Vector2(0, size.y * 0.2f);
            }

            // สว่างขึ้นตอนชี้เมาส์
            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint; btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = col; cb.highlightedColor = Shift(col, 0.12f);
            cb.pressedColor = Shift(col, -0.10f); cb.selectedColor = Shift(col, 0.06f);
            cb.fadeDuration = 0.1f; cb.colorMultiplier = 1f;
            btn.colors = cb;

            var txt = MakeText(go.transform, "Text", label, 30, new Vector2(0.5f, 0.5f), Vector2.zero, size, new Color(0.20f, 0.22f, 0.38f), FontStyles.Bold);
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            txt.transform.SetAsLastSibling();

            return btn;
        }

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
