#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    // จัดฉากมหาลัย 3D เป็นพื้นหลังเมนู + กล้องหมุนช้าๆ + หรี่ overlay ให้อ่านง่าย
    // ใช้: เมนู  Nisit -> Menu Background (3D)   (ทำหลัง Build M4 Menu)
    public static class MenuSceneryBuilder
    {
        const string MenuPath = "Assets/Scenes/Scene1.unity";
        const string MatDir = "Assets/_Project/Art/Materials/";
        const string CharFbx = "Assets/_Project/Art/Characters/Ch29_nonPBR.fbx";
        const string CharCtrl = "Assets/_Project/Art/Characters/PlayerAnimator.controller";

        [MenuItem("Nisit/Menu Background (3D)")]
        public static void Build()
        {
            if (EditorSceneManager.GetActiveScene().path != MenuPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
            }

            DestroyIfExists("MenuScenery");
            var root = new GameObject("MenuScenery").transform;

            // ---- พื้นหญ้า ----
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground"; ground.transform.SetParent(root);
            ground.transform.localScale = new Vector3(6, 1, 6);
            ground.GetComponent<Renderer>().sharedMaterial = Mat("Grass", new Color(0.42f, 0.68f, 0.34f));

            // ---- ทางเดิน ----
            var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "Path"; path.transform.SetParent(root);
            path.transform.position = new Vector3(0, 0.02f, 0);
            path.transform.localScale = new Vector3(4, 0.05f, 40);
            path.GetComponent<Renderer>().sharedMaterial = Mat("Path", new Color(0.78f, 0.75f, 0.68f));
            Strip(path);

            // ---- อาคาร (จากโมเดลที่โหลดมา) ----
            PlaceBuilding("building_C", new Vector3(-9, 0, -8), 25, root);
            PlaceBuilding("building_E", new Vector3(9, 0, -10), -20, root);
            PlaceBuilding("building_F", new Vector3(0, 0, -18), 0, root);

            // ---- ต้นไม้ ----
            var trunk = Mat("TreeTrunk", new Color(0.45f, 0.31f, 0.19f));
            var leaf = Mat("TreeLeaf", new Color(0.27f, 0.62f, 0.30f));
            Vector3[] trees = { new(-6, 0, 2), new(6, 0, 1), new(-10, 0, -3), new(10, 0, -4), new(-4, 0, -6), new(5, 0, -7) };
            foreach (var p in trees) Tree(p, root, trunk, leaf);

            // ---- ตัวละครยืนไอเดิล ----
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx);
            if (model != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "MenuCharacter"; inst.transform.SetParent(root);
                inst.transform.position = new Vector3(0, 0, -3);
                inst.transform.rotation = Quaternion.Euler(0, 200, 0);
                var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(CharCtrl);
                var anim = inst.GetComponentInChildren<Animator>();
                if (anim != null && ctrl != null) anim.runtimeAnimatorController = ctrl; // จะเล่นท่ายืน (Speed=0)
            }

            // ---- แสง ----
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional)
                {
                    l.transform.rotation = Quaternion.Euler(45, -30, 0);
                    l.intensity = 1.25f; l.color = new Color(1f, 0.96f, 0.88f);
                    l.shadows = LightShadows.Soft;
                }

            // ---- กล้องหมุน ----
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                var pivotGo = GameObject.Find("MenuPivot") ?? new GameObject("MenuPivot");
                pivotGo.transform.position = new Vector3(0, 0, -6);
                var orbit = cam.GetComponent<MenuCameraOrbit>() ?? cam.gameObject.AddComponent<MenuCameraOrbit>();
                orbit.pivot = pivotGo.transform;
                // ตั้งท่าเริ่มต้นให้เห็นในโหมด edit ด้วย
                cam.transform.position = new Vector3(-6, 7, -22);
                cam.transform.LookAt(pivotGo.transform.position + Vector3.up * 2.5f);
            }

            // ---- หรี่ overlay เมนูให้เห็นฉากด้านหลัง ----
            DimMenuOverlay();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=lime>[Nisit] จัดฉากเมนู 3D เสร็จ!</color>");
            EditorUtility.DisplayDialog("Nisit Simulator",
                "จัดพื้นหลังเมนู 3D เสร็จแล้ว! 🏫\n\nกด Play — กล้องจะหมุนรอบมหาลัยช้าๆ มีตัวละครยืนอยู่\n\n(ถ้าอาคารดูใหญ่/เล็กไป บอกผมปรับสเกลได้)", "เยี่ยม!");
        }

        // วางอาคารจากโมเดล KayKit (หาไฟล์อัตโนมัติ)
        static void PlaceBuilding(string keyword, Vector3 pos, float rotY, Transform parent)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model " + keyword))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!p.Contains("KayKit") && !p.Contains("Buildings")) continue;
                if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower() != keyword.ToLower()) continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (asset == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                inst.transform.SetParent(parent);
                inst.transform.position = pos;
                inst.transform.rotation = Quaternion.Euler(0, rotY, 0);
                return;
            }
        }

        static void Tree(Vector3 pos, Transform parent, Material trunk, Material leaf)
        {
            var t = new GameObject("Tree").transform; t.SetParent(parent); t.position = pos;
            var tr = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tr.transform.SetParent(t); tr.transform.localPosition = new Vector3(0, 1, 0);
            tr.transform.localScale = new Vector3(0.35f, 1, 0.35f);
            tr.GetComponent<Renderer>().sharedMaterial = trunk; Strip(tr);
            var lv = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lv.transform.SetParent(t); lv.transform.localPosition = new Vector3(0, 2.6f, 0);
            lv.transform.localScale = Vector3.one * 2.4f;
            lv.GetComponent<Renderer>().sharedMaterial = leaf; Strip(lv);
        }

        // หรี่ BG เมนู + เพิ่มแผงหลังปุ่มให้อ่านชัด
        static void DimMenuOverlay()
        {
            var canvas = GameObject.Find("Menu Canvas");
            if (canvas == null) return;
            var bg = canvas.transform.Find("BG");
            if (bg != null)
            {
                var img = bg.GetComponent<Image>();
                if (img != null) img.color = new Color(0.08f, 0.10f, 0.16f, 0.45f); // โปร่งใสให้เห็นฉาก
            }
            // แผงหลังเนื้อหา (กันตัวหนังสือกลืนฉาก)
            if (canvas.transform.Find("ContentBackdrop") == null)
            {
                var ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                var go = new GameObject("ContentBackdrop", typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                go.transform.SetSiblingIndex(1); // หลัง BG แต่หน้าฉาก 3D, อยู่ใต้ title/ปุ่ม
                var im = go.GetComponent<Image>();
                im.sprite = ui; im.type = Image.Type.Sliced; im.color = new Color(0.10f, 0.12f, 0.20f, 0.55f);
                var rt = im.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(560, 820);
                rt.anchoredPosition = new Vector2(0, 20);
            }
        }

        // ---- utils ----
        static Material Mat(string name, Color c)
        {
            string path = MatDir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); return m;
        }
        static void Strip(GameObject go) { var c = go.GetComponent<Collider>(); if (c) Object.DestroyImmediate(c); }
        static void DestroyIfExists(string n) { var g = GameObject.Find(n); if (g) Object.DestroyImmediate(g); }
    }
}
#endif
