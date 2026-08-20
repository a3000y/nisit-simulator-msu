#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NisitSimulator.EditorTools
{
    // ป้ายชื่อคณะลอยเหนือตึกจริง 4 หลัง (ใช้ตึกที่มี กระจายในแมพ = เข้าชุดกัน)
    //   คณะ IT→IT · อาคารบริหาร→บริหารธุรกิจ · อาคารเรียน→วิทยาศาสตร์ · อาคารชมรม→นิเทศศาสตร์
    // ใช้: เมนู  Nisit -> Build Faculty Buildings
    public static class M25FacultyBuildings
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        public static bool SuppressDialog = false;

        // (ชื่อตึกในแมพ, ป้ายที่จะแสดง, สีพาสเทล)
        static readonly (string building, string label, Color col)[] Faculties =
        {
            ("คณะ IT",       "คณะ IT",           new Color(0.62f, 0.80f, 0.96f)),  // ฟ้า
            ("อาคารบริหาร",  "คณะบริหารธุรกิจ",   new Color(0.99f, 0.82f, 0.62f)),  // พีช
            ("อาคารเรียน",   "คณะวิทยาศาสตร์",    new Color(0.60f, 0.86f, 0.68f)),  // มินต์
            ("อาคารชมรม",    "คณะนิเทศศาสตร์",    new Color(0.80f, 0.72f, 0.96f)),  // ลาเวนเดอร์
        };

        static Sprite Round => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_round.png")
            ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        [MenuItem("Nisit/Build Faculty Buildings")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            var campus = GameObject.Find("Campus");
            if (campus == null)
            {
                if (!SuppressDialog) EditorUtility.DisplayDialog("Nisit Simulator", "ไม่พบ Campus — สร้างเมืองก่อน (Build M5 Gameplay)", "OK");
                return;
            }

            // root เก็บป้าย (ลบเก่าก่อน)
            var oldRoot = GameObject.Find("FacultySigns");
            if (oldRoot != null) Object.DestroyImmediate(oldRoot);
            var root = new GameObject("FacultySigns").transform;

            var thai = ThaiFontSetup.GetOrCreateThaiFont();
            var log = new System.Text.StringBuilder();
            int made = 0;

            foreach (var f in Faculties)
            {
                var bt = campus.transform.Find(f.building);
                if (bt == null) { log.AppendLine("• ไม่พบตึก " + f.building); continue; }

                var b = WorldBounds(bt.gameObject);
                Vector3 pos = new Vector3(b.center.x, b.max.y + 2.4f, b.center.z);   // ลอยเหนือตึก
                MakeSign(root, f.label, pos, f.col, thai);
                log.AppendLine($"✓ {f.building} → {f.label}");
                made++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"<color=lime>[Nisit] ป้ายคณะ {made} หลัง</color>\n" + log);
            if (!SuppressDialog)
                EditorUtility.DisplayDialog("Nisit Simulator",
                    $"ติดป้ายชื่อคณะ {made} ตึกแล้ว! 🏛️\n\n" + log +
                    "\n4 คณะกระจายอยู่ในแมพ (ใช้ตึกเดิม) เข้าชุดกัน\nกด Ctrl+S แล้ว Play เดินดูได้เลย", "เยี่ยม!");
        }

        // ป้าย = Canvas world-space (พื้นมุมโค้งสีพาสเทล + ข้อความเข้ม) หันเข้าหากล้องไอโซเมตริก
        static void MakeSign(Transform parent, string text, Vector3 pos, Color col, TMP_FontAsset font)
        {
            var go = new GameObject("Sign_" + text, typeof(Canvas));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(30f, 45f, 0f);   // หันเข้ากล้องไอโซเมตริก
            go.transform.localScale = Vector3.one * 0.012f;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(420, 130);

            // พื้นป้าย (มุมโค้ง + ขอบดำ + เงา)
            var bgGo = new GameObject("BG", typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var bg = bgGo.GetComponent<Image>();
            bg.sprite = Round; bg.type = Image.Type.Sliced; bg.color = col;
            var brt = bg.rectTransform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            var ol = bgGo.AddComponent<Outline>(); ol.effectColor = new Color(0.12f, 0.14f, 0.24f, 1f); ol.effectDistance = new Vector2(6, -6); ol.useGraphicAlpha = false;
            var sh = bgGo.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.35f); sh.effectDistance = new Vector2(6, -8);

            // ข้อความ (สีกรมเข้มบนพาสเทล)
            var tGo = new GameObject("Text", typeof(TextMeshProUGUI));
            tGo.transform.SetParent(go.transform, false);
            var t = tGo.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = 54; t.color = new Color(0.16f, 0.18f, 0.32f);
            t.alignment = TextAlignmentOptions.Center; t.fontStyle = FontStyles.Bold; t.enableWordWrapping = true;
            if (font != null) t.font = font;
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(16, 10); trt.offsetMax = new Vector2(-16, -10);
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
    }
}
#endif
