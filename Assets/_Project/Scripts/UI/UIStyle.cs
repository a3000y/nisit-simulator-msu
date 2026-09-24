using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.UI
{
    // สไตล์การ์ดมุมมนแบบ runtime (สร้าง sprite เอง ไม่ต้องมีไฟล์) — ให้ UI ที่สร้างตอนรันเข้าชุดกับเมนู
    public static class UIStyle
    {
        static Sprite _round;
        public static Sprite Rounded { get { if (_round == null) _round = Gen(26); return _round; } }

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
