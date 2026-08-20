#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NisitSimulator.Core;
using NisitSimulator.Systems;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // สร้างหน้าต่างกระเป๋า (Inventory) + ต่อ InventoryManager/InventoryUI + ตั้งร้านค้าให้เก็บเข้ากระเป๋า
    // ใช้: เมนู  Nisit -> Build Inventory   (ทำหลัง Build Shop)
    public static class M21InventoryBuilder
    {
        static readonly Color Ink = new Color(0.16f, 0.13f, 0.20f, 1f);
        static readonly Color PanelCol = new Color(0.20f, 0.17f, 0.28f, 0.98f);
        static readonly Color Gold = new Color(1f, 0.86f, 0.42f);
        static readonly Color SlotCol = new Color(1f, 1f, 1f, 0.08f);

        static Sprite Round => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/round.png")
            ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        static Sprite Circle => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/circle.png") ?? Round;
        // สไปรต์ลูกกวาด (จาก Polish Menu Layout)
        static Sprite Pill  => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_pill2.png") ?? Round;
        static Sprite Gloss => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_gloss.png");
        static Color Shift(Color c, float d) => new Color(Mathf.Clamp01(c.r + d), Mathf.Clamp01(c.g + d), Mathf.Clamp01(c.b + d), c.a);

        public static bool SuppressDialog = false;   // ปิด popup เมื่อเรียกจาก Rebuild All

        [MenuItem("Nisit/Build Inventory")]
        public static void BuildInventory()
        {
            // 1) InventoryManager บน GameManager
            var gm = GameObject.Find("GameManager");
            if (gm == null) { gm = new GameObject("GameManager"); gm.AddComponent<GameManager>(); }
            if (gm.GetComponent<InventoryManager>() == null) gm.AddComponent<InventoryManager>();

            // 2) ตั้งร้านค้าทุกร้าน (ยกเว้นโรงอาหาร) ให้เก็บเข้ากระเป๋า — หาแบบกันพลาด (รวม inactive)
            int shopsSet = 0;
            foreach (var sc in Resources.FindObjectsOfTypeAll<ShopController>())
            {
                if (!sc.gameObject.scene.IsValid()) continue;   // ข้าม prefab asset
                bool isCafe = sc.gameObject.name.Contains("Cafeteria") || sc.gameObject.name.Contains("โรงอาหาร");
                sc.storeToInventory = !isCafe;                  // ร้านค้า=เข้ากระเป๋า · โรงอาหาร=กินทันที
                EditorUtility.SetDirty(sc);
                if (!isCafe) shopsSet++;
            }

            // 3) UI
            DestroyIfExists("Inventory Canvas");
            var canvasGo = new GameObject("Inventory Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 14;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var ui = canvasGo.AddComponent<InventoryUI>();

            var dim = Img(canvasGo.transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, null, new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim);
            var card = Img(dim.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 560), Round, PanelCol);
            Deco(card);

            var title = Txt(card.transform, "Title", "กระเป๋า", 34, TextAlignmentOptions.Left, Color.white, FontStyles.Bold);
            Place(title, new Vector2(0, 1), new Vector2(24, -14), new Vector2(360, 44));
            var hintTop = Txt(card.transform, "Hint", "กด I เพื่อปิด  ·  คลิกไอเทมเพื่อใช้", 18, TextAlignmentOptions.Right, new Color(0.82f, 0.87f, 0.96f), FontStyles.Normal);
            Place(hintTop, new Vector2(1, 1), new Vector2(-24, -18), new Vector2(400, 34));

            // กริดช่องเก็บของ (4 คอลัมน์)
            var content = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            content.transform.SetParent(card.transform, false);
            SetStretch(content.transform, Vector2.zero, Vector2.one, new Vector2(20, 74), new Vector2(-20, -74));
            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150, 150);
            grid.spacing = new Vector2(12, 12);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            // ต้นแบบช่อง (ทั้งช่อง = ปุ่มใช้) — Icon + Name + Count
            var slot = new GameObject("Slot", typeof(Image), typeof(Button));
            slot.transform.SetParent(content.transform, false);
            var slotImg = slot.GetComponent<Image>();
            slotImg.sprite = Round; slotImg.type = Image.Type.Sliced; slotImg.color = SlotCol;
            Deco(slotImg);

            var icon = Img(slot.transform, "Icon", new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(84, 84), Circle, Color.white);
            var nameT = Txt(slot.transform, "Name", "ไอเทม", 16, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            nameT.enableWordWrapping = true;
            Place(nameT, new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(140, 40));
            var countT = Txt(slot.transform, "Count", "", 18, TextAlignmentOptions.Right, Gold, FontStyles.Bold);
            Place(countT, new Vector2(1f, 1f), new Vector2(-8, -6), new Vector2(60, 26));
            slot.SetActive(false);

            // ข้อความกระเป๋าว่าง
            var empty = Txt(card.transform, "Empty", "กระเป๋าว่าง — ไปซื้อของที่ร้านค้าก่อน", 22, TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.85f), FontStyles.Italic);
            Place(empty, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 40));

            var closeBtn = Btn(card.transform, "ปิด (I)", new Color(0.80f, 0.72f, 0.96f));
            Place(closeBtn, new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(180, 48));

            ui.panel = dim.gameObject;
            ui.grid = content.transform;
            ui.slotTemplate = slot;
            ui.emptyHint = empty;
            // ปุ่มปิดใช้ event เดียวกับ Close ผ่าน onClick (ผูก runtime ไม่ได้จาก editor lambda) → ใช้ InventoryUI.Close ผ่าน component
            var closer = closeBtn.gameObject.AddComponent<InventoryCloseButton>();
            closer.ui = ui;

            // ฟอนต์ไทย
            var f = ThaiFontSetup.GetOrCreateThaiFont();
            if (f != null) foreach (var t in canvasGo.GetComponentsInChildren<TMP_Text>(true)) t.font = f;

            dim.gameObject.SetActive(false);
            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());   // เซฟทันที (กันหลุดเมื่อ tool อื่นเปิดฉากใหม่)
            Debug.Log($"<color=lime>[Nisit] สร้างกระเป๋าเสร็จ! ตั้งร้านค้าเก็บเข้ากระเป๋า {shopsSet} ร้าน · Play ได้เลย (กด I เปิดกระเป๋า)</color>");
            if (!SuppressDialog)
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างระบบกระเป๋าเสร็จ! 🎒\n\n• ซื้อของที่ร้านค้า → เข้ากระเป๋า\n• กด I เปิดกระเป๋า → คลิกไอเทมเพื่อใช้ (เพิ่มค่าสถานะ)\n• บันทึก/โหลดเกมเก็บไอเทมด้วย\n\nกด Ctrl+S แล้ว Play", "OK");
        }

        // ---------- helpers (ยกจาก ShopBuilder) ----------
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
            img.sprite = Pill; img.type = Image.Type.Sliced; img.color = col;
            Deco(img);

            var gl = Gloss;
            if (gl != null)
            {
                var g = new GameObject("Gloss", typeof(Image)); g.transform.SetParent(go.transform, false);
                var gi = g.GetComponent<Image>(); gi.sprite = gl; gi.type = Image.Type.Simple; gi.color = new Color(1f, 1f, 1f, 0.42f); gi.raycastTarget = false;
                var grt = gi.rectTransform; grt.anchorMin = new Vector2(0.06f, 0.48f); grt.anchorMax = new Vector2(0.94f, 0.92f); grt.offsetMin = grt.offsetMax = Vector2.zero;
            }

            var btn = go.GetComponent<Button>();
            var cb = btn.colors; cb.normalColor = col; cb.highlightedColor = Shift(col, 0.12f); cb.pressedColor = Shift(col, -0.10f); cb.fadeDuration = 0.1f; btn.colors = cb;

            var t = Txt(go.transform, "Text", label, 22, TextAlignmentOptions.Center, new Color(0.20f, 0.22f, 0.38f), FontStyles.Bold);
            Stretch(t);
            t.transform.SetAsLastSibling();
            return btn;
        }

        static void Deco(Graphic g)
        {
            var o = g.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.12f, 0.14f, 0.24f, 1f); o.effectDistance = new Vector2(5, -5); o.useGraphicAlpha = false;
            var s = g.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0f, 0f, 0f, 0.38f); s.effectDistance = new Vector2(4, -6);
        }

        static void Place(Component c, Vector2 anchor, Vector2 pos, Vector2 size)
        { var rt = (RectTransform)c.transform; rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size; }

        static void SetStretch(Component c, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        { var rt = (RectTransform)c.transform; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax; }

        static void Stretch(Component c) => SetStretch(c, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        static void DestroyIfExists(string name)
        { var go = GameObject.Find(name); if (go != null) Object.DestroyImmediate(go); }
    }
}
#endif
