using UnityEngine;
using TMPro;
using NisitSimulator.Systems;

namespace NisitSimulator.UI
{
    // 🧹 ทำให้หน้าจอตอนเล่นโล่ง ไม่รก
    //  - ซ่อนแผงภารกิจบนจอ → ย้ายไปดูในโทรศัพท์ (TAB > ภารกิจ)
    //  - ซ่อนป้ายเดือน/ฤดูกลางจอ (ดูได้ในแอปปฏิทิน)
    //  - จัดป้ายเลเวลไม่ให้ทับชิปขวาบน
    //  - มุมขวาล่างเหลือแค่ป้ายปุ่มลัด: [TAB] โทรศัพท์ (ภารกิจ x/3) · [M] แผนที่
    // แปะไว้ที่ HUD Canvas — ทำงานตอนเริ่มฉาก ไม่ต้องแก้ Editor builder เดิม
    public class CleanHUD : MonoBehaviour
    {
        public bool hideQuestPanel = true;
        public bool hideSeasonPill = true;
        public bool fixLevelBadge = true;

        private QuestSystem quests;
        private MinimapToggle map;
        private TMP_Text phoneHintText, mapHintText;
        private GameObject phoneHint;
        private int lastDone = -1, lastTotal = -1;

        System.Collections.IEnumerator Start()
        {
            yield return null; yield return null;   // รอให้ระบบอื่น (เช่น LevelSystem) สร้าง UI ของตัวเองก่อน
            quests = Object.FindFirstObjectByType<QuestSystem>();
            map = Object.FindFirstObjectByType<MinimapToggle>();

            if (hideQuestPanel) Hide("QuestPanel");
            if (hideSeasonPill) Hide("SeasonPill");
            if (fixLevelBadge) MoveLevelBadge();
            BuildHints();
        }

        static void Hide(string name)
        {
            foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) t.gameObject.SetActive(false);
        }

        // ป้ายเลเวลเดิมทับชิปเงิน → ย้ายไปใต้ชิปขวาบนแถวสุดท้าย
        void MoveLevelBadge()
        {
            var badge = FindRect("LevelBadge");
            if (badge == null) return;
            // ให้ป้ายเลเวลอยู่ชั้นเดียวกับ HUD — ไม่ลอยทับโทรศัพท์/เมนู
            var lvCanvas = badge.GetComponentInParent<Canvas>();
            if (lvCanvas != null && lvCanvas.sortingOrder > 10) lvCanvas.sortingOrder = 10;
            RectTransform lowest = null; float minY = float.MaxValue;
            var hud = GetComponent<RectTransform>();
            foreach (RectTransform c in transform)
            {
                if (!c.gameObject.activeSelf || c.name != "InfoChip") continue;
                var corners = new Vector3[4]; c.GetWorldCorners(corners);
                if (corners[0].y < minY) { minY = corners[0].y; lowest = c; }
            }
            if (lowest == null) return;
            var lc = new Vector3[4]; lowest.GetWorldCorners(lc);
            var bc = new Vector3[4]; badge.GetWorldCorners(bc);
            float gap = 6f * (hud != null ? hud.lossyScale.y : 1f);
            float dy = (lc[0].y - gap) - bc[2].y;           // ขอบบนป้าย = ขอบล่างชิป - ช่องว่าง
            float dx = lc[2].x - bc[2].x;                   // ชิดขวาเท่ากัน
            badge.position += new Vector3(dx, dy, 0f);
        }

        // ป้ายปุ่มลัดมุมขวาล่าง — ใช้ MapHint เดิมเป็นต้นแบบ
        void BuildHints()
        {
            GameObject mapHint = map != null ? map.hintWhenClosed : null;
            if (mapHint == null) return;
            mapHintText = mapHint.GetComponentInChildren<TMP_Text>(true);
            StyleHint(mapHintText);
            if (mapHintText != null) mapHintText.text = "<b>[M]</b> แผนที่";

            phoneHint = Instantiate(mapHint, mapHint.transform.parent);
            phoneHint.name = "PhoneHint";
            phoneHint.SetActive(true);
            var src = mapHint.GetComponent<RectTransform>();
            var rt = phoneHint.GetComponent<RectTransform>();
            rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, src.rect.height + 6f);
            rt.sizeDelta = new Vector2(Mathf.Max(src.sizeDelta.x, 250f), src.sizeDelta.y);
            phoneHintText = phoneHint.GetComponentInChildren<TMP_Text>(true);
            baseY = rt.anchoredPosition.y;
            StyleHint(phoneHintText);
            UpdatePhoneHint(true);
        }

        void Update()
        {
            UpdatePhoneHint(false);
            // มินิแมปเปิด → ยกป้ายโทรศัพท์ขึ้นไปอยู่เหนือกรอบแผนที่ (ไม่ทับกัน)
            if (phoneHint != null && map != null)
            {
                var rt = phoneHint.GetComponent<RectTransform>();
                float y = baseY;
                if (map.IsOpen && map.mapUI != null)
                {
                    var mr = map.mapUI.GetComponent<RectTransform>();
                    if (mr != null) y = mr.anchoredPosition.y + mr.rect.height + 36f;   // เว้นที่ให้ป้าย "แผนที่ (M ปิด)"
                }
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
            }
        }
        private float baseY;

        void UpdatePhoneHint(bool force)
        {
            if (phoneHintText == null) return;
            int done = quests != null ? quests.DoneCount : 0;
            int total = quests != null ? quests.Total : 0;
            if (!force && done == lastDone && total == lastTotal) return;
            lastDone = done; lastTotal = total;
            string q = total > 0
                ? (done >= total ? "  <color=#7BE38B>ภารกิจครบ!</color>" : $"  <color=#FFD766>ภารกิจ {done}/{total}</color>")
                : "";
            phoneHintText.text = $"<b>[TAB]</b> โทรศัพท์{q}";
        }

        // ตัวอักษรขาว บรรทัดเดียว ย่อเองถ้ายาว — อ่านง่ายบนพื้นเข้ม
        static void StyleHint(TMP_Text t)
        {
            if (t == null) return;
            t.color = Color.white;
            t.enableWordWrapping = false;
            t.enableAutoSizing = true;
            t.fontSizeMin = 10f;
            t.fontSizeMax = 18f;
            t.alignment = TextAlignmentOptions.Center;
        }

        static RectTransform FindRect(string name)
        {
            foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }
    }
}
