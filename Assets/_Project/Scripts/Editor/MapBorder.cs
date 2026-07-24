#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NisitSimulator.EditorTools
{
    // ล้อมขอบแมป: ต่อถนนออกไปตรงจุดที่เป็นถนน + แซมตึกฉากหลังกับแนวต้นไม้
    //   - อ่านตำแหน่งถนนจากเมืองจริง (road_junction) แล้วต่อ road_straight ออกไป
    //   - เติมตึก (เข้าไม่ได้ ฉากหลัง) + ต้นไม้/พุ่ม/หิน คละกัน
    // ใช้: เมนู  Nisit -> Add Map Border
    public static class MapBorder
    {
        private const string CityFolder = "Assets/_Project/Art/Models/KayKit_City";
        private const string ForestFolder = "Assets/_Project/Art/Models/KayKit_Forest";
        private const float GlobalScale = 2f;
        private const float RoadYaw = 90f;      // ให้ตรงกับ CampusBuilder
        private const int BorderRings = 2;      // ถนน/ขอบ ยื่นออกไปกี่ช่อง (แค่ขอบพื้นที่เล่น ไม่ให้แมปใหญ่)

        private static System.Random _rng;
        private static Transform _root;
        private static float _natureMul = 1f;

        [MenuItem("Nisit/Add Map Border", false, 6)]
        public static void AddBorder()
        {
            _rng = new System.Random(777);
            var lib = Load(CityFolder, ForestFolder);

            var trees = Pick(lib, "Tree_", "Bare");
            var bare = Pick(lib, "Tree_Bare", null);
            var bushes = Pick(lib, "Bush_", null);
            var rocks = Pick(lib, "Rock_", null);
            var buildings = new List<GameObject>();
            foreach (var kv in lib) if (kv.Key.StartsWith("building_") && !kv.Key.Contains("withoutBase")) buildings.Add(kv.Value);
            var roadModel = lib.GetValueOrDefault("road_straight");

            if (trees.Count == 0 || roadModel == null)
            {
                EditorUtility.DisplayDialog("Nisit", "ไม่พบต้นไม้ Forest หรือ road_straight\nลองกด Assets -> Refresh", "OK");
                return;
            }

            var campus = GameObject.Find("Campus");
            if (campus == null)
            {
                EditorUtility.DisplayDialog("Nisit", "ไม่พบ \"Campus\"\nสร้างเมืองด้วย Nisit -> Place Campus Buildings ก่อน", "OK");
                return;
            }

            // ---- อ่านเส้นถนนจากสี่แยกในเมือง ----
            var jx = new List<float>(); var jz = new List<float>();
            foreach (Transform c in campus.transform)
                if (c.name == "road_junction") { jx.Add(c.position.x); jz.Add(c.position.z); }
            var xLines = Distinct(jx); var zLines = Distinct(jz);
            if (xLines.Count == 0)
            {
                EditorUtility.DisplayDialog("Nisit", "ไม่พบถนน (road_junction) ในเมือง — สร้างเมืองใหม่ก่อน", "OK");
                return;
            }

            float cell = Footprint(roadModel) * GlobalScale;
            float xmin = Min(xLines), xmax = Max(xLines), zmin = Min(zLines), zmax = Max(zLines);
            float bFoot = 0f; foreach (var b in buildings) bFoot = Mathf.Max(bFoot, Footprint(b));
            bFoot *= GlobalScale;
            float bStep = Mathf.Max(cell * 2f, bFoot * 1.2f);

            float bH = buildings.Count > 0 ? Height(buildings[0]) : 1f;
            float tH = Height(trees[0]);
            _natureMul = (tH > 0.01f) ? Mathf.Clamp(0.9f * bH / tH, 0.05f, 1f) : 1f;

            if (!EditorUtility.DisplayDialog("Nisit — Map Border",
                "จะต่อถนนออกไปที่ขอบ + แซมตึกฉากหลัง + แนวต้นไม้รอบเมือง\n\nทำต่อไหม?", "ทำเลย", "ยกเลิก"))
                return;

            var old = GameObject.Find("MapBorder");
            if (old != null) Object.DestroyImmediate(old);
            _root = new GameObject("MapBorder").transform;
            Undo.RegisterCreatedObjectUndo(_root.gameObject, "Add Map Border");

            // ---- 1) ต่อถนนออกไปนอกเมือง ----
            foreach (float x in xLines)
                for (int k = 1; k <= BorderRings; k++)
                {
                    Spawn(roadModel, new Vector3(x, 0f, zmax + k * cell), RoadYaw + 90f, 1f, false, Col.None);
                    Spawn(roadModel, new Vector3(x, 0f, zmin - k * cell), RoadYaw + 90f, 1f, false, Col.None);
                }
            foreach (float z in zLines)
                for (int k = 1; k <= BorderRings; k++)
                {
                    Spawn(roadModel, new Vector3(xmax + k * cell, 0f, z), RoadYaw, 1f, false, Col.None);
                    Spawn(roadModel, new Vector3(xmin - k * cell, 0f, z), RoadYaw, 1f, false, Col.None);
                }

            // ---- กำแพงล่องหนรอบแมป (กรอบสี่เหลี่ยม 4 ด้าน) กันเดินหลุดทุกด้าน ----
            float wallH = Mathf.Max(1.5f, cell * 0.5f);
            float th = cell * 0.4f;
            float wo = (BorderRings + 1) * cell;                 // กำแพงอยู่นอกแนวต้นไม้
            float X1 = xmin - wo, X2 = xmax + wo, Z1 = zmin - wo, Z2 = zmax + wo;
            float midX = (X1 + X2) * 0.5f, midZ = (Z1 + Z2) * 0.5f;
            float lenX = (X2 - X1) + th, lenZ = (Z2 - Z1) + th;
            Barrier(new Vector3(midX, 0f, Z2), lenX, th, wallH, null);   // เหนือ
            Barrier(new Vector3(midX, 0f, Z1), lenX, th, wallH, null);   // ใต้
            Barrier(new Vector3(X2, 0f, midZ), th, lenZ, wallH, null);   // ตะวันออก
            Barrier(new Vector3(X1, 0f, midZ), th, lenZ, wallH, null);   // ตะวันตก

            // ขยายพื้น (Ground) ให้คลุมถึงกำแพง — ไม่มีตึก/ต้นไม้ลอยเกินพื้น
            var ground = GameObject.Find("Ground");
            if (ground != null)
            {
                float ext = Mathf.Max(Mathf.Max(Mathf.Abs(X1), X2), Mathf.Max(Mathf.Abs(Z1), Z2)) + cell;
                float gscale = ext * 2f / 10f;   // Plane = 10 หน่วยต่อ scale 1
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(gscale, 1f, gscale);
            }

            // ---- 2) จัดขอบเป็นชั้น: ตึกฉากหลังเรียงแถวรอบ (หันเข้าเมือง) + ต้นไม้เรียงด้านหน้า ----
            float outR = BorderRings * cell;
            float ringOff = outR * 0.72f;                        // ระยะแถวตึกฉากหลังจากขอบเมือง
            float bs = Mathf.Max(bFoot * 1.15f, cell * 2f);      // ระยะห่างตึกในแถว
            int count = 0;

            // ตึกฉากหลัง — แถวเหนือ/ใต้ (ไล่ x) เว้นถนน
            for (float x = xmin; x <= xmax + 0.01f; x += bs)
                if (!NearAny(x, xLines, cell * 0.9f))
                {
                    SpawnBuilding(buildings, new Vector3(x, 0f, zmax + ringOff), 180f); count++;
                    SpawnBuilding(buildings, new Vector3(x, 0f, zmin - ringOff), 0f); count++;
                }
            // แถวตะวันออก/ตะวันตก (ไล่ z) เว้นมุม
            for (float z = zmin + bs; z <= zmax - bs + 0.01f; z += bs)
                if (!NearAny(z, zLines, cell * 0.9f))
                {
                    SpawnBuilding(buildings, new Vector3(xmax + ringOff, 0f, z), 270f); count++;
                    SpawnBuilding(buildings, new Vector3(xmin - ringOff, 0f, z), 90f); count++;
                }

            // ต้นไม้ด้านหน้า — เติมระหว่างเมืองกับแถวตึก (แนวสวนล้อม)
            float treeStep = Mathf.Max(cell * 0.9f, 3f);
            for (float gx = xmin - outR; gx <= xmax + outR + 0.01f; gx += treeStep)
                for (float gz = zmin - outR; gz <= zmax + outR + 0.01f; gz += treeStep)
                {
                    if (gx > xmin - cell && gx < xmax + cell && gz > zmin - cell && gz < zmax + cell) continue; // ในเมือง
                    float bd = Mathf.Max(Mathf.Max(gx - xmax, xmin - gx), Mathf.Max(gz - zmax, zmin - gz));      // ห่างขอบเมือง
                    if (bd > ringOff * 0.85f) continue;                                                          // เฉพาะด้านหน้าตึก
                    if (OnRoadStrip(gx, gz, xLines, zLines, xmin, xmax, zmin, zmax, cell)) continue;             // เว้นถนน

                    var pos = new Vector3(gx + Jit(treeStep * 0.35f), 0f, gz + Jit(treeStep * 0.35f));
                    int r = _rng.Next(100);
                    GameObject m; float mul = _natureMul; Col col = Col.Trunk;
                    if (r < 58) m = trees[_rng.Next(trees.Count)];
                    else if (r < 72 && bare.Count > 0) m = bare[_rng.Next(bare.Count)];
                    else if (r < 92 && bushes.Count > 0) { m = bushes[_rng.Next(bushes.Count)]; col = Col.None; }
                    else if (rocks.Count > 0) { m = rocks[_rng.Next(rocks.Count)]; mul *= 0.5f; col = Col.None; }
                    else m = trees[_rng.Next(trees.Count)];
                    mul *= 0.85f + (float)_rng.NextDouble() * 0.3f;    // สุ่มขนาดเล็กน้อย (ไม่เพี้ยน)
                    Spawn(m, pos, RandYaw(), mul, true, col);
                    count++;
                }

            Selection.activeGameObject = _root.gameObject;
            Debug.Log($"<color=lime>[Nisit] ✅ ขอบแมปเสร็จ: ต่อถนนออกไป + ตึกฉากหลัง/ต้นไม้ {count} จุด\n" +
                      "กดซ้ำได้ไม่ซ้อน | ปรับความกว้าง: BorderRings ในไฟล์</color>");
        }

        private static bool NearAny(float v, List<float> lines, float tol)
        {
            foreach (var l in lines) if (Mathf.Abs(v - l) < tol) return true;
            return false;
        }

        private static void SpawnBuilding(List<GameObject> buildings, Vector3 pos, float yaw)
        {
            if (buildings.Count == 0) return;
            float s = 0.9f + (float)_rng.NextDouble() * 0.3f;   // สม่ำเสมอ 0.9-1.2
            Spawn(buildings[_rng.Next(buildings.Count)], pos, yaw, s, true, Col.Box);
        }

        private static bool OnRoadStrip(float gx, float gz, List<float> xLines, List<float> zLines,
            float xmin, float xmax, float zmin, float zmax, float cell)
        {
            foreach (float x in xLines) if (Mathf.Abs(gx - x) < cell * 0.75f && (gz > zmax || gz < zmin)) return true;
            foreach (float z in zLines) if (Mathf.Abs(gz - z) < cell * 0.75f && (gx > xmax || gx < xmin)) return true;
            return false;
        }

        private enum Col { None, Box, Trunk }

        private static void Spawn(GameObject model, Vector3 pos, float yaw, float mul, bool ground, Col col)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = model.name;
            inst.transform.SetParent(_root);
            inst.transform.localScale = Vector3.one * GlobalScale * mul;
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            inst.transform.position = pos;
            if (ground) { var b = Bounds(inst); inst.transform.position += new Vector3(0f, -b.min.y, 0f); }
            if (col == Col.Box) CampusBuilder.AddBoxCollider(inst);
            else if (col == Col.Trunk) CampusBuilder.AddTrunkCollider(inst);
        }

        // กำแพงล่องหน (มีแต่ BoxCollider ไม่มีตัวโมเดล) — กันเดินตกแมป แต่มองไม่เห็น
        private static void Barrier(Vector3 pos, float sx, float sz, float h, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);   // มี BoxCollider มาในตัว
            go.name = "Boundary";
            go.transform.SetParent(_root);
            go.transform.localScale = new Vector3(sx, h, sz);
            go.transform.position = pos + Vector3.up * h * 0.5f;
            var mf = go.GetComponent<MeshFilter>(); if (mf != null) Object.DestroyImmediate(mf);
            var mr = go.GetComponent<MeshRenderer>(); if (mr != null) Object.DestroyImmediate(mr);  // ล่องหน
        }

        private static Material Mat(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh);
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            return m;
        }

        // ---------- helpers ----------
        private static float Jit(float a) => (float)(_rng.NextDouble() - 0.5) * 2f * a;
        private static float RandYaw() => (new[] { 0f, 90f, 180f, 270f })[_rng.Next(4)];

        private static List<float> Distinct(List<float> vals)
        {
            vals.Sort();
            var o = new List<float>();
            foreach (var v in vals) if (o.Count == 0 || Mathf.Abs(v - o[o.Count - 1]) > 0.5f) o.Add(v);
            return o;
        }
        private static float Min(List<float> l) { float m = l[0]; foreach (var v in l) m = Mathf.Min(m, v); return m; }
        private static float Max(List<float> l) { float m = l[0]; foreach (var v in l) m = Mathf.Max(m, v); return m; }

        private static List<GameObject> Pick(Dictionary<string, GameObject> lib, string prefix, string exclude)
        {
            var l = new List<GameObject>();
            foreach (var kv in lib)
                if (kv.Key.StartsWith(prefix) && (exclude == null || !kv.Key.Contains(exclude)) && !kv.Key.Contains("Singlesided"))
                    l.Add(kv.Value);
            l.Sort((a, b) => string.Compare(a.name, b.name));
            return l;
        }

        private static Dictionary<string, GameObject> Load(params string[] folders)
        {
            var d = new Dictionary<string, GameObject>();
            foreach (var g in AssetDatabase.FindAssets("t:Model", folders))
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null) d[m.name] = m;
            }
            return d;
        }

        private static float Height(GameObject m) => SizeOf(m).y;
        private static float Footprint(GameObject m) { var s = SizeOf(m); return Mathf.Max(s.x, s.z); }
        private static Vector3 SizeOf(GameObject prefab)
        {
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tmp.transform.position = Vector3.zero; tmp.transform.localScale = Vector3.one; tmp.transform.rotation = Quaternion.identity;
            var b = Bounds(tmp); Object.DestroyImmediate(tmp);
            return b.size;
        }
        private static Bounds Bounds(GameObject go)
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
