#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

namespace NisitSimulator.EditorTools
{
    // 🏫 แผนที่ มมส เขตพื้นที่ขามเรียง (อ้างอิงแผนผัง KHAM RIANG Campus) — Synty POLYGON City + mesh สร้างเอง
    //   • ถนนวงแหวนรอบมหาวิทยาลัย + ถนนขามเรียง–ท่าขอนยางตัดทแยง (NW→SE) + วงเวียน + ประตูทางเข้าหลักทิศตะวันออกเฉียงเหนือ
    //   • ใจกลาง: ลานอัฐศิลป์ (24) ล้อมด้วยตึก A สำนักวิทยบริการ (ห้องสมุด) / B / C (ห้องสอบ) / D
    //   • คณะวิทยาการสารสนเทศ (9) ตะวันออก · สำนักศึกษาทั่วไป (38 = อาคารเรียน) ตะวันตก · สำนักงานอธิการบดี (1) ใต้ถนน
    //   • เหนือวงแหวน: ตลาดน้อย (โรงอาหาร) · หอพักนิสิต · MSU Plaza (ร้านค้า) · กองกิจการนิสิต (ชมรม) · สระว่ายน้ำ · สนามฟุตบอล · อาคารพลศึกษา
    //   • สระน้ำ คลองรอบใน เส้นทางศึกษาธรรมชาติ
    //   ประตูเข้าอาคาร/ห้องภายใน/ระบบเรียน ใช้ชื่อเดิมทั้งหมด (ย้ายตำแหน่งให้อัตโนมัติ)
    //   ใช้: เมนู Nisit -> Build MSU Campus (แผนที่ มมส) · แผนที่ก่อนหน้าสำรองไว้ที่ 01_Gameplay_BeforeMSU.unity
    //   พิกัดเป็นหน่วย local (1 = 1 หน่วย Synty) ทั้งแมพย่อด้วย Scale 0.72 เหมือน M42 · +Z = ทิศเหนือ
    public static class M44MsuCampus
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";
        const string BackupPath = "Assets/_Project/Scenes/01_Gameplay_BeforeMSU.unity";
        const string SyntyRoot = "Assets/Synty/PolygonCity/Prefabs/";
        const string MatDir = "Assets/_Project/Materials/MSU";
        public const float Scale = 0.72f;
        const float FrontYaw = 180f;   // Synty: หน้าตึกหัน +Z โดยกำเนิด

        // ----- โครงถนนหลัก -----
        public static readonly Vector2 RingC = new Vector2(-11f, -15f);
        public const float RingR = 106f, RingW = 9f;
        static readonly Vector2 HwP = new Vector2(-161.7f, 58.85f);                 // ปลายฝั่ง จ.ขอนแก่น
        static readonly Vector2 HwD = new Vector2(0.828f, -0.561f).normalized;      // ไปทางเข้าเมืองมหาสารคาม
        const float HwW = 12f;
        const float GateAngle = 41f;                                                // ประตูทางเข้าหลัก (องศาจากศูนย์วงแหวน)
        const float CanalR = 97.5f, CanalW = 3.5f;
        // ขอบเขตแมพ (local)
        const float MinX = -160f, MaxX = 125f, MinZ = -130f, MaxZ = 150f;

        static Transform root, gBuild, gGround, gRoad, gPath, gWater, gProps, gTrees, gFence, gLabels;
        static readonly Dictionary<string, Bld> Buildings = new Dictionary<string, Bld>();
        static readonly List<Vector3> Blocks = new List<Vector3>();          // (x, z, r) กันต้นไม้ทับ
        static readonly List<Vector4> Walks = new List<Vector4>();          // ทางเดิน (ax, az, bx, bz)
        public static readonly List<Vector3> BenchSpots = new List<Vector3>();
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        struct Bld { public string key; public Vector2 center; public float yaw; public float halfDepth; public float top; public float radius; }

        [MenuItem("Nisit/Build MSU Campus (แผนที่ มมส)")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BackupScene();
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
            string log = Build();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorUtility.DisplayDialog("Nisit Simulator", "สร้างแผนที่ มมส เขตพื้นที่ขามเรียงแล้ว\nฉากก่อนหน้าสำรองไว้ที่ 01_Gameplay_BeforeMSU.unity\n\n" + log, "เยี่ยม!");
        }

