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
                "ตั้งค่า Multiplayer (MP-1) เสร็จ! 🌐\n\n" +
                "• NetworkAvatar prefab (หุ่นเงา sync)\n" +
                "• NetworkManager + UnityTransport (127.0.0.1)\n" +
                "• UI ปุ่ม Host/Join (กด F3 เปิด/ปิด)\n\n" +
                "ทดสอบ: Window → Multiplayer Play Mode → เปิด Player 2\nแล้วเครื่องแรกกด Host, เครื่องสองกด Join → เห็นกันเดิน!\n\n" +
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
