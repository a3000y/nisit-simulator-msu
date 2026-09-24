#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // ⏸️ สร้างเมนู Pause ในเกม (Esc → เล่นต่อ/กลับเมนู/ออกเกม) — วางในฉากเกม
    //   ใช้: เมนู Nisit -> Build Pause Menu
    public static class M35PauseBuilder
    {
        public static bool SuppressDialog = false;
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string FontPath = "Assets/_Project/Art/Fonts/Mitr SDF.asset";

        [MenuItem("Nisit/Build Pause Menu", false, 34)]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
            var old = GameObject.Find("Pause Canvas");
            if (old != null) Object.DestroyImmediate(old);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var canGo = new GameObject("Pause Canvas");
            var canvas = canGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
            var scaler = canGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            canGo.AddComponent<GraphicRaycaster>();

            // dim เต็มจอ
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canGo.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            // การ์ดกลาง
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(panel.transform, false);
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(460f, 400f);
            card.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.28f, 0.98f);

            var title = MakeText(card.transform, font, "หยุดชั่วคราว", new Vector2(0f, -30f), 40, Color.white);
            title.alignment = TextAlignmentOptions.Center;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(0, 60); trt.anchoredPosition = new Vector2(0, -30);

            var resume = MakeButton(card.transform, font, "เล่นต่อ", new Vector2(0f, 30f), new Color(0.60f, 0.86f, 0.68f));
            var menu = MakeButton(card.transform, font, "กลับเมนูหลัก", new Vector2(0f, -46f), new Color(0.62f, 0.80f, 0.96f));
            var quit = MakeButton(card.transform, font, "ออกจากเกม", new Vector2(0f, -122f), new Color(0.99f, 0.74f, 0.78f));

            var pm = canGo.AddComponent<PauseMenu>();
            pm.panel = panel; pm.resumeButton = resume; pm.menuButton = menu; pm.quitButton = quit;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] สร้างเมนู Pause แล้ว</color>");
            if (!SuppressDialog)
                EditorUtility.DisplayDialog("Nisit Simulator", "สร้างเมนู Pause แล้ว! ⏸️\n\nกด Esc ในเกม → เล่นต่อ / กลับเมนูหลัก / ออกเกม", "เยี่ยม!");
        }

        static Button MakeButton(Transform parent, TMP_FontAsset font, string label, Vector2 pos, Color col)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(320f, 60f);
            go.GetComponent<Image>().color = col;
            var t = MakeText(go.transform, font, label, Vector2.zero, 24, new Color(0.14f, 0.16f, 0.26f));
            t.alignment = TextAlignmentOptions.Center;
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero; trt.anchoredPosition = Vector2.zero;
            return go.GetComponent<Button>();
        }

        static TMP_Text MakeText(Transform parent, TMP_FontAsset font, string s, Vector2 pos, float size, Color col)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = s; t.fontSize = size; t.color = col; t.alignment = TextAlignmentOptions.Center;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(400f, 60f);
            return t;
        }
    }
}
#endif
