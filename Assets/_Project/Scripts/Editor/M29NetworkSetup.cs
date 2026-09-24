#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using NisitSimulator.Net;
using NisitSimulator.Systems;
using NisitSimulator.Player;

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
            if (nmGo.GetComponent<CoopBonus>() == null) nmGo.AddComponent<CoopBonus>();   // 🤝 โบนัสเล่นด้วยกัน
            EditorUtility.SetDirty(nm);
            EditorUtility.SetDirty(nmGo);

            // แต่งตัว: สร้างแคตตาล็อกตัวละคร + ติดตัวสลับโมเดลบน Player
            EnsureCatalog();
            var playerGo = GameObject.Find("Player");
            if (playerGo != null && playerGo.GetComponent<PlayerModelSwapper>() == null)
                playerGo.AddComponent<PlayerModelSwapper>();

            BuildNetworkUI();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] ตั้งค่า Multiplayer (MP-1) เสร็จ!</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "ตั้งค่า Multiplayer (MP-1/2/3) เสร็จ! 🌐\n\n" +
                "• NetworkAvatar (sync เดิน+ท่า) + ป้ายชื่อ\n" +
                "• NetworkManager + UnityTransport (127.0.0.1)\n" +
                "• UI: Host/Join (F3) · อีโมท (ค้าง B) · แชท+พิมพ์เอง (Y) · เทรด (G) · รายชื่อ (F2)\n" +
                "• แต่งตัว: ชื่อ+สี+แบบตัวละคร (ในแผง F3) · 🤝 Co-op โบนัสอยู่ใกล้เพื่อน\n\n" +
                "ทดสอบ: Window → Multiplayer Play Mode → เปิด Player 2\nเครื่องแรก Host, เครื่องสอง Join → เห็นกันเดิน+ท่า+แชท!\n\n" +
                "⚠️ ถ้า NetworkManager → PlayerPrefab ว่าง ให้ลาก NetworkAvatar.prefab ใส่เอง", "เยี่ยม!");
        }

        // ---------- แคตตาล็อกตัวละคร (แต่งตัว) ----------
        static CharacterCatalog EnsureCatalog()
        {
            const string dir = "Assets/_Project/Resources";
            const string path = dir + "/CharacterCatalog.asset";
            var cat = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(path);
            if (cat != null && cat.Count > 0) return cat;

            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/_Project", "Resources");
            if (cat == null) { cat = ScriptableObject.CreateInstance<CharacterCatalog>(); AssetDatabase.CreateAsset(cat, path); }

            string[] want = { "Ch29", "Ch07", "Ch12", "Ch21", "Remy" };   // index 0 = Ch29 (เริ่มต้น ตรงกับ Player/avatar)
            var models = new List<GameObject>(); var labels = new List<string>();
            foreach (var key in want)
            {
                var go = FindCharModel(key);
                if (go != null) { models.Add(go); labels.Add("แบบ " + (models.Count)); }
            }
            if (models.Count == 0)   // สำรอง: เอาโมเดลใดก็ได้ที่มี
                foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/_Project/Art/Characters" }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.Contains("@") || !p.ToLower().EndsWith(".fbx")) continue;
                    models.Add(AssetDatabase.LoadAssetAtPath<GameObject>(p)); labels.Add("แบบ " + models.Count);
                    if (models.Count >= 5) break;
                }
            cat.models = models.ToArray(); cat.labels = labels.ToArray();
            cat.controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(CharCtrl);
            EditorUtility.SetDirty(cat); AssetDatabase.SaveAssets();
            return cat;
        }

        static GameObject FindCharModel(string key)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/_Project/Art/Characters" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.Contains("@")) continue;
                if (Path.GetFileName(p).ToLower().Contains(key.ToLower()))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
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
            if (inst.GetComponent<TradeRelay>() == null) inst.AddComponent<TradeRelay>();

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
            pr.anchoredPosition = new Vector2(-24f, -24f); pr.sizeDelta = new Vector2(360f, 640f);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.24f, 0.92f);

            var labelCol = new Color(0.75f, 0.8f, 0.95f);
            var status = MakeText(panel.transform, font, "ยังไม่เชื่อมต่อ", new Vector2(0f, -16f), 16, new Color(0.9f, 0.92f, 1f));

            // ชื่อผู้เล่น
            MakeText(panel.transform, font, "ชื่อผู้เล่น:", new Vector2(0f, -44f), 15, labelCol);
            var nameIn = MakeInput(panel.transform, font, new Vector2(0f, -64f), new Vector2(320f, 38f));
            nameIn.text = ""; nameIn.characterLimit = 16;

            // สีตัวละคร (สวอตช์)
            MakeText(panel.transform, font, "สีตัวละคร:", new Vector2(0f, -108f), 15, labelCol);
            var pal = NisitSimulator.Net.NetworkAvatar.Palette;
            var swatches = new Button[pal.Length];
            float sw = 34f, gap = 6f; float totalW = pal.Length * (sw + gap) - gap; float x0 = -totalW / 2f + sw / 2f;
            for (int i = 0; i < pal.Length; i++)
                swatches[i] = MakeSwatch(panel.transform, new Vector2(x0 + i * (sw + gap), -130f), sw, pal[i]);

            // แบบตัวละคร (จากแคตตาล็อก)
            MakeText(panel.transform, font, "แบบตัวละคร:", new Vector2(0f, -172f), 15, labelCol);
            var cat = AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/_Project/Resources/CharacterCatalog.asset");
            int mCount = (cat != null && cat.Count > 0) ? cat.Count : 1;
            var modelBtns = new Button[mCount];
            float mw = 62f, mgap = 6f; float mtot = mCount * (mw + mgap) - mgap; float mx0 = -mtot / 2f + mw / 2f;
            for (int i = 0; i < mCount; i++)
            {
                string lbl = cat != null ? cat.Label(i) : ("แบบ " + (i + 1));
                var b = MakeButton(panel.transform, font, lbl, Vector2.zero, new Color(0.80f, 0.82f, 0.95f));
                var brt = b.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f); brt.pivot = new Vector2(0.5f, 1f);
                brt.anchoredPosition = new Vector2(mx0 + i * (mw + mgap), -192f); brt.sizeDelta = new Vector2(mw, 44f);
                var lt = b.GetComponentInChildren<TMP_Text>(); if (lt != null) lt.fontSize = 14;
                modelBtns[i] = b;
            }

            // IP (LAN)
            MakeText(panel.transform, font, "IP ของ Host (LAN):", new Vector2(0f, -244f), 15, labelCol);
            var ip = MakeInput(panel.transform, font, new Vector2(0f, -264f), new Vector2(320f, 38f));

            var host = MakeButton(panel.transform, font, "Host (สร้างห้อง)", new Vector2(0f, -318f), new Color(0.60f, 0.86f, 0.68f));
            var client = MakeButton(panel.transform, font, "Join", new Vector2(0f, -372f), new Color(0.62f, 0.80f, 0.96f));
            var disc = MakeButton(panel.transform, font, "ออกจากห้อง", new Vector2(0f, -426f), new Color(0.99f, 0.74f, 0.78f));

            // ----- เล่นออนไลน์ (Relay / Join Code) — ข้ามเน็ตได้ ต้องต่อ Unity Cloud -----
            MakeText(panel.transform, font, "— หรือเล่นออนไลน์ (Join Code) —", new Vector2(0f, -466f), 14, labelCol);
            var code = MakeInput(panel.transform, font, new Vector2(0f, -488f), new Vector2(320f, 38f));
            var hostR = MakeButton(panel.transform, font, "Host ออนไลน์", new Vector2(0f, -536f), new Color(0.55f, 0.82f, 0.70f));
            var joinR = MakeButton(panel.transform, font, "Join ออนไลน์", new Vector2(0f, -588f), new Color(0.60f, 0.74f, 0.96f));

            var ui = canGo.AddComponent<NetworkUI>();
            ui.panel = panel;
            ui.statusText = status;
            ui.ipInput = ip;
            ui.nameInput = nameIn;
            ui.colorButtons = swatches;
            ui.modelButtons = modelBtns;
            ui.hostButton = host; ui.clientButton = client; ui.disconnectButton = disc;
            ui.codeInput = code; ui.hostRelayButton = hostR; ui.joinRelayButton = joinR;

            // ----- แชท (มุมซ้ายล่าง) — log + ช่องพิมพ์ + ปุ่มสำเร็จรูป -----
            var chatLog = MakeText(canGo.transform, font, "", Vector2.zero, 20, new Color(1f, 1f, 1f, 0.95f));
            var clrt = chatLog.rectTransform;
            clrt.anchorMin = clrt.anchorMax = new Vector2(0f, 0f); clrt.pivot = new Vector2(0f, 0f);
            clrt.anchoredPosition = new Vector2(24f, 150f); clrt.sizeDelta = new Vector2(600f, 220f);
            chatLog.alignment = TextAlignmentOptions.BottomLeft;
            chatLog.outlineWidth = 0.15f; chatLog.outlineColor = new Color32(0, 0, 0, 255);

            var chatPanel = new GameObject("ChatPanel", typeof(RectTransform), typeof(Image));
            chatPanel.transform.SetParent(canGo.transform, false);
            var cprt = chatPanel.GetComponent<RectTransform>();
            cprt.anchorMin = cprt.anchorMax = new Vector2(0f, 0f); cprt.pivot = new Vector2(0f, 0f);
            cprt.anchoredPosition = new Vector2(24f, 24f); cprt.sizeDelta = new Vector2(600f, 112f);
            chatPanel.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.24f, 0.9f);

            // ช่องพิมพ์เอง (บนสุดของแผง)
            var chatInput = MakeInput(chatPanel.transform, font, new Vector2(0f, -8f), new Vector2(580f, 42f));
            chatInput.text = ""; chatInput.characterLimit = 120;

            // ปุ่มข้อความสำเร็จรูป (ล่างของแผง)
            string[] presets = { "สวัสดี!", "รอด้วย~", "ไปไหนกัน?", "เก่งมาก!" };
            var chatBtns = new Button[presets.Length];
            for (int i = 0; i < presets.Length; i++)
            {
                var b = MakeButton(chatPanel.transform, font, presets[i], Vector2.zero, new Color(0.62f, 0.80f, 0.96f));
                var brt = b.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0f); brt.pivot = new Vector2(0f, 0f);
                brt.anchoredPosition = new Vector2(10f + i * 146f, 8f); brt.sizeDelta = new Vector2(140f, 42f);
                chatBtns[i] = b;
            }

            var chat = canGo.AddComponent<ChatUI>();
            chat.panel = chatPanel; chat.log = chatLog; chat.input = chatInput; chat.presetButtons = chatBtns; chat.presets = presets;

            // ----- รายชื่อผู้เล่น (F2) — บนกลางจอ -----
            var plPanel = new GameObject("PlayerListPanel", typeof(RectTransform), typeof(Image));
            plPanel.transform.SetParent(canGo.transform, false);
            var plrt = plPanel.GetComponent<RectTransform>();
            plrt.anchorMin = plrt.anchorMax = new Vector2(0.5f, 1f); plrt.pivot = new Vector2(0.5f, 1f);
            plrt.anchoredPosition = new Vector2(0f, -120f); plrt.sizeDelta = new Vector2(340f, 300f);
            plPanel.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.24f, 0.92f);
            var plText = MakeText(plPanel.transform, font, "", Vector2.zero, 20, Color.white);
            plText.alignment = TextAlignmentOptions.TopLeft;
            var pltrt = plText.rectTransform;
            pltrt.anchorMin = Vector2.zero; pltrt.anchorMax = Vector2.one; pltrt.offsetMin = new Vector2(18f, 14f); pltrt.offsetMax = new Vector2(-18f, -14f);
            var plUI = canGo.AddComponent<PlayerListUI>();
            plUI.panel = plPanel; plUI.listText = plText;

            BuildEmoteWheel(canGo, font);
            BuildTradeUI(canGo, font);
        }

        // ----- UI เทรด/ให้ของ (กด G) -----
        static void BuildTradeUI(GameObject canGo, TMP_FontAsset font)
        {
            // การ์ดกลางจอ
            var panel = new GameObject("Trade Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canGo.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero; prt.sizeDelta = new Vector2(420f, 480f);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.24f, 0.96f);

            var title = MakeText(panel.transform, font, "ให้ของกับผู้เล่น", new Vector2(0f, -34f), 24, Color.white);
            title.alignment = TextAlignmentOptions.Center;
            var trt = title.rectTransform; trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(0, 40); trt.anchoredPosition = new Vector2(0f, -14f);

            // content (เรียงแนวตั้ง)
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(panel.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -60f); crt.sizeDelta = new Vector2(-32f, 0f);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.childControlWidth = true; vlg.childForceExpandWidth = true; vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // row template (ปุ่ม + ข้อความ)
            var row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(content.transform, false);
            row.GetComponent<Image>().color = new Color(0.62f, 0.80f, 0.96f);
            row.GetComponent<LayoutElement>().minHeight = 52;
            var rlbl = MakeText(row.transform, font, "ไอเทม", Vector2.zero, 20, new Color(0.14f, 0.16f, 0.26f));
            rlbl.alignment = TextAlignmentOptions.Center;
            var rlrt = rlbl.rectTransform; rlrt.anchorMin = Vector2.zero; rlrt.anchorMax = Vector2.one; rlrt.sizeDelta = Vector2.zero; rlrt.anchoredPosition = Vector2.zero;

            var hint = MakeText(panel.transform, font, "กด G ปิด", new Vector2(0f, 20f), 16, new Color(0.7f, 0.75f, 0.9f));
            hint.alignment = TextAlignmentOptions.Center;
            var hrt = hint.rectTransform; hrt.anchorMin = new Vector2(0, 0); hrt.anchorMax = new Vector2(1, 0); hrt.pivot = new Vector2(0.5f, 0f); hrt.sizeDelta = new Vector2(0, 30); hrt.anchoredPosition = new Vector2(0, 12);

            var ui = canGo.AddComponent<TradeUI>();
            ui.panel = panel; ui.content = content.transform; ui.rowTemplate = row; ui.titleText = title;
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

        static Button MakeSwatch(Transform parent, Vector2 pos, float size, Color col)
        {
            var go = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
            go.GetComponent<Image>().color = col;
            return go.GetComponent<Button>();
        }

        static TMP_InputField MakeInput(Transform parent, TMP_FontAsset font, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("IPInput", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);

            var input = go.AddComponent<TMP_InputField>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null) txt.font = font;
            txt.color = new Color(0.12f, 0.14f, 0.24f); txt.fontSize = 20; txt.alignment = TextAlignmentOptions.Left;
            var txtRt = textGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one; txtRt.offsetMin = new Vector2(10f, 2f); txtRt.offsetMax = new Vector2(-10f, -2f);

            input.textViewport = rt;
            input.textComponent = txt;
            if (font != null) input.fontAsset = font;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.text = "127.0.0.1";
            return input;
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
