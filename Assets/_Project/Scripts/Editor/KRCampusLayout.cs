#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

namespace NisitSimulator.EditorTools
{
    // 🗺 ผังมหาวิทยาลัยมหาสารคาม เขตพื้นที่ขามเรียง (v2 — อ้างอิงภาพดาวเทียม + แผนผัง สภานิสิต + แผนผังหมายเลข)
    //   • ภาพดาวเทียม (ทิศเหนือขึ้นบน) = รูปทรงถนนวงรอบ, ถนน 2202 แนวเหนือ–ใต้, สระน้ำ, สัดส่วนพื้นที่
    //   • แผนผังสภานิสิต/แผนผังหมายเลข วาดหันกลับด้าน ~180° เทียบกับภาพดาวเทียม (แนวถนนโค้งด้านซ้ายของแผนผัง = วงรอบฝั่งตะวันออก)
    //     → ใช้เฉพาะตำแหน่งสัมพันธ์ของอาคาร (ประมาณ)
    //   • หน่วย = เมตรโลกเกม · +Z = ทิศเหนือ · จุดศูนย์วงรอบ = (0,0) · 1 px ดาวเทียม ≈ 0.714 ม.
    //   • อาคารเดิม (ประตูวาร์ป/ภายในจริง) ถูกย้ายทั้งก้อนพร้อมประตู จุดงาน NPC — ไม่สร้างซ้ำ
    //   ใช้: เมนู Nisit → KR Layout → Rebuild From Backup (คืนฉากจากสำรองแล้วจัดผังใหม่ทั้งหมด)
    public static class KRCampusLayout
    {
        public const string ScenePath = "Assets/_Project/Scenes/01_Gameplay.unity";
        public const string BackupPath = "Assets/_Project/Scenes/Backups/01_Gameplay_BeforeKRLayout_20261001.unity";
        const string MatDir = "Assets/_Project/Materials/MSU";
        const string PrefabDir = "Assets/_Project/Prefabs/KRCampus";
        const string SyntyRoot = "Assets/Synty/PolygonCity/Prefabs/";

        // ----- โครงหลัก -----
        public const float RingR = 105f, RingW = 9f;            // ถนนวงรอบ (ศูนย์กลาง 0,0)
        public const float RoadX = -16f, RoadW = 12f;            // ทล.2202 แนวเหนือ–ใต้
        public const float SubW = 7f, WalkW = 2.5f;
        public const float RoadNZ = 25f, RoadSZ = -38f, RoadEX = 58f, DormZ = 118f, SportX = 40f;
        public static readonly Vector2 PlazaC = new Vector2(22f, -6f);
        public const float MinX = -150f, MaxX = 150f, MinZ = -150f, MaxZ = 175f;

        static Transform root, gLabels;
        static readonly Dictionary<string, Transform> Zones = new Dictionary<string, Transform>();
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static readonly List<Vector4> Rects = new List<Vector4>();        // สิ่งกีดขวาง (minx,minz,maxx,maxz)
        static readonly List<Vector4> Segs = new List<Vector4>();         // ถนน/ทางเดิน (ax,az,bx,bz)
        static readonly List<float> SegHalf = new List<float>();
        static readonly List<Vector4> Ponds = new List<Vector4>();        // (cx,cz,rx,rz)
        public static readonly Dictionary<string, Bld> Buildings = new Dictionary<string, Bld>();
        static StringBuilder log;

        public struct Bld { public string key, label; public Bounds b; public float yaw; public Vector3 front; public bool moved; }

        // =====================================================================
        [MenuItem("Nisit/KR Layout/Rebuild From Backup (ผัง มมส ขามเรียง v2)")]
        public static void RebuildMenu()
        {
            if (!EditorUtility.DisplayDialog("Nisit", "คืน 01_Gameplay จากไฟล์สำรองก่อนจัดผัง แล้วจัดผังใหม่ทั้งหมด?", "ตกลง", "ยกเลิก")) return;
            Debug.Log(RebuildFromBackup());
        }

