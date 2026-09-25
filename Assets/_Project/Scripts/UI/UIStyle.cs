using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.UI
{
    // สไตล์การ์ดมุมมนแบบ runtime (สร้าง sprite เอง ไม่ต้องมีไฟล์) — ให้ UI ที่สร้างตอนรันเข้าชุดกับเมนู
    public static class UIStyle
    {
        static Sprite _round, _gloss;
        public static Sprite Rounded { get { if (_round == null) _round = Gen(26); return _round; } }
        public static Sprite Gloss { get { if (_gloss == null) _gloss = GenGloss(); return _gloss; } }

        // ปุ่มกระจกวาว: มุมมน + แสงวาวด้านบน (gloss) + เงานุ่ม + ขอบบาง
        public static void Button(GameObject go, Color color)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = color;

            var old = go.transform.Find("Gloss"); if (old != null) Object.Destroy(old.gameObject);   // เอา gloss เก่าออก
            if (go.transform.Find("UIGloss") == null)
            {
                var g = new GameObject("UIGloss", typeof(RectTransform), typeof(Image));
                g.transform.SetParent(go.transform, false);
                var gi = g.GetComponent<Image>(); gi.sprite = Gloss; gi.type = Image.Type.Simple; gi.color = new Color(1f, 1f, 1f, 0.30f); gi.raycastTarget = false;
                var grt = (RectTransform)g.transform;
                grt.anchorMin = new Vector2(0f, 0.5f); grt.anchorMax = new Vector2(1f, 1f);
                grt.offsetMin = new Vector2(7f, 0f); grt.offsetMax = new Vector2(-7f, -5f);
                g.transform.SetSiblingIndex(0);   // อยู่หลังข้อความ/ไอคอน
            }
            var sh = go.GetComponent<Shadow>(); if (sh == null) sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.30f); sh.effectDistance = new Vector2(0f, -5f);
            var ol = go.GetComponent<Outline>(); if (ol == null) ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0.12f, 0.14f, 0.24f, 0.9f); ol.effectDistance = new Vector2(2.5f, -2.5f); ol.useGraphicAlpha = false;
        }

        // ไล่แสงขาวแนวตั้ง (บนสว่าง→ล่างจาง) สำหรับแสงวาวบนปุ่ม
        static Sprite GenGloss()
        {
            int h = 48;
            var tex = new Texture2D(4, h, TextureFormat.RGBA32, false); tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Pow(y / (float)(h - 1), 1.4f) * 0.9f;   // บน (y สูง) สว่าง
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), 100f);
        }

        // การ์ด: มุมมน + ขอบเข้ม + เงานุ่ม
        public static void Card(GameObject go, Color col)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = col;
            if (go.GetComponent<Outline>() == null)
            {
                var ol = go.AddComponent<Outline>(); ol.effectColor = new Color(0.10f, 0.12f, 0.22f, 1f); ol.effectDistance = new Vector2(4f, -4f); ol.useGraphicAlpha = false;
            }
            if (go.GetComponent<Shadow>() == null)
            {
                var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.35f); sh.effectDistance = new Vector2(4f, -8f);
            }
        }

        // สร้างสี่เหลี่ยมมุมมนสีขาว (9-slice) รัศมี r
        static Sprite Gen(int r)
        {
            int s = r * 2 + 8;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float cx = Mathf.Clamp(x, r, s - 1 - r);
                    float cy = Mathf.Clamp(y, r, s - 1 - r);
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01(r + 0.5f - d);   // ขอบนุ่ม (AA)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            var border = new Vector4(r, r, r, r);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}
