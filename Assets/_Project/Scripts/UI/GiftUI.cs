using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Core;
using NisitSimulator.Systems;
using NisitSimulator.Interaction;
using NisitSimulator.Player;

namespace NisitSimulator.UI
{
    // 🎁 ให้ของขวัญ NPC — กด H ใกล้ NPC → เลือกไอเทมในกระเป๋าให้ → เพิ่มค่าสนิท
    //   สร้าง UI เองตอนรัน (ไม่ต้อง bake) · ตัว NPC เรียก GiftUI.EnsureExists() ตอน Start
    public class GiftUI : MonoBehaviour
    {
        static GiftUI _i;
        public static void EnsureExists()
        {
            if (_i == null) { var go = new GameObject("GiftUI"); _i = go.AddComponent<GiftUI>(); }
        }

        public float range = 3.5f;
        public KeyCode key = KeyCode.H;

        GameObject panel;
        RectTransform listRoot;
        TMP_Text titleText;
        TMP_FontAsset font;
        Transform player;
        PlayerMovement move;
        TalkNPC target;
        bool open;

        void Awake()
        {
            if (_i != null && _i != this) { Destroy(gameObject); return; }
            _i = this;
            font = Object.FindFirstObjectByType<TMP_Text>()?.font;   // ใช้ฟอนต์ไทยเดียวกับในเกม
            Build();
        }

        void Start()
        {
            var p = GameObject.Find("Player");
            if (p != null) { player = p.transform; move = p.GetComponent<PlayerMovement>(); }
        }

        void Update()
        {
            if (Input.GetKeyDown(key))
            {
                if (open) Close();
                else TryOpen();
            }
            else if (open && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        void TryOpen()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) return;
            if (move != null && !move.enabled) return;   // มีแผงอื่นเปิดอยู่
            if (player == null) return;

            target = NearestNpc();
            if (target == null) { HUDController.Toast("ไม่มีใครอยู่ใกล้ ๆ ให้ของขวัญ"); return; }

            var inv = InventoryManager.Instance;
            if (inv == null || inv.TotalCount() == 0) { HUDController.Toast("ไม่มีของในกระเป๋าให้ (ซื้อจากร้านก่อน)"); return; }

            Populate();
            open = true;
            panel.SetActive(true);
            if (move != null) move.enabled = false;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        }

        public void Close()
        {
            open = false;
            if (panel != null) panel.SetActive(false);
            if (move != null) move.enabled = true;
        }

        TalkNPC NearestNpc()
        {
            TalkNPC best = null; float bestSq = range * range;
            foreach (var n in Object.FindObjectsByType<TalkNPC>(FindObjectsSortMode.None))
            {
                if (!n.CanBefriend) continue;
                float sq = (n.transform.position - player.position).sqrMagnitude;
                if (sq <= bestSq) { bestSq = sq; best = n; }
            }
            return best;
        }

        void Populate()
        {
            if (titleText != null) titleText.text = $"ให้ของขวัญกับ {target.NpcName}";
            for (int i = listRoot.childCount - 1; i >= 0; i--) Destroy(listRoot.GetChild(i).gameObject);

            var inv = InventoryManager.Instance;
            float y = -6f;
            foreach (var s in inv.Stacks)
            {
                if (s == null || s.item == null) continue;
                string itemName = s.item.name;
                int friendship = Mathf.Clamp(8 + Mathf.RoundToInt(s.item.satisfaction), 5, 20);
                var btn = MakeRow($"{itemName}  x{s.count}   <color=#FF7BA6>+{friendship} สนิท</color>", y);
                y -= 48f;
                btn.onClick.AddListener(() => Give(itemName, friendship));
            }
        }

        void Give(string itemName, int friendship)
        {
            var inv = InventoryManager.Instance;
            if (inv == null || target == null) { Close(); return; }
            if (!inv.RemoveByName(itemName)) { Close(); return; }

            target.ReceiveGift(itemName, friendship);
            SFXManager.Coin();

            if (inv.TotalCount() == 0) Close();
            else Populate();   // ให้ต่อได้
        }

        // ---------- สร้าง UI ----------
        void Build()
        {
            var canGo = new GameObject("Gift Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canGo.transform.SetParent(transform, false);
            var canvas = canGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 70;
            var scaler = canGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

            var dim = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canGo.transform, false);
            var drt = (RectTransform)dim.transform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            panel = dim;

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(dim.transform, false);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(560f, 680f);
            card.GetComponent<Image>().color = new Color(0.15f, 0.17f, 0.27f, 0.99f);

            titleText = MakeText(card.transform, "ให้ของขวัญ", new Vector2(0f, 300f), new Vector2(520, 50), 30, new Color(1f, 0.9f, 0.5f));
            titleText.alignment = TextAlignmentOptions.Center;
            MakeText(card.transform, "เลือกไอเทมเพื่อให้ (กด H หรือ Esc เพื่อปิด)", new Vector2(0f, 262f), new Vector2(520, 30), 18, new Color(0.75f, 0.8f, 0.9f)).alignment = TextAlignmentOptions.Center;

            var listGo = new GameObject("List", typeof(RectTransform));
            listGo.transform.SetParent(card.transform, false);
            listRoot = (RectTransform)listGo.transform;
            listRoot.anchorMin = new Vector2(0f, 1f); listRoot.anchorMax = new Vector2(1f, 1f); listRoot.pivot = new Vector2(0.5f, 1f);
            listRoot.anchoredPosition = new Vector2(0f, 232f); listRoot.sizeDelta = new Vector2(-40f, 460f);

            var close = MakeButton(card.transform, "ปิด", new Vector2(0f, -312f), new Vector2(200f, 50f), new Color(0.86f, 0.80f, 0.88f));
            close.onClick.AddListener(Close);

            panel.SetActive(false);
        }

        Button MakeRow(string label, float y)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(listRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y); rt.sizeDelta = new Vector2(0f, 42f);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);
            var t = MakeText(go.transform, label, Vector2.zero, Vector2.zero, 22, Color.white);
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(16, 0); trt.offsetMax = new Vector2(-12, 0);
            t.alignment = TextAlignmentOptions.Left;
            return go.GetComponent<Button>();
        }

        Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = col;
            var t = MakeText(go.transform, label, Vector2.zero, size, 24, new Color(0.15f, 0.17f, 0.28f));
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            t.alignment = TextAlignmentOptions.Center;
            return go.GetComponent<Button>();
        }

        TMP_Text MakeText(Transform parent, string s, Vector2 pos, Vector2 size, float fs, Color col)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = s; t.fontSize = fs; t.color = col; t.alignment = TextAlignmentOptions.Center;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return t;
        }
    }
}
