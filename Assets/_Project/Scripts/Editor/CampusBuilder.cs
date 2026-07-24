#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NisitSimulator.EditorTools
{
    // สร้าง "ผังมหาลัย" จากชุด KayKit City + Forest:
    //   ถนนตาราง + บล็อกตึก (สุ่มแบบ/ตั้งชื่อ) + สวนสาธารณะ + ลานจอดรถ + สนามกีฬา + ประตูมหาลัย
    //   + ต้นไม้/เสาไฟตามถนน + ปรับขนาดพื้นอัตโนมัติ  (ทุกอย่างอยู่ใต้ "Campus")
    //
    // ปรับ: ถนนหันขวาง -> RoadYaw=0 | ใหญ่/เล็ก -> GlobalScale | ตึกเอาหลังให้ -> FrontYaw=180 | เมืองเล็กลง -> Blocks=2
    public static class CampusBuilder
    {
        private const string CityFolder = "Assets/_Project/Art/Models/KayKit_City";
        private const string ForestFolder = "Assets/_Project/Art/Models/KayKit_Forest";
        private const int Blocks = 3;         // 3 = 9 บล็อก (มีที่พอสำหรับสวน/ลานจอด/สนาม)
        private const float RoadYaw = 90f;
        private const float GlobalScale = 2f;
        private const float FrontYaw = 0f;

        internal static readonly string[] Places =
        { "อาคารเรียน", "ห้องสมุด", "โรงอาหาร", "หอพัก", "อาคารชมรม", "ร้านค้า", "อาคารบริหาร", "คณะ IT" };

        private enum Kind { Buildings, Park, Parking, Sports, Plaza }

        private static Dictionary<string, GameObject> _lib;
        private static Transform _root;
        private static System.Random _rng;
        private static float _natureMul = 1f;
        private static float _bH = 4f;        // ความสูงตึก (world)
        private static float _bStep = 2f;
        private static bool _hasForest;
        private static int _placeIdx;
        private static List<GameObject> _buildings, _trees, _bushes;

        [MenuItem("Nisit/Place Campus Buildings", false, 1)]
        public static void PlaceBuildings()
        {
            _lib = LoadLibrary(CityFolder, ForestFolder);
            _rng = new System.Random(20260716);

            var buildings = ByPrefix("building_", exclude: "withoutBase");
            var trees  = ByPrefix("Tree_", exclude: "Bare");
            var bushes = ByPrefix("Bush_");
            var grass  = ByPrefix("Grass_", exclude: "Singlesided"); grass.RemoveAll(g => g.name.Contains("Mesh"));

            if (buildings.Count == 0)
            {
                EditorUtility.DisplayDialog("Nisit", "ไม่พบตึก building_* ใน KayKit_City\nลองกด Assets -> Refresh ก่อน", "OK");
                return;
            }
            bool hasForest = trees.Count > 0;
            _hasForest = hasForest; _buildings = buildings; _trees = trees; _bushes = bushes; _placeIdx = 0;

            if (!EditorUtility.DisplayDialog("Nisit — Build Campus",
                "จะสร้างผังมหาลัย: ถนน + ตึก + สวน + ลานจอดรถ + สนามกีฬา\n\nทำต่อไหม?",
                "สร้างเลย", "ยกเลิก"))
                return;

            var oldGo = GameObject.Find("Campus");
            if (oldGo != null) Object.DestroyImmediate(oldGo);
            var campus = new GameObject("Campus");
            Undo.RegisterCreatedObjectUndo(campus, "Build Campus");
            _root = campus.transform;

            float cell = FootprintOf(_lib.GetValueOrDefault("road_straight")) * GlobalScale;
            if (cell < 0.01f) cell = 4f * GlobalScale;
            float bFoot = 0f; foreach (var b in buildings) bFoot = Mathf.Max(bFoot, FootprintOf(b));
            bFoot *= GlobalScale;
            float bStep = bFoot * 1.15f;
            _bStep = bStep;
            float S = Mathf.Max(3f, Mathf.Round((2f * bStep + cell * 1.5f) / cell)) * cell;

            _bH = HeightOf(buildings[0]) * GlobalScale;
            float tH = hasForest ? HeightOf(trees[0]) : 1f;
            _natureMul = (tH > 0.01f) ? Mathf.Clamp(0.9f * (_bH / GlobalScale) / tH, 0.05f, 1f) : 1f;

            var lines = new List<float>();
            for (int i = 0; i <= Blocks; i++) lines.Add((i - Blocks * 0.5f) * S);
            float lo = lines[0], hi = lines[lines.Count - 1];

            // ---- ถนน ----
            foreach (float lx in lines) foreach (float lz in lines)
                Place("road_junction", new Vector3(lx, 0f, lz), 0f, false);
            foreach (float lx in lines)
                for (float z = lo + cell; z < hi - 0.01f; z += cell)
                    if (!IsLine(z, lines)) Place("road_straight", new Vector3(lx, 0f, z), RoadYaw + 90f, false);
            foreach (float lz in lines)
                for (float x = lo + cell; x < hi - 0.01f; x += cell)
                    if (!IsLine(x, lines)) Place("road_straight", new Vector3(x, 0f, lz), RoadYaw, false);

            // ---- บล็อก ----
            for (int ix = 0; ix < Blocks; ix++)
                for (int iz = 0; iz < Blocks; iz++)
                {
                    float cx = (lines[ix] + lines[ix + 1]) * 0.5f;
                    float cz = (lines[iz] + lines[iz + 1]) * 0.5f;
                    switch (TypeOf(ix, iz))
                    {
                        case Kind.Plaza:   FillPlaza(cx, cz, S - cell); break;
                        case Kind.Park:    FillPark(cx, cz, S - cell * 1.8f, trees, bushes, grass); break;
                        case Kind.Parking: FillParking(cx, cz, S - cell); break;
                        case Kind.Sports:  FillSports(cx, cz, S - cell); break;
                        default:           BuildBlock(cx, cz, S - cell); break;
                    }
                }

            // ---- ต้นไม้/เสาไฟตามถนน ----
            foreach (float lz in lines)
                for (float x = lo + cell; x < hi; x += cell * 2f)
                {
                    bool tree = hasForest && _rng.Next(2) == 0;
                    var m = tree ? trees[_rng.Next(trees.Count)] : _lib.GetValueOrDefault("streetlight");
                    if (m != null) PlaceGO(m, m.name, new Vector3(x, 0f, lz + cell * 0.6f), RandYaw(), true, tree ? _natureMul : 1f);
                }

            // (เอาประตูมหาลัยออกแล้ว — ดูโล่งจำเจ)

            FitGround(hi - lo + cell * 4f);
            Selection.activeGameObject = campus;
            SceneView.FrameLastActiveSceneView();
            Debug.Log("<color=lime>[Nisit] ✅ สร้างมหาลัยเสร็จ (ถนน+ตึก+สวน+ลานจอดรถ+สนามกีฬา+ประตู)\n" +
                      "ปรับ RoadYaw/GlobalScale/FrontYaw/Blocks ในหัวไฟล์ แล้วกดใหม่</color>");
        }

        private static Kind TypeOf(int ix, int iz)
        {
            if (ix == Blocks / 2 && iz == Blocks / 2) return Kind.Plaza;   // บล็อกกลาง = ลานโล่ง (ตัวละครเกิดตรงนี้)
            if (ix == 0 && iz == Blocks - 1) return Kind.Park;
            if (ix == Blocks - 1 && iz == 0) return Kind.Parking;
            if (ix == Blocks - 1 && iz == Blocks - 1) return Kind.Sports;
            return Kind.Buildings;
        }

        // ---------- บล็อกตึก: สุ่ม 1 ใน 3 แบบ ให้ไม่ซ้ำกัน ----------
        private static void BuildBlock(float cx, float cz, float inner)
        {
            float bs = _bStep;
            int pattern = _rng.Next(3);

            if (pattern == 0)                       // 2x2 หันออก + ต้นไม้กลาง
            {
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        PlaceBuilding(new Vector3(cx + sx * bs * 0.5f, 0f, cz + sz * bs * 0.5f),
                                      Mathf.Atan2(sx, sz) * Mathf.Rad2Deg + FrontYaw);
                CenterGreen(cx, cz, bs * 0.8f, 1);
            }
            else if (pattern == 1)                  // คอร์ทยาร์ด: 4 ตึกหันเข้า + ลานกลาง
            {
                var offs = new[] { new Vector2(0, bs * 0.6f), new Vector2(0, -bs * 0.6f),
                                   new Vector2(bs * 0.6f, 0), new Vector2(-bs * 0.6f, 0) };
                foreach (var o in offs)
                {
                    var p = new Vector3(cx + o.x, 0f, cz + o.y);
                    PlaceBuilding(p, Mathf.Atan2(cx - p.x, cz - p.z) * Mathf.Rad2Deg + FrontYaw);
                }
                var bench = _lib.GetValueOrDefault("bench");
                if (bench != null)
                {
                    PlaceGO(bench, "bench", new Vector3(cx, 0f, cz + bs * 0.18f), 180f, true, 1f);
                    PlaceGO(bench, "bench", new Vector3(cx, 0f, cz - bs * 0.18f), 0f, true, 1f);
                }
                CenterGreen(cx, cz, bs * 0.3f, 1);
            }
            else                                    // เรียงแถวด้านหนึ่ง + สวนอีกด้าน
            {
                int side = _rng.Next(4);
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 p; float yaw;
                    if (side < 2) { float z = (side == 0 ? inner * 0.28f : -inner * 0.28f); p = new Vector3(cx + i * bs * 0.7f, 0f, cz + z); yaw = (side == 0 ? 180f : 0f) + FrontYaw; }
                    else { float x = (side == 2 ? inner * 0.28f : -inner * 0.28f); p = new Vector3(cx + x, 0f, cz + i * bs * 0.7f); yaw = (side == 2 ? 270f : 90f) + FrontYaw; }
                    PlaceBuilding(p, yaw);
                }
                float gx = side == 2 ? cx - inner * 0.25f : side == 3 ? cx + inner * 0.25f : cx;
                float gz = side == 0 ? cz - inner * 0.25f : side == 1 ? cz + inner * 0.25f : cz;
                CenterGreen(gx, gz, bs * 1.1f, 4);
            }

            var light = _lib.GetValueOrDefault("streetlight");
            if (light != null) PlaceGO(light, "streetlight", new Vector3(cx + inner * 0.42f, 0f, cz + inner * 0.42f), 0f, true, 1f);
        }

        // วางตึก 1 หลัง: สุ่มแบบ + สุ่มขนาด/ความสูง (สร้าง skyline) + ตั้งชื่อสถานที่
        private static void PlaceBuilding(Vector3 pos, float yaw)
        {
            var model = _buildings[_rng.Next(_buildings.Count)];
            float s = 0.85f + (float)_rng.NextDouble() * 0.35f;   // ขยายสัดส่วนเท่ากันทุกด้าน (ไม่ยืด -> ไม่เพี้ยน)
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = Places[_placeIdx++ % Places.Length];
            inst.transform.SetParent(_root);
            inst.transform.localScale = Vector3.one * GlobalScale * s;
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            inst.transform.position = pos;
            var b = GetWorldBounds(inst); inst.transform.position += new Vector3(0f, -b.min.y, 0f);
            AddBoxCollider(inst);   // ให้เดินชนตึก ไม่ทะลุ
        }

        // เพิ่ม BoxCollider พอดีตัวตึก (คำนวณจาก mesh local เลยแม่นแม้ตึกหมุน)
        internal static void AddBoxCollider(GameObject root)
        {
            if (root.GetComponent<Collider>() != null) return;
            if (!ComputeLocalBounds(root, out var center, out var size)) return;
            var col = root.AddComponent<BoxCollider>();
            col.center = center;
            col.size = size;
        }

        // แคปซูลรอบ "ลำต้น" ต้นไม้ (บางๆ) — บล็อกลำต้น แต่เดินใต้พุ่มได้
        internal static void AddTrunkCollider(GameObject root)
        {
            if (root.GetComponent<Collider>() != null) return;
            if (!ComputeLocalBounds(root, out var center, out var size)) return;
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 1;                                  // แกน Y
            col.center = center;
            col.height = size.y;
            col.radius = Mathf.Min(size.x, size.z) * 0.14f;     // บางเท่าลำต้น
        }

        // คำนวณกรอบสี่เหลี่ยม (local space ของ root) จาก mesh — แม่นแม้ root หมุน/สเกล
        private static bool ComputeLocalBounds(GameObject root, out Vector3 center, out Vector3 size)
        {
            center = size = Vector3.zero;
            bool has = false; Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh; if (mesh == null) continue;
                Vector3 c = mesh.bounds.center, e = mesh.bounds.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    var lp = root.transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!has) { min = max = lp; has = true; }
                    else { min = Vector3.Min(min, lp); max = Vector3.Max(max, lp); }
                }
            }
            if (!has) return false;
            center = (min + max) * 0.5f; size = max - min;
            return true;
        }

        // กระจายต้นไม้/พุ่มในพื้นที่เล็กๆ
        private static void CenterGreen(float cx, float cz, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector3(cx + Jitter(radius * 0.5f), 0f, cz + Jitter(radius * 0.5f));
                GameObject m = (_hasForest && _rng.Next(100) < 60 && _trees.Count > 0)
                    ? _trees[_rng.Next(_trees.Count)]
                    : (_bushes.Count > 0 ? _bushes[_rng.Next(_bushes.Count)] : _lib.GetValueOrDefault("bush"));
                if (m != null) PlaceGO(m, m.name, pos, RandYaw(), true, _hasForest ? _natureMul : 1f);
            }
        }

        // ---------- ลานกลาง (โล่ง — ตัวละครเกิดตรงนี้ ต้องไม่มีของบังตรงกลาง) ----------
        private static void FillPlaza(float cx, float cz, float extent)
        {
            float r = extent * 0.36f;
            var bench = _lib.GetValueOrDefault("bench");
            if (bench != null)   // ม้านั่ง 4 ด้าน หันเข้ากลาง
            {
                PlaceGO(bench, "bench", new Vector3(cx, 0f, cz + r), 180f, true, 1f);
                PlaceGO(bench, "bench", new Vector3(cx, 0f, cz - r), 0f, true, 1f);
                PlaceGO(bench, "bench", new Vector3(cx + r, 0f, cz), 270f, true, 1f);
                PlaceGO(bench, "bench", new Vector3(cx - r, 0f, cz), 90f, true, 1f);
            }
            float d = extent * 0.42f;   // ต้นไม้ที่ 4 มุม (ห่างจากจุดเกิด)
            foreach (var s in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
            {
                GameObject m = (_hasForest && _trees.Count > 0) ? _trees[_rng.Next(_trees.Count)]
                    : (_bushes.Count > 0 ? _bushes[_rng.Next(_bushes.Count)] : null);
                if (m != null) PlaceGO(m, m.name, new Vector3(cx + s.x * d, 0f, cz + s.y * d), RandYaw(), true, _hasForest ? _natureMul : 1f);
            }
        }

        // ---------- สวนสาธารณะ ----------
        private static void FillPark(float cx, float cz, float extent,
            List<GameObject> trees, List<GameObject> bushes, List<GameObject> grass)
        {
            float step = Mathf.Max(3f, extent / 3.5f);
            for (float x = -extent * 0.5f; x <= extent * 0.5f + 0.01f; x += step)
                for (float z = -extent * 0.5f; z <= extent * 0.5f + 0.01f; z += step)
                {
                    var pos = new Vector3(cx + x + Jitter(step * 0.25f), 0f, cz + z + Jitter(step * 0.25f));
                    int r = _rng.Next(100);
                    GameObject m =
                        (r < 40 && trees.Count > 0)  ? trees[_rng.Next(trees.Count)] :
                        (r < 75 && bushes.Count > 0) ? bushes[_rng.Next(bushes.Count)] :
                        (grass.Count > 0 ? grass[_rng.Next(grass.Count)] : (bushes.Count > 0 ? bushes[0] : null));
                    if (m != null) PlaceGO(m, m.name, pos, RandYaw(), true, _natureMul);
                }
            var bench = _lib.GetValueOrDefault("bench");
            if (bench != null)
            {
                PlaceGO(bench, "bench", new Vector3(cx, 0f, cz + extent * 0.3f), 180f, true, 1f);
                PlaceGO(bench, "bench", new Vector3(cx, 0f, cz - extent * 0.3f), 0f, true, 1f);
            }
        }

        // ---------- ลานจอดรถ ----------
        private static void FillParking(float cx, float cz, float extent)
        {
            var asphalt = Mat(new Color(0.22f, 0.22f, 0.24f));
            var white = Mat(new Color(0.9f, 0.9f, 0.9f));
            Prim(PrimitiveType.Cube, "Parking_Ground", new Vector3(cx, 0.02f, cz),
                 new Vector3(extent, 0.04f, extent), 0f, asphalt, collider: false);

            string[] cars = { "car_sedan", "car_taxi", "car_hatchback", "car_stationwagon", "car_police" };
            float lane = extent / 3f;                       // 2 แถว
            float slot = _bH * 0.9f;                         // ระยะช่องจอด
            int perRow = Mathf.Max(2, Mathf.FloorToInt(extent / slot));
            for (int row = 0; row < 2; row++)
            {
                float z = cz + (row == 0 ? lane * 0.5f : -lane * 0.5f);
                for (int i = 0; i < perRow; i++)
                {
                    float x = cx - extent * 0.5f + slot * (i + 0.5f);
                    // เส้นช่องจอด
                    Prim(PrimitiveType.Cube, "Line", new Vector3(x - slot * 0.5f, 0.05f, z),
                         new Vector3(0.1f, 0.02f, lane * 0.8f), 0f, white, collider: false);
                    var car = _lib.GetValueOrDefault(cars[(row * perRow + i) % cars.Length]);
                    if (car != null) PlaceGO(car, "car", new Vector3(x, 0.05f, z), row == 0 ? 0f : 180f, true, 1f);
                }
            }
        }

        // ---------- สนามกีฬา ----------
        private static void FillSports(float cx, float cz, float extent)
        {
            float fx = extent, fz = extent * 0.75f;
            var green = Mat(new Color(0.28f, 0.55f, 0.25f));
            var white = Mat(new Color(0.95f, 0.95f, 0.95f));

            Prim(PrimitiveType.Cube, "Field", new Vector3(cx, 0.02f, cz), new Vector3(fx, 0.04f, fz), 0f, green, collider: false);
            // เส้นขอบ + เส้นกลาง
            Prim(PrimitiveType.Cube, "Line_N", new Vector3(cx, 0.05f, cz + fz * 0.5f), new Vector3(fx, 0.02f, 0.15f), 0f, white, collider: false);
            Prim(PrimitiveType.Cube, "Line_S", new Vector3(cx, 0.05f, cz - fz * 0.5f), new Vector3(fx, 0.02f, 0.15f), 0f, white, collider: false);
            Prim(PrimitiveType.Cube, "Line_E", new Vector3(cx + fx * 0.5f, 0.05f, cz), new Vector3(0.15f, 0.02f, fz), 0f, white, collider: false);
            Prim(PrimitiveType.Cube, "Line_W", new Vector3(cx - fx * 0.5f, 0.05f, cz), new Vector3(0.15f, 0.02f, fz), 0f, white, collider: false);
            Prim(PrimitiveType.Cube, "Line_Mid", new Vector3(cx, 0.05f, cz), new Vector3(fx, 0.02f, 0.15f), 0f, white, collider: false);
            // โกล 2 ฝั่ง (แกน X)
            BuildGoal(new Vector3(cx - fx * 0.5f, 0f, cz), 90f, white);
            BuildGoal(new Vector3(cx + fx * 0.5f, 0f, cz), 90f, white);
        }

        private static void BuildGoal(Vector3 pos, float yaw, Material mat)
        {
            float w = _bH * 0.7f, h = _bH * 0.45f, p = 0.15f;
            var g = new GameObject("Goal"); g.transform.SetParent(_root);
            g.transform.position = pos; g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Prim(PrimitiveType.Cube, "post_L", g.transform.TransformPoint(new Vector3(-w * 0.5f, h * 0.5f, 0)), new Vector3(p, h, p), yaw, mat, false, g.transform);
            Prim(PrimitiveType.Cube, "post_R", g.transform.TransformPoint(new Vector3(w * 0.5f, h * 0.5f, 0)), new Vector3(p, h, p), yaw, mat, false, g.transform);
            Prim(PrimitiveType.Cube, "bar", g.transform.TransformPoint(new Vector3(0, h, 0)), new Vector3(w, p, p), yaw, mat, false, g.transform);
        }

        // ---------- ประตูมหาลัย ----------
        private static void BuildGate(float x, float z, float width)
        {
            var concrete = Mat(new Color(0.85f, 0.83f, 0.78f));
            float h = Mathf.Max(4f, _bH * 1.2f), p = 0.6f;
            var gate = new GameObject("ประตูมหาลัย"); gate.transform.SetParent(_root); gate.transform.position = new Vector3(x, 0f, z);
            Prim(PrimitiveType.Cube, "pillar_L", new Vector3(x - width, h * 0.5f, z), new Vector3(p, h, p), 0f, concrete, true, gate.transform);
            Prim(PrimitiveType.Cube, "pillar_R", new Vector3(x + width, h * 0.5f, z), new Vector3(p, h, p), 0f, concrete, true, gate.transform);
            Prim(PrimitiveType.Cube, "beam", new Vector3(x, h, z), new Vector3(width * 2f + p, p * 1.2f, p * 1.4f), 0f, concrete, false, gate.transform);
            // ป้ายบนคาน (แผ่นสีน้ำเงิน) — ตัวหนังสือค่อยทำทีหลังให้อ่านง่าย/ไม่กลับด้าน
            var sign = Mat(new Color(0.15f, 0.2f, 0.45f));
            Prim(PrimitiveType.Cube, "SignBoard", new Vector3(x, h + p * 0.9f, z), new Vector3(width * 1.6f, p * 1.3f, p * 0.3f), 0f, sign, false, gate.transform);
        }

        // ---------- helpers ----------
        private static Material Mat(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh);
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            return m;
        }

        private static GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, float yaw,
            Material mat, bool collider = true, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : _root);
            go.transform.localScale = scale;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = pos;
            var r = go.GetComponent<Renderer>(); if (mat != null) r.sharedMaterial = mat;
            if (!collider) { var col = go.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col); }
            return go;
        }

        private static float Jitter(float amt) => (float)(_rng.NextDouble() - 0.5) * 2f * amt;
        private static float RandYaw() => (new[] { 0f, 90f, 180f, 270f })[_rng.Next(4)];

        private static void Place(string name, Vector3 pos, float yaw, bool ground)
        {
            var m = _lib.GetValueOrDefault(name);
            if (m == null) { Debug.LogWarning($"[Nisit] ไม่พบโมเดล {name}"); return; }
            PlaceGO(m, name, pos, yaw, ground);
        }

        private static GameObject PlaceGO(GameObject model, string name, Vector3 pos, float yaw, bool ground, float scaleMul = 1f)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = name;
            inst.transform.SetParent(_root);
            inst.transform.localScale = Vector3.one * GlobalScale * scaleMul;
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            inst.transform.position = pos;
            if (ground) { var b = GetWorldBounds(inst); inst.transform.position += new Vector3(0f, -b.min.y, 0f); }
            // ต้นไม้/ม้านั่ง = ชนได้ (ถนน/รถ/พุ่ม/หญ้า = เดินผ่าน)
            if (name.StartsWith("Tree_")) AddTrunkCollider(inst);
            else if (name == "bench") AddBoxCollider(inst);
            return inst;
        }

        private static List<GameObject> ByPrefix(string prefix, string exclude = null)
        {
            var l = new List<GameObject>();
            foreach (var kv in _lib)
                if (kv.Key.StartsWith(prefix) && (exclude == null || !kv.Key.Contains(exclude)))
                    l.Add(kv.Value);
            l.Sort((a, b) => string.Compare(a.name, b.name));
            return l;
        }

        private static void FitGround(float span)
        {
            var ground = GameObject.Find("Ground");
            if (ground == null) return;
            float s = Mathf.Max(1f, span / 10f);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(s, 1f, s);
        }

        private static bool IsLine(float v, List<float> lines)
        {
            foreach (var l in lines) if (Mathf.Abs(v - l) < 0.05f) return true;
            return false;
        }

        private static float FootprintOf(GameObject model)
        {
            if (model == null) return 0f;
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(model);
            tmp.transform.position = Vector3.zero; tmp.transform.rotation = Quaternion.identity; tmp.transform.localScale = Vector3.one;
            var b = GetWorldBounds(tmp);
            Object.DestroyImmediate(tmp);
            return Mathf.Max(b.size.x, b.size.z);
        }

        private static float HeightOf(GameObject model)
        {
            if (model == null) return 0f;
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(model);
            tmp.transform.position = Vector3.zero; tmp.transform.rotation = Quaternion.identity; tmp.transform.localScale = Vector3.one;
            var b = GetWorldBounds(tmp);
            Object.DestroyImmediate(tmp);
            return b.size.y;
        }

        private static Dictionary<string, GameObject> LoadLibrary(params string[] folders)
        {
            var dict = new Dictionary<string, GameObject>();
            foreach (var g in AssetDatabase.FindAssets("t:Model", folders))
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null) dict[m.name] = m;
            }
            return dict;
        }

        private static Bounds GetWorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
    }
}
#endif
