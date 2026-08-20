#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // แต่งฉากให้สวยขึ้น: ฟอนต์ไทย + สีสัน + ต้นไม้/ตึก + ป้ายบอกทิศ + จัด HUD
    // ใช้: เมนู  Nisit -> Make It Pretty   (ทำหลัง Build M1 + M3)
    public static class SceneBeautifier
    {
        const string MatDir  = "Assets/_Project/Art/Materials/";
        const string FontTtf = "Assets/_Project/Art/Fonts/LeelawadeeUI.ttf";
        const string FontSdf = "Assets/_Project/Art/Fonts/Mitr SDF.asset";

        [MenuItem("Nisit/Make It Pretty")]
        public static void Beautify()
        {
            EnsureFolder(MatDir);

            // ===== 1) สี พื้น/ตัวละคร =====
            var grass    = GetOrCreateMat("Grass",       new Color(0.42f, 0.68f, 0.34f));
            var skin     = GetOrCreateMat("PlayerBlue",  new Color(0.30f, 0.55f, 0.95f));
            var trunkMat = GetOrCreateMat("TreeTrunk",   new Color(0.45f, 0.31f, 0.19f));
            var leafMat  = GetOrCreateMat("TreeLeaf",    new Color(0.27f, 0.62f, 0.30f));
            var pathMat  = GetOrCreateMat("Path",        new Color(0.78f, 0.75f, 0.68f));
            var wallA    = GetOrCreateMat("BuildingA",   new Color(0.86f, 0.53f, 0.34f));
            var wallB    = GetOrCreateMat("BuildingB",   new Color(0.55f, 0.63f, 0.78f));
            var noseMat  = GetOrCreateMat("PlayerNose",  new Color(0.15f, 0.20f, 0.35f));

            var ground = GameObject.Find("Ground");
            if (ground) ground.GetComponent<Renderer>().sharedMaterial = grass;

            var player = GameObject.Find("Player");
            if (player)
            {
                player.GetComponent<Renderer>().sharedMaterial = skin;
                AddNose(player.transform, noseMat);   // ป้ายบอกทิศที่หันหน้า
            }

            // ===== 2) สภาพแวดล้อม (ต้นไม้/ตึก/ทางเดิน) =====
            DestroyIfExists("Environment");
            var env = new GameObject("Environment").transform;

            // ทางเดิน
            var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "Path"; path.transform.SetParent(env);
            path.transform.position = new Vector3(0, 0.02f, 0);
            path.transform.localScale = new Vector3(3f, 0.05f, 40f);
            path.GetComponent<Renderer>().sharedMaterial = pathMat;
            Object.DestroyImmediate(path.GetComponent<BoxCollider>());

            // ต้นไม้กระจายรอบๆ
            Vector3[] trees = {
                new(-8,0,6), new(9,0,-4), new(-12,0,-9),
                new(13,0,8), new(6,0,13), new(-6,0,-14), new(14,0,-11)
            };
            foreach (var p in trees) MakeTree(p, env, trunkMat, leafMat);

            // ตึก
            MakeBuilding(new Vector3(-16, 0, 2),  new Vector3(7, 9, 7), env, wallA);
            MakeBuilding(new Vector3(15, 0, 12),  new Vector3(8, 7, 6), env, wallB);
            MakeBuilding(new Vector3(2, 0, -18),  new Vector3(10, 8, 6), env, wallA);

            // ===== 3) ฟอนต์ไทย + จัด HUD =====
            var thaiFont = GetOrCreateThaiFont();
            var canvas = GameObject.Find("HUD Canvas");
            if (canvas)
            {
                AddHudPanelAndLabels(canvas.transform);
                if (thaiFont != null)
                    foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true))
                        t.font = thaiFont;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("<color=magenta>[Nisit] แต่งฉากเสร็จ! กด Ctrl+S แล้ว Play ดูความสวย</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "แต่งฉากเสร็จแล้ว! ✨\n\n- พื้นเขียว + ตัวละครสีน้ำเงิน (มีจุดบอกทิศ)\n- ต้นไม้ + ตึก + ทางเดิน\n- ตัวอักษรไทยอ่านออกแล้ว\n- HUD มีป้ายกำกับ\n\nกด Ctrl+S แล้ว Play ได้เลย", "เยี่ยม!");
        }

        // ---------- ฟอนต์ไทย (TMP dynamic) ----------
        private static TMP_FontAsset GetOrCreateThaiFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdf);
            if (existing != null) return existing;

            var src = AssetDatabase.LoadAssetAtPath<Font>(FontTtf);
            if (src == null) { Debug.LogWarning("[Nisit] ไม่พบไฟล์ฟอนต์ไทย"); return null; }

            // Dynamic = เรนเดอร์ตัวอักษรไทยตอนรันได้ทุกตัว
            var fa = TMP_FontAsset.CreateFontAsset(src, 90, 9, GlyphRenderMode.SDFAA,
                                                   1024, 1024, AtlasPopulationMode.Dynamic);
            AssetDatabase.CreateAsset(fa, FontSdf);
            fa.material.name = "LeelawadeeUI SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            if (fa.atlasTextures != null && fa.atlasTextures.Length > 0)
            {
                fa.atlasTextures[0].name = "LeelawadeeUI Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Nisit] สร้าง TMP ฟอนต์ไทยแล้ว");
            return fa;
        }

        // ---------- HUD: พื้นหลัง + ป้ายกำกับแถบ ----------
        private static void AddHudPanelAndLabels(Transform canvas)
        {
            var hud = canvas.GetComponent<HUDController>();
            if (hud == null) return;

            DestroyChildIfExists(canvas, "StatusPanel");
            Sprite ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // พื้นหลังโปร่งใสหลังแถบสถานะ
            var panel = new GameObject("StatusPanel", typeof(Image));
            panel.transform.SetParent(canvas, false);
            var pImg = panel.GetComponent<Image>();
            pImg.sprite = ui; pImg.type = Image.Type.Sliced;
            pImg.color = new Color(0f, 0f, 0f, 0.35f);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0, 1);
            prt.pivot = new Vector2(0, 1);
            prt.anchoredPosition = new Vector2(10, -8);
            prt.sizeDelta = new Vector2(266, 120);
            panel.transform.SetAsFirstSibling();   // ไปอยู่หลังสุด

            AddBarLabel(hud.energyFill, "พลังงาน");
            AddBarLabel(hud.healthFill, "สุขภาพ");
            AddBarLabel(hud.hungerFill, "ความอิ่ม");
        }

        private static void AddBarLabel(Image fill, string text)
        {
            if (fill == null) return;
            var bar = fill.transform.parent; // bar background
            DestroyChildIfExists(bar, "Label");
            var go = new GameObject("Label", typeof(TextMeshProUGUI));
            go.transform.SetParent(bar, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = 15; t.color = Color.white;
            t.alignment = TextAlignmentOptions.Left;
            t.fontStyle = FontStyles.Bold;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(10, 0); rt.offsetMax = new Vector2(-6, 0);
        }

        // ---------- helper สร้างวัตถุ 3D ----------
        private static void MakeTree(Vector3 pos, Transform parent, Material trunk, Material leaf)
        {
            var tree = new GameObject("Tree").transform;
            tree.SetParent(parent); tree.position = pos;

            var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.name = "Trunk"; t.transform.SetParent(tree);
            t.transform.localPosition = new Vector3(0, 1f, 0);
            t.transform.localScale = new Vector3(0.35f, 1f, 0.35f);
            t.GetComponent<Renderer>().sharedMaterial = trunk;
            Object.DestroyImmediate(t.GetComponent<Collider>());

            var l = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            l.name = "Leaves"; l.transform.SetParent(tree);
            l.transform.localPosition = new Vector3(0, 2.6f, 0);
            l.transform.localScale = new Vector3(2.4f, 2.4f, 2.4f);
            l.GetComponent<Renderer>().sharedMaterial = leaf;
            Object.DestroyImmediate(l.GetComponent<Collider>());
        }

        private static void MakeBuilding(Vector3 pos, Vector3 size, Transform parent, Material mat)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "Building"; b.transform.SetParent(parent);
            b.transform.position = new Vector3(pos.x, size.y / 2f, pos.z);
            b.transform.localScale = size;
            b.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void AddNose(Transform player, Material mat)
        {
            DestroyChildIfExists(player, "FacingNose");
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingNose"; nose.transform.SetParent(player);
            nose.transform.localPosition = new Vector3(0, 1f, 0.55f);
            nose.transform.localScale = new Vector3(0.25f, 0.25f, 0.35f);
            nose.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(nose.GetComponent<BoxCollider>());
        }

        // ---------- utility ----------
        private static Material GetOrCreateMat(string name, Color c)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path)) { Directory.CreateDirectory(path); AssetDatabase.Refresh(); }
        }

        private static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        private static void DestroyChildIfExists(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }
    }
}
#endif
