#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.EditorTools
{
    // ตัวช่วยโหลด/ใส่ "ภาพพื้นหลังพาสเทล" (menu_custom.*) ให้หน้าต่าง ๆ ใช้ร่วมกัน → ทั้งเกมเข้าชุด
    public static class PastelBgUtil
    {
        const string Dir = "Assets/_Project/Art/UI";

        // โหลด sprite ของภาพพื้นหลัง (ลอง .png/.jpg/.jpeg) + บังคับ import เป็น Sprite ถ้ายังไม่ใช่
        public static Sprite Load()
        {
            string[] names = { "menu_custom.png", "menu_custom.jpg", "menu_custom.jpeg", "menu_bg.png" };
            foreach (var n in names)
            {
                string path = Dir + "/" + n;
                string abs = Application.dataPath + "/" + path.Substring("Assets/".Length);
                if (!File.Exists(abs)) continue;

                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null && imp.textureType != TextureImporterType.Sprite)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.mipmapEnabled = false;
                    imp.maxTextureSize = 2048;
                    imp.SaveAndReimport();
                }
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
            }
            Debug.LogWarning("[Nisit] ไม่พบภาพพื้นหลัง menu_custom.* ใน " + Dir);
            return null;
        }

        // ใส่ภาพพื้นหลังเต็มจอเป็น "ลูกตัวแรก" ของ parent (อยู่หลังสุด) — คืน Image (null ถ้าไม่มีภาพ)
        public static Image AddFullscreen(Transform parent, string name = "PastelBG")
        {
            var sprite = Load();
            if (sprite == null) return null;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.type = Image.Type.Simple; img.preserveAspect = false;
            img.color = Color.white; img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.transform.SetAsFirstSibling();
            return img;
        }
    }
}
#endif
