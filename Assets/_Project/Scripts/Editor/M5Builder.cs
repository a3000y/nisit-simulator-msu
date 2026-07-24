#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Core;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.Systems;
using NisitSimulator.Interaction;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // ประกอบระบบ M5: ชั้นปี/แพ้-ชนะ + ห้องเรียน/โรงอาหาร/หอพัก + หน้าจอจบเกม
    // ใช้: เมนู  Nisit -> Build M5 Gameplay   (ทำหลัง M1 + M3 + Make It Pretty)
    public static class M5Builder
    {
        const string FontSdf = "Assets/_Project/Art/Fonts/LeelawadeeUI SDF.asset";
        const string MatDir = "Assets/_Project/Art/Materials/";
        static TMP_FontAsset thai;
        static Sprite uiSprite;

        [MenuItem("Nisit/Build M5 Gameplay")]
        public static void Build()
        {
            thai = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdf);
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            int layer = LayerMask.NameToLayer("Interactable");

            // ===== 1) ระบบบน GameManager =====
            var gm = GameObject.Find("GameManager");
            if (gm == null) { gm = new GameObject("GameManager"); gm.AddComponent<GameManager>(); }
            if (gm.GetComponent<GameClock>() == null) gm.AddComponent<GameClock>();
            if (gm.GetComponent<GameOverWatcher>() == null) gm.AddComponent<GameOverWatcher>();
            if (gm.GetComponent<ProgressionManager>() == null) gm.AddComponent<ProgressionManager>();

            // เร่งเวลาแบบพอเหมาะ (1 วินาทีจริง = 20 นาทีในเกม → 1 วัน ≈ 72 วินาที)
            // ช้าพอให้วิ่งไปเข้าเรียนทันคาบ แต่เร็วพอให้เล่นจบใน ~15 นาที
            var clock = gm.GetComponent<GameClock>();
            clock.gameMinutesPerRealSecond = 20f;

            // ===== 2) ห้องเรียน (แปลงจากโต๊ะเรียน) =====
            var classroom = GameObject.Find("ห้องเรียน") ?? GameObject.Find("โต๊ะเรียน");
            if (classroom == null)
                classroom = MakeStation("ห้องเรียน", new Vector3(3, 0, 0), new Color(0.4f, 0.6f, 0.9f), layer);
            else
            {
                classroom.name = "ห้องเรียน";
                classroom.layer = layer;
            }
            var oldAct = classroom.GetComponent<ActivityStation>();
            if (oldAct != null) Object.DestroyImmediate(oldAct);
            if (classroom.GetComponent<ClassStation>() == null) classroom.AddComponent<ClassStation>();
            MakeLabel("ห้องเรียน", classroom.transform.position);

            // ===== 3) โรงอาหาร + หอพัก =====
            var canteen = MakeStation("โรงอาหาร", new Vector3(-5, 0, 4), new Color(0.95f, 0.6f, 0.25f), layer);
            SetActivity(canteen.GetComponent<ActivityStation>(), "กินข้าว",
                        energy: 5, hunger: 45, health: 3, knowledge: 0, satisfaction: 5, exp: 5);
            MakeLabel("โรงอาหาร", canteen.transform.position);

            var dorm = MakeStation("หอพัก", new Vector3(6, 0, -5), new Color(0.6f, 0.45f, 0.8f), layer);
            SetActivity(dorm.GetComponent<ActivityStation>(), "พักผ่อน",
                        energy: 55, hunger: -5, health: 12, knowledge: 0, satisfaction: 8, exp: 5);
            MakeLabel("หอพัก", dorm.transform.position);

            // ===== 4) เพิ่ม ปี + Toast บน HUD =====
            var hudCanvas = GameObject.Find("HUD Canvas");
            if (hudCanvas != null)
            {
                var hud = hudCanvas.GetComponent<HUDController>();
                DestroyChild(hudCanvas.transform, "YearText");
                DestroyChild(hudCanvas.transform, "ToastText");

                var yearText = NewText(hudCanvas.transform, "YearText", "ปี 1", 26,
                    TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(-20, -156), new Vector2(300, 40));
                hud.yearText = yearText;

                var toast = NewText(hudCanvas.transform, "ToastText", "", 30,
                    TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(900, 60));
                toast.color = new Color(1f, 0.95f, 0.5f);
                toast.fontStyle = FontStyles.Bold;
                hud.toastText = toast;
            }

            // ===== 5) หน้าจอจบเกม =====
            BuildEndScreen();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("<color=lime>[Nisit] ประกอบ M5 เสร็จ! กด Ctrl+S แล้ว Play</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "ประกอบ M5 เสร็จแล้ว! 🎓\n\nวิธีเล่น:\n- เข้าเรียน (E ที่ห้องเรียน) ช่วง 9-11 น. และ 14-16 น. → ได้ความรู้\n- กินข้าวที่โรงอาหาร (กันหิว)\n- พักที่หอพัก (เติมพลังงาน)\n- สะสมความรู้ให้ถึงเป้าก่อนสิ้นปี → เลื่อนชั้น\n- ผ่านปี 4 = จบการศึกษา!\n- พลังงาน/สุขภาพหมด = Game Over\n\nกด Ctrl+S แล้ว Play", "เยี่ยม!");
        }

        // ---------- หน้าจอจบเกม ----------
        private static void BuildEndScreen()
        {
            DestroyIfExists("End Canvas");
            var canvasGo = new GameObject("End Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;   // อยู่บน HUD
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var ctrl = canvasGo.AddComponent<EndScreenController>();

            // แผงเต็มจอสีดำโปร่ง
            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var pImg = panel.GetComponent<Image>();
            pImg.color = new Color(0f, 0f, 0f, 0.78f);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;

            var title = NewText(panel.transform, "Title", "GAME OVER", 80,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(1000, 120));
            title.fontStyle = FontStyles.Bold;

            var msg = NewText(panel.transform, "Message", "ข้อความ", 32,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1000, 80));

            var score = NewText(panel.transform, "Score", "คะแนนรวม: 0", 40,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1000, 60));

            var restart = NewButton(panel.transform, "เริ่มใหม่", new Vector2(-110, -130), new Color(0.3f, 0.6f, 0.35f));
            var quit    = NewButton(panel.transform, "ออก",     new Vector2(110, -130),  new Color(0.6f, 0.3f, 0.3f));

            ctrl.panel = panel;
            ctrl.titleText = title;
            ctrl.messageText = msg;
            ctrl.scoreText = score;
            ctrl.restartButton = restart;
            ctrl.quitButton = quit;

            panel.SetActive(false); // ซ่อนไว้ก่อน
        }

        // ---------- helper วัตถุ 3D ----------
        private static GameObject MakeStation(string name, Vector3 pos, Color color, int layer)
        {
            DestroyIfExists(name);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = new Vector3(pos.x, 0.75f, pos.z);
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            go.layer = layer;
            go.GetComponent<Renderer>().sharedMaterial = GetOrCreateMat("Station_" + name, color);
            go.AddComponent<ActivityStation>();
            return go;
        }

        private static void MakeLabel(string text, Vector3 stationPos)
        {
            var go = new GameObject("Label_" + text);
            go.transform.position = stationPos + Vector3.up * 2.2f;
            go.transform.rotation = Quaternion.Euler(30f, 45f, 0f); // หันเข้าหากล้อง isometric
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (thai != null) tmp.font = thai;
            tmp.GetComponent<RectTransform>().sizeDelta = new Vector2(8, 2);
        }

        // ---------- helper UI ----------
        private static TMP_Text NewText(Transform parent, string name, string text, float size,
            TextAlignmentOptions align, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white;
            if (thai != null) t.font = thai;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return t;
        }

        private static Button NewButton(Transform parent, string label, Vector2 pos, Color col)
        {
            var go = new GameObject(label + "Btn", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = uiSprite; img.type = Image.Type.Sliced; img.color = col;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(180, 60);

            var txt = NewText(go.transform, "Text", label, 28, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 60));
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        // ---------- ตั้งค่า ActivityStation (private fields) ----------
        private static void SetActivity(ActivityStation a, string name, float energy, float hunger,
            float health, float knowledge, float satisfaction, int exp)
        {
            var so = new SerializedObject(a);
            so.FindProperty("activityName").stringValue = name;
            so.FindProperty("energyChange").floatValue = energy;
            so.FindProperty("hungerChange").floatValue = hunger;
            so.FindProperty("healthChange").floatValue = health;
            so.FindProperty("knowledgeChange").floatValue = knowledge;
            so.FindProperty("satisfactionChange").floatValue = satisfaction;
            so.FindProperty("expReward").intValue = exp;
            so.ApplyModifiedProperties();
        }

        // ---------- utility ----------
        private static Material GetOrCreateMat(string name, Color c)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", c);
            return mat;
        }

        private static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        private static void DestroyChild(Transform parent, string name)
        {
            var c = parent.Find(name);
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }
    }
}
#endif
