#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // 🗺️ สร้าง Minimap (กล้องมองจากบน + วงกลมมุมขวาบน) — ตาราง 3.2 + HUD ใน PDF
    // ใช้: เมนู  Nisit -> Build Minimap
    public static class M13MinimapBuilder
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string FontSdf = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        const string RtPath = "Assets/_Project/Art/UI/MinimapRT.renderTexture";
        static TMP_FontAsset thai;
        static Sprite ui;

        [MenuItem("Nisit/Build Minimap")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            thai = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdf);
            ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // 1) RenderTexture asset (สร้างถ้ายังไม่มี)
            var rt = EnsureRenderTexture();

            // 2) กล้อง Minimap
            var oldCam = GameObject.Find("MinimapCamera");
            if (oldCam != null) Object.DestroyImmediate(oldCam);

            var camGo = new GameObject("MinimapCamera", typeof(Camera), typeof(MinimapFollow));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 22f;               // พื้นที่ที่เห็นรอบตัว (ยิ่งมากยิ่งซูมออก)
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.22f, 0.16f, 1f);
            cam.cullingMask = ~0;                      // เห็นทุกอย่าง
            cam.targetTexture = rt;
            cam.depth = -5;                            // เรนเดอร์ก่อนกล้องหลัก
            cam.allowMSAA = false;
            camGo.transform.position = new Vector3(0, 45, 0);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 3) UI วงกลมมุมขวาบน
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.name == "Minimap Canvas") Object.DestroyImmediate(c.gameObject);

            var canvasGo = new GameObject("Minimap Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // กรอบนอก (มุมขวาล่าง — เลี่ยงชนกับ HUD/ภารกิจ/โทรศัพท์)
            var frame = MakeImage(canvasGo.transform, "MinimapFrame", new Vector2(1f, 0f), new Vector2(1f, 0f), new Color(0.10f, 0.12f, 0.18f, 1f));
            frame.rectTransform.pivot = new Vector2(1f, 0f);
            frame.rectTransform.sizeDelta = new Vector2(280, 280);
            frame.rectTransform.anchoredPosition = new Vector2(-24, 24);

            // ภาพจากกล้อง Minimap
            var mapGo = new GameObject("MapImage", typeof(RawImage));
            mapGo.transform.SetParent(frame.transform, false);
            var raw = mapGo.GetComponent<RawImage>();
            raw.texture = rt;
            var mrt = raw.rectTransform;
            mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.sizeDelta = new Vector2(252, 252);
            mrt.anchoredPosition = Vector2.zero;

            // จุดผู้เล่น (อยู่กลางเสมอ เพราะกล้องตามตัว)
            var dot = MakeImage(frame.transform, "PlayerDot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(1f, 0.3f, 0.3f, 1f));
            dot.rectTransform.sizeDelta = new Vector2(16, 16);

            // ป้ายชื่อ
            MakeText(frame.transform, "MapLabel", "แผนที่ (M ปิด)", 20, new Vector2(0.5f, 1f), new Vector2(0, 24), new Vector2(220, 34), Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

            // ป้ายเล็ก "กด M เปิดแผนที่" (โชว์ตอนแผนที่ปิดอยู่)
            var hint = MakeImage(canvasGo.transform, "MapHint", new Vector2(1f, 0f), new Vector2(1f, 0f), new Color(0.10f, 0.12f, 0.18f, 0.75f));
            hint.rectTransform.pivot = new Vector2(1f, 0f);
            hint.rectTransform.sizeDelta = new Vector2(190, 44);
            hint.rectTransform.anchoredPosition = new Vector2(-24, 24);
            MakeText(hint.transform, "HintText", "กด M เปิดแผนที่", 20, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 40), Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

            // กด M เปิด/ปิด (ซ่อนไว้ก่อน)
            var toggle = canvasGo.AddComponent<MinimapToggle>();
            toggle.mapUI = frame.gameObject;
            toggle.minimapCam = cam;
            toggle.hintWhenClosed = hint.gameObject;
            toggle.key = KeyCode.M;
            toggle.startOpen = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] สร้าง Minimap เสร็จ!</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้าง Minimap เสร็จแล้ว! 🗺️\n\n• กด M เพื่อเปิด/ปิดแผนที่ (ซ่อนไว้ก่อน)\n• กล้องมองจากบนตามตัวผู้เล่น จุดแดง = ตัวเรา\n• แผนที่อยู่มุมขวาล่าง\n• ปรับระยะซูมได้ที่ MinimapCamera ▸ Orthographic Size\n\n(ตรงตาราง 3.2 + HUD ใน PDF)", "เยี่ยม!");
        }

        static RenderTexture EnsureRenderTexture()
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RtPath);
            if (rt != null) return rt;

            var dir = Path.GetDirectoryName(RtPath);
            if (!AssetDatabase.IsValidFolder(dir))
                Directory.CreateDirectory(Path.Combine(Application.dataPath, dir.Substring("Assets/".Length)));

            rt = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
            rt.name = "MinimapRT";
            AssetDatabase.CreateAsset(rt, RtPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<RenderTexture>(RtPath);
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
    }
}
#endif