        public static string RebuildFromBackup()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) == null) return "ไม่พบไฟล์สำรอง " + BackupPath;
            EditorSceneManager.OpenScene(BackupPath, OpenSceneMode.Single);   // เปิดสำรอง → บันทึกทับฉากจริง
            var s = EditorSceneManager.GetActiveScene();
            string res = Build();
            EditorSceneManager.SaveScene(s, ScenePath, false);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return res;
        }

        public static string Build()
        {
            log = new StringBuilder();
            Zones.Clear(); Mats.Clear(); Rects.Clear(); Segs.Clear(); SegHalf.Clear(); Ponds.Clear(); Buildings.Clear();
            var old = GameObject.Find("KR_Campus"); if (old) Object.DestroyImmediate(old);
            var oldL = GameObject.Find("KR_Labels"); if (oldL) Object.DestroyImmediate(oldL);

            root = new GameObject("KR_Campus").transform;
            gLabels = new GameObject("KR_Labels").transform;
            foreach (var z in new[] { "Infra_Roads", "Infra_Walks", "Zone_Center", "Zone_NorthGE", "Zone_East", "Zone_SouthEast",
                                      "Zone_West", "Zone_SW_Forest", "Zone_DormNW", "Zone_SportsN", "Zone_Outer", "Water", "Boundary" })
            {
                var t = new GameObject(z).transform; t.SetParent(root, false); Zones[z] = t;
            }

            EnsurePrefabs();
            Ground();
            Roads();
            WaterAll();
            MoveExisting();
            NewFaculties();
            WalksAll();
            SportsAll();
            Parking();
            PropsAll();
            TreesAll();
            BoundaryAll();
            RelinkSystems();
            LabelsAll();
            DisableOld();
            StaticFlags();
            log.AppendLine(Verify());
            Debug.Log("<color=lime>[Nisit] จัดผัง มมส ขามเรียง v2 แล้ว</color>\n" + log);
            return log.ToString();
        }

        static Transform Z(string n) => Zones[n];

        // =====================================================================
        // วัสดุ / mesh
        // =====================================================================
        static Material Mat(string name, Color c, float smooth = 0.1f)
        {
            if (Mats.TryGetValue(name, out var m) && m != null) return m;
            string path = MatDir + "/MSU_" + name + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "MSU");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
                m.enableInstancing = true;
                AssetDatabase.CreateAsset(m, path);
            }
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
        static Material MPlazaGreen => Mat("PlazaGreen", new Color(0.40f, 0.58f, 0.34f), 0.05f);
        static Material MPillar => Mat("GatePillar", new Color(0.93f, 0.88f, 0.76f), 0.2f);
        static Material MGold => Mat("GateGold", new Color(0.86f, 0.70f, 0.30f), 0.5f);
        static Material MCream => Mat("KR_WallCream", new Color(0.90f, 0.86f, 0.78f), 0.1f);
        static Material MBrick => Mat("KR_WallBrick", new Color(0.70f, 0.40f, 0.30f), 0.1f);
        static Material MRoof => Mat("KR_RoofTerracotta", new Color(0.78f, 0.45f, 0.33f), 0.1f);
        static Material MGlass => Mat("KR_WindowDark", new Color(0.20f, 0.28f, 0.34f), 0.7f);
        static Material MBase => Mat("KR_Plinth", new Color(0.58f, 0.57f, 0.55f), 0.1f);
        static Material MParking => Mat("KR_Parking", new Color(0.33f, 0.34f, 0.35f), 0.1f);
        static Material MCourt => Mat("KR_Court", new Color(0.25f, 0.48f, 0.55f), 0.2f);

        static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat)
        {
            var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            g.transform.SetParent(parent, false);
            mesh.name = name; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            g.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

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

        static GameObject Strip(string name, Transform parent, Vector2 a, Vector2 b, float w, float y, Material mat)
        {
            var dir = (b - a).normalized; var n = new Vector2(-dir.y, dir.x) * (w * 0.5f);
            var m = new Mesh();
            m.vertices = new[] { new Vector3(a.x - n.x, y, a.y - n.y), new Vector3(a.x + n.x, y, a.y + n.y), new Vector3(b.x + n.x, y, b.y + n.y), new Vector3(b.x - n.x, y, b.y - n.y) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            FixWinding(m);
            return MeshObj(name, parent, m, mat);
        }

        static GameObject Rect(string name, Transform parent, Vector2 c, float w, float d, float y, Material mat)
            => Strip(name, parent, new Vector2(c.x, c.y - d * 0.5f), new Vector2(c.x, c.y + d * 0.5f), w, y, mat);

        static GameObject Arc(string name, Transform parent, Vector2 c, float r, float w, float a0, float a1, float y, Material mat)
        {
            int seg = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 2f));
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, i / (float)seg);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var pi = c + dir * (r - w * 0.5f); var po = c + dir * (r + w * 0.5f);
                v.Add(new Vector3(pi.x, y, pi.y)); v.Add(new Vector3(po.x, y, po.y));
                if (i > 0) { int k = i * 2; t.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 }); }
            }
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); FixWinding(m);
            return MeshObj(name, parent, m, mat);
        }

        static GameObject EllipseRing(string name, Transform parent, Vector2 c, float rx, float rz, float w, float y, Material mat)
        {
            const int seg = 64; var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f; float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                v.Add(new Vector3(c.x + cs * (rx - w), y, c.y + sn * (rz - w))); v.Add(new Vector3(c.x + cs * rx, y, c.y + sn * rz));
                if (i > 0) { int k = i * 2; t.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 }); }
            }
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); FixWinding(m);
            return MeshObj(name, parent, m, mat);
        }

        static GameObject Ellipse(string name, Transform parent, Vector2 c, float rx, float rz, float y, Material mat, int seg = 40, float rotDeg = 0f)
        {
            var v = new List<Vector3> { new Vector3(c.x, y, c.y) }; var t = new List<int>();
            float ca = Mathf.Cos(rotDeg * Mathf.Deg2Rad), sa = Mathf.Sin(rotDeg * Mathf.Deg2Rad);
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f; float x = Mathf.Cos(a) * rx, z = Mathf.Sin(a) * rz;
                v.Add(new Vector3(c.x + x * ca - z * sa, y, c.y + x * sa + z * ca));
                if (i > 0) t.AddRange(new[] { 0, i + 1, i });
            }
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); FixWinding(m);
            return MeshObj(name, parent, m, mat);
        }

        static void WaterCollider(string name, Transform parent, Vector2 c, float rx, float rz)
        {
            var g = new GameObject(name + "_Collider"); g.transform.SetParent(parent, false);
            var v = new List<Vector3>(); var t = new List<int>(); const int seg = 20;
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float x = c.x + Mathf.Cos(a) * rx * 0.92f, z = c.y + Mathf.Sin(a) * rz * 0.92f;
                v.Add(new Vector3(x, -0.5f, z)); v.Add(new Vector3(x, 2.5f, z));
            }
            for (int i = 0; i < seg; i++) { int a = i * 2, b = ((i + 1) % seg) * 2; t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 }); }
            var m = new Mesh { name = name + "_Hull" }; m.SetVertices(v); m.SetTriangles(t, 0);
            var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = m; mc.convex = true;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, float yaw = 0f, bool collider = false)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name; b.transform.SetParent(parent, false);
            b.transform.localPosition = pos; b.transform.localRotation = Quaternion.Euler(0, yaw, 0); b.transform.localScale = size;
            b.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(b.GetComponent<Collider>());
            return b;
        }

        // =====================================================================
        // Synty / prefab helpers
        // =====================================================================
        static GameObject LoadSynty(string name)
        {
            foreach (var cat in new[] { "Props/", "Environments/", "Vehicles/", "Buildings/" })
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(SyntyRoot + cat + "SM_" + name + ".prefab");
                if (go != null) return go;
            }
            log.AppendLine("⚠ ไม่พบ Synty prefab: " + name);
            return null;
        }

        static GameObject Inst(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale = 1f)
        {
            if (prefab == null) return null;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            g.transform.position = pos; g.transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (scale != 1f) g.transform.localScale = Vector3.one * scale;
            return g;
        }
        // วางของ Synty บนพื้น (บาง prefab มี pivot กลางวัตถุ เช่น ATM → ยกให้ฐานอยู่ที่ y=0 ไม่จมดิน)
        static GameObject Put(string synty, Transform parent, Vector2 p, float yaw, float scale = 1f)
        {
            var g = Inst(LoadSynty(synty), parent, new Vector3(p.x, 0, p.y), yaw, scale);
            if (g != null) { var b = WB(g); if (b.min.y < -0.05f) g.transform.position += Vector3.up * -b.min.y; }
            return g;
        }

        static Bounds WB(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        // ทิศหน้าตึก: yaw 0 = หันไปใต้ (-Z) · 90 = ตะวันตก (-X) · -90 = ตะวันออก (+X) · 180 = เหนือ (+Z)
        static Vector3 FaceDir(float yaw) => Quaternion.Euler(0, yaw, 0) * Vector3.back;
        static Vector2 V2(Vector3 v) => new Vector2(v.x, v.z);
        static Vector3 V3(Vector2 v, float y = 0) => new Vector3(v.x, y, v.y);

        // ---------- prefab อาคารคณะแบบเรียบง่าย (LOD) + ต้นไม้ (LOD culling) ----------
        static readonly Dictionary<string, GameObject> Pf = new Dictionary<string, GameObject>();

        static void EnsurePrefabs()
        {
            Pf.Clear();
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "KRCampus");
            MakeFaculty("KR_Faculty_12x10_3F", 12f, 10f, 3, false);
            MakeFaculty("KR_Faculty_12x10_4F", 12f, 10f, 4, true);
            MakeFaculty("KR_Faculty_10x8_3F", 10f, 8f, 3, true);
            MakeFaculty("KR_Faculty_14x10_3F", 14f, 10f, 3, false);
            for (int i = 1; i <= 3; i++) MakeTree("KR_Tree_0" + i, "Env_Tree_0" + i);
        }

        // หน้าอาคาร = -Z ท้องถิ่น (yaw 0 หันใต้) · ชั้นละ 2.7 ม. (สเกลเดียวกับอาคาร GE/IT ×0.75)
        static void MakeFaculty(string name, float w, float d, int floors, bool brick)
        {
            string path = PrefabDir + "/" + name + ".prefab";
            var g = new GameObject(name);
            float fh = 2.7f, h = floors * fh + 0.3f;
            var lod0 = new GameObject("LOD0").transform; lod0.SetParent(g.transform, false);
            var lod1 = new GameObject("LOD1").transform; lod1.SetParent(g.transform, false);
            var wall = brick ? MBrick : MCream;
            // LOD0
            Box("Plinth", lod0, new Vector3(0, 0.15f, 0), new Vector3(w + 0.4f, 0.3f, d + 0.4f), MBase);
            Box("Body", lod0, new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), wall);
            Box("Roof", lod0, new Vector3(0, h + 0.2f, 0), new Vector3(w + 0.8f, 0.4f, d + 0.8f), MRoof);
            Box("RoofCap", lod0, new Vector3(0, h + 0.75f, 0), new Vector3(w * 0.6f, 0.7f, d * 0.5f), MRoof);
            for (int f = 0; f < floors; f++)
            {
                float y = 0.3f + f * fh + 1.45f;
                Box("WinF_" + f, lod0, new Vector3(0, y, -d * 0.5f - 0.03f), new Vector3(w * 0.84f, 0.95f, 0.06f), MGlass);
                Box("WinB_" + f, lod0, new Vector3(0, y, d * 0.5f + 0.03f), new Vector3(w * 0.84f, 0.95f, 0.06f), MGlass);
                Box("WinL_" + f, lod0, new Vector3(-w * 0.5f - 0.03f, y, 0), new Vector3(0.06f, 0.95f, d * 0.7f), MGlass);
                Box("WinR_" + f, lod0, new Vector3(w * 0.5f + 0.03f, y, 0), new Vector3(0.06f, 0.95f, d * 0.7f), MGlass);
            }
            // ทางเข้า: ช่องประตูทึบ (ตกแต่ง) + กันสาด + เสาครีบ
            Box("EntranceBay", lod0, new Vector3(0, h * 0.5f, -d * 0.5f - 0.35f), new Vector3(3.6f, h + 0.2f, 0.7f), brick ? MCream : MBrick);
            Box("EntranceDoor", lod0, new Vector3(0, 1.05f, -d * 0.5f - 0.72f), new Vector3(1.8f, 1.8f, 0.06f), MGlass);
            Box("Canopy", lod0, new Vector3(0, 2.25f, -d * 0.5f - 1.3f), new Vector3(3.8f, 0.15f, 1.9f), MBase);
            // LOD1 (กล่องเดียว + หลังคา) ใช้ mesh/วัสดุร่วมกัน
            Box("Body", lod1, new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), wall);
            Box("Roof", lod1, new Vector3(0, h + 0.2f, 0), new Vector3(w + 0.8f, 0.4f, d + 0.8f), MRoof);
            var bc = g.AddComponent<BoxCollider>(); bc.center = new Vector3(0, h * 0.5f, 0); bc.size = new Vector3(w, h, d);
            var lg = g.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(0.18f, lod0.GetComponentsInChildren<Renderer>()), new LOD(0.025f, lod1.GetComponentsInChildren<Renderer>()) });
            lg.RecalculateBounds();
            foreach (var r in g.GetComponentsInChildren<Renderer>()) if (r.name.StartsWith("Win") || r.name == "EntranceDoor" || r.name == "Canopy" || r.name == "RoofCap") r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
            foreach (Transform t in g.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            var pf = PrefabUtility.SaveAsPrefabAsset(g, path);
            Object.DestroyImmediate(g);
            Pf[name] = pf;
        }

        static void MakeTree(string name, string synty)
        {
            string path = PrefabDir + "/" + name + ".prefab";
            var src = LoadSynty(synty); if (src == null) return;
            var g = new GameObject(name);
            var t = (GameObject)PrefabUtility.InstantiatePrefab(src, g.transform);
            var lg = g.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(0.012f, t.GetComponentsInChildren<Renderer>()) });   // เล็กกว่า 1.2% ของจอ → ซ่อน
            lg.RecalculateBounds();
            var pf = PrefabUtility.SaveAsPrefabAsset(g, path);
            Object.DestroyImmediate(g);
            Pf[name] = pf;
        }

        // =====================================================================
        // สิ่งกีดขวาง (กันต้นไม้/ของตกแต่งทับถนน ทางเดิน อาคาร)
        // =====================================================================
        static void Block(Bounds b, float pad = 0f) => Rects.Add(new Vector4(b.min.x - pad, b.min.z - pad, b.max.x + pad, b.max.z + pad));
        static void BlockRect(Vector2 c, float w, float d) => Rects.Add(new Vector4(c.x - w / 2, c.y - d / 2, c.x + w / 2, c.y + d / 2));
        static void Seg(Vector2 a, Vector2 b, float half) { Segs.Add(new Vector4(a.x, a.y, b.x, b.y)); SegHalf.Add(half); }

        static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        public static bool Free(Vector2 p, float pad)
        {
            if (p.x < MinX + 4 || p.x > MaxX - 4 || p.y < MinZ + 4 || p.y > MaxZ - 4) return false;
            if (Mathf.Abs(p.x - RoadX) < RoadW * 0.5f + WalkW + 1.5f + pad) return false;
            if (Mathf.Abs(p.magnitude - RingR) < RingW * 0.5f + 1.8f + pad) return false;
            for (int i = 0; i < Rects.Count; i++) { var r = Rects[i]; if (p.x > r.x - pad && p.x < r.z + pad && p.y > r.y - pad && p.y < r.w + pad) return false; }
            for (int i = 0; i < Segs.Count; i++) { var s = Segs[i]; if (DistSeg(p, new Vector2(s.x, s.y), new Vector2(s.z, s.w)) < SegHalf[i] + 1.2f + pad) return false; }
            foreach (var w in Ponds) { var d = new Vector2((p.x - w.x) / (w.z + 2.5f + pad), (p.y - w.y) / (w.w + 2.5f + pad)); if (d.sqrMagnitude < 1f) return false; }
            return true;
        }

        // =====================================================================
        // พื้น
        // =====================================================================
        static void Ground()
        {
            GameObject g = null;
            foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (r.name == "Ground") g = r;
            if (g != null) { g.transform.position = Vector3.zero; g.transform.localScale = new Vector3(40, 1, 40); }   // 400 × 400 ม.
            // ป่าทึบฝั่งตะวันตกเฉียงใต้ในวงรอบ (ตามภาพดาวเทียม) + เส้นทางศึกษาธรรมชาติ
            Ellipse("Forest_SW", Z("Zone_SW_Forest"), new Vector2(-52f, -52f), 27f, 27f, 0.012f, MForest, 48);
            Ellipse("Forest_W", Z("Zone_SW_Forest"), new Vector2(-78f, -20f), 14f, 14f, 0.013f, MForest, 32);
            // สนามหญ้าโล่งฝั่งตะวันตกเฉียงเหนือ
            Ellipse("Lawn_NW", Z("Zone_West"), new Vector2(-62f, 62f), 22f, 14f, 0.011f, MField, 40, 30f);
        }

        // =====================================================================
        // ถนน
        // =====================================================================
        static void Road(string name, Transform parent, Vector2 a, Vector2 b, float w, float y, bool center, bool yellow = false)
        {
            Strip(name, parent, a, b, w, y, MAsphalt);
            var d = (b - a).normalized; var n = new Vector2(-d.y, d.x);
            Strip(name + "_EdgeL", parent, a + n * (w * 0.5f - 0.4f), b + n * (w * 0.5f - 0.4f), 0.2f, y + 0.008f, MLine);
            Strip(name + "_EdgeR", parent, a - n * (w * 0.5f - 0.4f), b - n * (w * 0.5f - 0.4f), 0.2f, y + 0.008f, MLine);
            if (center) Strip(name + "_Center", parent, a, b, yellow ? 0.4f : 0.2f, y + 0.01f, yellow ? MYellow : MLine);
            Seg(a, b, w * 0.5f);
        }

        static void Sidewalk(string name, Transform parent, Vector2 a, Vector2 b)
        {
            Strip(name, parent, a, b, WalkW, 0.026f, MWalk);
            Seg(a, b, WalkW * 0.5f);
        }

        // ถนนย่อยแนวราบ (ตะวันออก–ตะวันตก หรือ เหนือ–ใต้) + ทางเท้าสองข้าง
        static void SubRoad(string name, Vector2 a, Vector2 b, bool walks = true)
        {
            var t = Z("Infra_Roads");
            Road(name, t, a, b, SubW, 0.022f, true);
            if (!walks) return;
            var d = (b - a).normalized; var n = new Vector2(-d.y, d.x) * (SubW * 0.5f + WalkW * 0.5f);
            Sidewalk(name + "_WalkL", Z("Infra_Walks"), a + n, b + n);
            Sidewalk(name + "_WalkR", Z("Infra_Walks"), a - n, b - n);
        }

        // ทางเท้าเลียบ ทล.2202 ตัดช่วงที่ข้ามถนนวงรอบ/ถนนย่อย (ไม่ซ้อนบนผิวถนน)
        static void HwyWalk(string name, float x, float[] subZ)
        {
            float zi = Mathf.Sqrt(Mathf.Pow(RingR - RingW * 0.5f, 2) - x * x), zo = Mathf.Sqrt(Mathf.Pow(RingR + RingW * 0.5f, 2) - x * x);
            var cuts = new List<Vector2> { new Vector2(zi, zo), new Vector2(-zo, -zi) };
            foreach (var z in subZ) cuts.Add(new Vector2(z - SubW * 0.5f, z + SubW * 0.5f));
            cuts.Sort((a, b) => a.x.CompareTo(b.x));
            float z0 = MinZ + 6; int k = 0;
            foreach (var c in cuts) { if (c.x > z0) Sidewalk(name + "_" + (k++), Z("Infra_Walks"), new Vector2(x, z0), new Vector2(x, c.x)); z0 = Mathf.Max(z0, c.y); }
            if (MaxZ - 6 > z0) Sidewalk(name + "_" + k, Z("Infra_Walks"), new Vector2(x, z0), new Vector2(x, MaxZ - 6));
        }

        static float RingXAtZ(float z, float r) => Mathf.Sqrt(Mathf.Max(0f, r * r - z * z));

        static void Roads()
        {
            var t = Z("Infra_Roads");
            // ถนนวงรอบ (ทั้งวง) — ฝั่งตะวันตกคือแนว ทล.4069 ตามภาพดาวเทียม
            Arc("RingRoad", t, Vector2.zero, RingR, RingW, 0, 360, 0.02f, MAsphalt);
            Arc("RingRoad_EdgeIn", t, Vector2.zero, RingR - RingW * 0.5f + 0.4f, 0.2f, 0, 360, 0.028f, MLine);
            Arc("RingRoad_EdgeOut", t, Vector2.zero, RingR + RingW * 0.5f - 0.4f, 0.2f, 0, 360, 0.028f, MLine);
            Arc("RingRoad_Center", t, Vector2.zero, RingR, 0.2f, 0, 360, 0.03f, MLine);
            // ทางเท้าวงในรอบวง
            Arc("RingWalk_Inner", Z("Infra_Walks"), Vector2.zero, RingR - RingW * 0.5f - WalkW * 0.5f - 0.3f, WalkW, 0, 360, 0.026f, MWalk);

            // ทล.2202 (เหนือ–ใต้) สูงกว่าวงรอบเล็กน้อยกันพื้นซ้อน (ระยะ ≤ 4 ซม. ไม่เป็นขั้น)
            var a = new Vector2(RoadX, MinZ - 10); var b = new Vector2(RoadX, MaxZ + 10);
            Strip("Hwy2202", t, a, b, RoadW, 0.024f, MAsphalt);
            Strip("Hwy2202_Median", t, a, b, 0.5f, 0.036f, MYellow);
            Strip("Hwy2202_LaneL", t, a + new Vector2(-RoadW * 0.25f, 0), b + new Vector2(-RoadW * 0.25f, 0), 0.15f, 0.034f, MLine);
            Strip("Hwy2202_LaneR", t, a + new Vector2(RoadW * 0.25f, 0), b + new Vector2(RoadW * 0.25f, 0), 0.15f, 0.034f, MLine);
            Strip("Hwy2202_EdgeW", t, a + new Vector2(-RoadW * 0.5f + 0.4f, 0), b + new Vector2(-RoadW * 0.5f + 0.4f, 0), 0.2f, 0.034f, MLine);
            Strip("Hwy2202_EdgeE", t, a + new Vector2(RoadW * 0.5f - 0.4f, 0), b + new Vector2(RoadW * 0.5f - 0.4f, 0), 0.2f, 0.034f, MLine);
            float wx = RoadW * 0.5f + WalkW * 0.5f;
            HwyWalk("Hwy2202_WalkW", RoadX - wx, new[] { RoadNZ, DormZ });
            HwyWalk("Hwy2202_WalkE", RoadX + wx, new[] { RoadNZ, RoadSZ });

            float e = RoadX + RoadW * 0.5f, w = RoadX - RoadW * 0.5f;
            // ถนนย่อยเชื่อมกลุ่มอาคาร
            SubRoad("Road_CenterN_E", new Vector2(e, RoadNZ), new Vector2(RingXAtZ(RoadNZ, RingR - 2f), RoadNZ));
            SubRoad("Road_CenterN_W", new Vector2(w, RoadNZ), new Vector2(-RingXAtZ(RoadNZ, RingR - 2f), RoadNZ));
            SubRoad("Road_CenterS", new Vector2(e, RoadSZ), new Vector2(RingXAtZ(RoadSZ, RingR - 2f), RoadSZ));
            SubRoad("Road_East", new Vector2(RoadEX, RoadNZ - SubW * 0.5f), new Vector2(RoadEX, RoadSZ + SubW * 0.5f));
            // ถนนหอพัก (นอกวงรอบ ทิศตะวันตกเฉียงเหนือ)
            SubRoad("Road_Dorm", new Vector2(w, DormZ), new Vector2(-142f, DormZ));
            // ถนนเข้าพื้นที่กีฬา (นอกวงรอบ ทิศเหนือ)
            float sz0 = Mathf.Sqrt(RingR * RingR - SportX * SportX) + 2f;
            SubRoad("Road_Sports", new Vector2(SportX, sz0), new Vector2(SportX, 168f));

            // ทางม้าลาย
            Zebra(new Vector2(RoadX, RoadNZ + 5.5f), true, RoadW);
            Zebra(new Vector2(RoadX, RoadSZ - 5.5f), true, RoadW);
            Zebra(new Vector2(RoadX, 6f), true, RoadW);                 // ลานกลาง ↔ สำนักศึกษาทั่วไป
            Zebra(new Vector2(RoadX, DormZ - 5.5f), true, RoadW);
            Zebra(new Vector2(RoadX, -56f), true, RoadW);               // สนามฟุตบอล
            Zebra(new Vector2(RoadX, -68.5f), true, RoadW);
            Zebra(new Vector2(RoadX, Mathf.Sqrt(RingR * RingR - RoadX * RoadX) - 8f), true, RoadW);
            Zebra(new Vector2(RoadX, -Mathf.Sqrt(RingR * RingR - RoadX * RoadX) + 8f), true, RoadW);
            foreach (float x in new[] { PlazaC.x - 11f, PlazaC.x + 11f, 47.5f }) Zebra(new Vector2(x, RoadNZ), false, SubW);
            foreach (float x in new[] { PlazaC.x - 11f, PlazaC.x + 11f, 40.5f }) Zebra(new Vector2(x, RoadSZ), false, SubW);
            Zebra(new Vector2(RoadEX, -17f), true, SubW);
            Zebra(new Vector2(-56f, RoadNZ), false, SubW);
        }

        // vertical = ถนนแนวเหนือ–ใต้ (แถบลายขวางตามแกน X)
        static void Zebra(Vector2 c, bool vertical, float roadW)
        {
            var t = Z("Infra_Roads"); int n = 0;
            for (float o = -1.6f; o <= 1.61f; o += 0.8f)
            {
                if (vertical) Strip("Zebra", t, new Vector2(c.x - roadW * 0.45f, c.y + o), new Vector2(c.x + roadW * 0.45f, c.y + o), 0.45f, 0.042f, MLine);
                else Strip("Zebra", t, new Vector2(c.x + o, c.y - roadW * 0.45f), new Vector2(c.x + o, c.y + roadW * 0.45f), 0.45f, 0.042f, MLine);
                n++;
            }
        }

        // =====================================================================
        // น้ำ
        // =====================================================================
        static void Pond(string name, Transform parent, Vector2 c, float rx, float rz)
        {
            Ellipse(name + "_Edge", parent, c, rx + 1.2f, rz + 1.2f, 0.034f, MStone);
            Ellipse(name, parent, c, rx, rz, 0.05f, MWater);
            WaterCollider(name, parent, c, rx, rz);
            Ponds.Add(new Vector4(c.x, c.y, rx, rz));
        }

        static void WaterAll()
        {
            var w = Z("Water");
            // ตำแหน่งจากภาพดาวเทียม (แปลงพิกัดภาพ → เมตร) — ขนาดประมาณ
            Pond("Pond_North", w, new Vector2(9.3f, 77.8f), 12f, 12f);          // สระใหญ่เหนือกลุ่มอาคารกลาง ติด ทล.2202
            Pond("Pond_Center", w, new Vector2(55f, 44f), 4f, 6f);              // สระเล็กกลางกลุ่มอาคารฝั่งตะวันออก
            Pond("Pond_South", w, new Vector2(52f, -58f), 7f, 6f);              // สระใต้กลุ่มอาคารกลาง
            Pond("Pond_NatureTrail", w, new Vector2(-72f, -50f), 8f, 5f);       // สระในป่า
            Pond("Pond_NE_Outer", w, new Vector2(107f, 79f), 7f, 9f);           // นอกวงรอบ ตะวันออกเฉียงเหนือ
            // บ่อบำบัดน้ำ (กลุ่มบ่อสี่เหลี่ยม นอกวงรอบ ตะวันออกเฉียงเหนือ)
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    var c = new Vector2(118f + i * 13f, 108f + j * 11f);
                    Rect("TreatPond_Edge", w, c, 12f, 10f, 0.034f, MStone);
                    Rect("TreatPond", w, c, 10f, 8f, 0.05f, MWater);
                    WaterCollider("TreatPond", w, c, 5.4f, 4.4f);
                    Ponds.Add(new Vector4(c.x, c.y, 5.5f, 4.5f));
                }
        }

        // =====================================================================
        // ย้ายอาคารเดิม (ทั้งก้อน พร้อมประตู/จุดระบบ)
        // =====================================================================
        static Transform FindT(string path)
        {
            var g = GameObject.Find(path);
            if (g == null)
            {
                // หาแบบรวม inactive
                foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (path.StartsWith(r.name + "/")) { var t = r.transform.Find(path.Substring(r.name.Length + 1)); if (t) return t; }
                    else if (path == r.name) return r.transform;
                }
                log.AppendLine("⚠ ไม่พบ " + path); return null;
            }
            return g.transform;
        }

        static void RigidMove(IEnumerable<Transform> ts, Vector3 pivot, float dyaw, Vector3 newPivot)
        {
            foreach (var t in ts)
            {
                if (t == null) continue;
                t.RotateAround(pivot, Vector3.up, dyaw);
                t.position += newPivot - pivot;
            }
        }

        static float FrontCoord(Bounds b, float yaw)
        {
            var d = FaceDir(yaw);
            if (d.z < -0.5f) return b.min.z; if (d.z > 0.5f) return b.max.z;
            if (d.x > 0.5f) return b.max.x; return b.min.x;
        }

        // ย้ายอาคาร: หมุนจากทิศหน้าเดิม → ทิศใหม่ · วางจุดกึ่งกลางที่ center (แกนขนานหน้า) และให้ขอบหน้าอยู่ที่ frontLine
        static void Relocate(string key, string label, string bldPath, float oldYaw, Vector2 center, float newYaw, float frontLine, string zone, params string[] attached)
        {
            var bt = FindT(bldPath); if (bt == null) return;
            var list = new List<Transform> { bt };
            foreach (var a in attached) { var t = FindT(a); if (t) list.Add(t); }
            var b0 = WB(bt.gameObject);
            var pivot = new Vector3(b0.center.x, 0, b0.center.z);
            RigidMove(list, pivot, newYaw - oldYaw, V3(center));
            var b1 = WB(bt.gameObject);
            var d = FaceDir(newYaw);
            float shift = frontLine - FrontCoord(b1, newYaw);
            var off = (Mathf.Abs(d.z) > 0.5f) ? new Vector3(0, 0, shift) : new Vector3(shift, 0, 0);
            // จัดแกนขนานหน้าให้ตรง center
            var b1c = b1.center;
            if (Mathf.Abs(d.z) > 0.5f) off.x = center.x - b1c.x; else off.z = center.y - b1c.z;
            foreach (var t in list) if (t) t.position += off;
            bt.SetParent(Z(zone), true);
            Register(key, label, bt.gameObject, newYaw, true);
        }

        static void Register(string key, string label, GameObject g, float yaw, bool moved)
        {
            var b = WB(g);
            var d = FaceDir(yaw);
            var front = new Vector3(b.center.x, 0, b.center.z);
            if (Mathf.Abs(d.z) > 0.5f) front.z = FrontCoord(b, yaw); else front.x = FrontCoord(b, yaw);
            Buildings[key] = new Bld { key = key, label = label, b = b, yaw = yaw, front = front, moved = moved };
            Block(b, 0.6f);
        }

        static void MoveExisting()
        {
            // --- ลานกลาง (ลานอัฐศิลป์ + ตึก A/B/C/D) ย้ายทั้งชุด หมุน 90° ---
            //   แผนผังสภานิสิต: D (ทรงกากบาทกลาง) มี A อยู่ตะวันออกเฉียงใต้ B ทางใต้ C ทางตะวันตก (หลังปรับทิศ) ≈ หมุนชุดเดิม 90°
            var plaza = new List<Transform>();
            foreach (var n in new[] { "ห้องสมุด", "ตึก B สำนักคอมพิวเตอร์", "ห้องสอบ", "ตึก D วิทยาลัยการเมืองการปกครอง" })
                plaza.Add(FindT("NewCampus/Buildings/" + n));
            foreach (var grp in new[] { "NewCampus/Paths", "NewCampus/Props", "NewCampus/Trees" })
            {
                var g = FindT(grp); if (g == null) continue;
                foreach (Transform c in g)
                {
                    var b = WB(c.gameObject);
                    if (Mathf.Abs(b.center.x) <= 13.5f && Mathf.Abs(b.center.z) <= 13.5f && b.extents.x < 14f && b.extents.z < 14f) plaza.Add(c);
                }
            }
            var attached = new List<Transform>();
            foreach (var p in new[] { "BuildingDoors/Door_ห้องสมุด", "ExamPoint", "Job_Library", "TalkNPCs/NPC_รุ่นพี่ปี 4", "TalkNPCs/NPC_อาจารย์บรรณารักษ์" })
            { var t = FindT(p); if (t) attached.Add(t); }
            var seating = FindT("Seating");
            if (seating) foreach (Transform s in seating) if (Mathf.Abs(s.position.x) < 500f) attached.Add(s);
            var all = new List<Transform>(plaza); all.AddRange(attached);
            RigidMove(all, Vector3.zero, 90f, V3(PlazaC));
            foreach (var t in plaza) if (t) t.SetParent(Z("Zone_Center"), true);
            log.AppendLine($"✓ ย้ายลานกลาง + ตึก A/B/C/D ({plaza.Count} ชิ้น) และจุดระบบ {attached.Count} จุด → ({PlazaC.x},{PlazaC.y}) หมุน 90°");
            Register("A", "A · อาคารสำนักวิทยบริการ (ห้องสมุด)", GameObject.Find("KR_Campus/Zone_Center/ห้องสมุด"), 90f, true);
            Register("B", "B · อาคารสำนักคอมพิวเตอร์", GameObject.Find("KR_Campus/Zone_Center/ตึก B สำนักคอมพิวเตอร์"), 180f, true);
            Register("C", "C · วิทยาลัยดุริยางคศิลป์ (ห้องสอบ)", GameObject.Find("KR_Campus/Zone_Center/ห้องสอบ"), -90f, true);
            Register("D", "D · วิทยาลัยการเมืองการปกครอง", GameObject.Find("KR_Campus/Zone_Center/ตึก D วิทยาลัยการเมืองการปกครอง"), 0f, true);
            // ลานกลางเป็นพื้นที่โล่ง กันต้นไม้
            BlockRect(PlazaC, 26f, 26f);

            // --- อาคารเดินเข้าได้ (prefab) ---
            Relocate("GE", "อาคารเรียน GE", "GE_Building", 0f, new Vector2(30f, 0), 0f, 33f, "Zone_NorthGE");
            Relocate("IT", "IT · คณะวิทยาการสารสนเทศ", "IT_Building", 180f, new Vector2(22f, 0), 180f, -45.5f, "Zone_SouthEast",
                     "BuildingDoors/Door_คณะ IT", "FacultySigns/Sign_คณะ IT", "TalkNPCs/NPC_เพื่อนร่วมคณะ");
            Relocate("Dorm2", "หอพักนิสิต (อาคารใหม่)", "Dorm_Building", 0f, new Vector2(-58f, 0), 0f, DormZ + SubW * 0.5f + WalkW + 1.5f, "Zone_DormNW");

            // --- อาคาร Synty เดิมที่มีประตูวาร์ป/ภายใน ---
            float nFront = DormZ + SubW * 0.5f + WalkW + 1.5f, sFront = DormZ - SubW * 0.5f - WalkW - 1.5f;
            Relocate("หอพัก", "หอพักนิสิต", "NewCampus/Buildings/หอพัก", 0f, new Vector2(-37f, 0), 0f, nFront, "Zone_DormNW",
                     "BuildingDoors/Door_หอพัก", "DormSpawn/DormExteriorExit");
            Relocate("หอพัก2", "", "NewCampus/Buildings/หอพักนิสิต_2", 0f, new Vector2(-98f, 0), 0f, nFront, "Zone_DormNW");
            Relocate("ชมรม", "กองกิจการนิสิต (อาคารชมรม)", "NewCampus/Buildings/อาคารชมรม", 0f, new Vector2(-78f, 0), 0f, nFront, "Zone_DormNW",
                     "BuildingDoors/Door_อาคารชมรม");
            Relocate("โรงอาหาร", "ตลาดน้อย (โรงอาหาร)", "NewCampus/Buildings/โรงอาหาร", 0f, new Vector2(-50f, 0), 180f, sFront, "Zone_DormNW",
                     "BuildingDoors/Door_โรงอาหาร", "Job_Cafe");
            Relocate("ร้านค้า", "MSU Plaza (ร้านค้า)", "NewCampus/Buildings/ร้านค้า", 0f, new Vector2(-72f, 0), 180f, sFront, "Zone_DormNW",
                     "BuildingDoors/Door_ร้านค้า", "TalkNPCs/NPC_แม่ค้าร้านค้า");
            Relocate("อาคารเรียน", "สำนักศึกษาทั่วไป (อาคารเรียน)", "NewCampus/Buildings/อาคารเรียน", -90f, new Vector2(0, 6f), -90f, RoadX - RoadW * 0.5f - WalkW - 2.5f, "Zone_West",
                     "BuildingDoors/Door_อาคารเรียน");
            Relocate("บริหาร", "สำนักงานอธิการบดี (อาคารบริหาร)", "NewCampus/Buildings/อาคารบริหาร", 180f, new Vector2(36f, 0), 0f, 56f, "Zone_NorthGE",
                     "BuildingDoors/Door_อาคารบริหาร", "TalkNPCs/NPC_อาจารย์ที่ปรึกษา", "NewCampus/Props/FlagPole", "NewCampus/Props/ThaiFlag");
            Relocate("ทะเบียน", "กองทะเบียนและประมวลผล", "NewCampus/Buildings/กองทะเบียนและประมวลผล", 0f, new Vector2(52f, 0), 0f, 56f, "Zone_NorthGE");
            Relocate("พลศึกษา", "อาคารพลศึกษา", "NewCampus/Buildings/อาคารพลศึกษา", 180f, new Vector2(14f, 0), 0f, RingR + RingW * 0.5f + 4.5f, "Zone_SportsN");
            Relocate("บุคลากร", "อาคารชุดที่พักบุคลากร", "NewCampus/Buildings/อาคารชุดที่พักบุคลากร", 90f, new Vector2(0, 30f), -90f, -116f, "Zone_Outer");
            // อาคารคณะ (Synty เดิม · ตกแต่ง) → ใช้เป็นอาคารคณะตามแผนผัง
            Relocate("SC1", "SC1 · อาคารวิทยาศาสตร์", "NewCampus/Buildings/คณะวิทยาศาสตร์", 0f, new Vector2(72f, 0), 180f, RoadSZ - SubW * 0.5f - WalkW - 1.5f, "Zone_SouthEast");
            Relocate("MBS", "MBS · คณะการบัญชีและการจัดการ", "NewCampus/Buildings/คณะการบัญชีและการจัดการ", 0f, new Vector2(0, 12f), 90f, 83.5f, "Zone_East");
            Relocate("HS", "HS · คณะมนุษยศาสตร์และสังคมศาสตร์", "NewCampus/Buildings/คณะมนุษยศาสตร์และสังคมศาสตร์", -90f, new Vector2(0, -8f), 90f, RoadEX + SubW * 0.5f + WalkW + 2f, "Zone_East");
            Relocate("NU", "NU · คณะพยาบาลศาสตร์", "NewCampus/Buildings/คณะพยาบาลศาสตร์", 90f, new Vector2(0, -30f), -90f, RoadX - RoadW * 0.5f - WalkW - 2.5f, "Zone_West");
            Relocate("Pha", "Pha · คณะเภสัชศาสตร์", "NewCampus/Buildings/คณะเภสัชศาสตร์", 180f, new Vector2(0, -13f), -90f, RoadX - RoadW * 0.5f - WalkW - 2.5f, "Zone_West");
            log.AppendLine($"✓ ย้ายอาคารเดิม {Buildings.Count} หลัง (รวม A–D)");
        }

        // =====================================================================
        // อาคารคณะใหม่ (ภายนอกแบบเรียบง่าย · prefab + LOD)
        // =====================================================================
        static void NewFaculty(string key, string label, string pf, Vector2 center, float yaw, float frontLine, string zone)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(Pf[pf], Z(zone));
            g.name = key + "_" + label.Split('·')[1].Trim();
            g.transform.rotation = Quaternion.Euler(0, yaw, 0);
            g.transform.position = V3(center);
            var b = WB(g);
            float shift = frontLine - FrontCoord(b, yaw);
            var d = FaceDir(yaw);
            g.transform.position += (Mathf.Abs(d.z) > 0.5f) ? new Vector3(0, 0, shift) : new Vector3(shift, 0, 0);
            Register(key, label, g, yaw, false);
        }

        static void NewFaculties()
        {
            float eFront = RoadEX + SubW * 0.5f + WalkW + 2f;     // หน้าอาคารคอลัมน์แรกฝั่งตะวันออก
            NewFaculty("RN", "RN · อาคารราชนครินทร์", "KR_Faculty_14x10_3F", new Vector2(0, 12f), 90f, eFront, "Zone_East");
            NewFaculty("AR", "AR · คณะสถาปัตยกรรมศาสตร์ ผังเมือง และนฤมิตศิลป์", "KR_Faculty_12x10_3F", new Vector2(0, -26f), 90f, eFront, "Zone_East");
            NewFaculty("FA", "FA · คณะศิลปกรรมศาสตร์", "KR_Faculty_12x10_4F", new Vector2(0, -9f), 90f, 83.5f, "Zone_East");
            NewFaculty("TA", "TA · คณะเทคโนโลยี", "KR_Faculty_12x10_3F", new Vector2(70f, 0), 0f, RoadNZ + SubW * 0.5f + WalkW + 1.5f, "Zone_NorthGE");
            NewFaculty("SC3", "SC3 · อาคารปฏิบัติการกลางทางวิทยาศาสตร์", "KR_Faculty_12x10_4F", new Vector2(65f, 0), 0f, 56f, "Zone_NorthGE");
            NewFaculty("SC2", "SC2 · อาคารวิทยาศาสตร์ชีวภาพ", "KR_Faculty_10x8_3F", new Vector2(53f, 0), 180f, -69f, "Zone_SouthEast");
            NewFaculty("EN", "EN · คณะวิศวกรรมศาสตร์", "KR_Faculty_12x10_4F", new Vector2(34f, 0), 180f, -71f, "Zone_SouthEast");
            NewFaculty("PH", "PH · คณะสาธารณสุขศาสตร์", "KR_Faculty_12x10_3F", new Vector2(12f, 0), 180f, -71f, "Zone_SouthEast");
            NewFaculty("ENV", "ENV · คณะสิ่งแวดล้อมและทรัพยากรศาสตร์", "KR_Faculty_12x10_3F", new Vector2(0, 8f), -90f, -56f, "Zone_West");
            // อาคารทรงโดม 2 หลังฝั่งตะวันตก (เห็นในภาพดาวเทียม ไม่มีชื่อในภาพ — ตกแต่ง)
            foreach (var p in new[] { new Vector2(-46f, 64f), new Vector2(-34f, 74f) })
            {
                var dome = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                dome.name = "Dome_Unlabeled"; dome.transform.SetParent(Z("Zone_West"), false);
                dome.transform.position = new Vector3(p.x, 2.2f, p.y); dome.transform.localScale = new Vector3(10f, 2.2f, 10f);
                dome.GetComponent<Renderer>().sharedMaterial = MCream;
                var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = "DomeRoof"; cap.transform.SetParent(dome.transform, true);
                cap.transform.position = new Vector3(p.x, 4.4f, p.y); cap.transform.localScale = new Vector3(1.0f, 2.2f, 1.0f);
                cap.GetComponent<Renderer>().sharedMaterial = MRoof; Object.DestroyImmediate(cap.GetComponent<Collider>());
                BlockRect(p, 11f, 11f);
            }
            log.AppendLine("✓ สร้างอาคารคณะแบบเรียบง่าย 9 หลัง (RN, AR, FA, TA, SC3, SC2, EN, PH, ENV) + โดม 2 หลัง");
        }

        // =====================================================================
        // ทางเดินเชื่อมประตู
        // =====================================================================
        static void Walk(string name, params Vector2[] pts)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Strip(name + (pts.Length > 2 ? "_" + i : ""), Z("Infra_Walks"), pts[i], pts[i + 1], 3f, 0.027f, MWalk);
                Seg(pts[i], pts[i + 1], 1.5f);
            }
            // มุมต่อ (กันรอยแหว่ง)
            for (int i = 1; i + 1 < pts.Length; i++) Rect(name + "_Joint", Z("Infra_Walks"), pts[i], 3f, 3f, 0.027f, MWalk);
        }

        // ทางเดินจากหน้าอาคารออกไปตามทิศหน้า ยาว len
        static Vector2 FrontPt(string key, float extra = 0f)
        {
            var b = Buildings[key]; var d = FaceDir(b.yaw);
            return V2(b.front + d * extra);
        }

        static void WalkFront(string key, float len, params Vector2[] more)
        {
            if (!Buildings.ContainsKey(key)) return;
            var pts = new List<Vector2> { FrontPt(key, -0.3f), FrontPt(key, len) };
            pts.AddRange(more);
            Walk("Walk_" + key, pts.ToArray());
        }

        static void WalksAll()
        {
            float px0 = PlazaC.x - 11f, px1 = PlazaC.x + 11f, pz0 = PlazaC.y - 11f, pz1 = PlazaC.y + 11f;
            float nS = RoadNZ - SubW * 0.5f - WalkW, nN = RoadNZ + SubW * 0.5f + WalkW;      // ขอบทางเท้าถนนกลางเหนือ
            float sN = RoadSZ + SubW * 0.5f + WalkW, sS = RoadSZ - SubW * 0.5f - WalkW;
            float hwE = RoadX + RoadW * 0.5f + WalkW, hwW = RoadX - RoadW * 0.5f - WalkW;
            // ลานกลาง → ทางเท้ารอบ (ช่องระหว่างตึก A/B/C/D)
            Walk("Walk_PlazaNW", new Vector2(px0, pz1), new Vector2(px0, nS));
            Walk("Walk_PlazaNE", new Vector2(px1, pz1), new Vector2(px1, nS));
            Walk("Walk_PlazaSW", new Vector2(px0, pz0), new Vector2(px0, sN));
            Walk("Walk_PlazaSE", new Vector2(px1, pz0), new Vector2(px1, sN));
            Walk("Walk_PlazaW", new Vector2(px0, 6f), new Vector2(hwE, 6f));
            Walk("Walk_PlazaE", new Vector2(px1, -17f), new Vector2(RoadEX - SubW * 0.5f - WalkW, -17f));
            // GE / IT / อาคารเรียน
            WalkFront("GE", 33f - nN + 0.3f);
            WalkFront("IT", Mathf.Abs(-45.5f - sS) + 0.3f);
            WalkFront("อาคารเรียน", 0f, new Vector2(hwW, 6f));
            // อธิการบดี / ทะเบียน / SC3 → ทางเท้าถนนกลางเหนือ (อ้อมข้าง GE)
            Walk("Walk_AdminSpine", new Vector2(47.5f, 54f), new Vector2(47.5f, nN));
            WalkFront("บริหาร", 2f, new Vector2(47.5f, 54f));
            WalkFront("ทะเบียน", 2f, new Vector2(47.5f, 54f));
            WalkFront("SC3", 2f, new Vector2(47.5f, 54f));
            WalkFront("TA", 1.6f);
            // ฝั่งตะวันออก
            float eW = RoadEX + SubW * 0.5f + WalkW;
            foreach (var k in new[] { "RN", "HS", "AR" }) WalkFront(k, Buildings[k].front.x - eW + 0.3f);
            Walk("Walk_EastLane", new Vector2(81.5f, nS), new Vector2(81.5f, -17f), new Vector2(eW, -17f));
            foreach (var k in new[] { "MBS", "FA" }) WalkFront(k, Buildings[k].front.x - 81.5f);
            // ตะวันออกเฉียงใต้
            WalkFront("SC1", Mathf.Abs(Buildings["SC1"].front.z - sS) + 0.3f);
            Walk("Walk_SouthBack", new Vector2(hwE, -68.5f), new Vector2(53f, -68.5f));
            Walk("Walk_SouthLink", new Vector2(40.5f, -68.5f), new Vector2(40.5f, sS));
            foreach (var k in new[] { "SC2", "EN", "PH" }) WalkFront(k, Mathf.Abs(Buildings[k].front.z - (-68.5f)));
            // ฝั่งตะวันตก
            foreach (var k in new[] { "NU", "Pha" }) WalkFront(k, Mathf.Abs(Buildings[k].front.x - hwW) + 0.3f);
            WalkFront("ENV", 0f, new Vector2(Buildings["ENV"].front.x + 0f, RoadNZ - SubW * 0.5f - WalkW));
            Walk("Walk_Football", new Vector2(hwW, -56f), new Vector2(-33f, -56f));
            Walk("Walk_Court", new Vector2(-36f, RoadNZ + SubW * 0.5f + WalkW), new Vector2(-36f, 33f));
            // เส้นทางศึกษาธรรมชาติ (ทางเดินไม้ในป่า)
            Walk("NatureBoardwalk", new Vector2(hwW, -68.5f), new Vector2(-40f, -82f), new Vector2(-62f, -64f), new Vector2(-62f, -40f), new Vector2(-84f, -30f), new Vector2(-84f, -12f), new Vector2(-62f, -21f), new Vector2(hwW, -21f));
            // หอพัก / โรงอาหาร / ร้านค้า / ชมรม
            float dN = DormZ + SubW * 0.5f + WalkW, dS = DormZ - SubW * 0.5f - WalkW;
            foreach (var k in new[] { "Dorm2", "หอพัก", "ชมรม" }) WalkFront(k, Buildings[k].front.z - dN + 0.3f);
            foreach (var k in new[] { "โรงอาหาร", "ร้านค้า" }) WalkFront(k, dS - Buildings[k].front.z + 0.3f);
            // พลศึกษา → ถนนวงรอบ / ถนนกีฬา
            WalkFront("พลศึกษา", 2f, new Vector2(SportX - SubW * 0.5f - WalkW, Buildings["พลศึกษา"].front.z - 2f));
            WalkFront("บุคลากร", Mathf.Abs(Buildings["บุคลากร"].front.x - (-RingR - RingW * 0.5f)) - 0.5f);
            log.AppendLine("✓ ทางเดินเชื่อมประตูอาคาร " + Buildings.Count + " หลัง + ทางม้าลาย");
        }

        // =====================================================================
        // กีฬา / ลานโล่ง
        // =====================================================================
        static void SportsAll()
        {
            var s = Z("Zone_SportsN");
            // ลู่วงรีทางเหนือ (ติด ทล.2202) — ภาพดาวเทียม
            var oval = new Vector2(12f, 152f);
            Ellipse("Oval_Track", s, oval, 17f, 12.5f, 0.02f, MTrack, 48);
            Ellipse("Oval_Field", s, oval, 13.5f, 9f, 0.03f, MField, 48);
            Rects.Add(new Vector4(oval.x - 18f, oval.y - 13.5f, oval.x + 18f, oval.y + 13.5f));
            // สนามกรีฑามุมตะวันออกเฉียงเหนือ
            var ath = new Vector2(92f, 150f);
            Ellipse("Athletics_Track", s, ath, 22f, 13.5f, 0.02f, MTrack, 56);
            Ellipse("Athletics_Field", s, ath, 18f, 9.5f, 0.03f, MField, 56);
            Strip("Athletics_Line", s, ath + new Vector2(0, -9f), ath + new Vector2(0, 9f), 0.2f, 0.035f, MLine);
            Rects.Add(new Vector4(ath.x - 23f, ath.y - 14.5f, ath.x + 23f, ath.y + 14.5f));
            Walk("Walk_Athletics", new Vector2(SportX + SubW * 0.5f + WalkW, 150f), new Vector2(ath.x - 22f, 150f));
            Walk("Walk_Oval", new Vector2(SportX - SubW * 0.5f - WalkW, 152f), new Vector2(oval.x + 17f, 152f));
            // สระว่ายน้ำ
            var pool = new Vector2(56f, 132f);
            Rect("Pool_Deck", s, pool, 18f, 12f, 0.03f, MPillar);
            Rect("Pool_Water", s, pool, 14f, 8f, 0.045f, MWater);
            var pc = new GameObject("Pool_Collider"); pc.transform.SetParent(s, false); pc.transform.position = new Vector3(pool.x, 0.7f, pool.y);
            pc.AddComponent<BoxCollider>().size = new Vector3(14f, 1.4f, 8f);
            BlockRect(pool, 19f, 13f);
            Walk("Walk_Pool", new Vector2(SportX + SubW * 0.5f + WalkW, 132f), new Vector2(pool.x - 9f, 132f));

            // สนามฟุตบอลฝั่งตะวันตกของ ทล.2202 (ภาพดาวเทียม)
            var w = Z("Zone_West");
            var fb = new Vector2(-44f, -56f);
            Rect("Football_Field", w, fb, 22f, 32f, 0.03f, MField);
            foreach (var l in new[] { new[] { -10.5f, -15.5f, 10.5f, -15.5f }, new[] { -10.5f, 15.5f, 10.5f, 15.5f }, new[] { -10.5f, -15.5f, -10.5f, 15.5f }, new[] { 10.5f, -15.5f, 10.5f, 15.5f }, new[] { -10.5f, 0f, 10.5f, 0f } })
                Strip("Football_Line", w, fb + new Vector2(l[0], l[1]), fb + new Vector2(l[2], l[3]), 0.2f, 0.038f, MLine);
            EllipseRing("Football_Circle", w, fb, 3.2f, 3.2f, 0.2f, 0.038f, MLine);
            foreach (float zz in new[] { -15.8f, 15.8f })
            {
                Box("Goal_PostL", w, new Vector3(fb.x - 2.5f, 0.9f, fb.y + zz), new Vector3(0.15f, 1.8f, 0.15f), MLine, 0, true);
                Box("Goal_PostR", w, new Vector3(fb.x + 2.5f, 0.9f, fb.y + zz), new Vector3(0.15f, 1.8f, 0.15f), MLine, 0, true);
                Box("Goal_Bar", w, new Vector3(fb.x, 1.8f, fb.y + zz), new Vector3(5.15f, 0.15f, 0.15f), MLine);
            }
            BlockRect(fb, 24f, 34f);
            // สนามบาสเก็ตบอล/เอนกประสงค์
            var ct = new Vector2(-36f, 43f);
            Rect("Court", w, ct, 13f, 20f, 0.03f, MCourt);
            Strip("Court_Mid", w, ct + new Vector2(-6.5f, 0), ct + new Vector2(6.5f, 0), 0.15f, 0.038f, MLine);
            EllipseRing("Court_Circle", w, ct, 1.8f, 1.8f, 0.15f, 0.038f, MLine);
            BlockRect(ct, 14f, 21f);
            log.AppendLine("✓ ลู่วงรี, สนามกรีฑา, สระว่ายน้ำ, สนามฟุตบอล, สนามบาส");
        }

        // =====================================================================
        // ที่จอดรถ
        // =====================================================================
        static void ParkingLot(string name, Transform parent, Vector2 c, float w, float d, bool alongX, int cars, int seed)
        {
            Rect(name, parent, c, w, d, 0.021f, MParking);
            var rnd = new System.Random(seed);
            string[] vs = { "Veh_Car_Sedan_01", "Veh_Car_Small_01", "Veh_Car_Taxi_01" };
            float len = alongX ? w : d; int n = Mathf.FloorToInt((len - 2f) / 2.8f);
            for (int row = -1; row <= 1; row += 2)
                for (int i = 0; i <= n; i++)
                {
                    float o = -len * 0.5f + 1f + i * 2.8f;
                    var a = alongX ? c + new Vector2(o, row * d * 0.5f) : c + new Vector2(row * w * 0.5f, o);
                    var b = alongX ? c + new Vector2(o, row * (d * 0.5f - 4.6f)) : c + new Vector2(row * (w * 0.5f - 4.6f), o);
                    Strip(name + "_Stall", parent, a, b, 0.15f, 0.03f, MLine);
                    if (i < n && cars > 0 && rnd.NextDouble() < 0.45)
                    {
                        var mid = (a + b) * 0.5f + (alongX ? new Vector2(1.4f, 0) : new Vector2(0, 1.4f));
                        float yaw = alongX ? (row > 0 ? 180f : 0f) : (row > 0 ? -90f : 90f);
                        var car = Put(vs[rnd.Next(vs.Length)], parent, mid, yaw); cars--;
                        if (car) { foreach (var col in car.GetComponentsInChildren<Collider>()) col.enabled = true; }
                    }
                }
            BlockRect(c, w + 1f, d + 1f);
        }

        static void Parking()
        {
            ParkingLot("Parking_West", Z("Zone_West"), new Vector2(-60f, 40f), 16f, 14f, true, 6, 11);
            Walk("Walk_ParkingWest", new Vector2(-60f, RoadNZ + SubW * 0.5f + WalkW), new Vector2(-60f, 33f));
            ParkingLot("Parking_North", Z("Zone_NorthGE"), new Vector2(3f, 42f), 14f, 16f, false, 6, 12);
            Walk("Walk_ParkingNorth", new Vector2(3f, RoadNZ + SubW * 0.5f + WalkW), new Vector2(3f, 34f));
            ParkingLot("Parking_Dorm", Z("Zone_DormNW"), new Vector2(-132f, DormZ + 13f), 14f, 14f, true, 6, 13);
            log.AppendLine("✓ ที่จอดรถ 3 จุด");
        }

        // =====================================================================
        // ป้ายทางเข้า / ไฟถนน / ม้านั่ง / โต๊ะโรงอาหาร
        // =====================================================================
        static void PropsAll()
        {
            var o = Z("Zone_Outer");
            // ซุ้มประตูทางเข้า (ตำแหน่งสมมติ: ถนนกลางเหนือ ติด ทล.2202)
            var gate = new GameObject("MainGate_Assumed").transform; gate.SetParent(Z("Zone_Center"), false);
            float gx = RoadX + RoadW * 0.5f + WalkW + 3.5f;
            float zl = RoadNZ - SubW * 0.5f - WalkW - 1.4f, zr = RoadNZ + SubW * 0.5f + WalkW + 1.4f;
            Box("Pillar_S", gate, new Vector3(gx, 3.5f, zl), new Vector3(1.8f, 7f, 1.8f), MPillar, 0, true);
            Box("Pillar_N", gate, new Vector3(gx, 3.5f, zr), new Vector3(1.8f, 7f, 1.8f), MPillar, 0, true);
            Box("Beam", gate, new Vector3(gx, 7.3f, RoadNZ), new Vector3(1.0f, 1.3f, zr - zl + 2f), MGold);
            Box("Beam_Top", gate, new Vector3(gx, 8.2f, RoadNZ), new Vector3(0.6f, 0.5f, zr - zl - 3f), MPillar);
            BlockRect(new Vector2(gx, zl), 3f, 3f); BlockRect(new Vector2(gx, zr), 3f, 3f);

            // ไฟถนน (Synty ไม่มีแสง realtime) ตาม ทล.2202 และวงรอบ
            var pole = LoadSynty("Prop_LightPole_Base_01");
            for (float z = MinZ + 12; z < MaxZ - 8; z += 22f)
            {
                var pe = new Vector2(RoadX + RoadW * 0.5f + WalkW + 0.5f, z); var pw = new Vector2(RoadX - RoadW * 0.5f - WalkW - 0.5f, z + 11f);
                if (Free(pe, -1.6f)) { Inst(pole, o, V3(pe), -90f); BlockRect(pe, 1f, 1f); }
                if (Free(pw, -1.6f)) { Inst(pole, o, V3(pw), 90f); BlockRect(pw, 1f, 1f); }
            }
            for (float a = 0; a < 360; a += 15f)
            {
                var p = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * (RingR + RingW * 0.5f + 0.6f);
                if (Mathf.Abs(p.x - RoadX) < RoadW) continue;
                bool hit = false; foreach (var zz in new[] { RoadNZ, RoadSZ }) if (Mathf.Abs(p.y - zz) < SubW) hit = true;
                if (hit) continue;
                bool blocked = false; foreach (var r in Rects) if (p.x > r.x - 1 && p.x < r.z + 1 && p.y > r.y - 1 && p.y < r.w + 1) blocked = true;
                for (int i = 0; i < Segs.Count && !blocked; i++) { var s = Segs[i]; if (SegHalf[i] < 2f && DistSeg(p, new Vector2(s.x, s.y), new Vector2(s.z, s.w)) < SegHalf[i] + 0.8f) blocked = true; }
                if (blocked) continue;
                Inst(pole, o, V3(p), Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg + 90f); BlockRect(p, 1f, 1f);
            }

            // ม้านั่งริมสระ
            foreach (var p in new[] { new Vector3(-2f, 62.5f, 180f), new Vector3(20f, 62.5f, 180f), new Vector3(60f, -47.5f, 0f), new Vector3(110f, 66f, 180f) })
            { Put("Prop_ParkBench_01", Z("Zone_Outer"), new Vector2(p.x, p.y), p.z); BlockRect(new Vector2(p.x, p.y), 2.5f, 2.5f); }

            // ลานโต๊ะหน้าโรงอาหาร
            var caf = Buildings["โรงอาหาร"];
            for (int i = 0; i < 2; i++)
            {
                var pos = new Vector2(caf.b.max.x + 3.5f + i * 4f, caf.b.center.z);
                Put("Prop_PicnicTable_01", Z("Zone_DormNW"), pos, 90f); Put("Prop_Umbrella_01", Z("Zone_DormNW"), pos, 0f);
                BlockRect(pos, 3.5f, 3.5f);
            }
            var hd = new Vector2(caf.b.min.x - 3f, caf.b.center.z); Put("Prop_HotdogStand_01", Z("Zone_DormNW"), hd, 0f); BlockRect(hd, 3f, 3f);
            var shop = Buildings["ร้านค้า"];
            var atm = new Vector2(shop.b.min.x - 1.5f, shop.b.center.z); Put("Prop_ATM_01", Z("Zone_DormNW"), atm, 0f); BlockRect(atm, 2f, 2f);
            var bus = new Vector2(RoadX + RoadW * 0.5f + WalkW + 1.6f, 44f); Put("Prop_BusStop_01", o, bus, -90f); BlockRect(bus, 3f, 5f);
            log.AppendLine("✓ ซุ้มประตู (สมมติ), ไฟถนน, ม้านั่ง, ลานโต๊ะโรงอาหาร, ป้ายรถ");
        }

        // =====================================================================
        // ต้นไม้ (prefab + LOD)
        // =====================================================================
        static void TreesAll()
        {
            var placed = new List<Vector2>(); int n = 0; var rnd = new System.Random(20261001);
            Transform ZoneOf(Vector2 p)
            {
                if (p.magnitude > RingR) return p.y > 100f && p.x < RoadX ? Z("Zone_DormNW") : (p.y > 100f ? Z("Zone_SportsN") : Z("Zone_Outer"));
                if (p.x < RoadX) return p.y < -15f ? Z("Zone_SW_Forest") : Z("Zone_West");
                if (p.y > RoadNZ) return Z("Zone_NorthGE");
                if (p.x > RoadEX) return Z("Zone_East");
                if (p.y < RoadSZ) return Z("Zone_SouthEast");
                return Z("Zone_Center");
            }
            void Tree(Vector2 p, float s, float minGap = 4.2f)
            {
                foreach (var q in placed) if ((p - q).sqrMagnitude < minGap * minGap) return;
                var pf = Pf["KR_Tree_0" + (1 + n % 3)];
                var g = Inst(pf, ZoneOf(p), V3(p), (n * 67) % 360, s);
                placed.Add(p); n++;
            }
            // แนวต้นไม้ริมถนน 2202 (นอกทางเท้า)
            for (float z = MinZ + 8; z < MaxZ - 6; z += 9f)
                foreach (float x in new[] { RoadX - RoadW * 0.5f - WalkW - 1.8f, RoadX + RoadW * 0.5f + WalkW + 1.8f })
                { var p = new Vector2(x, z); if (Free(p, 0f)) Tree(p, 1.5f); }
            // ริมวงรอบ ทั้งใน/นอก
            for (float a = 0; a < 360; a += 5f)
            {
                var pin = new Vector2(Mathf.Cos((a + 2.5f) * Mathf.Deg2Rad), Mathf.Sin((a + 2.5f) * Mathf.Deg2Rad)) * (RingR - RingW * 0.5f - WalkW - 2.6f);
                var pout = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * (RingR + RingW * 0.5f + 2.8f);
                if (Free(pin, 0f)) Tree(pin, 1.5f);
                if (Free(pout, 0f)) Tree(pout, 1.6f);
            }
            // ริมถนนย่อย
            foreach (var s in new[] { new Vector4(RoadX + 8, RoadNZ, 100, RoadNZ), new Vector4(-98, RoadNZ, RoadX - 8, RoadNZ), new Vector4(RoadX + 8, RoadSZ, 94, RoadSZ), new Vector4(-140, DormZ, RoadX - 8, DormZ) })
                for (float x = s.x; x < s.z; x += 8f)
                    foreach (float side in new[] { -1f, 1f })
                    { var p = new Vector2(x, s.y + side * (SubW * 0.5f + WalkW + 1.8f)); if (Free(p, 0f)) Tree(p, 1.4f); }
            // ป่าตะวันตกเฉียงใต้ (หนาแน่น)
            for (int i = 0; i < 260; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rnd.NextDouble());
                var p = new Vector2(-56f, -48f) + new Vector2(Mathf.Cos(ang) * 36f * r, Mathf.Sin(ang) * 36f * r);
                if (Free(p, 0.3f)) Tree(p, 1.4f + (float)rnd.NextDouble() * 0.7f, 3.4f);
            }
            // พื้นที่ว่างทั่วไป (สนามหญ้า) — โปร่งกว่า
            for (float x = MinX + 6; x < MaxX - 6; x += 8f)
                for (float z = MinZ + 6; z < MaxZ - 6; z += 8f)
                {
                    bool inRing = new Vector2(x, z).magnitude < RingR;
                    if (rnd.NextDouble() > (inRing ? 0.55 : 0.40)) continue;
                    var p = new Vector2(x + (float)rnd.NextDouble() * 4f - 2f, z + (float)rnd.NextDouble() * 4f - 2f);
                    if (Free(p, 1.2f)) Tree(p, 1.5f + (float)rnd.NextDouble() * 0.7f, 4.6f);
                }
            log.AppendLine($"✓ ต้นไม้ {n} ต้น (prefab KR_Tree_0x + LOD culling)");
        }

        static void BoundaryAll()
        {
            var b = Z("Boundary");
            void Wall(string n, Vector3 pos, Vector3 size) { var w = new GameObject(n); w.transform.SetParent(b, false); w.transform.position = pos; w.AddComponent<BoxCollider>().size = size; }
            float cx = (MinX + MaxX) * 0.5f, cz = (MinZ + MaxZ) * 0.5f, w2 = MaxX - MinX, d = MaxZ - MinZ;
            Wall("Boundary_N", new Vector3(cx, 2, MaxZ), new Vector3(w2 + 4, 4, 1));
            Wall("Boundary_S", new Vector3(cx, 2, MinZ), new Vector3(w2 + 4, 4, 1));
            Wall("Boundary_W", new Vector3(MinX, 2, cz), new Vector3(1, 4, d + 4));
            Wall("Boundary_E", new Vector3(MaxX, 2, cz), new Vector3(1, 4, d + 4));
        }

        // =====================================================================
        // ระบบเดิม: NPC เส้นทาง สัตว์ ผู้เล่น ป้ายคณะ
        // =====================================================================
        static void PlaceRoute(string routePath, Vector2[] pts, string walkerPath)
        {
            var route = FindT(routePath); if (route == null) return;
            route.position = Vector3.zero;
            for (int i = 0; i < route.childCount; i++) route.GetChild(i).position = V3(pts[i % pts.Length]);
            var w = FindT(walkerPath);
            if (w != null) w.position = new Vector3(pts[0].x, w.position.y, pts[0].y);
        }

        static void RelinkSystems()
        {
            float hwE = RoadX + RoadW * 0.5f + WalkW * 0.5f, hwW = RoadX - RoadW * 0.5f - WalkW * 0.5f;
            Vector2[][] npc =
            {
                new[] { new Vector2(PlazaC.x - 6.5f, PlazaC.y - 6.5f), new Vector2(PlazaC.x + 6.5f, PlazaC.y - 6.5f), new Vector2(PlazaC.x + 6.5f, PlazaC.y + 6.5f), new Vector2(PlazaC.x - 6.5f, PlazaC.y + 6.5f) },
                new[] { new Vector2(PlazaC.x - 11, 6f), new Vector2(hwE, 6f), new Vector2(hwE, DormZ - 5.5f), new Vector2(hwW, DormZ - 5.5f), new Vector2(-40f, DormZ - SubW * 0.5f - WalkW * 0.5f) },
                new[] { new Vector2(22f, RoadSZ - SubW * 0.5f - WalkW * 0.5f), new Vector2(RoadEX - SubW * 0.5f - WalkW * 0.5f, RoadSZ - SubW * 0.5f - WalkW * 0.5f), new Vector2(RoadEX - SubW * 0.5f - WalkW * 0.5f, 15f) },
            };
            string[] walkers = { "NPC_นิสิตปี 1", "NPC_นิสิตปี 3", "NPC_รุ่นพี่ใกล้จบ" };
            for (int r = 0; r < 3; r++) PlaceRoute("TalkNPCs/Route" + r, npc[r], "TalkNPCs/" + walkers[r]);

            Vector2[][] animals =
            {
                new[] { new Vector2(-2f, 60f), new Vector2(6f, 61f), new Vector2(20f, 61f), new Vector2(14f, 57f) },
                new[] { new Vector2(-60f, -80f), new Vector2(-50f, -86f), new Vector2(-42f, -78f), new Vector2(-55f, -74f) },
                new[] { new Vector2(-80f, -28f), new Vector2(-70f, -36f), new Vector2(-84f, -42f), new Vector2(-88f, -32f) },
                new[] { new Vector2(-62f, 70f), new Vector2(-52f, 80f), new Vector2(-42f, 84f), new Vector2(-58f, 58f) },
                new[] { new Vector2(-76f, -70f), new Vector2(-66f, -78f), new Vector2(-60f, -68f), new Vector2(-70f, -62f) },
            };
            var an = FindT("CampusAnimals");
            if (an != null)
            {
                int ai = 0;
                foreach (Transform a in an)
                {
                    if (!a.name.StartsWith("Animal_")) continue;
                    if (ai < animals.Length) PlaceRoute("CampusAnimals/AnimalRoute" + ai, animals[ai], "CampusAnimals/" + a.name);
                    ai++;
                }
            }
            // ผู้เล่น (จุดยืนใน Editor — เกมจริงเกิดในหอพักผ่าน DormSpawnPoint)
            var player = FindT("Player");
            if (player) { player.position = new Vector3(PlazaC.x, 0.66f, PlazaC.y - 8f); player.rotation = Quaternion.identity; }
            // ประตูวาร์ป: ปรับ Trigger ให้มาตรฐาน (ตำแหน่งย้ายตามอาคารแล้ว)
            foreach (Transform d in FindT("BuildingDoors"))
            {
                d.position = new Vector3(d.position.x, d.name == "Door_คณะ IT" ? d.position.y : 0f, d.position.z);
            }
            log.AppendLine("✓ ย้าย NPC/เส้นทางเดิน/สัตว์/ผู้เล่น ตามผังใหม่");
        }

        // =====================================================================
        // ป้ายชื่อ
        // =====================================================================
        static void Label(string text, Vector3 at, Color? bg = null)
        {
            var tpl = GameObject.Find("FacultySigns/Sign_คณะ IT"); if (tpl == null) return;
            var g = Object.Instantiate(tpl, gLabels);
            g.name = "Label_" + text;
            g.transform.position = at; g.transform.rotation = tpl.transform.rotation;
            var rt = (RectTransform)g.transform;
            rt.sizeDelta = new Vector2(Mathf.Max(360f, 26f * text.Length + 60f), 110f);
            var tmp = g.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) { tmp.text = text; tmp.enableAutoSizing = true; tmp.fontSizeMin = 16; tmp.fontSizeMax = 50; }
            if (bg.HasValue) { var img = g.transform.Find("BG"); if (img != null && img.TryGetComponent<UnityEngine.UI.Image>(out var im)) im.color = bg.Value; }
        }

        static void LabelsAll()
        {
            var land = new Color(0.22f, 0.50f, 0.34f, 0.95f); var place = new Color(0.20f, 0.36f, 0.62f, 0.95f);
            foreach (var kv in Buildings)
            {
                if (string.IsNullOrEmpty(kv.Value.label) || kv.Key == "IT") continue;
                Label(kv.Value.label, new Vector3(kv.Value.b.center.x, kv.Value.b.max.y + 2.6f, kv.Value.b.center.z));
            }
            // ป้ายคณะเดิมของระบบหลักสูตร (FacultySigns) ไว้สูงกว่าป้ายชื่ออาคาร
            var signMap = new Dictionary<string, string> { { "Sign_คณะบริหารธุรกิจ", "MBS" }, { "Sign_คณะวิทยาศาสตร์", "SC1" }, { "Sign_คณะนิเทศศาสตร์", "HS" } };
            foreach (var kv in signMap)
            {
                var t = FindT("FacultySigns/" + kv.Key); var b = Buildings[kv.Value].b;
                if (t) t.position = new Vector3(b.center.x, b.max.y + 5.6f, b.center.z);
            }
            var itS = FindT("FacultySigns/Sign_คณะ IT"); var itB = Buildings["IT"].b;
            if (itS) itS.position = new Vector3(itB.center.x, itB.max.y + 2.2f, itB.center.z);
            Label("ลานอัฐศิลป์ (ลานกลาง)", V3(PlazaC, 4.2f), land);
            Label("ทล.2202", new Vector3(RoadX, 3f, 60f), place);
            Label("ทล.2202", new Vector3(RoadX, 3f, -110f), place);
            Label("ถนนวงรอบ (ทล.4069 ฝั่งตะวันตก)", new Vector3(-RingR, 3f, -12f), place);
            Label("สนามฟุตบอล", new Vector3(-44f, 3.5f, -56f), land);
            Label("สระน้ำ", new Vector3(9.3f, 3f, 77.8f), land);
            Label("เส้นทางศึกษาธรรมชาติ", new Vector3(-62f, 5f, -62f), land);
            Label("ลู่วิ่ง", new Vector3(12f, 3.5f, 152f), land);
            Label("สนามกรีฑา", new Vector3(92f, 3.5f, 150f), land);
            Label("สระว่ายน้ำ", new Vector3(56f, 3f, 132f), land);
            Label("บ่อบำบัดน้ำ", new Vector3(124f, 3f, 113f), land);
            Label("ประตูมหาวิทยาลัย", new Vector3(RoadX + RoadW * 0.5f + WalkW + 3.5f, 10.5f, RoadNZ), new Color(0.62f, 0.14f, 0.18f, 0.97f));
            Label("ที่จอดรถ", new Vector3(-60f, 3f, 40f), place);
            Label("ที่จอดรถ", new Vector3(3f, 3f, 42f), place);
        }

        // =====================================================================
        static void DisableOld()
        {
            foreach (var n in new[] { "NewCampus", "MsuLabels", "GE_Building_Connection", "Dorm_Building_Connection" })
            {
                var t = FindT(n); if (t) t.gameObject.SetActive(false);
            }
            var nc = FindT("NewCampus"); if (nc) nc.name = "NewCampus_M44_Old (ปิดไว้ — ลบได้)";
            log.AppendLine("✓ ปิดแผนที่ M44 เดิม (ถนน/สระ/ต้นไม้/ป้าย) + ทางเชื่อม GE/หอพักเดิม");
        }

        static void StaticFlags()
        {
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI;
            foreach (var z in new[] { "Infra_Roads", "Infra_Walks", "Water", "Zone_SW_Forest" })
                foreach (var t in Z(z).GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            foreach (var z in new[] { "Zone_Outer", "Zone_West", "Zone_East", "Zone_SouthEast", "Zone_NorthGE", "Zone_DormNW", "Zone_SportsN", "Zone_Center" })
                foreach (Transform c in Z(z))
                {
                    if (c.GetComponentInChildren<MonoBehaviour>(true) != null) continue;   // อาคารที่มีสคริปต์ (ประตู/cutaway) ไม่แตะ
                    foreach (var t in c.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags | StaticEditorFlags.OccluderStatic);
                }
        }

        // =====================================================================
        // ตรวจผัง
        // =====================================================================
        public static string Verify()
        {
            var sb = new StringBuilder(); int issues = 0;
            var list = new List<Bld>(Buildings.Values);
            float ringIn = RingR - RingW * 0.5f, ringOut = RingR + RingW * 0.5f;
            foreach (var a in list)
            {
                var b = a.b;
                // ทับถนนวงรอบ?
                float dMin = float.MaxValue, dMax = 0f;
                foreach (var c in new[] { new Vector2(b.min.x, b.min.z), new Vector2(b.max.x, b.min.z), new Vector2(b.min.x, b.max.z), new Vector2(b.max.x, b.max.z) })
                { dMin = Mathf.Min(dMin, c.magnitude); dMax = Mathf.Max(dMax, c.magnitude); }
                var nearest = new Vector2(Mathf.Clamp(0, b.min.x, b.max.x), Mathf.Clamp(0, b.min.z, b.max.z)).magnitude;
                if (!(dMax < ringIn - 0.5f || nearest > ringOut + 0.5f)) { sb.AppendLine($"⚠ {a.key} ทับถนนวงรอบ (r {nearest:0.0}–{dMax:0.0})"); issues++; }
                if (b.max.x > RoadX - RoadW * 0.5f - WalkW && b.min.x < RoadX + RoadW * 0.5f + WalkW) { sb.AppendLine($"⚠ {a.key} ทับ ทล.2202/ทางเท้า"); issues++; }
                foreach (var p in Ponds)
                {
                    var q = new Vector2(Mathf.Clamp(p.x, b.min.x, b.max.x), Mathf.Clamp(p.y, b.min.z, b.max.z));
                    var d = new Vector2((q.x - p.x) / (p.z + 1.2f), (q.y - p.y) / (p.w + 1.2f));
                    if (d.sqrMagnitude < 1f) { sb.AppendLine($"⚠ {a.key} ทับสระ ({p.x:0},{p.y:0})"); issues++; }
                }
                foreach (var o in list)
                {
                    if (string.CompareOrdinal(o.key, a.key) <= 0) continue;
                    var i = o.b; var j = a.b;
                    float ox = Mathf.Min(i.max.x, j.max.x) - Mathf.Max(i.min.x, j.min.x), oz = Mathf.Min(i.max.z, j.max.z) - Mathf.Max(i.min.z, j.min.z);
                    if (ox > 0.2f && oz > 0.2f) { sb.AppendLine($"⚠ {a.key} ซ้อน {o.key} ({ox:0.0}×{oz:0.0})"); issues++; }
                }
            }
            // ถนนย่อยทับอาคาร?
            for (int s = 0; s < Segs.Count; s++)
            {
                var sg = Segs[s]; float h = SegHalf[s];
                foreach (var a in list)
                {
                    var b = a.b;
                    for (float t = 0; t <= 1.001f; t += 0.02f)
                    {
                        var p = Vector2.Lerp(new Vector2(sg.x, sg.y), new Vector2(sg.z, sg.w), t);
                        if (p.x > b.min.x + h + 0.3f && p.x < b.max.x - h - 0.3f && p.y > b.min.z + h + 0.3f && p.y < b.max.z - h - 0.3f)
                        { sb.AppendLine($"⚠ ทาง/ถนน seg{s} ผ่านกลางอาคาร {a.key}"); issues++; break; }
                    }
                }
            }
            sb.AppendLine(issues == 0 ? "✓ ตรวจผัง: ไม่มีอาคารทับถนน/สระ/อาคารอื่น" : $"ตรวจผัง: {issues} จุดควรดู");
            return sb.ToString();
        }

        // =====================================================================
        // ภาพมุมสูง/มุมสายตา (เรนเดอร์ด้วยกล้องชั่วคราว ไม่แตะกล้องในฉาก)
        // =====================================================================
        public static string Capture(string file, Vector3 pos, Vector3 euler, bool ortho, float sizeOrFov, int w = 1600, int h = 1600, bool hideLabels = false)
        {
            var hidden = new List<GameObject>();
            if (hideLabels) foreach (var n in new[] { "KR_Labels", "FacultySigns" }) { var g = GameObject.Find(n); if (g) { g.SetActive(false); hidden.Add(g); } }
            var go = new GameObject("_KRCaptureCam");
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;   // หมอกในฉาก (สิ้นสุด ~67 ม.) ทำให้ภาพมุมสูงจาง — ปิดชั่วคราวเฉพาะตอนถ่าย
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.position = pos; cam.transform.rotation = Quaternion.Euler(euler);
                cam.orthographic = ortho; if (ortho) cam.orthographicSize = sizeOrFov; else cam.fieldOfView = sizeOrFov;
                cam.nearClipPlane = 0.3f; cam.farClipPlane = 1500f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.75f, 0.88f);
                cam.cullingMask = ~(1 << 5);   // ไม่เอา UI layer
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 4;
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, w, h), 0, 0); tex.Apply();
                RenderTexture.active = null; cam.targetTexture = null;
                System.IO.Directory.CreateDirectory("Assets/Screenshots");
                string path = "Assets/Screenshots/" + file + ".png";
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
                return path;
            }
            finally { Object.DestroyImmediate(go); RenderSettings.fog = fog; foreach (var g in hidden) g.SetActive(true); }
        }
    }
}
#endif
