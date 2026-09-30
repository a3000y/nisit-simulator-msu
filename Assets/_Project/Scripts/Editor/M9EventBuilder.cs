#if UNITY_EDITOR
using System.IO;
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
    // สร้าง UI สุ่มเหตุการณ์ (popup เลือกทาง) + ต่อ EventManager
    // ใช้: เมนู  Nisit -> Build Event System
    public static class M9EventBuilder
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string FontSdf = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        static TMP_FontAsset thai;
        static Sprite ui;
        static Sprite arrowSprite;

        [MenuItem("Nisit/Build Event System")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            thai = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdf);
            ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            arrowSprite = GenArrow("obj_arrow", 48);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.name == "Event Canvas") Object.DestroyImmediate(c.gameObject);

            var canvasGo = new GameObject("Event Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var root = MakeImage(canvasGo.transform, "EventRoot", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.6f));
            root.rectTransform.offsetMin = Vector2.zero; root.rectTransform.offsetMax = Vector2.zero;

            var card = MakeImage(root.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.15f, 0.18f, 0.27f, 1f));
            card.rectTransform.sizeDelta = new Vector2(820, 480);
            var cardT = card.transform;

            var title = MakeText(cardT, "Title", "เหตุการณ์!", 42, new Vector2(0.5f, 1f), new Vector2(0, -48), new Vector2(740, 60), new Color(0.42f, 0.26f, 0.58f), FontStyles.Bold, TextAlignmentOptions.Center);
            var desc = MakeText(cardT, "Desc", "รายละเอียด...", 30, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(720, 150), Color.white, FontStyles.Normal, TextAlignmentOptions.Top);

            var btns = new Button[2];
            var lbls = new TMP_Text[2];
            btns[0] = MakeButton(cardT, "Choice0", "ตัวเลือก 1", new Vector2(0, -70), new Vector2(700, 76), new Color(0.30f, 0.60f, 0.40f), out lbls[0]);
            btns[1] = MakeButton(cardT, "Choice1", "ตัวเลือก 2", new Vector2(0, -162), new Vector2(700, 76), new Color(0.32f, 0.48f, 0.68f), out lbls[1]);
            // ยึดปุ่มไว้ล่างการ์ด
            foreach (var b in btns)
            {
                var brt = b.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0f);
            }
            btns[0].GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 128);
            btns[1].GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 40);

            var em = canvasGo.AddComponent<EventManager>();
            em.panel = root.gameObject;
            em.titleText = title;
            em.descText = desc;
            em.choiceButtons = btns;
            em.choiceLabels = lbls;

            // ===== แถบภารกิจ (A) + ลูกศรชี้ทาง =====
            var objBar = MakeImage(canvasGo.transform, "ObjectiveBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Color(0.12f, 0.13f, 0.22f, 0.95f));
            objBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            objBar.rectTransform.sizeDelta = new Vector2(580, 58);
            objBar.rectTransform.anchoredPosition = new Vector2(0, -104);

            var arrowGo = new GameObject("Arrow", typeof(Image));
            arrowGo.transform.SetParent(objBar.transform, false);
            var aimg = arrowGo.GetComponent<Image>();
            aimg.sprite = arrowSprite; aimg.color = new Color(1f, 0.84f, 0.30f); aimg.preserveAspect = true;
            var art = aimg.rectTransform;
            art.anchorMin = art.anchorMax = new Vector2(0f, 0.5f); art.pivot = new Vector2(0.5f, 0.5f);
            art.anchoredPosition = new Vector2(42, 0); art.sizeDelta = new Vector2(40, 40);

            var objLabel = MakeText(objBar.transform, "ObjLabel", "ภารกิจ...", 24, new Vector2(0f, 0.5f), new Vector2(78, 0), new Vector2(480, 44), Color.white, FontStyles.Bold, TextAlignmentOptions.Left);

            var objHud = canvasGo.AddComponent<ObjectiveHUD>();
            objHud.root = objBar.gameObject; objHud.label = objLabel; objHud.arrow = art;
            objBar.gameObject.SetActive(false);
            em.objectiveHUD = objHud;

            var rootAnim = root.gameObject.AddComponent<UIPopupAnim>();
            rootAnim.scaleTarget = card.rectTransform;

            root.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] สร้างระบบเหตุการณ์เสร็จ!</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างระบบสุ่มเหตุการณ์เสร็จแล้ว! 🎲\n\n• ตอนเช้าวันใหม่มีโอกาส ~55% เกิดเหตุการณ์ (เว้นวันสอบ)\n• มีทั้งแบบเลือกทาง 2 ทาง และผลทันที\n• คลัง 10 เหตุการณ์ (เที่ยว/ติว/ชมรม/ป่วย/เจอเงิน ฯลฯ)\n\nกด Play แล้วข้ามวันเพื่อทดสอบ", "เยี่ยม!");
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
            Vector2 pos, Vector2 sizeDelta, Color col, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = col; t.fontStyle = style;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            if (thai != null) t.font = thai;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return t;
        }

        static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color col, out TMP_Text labelText)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = ui; img.type = Image.Type.Sliced; img.color = col;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;

            labelText = MakeText(go.transform, "Text", label, 28, new Vector2(0.5f, 0.5f), Vector2.zero, size, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            var trt = labelText.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        // วาดลูกศรสามเหลี่ยมชี้ขึ้น (ขาว) → PNG (มี AA)
        static Sprite GenArrow(string key, int size)
        {
            string path = "Assets/_Project/Art/UI/" + key + ".png";
            var ex = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (ex != null) return ex;
            const string dir = "Assets/_Project/Art/UI";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, ArrowA(x, y, size)));
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

        static float ArrowA(int px, int py, int size)
        {
            const int S = 3; float sum = 0f;
            for (int i = 0; i < S; i++)
                for (int j = 0; j < S; j++)
                {
                    float u = (px + (i + 0.5f) / S) / size;
                    float v = (py + (j + 0.5f) / S) / size;   // v ชี้ขึ้น
                    bool head = v >= 0.45f && v <= 0.92f && Mathf.Abs(u - 0.5f) <= (0.92f - v) * 0.62f;
                    bool stem = v >= 0.10f && v < 0.48f && Mathf.Abs(u - 0.5f) <= 0.13f;
                    if (head || stem) sum += 1f;
                }
            return sum / (S * S);
        }
    }
}
#endif
