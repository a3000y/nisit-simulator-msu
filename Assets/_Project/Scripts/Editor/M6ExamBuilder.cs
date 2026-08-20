#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NisitSimulator.Systems;
using NisitSimulator.Interaction;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // สร้าง UI ข้อสอบ (quiz popup) ในฉากเกม + ต่อ ExamController
    // ใช้: เมนู  Nisit -> Build Exam System
    public static class M6ExamBuilder
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string FontSdf = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        const string FurnDir = "Assets/_Project/Art/Models/KayKit_Furniture/";
        static TMP_FontAsset thai;
        static Sprite ui, round, pill, gloss;
        static Sprite LoadUI(string f) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/" + f);

        public static bool SuppressDialog = false;   // ปิด popup เมื่อเรียกจาก Rebuild All

        [MenuItem("Nisit/Build Exam System")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            thai = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdf);
            ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            round = LoadUI("ui_round.png"); pill = LoadUI("ui_pill2.png"); gloss = LoadUI("ui_gloss.png");

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.name == "Exam Canvas") Object.DestroyImmediate(c.gameObject);

            var canvasGo = new GameObject("Exam Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;   // อยู่เหนือ HUD
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // ราก popup (เปิด/ปิดทั้งชุด)
            var root = MakeImage(canvasGo.transform, "ExamRoot", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.62f));
            root.rectTransform.offsetMin = Vector2.zero; root.rectTransform.offsetMax = Vector2.zero;
            var rootT = root.transform;

            // การ์ดข้อสอบ (มุมโค้ง + เงา)
            var card = MakeImage(rootT, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.13f, 0.16f, 0.26f, 1f));
            card.sprite = round ?? ui; Deco(card);
            card.rectTransform.sizeDelta = new Vector2(920, 660);
            var cardT = card.transform;

            var header = MakeText(cardT, "Header", "สอบกลางภาค", 46, new Vector2(0.5f, 1f), new Vector2(0, -46), new Vector2(700, 60), new Color(1f, 0.86f, 0.42f), FontStyles.Bold);
            var progress = MakeText(cardT, "Progress", "ข้อ 1/3", 28, new Vector2(1f, 1f), new Vector2(-40, -50), new Vector2(220, 40), Color.white, FontStyles.Normal);
            progress.alignment = TextAlignmentOptions.Right;
            var question = MakeText(cardT, "Question", "คำถาม...", 34, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(820, 170), Color.white, FontStyles.Normal);
            question.alignment = TextAlignmentOptions.Top;

            var answerBtns = new Button[4];
            var answerLbls = new TMP_Text[4];
            float y0 = 30f;
            for (int i = 0; i < 4; i++)
            {
                answerBtns[i] = MakeButton(cardT, "AnsBtn" + i, "ตัวเลือก", new Vector2(0, y0 - i * 88f),
                    new Vector2(760, 76), new Color(0.66f, 0.82f, 0.96f), out answerLbls[i]);   // ฟ้าพาสเทล
            }

            // แผงผลสอบ (ทับบนการ์ด)
            var result = MakeImage(cardT, "ResultPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.12f, 0.15f, 0.24f, 1f));
            result.sprite = round ?? ui; Deco(result);
            result.rectTransform.sizeDelta = new Vector2(920, 660);
            var resT = result.transform;
            MakeText(resT, "RTitle", "ผลการสอบ", 34, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(600, 50), Color.white, FontStyles.Normal);
            var grade = MakeText(resT, "Grade", "A", 150, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(300, 200), new Color(0.3f, 0.8f, 0.42f), FontStyles.Bold);
            var detail = MakeText(resT, "Detail", "รายละเอียด...", 30, new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(760, 160), Color.white, FontStyles.Normal);
            var close = MakeButton(resT, "CloseBtn", "รับทราบ", new Vector2(0, -250), new Vector2(300, 76), new Color(0.60f, 0.86f, 0.68f), out _);   // มินต์พาสเทล

            // ต่อ ExamController
            var ec = canvasGo.AddComponent<ExamController>();
            ec.panel = root.gameObject;
            ec.headerText = header;
            ec.progressText = progress;
            ec.questionText = question;
            ec.answerButtons = answerBtns;
            ec.answerLabels = answerLbls;
            ec.resultPanel = result.gameObject;
            ec.gradeText = grade;
            ec.resultDetailText = detail;
            ec.closeButton = close;

            // แอนิเมชันเด้งเข้า
            var rootAnim = root.gameObject.AddComponent<UIPopupAnim>();
            rootAnim.scaleTarget = card.rectTransform;
            var resAnim = result.gameObject.AddComponent<UIPopupAnim>();
            resAnim.scaleTarget = grade.rectTransform;

            result.gameObject.SetActive(false);
            root.gameObject.SetActive(false);

            // เก็บกวาดโต๊ะสอบ/เฟอร์นิเจอร์เก่า — จุดสอบไปอยู่ที่ "อาคารเรียน" แทน
            // (กด  Nisit ▸ Place Exam & Jobs at Buildings  เพื่อวางจุดสอบ/งานที่อาคารจริง)
            var oldDesk = GameObject.Find("ExamDesk");
            if (oldDesk != null) Object.DestroyImmediate(oldDesk);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] สร้างระบบสอบเสร็จ!</color>");
            if (!SuppressDialog)
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างระบบสอบเสร็จแล้ว! 📝\n\n• วันสอบจะมีแจ้งเตือน → ต้อง**เดินไปห้องสอบ (ExamDesk) กด E**\n• ตัวละครนั่งลงทำข้อสอบจริง → quiz โผล่ตอนนั่ง\n• ได้เกรด A-F + GPA + รางวัลตามเกรด\n\n⚠️ โต๊ะสอบ 'ExamDesk' วางไว้ที่ (0, 4) — ลากไปวางในห้องเรียน/ตำแหน่งที่ต้องการได้\n(ต้องอยู่ Layer = Interactable ผมตั้งให้แล้ว)", "เยี่ยม!");
        }

        // วางโมเดลเฟอร์นิเจอร์เป็นลูกของสถานี (ภาพประกอบ)
        static void PlaceFurn(string assetPath, Transform parent, Vector3 localPos, float rotY, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null) return;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            inst.transform.SetParent(parent);
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            inst.transform.localScale = Vector3.one * scale;
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
            t.textWrappingMode = TextWrappingModes.Normal;
            if (thai != null) t.font = thai;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return t;
        }

        // ปุ่มลูกกวาด: ฐานหนา + พิลล์ + เงาวาว + tint hover
        static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color col, out TMP_Text labelText)
        {
            var sprite = pill ?? ui;

            var baseGo = new GameObject(name + "Base", typeof(Image));
            baseGo.transform.SetParent(parent, false);
            var bimg = baseGo.GetComponent<Image>();
            bimg.sprite = sprite; bimg.type = Image.Type.Sliced; bimg.color = Shift(col, -0.24f); bimg.raycastTarget = false;
            var brt = bimg.rectTransform; brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f); brt.anchoredPosition = pos + new Vector2(0, -6); brt.sizeDelta = size;

            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.type = Image.Type.Sliced; img.color = col;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            Deco(img);

            if (gloss != null)
            {
                var g = new GameObject("Gloss", typeof(Image)); g.transform.SetParent(go.transform, false);
                var gi = g.GetComponent<Image>(); gi.sprite = gloss; gi.type = Image.Type.Simple; gi.color = new Color(1f, 1f, 1f, 0.42f); gi.raycastTarget = false;
                var grt = gi.rectTransform; grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
                grt.sizeDelta = new Vector2(size.x * 0.94f, size.y * 0.5f); grt.anchoredPosition = new Vector2(0, size.y * 0.22f);
            }

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint; btn.targetGraphic = img;
            var cb = btn.colors; cb.normalColor = col; cb.highlightedColor = Shift(col, 0.12f); cb.pressedColor = Shift(col, -0.10f); cb.fadeDuration = 0.1f; btn.colors = cb;

            labelText = MakeText(go.transform, "Text", label, 28, new Vector2(0.5f, 0.5f), Vector2.zero, size, new Color(0.20f, 0.22f, 0.38f), FontStyles.Bold);
            var trt = labelText.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            labelText.transform.SetAsLastSibling();
            return btn;
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
