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
        const string ForestDir = "Assets/_Project/Art/Models/KayKit_Forest/";
        const string CityDir = "Assets/_Project/Art/Models/KayKit_City/";
        const float RoadScale = 1.6f;   // สเกลกระเบื้องถนน (ปรับได้ถ้าเป็นช่อง/ทับกัน)
        const float RoadStep = 6.4f;    // ระยะห่างสำรอง (ปกติวัดจากโมเดลจริง)

        // ===== ปรับขนาดองค์ประกอบฉาก: เพิ่มเลข = ใหญ่ขึ้น, ลด = เล็กลง =====
        const float TreeScale = 1.3f;       // 🌳 ต้นไม้
        const float BuildingScale = 1.3f;   // 🏫 ตึก
        const float RoadScaleMul = 1.3f;    // 🛣️ ถนน + เสาไฟ + รถ
        const float DecoScale = 1.3f;       // 🌿 พุ่มไม้ + ก้อนหิน

        static float s_roadHalf = 4f;       // ครึ่งความกว้างถนน (ตั้งจริงตอนวางถนน)

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
            ground.GetComponent<Renderer>().sharedMaterial = Mat("Grass", new Color(0.48f, 0.76f, 0.36f));

            // ---- ทางเดิน ----
            var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "Path"; path.transform.SetParent(root);
            path.transform.position = new Vector3(0, 0.02f, 0);
            path.transform.localScale = new Vector3(4, 0.05f, 40);
            path.GetComponent<Renderer>().sharedMaterial = Mat("Path", new Color(0.78f, 0.75f, 0.68f));
            Strip(path);

            // ---- อาคาร (KayKit_City) วางเป็นแคมปัส สเกลใหญ่กว่าต้นไม้ ----
            PlaceCity("building_F", new Vector3(0, 0, -16), 0, 2.4f, root);    // ตึกกลางใหญ่
            PlaceCity("building_C", new Vector3(-12, 0, -13), 20, 2.1f, root);
            PlaceCity("building_E", new Vector3(12, 0, -13), -20, 2.1f, root);
            PlaceCity("building_A", new Vector3(-23, 0, -16), 32, 1.9f, root);
            PlaceCity("building_G", new Vector3(23, 0, -16), -32, 1.9f, root);
            PlaceCity("building_B", new Vector3(-7, 0, -23), 8, 2.2f, root);
            PlaceCity("building_D", new Vector3(8, 0, -23), -8, 2.2f, root);
            PlaceCity("building_H", new Vector3(-17, 0, -24), 22, 2.0f, root);
            PlaceModel(CityDir + "watertower.fbx", new Vector3(18, 0, -25), 0, 2.2f * BuildingScale, root);   // แลนด์มาร์ก

            // ---- ถนน + เสาไฟ + รถ ----
            LayRoad(root);

            // ---- ต้นไม้ ----
            var trunk = Mat("TreeTrunk", new Color(0.50f, 0.34f, 0.21f));
            var leaf = Mat("TreeLeaf", new Color(0.32f, 0.70f, 0.32f));
            Vector3[] trees = {
                new(-6, 0, 2), new(6, 0, 1), new(-10, 0, -3), new(10, 0, -4),
                new(-4, 0, -6), new(5, 0, -7), new(-13, 0, 1), new(13, 0, 0),
                new(-8, 0, 5), new(8, 0, 4), new(0, 0, 6),
            };
            // โมเดลต้นไม้จริงจาก KayKit_Forest (โหลดไม่ได้ → ทรงกลม primitive แทน)
            string[] treeModels = {
                "Tree_1_A_Color1", "Tree_2_A_Color1", "Tree_2_C_Color1",
                "Tree_3_A_Color1", "Tree_4_A_Color1", "Tree_4_C_Color1",
            };
            foreach (var p in trees)
            {
                string modelPath = ForestDir + treeModels[Random.Range(0, treeModels.Length)] + ".fbx";
                var pos = AvoidRoad(p, s_roadHalf + 2.2f);   // กันไม่ให้ต้นไม้ทับถนน
                var inst = PlaceModel(modelPath, pos, Random.Range(0f, 360f), Random.Range(0.9f, 1.15f) * TreeScale, root);
                if (inst == null) Tree(pos, root, trunk, leaf);   // fallback
                else inst.name = "Tree";
            }

            // ประดับ: พุ่มไม้ + ก้อนหิน
            string[] deco = {
                "Bush_1_A_Color1", "Bush_2_A_Color1", "Bush_4_C_Color1",
                "Rock_1_A_Color1", "Rock_2_C_Color1", "Rock_3_A_Color1",
            };
            Vector3[] decoSpots = {
                new(-3, 0, 3), new(3, 0, 3), new(-7, 0, 0),
                new(7, 0, -1), new(-2, 0, 5), new(2.5f, 0, 4.5f),
            };
            foreach (var d in decoSpots)
                PlaceModel(ForestDir + deco[Random.Range(0, deco.Length)] + ".fbx",
                           AvoidRoad(d, s_roadHalf + 1.5f), Random.Range(0f, 360f), Random.Range(0.8f, 1.3f) * DecoScale, root);

            // ---- NPC เดินไปมา ----
            SetupNPCs(root);

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
            tr.transform.SetParent(t); tr.transform.localPosition = new Vector3(0, 1.1f, 0);
            tr.transform.localScale = new Vector3(0.38f, 1.1f, 0.38f);
            tr.GetComponent<Renderer>().sharedMaterial = trunk; Strip(tr);
            // พุ่มใบหลายก้อน → ทรงกลมฟูแบบการ์ตูน
            Foliage(t, new Vector3(0f, 2.7f, 0f), 2.5f, leaf);
            Foliage(t, new Vector3(1.0f, 2.3f, 0.3f), 1.8f, leaf);
            Foliage(t, new Vector3(-0.9f, 2.4f, -0.4f), 1.7f, leaf);
            Foliage(t, new Vector3(0.1f, 3.5f, 0.1f), 1.7f, leaf);
        }

        static void Foliage(Transform parent, Vector3 localPos, float scale, Material leaf)
        {
            var lv = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lv.transform.SetParent(parent); lv.transform.localPosition = localPos;
            lv.transform.localScale = Vector3.one * scale;
            lv.GetComponent<Renderer>().sharedMaterial = leaf; Strip(lv);
        }

        // ซ่อนภาพพื้นหลัง 2D เพื่อให้เห็นฉาก 3D ด้านหลัง (การ์ด/ปุ่มจัดการโดย Polish Menu Layout)
        static void DimMenuOverlay()
        {
            var canvas = GameObject.Find("Menu Canvas");
            if (canvas == null) return;
            var bg = canvas.transform.Find("BG");
            if (bg != null)
            {
                var img = bg.GetComponent<Image>();
                if (img != null) img.enabled = false;   // ปิดภาพ 2D → เห็นฉาก 3D
            }
        }

        // วางตึก KayKit_City ตามชื่อ + สเกล (โหลดตรงไม่ได้ → fallback ค้นหาแบบเดิม)
        static void PlaceCity(string name, Vector3 pos, float rotY, float scale, Transform parent)
        {
            var inst = PlaceModel(CityDir + name + ".fbx", pos, rotY, scale * BuildingScale, parent);
            if (inst == null) PlaceBuilding(name, pos, rotY, parent);
        }

        // ถนนกากบาท + ทางแยกกลาง + เสาไฟ + รถ (วัดขนาดกระเบื้องจริง → วางชิดไม่ขาด)
        static void LayRoad(Transform parent)
        {
            var straight = AssetDatabase.LoadAssetAtPath<GameObject>(CityDir + "road_straight.fbx");
            if (straight == null) return;

            var holder = new GameObject("Roads").transform;
            holder.SetParent(parent);
            float sc = RoadScale * RoadScaleMul, y = 0.06f;

            // วัดความยาวกระเบื้องจริง (หลังสเกล) จากขอบเขตโมเดล
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(straight);
            probe.transform.localScale = Vector3.one * sc;
            float step = MeasureTileLength(probe);
            Object.DestroyImmediate(probe);
            if (step <= 0.01f) step = RoadStep;   // กันพลาด
            s_roadHalf = step * 0.5f;             // จำความกว้างถนนไว้ให้ต้นไม้หลบ

            int arm = Mathf.Clamp(Mathf.RoundToInt(18f / step), 2, 8);   // ให้ถนนยาว ~36 หน่วยเสมอ
            for (int k = -arm; k <= arm; k++)
            {
                if (k == 0) continue;
                PlaceModel(CityDir + "road_straight.fbx", new Vector3(0, y, k * step), 0f, sc, holder);   // เหนือ-ใต้
                PlaceModel(CityDir + "road_straight.fbx", new Vector3(k * step, y, 0), 90f, sc, holder);  // ตะวันออก-ตก
            }
            PlaceModel(CityDir + "road_junction.fbx", new Vector3(0, y, 0), 0f, sc, holder);

            // เสาไฟสองข้างถนน (ตำแหน่งอิงขนาดกระเบื้อง)
            float side = step * 0.62f;
            for (int k = -2; k <= 2; k++)
            {
                if (k == 0) continue;
                PlaceModel(CityDir + "streetlight.fbx", new Vector3(side, 0, k * step), 0f, sc * 0.9f, holder);
                PlaceModel(CityDir + "streetlight.fbx", new Vector3(-side, 0, k * step), 180f, sc * 0.9f, holder);
            }

            // รถจอดบนถนน
            PlaceModel(CityDir + "car_sedan.fbx", new Vector3(step * 0.22f, 0, step * 0.7f), 0f, sc * 0.9f, holder);
            PlaceModel(CityDir + "car_taxi.fbx", new Vector3(-step * 0.22f, 0, -step * 0.9f), 180f, sc * 0.9f, holder);
        }

        // วัดความยาวด้านราบที่ยาวสุดของโมเดล (world units)
        static float MeasureTileLength(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return 0f;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return Mathf.Max(b.size.x, b.size.z);
        }

        // สร้าง NPC เดินวนตาม waypoints (ใช้โมเดลตัวละครเดียวกับผู้เล่น)
        static void SetupNPCs(Transform parent)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx);
            if (model == null) return;
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(CharCtrl);

            Vector3[] pts = { new(-5, 0, -2), new(5, 0, -2), new(5, 0, 7), new(-5, 0, 7) };
            var wps = MakeWaypoints(parent, pts);

            for (int i = 0; i < 3; i++)
            {
                int startIdx = i % wps.Length;
                var npc = (GameObject)PrefabUtility.InstantiatePrefab(model);
                npc.name = "MenuNPC"; npc.transform.SetParent(parent);
                npc.transform.position = wps[startIdx].position;
                var anim = npc.GetComponentInChildren<Animator>();
                if (anim != null && ctrl != null) anim.runtimeAnimatorController = ctrl;
                var walker = npc.AddComponent<MenuNPCWalker>();
                walker.waypoints = wps;
                walker.startIndex = (startIdx + 1) % wps.Length;
                walker.speed = Random.Range(1.3f, 2.0f);
            }
        }

        static Transform[] MakeWaypoints(Transform parent, Vector3[] pts)
        {
            var holder = new GameObject("NPCWaypoints").transform;
            holder.SetParent(parent);
            var arr = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var wp = new GameObject("WP" + i).transform;
                wp.SetParent(holder); wp.position = pts[i];
                arr[i] = wp;
            }
            return arr;
        }

        // ดันตำแหน่งให้พ้นแนวถนน (ถนนกากบาทตามแกน x=0 และ z=0)
        static Vector3 AvoidRoad(Vector3 p, float clear)
        {
            if (Mathf.Abs(p.x) < clear) p.x = (p.x >= 0f ? clear : -clear);   // พ้นถนนแนวเหนือ-ใต้
            if (Mathf.Abs(p.z) < clear) p.z = (p.z >= 0f ? clear : -clear);   // พ้นถนนแนวออก-ตก
            return p;
        }

        // วางโมเดล .fbx จาก path (คืน null ถ้าโหลดไม่ได้)
        static GameObject PlaceModel(string assetPath, Vector3 pos, float rotY, float scale, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (inst == null) return null;
            inst.transform.SetParent(parent);
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.Euler(0, rotY, 0);
            inst.transform.localScale = Vector3.one * scale;
            return inst;
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
