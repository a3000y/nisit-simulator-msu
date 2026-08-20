#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.EditorTools
{
    // 🎨 เปลี่ยนหน้าตากรอบ/ปุ่ม UI ทั้งเกม เป็นสไตล์ Kenney (โค้งมน มีมิติ) ในคลิกเดียว
    //   • สลับเฉพาะ Image ที่เป็นกรอบ/ปุ่ม (Type = Sliced) — ไม่ยุ่งกับบาร์สถานะ (Type = Filled)
    //   • ปุ่ม → แบบมันวาว (gloss) · กรอบ/แผง → แบบเรียบ (flat)
    //   • คงสีเดิม (tint) ไว้ทั้งหมด
    // ใช้: เมนู  Nisit -> Apply Kenney UI Skin
    public static class M15KenneySkin
    {
        const string MenuScene = "Assets/Scenes/Scene1.unity";
        const string GameScene = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string FlatPng  = "Assets/_Project/Art/UI/kenney_ui-pack/PNG/Grey/Default/button_rectangle_flat.png";
        const string GlossPng = "Assets/_Project/Art/UI/kenney_ui-pack/PNG/Grey/Default/button_rectangle_gloss.png";

        [MenuItem("Nisit/Apply Kenney UI Skin")]
        public static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var panel  = Prep(FlatPng, 24);
            var button = Prep(GlossPng, 24);
            if (panel == null || button == null)
            {
                EditorUtility.DisplayDialog("Nisit Simulator",
                    "ไม่พบ sprite ของ Kenney\nที่ " + FlatPng + "\n\nตรวจว่าวางโฟลเดอร์ kenney_ui-pack ไว้ที่ Assets/_Project/Art/UI/ แล้วหรือยัง", "ปิด");
                return;
            }

            var builtin = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            int a = Skin(MenuScene, panel, button, builtin);
            int b = Skin(GameScene, panel, button, builtin);

            Debug.Log($"<color=lime>[Nisit] สลับ skin Kenney แล้ว — เมนู {a} / เกม {b} ชิ้น</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                $"เปลี่ยนหน้าตา UI เป็นสไตล์ Kenney แล้ว! 🎨\n\n• หน้าเมนู: {a} ชิ้น\n• ในเกม: {b} ชิ้น\n\nกรอบ/ปุ่มโค้งมนมีมิติขึ้น (คงสีเดิม)\nบาร์สถานะไม่ถูกแตะ\n\n💡 ถ้ามุมโค้งดูเพี้ยน ปรับ Border ได้ที่ไฟล์ png ▸ Sprite Editor", "เยี่ยม!");
        }

        // ตั้งค่า import ให้เป็น Sprite + 9-slice (ยืดมุมไม่เพี้ยน)
        static Sprite Prep(string path, int border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var s = new TextureImporterSettings();
                importer.ReadTextureSettings(s);
                s.spriteBorder = new Vector4(border, border, border, border);
                s.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(s);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static int Skin(string scenePath, Sprite panel, Sprite button, Sprite builtin)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int n = 0;
            var imgs = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var img in imgs)
            {
                if (img.sprite == null) continue;

                // สลับเฉพาะที่ใช้ sprite ปุ่มมาตรฐาน (UISprite) เท่านั้น — ไม่แตะไอคอน/ภาพอื่น
                bool isBuiltin = img.sprite == builtin || img.sprite.name == "UISprite";
                if (!isBuiltin) continue;

                // บาร์สถานะเป็น Type = Filled → ข้าม (ไม่ทำพัง)
                if (img.type != Image.Type.Sliced) continue;

                bool isButton = img.GetComponent<Button>() != null;
                img.sprite = isButton ? button : panel;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f;
                EditorUtility.SetDirty(img);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return n;
        }
    }
}
#endif
