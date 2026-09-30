#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NisitSimulator.EditorTools
{
    // 🏫 แผนที่มหาวิทยาลัยใหม่ (Synty POLYGON City) — แทนที่ Campus + MapBorder เดิม
    // ระบบเดิม (ประตู/สถานี/NPC/ป้ายคณะ/ที่นั่ง) ถูกย้ายมาวางตามตึกใหม่อัตโนมัติ
    // ใช้: เมนู  Nisit -> Build Synty Campus (New Map)   (ฉากเดิมสำรองไว้ที่ 01_Gameplay_Old.unity)
    // ออกแบบเป็นหน่วย Synty (1 = 1 ม.) แล้วย่อทั้งแมพด้วย Scale ให้เข้ากับตัวละครสูง ~1.3
    public static class M42SyntyCampus
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string Root = "Assets/Synty/PolygonCity/Prefabs/";
        public const float Scale = 0.72f;
        // ถ้าหน้าตึกหันผิดด้าน ปรับค่านี้เป็น 180 แล้วสร้างใหม่
        public static float FrontYaw = 180f;   // Synty: หน้าตึกหัน +Z โดยกำเนิด

        static Transform root, gBuild, gPath, gProps, gTrees, gFence;

        // ---------- จุดเข้าตึก (ชื่อประตูต้องตรงกับ BuildingDoors เดิม) ----------
        struct Bld { public string key; public Vector2 center; public float yaw; public float halfDepth; public float top; }
        static readonly Dictionary<string, Bld> Buildings = new Dictionary<string, Bld>();

        [MenuItem("Nisit/Build Synty Campus (New Map)")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
            Build();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static string Build()
        {
            var log = new System.Text.StringBuilder();
            Buildings.Clear();

            // ลบแมพเก่า
            foreach (var n in new[] { "Campus", "MapBorder", "NewCampus" })
            {
                var old = GameObject.Find(n);
                if (old != null) Object.DestroyImmediate(old);
            }

            root = new GameObject("NewCampus").transform;
            root.localScale = Vector3.one * Scale;
            gBuild = Group("Buildings"); gPath = Group("Paths"); gProps = Group("Props");
            gTrees = Group("Trees"); gFence = Group("Boundary");

            Ground();
            PathsAndPlaza();
            BuildingsAll();
            PropsAll();
            TreesAll();
            BoundaryAll();

            log.AppendLine(RelinkSystems());
            Debug.Log("<color=lime>[Nisit] สร้างแผนที่ Synty ใหม่แล้ว</color>\n" + log);
            return log.ToString();
        }

        static Transform Group(string n) { var t = new GameObject(n).transform; t.SetParent(root, false); return t; }

        // ---------- helpers ----------
        static GameObject Load(string name)
        {
            foreach (var cat in new[] { "Buildings/", "Environments/", "Props/", "Vehicles/", "Environments/Custom/", "Buildings/Custom/" })
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(Root + cat + "SM_" + name + ".prefab");
                if (go != null) return go;
            }
            Debug.LogWarning("[Nisit] ไม่พบ Synty prefab: " + name);
            return null;
        }

        static Bounds LocalBounds(GameObject prefab)
        {
            var rs = prefab.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return b; // prefab asset อยู่ที่ origin → bounds = local
        }

        static GameObject Put(string name, Transform parent, Vector3 localPos, float yaw)
        {
            var p = Load(name); if (p == null) return null;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(p);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            g.isStatic = true;
            return g;
        }

        // วางชิ้นโดยให้ "กึ่งกลางฐาน" อยู่ที่ center (ชดเชย pivot มุมของ Synty)
        static GameObject PutCentered(string name, Transform parent, Vector2 center, float y, float yaw, Vector3? pivotOffset = null)
        {
            var p = Load(name); if (p == null) return null;
            var b = LocalBounds(p);
            Vector3 off = pivotOffset ?? new Vector3(b.center.x, 0, b.center.z);
            Vector3 rotOff = Quaternion.Euler(0, yaw, 0) * off;
            return Put(name, parent, new Vector3(center.x, y, center.y) - rotOff, yaw);
        }

        static Vector3 FootOffset(string name) { var b = LocalBounds(Load(name)); return new Vector3(b.center.x, 0, b.center.z); }
        static float Height(string name) { return LocalBounds(Load(name)).max.y; }

        // ตึกแบบต่อชั้น: base + floors + roof (ทุกชิ้นใช้ pivot เดียวกัน)
        static float Stack(Transform parent, Vector2 c, float yaw, string baseName, string floorName, int floors, string roofName)
        {
            var off = FootOffset(baseName);
            float y = 0;
            PutCentered(baseName, parent, c, y, yaw, off); y += Height(baseName);
            for (int i = 0; i < floors; i++) { PutCentered(floorName, parent, c, y, yaw, off); y += Height(floorName); }
            if (!string.IsNullOrEmpty(roofName)) { PutCentered(roofName, parent, c, y, yaw, off); y += Height(roofName); }
            return y;
        }

        static void Register(string key, Vector2 center, float faceYaw, float halfDepth, float top)
        {
            Buildings[key] = new Bld { key = key, center = center, yaw = faceYaw, halfDepth = halfDepth, top = top };
        }

        // ทิศหน้าตึก: yaw 0 = หันไป -Z (ทิศใต้), 90 = -X (ตะวันตก), -90 = +X, 180 = +Z
        static Vector3 FaceDir(float yaw) { return Quaternion.Euler(0, yaw, 0) * Vector3.back; }

        static void Pave(string tile, float x0, float x1, float z0, float z1, float y = 0f)
        {
            for (float x = x0; x < x1 - 0.01f; x += 5)
                for (float z = z0; z < z1 - 0.01f; z += 5)
                    Put(tile, gPath, new Vector3(x + 5, y + 0.03f, z + 5), 0);
        }

        // ---------- พื้น ----------
        static void Ground()
        {
            var g = GameObject.Find("Ground");
            if (g == null) { g = GameObject.CreatePrimitive(PrimitiveType.Plane); g.name = "Ground"; }
            g.transform.position = Vector3.zero;
            g.transform.localScale = new Vector3(30, 1, 30);   // 300 x 300 ม.
            const string matPath = "Assets/_Project/Materials/Campus_Grass.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials")) AssetDatabase.CreateFolder("Assets/_Project", "Materials");
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", new Color(0.42f, 0.60f, 0.33f));
                mat.SetFloat("_Smoothness", 0.05f);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            g.GetComponent<Renderer>().sharedMaterial = mat;
            g.isStatic = true;
        }

        // ---------- ทางเดิน + ลาน ----------
        static void PathsAndPlaza()
        {
            const string S = "Env_Sidewalk_01";
            Pave(S, -15, 15, -15, 15);        // ลานกลาง
            Pave(S, -5, 5, 15, 25);           // ขึ้นเหนือ → อาคารเรียน
            Pave(S, -40, 40, 20, 25);         // ทางเดินแถวเหนือ (คณะ IT – ห้องสอบ)
            Pave(S, 15, 25, -5, 5);           // ไปตะวันออก → ห้องสมุด
            Pave(S, -25, -15, -5, 5);         // ไปตะวันตก → อาคารบริหาร
            Pave(S, -25, -20, -25, -5);       // เลียบอาคารชมรม
            Pave(S, -25, -20, 5, 20);
            Pave(S, 25, 30, -35, 20);         // เลียบฝั่งตะวันออก (โรงอาหาร)
            Pave(S, -5, 5, -50, -15);         // ลงใต้ → ประตูมหาลัย
            Pave(S, -40, 30, -30, -25);       // ทางแถวใต้ (หอพัก – ร้านค้า)
            // ถนนนอกรั้วด้านใต้ + ลานจอดรถ
            for (float x = -65; x < 65; x += 5) { Put("Env_Road_01", gPath, new Vector3(x + 5, -0.01f, -55 + 5), 0); Put("Env_Road_01", gPath, new Vector3(x, -0.01f, -60), 180); }
            Pave(S, -65, 65, -50, -48 + 3);
        }

        // ---------- ตึก ----------
        static void BuildingsAll()
        {
            float f = FrontYaw;
            // อาคารเรียน (ศาลาหลังใหญ่ ปลายแกนเหนือ)
            var c = new Vector2(0, 34.5f);
            PutCentered("Bld_CityHall_01", gBuild, c, 0, 0 + f).name = "อาคารเรียน";
            Register("อาคารเรียน", c, 0, 8.9f, 8.8f);

            // ห้องสอบ (ExamPoint) — ตึกเก่า 2 ชั้น
            c = new Vector2(27, 31);
            var t = new GameObject("ห้องสอบ").transform; t.SetParent(gBuild, false);
            float top = Stack(t, c, 0 + f, "Bld_OfficeOld_Small_Base_01", "Bld_OfficeOld_Small_Floor_01", 1, "Bld_OfficeOld_Small_Roof_01");
            Register("ห้องสอบ", c, 0, 5.6f, top);

            // คณะ IT — ตึกกลม
            c = new Vector2(-28, 37.5f);
            t = new GameObject("คณะ IT").transform; t.SetParent(gBuild, false);
            top = Stack(t, c, 0 + f, "Bld_OfficeRound_Base_01", null, 0, "Bld_OfficeRound_Roof_01");
            Register("คณะ IT", c, 0, 11.6f, top);

            // ห้องสมุด — ตึกแปดเหลี่ยม 2 ชั้น (หันไปทางตะวันตก เข้าหาลาน)
            c = new Vector2(35, 0);
            t = new GameObject("ห้องสมุด").transform; t.SetParent(gBuild, false);
            top = Stack(t, c, 90 + f, "Bld_OfficeOctagon_Base_01", "Bld_OfficeOctagon_Floor_01", 1, "Bld_OfficeOctagon_Roof_01");
            Register("ห้องสมุด", c, 90, 10.4f, top);

            // อาคารบริหาร — ตึกเก่าใหญ่ (หันไปทางตะวันออก)
            c = new Vector2(-34, 6);
            t = new GameObject("อาคารบริหาร").transform; t.SetParent(gBuild, false);
            top = Stack(t, c, -90 + f, "Bld_OfficeOld_Large_Base_01", null, 0, "Bld_OfficeOld_Large_Roof_01");
            Register("อาคารบริหาร", c, -90, 8f, top);

            // อาคารชมรม
            c = new Vector2(-31, -14);
            PutCentered("Bld_Station_01", gBuild, c, 0, -90 + f).name = "อาคารชมรม";
            Register("อาคารชมรม", c, -90, 5f, 8.6f);

            // โรงอาหาร — ร้านค้าเรียงเป็นศูนย์อาหาร (หันไปทางตะวันตก)
            t = new GameObject("โรงอาหาร").transform; t.SetParent(gBuild, false);
            string[] shops = { "Bld_Shop_01", "Bld_Shop_02", "Bld_Shop_03", "Bld_Shop_04" };
            for (int i = 0; i < shops.Length; i++)
                PutCentered(shops[i], t, new Vector2(37.5f, -13 - i * 5), 0, 90 + f);
            Register("โรงอาหาร", new Vector2(37.5f, -20.5f), 90, 2.8f, 3.3f);
            Put("Prop_LargeSign_Noodles_01", t, new Vector3(38.5f, 3.3f, -20.5f), 90 + f);

            // ร้านค้า (สะดวกซื้อ) — ฝั่งใต้ หันขึ้นเหนือ
            t = new GameObject("ร้านค้า").transform; t.SetParent(gBuild, false);
            string[] mart = { "Bld_Shop_05", "Bld_Shop_06", "Bld_Shop_02" };
            for (int i = 0; i < mart.Length; i++)
                PutCentered(mart[i], t, new Vector2(12 + i * 5, -36.5f), 0, 180 + f);
            Register("ร้านค้า", new Vector2(17, -36.5f), 180, 2.8f, 3.3f);
            Put("Prop_LargeSign_Soda_01", t, new Vector3(17, 3.3f, -37.5f), 180 + f);

            // หอพัก — อพาร์ตเมนต์ 3 ชั้น 4 ห้องเรียง (หันขึ้นเหนือ)
            t = new GameObject("หอพัก").transform; t.SetParent(gBuild, false);
            for (int i = 0; i < 4; i++)
            {
                var cc = new Vector2(-38 + i * 5, -36.5f);
                var off = FootOffset("Bld_Apartment_Door_01");
                PutCentered("Bld_Apartment_Door_01", t, cc, 0, 180 + f, off);
                PutCentered("Bld_Apartment_01", t, cc, 3, 180 + f, off);
                PutCentered("Bld_Apartment_01", t, cc, 6, 180 + f, off);
                PutCentered("Bld_Apartment_Roof_01", t, cc, 9, 180 + f, off);
            }
            Register("หอพัก", new Vector2(-30.5f, -36.5f), 180, 2.8f, 9.5f);

            // ตึกสูงนอกรั้ว (วิวเมืองด้านหลัง)
            var sky = new GameObject("CitySkyline").transform; sky.SetParent(gBuild, false);
            string[] tall = { "Bld_OfficeSquare_01", "Bld_OfficeOld_Large_01", "Bld_OfficeSquare_04", "Bld_OfficeSquare_03", "Bld_OfficeOld_Small_02", "Bld_OfficeSquare_02" };
            for (int i = 0; i < tall.Length; i++) PutCentered(tall[i], sky, new Vector2(-55 + i * 22, 72 + (i % 2) * 8), 0, FrontYaw);
            for (int i = 0; i < 4; i++) PutCentered(tall[(i + 2) % tall.Length], sky, new Vector2(72 + (i % 2) * 6, 45 - i * 22), 0, 90 + FrontYaw);
        }

        // ---------- ของตกแต่ง ----------
        public static readonly List<Vector3> BenchSpots = new List<Vector3>(); // x,z,yaw (local)

        static void PropsAll()
        {
            BenchSpots.Clear();
            // ม้านั่งรอบลาน หันเข้าหากลางลาน
            Vector2[] bp = { new Vector2(-9, 13), new Vector2(9, 13), new Vector2(-9, -13), new Vector2(9, -13), new Vector2(-13, 9), new Vector2(-13, -9), new Vector2(13, 9), new Vector2(13, -9) };
            foreach (var p in bp)
            {
                float yaw = Mathf.Atan2(-p.x, -p.y) * Mathf.Rad2Deg;   // หันหน้าเข้ากลาง
                yaw = Mathf.Round(yaw / 90f) * 90f;
                Put("Prop_ParkBench_01", gProps, new Vector3(p.x, 0, p.y), yaw + 180);
                BenchSpots.Add(new Vector3(p.x, p.y, yaw));
            }
            // กระถางต้นไม้กลางลาน + มุมลาน
            Put("Prop_Planter_02", gProps, new Vector3(0, 0, 0), 0);
            foreach (var p in new[] { new Vector2(-13, 13), new Vector2(13, 13), new Vector2(-13, -13), new Vector2(13, -13) })
                Put("Prop_Planter_01", gProps, new Vector3(p.x, 0, p.y), 0);
            // ถังขยะ
            foreach (var p in new[] { new Vector2(-6, 14), new Vector2(6, -14), new Vector2(14, 6), new Vector2(-14, -6) })
                Put("Prop_Trashbin_01", gProps, new Vector3(p.x, 0, p.y), 0);
            // โรงอาหาร: โต๊ะปิกนิก + ร่ม
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                {
                    var pos = new Vector3(20 + j * 4.5f, 0, -14 - i * 6);
                    Put("Prop_PicnicTable_01", gProps, pos, 90);
                    Put("Prop_Umbrella_01", gProps, pos, 0);
                }
            Put("Prop_HotdogStand_01", gProps, new Vector3(31.5f, 0, -8), 90);
            // ร้านค้า: ตู้กดน้ำ ATM
            Put("Prop_ATM_01", gProps, new Vector3(8.5f, 0, -33.5f), 180);
            Put("Prop_Soda_01", gProps, new Vector3(25.5f, 0, -33.5f), 180);
            // ประตูมหาวิทยาลัย + ป้ายรถเมล์ + รถ
            Put("Prop_Sign_Entrance_01", gProps, new Vector3(-6.5f, 0, -49), 0);
            Put("Prop_BusStop_01", gProps, new Vector3(-20, 0, -48), 180);
            Put("Veh_Car_Sedan_01", gProps, new Vector3(-35, 0, -52.5f), 90);
            Put("Veh_Car_Taxi_01", gProps, new Vector3(28, 0, -57.5f), -90);
            Put("Veh_Car_Small_01", gProps, new Vector3(45, 0, -52.5f), 90);
            // ไฟทาง
            foreach (var z in new[] { -45f, -35f, 18f })
            {
                Put("Prop_LightPole_Base_01", gProps, new Vector3(-6.2f, 0, z), 90);
                Put("Prop_LightPole_Base_01", gProps, new Vector3(6.2f, 0, z), -90);
            }
            // ซักผ้าที่หอ
            Put("Prop_Washingline_01", gProps, new Vector3(-30, 0, -44), 0);
        }

        static void TreesAll()
        {
            string[] tr = { "Env_Tree_01", "Env_Tree_02", "Env_Tree_03" };
            Vector2[] spots =
            {
                // สนามหญ้ารอบลาน
                new Vector2(-19,12), new Vector2(-12,18), new Vector2(12,18), new Vector2(19,12),
                new Vector2(-19,-12), new Vector2(-11,-19), new Vector2(11,-19), new Vector2(19,-11),
                new Vector2(-9,-24), new Vector2(9,-22), new Vector2(-9,-40), new Vector2(9,-43),
                // ริมทางเหนือ
                new Vector2(-12,27.5f), new Vector2(12,27.5f), new Vector2(40,27), new Vector2(-43,27),
                // ข้างตึก
                new Vector2(-42,-24), new Vector2(-16,-44), new Vector2(31,-40), new Vector2(43,-40),
                new Vector2(43,14), new Vector2(20,14), new Vector2(-16,-20), new Vector2(-43,20), new Vector2(-17,17),
                new Vector2(18,-45), new Vector2(-40,-46), new Vector2(44,33), new Vector2(-12,40), new Vector2(13,42),
            };
            for (int i = 0; i < spots.Length; i++)
                { var tg = Put(tr[i % tr.Length], gTrees, new Vector3(spots[i].x, 0, spots[i].y), (i * 67) % 360); if (tg) tg.transform.localScale = Vector3.one * (1.5f + (i % 3) * 0.2f); }
            // พุ่มดอกไม้ริมลาน
            for (int i = -2; i <= 2; i++)
            {
                Put("Env_Flower_01", gTrees, new Vector3(i * 2.5f, 0, 16.5f), 0);
                Put("Env_Flower_01", gTrees, new Vector3(16.5f, 0, i * 2.5f), 90);
            }
        }

        // ---------- รั้ว + กำแพงล่องหน ----------
        static void BoundaryAll()
        {
            const float E = 50f;
            for (float x = -E; x < E; x += 5)
            {
                Put("Env_Fence_01", gFence, new Vector3(x, 0, E), 0);                     // เหนือ
                if (x < -5 || x >= 5) Put("Env_Fence_01", gFence, new Vector3(x, 0, -E), 0); // ใต้ (เว้นประตู)
            }
            for (float z = -E; z < E; z += 5)
            {
                Put("Env_Fence_01", gFence, new Vector3(-E, 0, z + 5), 90);
                Put("Env_Fence_01", gFence, new Vector3(E, 0, z + 5), 90);
            }
            // กำแพงล่องหน (รวมถนนด้านใต้)
            Wall("Boundary_N", new Vector3(0, 2, E + 1), new Vector3(2 * E + 4, 4, 1));
            Wall("Boundary_E", new Vector3(E + 1, 2, -6), new Vector3(1, 4, 2 * E + 24));
            Wall("Boundary_W", new Vector3(-E - 1, 2, -6), new Vector3(1, 4, 2 * E + 24));
            Wall("Boundary_S", new Vector3(0, 2, -E - 13), new Vector3(2 * E + 4, 4, 1));
        }

        static void Wall(string n, Vector3 pos, Vector3 size)
        {
            var w = new GameObject(n); w.transform.SetParent(gFence, false);
            w.transform.localPosition = pos;
            w.AddComponent<BoxCollider>().size = size;
            w.isStatic = true;
        }

        // ---------- ย้ายระบบเดิมมาที่ตึกใหม่ ----------
        static Vector3 W(Vector2 local, float y = 0) { return root.TransformPoint(new Vector3(local.x, y, local.y)); }

        static Vector3 DoorPoint(string key, float extra = 1.4f, float side = 0f)
        {
            var b = Buildings[key];
            var d = FaceDir(b.yaw);
            var right = Vector3.Cross(Vector3.up, d);
            var p = new Vector3(b.center.x, 0, b.center.y) + d * (b.halfDepth + extra) + right * side;
            return root.TransformPoint(p);
        }

        static void Move(string path, Vector3 pos, float? yaw = null)
        {
            var g = GameObject.Find(path);
            if (g == null) { Debug.LogWarning("[Nisit] ไม่พบ " + path); return; }
            g.transform.position = pos;
            if (yaw.HasValue) g.transform.rotation = Quaternion.Euler(0, yaw.Value, 0);
        }

        static float FacingYawToward(Vector3 from, Vector3 to) { var d = to - from; d.y = 0; return Quaternion.LookRotation(d).eulerAngles.y; }

        static string RelinkSystems()
        {
            var log = new System.Text.StringBuilder();

            // 1) ประตูอาคาร: วางหน้าทางเข้า, ขนาดกล่องพอดีประตู
            foreach (var key in new[] { "อาคารเรียน", "ห้องสมุด", "โรงอาหาร", "หอพัก", "อาคารบริหาร", "อาคารชมรม", "ร้านค้า", "คณะ IT" })
            {
                var door = GameObject.Find("BuildingDoors/Door_" + key);
                if (door == null) { log.AppendLine("• ไม่พบ Door_" + key); continue; }
                door.transform.position = DoorPoint(key, 0.9f);
                door.transform.rotation = Quaternion.Euler(0, Buildings[key].yaw + 180, 0);
                var bc = door.GetComponent<BoxCollider>();
                if (bc != null) { bc.size = new Vector3(2.6f, 2.2f, 1.6f); bc.center = new Vector3(0, 1.1f, 0); bc.isTrigger = true; }
                log.AppendLine("✓ Door_" + key + " → " + door.transform.position);
            }

            // 2) สถานีภายนอก
            Move("ExamPoint", DoorPoint("ห้องสอบ", 0.9f));
            Move("Job_Cafe", DoorPoint("โรงอาหาร", 0.9f, 4.5f));
            Move("Job_Library", DoorPoint("ห้องสมุด", 0.9f, 5f));
            var exam = GameObject.Find("ExamPoint");
            if (exam != null) { var eb = exam.GetComponent<BoxCollider>(); if (eb) { eb.size = new Vector3(2.6f, 2.2f, 1.6f); eb.center = new Vector3(0, 1.1f, 0); } }

            // 3) ป้ายคณะ ลอยเหนือตึก
            var signMap = new Dictionary<string, string> {
                { "Sign_คณะ IT", "คณะ IT" }, { "Sign_คณะบริหารธุรกิจ", "อาคารบริหาร" },
                { "Sign_คณะวิทยาศาสตร์", "อาคารเรียน" }, { "Sign_คณะนิเทศศาสตร์", "อาคารชมรม" } };
            foreach (var kv in signMap)
            {
                var b = Buildings[kv.Value];
                Move("FacultySigns/" + kv.Key, W(b.center, b.top + 3.2f));
            }

            // 4) ที่นั่ง (ม้านั่งรอบลาน)
            var seats = new List<Transform>();
            var seating = GameObject.Find("Seating");
            if (seating != null) foreach (Transform s in seating.transform) if (Mathf.Abs(s.position.x) < 500) seats.Add(s);
            var sit = GameObject.Find("SitTest"); if (sit != null) sit.SetActive(false);
            for (int i = 0; i < seats.Count; i++)
            {
                if (i >= BenchSpots.Count) { seats[i].gameObject.SetActive(false); continue; }
                var bs = BenchSpots[i];
                seats[i].position = W(new Vector2(bs.x, bs.y));
                seats[i].rotation = Quaternion.Euler(0, bs.z, 0);
            }
            log.AppendLine("✓ ที่นั่งภายนอก " + seats.Count + " จุด");

            // 5) NPC ยืนประจำจุด
            void NPC(string name, Vector3 pos)
            {
                var g = GameObject.Find("TalkNPCs/" + name); if (g == null) return;
                var center = W(Vector2.zero);
                g.transform.position = new Vector3(pos.x, g.transform.position.y, pos.z);
            }
            NPC("NPC_รุ่นพี่ปี 4", DoorPoint("ห้องสอบ", 2.2f, -3f));
            NPC("NPC_เพื่อนร่วมคณะ", DoorPoint("อาคารเรียน", 3f, 4f));
            NPC("NPC_อาจารย์ที่ปรึกษา", DoorPoint("อาคารบริหาร", 2.5f, 3f));
            NPC("NPC_อาจารย์บรรณารักษ์", DoorPoint("ห้องสมุด", 2.5f, -3.5f));
            NPC("NPC_แม่ค้าร้านค้า", DoorPoint("ร้านค้า", 2.2f, -4f));
            // หันหน้าออกจากตึก
            foreach (var kv in new[] { ("NPC_รุ่นพี่ปี 4", "ห้องสอบ"), ("NPC_เพื่อนร่วมคณะ", "อาคารเรียน"), ("NPC_อาจารย์ที่ปรึกษา", "อาคารบริหาร"), ("NPC_อาจารย์บรรณารักษ์", "ห้องสมุด"), ("NPC_แม่ค้าร้านค้า", "ร้านค้า") })
            {
                var g = GameObject.Find("TalkNPCs/" + kv.Item1); if (g == null) continue;
                g.transform.rotation = Quaternion.Euler(0, Buildings[kv.Item2].yaw + 180, 0);
            }

            // 6) เส้นทางเดินของ NPC / สัตว์ (วางบนทางเดินใหม่)
            Vector2[][] npcRoutes =
            {
                new[] { new Vector2(-10, -10), new Vector2(10, -10), new Vector2(10, 10) },
                new[] { new Vector2(0, -45), new Vector2(0, -27), new Vector2(-30, -27) },
                new[] { new Vector2(-35, 22), new Vector2(35, 22), new Vector2(27, -20) },
            };
            string[] walkers = { "NPC_นิสิตปี 1", "NPC_นิสิตปี 3", "NPC_รุ่นพี่ใกล้จบ" };
            for (int r = 0; r < 3; r++) PlaceRoute("TalkNPCs/Route" + r, npcRoutes[r], "TalkNPCs/" + walkers[r]);

            Vector2[][] animalRoutes =
            {
                new[] { new Vector2(-18, 16), new Vector2(-8, 18), new Vector2(-12, 10), new Vector2(-19, 8) },
                new[] { new Vector2(18, -16), new Vector2(8, -18), new Vector2(12, -22), new Vector2(20, -20) },
                new[] { new Vector2(-18, -16), new Vector2(-10, -20), new Vector2(-12, -22), new Vector2(-18, -20) },
                new[] { new Vector2(40, 38), new Vector2(44, 30), new Vector2(38, 26), new Vector2(34, 40) },
                new[] { new Vector2(-44, -20), new Vector2(-40, -28), new Vector2(-44, -30), new Vector2(-46, -24) },
            };
            var animals = GameObject.Find("CampusAnimals");
            if (animals != null)
            {
                int ai = 0;
                foreach (Transform a in animals.transform)
                {
                    if (!a.name.StartsWith("Animal_")) continue;
                    if (ai < animalRoutes.Length) PlaceRoute("CampusAnimals/AnimalRoute" + ai, animalRoutes[ai], "CampusAnimals/" + a.name);
                    ai++;
                }
            }

            // 7) ผู้เล่นเริ่มกลางลาน หันขึ้นเหนือ
            var player = GameObject.Find("Player");
            if (player != null) { player.transform.position = W(new Vector2(0, -8), 0) + Vector3.up * 0.66f; player.transform.rotation = Quaternion.identity; }

            // 8) Minimap ให้เห็นกว้างขึ้นเล็กน้อย
            var mm = GameObject.Find("MinimapCamera");
            if (mm != null) { var cam = mm.GetComponent<Camera>(); if (cam != null && cam.orthographic) cam.orthographicSize = Mathf.Max(cam.orthographicSize, 18f); }

            return log.ToString();
        }

        static void PlaceRoute(string routePath, Vector2[] pts, string walkerPath)
        {
            var route = GameObject.Find(routePath);
            if (route == null) return;
            route.transform.position = Vector3.zero;
            for (int i = 0; i < route.transform.childCount; i++)
            {
                var p = pts[i % pts.Length];
                route.transform.GetChild(i).position = W(p);
            }
            var w = GameObject.Find(walkerPath);
            if (w != null) w.transform.position = new Vector3(W(pts[0]).x, w.transform.position.y, W(pts[0]).z);
        }
    }
}
#endif
