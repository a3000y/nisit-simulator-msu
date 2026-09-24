#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // 📱 โทรศัพท์นิสิต (TAB) — หน้าโฮม + 5 แอป · ดีไซน์หรู: ไล่เฉดสี + ขอบทอง + เงาลอย + วงไอคอนฝ้า
    // ใช้: เมนู  Nisit -> Build Phone (TAB)
    public static class M12PhoneBuilder
    {
        public static bool SuppressDialog = false;
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string FontSdf = "Assets/_Project/Art/Fonts/Mitr SDF.asset";
        const string IconDir = "Assets/_Project/Art/UI/kenney_game-icons/PNG/White/2x/";
        const string UIDir = "Assets/_Project/UI";

        static readonly Color Ink  = new Color(0.08f, 0.07f, 0.11f, 1f);
        static readonly Color Gold = new Color(0.86f, 0.72f, 0.38f, 1f);
        static TMP_FontAsset thai;
        static Sprite round, ui, circle, tile, gradV;

        [MenuItem("Nisit/Build Phone (TAB)")]
        public static void Build()
        {
            if (!SuppressDialog && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            thai = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdf);
            ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            round  = LoadOr(UIDir + "/round.png", ui);
            circle = LoadOr(UIDir + "/circle.png", round);
            tile   = GenTile("tile_grad", 178, 150, 30);   // การ์ดไล่เฉด (มุมมน)
            gradV  = GenGradV("grad_v", 16, 128);          // ไล่แสงแนวตั้ง

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.name == "Phone Canvas") Object.DestroyImmediate(c.gameObject);

            var canvasGo = new GameObject("Phone Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var pc = canvasGo.AddComponent<PhoneController>();

            var root = Img(canvasGo.transform, "PhoneRoot", ui, new Color(0f, 0f, 0f, 0.5f));
            root.rectTransform.anchorMin = Vector2.zero; root.rectTransform.anchorMax = Vector2.one;
            root.rectTransform.offsetMin = Vector2.zero; root.rectTransform.offsetMax = Vector2.zero;
            pc.panel = root.gameObject;

            // ===== ขอบทอง (แผ่นทองด้านหลังตัวเครื่อง เผยเป็นเส้นบาง) =====
            var bezel = Img(root.transform, "GoldBezel", round, Gold);
            var bzt = bezel.rectTransform;
            bzt.anchorMin = bzt.anchorMax = new Vector2(1f, 0.5f); bzt.pivot = new Vector2(1f, 0.5f);
            bzt.sizeDelta = new Vector2(534, 922); bzt.anchoredPosition = new Vector2(-40, 0);
            Deco(bezel, 26f, 0.55f);

            // ===== ตัวเครื่อง ดำเงา =====
            var body = Img(bezel.transform, "PhoneBody", round, new Color(0.07f, 0.08f, 0.12f, 1f));
            var brt = body.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(520, 908);
            Sheen(body.transform, 0.06f, false);   // สะท้อนเมทัลลิกจาง

            var speaker = Img(body.transform, "Speaker", round, new Color(0.26f, 0.28f, 0.34f, 1f));
            var sprt = speaker.rectTransform; sprt.anchorMin = sprt.anchorMax = new Vector2(0.5f, 1f); sprt.pivot = new Vector2(0.5f, 1f);
            sprt.sizeDelta = new Vector2(92, 10); sprt.anchoredPosition = new Vector2(0, -16);

            // ===== จอ (มน + Mask ให้ทุกอย่างโค้งตาม) =====
            var screen = Img(body.transform, "Screen", round, new Color(0.12f, 0.15f, 0.23f, 1f));
            screen.rectTransform.sizeDelta = new Vector2(466, 808);
            var mask = screen.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // wallpaper (ใส่รูปเองได้ที่ Art/UI/phone_wallpaper.png, ไม่มีก็ใช้ไล่เฉดที่วาดให้)
            var wallSprite = LoadWallpaper();
            var wall = Img(screen.transform, "Wallpaper", wallSprite, Color.white);
            wall.type = Image.Type.Simple; wall.raycastTarget = false; wall.preserveAspect = false;
            var wrt = wall.rectTransform; wrt.anchorMin = Vector2.zero; wrt.anchorMax = Vector2.one; wrt.offsetMin = Vector2.zero; wrt.offsetMax = Vector2.zero;

            Sheen(screen.transform, 0.05f, false);

            // ===== แถบหัว =====
            var bar = Img(screen.transform, "StatusBar", round, new Color(0.20f, 0.42f, 0.72f, 1f));
            var barT = bar.rectTransform; barT.anchorMin = barT.anchorMax = new Vector2(0.5f, 1f); barT.pivot = new Vector2(0.5f, 1f);
            barT.sizeDelta = new Vector2(466, 60); barT.anchoredPosition = Vector2.zero;
            Sheen(bar.transform, 0.22f, false);
            MakeText(bar.transform, "Title", "โทรศัพท์นิสิต", 26, new Vector2(0f, 0.5f), new Vector2(22, 0), new Vector2(240, 44), Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            pc.clockBar = MakeText(bar.transform, "Clock", "08:00 AM", 26, new Vector2(1f, 0.5f), new Vector2(-22, 0), new Vector2(200, 44), Gold, FontStyles.Bold, TextAlignmentOptions.Right);

            var homebar = Img(screen.transform, "HomeIndicator", round, new Color(1f, 1f, 1f, 0.4f));
            var hb = homebar.rectTransform; hb.anchorMin = hb.anchorMax = new Vector2(0.5f, 0f); hb.pivot = new Vector2(0.5f, 0f);
            hb.sizeDelta = new Vector2(130, 7); hb.anchoredPosition = new Vector2(0, 12);

            // ===== หน้าโฮม =====
            var home = Img(screen.transform, "HomeView", ui, new Color(0, 0, 0, 0));
            var hrt = home.rectTransform; hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one; hrt.offsetMin = new Vector2(0, 26); hrt.offsetMax = new Vector2(0, -60);
            home.raycastTarget = false;
            pc.homeView = home.gameObject;

            var appBtns = new Button[6];
            appBtns[0] = AppBtn(home.transform, "สถานะ",  "leaderboardsComplex", new Vector2(-104, 244), new Color(0.34f, 0.62f, 0.98f));
            appBtns[1] = AppBtn(home.transform, "ปฏิทิน", "information",         new Vector2(104, 244),  new Color(0.30f, 0.78f, 0.52f));
            appBtns[2] = AppBtn(home.transform, "ภารกิจ", "checkmark",           new Vector2(-104, 74),  new Color(1.00f, 0.68f, 0.26f));
            appBtns[3] = AppBtn(home.transform, "เกรด",   "trophy",              new Vector2(104, 74),   new Color(0.74f, 0.50f, 1.00f));
            appBtns[4] = AppBtn(home.transform, "แผนที่", "target",              new Vector2(-104, -96), new Color(0.22f, 0.80f, 0.82f));
            appBtns[5] = AppBtn(home.transform, "เพื่อน", "star",                new Vector2(104, -96),  new Color(1.00f, 0.55f, 0.68f));
            pc.appButtons = appBtns;

            MakeText(home.transform, "Hint", "กด TAB เพื่อปิด", 18, new Vector2(0.5f, 0f), new Vector2(0, -2), new Vector2(300, 30), new Color(1, 1, 1, 0.55f), FontStyles.Italic, TextAlignmentOptions.Center);

            // ===== หน้าแอป =====
            var app = Img(screen.transform, "AppView", ui, new Color(0, 0, 0, 0));
            var art = app.rectTransform; art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = new Vector2(0, 26); art.offsetMax = new Vector2(0, -60);
            app.raycastTarget = false;
            pc.appView = app.gameObject;

            var back = MakeButton(app.transform, "Back", new Vector2(0f, 1f), new Vector2(14, -8), new Vector2(60, 48), new Color(0.30f, 0.34f, 0.46f));
            Deco(back.GetComponent<Image>(), 4f, 0.3f);
            AddIcon(back.transform, Icon("left"), 30);
            pc.backButton = back;
            pc.appTitle = MakeText(app.transform, "AppTitle", "แอป", 30, new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(320, 44), Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

            var card = Img(app.transform, "Card", round, new Color(0.17f, 0.20f, 0.30f, 1f));
            var crt = card.rectTransform; crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(20, 24); crt.offsetMax = new Vector2(-20, -66);
            Deco(card, 5f, 0.3f);
            Sheen(card.transform, 0.06f, false);
            var strip = Img(card.transform, "TopStrip", round, Gold);
            var st = strip.rectTransform; st.anchorMin = new Vector2(0, 1); st.anchorMax = new Vector2(1, 1); st.pivot = new Vector2(0.5f, 1f);
            st.offsetMin = new Vector2(14, -6); st.offsetMax = new Vector2(-14, 0); st.sizeDelta = new Vector2(0, 6);

            pc.appBody = MakeText(card.transform, "AppBody", "...", 27, new Vector2(0.5f, 1f), new Vector2(0, -26), new Vector2(400, 596), Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            pc.appBody.lineSpacing = 12f;
            pc.appBody.richText = true;
            pc.appCard = card.gameObject;

            // ===== มุมมองแผนที่ใหญ่ (โชว์ภาพจากกล้องมินิแมป) =====
            var mapFrame = Img(app.transform, "MapView", round, new Color(0.09f, 0.11f, 0.16f, 1f));
            var mfrt = mapFrame.rectTransform; mfrt.anchorMin = mfrt.anchorMax = mfrt.pivot = new Vector2(0.5f, 0.5f);
            mfrt.anchoredPosition = new Vector2(0, -26); mfrt.sizeDelta = new Vector2(428, 428);
            Deco(mapFrame, 5f, 0.3f);

            var rawGo = new GameObject("MapImage", typeof(RawImage));
            rawGo.transform.SetParent(mapFrame.transform, false);
            var raw = rawGo.GetComponent<RawImage>();
            var rrt = raw.rectTransform; rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(404, 404);
            var mmRt = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/_Project/Art/UI/MinimapRT.renderTexture");
            if (mmRt != null) raw.texture = mmRt;

            // จุดผู้เล่น (อยู่กลางเสมอ)
            var dot = Img(mapFrame.transform, "PlayerDot", circle, new Color(1f, 0.32f, 0.32f, 1f));
            dot.rectTransform.sizeDelta = new Vector2(20, 20);

            pc.mapView = mapFrame.gameObject;
            pc.mapImage = raw;
            var mmCam = GameObject.Find("MinimapCamera");
            if (mmCam != null) pc.minimapCam = mmCam.GetComponent<Camera>();
            mapFrame.gameObject.SetActive(false);

            app.gameObject.SetActive(false);

            var anim = root.gameObject.AddComponent<UIPopupAnim>();
            anim.scaleTarget = bezel.rectTransform;
            root.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] โทรศัพท์ดีไซน์หรูเสร็จ!</color>");
            if (!SuppressDialog)
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "อัปเกรดโทรศัพท์แล้ว! 📱✨\n\n• 6 แอป: สถานะ/ปฏิทิน/ภารกิจ/เกรด/แผนที่/เพื่อน\n• สไลด์ลื่น ๆ ตอนสลับแอป\n\nกด Play → TAB ดูได้เลย", "เยี่ยม!");
        }

        // ---------- ปุ่มแอป: การ์ดไล่เฉด + วงไอคอนฝ้า + เงา ----------
        static Button AppBtn(Transform parent, string label, string iconName, Vector2 pos, Color col)
        {
            var size = new Vector2(178, 150);
            var go = new GameObject(label + "App", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = tile; img.type = Image.Type.Simple; img.color = col;   // sprite ไล่เฉด (ขนาดตรงพอดี)
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            Deco(img, 6f, 0.4f);

            // วงกลมฝ้าหลังไอคอน
            var halo = Img(go.transform, "Halo", circle, new Color(1f, 1f, 1f, 0.20f));
            halo.raycastTarget = false;
            var ht = halo.rectTransform; ht.sizeDelta = new Vector2(84, 84); ht.anchoredPosition = new Vector2(0, 24);

            AddIcon(go.transform, Icon(iconName), 54).rectTransform.anchoredPosition = new Vector2(0, 24);
            MakeText(go.transform, "Label", label, 24, new Vector2(0.5f, 0.5f), new Vector2(0, -48), new Vector2(size.x - 12, 34), Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            return go.GetComponent<Button>();
        }

        // แผ่นไล่แสงแนวตั้ง (บนสว่าง) วางทับให้ดูมันวาว
        static void Sheen(Transform parent, float alpha, bool dark)
        {
            var go = new GameObject("Sheen", typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = gradV; img.type = Image.Type.Simple;
            img.color = new Color(dark ? 0 : 1, dark ? 0 : 1, dark ? 0 : 1, alpha);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0, 0.4f); rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8, 0); rt.offsetMax = new Vector2(-8, -6);
        }

        static void Deco(Graphic g, float shadow, float shadowAlpha)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, shadowAlpha);
            s.effectDistance = new Vector2(shadow * 0.55f, -shadow);
        }

        static Image AddIcon(Transform parent, Sprite icon, float sz)
        {
            var go = new GameObject("Icon", typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = icon; img.color = Color.white; img.raycastTarget = false; img.preserveAspect = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(sz, sz);
            return img;
        }

        static Sprite Icon(string name)
        {
            string path = IconDir + name + ".png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.Sprite)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------- สร้างสไปรต์ไล่เฉด ----------
        static Sprite GenTile(string key, int w, int h, int r)
        {
            string path = UIDir + "/" + key + ".png";
            var ex = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (ex != null) return ex;
            if (!Directory.Exists(UIDir)) Directory.CreateDirectory(UIDir);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = RectRound(x + 0.5f, y + 0.5f, w, h, r);
                    float v = Mathf.Clamp01(0.60f + 0.42f * (y / (float)(h - 1)));  // ล่างเข้ม บนสว่าง
                    tex.SetPixel(x, y, new Color(v, v, v, a));
                }
            SaveSprite(tex, path, 0);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite GenGradV(string key, int w, int h)
        {
            string path = UIDir + "/" + key + ".png";
            var ex = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (ex != null) return ex;
            if (!Directory.Exists(UIDir)) Directory.CreateDirectory(UIDir);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, y / (float)(h - 1)));  // บน alpha1 ล่าง 0
            SaveSprite(tex, path, 0);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ใช้รูปเองถ้ามี (Art/UI/phone_wallpaper.png) ไม่งั้นวาดไล่เฉดให้
        static Sprite LoadWallpaper()
        {
            const string custom = "Assets/_Project/Art/UI/phone_wallpaper.png";
            if (File.Exists(custom))
            {
                var imp = AssetImporter.GetAtPath(custom) as TextureImporter;
                if (imp != null && imp.textureType != TextureImporterType.Sprite)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.SaveAndReimport();
                }
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(custom);
                if (s != null) return s;
            }
            return GenWallpaper("phone_wall", 256, 448);
        }

        // วาด wallpaper ไล่เฉดม่วง-คราม + แสงนวลกลางบน
        static Sprite GenWallpaper(string key, int w, int h)
        {
            string path = UIDir + "/" + key + ".png";
            var ex = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (ex != null) return ex;
            if (!Directory.Exists(UIDir)) Directory.CreateDirectory(UIDir);

            Color top = new Color(0.22f, 0.24f, 0.50f);
            Color bot = new Color(0.07f, 0.08f, 0.16f);
            Color glow = new Color(0.50f, 0.42f, 0.85f);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float t = y / (float)(h - 1);
                    Color c = Color.Lerp(bot, top, t);
                    float gx = x / (float)(w - 1) - 0.5f;
                    float gy = y / (float)(h - 1) - 0.72f;
                    float dist = Mathf.Sqrt(gx * gx * 1.2f + gy * gy);
                    float g = Mathf.Clamp01(1f - dist / 0.7f);
                    c += glow * (g * g * 0.40f);
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
                }
            SaveSprite(tex, path, 0);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void SaveSprite(Texture2D tex, string path, int border)
        {
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = border > 0 ? new Vector4(border, border, border, border) : Vector4.zero;
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }

        // รูปทรงสี่เหลี่ยมมุมมน (มี AA)
        static float RectRound(float px, float py, int w, int h, int r)
        {
            float cx = Mathf.Clamp(px, r, w - r);
            float cy = Mathf.Clamp(py, r, h - r);
            float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }

        static Sprite LoadOr(string path, Sprite fallback)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return s != null ? s : fallback;
        }

        // ---------- helpers ----------
        static Image Img(Transform parent, string name, Sprite sprite, Color col)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = (sprite != null && sprite.border.sqrMagnitude > 0.01f) ? Image.Type.Sliced : Image.Type.Simple;
            img.color = col;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(100, 100);
            return img;
        }

        static Button MakeButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = round; img.type = Image.Type.Sliced; img.color = col;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return go.GetComponent<Button>();
        }

        static TMP_Text MakeText(Transform parent, string name, string text, float size, Vector2 anchor,
            Vector2 pos, Vector2 sizeDelta, Color col, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = col; t.fontStyle = style;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            if (thai != null) t.font = thai;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return t;
        }
    }
}
#endif
