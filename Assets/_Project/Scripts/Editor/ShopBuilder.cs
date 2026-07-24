#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using TMPro;
using NisitSimulator.Core;
using NisitSimulator.Systems;
using NisitSimulator.Interaction;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // สร้างหน้าร้านค้า (UI) + จุดซื้อของในห้องร้านค้า + แหล่งเงิน (ค่าขนมรายวัน)
    // ใช้: เมนู  Nisit -> Build Shop   (ทำหลัง Build M3 HUD + Build Interiors + Setup Building Doors)
    public static class ShopBuilder
    {
        static readonly Color Ink = new Color(0.16f, 0.13f, 0.20f, 1f);
        static readonly Color PanelCol = new Color(0.20f, 0.17f, 0.28f, 0.98f);
        static readonly Color RowCol = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color Gold = new Color(1f, 0.86f, 0.42f);

        static Sprite Round => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/round.png")
            ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        static Sprite Circle => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/circle.png") ?? Round;

        [MenuItem("Nisit/Build Shop")]
        public static void BuildShop() => BuildStore("Shop Canvas", "ร้านค้า", ShopController.DefaultCatalog(),
            "Spawn_ร้านค้า", "ShopCounterSpot", "ShopStation", "กด E เพื่อซื้อของ");

        [MenuItem("Nisit/Build Cafeteria")]
        public static void BuildCafeteria() => BuildStore("Cafeteria Canvas", "โรงอาหาร", ShopController.CafeteriaCatalog(),
            "Spawn_โรงอาหาร", "CafeCounterSpot", "CafeStation", "กด E เพื่อสั่งอาหาร");

        static void BuildStore(string canvasName, string menuTitle, System.Collections.Generic.List<ShopItem> catalog,
            string spawnName, string counterSpotName, string stationName, string prompt)
        {
            // ===== 1) แหล่งเงิน =====
            var gm = GameObject.Find("GameManager");
            if (gm == null) { gm = new GameObject("GameManager"); gm.AddComponent<GameManager>(); }
            if (gm.GetComponent<DailyAllowance>() == null) gm.AddComponent<DailyAllowance>();

            // ===== 2) UI =====
            DestroyIfExists(canvasName);
            var canvasGo = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var shop = canvasGo.AddComponent<ShopController>();

            var dim = Img(canvasGo.transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, null, new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim);
            var card = Img(dim.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660, 740), Round, PanelCol);
            Deco(card);

            var title = Txt(card.transform, "Title", menuTitle, 34, TextAlignmentOptions.Left, Color.white, FontStyles.Bold);
            Place(title, new Vector2(0, 1), new Vector2(24, -12), new Vector2(320, 44));
            var money = Txt(card.transform, "Money", "เงิน: 0฿", 26, TextAlignmentOptions.Right, Gold, FontStyles.Bold);
            Place(money, new Vector2(1, 1), new Vector2(-24, -14), new Vector2(320, 40));

            // เมนูการ์ด (grid 2 คอลัมน์)
            var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
            content.transform.SetParent(card.transform, false);
            SetStretch(content.transform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 74), new Vector2(-20, -120));
            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300, 120);
            grid.spacing = new Vector2(12, 10);
            grid.padding = new RectOffset(6, 6, 6, 6);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            // การ์ดสินค้า (การ์ดทั้งใบ = ปุ่มซื้อ) — ไอคอน + ชื่อ + ผล + ราคา
            var cardGo = new GameObject("Item", typeof(Image), typeof(Button));
            cardGo.transform.SetParent(content.transform, false);
            var cImg = cardGo.GetComponent<Image>();
            cImg.sprite = Round; cImg.type = Image.Type.Sliced; cImg.color = new Color(1f, 1f, 1f, 0.09f);
            Deco(cImg);
            var sr = cardGo.AddComponent<ShopRow>();
            sr.buyButton = cardGo.GetComponent<Button>();

            var iconBg = Img(cardGo.transform, "IconBg", new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(50, 50), Circle, Color.white);
            Deco(iconBg); sr.iconBg = iconBg;
            var icon = Img(iconBg.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40), null, Color.white);
            SetStretch(icon, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            icon.enabled = false; sr.icon = icon;
            var nameT = Txt(cardGo.transform, "Name", "สินค้า", 21, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            Place(nameT, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(290, 26));
            var effT = Txt(cardGo.transform, "Effect", "+ผล", 14, TextAlignmentOptions.Center, new Color(0.82f, 0.87f, 0.96f), FontStyles.Normal);
            effT.enableWordWrapping = true;
            Place(effT, new Vector2(0.5f, 1), new Vector2(0, -72), new Vector2(290, 26));
            var priceT = Txt(cardGo.transform, "Price", "0฿", 20, TextAlignmentOptions.Center, Gold, FontStyles.Bold);
            Place(priceT, new Vector2(0.5f, 0), new Vector2(0, 5), new Vector2(290, 24));
            var badge = Img(cardGo.transform, "QtyBadge", new Vector2(1, 1), new Vector2(-4, -4), new Vector2(32, 32), Circle, new Color(0.95f, 0.42f, 0.42f));
            Deco(badge);
            var badgeT = Txt(badge.transform, "Qty", "x1", 16, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            Stretch(badgeT);
            badge.gameObject.SetActive(false);
            sr.nameText = nameT; sr.effectText = effT; sr.priceText = priceT;
            sr.qtyBadge = badge.gameObject; sr.qtyText = badgeT;
            cardGo.SetActive(false);

            // แถบล่าง: รวมราคา + ปุ่ม
            var totalT = Txt(card.transform, "Total", "รวม: 0฿", 26, TextAlignmentOptions.Center, Gold, FontStyles.Bold);
            Place(totalT, new Vector2(0.5f, 0), new Vector2(0, 82), new Vector2(400, 30));
            var closeBtn = Btn(card.transform, "ปิด", new Color(0.50f, 0.42f, 0.62f));
            Place(closeBtn, new Vector2(0, 0), new Vector2(22, 16), new Vector2(126, 52));
            var clearBtn = Btn(card.transform, "ล้าง", new Color(0.60f, 0.45f, 0.40f));
            Place(clearBtn, new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(120, 52));
            var confirmBtn = Btn(card.transform, "ยืนยันซื้อ", new Color(0.30f, 0.72f, 0.42f));
            Place(confirmBtn, new Vector2(1, 0), new Vector2(-22, 16), new Vector2(190, 52));

            shop.panel = dim.gameObject;
            shop.moneyText = money;
            shop.content = content.transform;
            shop.rowTemplate = cardGo;
            shop.closeButton = closeBtn;
            shop.clearButton = clearBtn;
            shop.confirmButton = confirmBtn;
            shop.totalText = totalT;

            // ===== bake ไอคอนจากโมเดล KayKit + ใส่แคตตาล็อก =====
            var items = catalog;
            int baked = 0;
            foreach (var it in items) { it.icon = BakeIcon(it.modelName); if (it.icon != null) baked++; }
            shop.catalog = items;
            EditorUtility.SetDirty(shop);

            // ===== 3) จุดซื้อของในห้องร้านค้า =====
            int stations = 0;
            var counterSpot = GameObject.Find(counterSpotName);   // มาร์กจากเคาน์เตอร์จริง (InteriorBuilder)
            var spawn = GameObject.Find(spawnName);
            var anchor = counterSpot != null ? counterSpot.transform : (spawn != null ? spawn.transform : null);
            if (anchor != null)
            {
                DestroyIfExists(stationName); DestroyIfExists(stationName + "Counter"); DestroyIfExists(stationName + "Sign");
                int layer = LayerMask.NameToLayer("Interactable");
                float unit = 1.3f;
                var player = GameObject.Find("Player");
                if (player != null)
                {
                    var cc = player.GetComponent<CharacterController>();
                    if (cc != null && cc.height > 0.01f) unit = cc.height * Mathf.Abs(player.transform.lossyScale.y);
                }

                // ถ้ามีเคาน์เตอร์จริง = ใช้ตำแหน่งนั้น (ไม่มีก้อนเขียว) · ถ้าไม่มีก็สำรองข้างจุดเกิด + cube
                Vector3 basePos = counterSpot != null ? anchor.position
                                                      : anchor.position + new Vector3(3.2f * unit, 0f, 1.2f * unit);
                if (counterSpot == null)
                {
                    var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    counter.name = stationName + "Counter";
                    counter.transform.position = basePos + new Vector3(0f, 0.5f * unit, 0f);
                    counter.transform.localScale = new Vector3(2.2f * unit, 1f * unit, 0.9f * unit);
                    counter.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.30f, 0.75f, 0.62f));
                }

                // ป้ายลอย
                var sign = new GameObject(stationName + "Sign");
                sign.transform.position = basePos + new Vector3(0f, 1.7f * unit, 0f);
                sign.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
                var tmp = sign.AddComponent<TextMeshPro>();
                tmp.text = menuTitle; tmp.fontSize = 4f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
                var thai = ThaiFontSetup.GetOrCreateThaiFont(); if (thai != null) tmp.font = thai;
                tmp.rectTransform.sizeDelta = new Vector2(8, 2);

                // จุดกด E (ผูกกับร้านนี้โดยตรง)
                var st = new GameObject(stationName);
                st.transform.position = basePos;
                if (layer >= 0) st.layer = layer;
                var col = st.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.8f * unit, 0f);
                col.size = new Vector3(1.4f * unit, 2.0f * unit, 1.4f * unit);   // แคบลง ต้องยืนใกล้เคาน์เตอร์
                var station = st.AddComponent<ShopStation>();
                station.shop = shop; station.prompt = prompt;
                stations = 1;
            }

            // ===== 4) ฟอนต์ไทย =====
            var f = ThaiFontSetup.GetOrCreateThaiFont();
            if (f != null) foreach (var t in canvasGo.GetComponentsInChildren<TMP_Text>(true)) t.font = f;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"<color=lime>[Nisit] สร้าง \"{menuTitle}\" เสร็จ! จุดกด E {stations} · ไอคอน {baked}/{items.Count} · กด Ctrl+S แล้ว Play</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                stations > 0
                ? $"สร้าง \"{menuTitle}\" เสร็จ!\n\nเข้าตึก → เดินไปเคาน์เตอร์ (ป้าย \"{menuTitle}\") → กด E → เลือกใส่ตะกร้า → ยืนยัน\n\nกด Ctrl+S แล้ว Play"
                : $"สร้าง UI \"{menuTitle}\" แล้ว แต่ไม่พบห้อง ({spawnName})\nกด Build Interiors + Setup Building Doors ก่อน แล้วรันอีกที", "OK");
        }

        // ---------- helpers ----------
        static Image Img(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = (sprite != null && sprite.border.sqrMagnitude > 0.01f) ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return img;
        }

        static TMP_Text Txt(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = color; t.fontStyle = style; t.enableWordWrapping = false;
            return t;
        }

        static Button Btn(Transform parent, string label, Color col)
        {
            var go = new GameObject(label + "Btn", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = Round; img.type = Image.Type.Sliced; img.color = col;
            Deco(img);
            var t = Txt(go.transform, "Text", label, 22, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            Stretch(t);
            return go.GetComponent<Button>();
        }

        static void Deco(Graphic g)
        {
            var o = g.gameObject.AddComponent<Outline>(); o.effectColor = Ink; o.effectDistance = new Vector2(4, -4); o.useGraphicAlpha = false;
            var s = g.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0f, 0f, 0f, 0.38f); s.effectDistance = new Vector2(4, -6);
        }

        static void Place(Component c, Vector2 anchor, Vector2 pos, Vector2 size)
        { var rt = (RectTransform)c.transform; rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size; }

        static void SetStretch(Component c, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        { var rt = (RectTransform)c.transform; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax; }

        static void Stretch(Component c) => SetStretch(c, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // เรนเดอร์โมเดล KayKit เป็นไอคอน PNG (พื้นหลังโปร่ง) เก็บใน UI/icons/ — คืน null ถ้าทำไม่ได้
        static Sprite BakeIcon(string modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return null;
            const string dir = "Assets/_Project/UI/icons";
            string path = dir + "/" + modelName + ".png";
            var cached = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (cached != null) return cached;

            var prefab = LoadModel(modelName);
            if (prefab == null) return null;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.position = new Vector3(0f, 5000f, 0f);
            inst.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            var b = ModelBounds(inst);
            float ext = Mathf.Max(b.size.x, b.size.y, b.size.z); if (ext < 0.0001f) ext = 1f;

            var camGo = new GameObject("IconCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = ext * 0.62f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.nearClipPlane = 0.01f; cam.farClipPlane = ext * 20f + 50f;
            cam.transform.position = b.center + new Vector3(0.55f, 0.5f, -1f).normalized * (ext * 3f + 2f);
            cam.transform.LookAt(b.center);

            var lightGo = new GameObject("IconLight", typeof(Light));
            var lt = lightGo.GetComponent<Light>(); lt.type = LightType.Directional; lt.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 205f, 0f);

            const int S = 256;
            var rt = new RenderTexture(S, S, 16, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();

            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, S, S), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null;

            Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); Object.DestroyImmediate(inst);
            rt.Release(); Object.DestroyImmediate(rt);

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static GameObject LoadModel(string name)
        {
            string[] folders = { "Assets/_Project/Art/Models/KayKit_Restaurant", "Assets/_Project/Art/Models/KayKit_Furniture" };
            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var g in AssetDatabase.FindAssets(name + " t:GameObject", new[] { folder }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (Path.GetFileNameWithoutExtension(p) == name)
                    {
                        var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                        if (go != null) return go;
                    }
                }
            }
            return null;
        }

        static Bounds ModelBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        static Material Mat(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c); return m;
        }

        static void DestroyIfExists(string name)
        { var go = GameObject.Find(name); if (go != null) Object.DestroyImmediate(go); }
    }
}
#endif
