#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // จัดเลย์เอาต์เมนู + ปุ่มให้สวยแบบพรีเมียม:
    //   การ์ดมุมโค้ง + เงาใต้การ์ด + vignette หรี่ขอบจอ + หัวเรื่องเงา/ไล่สีทอง + ปุ่มมุมโค้ง hover
    //   (สร้างเฉพาะรูปทรง UI มุมโค้ง/vignette เป็นไฟล์ PNG เล็ก ๆ ที่ Assets/_Project/Art/UI/)
    //   ไม่ยุ่งกับ "ภาพพื้นหลัง" — เอาภาพมาใส่ที่วัตถุ BG เองทีหลังได้
    // ใช้: เมนู  Nisit -> Polish Menu Layout
    public static class MenuPolish
    {
        const string MenuPath = "Assets/Scenes/Scene1.unity";
        const string FontSdf  = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        const string ArtDir   = "Assets/_Project/Art/UI";

        // ปิด popup ชั่วคราว (ให้ Build M4 Menu เรียกต่อกันแบบไม่เด้ง dialog หลายครั้ง)
        public static bool SuppressDialog = false;

        static TMP_FontAsset thai;
        static Sprite roundSprite;    // มุมโค้ง (9-slice)
        static Sprite vignetteSprite;
        static Sprite glowSprite;     // แสงเรืองวงกลมนุ่ม
        static Sprite buttonSprite;   // ปุ่มพิลล์ + ไล่สีมีมิติ (9-slice)
        static Sprite glowRectSprite; // แสงเรืองรอบปุ่ม (soft rounded)
        static Sprite glossSprite;    // แผ่นเงาวาวด้านบนปุ่ม (candy gloss)
        static Sprite circleSprite;   // วงกลมพื้นไอคอน
        static Sprite shineSprite;    // แถบแสงวิ่งกวาด
        static Sprite icPlay, icContinue, icSettings, icExit;   // ไอคอนปุ่ม

        [MenuItem("Nisit/Polish Menu Layout")]
        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().path != MenuPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
            }

            var canvasGo = GameObject.Find("Menu Canvas");
            if (canvasGo == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "ยังไม่พบเมนูหลัก (Menu Canvas)\n\nกรุณารัน  Nisit ▸ Build M4 Menu  ก่อนหนึ่งครั้ง", "โอเค");
                return;
            }

            thai           = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdf);
            roundSprite    = EnsureSprite("ui_round.png",    MakeRoundedTex,     24);
            vignetteSprite = EnsureSprite("ui_vignette.png", MakeVignetteTex,     0);
            glowSprite     = EnsureSprite("ui_soft.png",     MakeSoftCircleTex,   0);
            buttonSprite   = EnsureSprite("ui_pill2.png",    MakePillTex,        30);
            glossSprite    = EnsureSprite("ui_gloss.png",    MakeGlossTex,        0);
            circleSprite   = EnsureSprite("ui_circle.png",   MakeCircleTex,       0);
            shineSprite    = EnsureSprite("ui_shine.png",    MakeShineTex,        0);
            icPlay     = EnsureSprite("ui_ic_play.png",     () => MakeIcon("play"),     0);
            icContinue = EnsureSprite("ui_ic_continue.png", () => MakeIcon("continue"), 0);
            icSettings = EnsureSprite("ui_ic_settings.png", () => MakeIcon("settings"), 0);
            icExit     = EnsureSprite("ui_ic_exit.png",     () => MakeIcon("exit"),     0);
            var root = canvasGo.transform;

            EnsureVignette(root);
            HideOldDecor(root);        // ลบกล่อง/glow/เงาเก่าออก
            LayoutTitle(root);         // โลโก้ขอบหนา + เงานูน
            LayoutTagline(root);       // แคปซูลคำโปรย
            LayoutButtons(root);       // ปุ่มลูกกวาด + ฐานหนา + ไอคอน
            EnsureFooter(root);
            EnsureSparkles(root);      // ประกายแสงลอย (ambient)
            ReorderLayers(root);
            BuildParallax(root, canvasGo);
            BuildIntro(root, canvasGo); // แอนิเมชันตอนเข้า

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("<color=lime>[Nisit] แต่งเมนู (ดีไซน์ใหม่) เสร็จ!</color>");
            if (!SuppressDialog)
            EditorUtility.DisplayDialog("Nisit Simulator",
                "แต่งหน้าเมนูดีไซน์ใหม่เสร็จแล้ว! 🍬\n\n• ลบกล่องทึบ + แสงเรืองเบลอออก\n• โลโก้ขอบหนา + เงานูน 3D\n• ปุ่มลูกกวาด ฐานหนา + เงาวาว + ไอคอน\n\nกด Play ดูได้เลย", "เยี่ยม!");
        }

        // ---------------------------------------------------------------- ภาพพื้นหลังอลังการ

        [MenuItem("Nisit/Epic Menu Background")]
        public static void EpicBackground()
        {
            if (EditorSceneManager.GetActiveScene().path != MenuPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
            }
            var canvasGo = GameObject.Find("Menu Canvas");
            if (canvasGo == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "ยังไม่พบเมนูหลัก (Menu Canvas)\n\nกรุณารัน  Nisit ▸ Build M4 Menu  ก่อน", "โอเค");
                return;
            }
            var root = canvasGo.transform;

            var sprite = GenerateBigImage("menu_bg.png", 1920, 1080, MakeEpicBackgroundTex);
            ApplyBgSprite(root, sprite, 0.55f);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=lime>[Nisit] สร้างพื้นหลังอลังการเสร็จ!</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างภาพพื้นหลังอลังการเสร็จแล้ว! 🌅\n\nพระอาทิตย์ตก + เมฆ + ดาว + เงาเมือง/ภูเขา\n\n💡 กดคำสั่งนี้ซ้ำได้เรื่อย ๆ — จะสุ่มชุดใหม่ทุกครั้ง", "เยี่ยม!");
        }

        [MenuItem("Nisit/Day Campus Background")]
        public static void DayCampusBackground()
        {
            if (EditorSceneManager.GetActiveScene().path != MenuPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
            }
            var canvasGo = GameObject.Find("Menu Canvas");
            if (canvasGo == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "ยังไม่พบเมนูหลัก (Menu Canvas)\n\nกรุณารัน  Nisit ▸ Build M4 Menu  ก่อน", "โอเค");
                return;
            }
            var root = canvasGo.transform;

            var sprite = GenerateBigImage("menu_bg.png", 1920, 1080, MakeDayCampusTex);
            ApplyBgSprite(root, sprite, 0.28f);   // กลางวันสดใส → vignette เบา

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=lime>[Nisit] สร้างฉากมหาลัยกลางวันเสร็จ!</color>");
            if (!SuppressDialog)
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สร้างฉากมหาลัยกลางวันสดใสเสร็จแล้ว! 🏫☀️\n\nฟ้า + เมฆ + ตึกโรงเรียน + ต้นไม้ + สนามหญ้า\n\n💡 กดซ้ำได้เรื่อย ๆ — เมฆ/ต้นไม้จะสุ่มใหม่ทุกครั้ง", "เยี่ยม!");
        }

        // ใช้ภาพของผู้ใช้เอง (เช่นภาพ AI) เป็นพื้นหลัง — วางไฟล์ menu_custom.png ที่ Art/UI ก่อน
        [MenuItem("Nisit/Use Custom Background Image")]
        public static void UseCustomBackground()
        {
            if (EditorSceneManager.GetActiveScene().path != MenuPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
            }
            var canvasGo = GameObject.Find("Menu Canvas");
            if (canvasGo == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "ยังไม่พบเมนูหลัก (Menu Canvas)\n\nกรุณารัน  Nisit ▸ Build M4 Menu  ก่อน", "โอเค");
                return;
            }
            var root = canvasGo.transform;

            AssetDatabase.Refresh();
            string[] names = { "menu_custom.png", "menu_custom.jpg", "menu_custom.jpeg" };
            string assetPath = null;
            foreach (var n in names)
            {
                string ap = ArtDir + "/" + n;
                if (File.Exists(Application.dataPath + "/" + ap.Substring("Assets/".Length))) { assetPath = ap; break; }
            }
            if (assetPath == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "ยังไม่พบไฟล์ภาพครับ 🖼️\n\n1) เซฟภาพ AI เป็นชื่อ  menu_custom.png  (หรือ .jpg)\n2) ลากไฟล์ไปวางในโฟลเดอร์  Assets/_Project/Art/UI/  (หน้าต่าง Project ของ Unity)\n3) กดคำสั่งนี้อีกครั้ง", "โอเค");
                return;
            }

            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator", "โหลดภาพไม่สำเร็จ: " + assetPath, "โอเค");
                return;
            }
            ApplyBgSprite(root, sprite, 0.30f);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=lime>[Nisit] ใส่ภาพพื้นหลังของผู้ใช้แล้ว: " + assetPath + "</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "ใส่ภาพพื้นหลังของคุณแล้ว! 🎨\n\n(ภาพควรเป็นอัตราส่วน 16:9 เช่น 1920×1080 จะพอดีจอ)\nถ้าอยากเปลี่ยนภาพ: เอาไฟล์ใหม่ทับ menu_custom.png แล้วกดคำสั่งนี้อีกครั้ง", "เยี่ยม!");
        }

        // สลับไปใช้ "ฉาก 3D จริง" เป็นพื้นหลัง (ซ่อนภาพ 2D)
        [MenuItem("Nisit/Use 3D Background")]
        public static void UseThreeDBackground()
        {
            if (EditorSceneManager.GetActiveScene().path != MenuPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
            }
            var canvasGo = GameObject.Find("Menu Canvas");
            if (canvasGo == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "ยังไม่พบเมนูหลัก (Menu Canvas)\n\nกรุณารัน  Nisit ▸ Build M4 Menu  ก่อน", "โอเค");
                return;
            }
            var root = canvasGo.transform;

            // ซ่อนภาพ 2D ให้เห็นฉาก 3D
            var bgT = root.Find("BG");
            if (bgT != null) { var img = bgT.GetComponent<Image>(); if (img) img.enabled = false; }
            var vg = root.Find("Vignette");
            if (vg != null) { var i = vg.GetComponent<Image>(); if (i) i.color = new Color(1f, 1f, 1f, 0.20f); }

            // เปิดฉาก 3D
            var scenery = GameObject.Find("MenuScenery") ?? FindInactive("MenuScenery");
            if (scenery == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "ยังไม่มีฉาก 3D ในซีนนี้\n\nกรุณารัน  Nisit ▸ Menu Background (3D)  ก่อน\nแล้วค่อยกดคำสั่งนี้อีกครั้ง", "โอเค");
                return;
            }
            scenery.SetActive(true);

            // กล้อง: หมุนรอบ + ใช้ skybox
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                if (cam.GetComponent<MenuCameraOrbit>() == null) cam.gameObject.AddComponent<MenuCameraOrbit>();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=lime>[Nisit] สลับไปพื้นหลัง 3D แล้ว!</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "สลับไปใช้พื้นหลัง 3D จริงแล้ว! 🏫\n\nกด Play — กล้องจะหมุนรอบมหาลัยช้า ๆ มีความลึกจริง\n\n(อยากกลับไปภาพ 2D: กด Day Campus / Epic Menu Background)", "เยี่ยม!");
        }

        static GameObject FindInactive(string name)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name != name || go.hideFlags != HideFlags.None) continue;
                if (!go.scene.IsValid()) continue;   // ข้าม prefab asset
                return go;
            }
            return null;
        }

        // ใส่ภาพลงวัตถุ BG (เต็มจอ อยู่หลังสุด) + ปรับ vignette UI
        static void ApplyBgSprite(Transform root, Sprite sprite, float vignetteAlpha)
        {
            var bgT = root.Find("BG");
            Image bg = bgT != null ? bgT.GetComponent<Image>() : NewImage(root, "BG");
            bg.enabled = true;   // เผื่อก่อนหน้าสลับไปโหมด 3D (ปิดภาพไว้)
            bg.sprite = sprite; bg.type = Image.Type.Simple; bg.preserveAspect = false;
            bg.color = Color.white; bg.raycastTarget = false;
            var rt = bg.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            bg.transform.SetAsFirstSibling();

            var vg = root.Find("Vignette");
            if (vg != null) { var i = vg.GetComponent<Image>(); if (i) i.color = new Color(1f, 1f, 1f, vignetteAlpha); }
        }

        // ---------------------------------------------------------------- vignette

        static void EnsureVignette(Transform root)
        {
            if (vignetteSprite == null) return;
            var t = root.Find("Vignette");
            var img = t != null ? t.GetComponent<Image>() : NewImage(root, "Vignette");
            img.sprite = vignetteSprite;
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // ---------------------------------------------------------------- ลบของตกแต่งเก่า

        static void HideOldDecor(Transform root)
        {
            string[] gone = {
                "ContentBackdrop", "MenuCard", "CardShadow", "TitleGlow", "TitleShadow", "TitleAccent",
                "เล่นคนเดียวBtnGlow", "เล่นต่อBtnGlow", "ตั้งค่าBtnGlow", "ออกBtnGlow",
            };
            foreach (var n in gone)
            {
                var t = root.Find(n);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
        }

        // ---------------------------------------------------------------- title

        static void LayoutTitle(Transform root)
        {
            var titleT = root.Find("Title");
            if (titleT == null) return;
            var title = titleT.GetComponent<TMP_Text>();
            if (title == null) return;

            title.fontStyle = FontStyles.Bold;
            title.fontSize = 90;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(
                new Color(1.00f, 0.95f, 0.66f), new Color(1.00f, 0.95f, 0.66f),   // ทองอ่อนบน
                new Color(0.96f, 0.64f, 0.11f), new Color(0.96f, 0.64f, 0.11f));  // ทองเข้มล่าง
            Place(title.rectTransform, new Vector2(0, 250), new Vector2(1000, 140));

            // ขอบเข้มหนา + เงานูน 3D ผ่าน material instance (ไม่กระทบข้อความอื่น)
            var mat = title.fontMaterial;
            Color ink = new Color(0.09f, 0.15f, 0.27f, 1f);
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, ink);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.24f);
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.55f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.7f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -1.2f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.12f);

            // แสงเรืองทองนุ่มหลังโลโก้ (grandeur)
            var glowT = root.Find("LogoGlow");
            Image lglow = glowT != null ? glowT.GetComponent<Image>() : NewImage(root, "LogoGlow");
            if (glowSprite != null) { lglow.sprite = glowSprite; lglow.type = Image.Type.Simple; }
            lglow.color = new Color(1f, 0.86f, 0.5f, 0.26f);
            lglow.raycastTarget = false;
            Place(lglow.rectTransform, new Vector2(0, 250), new Vector2(1000, 460));
        }

        // แคปซูลคำโปรย "จำลองชีวิตนิสิต"
        static void LayoutTagline(Transform root)
        {
            var subT = root.Find("Sub");
            if (subT == null) return;
            var sub = subT.GetComponent<TMP_Text>();
            if (sub == null) return;
            sub.fontSize = 30;
            sub.fontStyle = FontStyles.Bold;
            sub.color = new Color(0.99f, 0.96f, 0.87f, 1f);
            Place(sub.rectTransform, new Vector2(0, 150), new Vector2(560, 46));

            // แคปซูลเข้มโปร่งหลังคำโปรย
            var pillT = root.Find("TaglinePill");
            Image pill = pillT != null ? pillT.GetComponent<Image>() : NewRounded(root, "TaglinePill");
            if (roundSprite != null) { pill.sprite = roundSprite; pill.type = Image.Type.Sliced; }
            pill.color = new Color(0.09f, 0.15f, 0.27f, 0.62f);
            pill.raycastTarget = false;
            Place(pill.rectTransform, new Vector2(0, 150), new Vector2(360, 54));
        }

        // ---------------------------------------------------------------- buttons

        static void LayoutButtons(Transform root)
        {
            StyleButton(root, "เล่นคนเดียวBtn", new Vector2(0,   78), new Color(0.60f, 0.86f, 0.68f), icPlay);     // มินต์พาสเทล
            StyleButton(root, "เล่นหลายคนBtn", new Vector2(0,  -12), new Color(0.99f, 0.82f, 0.62f), icContinue); // พีชพาสเทล
            StyleButton(root, "เล่นต่อBtn",     new Vector2(0, -102), new Color(0.62f, 0.80f, 0.96f), icContinue); // ฟ้าพาสเทล
            StyleButton(root, "ตั้งค่าBtn",      new Vector2(0, -192), new Color(0.80f, 0.72f, 0.96f), icSettings); // ลาเวนเดอร์
            StyleButton(root, "ออกBtn",         new Vector2(0, -282), new Color(0.99f, 0.74f, 0.78f), icExit);     // ชมพูพาสเทล
        }

        static void StyleButton(Transform root, string name, Vector2 pos, Color baseCol, Sprite icon)
        {
            var t = root.Find(name);
            if (t == null) return;
            var img = t.GetComponent<Image>();
            var btn = t.GetComponent<Button>();
            if (img == null || btn == null) return;

            var size = new Vector2(410, 76);
            Place(img.rectTransform, pos, size);
            if (buttonSprite != null) { img.sprite = buttonSprite; img.type = Image.Type.Sliced; }
            img.color = baseCol;

            // เส้นขอบดำหนา (สไตล์การ์ตูน)
            var ol = img.GetComponent<Outline>() ?? img.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0.12f, 0.14f, 0.24f, 1f); ol.effectDistance = new Vector2(5f, -5f); ol.useGraphicAlpha = false;

            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor      = baseCol;
            cb.highlightedColor = Shift(baseCol,  0.12f);
            cb.pressedColor     = Shift(baseCol, -0.10f);
            cb.selectedColor    = Shift(baseCol,  0.06f);
            cb.disabledColor    = new Color(baseCol.r, baseCol.g, baseCol.b, 0.35f);
            cb.fadeDuration     = 0.1f;
            cb.colorMultiplier  = 1f;
            btn.colors = cb;

            // ---- ฐานหนา 3D (สีเข้ม เยื้องลง อยู่หลังปุ่ม) ----
            var baseT = root.Find(name + "Base");
            Image bas = baseT != null ? baseT.GetComponent<Image>() : NewImage(root, name + "Base");
            if (buttonSprite != null) { bas.sprite = buttonSprite; bas.type = Image.Type.Sliced; }
            bas.color = Shift(baseCol, -0.26f);
            bas.raycastTarget = false;
            Place(bas.rectTransform, pos + new Vector2(0, -8), size);

            // ---- แผ่นเงาวาวด้านบน (candy gloss) ----
            var glossT = t.Find("Gloss");
            Image gloss = glossT != null ? glossT.GetComponent<Image>() : NewImage(t, "Gloss");
            if (glossSprite != null) { gloss.sprite = glossSprite; gloss.type = Image.Type.Simple; }
            gloss.color = new Color(1f, 1f, 1f, 0.5f);
            gloss.raycastTarget = false;
            var grt = gloss.rectTransform;
            grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = new Vector2(size.x * 0.9f, size.y * 0.55f);
            grt.anchoredPosition = new Vector2(0, size.y * 0.2f);

            // ---- ไอคอนวงกลมด้านซ้าย ----
            var badgeT = t.Find("Badge");
            Image badge = badgeT != null ? badgeT.GetComponent<Image>() : NewImage(t, "Badge");
            if (circleSprite != null) { badge.sprite = circleSprite; badge.type = Image.Type.Simple; }
            badge.color = new Color(1f, 1f, 1f, 0.22f);
            badge.raycastTarget = false;
            Place(badge.rectTransform, new Vector2(-size.x * 0.5f + 40f, 0f), new Vector2(46, 46));
            var icoT = badge.transform.Find("Ico");
            Image ico = icoT != null ? icoT.GetComponent<Image>() : NewImage(badge.transform, "Ico");
            if (icon != null) { ico.sprite = icon; ico.type = Image.Type.Simple; }
            ico.color = new Color(0.20f, 0.22f, 0.38f); ico.raycastTarget = false;
            Place(ico.rectTransform, Vector2.zero, new Vector2(26, 26));

            // ---- แสงวิ่งกวาด (clip ตามทรงปุ่มด้วย Mask) ----
            var mask = t.GetComponent<Mask>();
            if (mask == null) mask = t.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var shineT = t.Find("Shine");
            Image shine = shineT != null ? shineT.GetComponent<Image>() : NewImage(t, "Shine");
            if (shineSprite != null) { shine.sprite = shineSprite; shine.type = Image.Type.Simple; }
            shine.color = new Color(1f, 1f, 1f, 0f);
            shine.raycastTarget = false;
            var srt = shine.rectTransform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(size.x * 0.32f, size.y * 1.8f);
            srt.localRotation = Quaternion.Euler(0, 0, 18f);
            srt.anchoredPosition = new Vector2(-size.x * 0.6f, 0f);

            // ---- ตัวหนังสือ (บนสุด) ----
            var textT = t.Find("Text");
            if (textT != null)
            {
                var txt = textT.GetComponent<TMP_Text>();
                if (txt != null) { txt.fontSize = 32; txt.fontStyle = FontStyles.Bold; txt.color = new Color(0.20f, 0.22f, 0.38f); }
                textT.SetAsLastSibling();
            }

            // ยกตัว + แสงกวาดตอนชี้เมาส์
            var hover = t.GetComponent<UIButtonHover>();
            if (hover == null) hover = t.gameObject.AddComponent<UIButtonHover>();
            hover.glow = null;
            hover.shine = srt;
            hover.shineTravel = size.x * 0.6f;
        }

        // ---------------------------------------------------------------- footer

        static void EnsureFooter(Transform root)
        {
            var t = root.Find("Footer");
            var txt = t != null ? t.GetComponent<TMP_Text>() : NewText(root, "Footer");
            txt.text = "มหาวิทยาลัยมหาสารคาม • ปริญญานิพนธ์  |  v0.4";
            txt.fontSize = 24;
            txt.color = new Color(1f, 1f, 1f, 0.62f);
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            var rt = txt.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(1200, 40);
            rt.anchoredPosition = new Vector2(0, 28);
        }

        // ---------------------------------------------------------------- ลำดับการซ้อน

        static void ReorderLayers(Transform root)
        {
            // ล่างสุด → บนสุด
            Front(root, "BG");
            Front(root, "Vignette");
            Front(root, "Sparkles");
            Front(root, "LogoGlow");
            Front(root, "Title");
            Front(root, "TaglinePill"); Front(root, "Sub");
            // แต่ละปุ่ม: ฐานหนาก่อน แล้วตัวปุ่ม (ฐานอยู่หลัง)
            FrontButton(root, "เล่นคนเดียวBtn"); FrontButton(root, "เล่นหลายคนBtn"); FrontButton(root, "เล่นต่อBtn");
            FrontButton(root, "ตั้งค่าBtn");      FrontButton(root, "ออกBtn");
            Front(root, "Footer");
            Front(root, "SettingsPanel");    // แผงตั้งค่าอยู่บนสุดเสมอ
            Front(root, "ComingSoonPanel");  // ป็อปอัปกำลังพัฒนาอยู่บนสุด
        }
        static void Front(Transform root, string name)
        {
            var t = root.Find(name);
            if (t != null) t.SetAsLastSibling();
        }
        static void FrontButton(Transform root, string name)
        {
            Front(root, name + "Base");   // ฐานหนาอยู่หลัง
            Front(root, name);            // ตัวปุ่มอยู่หน้า
        }

        // ---------------------------------------------------------------- parallax (มิติ)

        static void BuildParallax(Transform root, GameObject canvasGo)
        {
            var layers = new List<MenuParallax.Layer>();
            void Add(string name, float amount)
            {
                var rt = root.Find(name) as RectTransform;
                if (rt != null) layers.Add(new MenuParallax.Layer { target = rt, amount = amount });
            }

            // เลเยอร์หลัง = ขยับน้อย/สวนทาง, เลเยอร์หน้า = ขยับมาก
            Add("Vignette", -8f);
            // ปุ่ม + ฐานหนา ขยับเท่ากัน (10) จะได้ไม่หลุดจากกัน
            Add("เล่นคนเดียวBtnBase", 10f); Add("เล่นคนเดียวBtn", 10f);
            Add("เล่นหลายคนBtnBase", 10f); Add("เล่นหลายคนBtn", 10f);
            Add("เล่นต่อBtnBase", 10f);     Add("เล่นต่อBtn", 10f);
            Add("ตั้งค่าBtnBase", 10f);      Add("ตั้งค่าBtn", 10f);
            Add("ออกBtnBase", 10f);         Add("ออกBtn", 10f);
            Add("TaglinePill", 16f); Add("Sub", 16f);
            Add("Title", 20f);

            var mp = canvasGo.GetComponent<MenuParallax>();
            if (mp == null) mp = canvasGo.AddComponent<MenuParallax>();
            mp.layers = layers.ToArray();
            mp.smooth = 6f;
        }

        // ---------------------------------------------------------------- ประกายแสง (ambient)

        static void EnsureSparkles(Transform root)
        {
            if (glowSprite == null) return;
            var t = root.Find("Sparkles");
            GameObject go = t != null ? t.gameObject : new GameObject("Sparkles", typeof(RectTransform));
            if (t == null) go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var sp = go.GetComponent<MenuSparkles>() ?? go.AddComponent<MenuSparkles>();
            sp.sprite = glowSprite;
        }

        // ---------------------------------------------------------------- แอนิเมชันตอนเข้า

        static void BuildIntro(Transform root, GameObject canvasGo)
        {
            var items = new List<MenuIntro.Item>();
            void AddG(string name, float delay)
            {
                var t = root.Find(name);
                if (t == null) return;
                var cg = t.GetComponent<CanvasGroup>() ?? t.gameObject.AddComponent<CanvasGroup>();
                items.Add(new MenuIntro.Item { group = cg, delay = delay });
            }

            AddG("Title", 0f); AddG("LogoGlow", 0f);
            AddG("TaglinePill", 0.15f); AddG("Sub", 0.15f);
            AddG("เล่นคนเดียวBtn", 0.26f); AddG("เล่นคนเดียวBtnBase", 0.26f);
            AddG("เล่นหลายคนBtn", 0.31f); AddG("เล่นหลายคนBtnBase", 0.31f);
            AddG("เล่นต่อBtn", 0.36f);     AddG("เล่นต่อBtnBase", 0.36f);
            AddG("ตั้งค่าBtn", 0.42f);      AddG("ตั้งค่าBtnBase", 0.42f);
            AddG("ออกBtn", 0.48f);         AddG("ออกBtnBase", 0.48f);
            AddG("Footer", 0.55f);

            var intro = canvasGo.GetComponent<MenuIntro>() ?? canvasGo.AddComponent<MenuIntro>();
            intro.items = items.ToArray();
            intro.duration = 0.45f;
            intro.logo = root.Find("Title") as RectTransform;
            intro.logoFrom = 0.82f;
        }

        // ---------------------------------------------------------------- สร้าง sprite (PNG)

        static Sprite EnsureSprite(string file, System.Func<Texture2D> gen, int border)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder(ArtDir))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "UI");

            string assetPath = ArtDir + "/" + file;
            string abs = Application.dataPath + "/" + assetPath.Substring("Assets/".Length);
            if (!File.Exists(abs))
            {
                var tex = gen();
                File.WriteAllBytes(abs, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            AssetDatabase.Refresh();

            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                if (border > 0)
                {
                    var s = new TextureImporterSettings();
                    imp.ReadTextureSettings(s);
                    s.spriteBorder = new Vector4(border, border, border, border);
                    imp.SetTextureSettings(s);
                }
                imp.SaveAndReimport();
            }

            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sp == null) Debug.LogWarning("[Nisit] โหลด sprite ไม่ได้: " + assetPath);
            return sp;
        }

        // สี่เหลี่ยมมุมโค้งสีขาว (ขอบโปร่งใส, กันขอบหยัก)
        static Texture2D MakeRoundedTex()
        {
            const int N = 64; const float r = 24f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color[N * N];
            float half = N / 2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float px_ = x + 0.5f - half, py_ = y + 0.5f - half;
                    float qx = Mathf.Abs(px_) - (half - r);
                    float qy = Mathf.Abs(py_) - (half - r);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) +
                                               Mathf.Max(qy, 0) * Mathf.Max(qy, 0));
                    float d = outside - r;
                    float a = Mathf.Clamp01(0.5f - d);   // AA ~1px
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // vignette วงกลมดำโปร่ง เข้มที่ขอบ ใสตรงกลาง
        static Texture2D MakeVignetteTex()
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N - 0.5f;
                    float v = (y + 0.5f) / N - 0.5f;
                    float dist = Mathf.Sqrt(u * u + v * v) / 0.7071f;   // 0 กลาง, 1 มุม
                    float a = Mathf.SmoothStep(0.52f, 1.0f, dist) * 0.5f;
                    px[y * N + x] = new Color(0f, 0f, 0f, a);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // ปุ่มมุมโค้ง + ไล่สีแนวตั้ง (บนสว่าง-ล่างเข้ม) → tint ด้วยสีปุ่มแล้วได้ปุ่มมีมิติ
        static Texture2D MakeButtonTex()
        {
            const int N = 64; const float r = 24f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color[N * N];
            float half = N / 2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - r);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - r);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0));
                    float a = Mathf.Clamp01(0.5f - (outside - r));

                    float ty = y / (N - 1f);                        // 0 ล่าง, 1 บน
                    float lum = Mathf.Lerp(0.72f, 1.0f, ty);        // ไล่สีมีมิติ
                    lum += 0.06f * Mathf.SmoothStep(0.72f, 1f, ty); // ไฮไลต์ขอบบน
                    lum = Mathf.Clamp01(lum);
                    px[y * N + x] = new Color(lum, lum, lum, a);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // ปุ่มพิลล์ (มุมโค้งมาก) + ไล่สีมีมิติ + เงาในด้านล่าง → tint แล้วได้ปุ่มลูกกวาด
        static Texture2D MakePillTex()
        {
            const int N = 64; const float r = 30f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color[N * N];
            float half = N / 2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - r);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - r);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0));
                    float a = Mathf.Clamp01(0.5f - (outside - r));

                    float ty = y / (N - 1f);                         // 0 ล่าง, 1 บน
                    float lum = Mathf.Lerp(0.82f, 1.0f, ty);         // สว่างทั้งใบ (ล่างไม่เข้มมาก)
                    lum -= 0.06f * Mathf.SmoothStep(0.16f, 0f, ty);  // เงาในขอบล่างบาง ๆ (ให้ยังดูนูน)
                    lum = Mathf.Clamp01(lum);
                    px[y * N + x] = new Color(lum, lum, lum, a);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // แผ่นเงาวาวสีขาว: สว่างด้านบน จางลงล่าง จางที่ขอบซ้าย-ขวา (candy gloss)
        static Texture2D MakeGlossTex()
        {
            const int W = 128, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (W - 1f), v = y / (H - 1f);
                    float ax = Mathf.SmoothStep(0f, 0.18f, u) * Mathf.SmoothStep(1f, 0.82f, u);  // จางขอบซ้าย-ขวา
                    float ay = Mathf.Clamp01((v - 0.15f) / 0.85f); ay *= ay;                      // สว่างด้านบน
                    px[y * W + x] = new Color(1f, 1f, 1f, ax * ay * 0.9f);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // สี่เหลี่ยมมุมโค้งขอบฟุ้ง (สำหรับแสงเรืองรอบปุ่ม)
        static Texture2D MakeGlowRectTex()
        {
            const int N = 96; const float r = 30f; const float soft = 20f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color[N * N];
            float half = N / 2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - r);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - r);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0));
                    float d = outside - r;
                    float a = Mathf.Clamp01(0.5f - d / soft);
                    a = a * a;   // ฟุ้งนุ่มขึ้น
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // วงกลมสีขาวนุ่ม สว่างตรงกลาง จางที่ขอบ (ใช้ทำแสงเรือง)
        static Texture2D MakeSoftCircleTex()
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N - 0.5f;
                    float v = (y + 0.5f) / N - 0.5f;
                    float dist = Mathf.Sqrt(u * u + v * v) / 0.5f;   // 0 กลาง, 1 ขอบ
                    float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(dist));
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // แถบแสงขาวนุ่มแนวตั้ง (เอียงแล้วใช้วิ่งกวาดปุ่ม)
        static Texture2D MakeShineTex()
        {
            const int W = 48, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (W - 1f);
                    float a = Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.22f, 2f));   // สว่างกลาง จางข้าง
                    px[y * W + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // วงกลมทึบสีขาว (พื้นไอคอน)
        static Texture2D MakeCircleTex()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color[N * N];
            float half = N / 2f, r = half - 2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * N + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // ไอคอนปุ่ม (สีขาวบนพื้นโปร่ง): play / continue / settings / exit
        static Texture2D MakeIcon(string kind)
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N - 0.5f;
                    float v = (y + 0.5f) / N - 0.5f;
                    bool on = false;
                    switch (kind)
                    {
                        case "play":
                            on = InTri(u, v, -0.16f, -0.22f, -0.16f, 0.22f, 0.24f, 0f);
                            break;
                        case "continue":   // ▶▶ เดินหน้า/เล่นต่อ
                            on = InTri(u, v, -0.24f, -0.20f, -0.24f, 0.20f, -0.02f, 0f)
                              || InTri(u, v, 0.02f, -0.20f, 0.02f, 0.20f, 0.24f, 0f);
                            break;
                        case "settings":   // เฟือง
                        {
                            float d = Mathf.Sqrt(u * u + v * v);
                            float ang = Mathf.Atan2(v, u);
                            float rEff = 0.30f + 0.05f * (Mathf.Cos(ang * 8f) > 0.25f ? 1f : 0f);
                            on = d >= 0.15f && d <= rEff;
                            break;
                        }
                        case "exit":       // ปุ่มเปิด-ปิด
                        {
                            float d = Mathf.Sqrt(u * u + v * v);
                            float ang = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                            bool ring = d >= 0.20f && d <= 0.29f && !(ang > 62f && ang < 118f);
                            bool stem = Mathf.Abs(u) < 0.04f && v > 0f && v < 0.30f;
                            on = ring || stem;
                            break;
                        }
                    }
                    px[y * N + x] = new Color(1f, 1f, 1f, on ? 1f : 0f);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        static bool InTri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = TriSign(px, py, ax, ay, bx, by);
            float d2 = TriSign(px, py, bx, by, cx, cy);
            float d3 = TriSign(px, py, cx, cy, ax, ay);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }
        static float TriSign(float px, float py, float ax, float ay, float bx, float by)
        {
            return (px - bx) * (ay - by) - (ax - bx) * (py - by);
        }

        // ---------------------------------------------------------------- ภาพใหญ่ (bg)

        static Sprite GenerateBigImage(string file, int w, int h, System.Func<int, int, Texture2D> gen)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder(ArtDir))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "UI");

            string assetPath = ArtDir + "/" + file;
            string abs = Application.dataPath + "/" + assetPath.Substring("Assets/".Length);

            var tex = gen(w, h);
            File.WriteAllBytes(abs, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.Refresh();

            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sp == null) Debug.LogWarning("[Nisit] สร้างภาพพื้นหลังไม่สำเร็จ: " + assetPath);
            return sp;
        }

        static readonly float[] FLARE_T = { 0.35f, 0.60f, 1.15f, 1.50f };
        static readonly float[] FLARE_S = { 0.03f, 0.05f, 0.025f, 0.06f };
        static readonly Color[] FLARE_C = {
            new Color(1.0f, 0.70f, 0.40f), new Color(0.45f, 0.70f, 1.0f),
            new Color(1.0f, 0.90f, 0.60f), new Color(0.80f, 0.50f, 0.95f) };

        // วาดฉากพระอาทิตย์ตกอลังการทีละพิกเซล (สุ่มใหม่ทุกครั้งที่เรียก)
        static Texture2D MakeEpicBackgroundTex(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            float aspect = (float)w / h;

            // ---- เส้นขอบฟ้าเมือง (สุ่มตึก) ----
            float[] top = new float[w];
            int bx = 0;
            while (bx < w)
            {
                int bw = Random.Range(w / 60, w / 22);
                float tv = Random.value < 0.14f ? Random.Range(0.28f, 0.40f) : Random.Range(0.10f, 0.26f);
                for (int i = 0; i < bw && bx < w; i++, bx++) top[bx] = tv;
                bx += Random.Range(3, Mathf.Max(4, w / 110));
            }

            Vector2 sun = new Vector2(0.62f, 0.36f);
            Color sunWarm = new Color(1.0f, 0.86f, 0.55f);
            float noiseOff = Random.Range(0f, 100f);
            Vector2 fdir = new Vector2(0.5f, 0.5f) - sun;     // ทิศ lens flare

            // แลนด์มาร์ก (สุ่มตำแหน่งซ้าย/ขวา)
            float uTower = Random.Range(0.12f, 0.20f);
            float uDome  = Random.Range(0.80f, 0.88f);

            // ดาวตก (พิกัดปรับ aspect แล้ว)
            Vector2 ssDir = new Vector2(0.9f, -0.45f).normalized;
            Vector2 ssA = new Vector2(Random.Range(0.15f, 0.45f) * aspect, Random.Range(0.72f, 0.9f));
            float ssLen = 0.22f;

            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (w - 1f);
                    float su = u * aspect;
                    Color c = SkyColor(v);

                    // ---- ดวงอาทิตย์: รัศมี + ลำแสง + จาน ----
                    float dx = (u - sun.x) * aspect, dy = v - sun.y;
                    float dsun = Mathf.Sqrt(dx * dx + dy * dy);
                    float halo = Mathf.Exp(-dsun * 4.5f);
                    c += sunWarm * (halo * 1.05f);
                    float ang = Mathf.Atan2(dy, dx);
                    float ray = 0.5f + 0.5f * Mathf.Sin(ang * 18f + noiseOff);
                    c += sunWarm * (ray * Mathf.Clamp01(1f - dsun * 1.2f) * 0.12f);
                    float disc = Mathf.SmoothStep(0.06f, 0.05f, dsun);
                    c = Color.Lerp(c, new Color(1f, 0.98f, 0.88f), disc);

                    // ---- lens flare ----
                    for (int fi = 0; fi < FLARE_T.Length; fi++)
                    {
                        Vector2 fp = sun + fdir * FLARE_T[fi];
                        float fx = (u - fp.x) * aspect, fy = v - fp.y;
                        float fd = Mathf.Sqrt(fx * fx + fy * fy);
                        c += FLARE_C[fi] * (Mathf.Exp(-Mathf.Pow(fd / FLARE_S[fi], 2f)) * 0.22f);
                    }

                    // ---- เมฆ ----
                    if (v > 0.16f)
                    {
                        float n = Fbm(u * 3.2f + noiseOff, v * 3.2f + noiseOff * 0.5f);
                        float mask = Mathf.SmoothStep(0.55f, 0.85f, n);
                        Color cloud = Color.Lerp(new Color(0.28f, 0.22f, 0.36f),
                                                 new Color(1.0f, 0.82f, 0.56f),
                                                 Mathf.Clamp01(halo * 2.4f));
                        c = Color.Lerp(c, cloud, mask * 0.72f);
                    }

                    // ---- แสงเหนือ (aurora) บนฟ้าส่วนบน ----
                    if (v > 0.46f)
                    {
                        float fade = Mathf.SmoothStep(0.46f, 0.62f, v);
                        float c1 = 0.66f + 0.06f * Mathf.Sin(u * 7f + noiseOff) + 0.03f * Mathf.Sin(u * 17f + 1f);
                        float band1 = Mathf.Exp(-Mathf.Pow((v - c1) / 0.055f, 2f));
                        float c2 = 0.80f + 0.05f * Mathf.Sin(u * 5f + noiseOff * 1.3f + 2f);
                        float band2 = Mathf.Exp(-Mathf.Pow((v - c2) / 0.05f, 2f));
                        float stri = 0.55f + 0.45f * Mathf.Sin(u * 150f + Fbm(u * 5f, v * 5f) * 7f);
                        float curtain = 0.4f + 0.6f * Fbm(u * 3f + noiseOff, v * 2f);
                        Color aur = new Color(0.25f, 0.95f, 0.6f) * band1 + new Color(0.75f, 0.35f, 0.95f) * band2;
                        c += aur * (stri * curtain * fade * 0.32f);
                    }

                    // ---- ดาว (สี + กะพริบ) ----
                    if (v > 0.5f)
                    {
                        float hs = Hash21(x, y);
                        if (hs > 0.996f)
                        {
                            float b = (hs - 0.996f) / 0.004f;
                            float fade = Mathf.Clamp01((v - 0.5f) / 0.3f);
                            float tw = 0.6f + 0.4f * Mathf.Sin(noiseOff + x * 0.3f + y * 0.2f);
                            Color sc = Hash21(y, x) > 0.6f ? new Color(1f, 1f, 0.95f) : new Color(0.75f, 0.85f, 1f);
                            c += sc * (b * fade * tw);
                        }
                    }

                    // ---- ดาวตก ----
                    float proj = (su - ssA.x) * ssDir.x + (v - ssA.y) * ssDir.y;
                    if (proj > 0f && proj < ssLen)
                    {
                        float clx = ssA.x + ssDir.x * proj, cly = ssA.y + ssDir.y * proj;
                        float pd = Mathf.Sqrt((su - clx) * (su - clx) + (v - cly) * (v - cly));
                        float glow = Mathf.Exp(-Mathf.Pow(pd / 0.006f, 2f));
                        c += new Color(1f, 1f, 0.95f) * (glow * (proj / ssLen) * 0.9f);
                    }

                    // ---- หมอกขอบฟ้า ----
                    float haze = Mathf.Exp(-Mathf.Abs(v - 0.22f) * 9f);
                    c += new Color(1.0f, 0.70f, 0.42f) * (haze * 0.20f);

                    // ---- ภูเขาซ้อนชั้น ----
                    float hf = 0.205f + 0.03f * Mathf.Sin(u * 9.4f + 0.5f) + 0.015f * Mathf.Sin(u * 22f + 1.2f);
                    float hm = 0.150f + 0.035f * Mathf.Sin(u * 6.6f + 2.0f) + 0.02f * Mathf.Sin(u * 17f + 0.3f);
                    float hn = 0.100f + 0.040f * Mathf.Sin(u * 4.6f + 4.0f) + 0.02f * Mathf.Sin(u * 13f + 2.2f);
                    if (v < hf) c = new Color(0.30f, 0.24f, 0.42f);
                    if (v < hm) c = new Color(0.19f, 0.15f, 0.30f);
                    if (v < hn) c = new Color(0.10f, 0.08f, 0.18f);

                    // ---- ตึก + ไฟหน้าต่าง ----
                    float bt = top[x];
                    if (bt > 0f && v < bt)
                    {
                        c = new Color(0.05f, 0.05f, 0.10f);
                        if (v < bt - 0.008f && Hash21(x * 1.7f, y * 1.3f) > 0.988f)
                            c = new Color(1.0f, 0.80f, 0.42f);
                    }

                    // ---- หอนาฬิกา (ซ้าย) ----
                    float towerTop = 0.46f, spireH = 0.52f, tw2 = 0.016f;
                    if (Mathf.Abs(u - uTower) < tw2 && v < towerTop) c = new Color(0.04f, 0.04f, 0.09f);
                    float spireW = tw2 * Mathf.Clamp01((spireH - v) / (spireH - towerTop));
                    if (v >= towerTop && v < spireH && Mathf.Abs(u - uTower) < spireW) c = new Color(0.04f, 0.04f, 0.09f);
                    float clkx = (u - uTower) * aspect, clky = v - 0.40f;
                    float clk = Mathf.Sqrt(clkx * clkx + clky * clky);
                    if (clk < 0.011f) c = Color.Lerp(new Color(1f, 0.85f, 0.5f), c, Mathf.SmoothStep(0.006f, 0.011f, clk));

                    // ---- โดม (ขวา) ----
                    float dmx = (u - uDome) * aspect, dmy = v - 0.20f;
                    float dm = Mathf.Sqrt(dmx * dmx + dmy * dmy);
                    if (v < 0.20f && Mathf.Abs(u - uDome) < 0.03f) c = new Color(0.05f, 0.05f, 0.10f);
                    if (v >= 0.20f && dm < 0.03f) c = new Color(0.05f, 0.05f, 0.10f);
                    if (Mathf.Abs(u - uDome) < 0.004f && v > 0.20f && v < 0.255f) c = new Color(0.05f, 0.05f, 0.10f);

                    // ---- vignette ----
                    float cx = (u - 0.5f) * aspect, cy = v - 0.5f;
                    float vd = Mathf.Sqrt(cx * cx + cy * cy);
                    c *= 1f - 0.30f * Mathf.SmoothStep(0.45f, 1.15f, vd);

                    // ---- บูสต์สีให้จัดจ้าน ----
                    c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b);
                    float H, S, Vv; Color.RGBToHSV(c, out H, out S, out Vv);
                    c = Color.HSVToRGB(H, Mathf.Clamp01(S * 1.18f), Mathf.Clamp01(Vv * 1.03f));
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }

            // ---- หิ่งห้อยเรือง (post) ใกล้ขอบฟ้า ----
            int fireflies = 46;
            for (int i = 0; i < fireflies; i++)
            {
                float fu = Random.value, fv = Random.Range(0.12f, 0.42f);
                float rad = Random.Range(4f, 12f);
                Color col = new Color(1f, 0.85f, 0.45f) * Random.Range(0.4f, 0.9f);
                StampGlow(px, w, h, fu * w, fv * h, rad, col);
            }

            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // แต้มแสงเรืองแบบ additive (หิ่งห้อย)
        static void StampGlow(Color[] px, int w, int h, float cx, float cy, float rad, Color col)
        {
            int x0 = Mathf.Max(0, (int)(cx - rad * 2)), x1 = Mathf.Min(w - 1, (int)(cx + rad * 2));
            int y0 = Mathf.Max(0, (int)(cy - rad * 2)), y1 = Mathf.Min(h - 1, (int)(cy + rad * 2));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float ddx = x - cx, ddy = y - cy;
                    float a = Mathf.Exp(-(ddx * ddx + ddy * ddy) / (rad * rad));
                    var c = px[y * w + x];
                    c.r = Mathf.Clamp01(c.r + col.r * a);
                    c.g = Mathf.Clamp01(c.g + col.g * a);
                    c.b = Mathf.Clamp01(c.b + col.b * a);
                    px[y * w + x] = c;
                }
        }

        // ================================================================ ฉากมหาลัยกลางวันสดใส

        static Texture2D MakeDayCampusTex(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            float aspect = (float)w / h;
            const float groundY = 0.30f;

            // ---- ฟ้า + สนามหญ้า + ทางเดิน (per-pixel) ----
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (w - 1f);
                    Color c;
                    if (v >= groundY)
                    {
                        float t = Mathf.InverseLerp(groundY, 1f, v);
                        c = Color.Lerp(new Color(0.76f, 0.90f, 0.99f), new Color(0.26f, 0.56f, 0.93f), t);
                        float sdx = (u - 0.18f) * aspect, sdy = v - 0.92f;
                        float sd = Mathf.Sqrt(sdx * sdx + sdy * sdy);
                        c += new Color(1f, 0.97f, 0.82f) * (Mathf.Exp(-sd * 3.5f) * 0.5f);   // แสงอาทิตย์มุมซ้ายบน
                    }
                    else
                    {
                        float t = v / groundY;   // 0 ล่าง, 1 ขอบฟ้า
                        c = Color.Lerp(new Color(0.34f, 0.62f, 0.24f), new Color(0.56f, 0.80f, 0.42f), t);
                        float pw = 0.03f + 0.11f * t;   // ทางเดินสอบเข้าตามระยะ
                        if (Mathf.Abs(u - 0.5f) < pw)
                            c = Color.Lerp(new Color(0.86f, 0.80f, 0.66f), new Color(0.80f, 0.73f, 0.58f), t);
                    }
                    c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b); c.a = 1f;
                    px[y * w + x] = c;
                }
            }

            // ---- เมฆก้อนฟู (หลังตึก) ----
            int clouds = 6;
            for (int i = 0; i < clouds; i++)
            {
                float cu = Random.Range(0.08f, 0.92f), cv = Random.Range(0.64f, 0.90f);
                float sc = Random.Range(0.05f, 0.085f);
                int blobs = Random.Range(4, 7);
                for (int b = 0; b < blobs; b++)
                {
                    float ox = Random.Range(-1.4f, 1.4f) * sc, oy = Random.Range(-0.35f, 0.35f) * sc;
                    float r = sc * Random.Range(0.7f, 1.3f);
                    StampBlob(px, w, h, aspect, cu + ox, cv + oy, r * 1.35f, r,
                              new Color(0.85f, 0.90f, 0.97f), Color.white, 0.18f);
                }
            }

            // ---- ตึกโรงเรียน ----
            Color cream = new Color(0.95f, 0.93f, 0.87f);
            Color creamHi = new Color(0.98f, 0.96f, 0.92f);
            Color roof = new Color(0.83f, 0.81f, 0.76f);
            Color win = new Color(0.52f, 0.73f, 0.90f);
            StampRect(px, w, h, 0.30f, 0.70f, groundY, 0.66f, cream);          // ตัวตึกหลัก
            StampRect(px, w, h, 0.29f, 0.71f, 0.655f, 0.685f, roof);           // ขอบหลังคา
            StampRect(px, w, h, 0.44f, 0.56f, groundY, 0.74f, creamHi);        // มุขกลางสูง
            StampRect(px, w, h, 0.43f, 0.57f, 0.735f, 0.765f, roof);           // หลังคามุข
            // หน้าต่างปีกซ้าย-ขวา (เว้นมุขกลาง)
            for (int col = 0; col < 6; col++)
            {
                float wx = 0.315f + col * 0.062f;
                if (wx > 0.43f && wx < 0.56f) continue;   // ข้ามมุขกลาง
                for (int rowi = 0; rowi < 3; rowi++)
                {
                    float wy = groundY + 0.03f + rowi * 0.10f;
                    if (wy + 0.06f > 0.65f) continue;
                    StampRect(px, w, h, wx, wx + 0.036f, wy, wy + 0.06f, win);
                }
            }
            // หน้าต่างมุขกลาง (2 บาน บน)
            StampRect(px, w, h, 0.465f, 0.535f, 0.55f, 0.64f, win);
            // ประตู + บันได
            StampRect(px, w, h, 0.47f, 0.53f, groundY, groundY + 0.07f, new Color(0.42f, 0.52f, 0.62f));
            StampRect(px, w, h, 0.44f, 0.56f, groundY - 0.02f, groundY, new Color(0.88f, 0.86f, 0.82f));

            // ---- ต้นไม้กลมพุ่ม (กรอบซ้าย-ขวา, foreground) ----
            DrawTree(px, w, h, aspect, 0.05f, 0.14f, 0.62f, 0.20f);   // ซ้ายใหญ่
            DrawTree(px, w, h, aspect, 0.95f, 0.14f, 0.62f, 0.20f);   // ขวาใหญ่
            DrawTree(px, w, h, aspect, 0.24f, 0.19f, 0.40f, 0.11f);   // ซ้ายกลาง
            DrawTree(px, w, h, aspect, 0.76f, 0.19f, 0.40f, 0.11f);   // ขวากลาง

            // ---- พุ่มไม้เตี้ยหน้าตึก ----
            for (float bu = 0.31f; bu <= 0.69f; bu += 0.05f)
                StampBlob(px, w, h, aspect, bu + Random.Range(-0.01f, 0.01f), groundY + 0.005f,
                          0.028f, 0.022f, new Color(0.22f, 0.48f, 0.18f), new Color(0.42f, 0.72f, 0.30f), 0.25f);

            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // ต้นไม้ = ลำต้น + พุ่มใบหลายก้อน
        static void DrawTree(Color[] px, int w, int h, float aspect, float cu, float baseV, float folCenterV, float folR)
        {
            StampRect(px, w, h, cu - 0.008f, cu + 0.008f, baseV, folCenterV, new Color(0.52f, 0.36f, 0.22f));
            int blobs = 11;
            for (int i = 0; i < blobs; i++)
            {
                float a = i / (float)blobs * 6.2832f;
                float rr = folR * (i == 0 ? 0f : Random.Range(0.35f, 0.85f));
                float ox = Mathf.Cos(a) * rr, oy = Mathf.Sin(a) * rr * 0.9f;
                float r = folR * Random.Range(0.45f, 0.7f);
                // ก้อนบนสว่าง (โดนแสง) ก้อนล่างเข้ม
                Color bot = new Color(0.18f, 0.44f, 0.15f);
                Color topc = new Color(0.48f, 0.80f, 0.32f);
                StampBlob(px, w, h, aspect, cu + ox, folCenterV + oy, r * 1.05f, r, bot, topc, 0.16f);
            }
        }

        // แต้มสี่เหลี่ยม (พิกัด uv)
        static void StampRect(Color[] px, int w, int h, float u0, float u1, float v0, float v1, Color col)
        {
            int x0 = Mathf.Clamp(Mathf.RoundToInt(u0 * w), 0, w - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(u1 * w), 0, w - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(v0 * h), 0, h - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(v1 * h), 0, h - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    px[y * w + x] = col;
        }

        // แต้มวงรีขอบนุ่ม + ไล่สีตั้ง (สำหรับเมฆ/ใบไม้)
        static void StampBlob(Color[] px, int w, int h, float aspect,
                              float cu, float cv, float ru, float rv, Color colBottom, Color colTop, float edge)
        {
            float cx = cu * w, cy = cv * h, rxp = ru * w, ryp = rv * h;
            int x0 = Mathf.Max(0, (int)(cx - rxp - 1)), x1 = Mathf.Min(w - 1, (int)(cx + rxp + 1));
            int y0 = Mathf.Max(0, (int)(cy - ryp - 1)), y1 = Mathf.Min(h - 1, (int)(cy + ryp + 1));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float nx = (x - cx) / rxp, ny = (y - cy) / ryp;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.SmoothStep(1f, 1f - edge, d);
                    if (a <= 0f) continue;
                    float ty = Mathf.Clamp01((y - (cy - ryp)) / (2f * ryp));
                    Color col = Color.Lerp(colBottom, colTop, ty);
                    var bg = px[y * w + x];
                    px[y * w + x] = Color.Lerp(bg, col, a);
                }
        }

        // ไล่สีท้องฟ้า (t: 0 ขอบฟ้า → 1 บนสุด)
        static Color SkyColor(float t)
        {
            t = Mathf.Clamp01(t);
            Color cream  = new Color(1.00f, 0.90f, 0.66f);
            Color gold   = new Color(1.00f, 0.74f, 0.40f);
            Color orange = new Color(0.96f, 0.50f, 0.32f);
            Color mag    = new Color(0.55f, 0.28f, 0.48f);
            Color indigo = new Color(0.20f, 0.17f, 0.42f);
            Color night  = new Color(0.05f, 0.06f, 0.16f);
            if (t < 0.12f) return Color.Lerp(cream,  gold,   t / 0.12f);
            if (t < 0.26f) return Color.Lerp(gold,   orange, (t - 0.12f) / 0.14f);
            if (t < 0.45f) return Color.Lerp(orange, mag,    (t - 0.26f) / 0.19f);
            if (t < 0.68f) return Color.Lerp(mag,    indigo, (t - 0.45f) / 0.23f);
            return Color.Lerp(indigo, night, (t - 0.68f) / 0.32f);
        }

        // fractal Perlin noise (สำหรับเมฆ)
        static float Fbm(float x, float y)
        {
            float s = 0f, amp = 0.5f, f = 1f;
            for (int i = 0; i < 4; i++) { s += amp * Mathf.PerlinNoise(x * f, y * f); f *= 2.02f; amp *= 0.5f; }
            return s;
        }

        // สุ่มค่า 0..1 จากพิกัด (สำหรับดาว/ไฟหน้าต่าง)
        static float Hash21(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        // ---------------------------------------------------------------- helpers

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static Image NewImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        static Image NewRounded(Transform parent, string name)
        {
            var img = NewImage(parent, name);
            if (roundSprite != null) { img.sprite = roundSprite; img.type = Image.Type.Sliced; }
            return img;
        }

        static Image FindOrRounded(Transform root, string name)
        {
            var t = root.Find(name);
            return t != null ? t.GetComponent<Image>() : NewRounded(root, name);
        }

        static TMP_Text NewText(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            if (thai != null) t.font = thai;
            return t;
        }

        static Color Shift(Color c, float amt)
        {
            return new Color(Mathf.Clamp01(c.r + amt), Mathf.Clamp01(c.g + amt), Mathf.Clamp01(c.b + amt), c.a);
        }
    }
}
#endif