        public static bool BackupScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) != null) return false;   // สำรองครั้งแรกครั้งเดียว
            return AssetDatabase.CopyAsset(GameplayPath, BackupPath);
        }

        public static string Build()
        {
            var log = new System.Text.StringBuilder();
            Buildings.Clear(); Blocks.Clear(); Walks.Clear(); BenchSpots.Clear(); Mats.Clear();

            foreach (var n in new[] { "Campus", "MapBorder", "NewCampus", "MsuLabels" })
            {
                var old = GameObject.Find(n);
                if (old != null) Object.DestroyImmediate(old);
            }

            root = new GameObject("NewCampus").transform;
            root.localScale = Vector3.one * Scale;
            gGround = Group("Ground"); gRoad = Group("Roads"); gPath = Group("Paths"); gWater = Group("Water");
            gBuild = Group("Buildings"); gProps = Group("Props"); gTrees = Group("Trees"); gFence = Group("Boundary");
            gLabels = new GameObject("MsuLabels").transform;   // ป้ายชื่อ (world space ไม่ย่อตาม root)

            Ground();
            Roads();
            WaterAll();
            Plaza();
            BuildingsAll();
            WalksAll();
            SportsAll();
            PropsAll();
            TreesAll();
            BoundaryAll();

            log.AppendLine(RelinkSystems());
            LabelsAll();
            log.AppendLine(Verify());
            Debug.Log("<color=lime>[Nisit] สร้างแผนที่ มมส แล้ว</color>\n" + log);
            return log.ToString();
        }

        static Transform Group(string n) { var t = new GameObject(n).transform; t.SetParent(root, false); return t; }

        // =====================================================================
        // วัสดุ + mesh สร้างเอง
        // =====================================================================
        static Material Mat(string name, Color c, float smooth = 0.1f, Texture2D tex = null)
        {
            if (Mats.TryGetValue(name, out var m) && m != null) return m;
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials")) AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "MSU");
            string path = MatDir + "/MSU_" + name + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        static Material MAsphalt => Mat("Asphalt", new Color(0.24f, 0.25f, 0.27f), 0.15f);
        static Material MLine => Mat("RoadLine", new Color(0.95f, 0.95f, 0.92f));
        static Material MYellow => Mat("RoadYellow", new Color(0.96f, 0.78f, 0.18f));
        static Material MWalk => Mat("Walkway", new Color(0.78f, 0.74f, 0.68f));
        static Material MWater => Mat("Water", new Color(0.36f, 0.62f, 0.80f), 0.85f);
        static Material MStone => Mat("PondEdge", new Color(0.55f, 0.53f, 0.50f));
        static Material MForest => Mat("GrassDark", new Color(0.30f, 0.50f, 0.26f), 0.05f);
        static Material MField => Mat("Field", new Color(0.36f, 0.64f, 0.30f), 0.05f);
        static Material MTrack => Mat("Track", new Color(0.72f, 0.36f, 0.28f), 0.1f);
        static Material MPillar => Mat("GatePillar", new Color(0.93f, 0.88f, 0.76f), 0.2f);
        static Material MGold => Mat("GateGold", new Color(0.86f, 0.70f, 0.30f), 0.5f);
        static Material MPlazaGreen => Mat("PlazaGreen", new Color(0.40f, 0.58f, 0.34f), 0.05f);

        static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat)
        {
            var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            g.transform.SetParent(parent, false);
            mesh.name = name;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            g.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.isStatic = true;
            return g;
        }

        static Mesh Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var m = new Mesh();
            m.vertices = new[] { a, b, c, d };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            return m;
        }

        // แถบตรงจาก a → b กว้าง w (บนพื้น)
        static GameObject Strip(string name, Transform parent, Vector2 a, Vector2 b, float w, float y, Material mat)
        {
            var dir = (b - a).normalized;
            var n = new Vector2(-dir.y, dir.x) * (w * 0.5f);
            var m = Quad(new Vector3(a.x - n.x, y, a.y - n.y), new Vector3(a.x + n.x, y, a.y + n.y),
                         new Vector3(b.x + n.x, y, b.y + n.y), new Vector3(b.x - n.x, y, b.y - n.y));
            FixWinding(m);
            return MeshObj(name, parent, m, mat);
        }

        static GameObject Rect(string name, Transform parent, Vector2 c, float w, float d, float y, Material mat)
            => Strip(name, parent, new Vector2(c.x, c.y - d * 0.5f), new Vector2(c.x, c.y + d * 0.5f), w, y, mat);

        // ส่วนโค้ง (วงแหวน) จุดศูนย์ c รัศมี r กว้าง w จากมุม a0 → a1 (องศา, 0 = ตะวันออก, ทวนเข็ม)
        static GameObject Arc(string name, Transform parent, Vector2 c, float r, float w, float a0, float a1, float y, Material mat)
        {
            int seg = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 2.5f));
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, i / (float)seg);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var pi = c + dir * (r - w * 0.5f); var po = c + dir * (r + w * 0.5f);
                v.Add(new Vector3(pi.x, y, pi.y)); v.Add(new Vector3(po.x, y, po.y));
                uv.Add(new Vector2(0, i)); uv.Add(new Vector2(1, i));
                if (i > 0) { int k = i * 2; t.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 }); }
            }
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv);
            FixWinding(m);
            return MeshObj(name, parent, m, mat);
        }

        // วงรี (พื้นเรียบ)
        static GameObject Ellipse(string name, Transform parent, Vector2 c, float rx, float rz, float y, Material mat, int seg = 36)
        {
            var v = new List<Vector3> { new Vector3(c.x, y, c.y) }; var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(c.x + Mathf.Cos(a) * rx, y, c.y + Mathf.Sin(a) * rz));
                if (i > 0) t.AddRange(new[] { 0, i + 1, i });
            }
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0);
            FixWinding(m);
            return MeshObj(name, parent, m, mat);
        }

        // ให้ทุกหน้าหงายขึ้น (normal +Y) ไม่ว่าจะส่งจุดมาทิศไหน
        static void FixWinding(Mesh m)
        {
            var v = m.vertices; var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (n.y < 0) { int s = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = s; }
            }
            m.triangles = t;
        }

        // กำแพงล่องหนรอบแหล่งน้ำ (convex prism) — กันเดินลงน้ำ
        static void WaterCollider(string name, Vector2 c, float rx, float rz)
        {
            var g = new GameObject(name + "_Collider"); g.transform.SetParent(gWater, false);
            var v = new List<Vector3>(); var t = new List<int>();
            const int seg = 20;
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float x = c.x + Mathf.Cos(a) * rx * 0.92f, z = c.y + Mathf.Sin(a) * rz * 0.92f;
                v.Add(new Vector3(x, -0.5f, z)); v.Add(new Vector3(x, 2.5f, z));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = ((i + 1) % seg) * 2;
                t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
            var m = new Mesh { name = name + "_Hull" }; m.SetVertices(v); m.SetTriangles(t, 0);
            var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = m; mc.convex = true;
            g.isStatic = true;
        }

        // =====================================================================
        // Synty helpers (เหมือน M42)
        // =====================================================================
        static GameObject Load(string name)
        {
            foreach (var cat in new[] { "Buildings/", "Environments/", "Props/", "Vehicles/", "Environments/Custom/", "Buildings/Custom/" })
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(SyntyRoot + cat + "SM_" + name + ".prefab");
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
            return b;
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

        static float Stack(Transform parent, Vector2 c, float yaw, string baseName, string floorName, int floors, string roofName)
        {
            var off = FootOffset(baseName);
            float y = 0;
            PutCentered(baseName, parent, c, y, yaw, off); y += Height(baseName);
            for (int i = 0; i < floors; i++) { PutCentered(floorName, parent, c, y, yaw, off); y += Height(floorName); }
            if (!string.IsNullOrEmpty(roofName)) { PutCentered(roofName, parent, c, y, yaw, off); y += Height(roofName); }
            return y;
        }

        static void Register(string key, Vector2 center, float faceYaw, float halfDepth, float top, float radius)
        {
            Buildings[key] = new Bld { key = key, center = center, yaw = faceYaw, halfDepth = halfDepth, top = top, radius = radius };
            Blocks.Add(new Vector3(center.x, center.y, radius + 1.5f));
        }

        // ทิศหน้าตึก: yaw 0 = หันไป -Z (ใต้), 90 = -X (ตะวันตก), -90 = +X (ตะวันออก), 180 = +Z (เหนือ)
        static Vector3 FaceDir(float yaw) { return Quaternion.Euler(0, yaw, 0) * Vector3.back; }
        static Vector2 Front(string key, float extra) { var b = Buildings[key]; var d = FaceDir(b.yaw); return b.center + new Vector2(d.x, d.z) * (b.halfDepth + extra); }

        static void Pave(string tile, float x0, float x1, float z0, float z1)
        {
            for (float x = x0; x < x1 - 0.01f; x += 5)
                for (float z = z0; z < z1 - 0.01f; z += 5)
                    Put(tile, gPath, new Vector3(x + 5, 0.03f, z + 5), 0);
        }

        static Vector2 Hw(float t) => HwP + HwD * t;
        static Vector2 RingPoint(float deg, float r = RingR) => RingC + new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)) * r;
        static float YawAlong(Vector2 d) => Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;   // yaw ที่ทำให้ +Z ชี้ไปตาม d

        // จุดที่ถนนทแยงตัดวงแหวน (t ของ Hw)
        static void HwRingT(out float tNW, out float tSE)
        {
            var pc = HwP - RingC;
            float b = 2f * Vector2.Dot(HwD, pc), c = pc.sqrMagnitude - RingR * RingR;
            float disc = Mathf.Sqrt(Mathf.Max(0f, b * b - 4f * c));
            tNW = (-b - disc) / 2f; tSE = (-b + disc) / 2f;
        }

        static float DistToHw(Vector2 p) { var rel = p - HwP; return Mathf.Abs(rel.x * HwD.y - rel.y * HwD.x); }

        static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        // =====================================================================
        // พื้น / ถนน
        // =====================================================================
        static void Ground()
        {
            var g = GameObject.Find("Ground");
            if (g == null) { g = GameObject.CreatePrimitive(PrimitiveType.Plane); g.name = "Ground"; }
            g.transform.position = Vector3.zero;
            g.transform.localScale = new Vector3(30, 1, 30);   // 300 x 300 ม. (ครอบแมพ ±115 ม.)
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Campus_Grass.mat");
            if (mat != null) g.GetComponent<Renderer>().sharedMaterial = mat;
            g.isStatic = true;
        }

        static void Road(string name, Vector2 a, Vector2 b, float w, bool yellowCenter)
        {
            Strip(name, gRoad, a, b, w, 0.02f, MAsphalt);
            // เส้นขอบถนนสีขาว + เส้นกลาง
            var d = (b - a).normalized; var n = new Vector2(-d.y, d.x);
            Strip(name + "_EdgeL", gRoad, a + n * (w * 0.5f - 0.5f), b + n * (w * 0.5f - 0.5f), 0.25f, 0.032f, MLine);
            Strip(name + "_EdgeR", gRoad, a - n * (w * 0.5f - 0.5f), b - n * (w * 0.5f - 0.5f), 0.25f, 0.032f, MLine);
            Strip(name + "_Center", gRoad, a, b, yellowCenter ? 0.45f : 0.25f, 0.034f, yellowCenter ? MYellow : MLine);
        }

        static void Roads()
        {
            // ถนนวงแหวน
            Arc("RingRoad", gRoad, RingC, RingR, RingW, 0, 360, 0.02f, MAsphalt);
            Arc("RingRoad_EdgeIn", gRoad, RingC, RingR - RingW * 0.5f + 0.5f, 0.25f, 0, 360, 0.032f, MLine);
            Arc("RingRoad_EdgeOut", gRoad, RingC, RingR + RingW * 0.5f - 0.5f, 0.25f, 0, 360, 0.032f, MLine);
            Arc("RingRoad_Center", gRoad, RingC, RingR, 0.25f, 0, 360, 0.034f, MLine);

            // ถนนขามเรียง–ท่าขอนยาง (ทแยงผ่านมหาวิทยาลัย) — สูงกว่าวงแหวนเล็กน้อยกันพื้นซ้อน
            Strip("Highway", gRoad, Hw(-20), Hw(400), HwW, 0.024f, MAsphalt);
            Strip("Highway_Median", gRoad, Hw(-20), Hw(400), 0.5f, 0.036f, MYellow);
            var hn = new Vector2(-HwD.y, HwD.x);
            Strip("Highway_EdgeL", gRoad, Hw(-20) + hn * (HwW * 0.5f - 0.5f), Hw(400) + hn * (HwW * 0.5f - 0.5f), 0.25f, 0.036f, MLine);
            Strip("Highway_EdgeR", gRoad, Hw(-20) - hn * (HwW * 0.5f - 0.5f), Hw(400) - hn * (HwW * 0.5f - 0.5f), 0.25f, 0.036f, MLine);

            // วงเวียนฝั่งตะวันออกเฉียงใต้ (ถนนทแยงตัดวงแหวน)
            HwRingT(out float tNW, out float tSE);
            var rb = Hw(tSE);
            Ellipse("Roundabout", gRoad, rb, 12f, 12f, 0.028f, MAsphalt);
            Ellipse("Roundabout_Island", gRoad, rb, 5.5f, 5.5f, 0.04f, MPlazaGreen);
            Put("Prop_Planter_02", gProps, new Vector3(rb.x, 0.04f, rb.y), 0);
            Blocks.Add(new Vector3(rb.x, rb.y, 13f));

            // ถนนเข้าประตูหลัก (ตะวันออกเฉียงเหนือ) ออกนอกแมพ
            var g0 = RingPoint(GateAngle, RingR + RingW * 0.4f);
            var gd = (RingPoint(GateAngle) - RingC).normalized;
            var g1 = g0 + gd * 60f;
            Road("GateRoad", g0, g1, 10f, true);

            // ถนนขึ้นโซนหอพัก (ตลาดน้อย/หอพัก/MSU Plaza) — จากวงแหวนด้านเหนือ
            var nTop = RingPoint(90f, RingR + RingW * 0.4f);
            Road("DormRoadN", nTop, new Vector2(nTop.x, 103f), 7f, false);
            Road("DormStreet", new Vector2(-112f, 103f), new Vector2(32f, 103f), 7f, false);
            // ทางเท้าเลียบถนนหอพัก
            Strip("DormWalkS", gPath, new Vector2(-112f, 98.3f), new Vector2(32f, 98.3f), 2.4f, 0.026f, MWalk);
            Strip("DormWalkN", gPath, new Vector2(-112f, 107.7f), new Vector2(32f, 107.7f), 2.4f, 0.026f, MWalk);

            // ทางม้าลายข้ามถนนทแยง (ลานกลาง → สำนักงานอธิการบดี)
            Zebra(new Vector2(-38f, -25f));
        }

        static void Zebra(Vector2 center)
        {
            var hn = new Vector2(-HwD.y, HwD.x);
            for (int i = -3; i <= 3; i++)
            {
                var c = center + HwD * (i * 1.1f);
                Strip("Zebra_" + (i + 3), gRoad, c - hn * (HwW * 0.45f), c + hn * (HwW * 0.45f), 0.6f, 0.04f, MLine);
            }
        }

        // =====================================================================
        // น้ำ: คลองรอบใน + สระ + เส้นทางศึกษาธรรมชาติ
        // =====================================================================
        static void Pond(string name, Vector2 c, float rx, float rz)
        {
            Ellipse(name + "_Edge", gWater, c, rx + 1.2f, rz + 1.2f, 0.035f, MStone);
            Ellipse(name, gWater, c, rx, rz, 0.05f, MWater);
            WaterCollider(name, c, rx, rz);
            Blocks.Add(new Vector3(c.x, c.y, Mathf.Max(rx, rz) + 2f));
        }

        static void WaterAll()
        {
            // คลองรอบใน (เว้นช่องตรงทางผ่าน: ประตูหลัก / ถนนหอพัก / ทางไปอาคารพลศึกษา)
            float[][] spans = { new[] { -20f, 36f }, new[] { 46f, 85f }, new[] { 95f, 128f }, new[] { 139f, 150f } };
            for (int i = 0; i < spans.Length; i++)
                Arc("Canal_" + i, gWater, RingC, CanalR, CanalW, spans[i][0], spans[i][1], 0.05f, MWater);

            Pond("Pond_North", new Vector2(-28f, 48f), 8f, 7f);        // สระหน้าอาคาร (เหนือลาน)
            Pond("Pond_NW", new Vector2(-72f, 30f), 9f, 13f);          // สระใหญ่ฝั่งตะวันตกเฉียงเหนือ
            Pond("Pond_East", new Vector2(62f, 6f), 8f, 9f);           // สระฝั่งตะวันออก

            // เส้นทางศึกษาธรรมชาติ (3) — ป่าเขียว + สระ ทางใต้ถนนทแยง
            Ellipse("NatureTrail", gGround, new Vector2(2f, -86f), 32f, 13f, 0.015f, MForest);
            Pond("Pond_Nature", new Vector2(10f, -86f), 9f, 3.5f);
            Strip("NatureBoardwalk", gPath, new Vector2(-24f, -84f), new Vector2(-2f, -80f), 2.2f, 0.03f, MWalk);
            Strip("NatureBoardwalk2", gPath, new Vector2(-2f, -80f), new Vector2(24f, -80f), 2.2f, 0.03f, MWalk);
        }

        // =====================================================================
        // ลานอัฐศิลป์ (24)
        // =====================================================================
        static void Plaza()
        {
            Pave("Env_Sidewalk_01", -15, 15, -15, 15);
            Ellipse("Plaza_Green", gPath, Vector2.zero, 7f, 7f, 0.07f, MPlazaGreen, 8);   // แปดเหลี่ยมสีเขียวกลางลาน
            Put("Prop_Planter_02", gProps, new Vector3(0, 0.07f, 0), 0);
            Blocks.Add(new Vector3(0, 0, 17f));

            Vector2[] bp = { new Vector2(-9, 13), new Vector2(9, 13), new Vector2(-9, -13), new Vector2(9, -13), new Vector2(-13, 9), new Vector2(-13, -9), new Vector2(13, 9), new Vector2(13, -9) };
            foreach (var p in bp)
            {
                float yaw = Mathf.Atan2(-p.x, -p.y) * Mathf.Rad2Deg;
                yaw = Mathf.Round(yaw / 90f) * 90f;
                Put("Prop_ParkBench_01", gProps, new Vector3(p.x, 0, p.y), yaw + 180);
                BenchSpots.Add(new Vector3(p.x, p.y, yaw));
            }
            foreach (var p in new[] { new Vector2(-13, 13), new Vector2(13, 13), new Vector2(-13, -13), new Vector2(13, -13) })
                Put("Prop_Planter_01", gProps, new Vector3(p.x, 0, p.y), 0);
        }

        // =====================================================================
        // อาคาร (หมายเลขตามแผนผัง มมส)
        // =====================================================================
        static void BuildingsAll()
        {
            float f = FrontYaw;
            Transform t; float top; Vector2 c;

            // 20 ตึก A สำนักวิทยบริการ = ห้องสมุด (เหนือลาน หันลงใต้)
            c = new Vector2(0, 27);
            t = Sub("ห้องสมุด");
            top = Stack(t, c, 0 + f, "Bld_OfficeOctagon_Base_01", "Bld_OfficeOctagon_Floor_01", 1, "Bld_OfficeOctagon_Roof_01");
            Register("ห้องสมุด", c, 0, 10.4f, top, 14.5f);

            // 21 ตึก B สำนักคอมพิวเตอร์ (ตะวันออกของลาน)
            c = new Vector2(26, 0);
            // (ชุด OfficeSquare ของ Synty เป็นแค่ผนังสองด้าน มองใกล้จะกลวง → ใช้ OfficeOld ที่เป็นตึกเต็มหลัง)
            t = Sub("ตึก B สำนักคอมพิวเตอร์");
            top = Stack(t, c, 90 + f, "Bld_OfficeOld_Small_Base_01", "Bld_OfficeOld_Small_Floor_01", 1, "Bld_OfficeOld_Small_Roof_01");
            Register("ตึก B", c, 90, 5.6f, top, 7.9f);

            // 22 ตึก C = ห้องสอบ (ใต้ลาน หันขึ้นเหนือ)
            c = new Vector2(0, -25);
            t = Sub("ห้องสอบ");
            top = Stack(t, c, 180 + f, "Bld_OfficeOld_Small_Base_01", "Bld_OfficeOld_Small_Floor_01", 1, "Bld_OfficeOld_Small_Roof_01");
            Register("ห้องสอบ", c, 180, 5.6f, top, 7.9f);

            // 23 ตึก D วิทยาลัยการเมืองการปกครอง (ตะวันตกของลาน)
            c = new Vector2(-26, 0);
            t = Sub("ตึก D วิทยาลัยการเมืองการปกครอง");
            top = Stack(t, c, -90 + f, "Bld_OfficeOld_Small_Base_01", "Bld_OfficeOld_Small_Floor_01", 1, "Bld_OfficeOld_Small_Roof_01");
            Register("ตึก D", c, -90, 5.6f, top, 7.9f);

            // 9 คณะวิทยาการสารสนเทศ = คณะ IT (ตะวันออกเฉียงเหนือของลาน หันไปตะวันตก)
            c = new Vector2(42, 26);
            t = Sub("คณะ IT");
            top = Stack(t, c, 90 + f, "Bld_OfficeRound_Base_01", null, 0, "Bld_OfficeRound_Roof_01");
            Register("คณะ IT", c, 90, 11.6f, top, 16.3f);

            // 38 สำนักศึกษาทั่วไป = อาคารเรียน (วิชา GE) ตะวันตก หันไปตะวันออก
            c = new Vector2(-50, 24);
            PutCentered("Bld_CityHall_01", gBuild, c, 0, -90 + f).name = "อาคารเรียน";
            Register("อาคารเรียน", c, -90, 8.9f, 8.8f, 13.4f);

            // 1 สำนักงานอธิการบดี = อาคารบริหาร (ใต้ถนนทแยง หันขึ้นเหนือ) + เสาธงชาติ
            c = new Vector2(-38, -52);
            t = Sub("อาคารบริหาร");
            top = Stack(t, c, 180 + f, "Bld_OfficeOld_Large_Base_01", null, 0, "Bld_OfficeOld_Large_Roof_01");
            Register("อาคารบริหาร", c, 180, 8f, top, 11.3f);
            FlagPole(new Vector2(-26f, -40f));

            // 19 คณะวิทยาศาสตร์ (เหนือ)
            c = new Vector2(22, 50);
            t = Sub("คณะวิทยาศาสตร์");
            top = Stack(t, c, 0 + f, "Bld_OfficeOld_Large_Base_01", null, 0, "Bld_OfficeOld_Large_Roof_01");
            Register("คณะวิทยาศาสตร์", c, 0, 8f, top, 11.3f);

            // 29 กองทะเบียนและประมวลผล
            c = new Vector2(5, 64);
            PutCentered("Bld_Station_03", gBuild, c, 0, 0 + f).name = "กองทะเบียนและประมวลผล";
            Register("กองทะเบียน", c, 0, 3f, 5.7f, 6.2f);

            // 27 คณะการบัญชีและการจัดการ (ตะวันตกเฉียงเหนือ)
            c = new Vector2(-52, 56);
            t = Sub("คณะการบัญชีและการจัดการ");
            top = Stack(t, c, 0 + f, "Bld_OfficeOld_Large_Base_01", "Bld_OfficeOld_Large_Floor_01", 1, "Bld_OfficeOld_Large_Roof_01");
            Register("คณะการบัญชี", c, 0, 8f, top, 11.3f);

            // 39 คณะมนุษยศาสตร์และสังคมศาสตร์
            c = new Vector2(-44, -2);
            t = Sub("คณะมนุษยศาสตร์และสังคมศาสตร์");
            top = Stack(t, c, -90 + f, "Bld_OfficeOld_Small_Base_01", null, 0, "Bld_OfficeOld_Small_Roof_01");
            Register("คณะมนุษยศาสตร์", c, -90, 5.6f, top, 7.9f);

            // 8 คณะพยาบาลศาสตร์
            c = new Vector2(42, -12);
            t = Sub("คณะพยาบาลศาสตร์");
            top = Stack(t, c, 90 + f, "Bld_OfficeOld_Small_Base_01", "Bld_OfficeOld_Small_Floor_01", 1, "Bld_OfficeOld_Small_Roof_01");
            Register("คณะพยาบาลศาสตร์", c, 90, 5.6f, top, 7.9f);

            // 6 คณะเภสัชศาสตร์
            c = new Vector2(24, -34);
            t = Sub("คณะเภสัชศาสตร์");
            top = Stack(t, c, 180 + f, "Bld_OfficeOld_Small_Base_01", "Bld_OfficeOld_Small_Floor_01", 2, "Bld_OfficeOld_Small_Roof_01");
            Register("คณะเภสัชศาสตร์", c, 180, 5.6f, top, 7.9f);

            // ===== โซนเหนือวงแหวน (ถนนหอพัก z=103 · ตึกหันลงใต้) =====
            // 33 หอพักนิสิต — อพาร์ตเมนต์ 3 ชั้น 4 หลัง
            t = Sub("หอพัก");
            for (int i = 0; i < 4; i++) Apartment(t, new Vector2(-80 + i * 5, 112), 3);
            Register("หอพัก", new Vector2(-72.5f, 112), 0, 2.8f, 9.5f, 11f);
            // หอพักหลังที่สอง (ตกแต่ง)
            var t2 = Sub("หอพักนิสิต_2");
            for (int i = 0; i < 4; i++) Apartment(t2, new Vector2(-80 + i * 5, 126), 2);
            Blocks.Add(new Vector3(-72.5f, 126, 11f));

            // 34 MSU Plaza = ร้านค้า
            t = Sub("ร้านค้า");
            string[] mart = { "Bld_Shop_05", "Bld_Shop_06", "Bld_Shop_02" };
            for (int i = 0; i < mart.Length; i++) PutCentered(mart[i], t, new Vector2(-45 + i * 5, 112), 0, 0 + f);
            Register("ร้านค้า", new Vector2(-40, 112), 0, 2.8f, 3.3f, 8f);
            Put("Prop_LargeSign_Soda_01", t, new Vector3(-40, 3.3f, 113), 0 + f);

            // 31 ตลาดน้อย = โรงอาหาร (ร้านอาหารเรียง + โต๊ะปิกนิก)
            t = Sub("โรงอาหาร");
            string[] shops = { "Bld_Shop_01", "Bld_Shop_02", "Bld_Shop_04", "Bld_Shop_05" };
            for (int i = 0; i < shops.Length; i++) PutCentered(shops[i], t, new Vector2(0 + i * 5, 112), 0, 0 + f);
            Register("โรงอาหาร", new Vector2(7.5f, 112), 0, 2.8f, 3.3f, 11f);
            Put("Prop_LargeSign_Noodles_01", t, new Vector3(7.5f, 3.3f, 113), 0 + f);

            // 32 กองกิจการนิสิต = อาคารชมรม
            c = new Vector2(-20, 125);
            PutCentered("Bld_Station_01", gBuild, c, 0, 0 + f).name = "อาคารชมรม";
            Register("อาคารชมรม", c, 0, 5f, 8.6f, 7.1f);

            // 37 อาคารพลศึกษา (หันขึ้นเหนือไปทางถนนหอพัก)
            // อาคารทรงกลม = ยิม
            c = new Vector2(-96, 84);
            t = Sub("อาคารพลศึกษา");
            top = Stack(t, c, 180 + f, "Bld_OfficeRound_Base_01", null, 0, "Bld_OfficeRound_Roof_01");
            Register("อาคารพลศึกษา", c, 180, 11.6f, top, 16.3f);

            // 15 อาคารชุดที่พักบุคลากร (นอกวงแหวน ตะวันออก)
            t = Sub("อาคารชุดที่พักบุคลากร");
            for (int i = 0; i < 3; i++) Apartment(t, new Vector2(100, 34 + i * 5.2f), 2, 90);
            Blocks.Add(new Vector3(100, 39, 9f));
        }

        static Transform Sub(string n) { var t = new GameObject(n).transform; t.SetParent(gBuild, false); return t; }

        static void Apartment(Transform parent, Vector2 cc, int floors, float face = 0)
        {
            float f = FrontYaw + face;
            var off = FootOffset("Bld_Apartment_Door_01");
            PutCentered("Bld_Apartment_Door_01", parent, cc, 0, f, off);
            float y = 3;
            for (int k = 1; k < floors; k++, y += 3) PutCentered("Bld_Apartment_01", parent, cc, y, f, off);
            PutCentered("Bld_Apartment_Roof_01", parent, cc, y, f, off);
        }

        // เสาธงชาติ (ธงไตรรงค์) หน้าสำนักงานอธิการบดี
        static void FlagPole(Vector2 p)
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "FlagPole"; pole.transform.SetParent(gProps, false);
            pole.transform.localPosition = new Vector3(p.x, 4.5f, p.y);
            pole.transform.localScale = new Vector3(0.25f, 4.5f, 0.25f);
            pole.GetComponent<Renderer>().sharedMaterial = Mat("FlagPole", new Color(0.85f, 0.85f, 0.88f), 0.6f);

            var tex = ThaiFlagTexture();
            var flag = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(flag.GetComponent<Collider>());
            flag.name = "ThaiFlag"; flag.transform.SetParent(gProps, false);
            flag.transform.localPosition = new Vector3(p.x + 1.65f, 8.1f, p.y + 1.65f);
            flag.transform.localRotation = Quaternion.Euler(0, 45, 0);
            flag.transform.localScale = new Vector3(3f, 2f, 1f);
            var m = Mat("ThaiFlag", Color.white, 0.1f, tex);
            m.SetFloat("_Cull", 0f);   // สองหน้า
            flag.GetComponent<Renderer>().sharedMaterial = m;
            Blocks.Add(new Vector3(p.x, p.y, 2f));
        }

        static Texture2D ThaiFlagTexture()
        {
            const string path = "Assets/_Project/Art/UI/thai_flag.png";
            var ex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (ex != null) return ex;
            var red = new Color32(165, 25, 49, 255); var white = new Color32(244, 245, 248, 255); var blue = new Color32(45, 42, 74, 255);
            var tex = new Texture2D(9, 6, TextureFormat.RGBA32, false);
            Color32[] rows = { red, white, blue, blue, white, red };
            for (int y = 0; y < 6; y++) for (int x = 0; x < 9; x++) tex.SetPixel(x, y, rows[y]);
            tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "_Project/Art/UI/thai_flag.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // =====================================================================
        // ทางเดิน
        // =====================================================================
        static void Walk(string name, Vector2 a, Vector2 b, float w = 4f)
        {
            Strip(name, gPath, a, b, w, 0.026f, MWalk);
            Walks.Add(new Vector4(a.x, a.y, b.x, b.y));
        }

        static void WalksAll()
        {
            // แกนเหนือ–ใต้ (ลาน → ถนนวงแหวน → โซนหอพัก)
            Walk("Walk_Spine", new Vector2(-13, 15), new Vector2(-13, RingC.y + Mathf.Sqrt(RingR * RingR - 4f) - RingW * 0.5f));
            // แนวตะวันออก–ตะวันตกเหนือห้องสมุด (สระ / กองทะเบียน / คณะวิทยาศาสตร์)
            Walk("Walk_North", new Vector2(-13, 40), new Vector2(28, 40));
            Walk("Walk_Registrar", new Vector2(5, 40), Front("กองทะเบียน", 0.5f));
            Walk("Walk_Science", new Vector2(22, 40), Front("คณะวิทยาศาสตร์", 0.5f));
            // คณะ IT ↔ ประตูหลัก
            Walk("Walk_ITNorth", new Vector2(28, 12), new Vector2(28, 40));
            Walk("Walk_ToGate", new Vector2(28, 40), RingPoint(GateAngle, RingR - RingW * 0.5f));
            // ลาน → คณะ IT / ตึก B
            Walk("Walk_East", new Vector2(15, 12), new Vector2(30, 12));
            Walk("Walk_IT", new Vector2(28, 12), Front("คณะ IT", 0.5f) + new Vector2(0, 0));
            // ลาน → อาคารเรียน (สำนักศึกษาทั่วไป)
            Walk("Walk_West", new Vector2(-15, 10), new Vector2(-37, 10));
            Walk("Walk_WestUp", new Vector2(-37, 10), new Vector2(-37, 24));
            Walk("Walk_Hall", new Vector2(-37, 24), Front("อาคารเรียน", 0.5f));
            Walk("Walk_Human", new Vector2(-37, 10), Front("คณะมนุษยศาสตร์", 0.5f));
            // แกนตะวันตก → คณะการบัญชี
            Walk("Walk_NW", new Vector2(-13, 37), new Vector2(-52, 37));
            Walk("Walk_Account", new Vector2(-52, 37), Front("คณะการบัญชี", 0.5f));
            // ลาน → ห้องสอบ (ตึก C)
            Walk("Walk_Exam", new Vector2(0, -15), Front("ห้องสอบ", 0.3f));
            // ลาน → คณะพยาบาล / เภสัช
            Walk("Walk_SE", new Vector2(15, -12), Front("คณะพยาบาลศาสตร์", 0.5f));
            Walk("Walk_Pharm", new Vector2(24, -12), Front("คณะเภสัชศาสตร์", 0.5f));
            // ลาน → ข้ามถนนทแยง (ทางม้าลาย) → สำนักงานอธิการบดี → เส้นทางธรรมชาติ
            Walk("Walk_SW", new Vector2(-15, -15), new Vector2(-38, -15));
            Walk("Walk_Admin", new Vector2(-38, -15), Front("อาคารบริหาร", 0.3f));
            Walk("Walk_Nature", new Vector2(-30, -62), new Vector2(-24, -84));
            Walk("Walk_AdminNature", Front("อาคารบริหาร", 0.3f) + new Vector2(8, 0), new Vector2(-30, -62));
            // โซนหอพัก
            Walk("Walk_Club", new Vector2(-20, 107), Front("อาคารชมรม", 0.3f));
            Walk("Walk_PE", new Vector2(-96, 99), Front("อาคารพลศึกษา", 0.3f));
            // หน้าตลาดน้อย: ลานโต๊ะ
            Rect("Market_Yard", gPath, new Vector2(27, 113), 12, 10, 0.026f, MWalk);
            Blocks.Add(new Vector3(27, 113, 7f));
        }

        // =====================================================================
        // สนามกีฬา
        // =====================================================================
        static void SportsAll()
        {
            // 36 สนามฟุตบอล + ลู่วิ่ง
            var fc = new Vector2(-136, 104);
            Rect("Stadium_Track", gGround, fc, 44, 30, 0.02f, MTrack);
            Rect("Football_Field", gGround, fc, 38, 24, 0.03f, MField);
            Rect("Field_LineMid", gGround, fc, 0.3f, 24, 0.04f, MLine);
            Ellipse("Field_Circle", gGround, fc, 3.5f, 3.5f, 0.035f, MLine);
            Ellipse("Field_CircleIn", gGround, fc, 3.2f, 3.2f, 0.038f, MField);
            Rect("Field_LineN", gGround, fc + new Vector2(0, 11.85f), 38, 0.3f, 0.04f, MLine);
            Rect("Field_LineS", gGround, fc - new Vector2(0, 11.85f), 38, 0.3f, 0.04f, MLine);
            Rect("Field_LineW", gGround, fc - new Vector2(18.85f, 0), 0.3f, 24, 0.04f, MLine);
            Rect("Field_LineE", gGround, fc + new Vector2(18.85f, 0), 0.3f, 24, 0.04f, MLine);
            Goal(fc + new Vector2(-18.6f, 0), 90); Goal(fc + new Vector2(18.6f, 0), -90);
            Blocks.Add(new Vector3(fc.x, fc.y, 24f));
            Walk("Walk_Stadium", new Vector2(-112, 103), new Vector2(-114, 103));

            // 35 สระว่ายน้ำ
            var pc = new Vector2(-100, 120);
            Rect("Pool_Deck", gGround, pc, 18, 11, 0.03f, MWalk);
            Rect("Pool_Water", gGround, pc, 14, 7, 0.05f, MWater);
            for (int i = -2; i <= 2; i++) Rect("Pool_Lane" + i, gGround, pc + new Vector2(0, i * 1.4f), 13.6f, 0.12f, 0.055f, MLine);
            var pg = new GameObject("Pool_Collider"); pg.transform.SetParent(gGround, false);
            pg.transform.localPosition = new Vector3(pc.x, 1f, pc.y);
            pg.AddComponent<BoxCollider>().size = new Vector3(13.2f, 2f, 6.4f);
            Blocks.Add(new Vector3(pc.x, pc.y, 10f));
            Walk("Walk_Pool", new Vector2(-100, 107), new Vector2(-100, 114.5f), 3f);
        }

        static void Goal(Vector2 p, float yaw)
        {
            var g = new GameObject("Goal"); g.transform.SetParent(gProps, false);
            g.transform.localPosition = new Vector3(p.x, 0, p.y);
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var m = Mat("GoalPost", Color.white, 0.4f);
            void Bar(Vector3 pos, Vector3 scale)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.transform.SetParent(g.transform, false); b.transform.localPosition = pos; b.transform.localScale = scale;
                b.GetComponent<Renderer>().sharedMaterial = m;
            }
            Bar(new Vector3(-3.6f, 1.2f, 0), new Vector3(0.18f, 2.4f, 0.18f));
            Bar(new Vector3(3.6f, 1.2f, 0), new Vector3(0.18f, 2.4f, 0.18f));
            Bar(new Vector3(0, 2.4f, 0), new Vector3(7.4f, 0.18f, 0.18f));
        }

        // =====================================================================
        // ของตกแต่ง + ซุ้มประตู
        // =====================================================================
        static void PropsAll()
        {
            // ซุ้มประตูทางเข้าหลัก (ตะวันออกเฉียงเหนือ) — เสาครีม 2 ต้น + คานทอง
            var gd = (RingPoint(GateAngle) - RingC).normalized;
            var gp = RingPoint(GateAngle, RingR + RingW * 0.5f + 5f);
            var side = new Vector2(-gd.y, gd.x);
            var gate = new GameObject("MainGate"); gate.transform.SetParent(gProps, false);
            void Box(string n, Vector2 at, float y, Vector3 size, Material m, float yawDeg)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = n; b.transform.SetParent(gate.transform, false);
                b.transform.localPosition = new Vector3(at.x, y, at.y);
                b.transform.localRotation = Quaternion.Euler(0, yawDeg, 0);
                b.transform.localScale = size;
                b.GetComponent<Renderer>().sharedMaterial = m;
                b.isStatic = true;
            }
            float yaw = YawAlong(side);
            Box("Pillar_L", gp + side * 7.5f, 3.5f, new Vector3(2.2f, 7f, 2.2f), MPillar, yaw);
            Box("Pillar_R", gp - side * 7.5f, 3.5f, new Vector3(2.2f, 7f, 2.2f), MPillar, yaw);
            Box("Beam", gp, 7.4f, new Vector3(0.9f, 1.4f, 17.5f), MGold, yaw + 90);
            Box("Beam_Top", gp, 8.4f, new Vector3(0.6f, 0.6f, 12f), MPillar, yaw + 90);
            Blocks.Add(new Vector3(gp.x + side.x * 7.5f, gp.y + side.y * 7.5f, 2.5f));
            Blocks.Add(new Vector3(gp.x - side.x * 7.5f, gp.y - side.y * 7.5f, 2.5f));
            Put("Prop_Sign_Entrance_01", gProps, new Vector3(gp.x + side.x * 11f, 0, gp.y + side.y * 11f), YawAlong(-gd));

            // ไฟทางรอบวงแหวน (ฝั่งใน)
            HwRingT(out float tNW, out float tSE);
            for (float a = 0; a < 360; a += 18)
            {
                var p = RingPoint(a, RingR - RingW * 0.5f - 1.3f);
                if (DistToHw(p) < HwW) continue;
                var toC = (RingC - p).normalized;
                Put("Prop_LightPole_Base_01", gProps, new Vector3(p.x, 0, p.y), YawAlong(toC) + 90);
            }
            // ไฟจราจรที่จุดตัดฝั่งตะวันตกเฉียงเหนือ + ป้ายรถเมล์ริมถนนทแยง
            var nw = Hw(tNW); var hn = new Vector2(-HwD.y, HwD.x);
            Put("Prop_TrafficLight_01", gProps, new Vector3(nw.x + hn.x * 8f + HwD.x * 8f, 0, nw.y + hn.y * 8f + HwD.y * 8f), YawAlong(HwD));
            Put("Prop_TrafficLight_01", gProps, new Vector3(nw.x - hn.x * 8f - HwD.x * 8f, 0, nw.y - hn.y * 8f - HwD.y * 8f), YawAlong(-HwD));
            var bs = Hw(128f) - hn * (HwW * 0.5f + 2f);
            Put("Prop_BusStop_01", gProps, new Vector3(bs.x, 0, bs.y), YawAlong(HwD) - 90);
            Blocks.Add(new Vector3(bs.x, bs.y, 3f));

            // รถบนถนนทแยง / วงแหวน
            float hyaw = YawAlong(HwD);
            Put("Veh_Car_Sedan_01", gProps, V3(Hw(40f) - hn * 3f), hyaw);
            Put("Veh_Car_Taxi_01", gProps, V3(Hw(215f) + hn * 3f), hyaw + 180);
            Put("Veh_Car_Small_01", gProps, V3(Hw(300f) - hn * 3f), hyaw);
            var rc = RingPoint(200f, RingR - 2.2f); Put("Veh_Car_Small_01", gProps, V3(rc), YawAlong(new Vector2(Mathf.Sin(200 * Mathf.Deg2Rad), -Mathf.Cos(200 * Mathf.Deg2Rad))));

            // ตลาดน้อย: โต๊ะปิกนิก + ร่ม + รถเข็น
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    var pos = new Vector3(23 + j * 7, 0, 110 + i * 5.5f);
                    Put("Prop_PicnicTable_01", gProps, pos, 90);
                    Put("Prop_Umbrella_01", gProps, pos, 0);
                }
            Put("Prop_HotdogStand_01", gProps, new Vector3(-6, 0, 108.5f), 0);
            // MSU Plaza: ATM ตู้กดน้ำ
            Put("Prop_ATM_01", gProps, new Vector3(-50, 0, 109.5f), 180);
            Put("Prop_Soda_01", gProps, new Vector3(-30, 0, 109.5f), 180);
            // ราวตากผ้าหลังหอพัก
            Put("Prop_Washingline_01", gProps, new Vector3(-72, 0, 119), 0);
            // ม้านั่งริมสระ
            foreach (var p in new[] { new Vector3(-28, 39, 0), new Vector3(-60, 30, -90), new Vector3(52, 6, 90) })
                Put("Prop_ParkBench_01", gProps, new Vector3(p.x, 0, p.y), p.z + 180);
            // ถังขยะรอบลาน
            foreach (var p in new[] { new Vector2(-6, 14), new Vector2(6, -14), new Vector2(14, 6), new Vector2(-14, -6) })
                Put("Prop_Trashbin_01", gProps, new Vector3(p.x, 0, p.y), 0);
        }

        static Vector3 V3(Vector2 p) => new Vector3(p.x, 0, p.y);

        // =====================================================================
        // ต้นไม้ (หลบถนน/ตึก/ทางเดิน/น้ำ)
        // =====================================================================
        static bool Free(Vector2 p, float pad)
        {
            if (p.x < MinX + 4 || p.x > MaxX - 4 || p.y < MinZ + 4 || p.y > MaxZ - 4) return false;
            if (DistToHw(p) < HwW * 0.5f + 2.5f + pad) return false;
            float dr = Mathf.Abs(Vector2.Distance(p, RingC) - RingR);
            if (dr < RingW * 0.5f + 2.5f + pad) return false;
            if (Mathf.Abs(Vector2.Distance(p, RingC) - CanalR) < CanalW * 0.5f + 1.5f) return false;
            foreach (var b in Blocks) if (Vector2.Distance(p, new Vector2(b.x, b.y)) < b.z + pad) return false;
            foreach (var w in Walks) if (DistSeg(p, new Vector2(w.x, w.y), new Vector2(w.z, w.w)) < 3.2f + pad) return false;
            // ถนนเส้นอื่น
            var gd = (RingPoint(GateAngle) - RingC).normalized; var g0 = RingPoint(GateAngle, RingR);
            if (DistSeg(p, g0, g0 + gd * 60f) < 7.5f + pad) return false;
            if (p.y > 95 && p.y < 111 && p.x > -116 && p.x < 36) return false;          // ถนน + ทางเท้าหอพัก
            if (Mathf.Abs(p.x - (-11)) < 6f && p.y > 85 && p.y < 106) return false;      // ถนนขึ้นหอ
            if (p.x > -13 - 14 && p.x < 15 + 14 && p.y > -15 - 2 && p.y < 15 + 2 && Mathf.Abs(p.x) < 17 && Mathf.Abs(p.y) < 17) return false;
            return true;
        }

        static void TreesAll()
        {
            string[] tr = { "Env_Tree_01", "Env_Tree_02", "Env_Tree_03" };
            var rnd = new System.Random(2512);
            var placed = new List<Vector2>();
            int n = 0;
            void Tree(Vector2 p, float s)
            {
                foreach (var q in placed) if (Vector2.Distance(p, q) < 4.5f) return;
                var g = Put(tr[n % tr.Length], gTrees, new Vector3(p.x, 0, p.y), (n * 67) % 360);
                if (g) g.transform.localScale = Vector3.one * s;
                placed.Add(p); n++;
            }
            // แนวต้นไม้สองข้างถนนวงแหวน
            for (float a = 0; a < 360; a += 7.5f)
            {
                var pin = RingPoint(a + 3.7f, RingR - RingW * 0.5f - 4.5f);
                var pout = RingPoint(a, RingR + RingW * 0.5f + 4.5f);
                if (Free(pin, 0)) Tree(pin, 1.6f);
                if (Free(pout, 0)) Tree(pout, 1.7f);
            }
            // ป่าเส้นทางศึกษาธรรมชาติ (หนาแน่น)
            for (int i = 0; i < 60; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rnd.NextDouble());
                var p = new Vector2(2f, -86f) + new Vector2(Mathf.Cos(a) * 30f * r, Mathf.Sin(a) * 12f * r);
                if (Free(p, 0.5f)) Tree(p, 1.5f + (float)rnd.NextDouble() * 0.6f);
            }
            // กระจายทั่วมหาวิทยาลัย
            for (float x = MinX + 8; x < MaxX - 8; x += 9)
                for (float z = MinZ + 8; z < MaxZ - 8; z += 9)
                {
                    if (rnd.NextDouble() > 0.33) continue;
                    var p = new Vector2(x + (float)rnd.NextDouble() * 5f - 2.5f, z + (float)rnd.NextDouble() * 5f - 2.5f);
                    if (Free(p, 1.2f)) Tree(p, 1.4f + (float)rnd.NextDouble() * 0.6f);
                }
            // พุ่มดอกไม้รอบลาน (เว้นทางออกกลางลาน — ดอกไม้มี collider ขวางทางเดินได้)
            for (int i = -2; i <= 2; i += 4)
            {
                Put("Env_Flower_01", gTrees, new Vector3(i * 2.5f, 0, 16.5f), 0);
                Put("Env_Flower_01", gTrees, new Vector3(16.5f, 0, i * 2.5f), 90);
                Put("Env_Flower_01", gTrees, new Vector3(i * 2.5f, 0, -16.5f), 0);
                Put("Env_Flower_01", gTrees, new Vector3(-16.5f, 0, i * 2.5f + 0.5f), 90);
            }
        }

        // =====================================================================
        // กำแพงล่องหนรอบแมพ
        // =====================================================================
        static void BoundaryAll()
        {
            float cx = (MinX + MaxX) * 0.5f, cz = (MinZ + MaxZ) * 0.5f, w = MaxX - MinX, d = MaxZ - MinZ;
            Wall("Boundary_N", new Vector3(cx, 2, MaxZ), new Vector3(w + 4, 4, 1));
            Wall("Boundary_S", new Vector3(cx, 2, MinZ), new Vector3(w + 4, 4, 1));
            Wall("Boundary_W", new Vector3(MinX, 2, cz), new Vector3(1, 4, d + 4));
            Wall("Boundary_E", new Vector3(MaxX, 2, cz), new Vector3(1, 4, d + 4));
        }

        static void Wall(string n, Vector3 pos, Vector3 size)
        {
            var w = new GameObject(n); w.transform.SetParent(gFence, false);
            w.transform.localPosition = pos;
            w.AddComponent<BoxCollider>().size = size;
            w.isStatic = true;
        }

        // =====================================================================
        // ป้ายชื่อสถานที่ (โคลนป้ายคณะเดิม → ฟอนต์ไทย/สไตล์เดียวกัน)
        // =====================================================================
        static Vector3 W(Vector2 local, float y = 0) { return root.TransformPoint(new Vector3(local.x, y, local.y)); }

        static void Label(string text, Vector2 at, float y, Color? bg = null)
        {
            var tpl = GameObject.Find("FacultySigns/Sign_คณะ IT");
            if (tpl == null) return;
            var g = Object.Instantiate(tpl, gLabels);
            g.name = "Label_" + text;
            g.transform.position = W(at, y);
            g.transform.rotation = tpl.transform.rotation;
            var rt = (RectTransform)g.transform;
            rt.sizeDelta = new Vector2(Mathf.Max(360f, 30f * text.Length + 60f), 110f);
            var tmp = g.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) { tmp.text = text; tmp.enableAutoSizing = true; tmp.fontSizeMin = 18; tmp.fontSizeMax = 54; }
            if (bg.HasValue)
            {
                var img = g.transform.Find("BG"); if (img != null && img.TryGetComponent<UnityEngine.UI.Image>(out var im)) im.color = bg.Value;
            }
        }

        static void LabelOn(string key, string text)
        {
            if (!Buildings.TryGetValue(key, out var b)) return;
            Label(text, b.center, b.top + 3.2f);
        }

        static void LabelsAll()
        {
            var place = new Color(0.20f, 0.36f, 0.62f, 0.95f);
            var land = new Color(0.22f, 0.50f, 0.34f, 0.95f);
            LabelOn("ห้องสมุด", "ตึก A สำนักวิทยบริการ (ห้องสมุด)");
            LabelOn("ตึก B", "ตึก B สำนักคอมพิวเตอร์");
            LabelOn("ห้องสอบ", "ตึก C · ห้องสอบ");
            LabelOn("ตึก D", "ตึก D วิทยาลัยการเมืองการปกครอง");
            LabelOn("อาคารเรียน", "สำนักศึกษาทั่วไป (อาคารเรียน)");
            LabelOn("อาคารบริหาร", "สำนักงานอธิการบดี (อาคารบริหาร)");
            LabelOn("กองทะเบียน", "กองทะเบียนและประมวลผล");
            LabelOn("คณะพยาบาลศาสตร์", "คณะพยาบาลศาสตร์");
            LabelOn("คณะเภสัชศาสตร์", "คณะเภสัชศาสตร์");
            LabelOn("หอพัก", "หอพักนิสิต");
            LabelOn("ร้านค้า", "MSU Plaza (ร้านค้า)");
            LabelOn("โรงอาหาร", "ตลาดน้อย (โรงอาหาร)");
            LabelOn("อาคารชมรม", "กองกิจการนิสิต (ชมรม)");
            LabelOn("อาคารพลศึกษา", "อาคารพลศึกษา");
            Label("ลานอัฐศิลป์", new Vector2(0, 0), 4.2f, land);
            Label("สนามฟุตบอล", new Vector2(-136, 104), 4f, land);
            Label("สระว่ายน้ำ", new Vector2(-100, 120), 4f, land);
            Label("เส้นทางศึกษาธรรมชาติ", new Vector2(2, -86), 5f, land);
            Label("อาคารชุดที่พักบุคลากร", new Vector2(100, 39), 10f);
            var gp = RingPoint(GateAngle, RingR + RingW * 0.5f + 5f);
            Label("มหาวิทยาลัยมหาสารคาม · ประตูทางเข้าหลัก", gp, 11f, new Color(0.62f, 0.14f, 0.18f, 0.97f));
            Label("ถนนขามเรียง–ท่าขอนยาง ▸ เข้าเมืองมหาสารคาม", Hw(250f) - new Vector2(-HwD.y, HwD.x) * 10f, 3.5f, place);
            Label("◂ ไป จ.ขอนแก่น", Hw(20f) + new Vector2(-HwD.y, HwD.x) * 10f, 3.5f, place);
        }

        // =====================================================================
        // ย้ายระบบเดิมมาที่ตึกใหม่ (ชื่อวัตถุเดิมทั้งหมด)
        // =====================================================================
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

        static string RelinkSystems()
        {
            var log = new System.Text.StringBuilder();

            // 1) ประตูอาคาร (ชื่อเดิม → ห้องภายในเดิม)
            foreach (var key in new[] { "อาคารเรียน", "ห้องสมุด", "โรงอาหาร", "หอพัก", "อาคารบริหาร", "อาคารชมรม", "ร้านค้า", "คณะ IT" })
            {
                var door = GameObject.Find("BuildingDoors/Door_" + key);
                if (door == null) { log.AppendLine("• ไม่พบ Door_" + key); continue; }
                door.transform.position = DoorPoint(key, 0.9f);
                door.transform.rotation = Quaternion.Euler(0, Buildings[key].yaw + 180, 0);
                var bc = door.GetComponent<BoxCollider>();
                if (bc != null) { bc.size = new Vector3(2.6f, 2.2f, 1.6f); bc.center = new Vector3(0, 1.1f, 0); bc.isTrigger = true; }
                log.AppendLine("✓ Door_" + key);
            }

            // 2) สถานีภายนอก
            Move("ExamPoint", DoorPoint("ห้องสอบ", 0.9f));
            Move("Job_Cafe", DoorPoint("โรงอาหาร", 0.9f, 7f));
            Move("Job_Library", DoorPoint("ห้องสมุด", 0.9f, 6f));
            var exam = GameObject.Find("ExamPoint");
            if (exam != null) { var eb = exam.GetComponent<BoxCollider>(); if (eb) { eb.size = new Vector3(2.6f, 2.2f, 1.6f); eb.center = new Vector3(0, 1.1f, 0); } }

            // 3) ป้ายคณะเดิม → ตึกคณะที่ตรงกับของจริง
            var signMap = new Dictionary<string, string> {
                { "Sign_คณะ IT", "คณะ IT" }, { "Sign_คณะบริหารธุรกิจ", "คณะการบัญชี" },
                { "Sign_คณะวิทยาศาสตร์", "คณะวิทยาศาสตร์" }, { "Sign_คณะนิเทศศาสตร์", "คณะมนุษยศาสตร์" } };
            foreach (var kv in signMap)
            {
                var b = Buildings[kv.Value];
                Move("FacultySigns/" + kv.Key, W(b.center, b.top + 3.2f));
            }

            // 4) ที่นั่งภายนอก = ม้านั่งรอบลานอัฐศิลป์
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

            // 5) NPC ยืนประจำจุด (หันออกจากตึก)
            void NPC(string name, string key, float extra, float side)
            {
                var g = GameObject.Find("TalkNPCs/" + name); if (g == null) return;
                var pos = DoorPoint(key, extra, side);
                g.transform.position = new Vector3(pos.x, g.transform.position.y, pos.z);
                g.transform.rotation = Quaternion.Euler(0, Buildings[key].yaw + 180, 0);
            }
            NPC("NPC_รุ่นพี่ปี 4", "ห้องสอบ", 2.2f, -3f);
            NPC("NPC_เพื่อนร่วมคณะ", "คณะ IT", 3f, 4f);
            NPC("NPC_อาจารย์ที่ปรึกษา", "อาคารบริหาร", 2.5f, 3f);
            NPC("NPC_อาจารย์บรรณารักษ์", "ห้องสมุด", 2.5f, -3.5f);
            NPC("NPC_แม่ค้าร้านค้า", "ร้านค้า", 2.2f, -4f);

            // 6) เส้นทางเดินของ NPC / สัตว์
            Vector2[][] npcRoutes =
            {
                new[] { new Vector2(-10, -10), new Vector2(10, -10), new Vector2(10, 10) },           // รอบลาน
                new[] { new Vector2(-13, 20), new Vector2(-13, 80), new Vector2(-30, 103) },           // ลาน → หอพัก
                new[] { new Vector2(28, 14), new Vector2(28, 40), new Vector2(60, 52) },               // คณะ IT → ประตูหลัก
            };
            string[] walkers = { "NPC_นิสิตปี 1", "NPC_นิสิตปี 3", "NPC_รุ่นพี่ใกล้จบ" };
            for (int r = 0; r < 3; r++) PlaceRoute("TalkNPCs/Route" + r, npcRoutes[r], "TalkNPCs/" + walkers[r]);

            Vector2[][] animalRoutes =
            {
                new[] { new Vector2(16, 20), new Vector2(24, 22), new Vector2(22, 30), new Vector2(14, 28) },     // สนามหญ้าข้างห้องสมุด
                new[] { new Vector2(-15, -86), new Vector2(-6, -78), new Vector2(-2, -94), new Vector2(-18, -93) }, // เส้นทางธรรมชาติ
                new[] { new Vector2(25, -78), new Vector2(32, -86), new Vector2(28, -94), new Vector2(21, -91) },
                new[] { new Vector2(-78, 50), new Vector2(-66, 60), new Vector2(-64, 50), new Vector2(-80, 46) },   // ริมสระใหญ่
                new[] { new Vector2(-28, -86), new Vector2(-20, -76), new Vector2(-12, -98), new Vector2(-22, -96) },
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

            // 7) ผู้เล่นเริ่มที่ลานอัฐศิลป์ หันขึ้นเหนือ (ห้องสมุด)
            var player = GameObject.Find("Player");
            if (player != null) { player.transform.position = W(new Vector2(0, -8), 0) + Vector3.up * 0.66f; player.transform.rotation = Quaternion.identity; }

            // 8) Minimap ให้เห็นกว้างขึ้น (แมพใหญ่ขึ้น)
            var mm = GameObject.Find("MinimapCamera");
            if (mm != null) { var cam = mm.GetComponent<Camera>(); if (cam != null && cam.orthographic) cam.orthographicSize = Mathf.Max(cam.orthographicSize, 26f); }

            return log.ToString();
        }

        static void PlaceRoute(string routePath, Vector2[] pts, string walkerPath)
        {
            var route = GameObject.Find(routePath);
            if (route == null) return;
            route.transform.position = Vector3.zero;
            for (int i = 0; i < route.transform.childCount; i++)
                route.transform.GetChild(i).position = W(pts[i % pts.Length]);
            var w = GameObject.Find(walkerPath);
            if (w != null) w.transform.position = new Vector3(W(pts[0]).x, w.transform.position.y, W(pts[0]).z);
        }

        // =====================================================================
        // ตรวจ: ตึกทับกัน / ทับถนน
        // =====================================================================
        static string Verify()
        {
            var sb = new System.Text.StringBuilder();
            int issues = 0;
            var list = new List<Bld>(Buildings.Values);
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                float hw = DistToHw(a.center) - a.radius - HwW * 0.5f;
                float rg = Mathf.Abs(Vector2.Distance(a.center, RingC) - RingR) - a.radius - RingW * 0.5f;
                if (hw < 0) { sb.AppendLine($"⚠ {a.key} ทับถนนทแยง ({hw:0.0})"); issues++; }
                if (rg < 0) { sb.AppendLine($"⚠ {a.key} ทับถนนวงแหวน ({rg:0.0})"); issues++; }
                for (int j = i + 1; j < list.Count; j++)
                {
                    var b = list[j];
                    float gap = Vector2.Distance(a.center, b.center) - a.radius - b.radius;
                    if (gap < -3f) { sb.AppendLine($"⚠ {a.key} ชิด {b.key} ({gap:0.0})"); issues++; }
                }
            }
            sb.AppendLine(issues == 0 ? "✓ ตรวจผัง: ไม่มีตึกทับถนน/ทับกัน" : $"ตรวจผัง: {issues} จุดควรดู");
            return sb.ToString();
        }
    }
}
#endif
