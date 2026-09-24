#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using NisitSimulator.Net;

namespace NisitSimulator.EditorTools
{
    // 🏠 สร้างฉากล็อบบี้เล่นหลายคน (02_Lobby): NetworkManager(scene mgmt) + UI Host/Join/แต่งตัว/รายชื่อ/เริ่มเกม
    //   NetworkManager ย้ายมาอยู่ที่ล็อบบี้ (persist → เข้าเกมพร้อมกันด้วย NGO LoadScene) · ลบ NM ออกจากฉากเกม
    //   ใช้: เมนู Nisit -> Build Lobby
    public static class M38LobbyBuilder
    {
        const string LobbyPath = "Assets/_Project/Scenes/02_Lobby.unity";
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string MenuPath = "Assets/Scenes/Scene1.unity";
        const string PrefabPath = "Assets/_Project/Prefabs/NetworkAvatar.prefab";
        const string FontPath = "Assets/_Project/Art/Fonts/Mitr SDF.asset";

        [MenuItem("Nisit/Build Lobby", false, 38)]
        public static void Build()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { EditorUtility.DisplayDialog("Nisit", "ไม่พบ NetworkAvatar.prefab — กด Setup Multiplayer ก่อน", "OK"); return; }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            // 1) ลบ NetworkManager ออกจากฉากเกม (ล็อบบี้เป็นเจ้าของ NM แทน — กันซ้ำซ้อน)
            var gp = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
            var oldNm = GameObject.Find("NetworkManager");
            if (oldNm != null) { Object.DestroyImmediate(oldNm); EditorSceneManager.SaveScene(gp); }

