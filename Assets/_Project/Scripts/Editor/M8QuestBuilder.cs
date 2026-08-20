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
    // สร้างแผงภารกิจรายวัน (มุมขวาบน) + ต่อ QuestSystem
    // ใช้: เมนู  Nisit -> Build Quest System
    public static class M8QuestBuilder
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string FontSdf = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        static TMP_FontAsset thai;
        static Sprite ui;

        [MenuItem("Nisit/Build Quest System")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            thai = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdf);
            ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.name == "Quest Canvas") Object.DestroyImmediate(c.gameObject);

            var canvasGo = new GameObject("Quest Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // แผงมุมขวาบน
            var panel = MakeImage(canvasGo.transform, "QuestPanel", new Color(0.14f, 0.15f, 0.27f, 0.96f));
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 1f);   // มุมซ้ายบน
            prt.anchoredPosition = new Vector2(24, -170);   // ใต้แถบสถานะซ้าย (ไม่ทับ HUD ขวา)
            prt.sizeDelta = new Vector2(380, 250);
            panel.raycastTarget = false;

            MakeText(panel.transform, "Title", "ภารกิจวันนี้", 26, new Vector2(0.5f, 1f),
                new Vector2(0, -12), new Vector2(340, 34), new Color(0.96f, 0.80f, 0.36f), FontStyles.Bold, TextAlignmentOptions.Center);

            var rows = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                rows[i] = MakeText(panel.transform, "Quest" + i, "-", 22, new Vector2(0.5f, 1f),
                    new Vector2(0, -56 - i * 62), new Vector2(344, 58), Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            }

            var qs = canvasGo.AddComponent<QuestSystem>();
            qs.rows = rows;
            qs.dailyCount = 3;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] สร้างระบบภารกิจเสร็จ!</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างระบบภารกิจเสร็จแล้ว! 📋\n\n• สุ่ม 3 ภารกิจต่อวัน (มุมขวาบน)\n• ทำสำเร็จ = ได้เงิน/EXP/ความพอใจ + Toast\n• รีเซ็ตใหม่ทุกวัน\n\nติดตามความคืบหน้าอัตโนมัติจากการเรียน/หาเงิน/ทำกิจกรรม\n(ถ้าแผงทับ HUD ลากตำแหน่ง QuestPanel ได้)", "เยี่ยม!");
        }

        // ---------- helpers ----------
        static Image MakeImage(Transform parent, string name, Color col)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = ui; img.type = Image.Type.Sliced; img.color = col;
            img.rectTransform.sizeDelta = new Vector2(100, 100);
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
            t.raycastTarget = false;
            if (thai != null) t.font = thai;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return t;
        }
    }
}
#endif
