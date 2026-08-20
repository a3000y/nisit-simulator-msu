#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NisitSimulator.Systems;

namespace NisitSimulator.EditorTools
{
    // ตั้งค่าปีการศึกษา (8 วัน/ปี) + สร้าง UI ฤดูกาล (โทนสีเต็มจอ + ป้ายภาค/ฤดู) + ต่อ SeasonManager
    // ใช้: เมนู  Nisit -> Build Season System
    public static class M7SeasonBuilder
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string FontSdf = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        static TMP_FontAsset thai;
        static Sprite ui;

        [MenuItem("Nisit/Build Season System")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            thai = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdf);
            ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // ---- ตั้งค่าปีการศึกษาบน ProgressionManager ในซีน ----
            var prog = Object.FindFirstObjectByType<ProgressionManager>();
            if (prog != null)
            {
                prog.daysPerYear = 12;   // 12 เดือน: ต้น 5 + ปลาย 5 + ฤดูร้อน 2
                prog.knowledgeTargets = new float[] { 180f, 420f, 720f, 1080f };
                EditorUtility.SetDirty(prog);
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // ---- โทนสีบรรยากาศ (canvas อยู่ใต้ HUD → ไม่ทับ UI) ----
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.name == "Season Tint" || c.name == "Season HUD") Object.DestroyImmediate(c.gameObject);

            var tintCanvas = MakeCanvas("Season Tint", -1);
            var tint = MakeImage(tintCanvas.transform, "Tint", new Color(0.36f, 0.52f, 0.68f, 0.13f));
            tint.rectTransform.anchorMin = Vector2.zero; tint.rectTransform.anchorMax = Vector2.one;
            tint.rectTransform.offsetMin = Vector2.zero; tint.rectTransform.offsetMax = Vector2.zero;
            tint.raycastTarget = false;

            // ---- อากาศตามฤดู (ฝน/หิมะ/แดด) ----
            var weatherGo = new GameObject("Weather", typeof(RectTransform), typeof(WeatherOverlay));
            weatherGo.transform.SetParent(tintCanvas.transform, false);
            var wrt = (RectTransform)weatherGo.transform;
            wrt.anchorMin = Vector2.zero; wrt.anchorMax = Vector2.one;
            wrt.offsetMin = Vector2.zero; wrt.offsetMax = Vector2.zero;
            weatherGo.GetComponent<WeatherOverlay>().sprite =
                AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // ---- ป้ายภาค/ฤดู (canvas เหนือ HUD) ----
            var hudCanvas = MakeCanvas("Season HUD", 5);
            var pill = MakeImage(hudCanvas.transform, "SeasonPill", new Color(0.10f, 0.14f, 0.22f, 0.7f));
            var prt = pill.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0, -14); prt.sizeDelta = new Vector2(360, 66);
            pill.raycastTarget = false;
            var label = MakeText(pill.transform, "SeasonLabel",
                "<size=27><b>มิถุนายน</b></size>\n<size=20>ภาคต้น • ฤดูฝน</size>", 24,
                new Color(0.99f, 0.96f, 0.87f));
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

            // ---- ต่อ SeasonManager ----
            var sm = hudCanvas.GetComponent<SeasonManager>() ?? hudCanvas.gameObject.AddComponent<SeasonManager>();
            sm.tintOverlay = tint;
            sm.seasonLabel = label;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] สร้างระบบฤดูกาลเสร็จ!</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างระบบฤดูกาลเสร็จแล้ว! 🌧️❄️☀️\n\n• ปีละ 12 วัน = 12 เดือน (มิ.ย.→พ.ค.)\n  ภาคต้น(ฝน) 5 + ภาคปลาย(หนาว) 5 + ภาคฤดูร้อน(ร้อน) 2\n• โทนสี + ป้ายบอกเดือน/ภาค/ฤดูบนจอ\n• สอบ 5 ครั้ง/ปี (วันที่ 3,5 / 8,10 / 12)\n\nเป้าความรู้: 180/420/720/1080 (ปรับได้ที่ ProgressionManager)\n\n(ถ้าป้ายทับ HUD ลากตำแหน่งใน SeasonPill ได้)", "เยี่ยม!");
        }

        // ---------- helpers ----------
        static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            return c;
        }

        static Image MakeImage(Transform parent, string name, Color col)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = ui; img.type = Image.Type.Sliced; img.color = col;
            img.rectTransform.sizeDelta = new Vector2(100, 100);
            return img;
        }

        static TMP_Text MakeText(Transform parent, string name, string text, float size, Color col)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = col;
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = FontStyles.Bold;
            if (thai != null) t.font = thai;
            return t;
        }
    }
}
#endif
