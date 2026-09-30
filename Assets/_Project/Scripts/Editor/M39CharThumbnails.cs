#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using NisitSimulator.Systems;

namespace NisitSimulator.EditorTools
{
    // 📸 สร้างรูปตัวอย่าง (thumbnail) ของแต่ละแบบตัวละคร แล้วใส่ลง CharacterCatalog.icons
    //   → ช่อง "แบบตัวละคร" ในหน้าแต่งตัวจะโชว์รูปแทนกล่องเปล่า (พื้นโปร่ง)
    //   ใช้: Nisit ▸ Generate Character Thumbnails  (รันหลัง Setup Multiplayer ที่มีโมเดลแล้ว)
    public static class M39CharThumbnails
    {
        const string OutDir = "Assets/_Project/Art/CharThumbs";
        const int SIZE = 256;

        [MenuItem("Nisit/Generate Character Thumbnails", false, 37)]
        public static void Generate()
        {
            var cat = Resources.Load<CharacterCatalog>("CharacterCatalog");
            if (cat == null || cat.Count == 0)
            {
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "ยังไม่มี CharacterCatalog หรือยังไม่มีโมเดล\n\nกด Nisit ▸ Setup Multiplayer แล้วใส่โมเดลก่อน", "โอเค");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art")) AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/_Project/Art", "CharThumbs");

            var far = new Vector3(12000f, 12000f, 12000f);

            // ---- ตั้งเวทีชั่วคราว (กล้อง + ไฟ) ----
            var camGo = new GameObject("__ThumbCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // พื้นโปร่ง
            cam.fieldOfView = 26f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 300f;
            cam.forceIntoRenderTexture = true;

            var keyGo = new GameObject("__ThumbKey");
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.35f; key.color = new Color(1f, 0.98f, 0.94f);
            keyGo.transform.position = far;
            keyGo.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

            var fillGo = new GameObject("__ThumbFill");
            var fillL = fillGo.AddComponent<Light>();
            fillL.type = LightType.Directional; fillL.intensity = 0.75f; fillL.color = new Color(0.85f, 0.9f, 1f);
            fillGo.transform.position = far;
            fillGo.transform.rotation = Quaternion.Euler(18f, -40f, 0f);

            var rt = new RenderTexture(SIZE, SIZE, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            var paths = new string[cat.Count];

            for (int i = 0; i < cat.Count; i++)
            {
                var prefab = cat.Model(i);
                if (prefab == null) continue;

                var inst = Object.Instantiate(prefab);
                inst.transform.position = far;
                inst.transform.rotation = Quaternion.Euler(0f, 165f, 0f);   // เอียงนิดให้เห็นมิติ

                var rends = inst.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) { Object.DestroyImmediate(inst); continue; }
                Bounds b = rends[0].bounds;
                for (int r = 1; r < rends.Length; r++) b.Encapsulate(rends[r].bounds);

                Vector3 center = b.center;
                float h = Mathf.Max(b.size.y, 0.2f);
                float dist = (h * 0.60f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) + b.size.z * 0.5f;
                cam.transform.position = center + new Vector3(0f, h * 0.06f, dist);
                cam.transform.LookAt(center + new Vector3(0f, h * 0.02f, 0f));

                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, SIZE, SIZE), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                string path = OutDir + "/char_" + i + ".png";
                string abs = Application.dataPath + path.Substring("Assets".Length);
                File.WriteAllBytes(abs, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(inst);
                paths[i] = path;
            }

            cam.targetTexture = null;
            rt.Release(); Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(keyGo);
            Object.DestroyImmediate(fillGo);

            AssetDatabase.Refresh();

            var icons = new Sprite[cat.Count];
            for (int i = 0; i < cat.Count; i++)
            {
                if (paths[i] == null) continue;
                var imp = AssetImporter.GetAtPath(paths[i]) as TextureImporter;
                if (imp != null)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.alphaIsTransparency = true;
                    imp.mipmapEnabled = false;
                    imp.SaveAndReimport();
                }
                icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            }

            cat.icons = icons;
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=lime>[Nisit] สร้างรูปตัวอย่างตัวละคร " + cat.Count + " แบบ แล้ว</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                $"สร้างรูปตัวอย่าง {cat.Count} แบบแล้ว! 📸✅\n\nกด Play → หน้าแต่งตัว → ช่อง 'แบบตัวละคร' จะโชว์รูป\n(ถ้ารูปมุม/แสงไม่สวย แก้ไฟล์ char_i.png ใน Art/CharThumbs หรือกดคำสั่งนี้ซ้ำได้)", "เยี่ยม!");
        }
    }
}
#endif
