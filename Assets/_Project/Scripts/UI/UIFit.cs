using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NisitSimulator.UI
{
    // ===== ตัวช่วยกันตัวอักษรล้นกรอบ (ใช้ร่วมทุกหน้า) =====
    //   • Scaler: ทุก canvas อ้างอิง 1920×1080 แบบ Expand → ทั้งหน้าอยู่ในจอเสมอ (จอกว้าง/จอสูงไม่ตัดขอบ)
    //   • OneLine: ข้อความบรรทัดเดียว ย่อเองเมื่อยาว แต่ไม่เล็กกว่าขนาดต่ำสุดที่อ่านได้
    //   • Outline: ขอบตัวอักษรให้อ่านได้บนพื้นหลายสี
    //   • ฟังก์ชันคำนวณล้วน (ทดสอบด้วย EditMode): ClampWidth, Ellipsize, LineCount
    public static class UIFit
    {
        public const float RefW = 1920f, RefH = 1080f;
        public const float MinReadable = 18f;   // ขนาดตัวอักษรต่ำสุดที่ 1080p

        public static void Scaler(CanvasScaler s)
        {
            if (s == null) return;
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(RefW, RefH);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        public static void OneLine(TMP_Text t, float max, float min = MinReadable)
        {
            if (t == null) return;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableAutoSizing = true;
            t.fontSizeMax = max;
            t.fontSizeMin = Mathf.Min(min, max);
        }

        public static void Wrap(TMP_Text t, float max, float min = MinReadable)
        {
            if (t == null) return;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.enableAutoSizing = true;
            t.fontSizeMax = max;
            t.fontSizeMin = Mathf.Min(min, max);
        }

        public static void Outline(TMP_Text t, Color col, float width = 0.18f)
        {
            if (t == null) return;
            t.outlineColor = col;
            t.outlineWidth = width;   // สร้าง material instance ของข้อความนี้เอง (ไม่กระทบฟอนต์กลาง)
        }

        // ความกว้างกล่องจากความกว้างข้อความ + ขอบซ้ายขวา จำกัดช่วง [min,max]
        public static float ClampWidth(float textWidth, float padding, float min, float max) =>
            Mathf.Clamp(textWidth + padding * 2f, min, max);

        // จำนวนบรรทัดโดยประมาณเมื่อห่อในความกว้างที่กำหนด
        public static int LineCount(float textWidth, float boxInnerWidth) =>
            boxInnerWidth <= 0f ? 1 : Mathf.Max(1, Mathf.CeilToInt(textWidth / boxInnerWidth - 0.001f));

        // ตัดข้อความยาวเกิน maxChars แล้วต่อ "…" (ไม่ตัดกลางสระ/วรรณยุกต์ไทยที่ลอยอยู่บน-ล่าง)
        public static string Ellipsize(string s, int maxChars)
        {
            if (string.IsNullOrEmpty(s) || maxChars <= 0 || s.Length <= maxChars) return s ?? "";
            int cut = maxChars - 1;
            while (cut > 0 && IsThaiCombining(s[cut])) cut--;   // อย่าให้สระบน/ล่างหรือวรรณยุกต์ไปขึ้นต้นตรง "…"
            return s.Substring(0, cut) + "…";
        }

        public static bool IsThaiCombining(char c) =>
            c == 'ั' || (c >= 'ิ' && c <= 'ฺ') || (c >= '็' && c <= '๎');

        // ScrollRect แนวตั้งพร้อม content ที่สูงตามลูก (VerticalLayoutGroup + ContentSizeFitter)
        public static ScrollRect VerticalScroll(Transform parent, string name, out RectTransform content, float spacing = 4f, RectOffset padding = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            var sr = go.GetComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40f;
            var c = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            c.transform.SetParent(go.transform, false);
            content = (RectTransform)c.transform;
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var vl = c.GetComponent<VerticalLayoutGroup>();
            vl.spacing = spacing; vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            vl.padding = padding ?? new RectOffset(0, 0, 0, 0);
            c.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content;
            sr.viewport = (RectTransform)go.transform;
            return sr;
        }

        public static void Stretch(RectTransform rt, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offMin; rt.offsetMax = offMax;
        }
    }
}
