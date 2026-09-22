#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using NisitSimulator.Net;

namespace NisitSimulator.EditorTools
{
    // 🌐 MP-1: ตั้งค่า Multiplayer พื้นฐาน (NGO) แบบ "เสริมทับ SP ไม่แตะระบบเดิม"
    //   สร้าง: NetworkAvatar prefab + NetworkManager(UnityTransport) + UI ปุ่ม Host/Join
    //   ต้องลง package com.unity.netcode.gameobjects ก่อน · ใช้: Nisit -> Setup Multiplayer (MP-1)
    public static class M29NetworkSetup
    {
        const string CharFbx = "Assets/_Project/Art/Characters/Ch29_nonPBR.fbx";
        const string CharCtrl = "Assets/_Project/Art/Characters/NisitCharacter.controller";
        const string FontPath = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        const string PrefabDir = "Assets/_Project/Prefabs";
        const string PrefabPath = PrefabDir + "/NetworkAvatar.prefab";
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";

        [MenuItem("Nisit/Setup Multiplayer (MP-1)", false, 40)]
        public static void Setup()
        {
            var prefab = BuildAvatarPrefab();
            if (prefab == null) { EditorUtility.DisplayDialog("Nisit", "สร้าง prefab ไม่สำเร็จ (ไม่พบโมเดล Ch29)", "OK"); return; }

            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            // NetworkManager + UnityTransport
            var nmGo = GameObject.Find("NetworkManager") ?? new GameObject("NetworkManager");
            var nm = nmGo.GetComponent<NetworkManager>() ?? nmGo.AddComponent<NetworkManager>();
            var utp = nmGo.GetComponent<UnityTransport>() ?? nmGo.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            nm.NetworkConfig.PlayerPrefab = prefab;
            EditorUtility.SetDirty(nm);
            EditorUtility.SetDirty(nmGo);

            BuildNetworkUI();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] ตั้งค่า Multiplayer (MP-1) เสร็จ!</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "ตั้งค่า Multiplayer (MP-1/2/3) เสร็จ! 🌐\n\n" +
                "• NetworkAvatar (sync เดิน+ท่า) + ป้ายชื่อ\n" +
                "• NetworkManager + UnityTransport (127.0.0.1)\n" +
                "• UI: Host/Join (F3) · วงล้ออีโมท (กดค้าง B) · แชท (Y)\n\n" +
                "ทดสอบ: Window → Multiplayer Play Mode → เปิด Player 2\nเครื่องแรก Host, เครื่องสอง Join → เห็นกันเดิน+ท่า+แชท!\n\n" +
                "⚠️ ถ้า NetworkManager → PlayerPrefab ว่าง ให้ลาก NetworkAvatar.prefab ใส่เอง", "เยี่ยม!");
        }

        // ---------- prefab อวตารเครือข่าย ----------
        static GameObject BuildAvatarPrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx);
            if (model == null) return null;
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = "NetworkAvatar";

            var anim = inst.GetComponentInChildren<Animator>();
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(CharCtrl);
            if (anim != null && ctrl != null) anim.runtimeAnimatorController = ctrl;

            if (inst.GetComponent<NetworkObject>() == null) inst.AddComponent<NetworkObject>();
            if (inst.GetComponent<NetworkAvatar>() == null) inst.AddComponent<NetworkAvatar>();
            if (inst.GetComponent<ChatRelay>() == null) inst.AddComponent<ChatRelay>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
            Object.DestroyImmediate(inst);
            return prefab;
        }

        // ---------- UI ปุ่ม Host/Join ----------
        static void BuildNetworkUI()
        {
            var old = GameObject.Find("Network Canvas");
            if (old != null) Object.DestroyImmediate(old);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var canGo = new GameObject("Network Canvas");
            var canvas = canGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canGo.AddComponent<GraphicRaycaster>();

            // แผงมุมขวาบน
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canGo.transform, false);
            var pr = panel.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = new Vector2(1f, 1f); pr.pivot = new Vector2(1f, 1f);
            pr.anchoredPosition = new Vector2(-24f, -24f); pr.sizeDelta = new Vector2(320f, 260f);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.24f, 0.92f);

            var status = MakeText(panel.transform, font, "ยังไม่เชื่อมต่อ — กด Host หรือ Join",
                new Vector2(0f, -34f), 20, new Color(0.9f, 0.92f, 1f));
            var host = MakeButton(panel.transform, font, "Host (สร้างห้อง)", new Vector2(0f, -90f), new Color(0.60f, 0.86f, 0.68f));
            var client = MakeButton(panel.transform, font, "Join (127.0.0.1)", new Vector2(0f, -150f), new Color(0.62f, 0.80f, 0.96f));
            var disc = MakeButton(panel.transform, font, "ออกจากห้อง", new Vector2(0f, -210f), new Color(0.99f, 0.74f, 0.78f));

            var ui = canGo.AddComponent<NetworkUI>();
            ui.panel = panel;
            ui.statusText = status;
            ui.hostButton = host; ui.clientButton = client; ui.disconnectButton = disc;

            // ----- แชทด่วน (มุมซ้ายล่าง) -----
            var chatLog = MakeText(canGo.transform, font, "", Vector2.zero, 20, new Color(1f, 1f, 1f, 0.95f));
            var clrt = chatLog.rectTransform;
            clrt.anchorMin = clrt.anchorMax = new Vector2(0f, 0f); clrt.pivot = new Vector2(0f, 0f);
            clrt.anchoredPosition = new Vector2(24f, 100f); clrt.sizeDelta = new Vector2(600f, 220f);
            chatLog.alignment = TextAlignmentOptions.BottomLeft;
            chatLog.outlineWidth = 0.15f; chatLog.outlineColor = new Color32(0, 0, 0, 255);

            var chatPanel = new GameObject("ChatPanel", typeof(RectTransform), typeof(Image));
            chatPanel.transform.SetParent(canGo.transform, false);
            var cprt = chatPanel.GetComponent<RectTransform>();
            cprt.anchorMin = cprt.anchorMax = new Vector2(0f, 0f); cprt.pivot = new Vector2(0f, 0f);
            cprt.anchoredPosition = new Vector2(24f, 24f); cprt.sizeDelta = new Vector2(600f, 60f);
            chatPanel.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.24f, 0.9f);

            string[] presets = { "สวัสดี!", "รอด้วย~", "ไปไหนกัน?", "เก่งมาก!" };
            var chatBtns = new Button[presets.Length];
            for (int i = 0; i < presets.Length; i++)
            {
                var b = MakeButton(chatPanel.transform, font, presets[i], Vector2.zero, new Color(0.62f, 0.80f, 0.96f));
                var brt = b.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f); brt.pivot = new Vector2(0f, 0.5f);
                brt.anchoredPosition = new Vector2(10f + i * 146f, 0f); brt.sizeDelta = new Vector2(140f, 44f);
                chatBtns[i] = b;
            }

            var chat = canGo.AddComponent<ChatUI>();
            chat.panel = chatPanel; chat.log = chatLog; chat.presetButtons = chatBtns; chat.presets = presets;

            BuildEmoteWheel(canGo, font);
        }

        // ----- วงล้ออีโมท (กดค้าง B เลือก) — สไตล์ candy พาสเทล -----
        static void BuildEmoteWheel(GameObject canGo, TMP_FontAsset font)
        {
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_circle.png");
            var gloss  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_gloss.png");
            var round  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_round.png");

            // panel เต็มจอ (dim + วงล้อ)
            var panel = new GameObject("EmoteWheel", typeof(RectTransform));
            panel.transform.SetParent(canGo.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(panel.transform, false);
            var drt = dim.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

            // จานกลางวง + ชื่ออีโมทที่เลือก
            var hub = MakeCircle(panel.transform, round ?? circle, Vector2.zero, 160f, new Color(0.16f, 0.18f, 0.28f, 0.95f));
            var center = MakeText(hub, font, "เลือกอีโมท", Vector2.zero, 24, Color.white);
            center.alignment = TextAlignmentOptions.Center;
            var crt = center.rectTransform; crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.sizeDelta = Vector2.zero; crt.anchoredPosition = Vector2.zero;

            string[] labels = { "โบกมือ", "เชียร์", "ทักทาย" };
            Color[] cols = { new Color(0.62f, 0.80f, 0.96f), new Color(1.00f, 0.85f, 0.55f), new Color(0.80f, 1.00f, 0.84f) };
            float[] deg = { 90f, 210f, 330f };   // บน · ล่างซ้าย · ล่างขวา
            var items = new RectTransform[3]; var bgs = new Image[3]; var rings = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float a = deg[i] * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 180f;

                // วงแหวนไฮไลต์ (ใหญ่กว่า อยู่หลัง ปิดไว้ก่อน)
                var ring = MakeCircle(panel.transform, circle, pos, 152f, Color.white);
                var ringImg = ring.GetComponent<Image>(); ringImg.enabled = false; ringImg.raycastTarget = false;
                rings[i] = ringImg;

                // ไอคอนวงกลมพาสเทล
                var it = MakeCircle(panel.transform, circle, pos, 132f, cols[i]);
                if (gloss != null) { var g = MakeCircle(it, gloss, Vector2.zero, 132f, new Color(1, 1, 1, 0.5f)); g.GetComponent<Image>().raycastTarget = false; g.sizeDelta = new Vector2(132f, 132f); g.anchorMin = g.anchorMax = new Vector2(0.5f, 0.5f); }

                var lbl = MakeText(it, font, labels[i], Vector2.zero, 22, new Color(0.14f, 0.16f, 0.26f));
                lbl.alignment = TextAlignmentOptions.Center;
                var lrt = lbl.rectTransform; lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.sizeDelta = Vector2.zero; lrt.anchoredPosition = Vector2.zero;

                items[i] = it; bgs[i] = it.GetComponent<Image>();
            }

            var ew = canGo.AddComponent<EmoteWheel>();
            ew.panel = panel; ew.items = items; ew.itemBg = bgs; ew.rings = rings; ew.centerLabel = center;
            ew.names = labels; ew.kinds = new[] { 1, 2, 3 };
        }

        // วงกลม (ใช้ sprite ui_circle/ui_round) — คืน RectTransform
        static RectTransform MakeCircle(Transform parent, Sprite sp, Vector2 pos, float size, Color col)
        {
            var go = new GameObject("Circle", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            if (sp != null) img.sprite = sp;
            img.color = col;
            return rt;
        }

        static Button MakeButton(Transform parent, TMP_FontAsset font, string label, Vector2 pos, Color col)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(280f, 48f);
            go.GetComponent<Image>().color = col;

            var txt = MakeText(go.transform, font, label, Vector2.zero, 22, new Color(0.14f, 0.16f, 0.26f));
            txt.alignment = TextAlignmentOptions.Center;
            var trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero; trt.anchoredPosition = Vector2.zero;
            return go.GetComponent<Button>();
        }

        static TMP_Text MakeText(Transform parent, TMP_FontAsset font, string s, Vector2 pos, float size, Color col)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = s; t.fontSize = size; t.color = col;
            t.alignment = TextAlignmentOptions.Center;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(300f, 40f);
            return t;
        }
    }
}
#endif