            // 2) สร้างฉากล็อบบี้ใหม่
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(500f, 3f, 490f);   // ไกลจากจุด (0,0,0) ที่อวตารอาจ spawn
                cam.transform.rotation = Quaternion.identity;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.13f, 0.16f, 0.26f);
            }
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // 3) NetworkManager + UnityTransport (เปิด scene management → เริ่มเกมพร้อมกันได้)
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var utp = nmGo.AddComponent<UnityTransport>();
            utp.SetConnectionData("127.0.0.1", 7777);
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp,
                PlayerPrefab = prefab,
                EnableSceneManagement = true,
            };
            nmGo.AddComponent<CoopBonus>();

            // 4) Canvas
            var canGo = new GameObject("Lobby Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            var root = canGo.transform;

            MakeImage(root, "BG", Vector2.zero, Vector2.one, new Color(0.10f, 0.13f, 0.21f, 1f));
            MakeText(root, font, "ล็อบบี้ — เล่นหลายคน", new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(900, 70), 48, new Color(1f, 0.9f, 0.5f), TextAlignmentOptions.Center);

            // ----- การ์ดซ้าย: เชื่อมต่อ + แต่งตัว (NetworkUI) -----
            var card = MakeCard(root, new Vector2(-470, -20), new Vector2(640, 720));
            var status = MakeText(card, font, "สร้างห้อง หรือเข้าห้องเพื่อเริ่ม", new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(600, 30), 18, new Color(0.9f, 0.92f, 1f), TextAlignmentOptions.Center);

            MakeLabel(card, font, "ชื่อผู้เล่น:", -56);
            var nameIn = MakeInput(card, font, new Vector2(0, -78), new Vector2(560, 44));
            nameIn.characterLimit = 16;

            MakeLabel(card, font, "สีตัวละคร:", -124);
            var pal = NetworkAvatar.Palette;
            var swatches = new Button[pal.Length];
            float sw = 40f, gp2 = 8f; float tot = pal.Length * (sw + gp2) - gp2; float x0 = -tot / 2f + sw / 2f;
            for (int i = 0; i < pal.Length; i++) swatches[i] = MakeSwatch(card, new Vector2(x0 + i * (sw + gp2), -150f), sw, pal[i]);

            MakeLabel(card, font, "แบบตัวละคร:", -196);
            var cat = AssetDatabase.LoadAssetAtPath<NisitSimulator.Systems.CharacterCatalog>("Assets/_Project/Resources/CharacterCatalog.asset");
            int mCount = (cat != null && cat.Count > 0) ? cat.Count : 1;
            var modelBtns = new Button[mCount];
            float mw = 88f, mg = 8f; float mtot = mCount * (mw + mg) - mg; float mx0 = -mtot / 2f + mw / 2f;
            for (int i = 0; i < mCount; i++)
            {
                var b = MakeButton(card, font, cat != null ? cat.Label(i) : ("แบบ " + (i + 1)), new Vector2(mx0 + i * (mw + mg), -222f), new Vector2(mw, 46f), new Color(0.80f, 0.82f, 0.95f), 16);
                modelBtns[i] = b;
            }

            MakeLabel(card, font, "IP ของ Host (LAN):", -284);
            var ip = MakeInput(card, font, new Vector2(0, -306), new Vector2(560, 44));
            var host = MakeButton(card, font, "Host (สร้างห้อง)", new Vector2(0, -360), new Vector2(560, 50), new Color(0.60f, 0.86f, 0.68f), 24);
            var client = MakeButton(card, font, "Join", new Vector2(0, -418), new Vector2(560, 50), new Color(0.62f, 0.80f, 0.96f), 24);
            var disc = MakeButton(card, font, "ออกจากห้อง", new Vector2(0, -476), new Vector2(560, 46), new Color(0.99f, 0.74f, 0.78f), 22);

            MakeLabel(card, font, "— หรือออนไลน์ (Join Code) —", -520);
            var code = MakeInput(card, font, new Vector2(0, -542), new Vector2(560, 44));
            var hostR = MakeButton(card, font, "Host ออนไลน์", new Vector2(-145, -596), new Vector2(270, 48), new Color(0.55f, 0.82f, 0.70f), 20);
            var joinR = MakeButton(card, font, "Join ออนไลน์", new Vector2(145, -596), new Vector2(270, 48), new Color(0.60f, 0.74f, 0.96f), 20);

            var nui = canGo.AddComponent<NetworkUI>();
            nui.panel = card.gameObject; nui.statusText = status; nui.ipInput = ip; nui.nameInput = nameIn;
            nui.colorButtons = swatches; nui.modelButtons = modelBtns;
            nui.hostButton = host; nui.clientButton = client; nui.disconnectButton = disc;
            nui.codeInput = code; nui.hostRelayButton = hostR; nui.joinRelayButton = joinR;

            // ----- การ์ดขวา: รายชื่อผู้เล่น + คำแนะนำ + เริ่มเกม -----
            var rcard = MakeCard(root, new Vector2(470, -20), new Vector2(640, 720));
            MakeText(rcard, font, "ห้องของเรา", new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(600, 40), 30, Color.white, TextAlignmentOptions.Center);
            var listText = MakeText(rcard, font, "ยังไม่มีผู้เล่นในห้อง", new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(560, 380), 24, new Color(0.92f, 0.94f, 1f), TextAlignmentOptions.Top);
            var hint = MakeText(rcard, font, "", new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(560, 80), 20, new Color(0.8f, 0.85f, 0.95f), TextAlignmentOptions.Center);
            var startBtn = MakeButton(rcard, font, "เริ่มเกม!", new Vector2(0, 96), new Vector2(400, 60), new Color(0.60f, 0.86f, 0.68f), 30, 0.5f, 0f);
            var backBtn = MakeButton(rcard, font, "กลับเมนู", new Vector2(0, 28), new Vector2(400, 50), new Color(0.86f, 0.80f, 0.88f), 24, 0.5f, 0f);

            var lobby = canGo.AddComponent<LobbyController>();
            lobby.startButton = startBtn; lobby.backButton = backBtn; lobby.hintText = hint; lobby.playerListText = listText;
            startBtn.gameObject.SetActive(false);

            // 5) เซฟฉาก + ตั้ง Build Settings (เมนู → ล็อบบี้ → เกม)
            EditorSceneManager.SaveScene(scene, LobbyPath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(LobbyPath, true),
                new EditorBuildSettingsScene(GameplayPath, true),
            };

            Debug.Log("<color=lime>[Nisit] สร้างฉากล็อบบี้แล้ว</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างล็อบบี้เล่นหลายคนแล้ว! 🏠\n\n• เมนู 'เล่นหลายคน' → เข้าล็อบบี้\n• Host/Join (LAN หรือโค้ดออนไลน์) → แต่งตัว + เห็นรายชื่อ\n• โฮสต์กด 'เริ่มเกม' → เข้าพร้อมกัน\n\nย้าย NetworkManager มาที่ล็อบบี้แล้ว (ลบออกจากฉากเกม)\nเทสต์ 2 หน้าต่าง (Multiplayer Play Mode) ได้", "เยี่ยม!");
        }

        // ---------- helpers ----------
        static Image MakeImage(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = col;
            var rt = img.rectTransform; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return img;
        }

        static RectTransform MakeCard(Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.14f, 0.17f, 0.27f, 0.98f);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static void MakeLabel(Transform parent, TMP_FontAsset font, string s, float y)
            => MakeText(parent, font, s, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(560, 26), 18, new Color(0.75f, 0.8f, 0.95f), TextAlignmentOptions.Left);

        static TMP_Text MakeText(Transform parent, TMP_FontAsset font, string s, Vector2 anchor, Vector2 pos, Vector2 size, float fs, Color col, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = s; t.fontSize = fs; t.color = col; t.alignment = align; t.enableWordWrapping = true;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return t;
        }

        static Button MakeButton(Transform parent, TMP_FontAsset font, string label, Vector2 pos, Vector2 size, Color col, float fs, float ax = 0.5f, float ay = 1f)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = col;
            var t = MakeText(go.transform, font, label, new Vector2(0.5f, 0.5f), Vector2.zero, size, fs, new Color(0.15f, 0.17f, 0.28f), TextAlignmentOptions.Center);
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }

        static Button MakeSwatch(Transform parent, Vector2 pos, float d, Color col)
        {
            var go = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(d, d);
            go.GetComponent<Image>().color = col;
            return go.GetComponent<Button>();
        }

        static TMP_InputField MakeInput(Transform parent, TMP_FontAsset font, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);
            var input = go.GetComponent<TMP_InputField>();

            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(go.transform, false);
            var art = (RectTransform)area.transform; art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = new Vector2(12, 6); art.offsetMax = new Vector2(-12, -6);

            var txt = new GameObject("Text", typeof(RectTransform)); txt.transform.SetParent(area.transform, false);
            var tt = txt.AddComponent<TextMeshProUGUI>(); if (font != null) tt.font = font; tt.fontSize = 22; tt.color = new Color(0.12f, 0.13f, 0.18f);
            var ttrt = (RectTransform)txt.transform; ttrt.anchorMin = Vector2.zero; ttrt.anchorMax = Vector2.one; ttrt.offsetMin = ttrt.offsetMax = Vector2.zero;

            input.textViewport = art; input.textComponent = tt; input.fontAsset = font; input.pointSize = 22;
            return input;
        }
    }
}
#endif
