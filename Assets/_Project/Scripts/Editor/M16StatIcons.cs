#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.EditorTools
{
    // ⚡❤️🍴 วาดไอคอนสถานะ (สายฟ้า/หัวใจ/ส้อม) แล้ววางทับวงกลมสีบน HUD
    //   (Kenney ไม่มีไอคอนพวกนี้ เลยวาดเองแบบ vector → PNG ขาวโปร่ง)
    // ใช้: เมนู  Nisit -> Build Stat Icons
    public static class M16StatIcons
    {
        const string GameScene = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string Dir = "Assets/_Project/UI";

        [MenuItem("Nisit/Build Stat Icons")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);

            var bolt  = Gen("icon_bolt",  Bolt);
            var heart = Gen("icon_heart", Heart);
            var fork  = Gen("icon_fork",  Fork);

            int n = 0;
            n += Place("EnergyIcon", bolt);
            n += Place("HealthIcon", heart);
            n += Place("HungerIcon", fork);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"<color=lime>[Nisit] ใส่ไอคอนสถานะ {n}/3 อันแล้ว</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                n == 3
                ? "ใส่ไอคอนสถานะเสร็จ! ⚡❤️🍴\n\n• พลังงาน → สายฟ้า\n• สุขภาพ → หัวใจ\n• ความอิ่ม → ส้อม\n\nวางทับวงกลมสีเดิม กด Play ดูได้เลย"
                : $"ใส่ได้ {n}/3 อัน\nไม่พบวงกลมไอคอนบางอัน — กด Nisit ▸ Build M3 HUD ก่อนแล้วลองใหม่", "โอเค");
        }

        // วางสัญลักษณ์ขาวทับวงกลมไอคอน
        static int Place(string iconName, Sprite sym)
        {
            var go = GameObject.Find(iconName);
            if (go == null || sym == null) return 0;

            var old = go.transform.Find("Symbol");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var s = new GameObject("Symbol", typeof(Image));
            s.transform.SetParent(go.transform, false);
            var img = s.GetComponent<Image>();
            img.sprite = sym; img.color = Color.white; img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(24, 24);
            s.transform.SetAsLastSibling();
            EditorUtility.SetDirty(img);
            return 1;
        }

        // ---------- สร้าง sprite จากฟังก์ชันรูปทรง (มี anti-alias) ----------
        static Sprite Gen(string key, System.Func<float, float, float> shape)
        {
            string path = Dir + "/" + key + ".png";
            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Sample(shape, x, y, size)));
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = Vector4.zero;
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // สุ่มตัวอย่าง 3x3 ต่อพิกเซล = ขอบเนียน
        static float Sample(System.Func<float, float, float> f, int px, int py, int size)
        {
            const int S = 3; float sum = 0f;
            for (int i = 0; i < S; i++)
                for (int j = 0; j < S; j++)
                    sum += f((px + (i + 0.5f) / S) / size, (py + (j + 0.5f) / S) / size);
            return sum / (S * S);
        }

        // ---------- รูปทรง (u,v = 0..1, v ชี้ขึ้น) คืน 1=ทึบ 0=โปร่ง ----------
        static float Heart(float u, float v)
        {
            float x = (u - 0.5f) * 2.3f;
            float y = (v - 0.45f) * 2.3f;
            float a = x * x + y * y - 1f;
            return (a * a * a - x * x * y * y * y) < 0f ? 1f : 0f;
        }

        static readonly Vector2[] BoltPoly =
        {
            new Vector2(0.52f, 0.96f), new Vector2(0.20f, 0.50f), new Vector2(0.45f, 0.50f),
            new Vector2(0.32f, 0.04f), new Vector2(0.80f, 0.58f), new Vector2(0.55f, 0.58f),
        };
        static float Bolt(float u, float v) => InPoly(BoltPoly, u, v) ? 1f : 0f;

        static float Fork(float u, float v)
        {
            bool handle = u >= 0.44f && u <= 0.56f && v >= 0.06f && v <= 0.54f;
            bool baseBar = u >= 0.33f && u <= 0.67f && v >= 0.50f && v <= 0.60f;
            bool p1 = u >= 0.35f && u <= 0.41f && v >= 0.58f && v <= 0.94f;
            bool p2 = u >= 0.47f && u <= 0.53f && v >= 0.58f && v <= 0.94f;
            bool p3 = u >= 0.59f && u <= 0.65f && v >= 0.58f && v <= 0.94f;
            return (handle || baseBar || p1 || p2 || p3) ? 1f : 0f;
        }

        // point-in-polygon (even-odd)
        static bool InPoly(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if ((p[i].y > y) != (p[j].y > y) &&
                    x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
#endif
