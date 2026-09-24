using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Core;
using NisitSimulator.Systems;
using NisitSimulator.Player;

namespace NisitSimulator.UI
{
    // 🏆 สมุดสถิติ & ความสำเร็จ — กด J เปิด/ปิด · โชว์สถิติสะสม (ซ้าย) + รายการความสำเร็จ (ขวา)
    //   สร้าง UI เองตอนรัน (ไม่ต้อง bake) · GameplayBootstrap เรียก EnsureExists() ตอนเข้าเกม
    public class AchievementsUI : MonoBehaviour
    {
        static AchievementsUI _i;
        public static void EnsureExists()
        {
            if (_i == null) { var go = new GameObject("AchievementsUI"); _i = go.AddComponent<AchievementsUI>(); }
        }

        public KeyCode key = KeyCode.J;
        GameObject panel;
        TMP_Text statsText, achText;
        TMP_FontAsset font;
        PlayerMovement move;
        bool open;

        void Awake()
        {
            if (_i != null && _i != this) { Destroy(gameObject); return; }
            _i = this;
            font = Object.FindFirstObjectByType<TMP_Text>()?.font;
            Build();
        }

        void Start()
        {
            var p = GameObject.Find("Player");
            if (p != null) move = p.GetComponent<PlayerMovement>();
        }

        void Update()
        {
            if (Input.GetKeyDown(key)) { if (open) Close(); else TryOpen(); }
            else if (open && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        void TryOpen()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) return;
            if (move != null && !move.enabled) return;
            Refresh();
            open = true; panel.SetActive(true);
            if (move != null) move.enabled = false;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        }

        public void Close()
        {
            open = false;
            if (panel != null) panel.SetActive(false);
            if (move != null) move.enabled = true;
        }

        void Refresh()
        {
            if (statsText != null) statsText.text = StatsBody();
            if (achText != null) achText.text = AchBody();
        }

        string StatsBody()
        {
            var s = StatsTracker.Instance;
            int friends = RelationshipManager.Instance.FriendCount;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>สถิติสะสม</b>\n");
            sb.AppendLine($"เวลาเล่น: {s.PlaytimeText()}");
            sb.AppendLine($"วันในมหาลัย: {s.GetInt("maxDay")} วัน");
            sb.AppendLine($"เข้าเรียน: {s.GetInt("classes")} คาบ");
            sb.AppendLine($"ทำภารกิจสำเร็จ: {s.GetInt("quests")} ครั้ง");
            sb.AppendLine($"ให้ของขวัญ: {s.GetInt("gifts")} ครั้ง");
            sb.AppendLine($"เพื่อน: {friends} คน");
            sb.AppendLine($"เงินที่หาได้รวม: {s.GetInt("moneyEarned"):n0}฿");
            sb.AppendLine($"ความรู้สะสมรวม: {s.GetInt("knowledgeGained"):n0}");
            return sb.ToString();
        }

        string AchBody()
        {
            var am = AchievementManager.Instance;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>ความสำเร็จ  {am.UnlockedCount}/{am.Total}</b>\n");
            foreach (var a in am.All)
            {
                bool got = am.IsUnlocked(a.id);
                string mark = got ? "<color=#7BE38B>[สำเร็จ]</color>" : "<color=#6B7386>[ล็อก]</color>";
                string title = got ? a.title : $"<color=#9AA6BF>{a.title}</color>";
                sb.AppendLine($"{mark} {title}");
                sb.AppendLine($"<size=68%><color=#8A93AB>{a.desc}</color></size>");
            }
            return sb.ToString();
        }

        // ---------- สร้าง UI ----------
        void Build()
        {
            var canGo = new GameObject("Achievements Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canGo.transform.SetParent(transform, false);
            var canvas = canGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 72;
            var scaler = canGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

            var dim = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canGo.transform, false);
            var drt = (RectTransform)dim.transform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
            panel = dim;

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(dim.transform, false);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(980f, 700f);
            card.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.26f, 0.99f);

            MakeText(card.transform, "สมุดนิสิต — สถิติ & ความสำเร็จ", new Vector2(0f, 315f), new Vector2(940, 50), 30, new Color(1f, 0.9f, 0.5f)).alignment = TextAlignmentOptions.Center;

            statsText = MakeText(card.transform, "", new Vector2(-238f, -14f), new Vector2(420, 540), 22, Color.white);
            statsText.alignment = TextAlignmentOptions.TopLeft;
            achText = MakeText(card.transform, "", new Vector2(232f, -14f), new Vector2(460, 540), 21, Color.white);
            achText.alignment = TextAlignmentOptions.TopLeft;

            var close = MakeButton(card.transform, "ปิด (J)", new Vector2(0f, -320f), new Vector2(220f, 50f), new Color(0.86f, 0.80f, 0.88f));
            close.onClick.AddListener(Close);

            panel.SetActive(false);
        }

        Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
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
