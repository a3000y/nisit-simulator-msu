using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using NisitSimulator.GEBuilding;

namespace NisitSimulator.EditorTools
{
    // สร้างหอพักนักศึกษา 3 ชั้น (เดินเข้าได้จริง) — เมนู Nisit > Dorm Building > Rebuild In Active Scene
    // ออกแบบด้วยขนาดจริง (เมตร) แล้ววางในฉากเกมที่ scale 0.75 ให้เข้ากับผู้เล่นเดิม (เหมือนตึก GE)
    //
    // ผัง (พิกัดท้องถิ่น): อาคาร x -11.2..11.2, z 0..15.2 · ทางเดินกลาง z 6.6..8.6 (กว้าง 2 ม.)
    //   ห้องพักแถวหน้า z 0..6.6 และแถวหลัง z 8.6..15.2 · ปีกละ 2 ห้อง/แถว → 8 ห้อง/ชั้น (กว้าง 3.8 ม.)
    //   แกนกลาง x -3.6..3.6: หน้า = โถงต้อนรับ (ชั้น 1) / มุมนั่งพัก (ชั้น 2–3) · หลัง = บันได (x -3.6..0) + ห้องซักผ้า/ห้องอ่านหนังสือ (x 0..3.6)
    public static class DormBuildingGenerator
    {
        public const float B = 0.3f, H = 3.2f, T = 0.2f, W = 0.2f, Wp = 0.1f;
        public const int Floors = 3;
        public static float FloorY(int i) => B + i * H;
        public static float WallH => H - T;
        const float Rise = 0.16f, Tread = 0.28f; const int Steps = 10;
        const float XL = -11.2f, XR = 11.2f, ZC1 = 6.6f, ZC2 = 8.6f, ZB = 15.2f, CoreX = 3.6f;
        static readonly float[] RoomX = { -9.3f, -5.5f, 5.5f, 9.3f };
        const float SA0 = -3.5f, SA1 = -2.0f, SB0 = -1.6f, SB1 = -0.1f, SZ0 = 10.2f;
        static float SLand => SZ0 + (Steps - 1) * Tread;

        const string MatDir = "Assets/_Project/Art/Materials/Dorm";
        const string PfDir = "Assets/_Project/Prefabs/Dorm";

        static Material mWall, mAccent, mTrim, mCorr, mRoomFloor, mBathFloor, mBathWall, mStair, mRail, mGlass, mDoor, mFrame, mWood,
            mMattress, mBlanket, mPillow, mCeramic, mMetal, mMirror, mSign, mSignDark, mLight, mWasher, mCork, mPlaza, mCurtain;
        static TMP_FontAsset thaiFont;
        static GameObject pfDoor, pfBed, pfWardrobe, pfDesk, pfToilet, pfSink, pfShower, pfWasher, pfMailbox, pfReception, pfRoomSign, pfRoom,
            pfLight, pfBench, pfChair, pfLamp, pfCouch, pfPlant, pfClock, pfBush;

        struct Op
        {
            public float a, b, bottom, top; public bool glass;
            public Op(float a, float b, float bottom, float top, bool glass) { this.a = a; this.b = b; this.bottom = bottom; this.top = top; this.glass = glass; }
        }

        [MenuItem("Nisit/Dorm Building/Rebuild In Active Scene")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            EnsureFolder("Assets/_Project/Art/Materials", "Dorm");
            EnsureFolder("Assets/_Project/Prefabs", "Dorm");
            thaiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/Mitr SDF.asset");
            MakeMaterials();
            LoadExisting();
            MakeFurniturePrefabs();
            MakeRoomPrefab();

            var old = GameObject.Find("Dorm_Building");
            var oldParent = old != null ? old.transform.parent : null;
            var oldPosition = old != null ? old.transform.position : Vector3.zero;
            var oldRotation = old != null ? old.transform.rotation : Quaternion.identity;
            var oldScale = old != null ? old.transform.localScale : Vector3.one;
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Dorm_Building").transform;
            root.SetParent(oldParent, false);
            root.SetPositionAndRotation(oldPosition, oldRotation);
            root.localScale = oldScale;
            BuildExterior(G("Exterior", root));
            BuildStructure(G("Structure", root));
            for (int i = 0; i < Floors; i++) BuildFloor(G("Floor_" + (i + 1), root), i);
            BuildStairwell(G("Stairwell", root));

            var cut = root.gameObject.AddComponent<GEBuildingCutaway>();
            cut.localMin = new Vector3(-11.8f, -1f, -0.6f); cut.localMax = new Vector3(11.8f, 11f, 15.8f);
            cut.firstFloorY = B; cut.floorHeight = H; cut.slabThickness = T; cut.floorLookAhead = 0.35f;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.GetComponentInParent<GEDoor>() == null) t.gameObject.isStatic = true;

            M46DayNightDormSetup.SetupDormBuilding(root);

            return $"Dorm_Building built: colliders={root.GetComponentsInChildren<Collider>().Length} lights={root.GetComponentsInChildren<Light>().Length} doors={root.GetComponentsInChildren<GEDoor>().Length} rooms={Floors * 8}";
        }

        // ------------------------------------------------------------ helpers
        public static void EnsureEntranceDoors(Transform building)
        {
            var lobby = building.Find("Floor_1/Common/Lobby");
            if (lobby == null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PfDir + "/DM_Door.prefab");
            if (prefab == null) throw new System.InvalidOperationException("ไม่พบ DM_Door.prefab");
            for (int i = 0; i < 2; i++)
            {
                string name = i == 0 ? "Door_Main_Left" : "Door_Main_Right";
                var point = lobby.Find(name);
                if (point == null) point = Inst(prefab, lobby, new Vector3(i == 0 ? -1.2f : 1.2f, B, 0f), i == 0 ? 0f : 180f, name).transform;
                var door = point.GetComponent<GEDoor>();
                door.startOpen = false;
                door.openAngle = i == 0 ? -90f : 90f;
                if (door.hinge != null) door.hinge.localRotation = Quaternion.identity;
                EditorUtility.SetDirty(door);
                PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            }
        }

        static void EnsureFolder(string parent, string name) { if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name); }

        static Transform G(string name, Transform parent, Vector3? pos = null, float rotY = 0f)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            t.localPosition = pos ?? Vector3.zero;
            t.localRotation = Quaternion.Euler(0f, rotY, 0f);
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material m, bool col = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name; g.transform.SetParent(parent, false);
            g.transform.localPosition = center; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!col) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
        static GameObject BoxMM(string name, Transform parent, Vector3 min, Vector3 max, Material m, bool col = true) => Box(name, parent, (min + max) * 0.5f, max - min, m, col);

        static GameObject Cyl(string name, Transform parent, Vector3 center, Vector3 size, Vector3 euler, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name; g.transform.SetParent(parent, false);
            g.transform.localPosition = center; g.transform.localEulerAngles = euler; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static BoxCollider RootCollider(GameObject go, Vector3 center, Vector3 size)
        {
            var bc = go.GetComponent<BoxCollider>();
            if (bc == null) bc = go.AddComponent<BoxCollider>();
            bc.center = center; bc.size = size; return bc;
        }

        static Material Mat(string name, Color c, float smooth, float metal = 0f)
        {
            string p = MatDir + "/M_Dorm_" + name + ".mat";
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, p); }
            m.shader = lit; m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", 1); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
        }

        static void MakeMaterials()
        {
            mWall = Mat("Wall", new Color(0.95f, 0.93f, 0.88f), 0.1f);
            mAccent = Mat("Accent", new Color(0.56f, 0.70f, 0.60f), 0.2f);
            mTrim = Mat("Trim", new Color(0.97f, 0.97f, 0.96f), 0.2f);
            mCorr = Mat("FloorTile", new Color(0.85f, 0.84f, 0.81f), 0.35f);
            mRoomFloor = Mat("RoomFloor", new Color(0.80f, 0.68f, 0.52f), 0.3f);
            mBathFloor = Mat("BathFloor", new Color(0.76f, 0.83f, 0.85f), 0.45f);
            mBathWall = Mat("BathWall", new Color(0.88f, 0.93f, 0.94f), 0.4f);
            mStair = Mat("Stair", new Color(0.74f, 0.72f, 0.68f), 0.25f);
            mRail = Mat("Rail", new Color(0.30f, 0.32f, 0.34f), 0.5f, 0.6f);
            mDoor = Mat("Door", new Color(0.72f, 0.56f, 0.40f), 0.3f);
            mFrame = Mat("Frame", new Color(0.92f, 0.92f, 0.92f), 0.4f, 0.3f);
            mWood = Mat("Wood", new Color(0.80f, 0.66f, 0.48f), 0.3f);
            mMattress = Mat("Mattress", new Color(0.96f, 0.96f, 0.94f), 0.1f);
            mBlanket = Mat("Blanket", new Color(0.62f, 0.74f, 0.85f), 0.1f);
            mPillow = Mat("Pillow", new Color(0.99f, 0.99f, 0.98f), 0.1f);
            mCeramic = Mat("Ceramic", new Color(0.97f, 0.97f, 0.97f), 0.8f);
            mMetal = Mat("Metal", new Color(0.65f, 0.67f, 0.70f), 0.6f, 0.7f);
            mMirror = Mat("Mirror", new Color(0.80f, 0.86f, 0.90f), 0.95f, 0.9f);
            mSign = Mat("Sign", new Color(0.36f, 0.54f, 0.46f), 0.3f);
            mSignDark = Mat("SignDark", new Color(0.22f, 0.25f, 0.27f), 0.3f);
            mWasher = Mat("Appliance", new Color(0.93f, 0.94f, 0.96f), 0.6f);
            mCork = Mat("Cork", new Color(0.74f, 0.58f, 0.40f), 0.05f);
            mPlaza = Mat("Paving", new Color(0.78f, 0.74f, 0.68f), 0.15f);
            mCurtain = Mat("Curtain", new Color(0.80f, 0.88f, 0.86f, 0.85f), 0.1f); MakeTransparent(mCurtain);
            mGlass = Mat("Glass", new Color(0.72f, 0.86f, 0.95f, 0.25f), 0.95f); MakeTransparent(mGlass);
            mLight = Mat("LightPanel", Color.white, 0.2f);
            mLight.EnableKeyword("_EMISSION"); mLight.SetColor("_EmissionColor", new Color(1f, 0.97f, 0.92f) * 1.4f);
            mLight.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.SaveAssets();
        }

        static void LoadExisting()
        {
            pfLight = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/GEBuilding/GE_CeilingLight.prefab");
            pfBench = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/GEBuilding/GE_Bench.prefab");
            pfChair = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Classroom/TeacherChair.prefab");
            pfLamp = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/KayKit_Furniture/lamp_table.fbx");
            pfCouch = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Couch_01.prefab");
            pfPlant = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_PotPlant_01.prefab");
            pfClock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Clock_01.prefab");
            pfBush = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Bush_01.prefab");
        }

        static TextMeshPro Text(string name, Transform parent, Vector3 pos, Vector2 size, string text, Color color, float maxSize = 3f)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            if (thaiFont != null) tmp.font = thaiFont;
            tmp.text = text; tmp.color = color; tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.05f; tmp.fontSizeMax = maxSize;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = size;
            go.transform.localPosition = pos;
            return tmp;
        }

        static Transform Plate(string name, Transform parent, Vector3 pos, float rotY, Vector2 size, string text, Material plate, float maxSize = 2f)
        {
            var s = G(name, parent, pos, rotY);
            Box("Plate", s, Vector3.zero, new Vector3(size.x, size.y, 0.03f), plate, false);
            Text("Label", s, new Vector3(0, 0, -0.02f), size * 0.9f, text, Color.white, maxSize);
            return s;
        }

        static GameObject Inst(GameObject prefab, Transform parent, Vector3 pos, float rotY, string name = null)
        {
            if (prefab == null) return null;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(0, rotY, 0);
            if (name != null) g.name = name;
            return g;
        }

        // วาง prefab แล้วย่อ/ขยายให้ด้านที่ยาวที่สุดเท่ากับ size
        static GameObject Fit(GameObject prefab, Transform parent, Vector3 pos, float rotY, float size, string name)
        {
            var g = Inst(prefab, parent, pos, rotY, name);
            if (g == null) return null;
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return g;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (maxDim > 0.001f) g.transform.localScale *= size / maxDim;
            return g;
        }

        static GameObject SavePrefab(GameObject temp, string name)
        {
            var pf = PrefabUtility.SaveAsPrefabAsset(temp, PfDir + "/" + name + ".prefab");
            Object.DestroyImmediate(temp);
            return pf;
        }

        // ผนังแนวแกน X ที่ระนาบ z=zc มีช่องประตู/หน้าต่าง
        static Transform WallX(Transform p, string name, float x0, float x1, float zc, float y0, float h, Material m, List<Op> ops = null, float th = W)
        {
            var g = G(name, p);
            ops = ops ?? new List<Op>(); ops.Sort((u, v) => u.a.CompareTo(v.a));
            float cur = x0; int n = 0;
            foreach (var o in ops)
            {
                if (o.a > cur + 0.001f) BoxMM("Seg_" + n++, g, new Vector3(cur, y0, zc - th / 2), new Vector3(o.a, y0 + h, zc + th / 2), m);
                if (o.bottom > 0.001f) BoxMM("Below_" + n, g, new Vector3(o.a, y0, zc - th / 2), new Vector3(o.b, y0 + o.bottom, zc + th / 2), m);
                if (o.top < h - 0.001f) BoxMM("Above_" + n, g, new Vector3(o.a, y0 + o.top, zc - th / 2), new Vector3(o.b, y0 + h, zc + th / 2), m);
                if (o.glass) Window(g, "Window_" + n, new Vector3((o.a + o.b) / 2, y0 + (o.bottom + o.top) / 2, zc), o.b - o.a, o.top - o.bottom, th, 0f);
                cur = o.b;
            }
            if (x1 > cur + 0.001f) BoxMM("Seg_" + n, g, new Vector3(cur, y0, zc - th / 2), new Vector3(x1, y0 + h, zc + th / 2), m);
            return g;
        }

        static Transform WallZ(Transform p, string name, float z0, float z1, float xc, float y0, float h, Material m, List<Op> ops = null, float th = W)
        {
            var g = G(name, p);
            ops = ops ?? new List<Op>(); ops.Sort((u, v) => u.a.CompareTo(v.a));
            float cur = z0; int n = 0;
            foreach (var o in ops)
            {
                if (o.a > cur + 0.001f) BoxMM("Seg_" + n++, g, new Vector3(xc - th / 2, y0, cur), new Vector3(xc + th / 2, y0 + h, o.a), m);
                if (o.bottom > 0.001f) BoxMM("Below_" + n, g, new Vector3(xc - th / 2, y0, o.a), new Vector3(xc + th / 2, y0 + o.bottom, o.b), m);
                if (o.top < h - 0.001f) BoxMM("Above_" + n, g, new Vector3(xc - th / 2, y0 + o.top, o.a), new Vector3(xc + th / 2, y0 + h, o.b), m);
                if (o.glass) Window(g, "Window_" + n, new Vector3(xc, y0 + (o.bottom + o.top) / 2, (o.a + o.b) / 2), o.b - o.a, o.top - o.bottom, th, 90f);
                cur = o.b;
            }
            if (z1 > cur + 0.001f) BoxMM("Seg_" + n, g, new Vector3(xc - th / 2, y0, cur), new Vector3(xc + th / 2, y0 + h, z1), m);
            return g;
        }

        static void Window(Transform p, string name, Vector3 center, float width, float height, float th, float rotY)
        {
            var w = G(name, p, center, rotY);
            Box("Glass", w, Vector3.zero, new Vector3(width, height, 0.03f), mGlass, true).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            float f = 0.06f, d = th + 0.02f;
            Box("Frame_Top", w, new Vector3(0, height / 2 - f / 2, 0), new Vector3(width, f, d), mTrim, false);
            Box("Frame_Bottom", w, new Vector3(0, -height / 2 + f / 2, 0), new Vector3(width, f, d), mTrim, false);
            Box("Frame_L", w, new Vector3(-width / 2 + f / 2, 0, 0), new Vector3(f, height, d), mTrim, false);
            Box("Frame_R", w, new Vector3(width / 2 - f / 2, 0, 0), new Vector3(f, height, d), mTrim, false);
            int panes = Mathf.Max(1, Mathf.RoundToInt(width / 1.0f));
            for (int i = 1; i < panes; i++) Box("Mullion_" + i, w, new Vector3(-width / 2 + width * i / panes, 0, 0), new Vector3(0.05f, height, 0.08f), mTrim, false);
        }

        static Transform Railing(Transform p, string name, Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a; float len = dir.magnitude;
            var r = G(name, p);
            r.position = (a + b) * 0.5f;
            r.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0, 90, 0);
            var bc = r.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.6f, 0); bc.size = new Vector3(len, 1.2f, 0.12f);
            Box("Glass", r, new Vector3(0, 0.5f, 0), new Vector3(len, 0.85f, 0.03f), mGlass, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box("HandRail", r, new Vector3(0, 1.05f, 0), new Vector3(len, 0.06f, 0.08f), mRail, false);
            Box("BaseRail", r, new Vector3(0, 0.04f, 0), new Vector3(len, 0.08f, 0.08f), mRail, false);
            return r;
        }

        // ราวตามแนวลาด: Collider เป็นท่อนแนวตั้งสั้น ๆ ไล่ตามความชัน (ปลายราวไม่ยื่นเข้าทางเดิน)
        static Transform SlopedRailing(Transform p, string name, Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a; float len = dir.magnitude;
            var r = G(name, p);
            r.position = (a + b) * 0.5f;
            r.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Box("Glass", r, new Vector3(0, 0.5f, 0), new Vector3(0.03f, 0.85f, len), mGlass, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box("HandRail", r, new Vector3(0, 0.98f, 0), new Vector3(0.08f, 0.06f, len + 0.1f), mRail, false);
            var cols = G(name + "_Colliders", p);
            Vector3 flat = new Vector3(dir.x, 0, dir.z); float hl = flat.magnitude;
            int segs = Mathf.Max(1, Mathf.CeilToInt(hl / 0.4f));
            Quaternion yaw = Quaternion.LookRotation(flat.normalized, Vector3.up);
            for (int s = 0; s < segs; s++)
            {
                Vector3 pa = Vector3.Lerp(a, b, (float)s / segs), pb = Vector3.Lerp(a, b, (float)(s + 1) / segs);
                float yLo = Mathf.Min(pa.y, pb.y) - 0.3f, yHi = Mathf.Max(pa.y, pb.y) + 1.1f;
                var seg = new GameObject(name + "_Col_" + s).transform; seg.SetParent(cols, false);
                Vector3 mid = (pa + pb) * 0.5f; seg.position = new Vector3(mid.x, (yLo + yHi) * 0.5f, mid.z); seg.rotation = yaw;
                seg.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.12f, yHi - yLo, hl / segs);
            }
            return r;
        }

        static Transform RampPlate(Transform p, string name, Vector3 a, Vector3 b, float width, Material visual = null, float thickness = 0.1f)
        {
            Vector3 dir = b - a; float len = dir.magnitude;
            var r = G(name, p);
            r.position = (a + b) * 0.5f;
            r.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            if (visual != null) Box("Surface", r, new Vector3(0, -thickness / 2, 0), new Vector3(width, thickness, len), visual, true);
            else { var bc = r.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, -thickness / 2, 0); bc.size = new Vector3(width, thickness, len); }
            return r;
        }

        // ------------------------------------------------------------ furniture prefabs
        static void MakeFurniturePrefabs()
        {
            // ประตู (ช่องเปิด 1.2 x 2.2 ม. ตามแกน X ท้องถิ่น · เปิดเข้า +Z) — ใช้ GEDoor + Trigger ให้ PlayerInteraction เดิมเจอ
            var d = new GameObject("DM_Door");
            int inter = LayerMask.NameToLayer("Interactable"); if (inter >= 0) d.layer = inter;
            var trig = d.AddComponent<BoxCollider>(); trig.isTrigger = true; trig.center = new Vector3(0.6f, 1.0f, 0f); trig.size = new Vector3(1.4f, 2.0f, 1.2f);
            var fr = G("Frame", d.transform);
            Box("Jamb_Hinge", fr, new Vector3(0.025f, 1.1f, 0), new Vector3(0.05f, 2.2f, W + 0.04f), mTrim, true);
            Box("Jamb_Latch", fr, new Vector3(1.175f, 1.1f, 0), new Vector3(0.05f, 2.2f, W + 0.04f), mTrim, true);
            Box("Head", fr, new Vector3(0.6f, 2.175f, 0), new Vector3(1.2f, 0.05f, W + 0.04f), mTrim, false);
            var hinge = G("Hinge", d.transform, new Vector3(0.06f, 0, 0));
            var leaf = Box("Leaf", hinge, new Vector3(0.54f, 1.075f, 0), new Vector3(1.07f, 2.12f, 0.045f), mDoor, true);
            if (inter >= 0) leaf.layer = inter;
            Box("Handle_A", hinge, new Vector3(0.95f, 1.0f, 0.05f), new Vector3(0.14f, 0.03f, 0.03f), mMetal, false);
            Box("Handle_B", hinge, new Vector3(0.95f, 1.0f, -0.05f), new Vector3(0.14f, 0.03f, 0.03f), mMetal, false);
            var door = d.AddComponent<GEDoor>(); door.hinge = hinge; door.openAngle = -90f; door.startOpen = false;
            hinge.localRotation = Quaternion.Euler(0, -90f, 0);
            pfDoor = SavePrefab(d, "DM_Door");

            // เตียงเดี่ยว 0.9 x 2.0 ม. (หัวเตียงด้าน +Z)
            var bed = new GameObject("DM_Bed");
            Box("Frame", bed.transform, new Vector3(0, 0.2f, 0), new Vector3(0.95f, 0.16f, 2.05f), mWood, false);
            foreach (var x in new[] { -0.43f, 0.43f }) foreach (var z in new[] { -0.98f, 0.98f })
                Box("Leg", bed.transform, new Vector3(x, 0.06f, z), new Vector3(0.06f, 0.12f, 0.06f), mWood, false);
            Box("Mattress", bed.transform, new Vector3(0, 0.37f, 0), new Vector3(0.9f, 0.18f, 2.0f), mMattress, false);
            Box("Blanket", bed.transform, new Vector3(0, 0.47f, -0.3f), new Vector3(0.93f, 0.04f, 1.38f), mBlanket, false);
            Box("Pillow", bed.transform, new Vector3(0, 0.5f, 0.72f), new Vector3(0.6f, 0.1f, 0.34f), mPillow, false);
            Box("Headboard", bed.transform, new Vector3(0, 0.5f, 1.02f), new Vector3(0.95f, 0.9f, 0.05f), mWood, false);
            RootCollider(bed, new Vector3(0, 0.5f, 0), new Vector3(0.95f, 1.0f, 2.05f));   // สูงพอไม่ให้ผู้เล่นปีนขึ้นเตียง
            pfBed = SavePrefab(bed, "DM_Bed");

            // ตู้เสื้อผ้า 1.0 x 2.0 x 0.6 (หน้าตู้ +Z)
            var wd = new GameObject("DM_Wardrobe");
            Box("Body", wd.transform, new Vector3(0, 1.0f, 0), new Vector3(1.0f, 2.0f, 0.6f), mWood, false);
            foreach (var x in new[] { -0.25f, 0.25f })
            {
                Box("DoorPanel", wd.transform, new Vector3(x, 1.02f, 0.305f), new Vector3(0.47f, 1.88f, 0.02f), mTrim, false);
                Box("Handle", wd.transform, new Vector3(x * 0.2f, 1.05f, 0.33f), new Vector3(0.02f, 0.2f, 0.02f), mMetal, false);
            }
            RootCollider(wd, new Vector3(0, 1.0f, 0), new Vector3(1.0f, 2.0f, 0.6f));
            pfWardrobe = SavePrefab(wd, "DM_Wardrobe");

            // โต๊ะอ่านหนังสือ 1.2 x 0.55 (ผู้ใช้นั่งด้าน -Z) + โคมไฟ KayKit
            var dk = new GameObject("DM_StudyDesk");
            Box("Top", dk.transform, new Vector3(0, 0.74f, 0), new Vector3(1.2f, 0.04f, 0.55f), mWood, false);
            Box("Side_L", dk.transform, new Vector3(-0.58f, 0.36f, 0), new Vector3(0.04f, 0.72f, 0.53f), mTrim, false);
            Box("Drawers", dk.transform, new Vector3(0.4f, 0.36f, 0), new Vector3(0.4f, 0.72f, 0.53f), mTrim, false);
            Box("Shelf", dk.transform, new Vector3(0, 1.2f, 0.2f), new Vector3(1.1f, 0.03f, 0.22f), mWood, false);
            if (pfLamp != null) Fit(pfLamp, dk.transform, new Vector3(-0.38f, 0.76f, 0.12f), 0f, 0.38f, "DeskLamp");
            RootCollider(dk, new Vector3(0, 0.5f, 0), new Vector3(1.2f, 1.0f, 0.55f));
            pfDesk = SavePrefab(dk, "DM_StudyDesk");

            // สุขภัณฑ์: ชักโครก (ด้านนั่งหัน +Z, ถังน้ำด้าน -Z)
            var tl = new GameObject("DM_Toilet");
            Box("Bowl", tl.transform, new Vector3(0, 0.2f, 0.05f), new Vector3(0.38f, 0.4f, 0.48f), mCeramic, false);
            Box("Seat", tl.transform, new Vector3(0, 0.42f, 0.05f), new Vector3(0.4f, 0.04f, 0.5f), mCeramic, false);
            Box("Tank", tl.transform, new Vector3(0, 0.62f, -0.27f), new Vector3(0.42f, 0.42f, 0.17f), mCeramic, false);
            Box("FlushButton", tl.transform, new Vector3(0, 0.84f, -0.27f), new Vector3(0.08f, 0.02f, 0.05f), mMetal, false);
            RootCollider(tl, new Vector3(0, 0.45f, -0.05f), new Vector3(0.45f, 0.9f, 0.7f));
            pfToilet = SavePrefab(tl, "DM_Toilet");

            // อ่างล้างหน้า + กระจก (ผู้ใช้ยืนด้าน +Z)
            var sk = new GameObject("DM_BathSink");
            Box("Vanity", sk.transform, new Vector3(0, 0.4f, 0), new Vector3(0.6f, 0.8f, 0.42f), mTrim, false);
            Box("Basin", sk.transform, new Vector3(0, 0.83f, 0.02f), new Vector3(0.5f, 0.06f, 0.36f), mCeramic, false);
            Box("Tap", sk.transform, new Vector3(0, 0.93f, -0.14f), new Vector3(0.04f, 0.14f, 0.04f), mMetal, false);
            Box("Mirror", sk.transform, new Vector3(0, 1.45f, -0.2f), new Vector3(0.55f, 0.7f, 0.02f), mMirror, false);
            RootCollider(sk, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 1.0f, 0.42f));
            pfSink = SavePrefab(sk, "DM_BathSink");

            // ฝักบัว: ถาดรองพื้น + ฝักบัว + ม่าน (ไม่มี Collider ให้เดินเข้าได้)
            var sh = new GameObject("DM_Shower");
            Box("Tray", sh.transform, new Vector3(0, 0.02f, 0), new Vector3(0.7f, 0.04f, 1.0f), mBathFloor, false);
            Box("Pipe", sh.transform, new Vector3(0.33f, 1.4f, 0), new Vector3(0.03f, 1.2f, 0.03f), mMetal, false);
            Cyl("Head", sh.transform, new Vector3(0.27f, 2.0f, 0), new Vector3(0.18f, 0.02f, 0.18f), new Vector3(0, 0, 90f), mMetal);
            Box("Rod", sh.transform, new Vector3(-0.35f, 2.05f, 0), new Vector3(0.02f, 0.02f, 1.0f), mMetal, false);
            Box("Curtain", sh.transform, new Vector3(-0.35f, 1.3f, 0.2f), new Vector3(0.01f, 1.45f, 0.6f), mCurtain, false);
            pfShower = SavePrefab(sh, "DM_Shower");

            // เครื่องซักผ้าฝาหน้า (หน้าเครื่อง +Z)
            var wm = new GameObject("DM_WashingMachine");
            Box("Body", wm.transform, new Vector3(0, 0.43f, 0), new Vector3(0.6f, 0.85f, 0.6f), mWasher, false);
            Box("Panel", wm.transform, new Vector3(0, 0.8f, 0.301f), new Vector3(0.58f, 0.1f, 0.01f), mSignDark, false);
            Cyl("DoorRing", wm.transform, new Vector3(0, 0.42f, 0.305f), new Vector3(0.38f, 0.01f, 0.38f), new Vector3(90f, 0, 0), mMetal);
            Cyl("DoorGlass", wm.transform, new Vector3(0, 0.42f, 0.31f), new Vector3(0.3f, 0.01f, 0.3f), new Vector3(90f, 0, 0), mSignDark);
            RootCollider(wm, new Vector3(0, 0.43f, 0), new Vector3(0.6f, 0.86f, 0.6f));
            pfWasher = SavePrefab(wm, "DM_WashingMachine");

            // ตู้จดหมายแบบช่อง 6 x 4 (หน้าตู้ +Z)
            var mb = new GameObject("DM_MailboxWall");
            Box("Cabinet", mb.transform, new Vector3(0, 1.05f, 0), new Vector3(2.4f, 1.4f, 0.35f), mMetal, false);
            for (int c = 0; c < 6; c++) for (int r = 0; r < 4; r++)
                    Box("Slot_" + r + "_" + c, mb.transform, new Vector3(-1.0f + c * 0.4f, 0.55f + r * 0.33f, 0.18f), new Vector3(0.36f, 0.29f, 0.01f), mTrim, false);
            Text("Label", mb.transform, new Vector3(0, 1.95f, 0.2f), new Vector2(2.2f, 0.25f), "ตู้จดหมาย  MAILBOX", new Color(0.25f, 0.4f, 0.33f), 2f).transform.localRotation = Quaternion.Euler(0, 180f, 0);
            RootCollider(mb, new Vector3(0, 0.9f, 0), new Vector3(2.4f, 1.8f, 0.35f));
            pfMailbox = SavePrefab(mb, "DM_MailboxWall");

            // โต๊ะเจ้าหน้าที่ (ด้านผู้มาติดต่อ +Z)
            var rc = new GameObject("DM_ReceptionDesk");
            Box("Counter", rc.transform, new Vector3(0, 0.5f, 0), new Vector3(2.0f, 1.0f, 0.6f), mWood, false);
            Box("FrontPanel", rc.transform, new Vector3(0, 0.5f, 0.305f), new Vector3(1.9f, 0.8f, 0.02f), mAccent, false);
            Box("Top", rc.transform, new Vector3(0, 1.03f, 0.05f), new Vector3(2.1f, 0.05f, 0.75f), mTrim, false);
            Box("Monitor", rc.transform, new Vector3(0.4f, 1.25f, -0.1f), new Vector3(0.45f, 0.3f, 0.03f), mSignDark, false);
            Text("Label", rc.transform, new Vector3(0, 0.6f, 0.32f), new Vector2(1.7f, 0.22f), "เจ้าหน้าที่หอพัก", Color.white, 2f).transform.localRotation = Quaternion.Euler(0, 180f, 0);
            RootCollider(rc, new Vector3(0, 0.55f, 0.05f), new Vector3(2.1f, 1.1f, 0.75f));
            pfReception = SavePrefab(rc, "DM_ReceptionDesk");

            // ป้ายหมายเลขห้อง
            var rs = new GameObject("DM_RoomSign");
            Box("Plate", rs.transform, Vector3.zero, new Vector3(0.42f, 0.24f, 0.03f), mSign, false);
            Text("Label", rs.transform, new Vector3(0, 0, -0.02f), new Vector2(0.38f, 0.2f), "000\nห้องพัก", Color.white, 1.2f);
            pfRoomSign = SavePrefab(rs, "DM_RoomSign");
        }

        // ห้องพัก 1 ห้อง (กว้าง 3.8 ลึก 6.6) — จุดกำเนิดที่กึ่งกลางผนังฝั่งทางเดิน · ห้องอยู่ด้าน +Z
        //   ทางเข้า x -1.6..-0.4 · ห้องน้ำ x 0..1.8, z 0.1..2.3 (ประตูบนผนัง x=0) · ส่วนนอน z 2.3..6.5
        static void MakeRoomPrefab()
        {
            var r = new GameObject("DM_DormRoom");
            float h = WallH;
            var fl = G("Floors", r.transform);
            BoxMM("Floor_Room", fl, new Vector3(-1.8f, 0.004f, 2.35f), new Vector3(1.8f, 0.012f, 6.5f), mRoomFloor, false);
            BoxMM("Floor_Entry", fl, new Vector3(-1.8f, 0.004f, 0.1f), new Vector3(-0.05f, 0.012f, 2.35f), mRoomFloor, false);
            BoxMM("Floor_Bath", fl, new Vector3(0.05f, 0.004f, 0.1f), new Vector3(1.8f, 0.012f, 2.25f), mBathFloor, false);

            var bath = G("Bathroom", r.transform);
            var bw = G("Walls", bath);
            BoxMM("Wall_West_A", bw, new Vector3(-0.05f, 0, 0.1f), new Vector3(0.05f, h, 0.6f), mBathWall);
            BoxMM("Wall_West_B", bw, new Vector3(-0.05f, 0, 1.8f), new Vector3(0.05f, h, 2.35f), mBathWall);
            BoxMM("Wall_West_Above", bw, new Vector3(-0.05f, 2.2f, 0.6f), new Vector3(0.05f, h, 1.8f), mBathWall);
            BoxMM("Wall_North", bw, new Vector3(0.05f, 0, 2.25f), new Vector3(1.8f, h, 2.35f), mBathWall);
            Inst(pfDoor, bath, new Vector3(0f, 0, 1.8f), 90f, "Door_Bathroom");
            Inst(pfToilet, bath, new Vector3(1.43f, 0, 0.45f), -90f, "Toilet");
            Inst(pfSink, bath, new Vector3(0.55f, 0, 0.32f), 0f, "Sink");
            Inst(pfShower, bath, new Vector3(1.45f, 0, 1.72f), 0f, "Shower");
            var bl = G("BathLight", bath, new Vector3(0.9f, h - 0.15f, 1.2f));
            var l = bl.gameObject.AddComponent<Light>(); l.type = LightType.Point; l.range = 2.6f; l.intensity = 1.2f; l.color = new Color(1f, 0.97f, 0.92f); l.shadows = LightShadows.None;

            Inst(pfDoor, r.transform, new Vector3(-1.6f, 0, 0f), 0f, "Door_Entry");

            var fu = G("Furniture", r.transform);
            Inst(pfWardrobe, fu, new Vector3(-1.5f, 0, 2.9f), 90f, "Wardrobe_A");
            Inst(pfWardrobe, fu, new Vector3(-1.5f, 0, 3.95f), 90f, "Wardrobe_B");
            Inst(pfBed, fu, new Vector3(-1.33f, 0, 5.47f), 0f, "Bed_A");
            Inst(pfBed, fu, new Vector3(1.33f, 0, 5.47f), 0f, "Bed_B");
            Inst(pfDesk, fu, new Vector3(0f, 0, 6.2f), 0f, "Desk_Window");
            Inst(pfChair, fu, new Vector3(0f, 0, 5.55f), 0f, "Chair_Window");
            Inst(pfDesk, fu, new Vector3(1.52f, 0, 3.0f), 90f, "Desk_Side");
            Inst(pfChair, fu, new Vector3(0.88f, 0, 3.0f), 90f, "Chair_Side");

            Inst(pfLight, r.transform, new Vector3(0, h - 0.03f, 4.3f), 90f, "CeilingLight");
            pfRoom = SavePrefab(r, "DM_DormRoom");
        }

        // ------------------------------------------------------------ exterior
        static void BuildExterior(Transform ex)
        {
            var porch = G("EntrancePorch", ex);
            BoxMM("Porch", porch, new Vector3(-2.6f, 0, -1.3f), new Vector3(2.6f, B, -0.1f), mCorr);
            BoxMM("Step", porch, new Vector3(-2.0f, 0, -1.65f), new Vector3(2.0f, 0.15f, -1.3f), mStair);
            var ramp = G("AccessRamp", ex);
            float rampEnd = 2.6f + B * 12f;
            RampPlate(ramp, "RampSurface", new Vector3(rampEnd, 0.02f, -0.75f), new Vector3(2.6f, B, -0.75f), 1.0f, mStair, 0.15f);
            SlopedRailing(ramp, "Rail_Outer", new Vector3(rampEnd, 0.02f, -1.27f), new Vector3(2.6f, B, -1.27f));

            var canopy = G("EntranceCanopy", ex);
            float cy = B + 2.65f;
            BoxMM("CanopySlab", canopy, new Vector3(-2.5f, cy, -1.6f), new Vector3(2.5f, cy + 0.2f, -0.1f), mTrim, false);
            BoxMM("Fascia", canopy, new Vector3(-2.5f, cy - 0.35f, -1.68f), new Vector3(2.5f, cy + 0.2f, -1.58f), mAccent, false);
            foreach (var x in new[] { -2.25f, 2.25f }) BoxMM("Column", canopy, new Vector3(x - 0.1f, B, -1.28f), new Vector3(x + 0.1f, cy, -1.08f), mTrim);
            var sg = G("Sign_Entrance", canopy, new Vector3(0, cy - 0.07f, -1.7f));
            Text("Title", sg, new Vector3(0, 0.08f, 0), new Vector2(4.6f, 0.32f), "หอพักนักศึกษา", Color.white, 5f);
            Text("Sub", sg, new Vector3(0, -0.17f, 0), new Vector2(4.4f, 0.14f), "STUDENT DORMITORY", new Color(0.92f, 0.96f, 0.93f), 2f);

            var mono = G("Sign_Standing", ex, new Vector3(-6.1f, 0, -0.85f), 0f);   // ชิดอาคาร ไม่ขวางทางเดินเชื่อม
            BoxMM("Base", mono, new Vector3(-1.3f, 0, -0.18f), new Vector3(1.3f, 1.0f, 0.18f), mSignDark);
            Box("AccentBar", mono, new Vector3(0, 1.02f, 0), new Vector3(2.7f, 0.05f, 0.4f), mAccent, false);
            Text("Title", mono, new Vector3(0, 0.62f, -0.2f), new Vector2(2.4f, 0.38f), "หอพักนักศึกษา", Color.white, 4f);
            Text("Sub", mono, new Vector3(0, 0.3f, -0.2f), new Vector2(2.4f, 0.16f), "Student Dormitory", new Color(0.85f, 0.9f, 0.88f), 2f);

            var land = G("Landscaping", ex);
            if (pfBush != null) foreach (var x in new[] { -10.2f, -8.2f, -4.0f, 8.0f, 10.2f }) Inst(pfBush, land, new Vector3(x, 0, -0.75f), 0f, "Bush");
            if (pfPlant != null) { Inst(pfPlant, land, new Vector3(-2.9f, 0, -1.2f), 0f, "PotPlant"); }
        }

        static void BuildStructure(Transform s)
        {
            var slabs = G("FloorSlabs", s);
            BoxMM("Plinth", slabs, new Vector3(XL - 0.1f, 0, -0.1f), new Vector3(XR + 0.1f, B, ZB + 0.1f), mCorr);
            for (int i = 1; i < Floors; i++)
            {
                float y = FloorY(i);
                var f = G("Slab_Floor" + (i + 1), slabs);
                BoxMM("Front", f, new Vector3(XL - 0.1f, y - T, -0.1f), new Vector3(XR + 0.1f, y, SZ0), mCorr);
                BoxMM("BackWest", f, new Vector3(XL - 0.1f, y - T, SZ0), new Vector3(-CoreX, y, ZB + 0.1f), mCorr);
                BoxMM("BackEast", f, new Vector3(0f, y - T, SZ0), new Vector3(XR + 0.1f, y, ZB + 0.1f), mCorr);
                BoxMM("Band", f, new Vector3(XL - 0.12f, y - T, -0.12f), new Vector3(XR + 0.12f, y + 0.02f, -0.1f), mAccent, false);
            }
            float roofY = FloorY(Floors);
            var roof = G("Roof", s);
            BoxMM("RoofSlab", roof, new Vector3(XL - 0.3f, roofY - T, -0.3f), new Vector3(XR + 0.3f, roofY, ZB + 0.3f), mTrim);
            BoxMM("Parapet_Front", roof, new Vector3(XL - 0.3f, roofY, -0.3f), new Vector3(XR + 0.3f, roofY + 0.7f, -0.1f), mAccent);
            BoxMM("Parapet_Back", roof, new Vector3(XL - 0.3f, roofY, ZB + 0.1f), new Vector3(XR + 0.3f, roofY + 0.7f, ZB + 0.3f), mWall);
            BoxMM("Parapet_W", roof, new Vector3(XL - 0.3f, roofY, -0.3f), new Vector3(XL - 0.1f, roofY + 0.7f, ZB + 0.3f), mWall);
            BoxMM("Parapet_E", roof, new Vector3(XR + 0.1f, roofY, -0.3f), new Vector3(XR + 0.3f, roofY + 0.7f, ZB + 0.3f), mWall);
            var rsgn = G("Sign_Roof", roof, new Vector3(0, roofY + 0.35f, -0.32f));
            Text("Title", rsgn, Vector3.zero, new Vector2(8f, 0.55f), "หอพักนักศึกษา  ·  STUDENT DORMITORY", Color.white, 6f);
        }

        // ------------------------------------------------------------ floors
        static void BuildFloor(Transform fl, int i)
        {
            float y = FloorY(i), h = WallH; int f = i + 1;
            var walls = G("Walls", fl); var rooms = G("Rooms", fl); var common = G("Common", fl); var lights = G("Lights", fl); var signs = G("Signs", fl);

            // ผนังหน้า (z=0)
            var front = new List<Op>();
            for (int k = 0; k < RoomX.Length; k++) front.Add(new Op(RoomX[k] - 1.0f, RoomX[k] + 1.0f, 0.9f, 2.2f, true));
            if (i == 0) { front.Add(new Op(-1.2f, 1.2f, 0f, 2.6f, false)); front.Add(new Op(-3.2f, -1.7f, 0.9f, 2.4f, true)); front.Add(new Op(1.7f, 3.2f, 0.9f, 2.4f, true)); }
            else front.Add(new Op(-3.0f, 3.0f, 0.9f, 2.4f, true));
            WallX(walls, "Wall_Front", XL, XR, 0f, y, h, mWall, front);
            // ผนังหลัง (z=15.2) — ช่วงบันไดเป็นผนังสูงต่อเนื่องใน Stairwell
            var backW = new List<Op>(); var backE = new List<Op>();
            for (int k = 0; k < RoomX.Length; k++) (RoomX[k] < 0 ? backW : backE).Add(new Op(RoomX[k] - 1.0f, RoomX[k] + 1.0f, 0.9f, 2.2f, true));
            backE.Add(new Op(1.0f, 2.6f, 1.2f, 2.4f, true));
            WallX(walls, "Wall_Back_West", XL, -CoreX, ZB, y, h, mWall, backW);
            WallX(walls, "Wall_Back_East", 0f, XR, ZB, y, h, mWall, backE);
            // ผนังข้าง (หน้าต่างปลายทางเดิน)
            WallZ(walls, "Wall_Side_West", 0f, ZB, XL, y, h, mWall, new List<Op> { new Op(6.9f, 8.3f, 0.9f, 2.4f, true) });
            WallZ(walls, "Wall_Side_East", 0f, ZB, XR, y, h, mWall, new List<Op> { new Op(6.9f, 8.3f, 0.9f, 2.4f, true) });
            // ผนังทางเดิน + ช่องประตูห้อง
            var c1W = new List<Op>(); var c1E = new List<Op>(); var c2W = new List<Op>(); var c2E = new List<Op>();
            foreach (var cx in RoomX)
            {
                (cx < 0 ? c1W : c1E).Add(new Op(cx + 0.4f, cx + 1.6f, 0f, 2.2f, false));   // แถวหน้า (หมุน 180)
                (cx < 0 ? c2W : c2E).Add(new Op(cx - 1.6f, cx - 0.4f, 0f, 2.2f, false));   // แถวหลัง
            }
            WallX(walls, "Wall_Corridor_S_West", XL, -CoreX, ZC1, y, h, mWall, c1W);
            WallX(walls, "Wall_Corridor_S_East", CoreX, XR, ZC1, y, h, mWall, c1E);
            WallX(walls, "Wall_Corridor_N_West", XL, -CoreX, ZC2, y, h, mWall, c2W);
            WallX(walls, "Wall_Corridor_N_East", CoreX, XR, ZC2, y, h, mWall, c2E);
            WallX(walls, "Wall_Corridor_N_Core", 0f, CoreX, ZC2, y, h, mWall, new List<Op> { new Op(0.4f, 1.6f, 0f, 2.2f, false) });
            // ผนังกั้นห้อง / แกนกลาง
            foreach (var x in new[] { -7.4f, 7.4f })
            {
                WallZ(walls, "Wall_Partition_S_" + x, 0.1f, ZC1 - 0.1f, x, y, h, mWall);
                WallZ(walls, "Wall_Partition_N_" + x, ZC2 + 0.1f, ZB - 0.1f, x, y, h, mWall);
            }
            WallZ(walls, "Wall_Core_West_S", 0.1f, ZC1 - 0.1f, -CoreX, y, h, mWall);
            WallZ(walls, "Wall_Core_East_S", 0.1f, ZC1 - 0.1f, CoreX, y, h, mWall);
            WallZ(walls, "Wall_Core_East_N", ZC2 + 0.1f, ZB - 0.1f, CoreX, y, h, mWall);

            // ห้องพัก 8 ห้อง/ชั้น
            int n = 1;
            foreach (bool back in new[] { false, true })
                foreach (var cx in RoomX)
                {
                    string code = $"{f}{n:00}";
                    var room = Inst(pfRoom, rooms, new Vector3(cx, y, back ? ZC2 : ZC1), back ? 0f : 180f, "Room_" + code);
                    var sign = Inst(pfRoomSign, signs, back ? new Vector3(cx + 0.45f, y + 1.6f, ZC2 - 0.115f) : new Vector3(cx - 0.45f, y + 1.6f, ZC1 + 0.115f), back ? 0f : 180f, "Sign_" + code);
                    sign.GetComponentInChildren<TextMeshPro>().text = code + "\nห้องพัก";
                    n++;
                }

            // แกนกลาง
            if (i == 0) BuildLobby(common, y); else BuildLounge(common, y, f);
            var back2 = G(i == 0 ? "Laundry" : "StudyRoom", common);
            Inst(pfDoor, back2, new Vector3(0.4f, y, ZC2), 0f, "Door");
            if (i == 0)
            {
                for (int k = 0; k < 4; k++) Inst(pfWasher, back2, new Vector3(0.55f + k * 0.8f, y, ZB - 0.45f), 180f, "WashingMachine_" + (k + 1));
                BoxMM("FoldingTable", back2, new Vector3(2.95f, y, 10.0f), new Vector3(3.5f, y + 0.85f, 12.4f), mTrim);
                Inst(pfBench, back2, new Vector3(1.0f, y, 11.2f), -90f, "Bench");
                Plate("Sign_Laundry", signs, new Vector3(2.5f, y + 1.7f, ZC2 - 0.115f), 0f, new Vector2(1.1f, 0.3f), "ห้องซักผ้า  LAUNDRY", mSign, 1.5f);
            }
            else
            {
                BoxMM("Table", back2, new Vector3(1.2f, y, 11.6f), new Vector3(2.6f, y + 0.75f, 12.6f), mWood);
                Inst(pfChair, back2, new Vector3(1.55f, y, 11.05f), 0f, "Chair_1");
                Inst(pfChair, back2, new Vector3(2.25f, y, 11.05f), 0f, "Chair_2");
                Inst(pfChair, back2, new Vector3(1.55f, y, 13.15f), 180f, "Chair_3");
                Inst(pfChair, back2, new Vector3(2.25f, y, 13.15f), 180f, "Chair_4");
                Plate("Sign_Study", signs, new Vector3(2.5f, y + 1.7f, ZC2 - 0.115f), 0f, new Vector2(1.1f, 0.3f), "ห้องอ่านหนังสือ", mSign, 1.5f);
            }
            Inst(pfLight, back2, new Vector3(1.8f, y + h - 0.03f, 12.0f), 0f, "CeilingLight");

            // ป้ายบอกชั้น (ผนังโถง/มุมพัก ฝั่งตะวันตก หันเข้าโถง)
            var fs = G("Sign_Floor", signs, new Vector3(-CoreX + 0.115f, y + 1.8f, 5.6f), -90f);
            Box("Plate", fs, Vector3.zero, new Vector3(1.3f, 0.6f, 0.03f), mSign, false);
            Text("Label", fs, new Vector3(0, 0.07f, -0.02f), new Vector2(1.2f, 0.34f), "ชั้น " + f, Color.white, 4f);
            Text("Sub", fs, new Vector3(0, -0.18f, -0.02f), new Vector2(1.2f, 0.14f), $"ห้อง {f}01 – {f}08", new Color(0.9f, 0.95f, 0.92f), 1.5f);

            // ไฟทางเดิน / แกนกลาง
            float ly = y + h - 0.03f;
            foreach (var x in new[] { -9.3f, -5.5f, 5.5f, 9.3f }) Inst(pfLight, lights, new Vector3(x, ly, 7.6f), 0f, "CorridorLight");
            Inst(pfLight, lights, new Vector3(-1.8f, ly, 3.3f), 0f, "CoreLight");
            Inst(pfLight, lights, new Vector3(1.8f, ly, 3.3f), 0f, "CoreLight");
            Inst(pfLight, lights, new Vector3(-1.8f, ly, 9.4f), 0f, "StairLandingLight");
        }

        static void BuildLobby(Transform common, float y)
        {
            var lb = G("Lobby", common);
            Inst(pfReception, lb, new Vector3(-2.75f, y, 3.6f), 90f, "ReceptionDesk");
            Inst(pfChair, lb, new Vector3(-3.25f, y, 3.6f), 90f, "StaffChair");
            Inst(pfMailbox, lb, new Vector3(3.31f, y, 2.4f), -90f, "MailboxWall");
            if (pfCouch != null) Inst(pfCouch, lb, new Vector3(2.92f, y, 5.25f), -90f, "Couch");
            BoxMM("CoffeeTable", lb, new Vector3(1.55f, y, 4.75f), new Vector3(2.0f, y + 0.42f, 5.75f), mWood);
            if (pfPlant != null) { Inst(pfPlant, lb, new Vector3(-3.15f, y, 0.55f), 0f, "PotPlant"); Inst(pfPlant, lb, new Vector3(3.15f, y, 0.55f), 0f, "PotPlant"); }
            if (pfClock != null) Inst(pfClock, lb, new Vector3(0f, y + 2.8f, 0.1f), 0f, "WallClock");
            // บานประตูกระจกเลื่อนที่เปิดค้างไว้ (ตกแต่ง — ไม่ขวางทางเข้า)
            Box("SlidingGlass_L", lb, new Vector3(-1.75f, y + 1.3f, 0.17f), new Vector3(1.1f, 2.5f, 0.03f), mGlass, false);
            Box("SlidingGlass_R", lb, new Vector3(1.75f, y + 1.3f, 0.17f), new Vector3(1.1f, 2.5f, 0.03f), mGlass, false);
            var ws = G("Sign_Welcome", lb, new Vector3(-CoreX + 0.115f, y + 2.25f, 3.6f), -90f);
            Box("Plate", ws, Vector3.zero, new Vector3(2.3f, 0.42f, 0.03f), mSign, false);
            Text("Label", ws, new Vector3(0, 0, -0.02f), new Vector2(2.2f, 0.36f), "หอพักนักศึกษา · ยินดีต้อนรับ", Color.white, 2.5f);
            var nb = G("NoticeBoard", lb, new Vector3(CoreX - 0.115f, y + 1.6f, 5.2f), 90f);
            Box("Cork", nb, Vector3.zero, new Vector3(1.2f, 0.8f, 0.02f), mCork, false);
            Box("Frame", nb, new Vector3(0, 0, 0.005f), new Vector3(1.26f, 0.86f, 0.02f), mWood, false);
        }

        static void BuildLounge(Transform common, float y, int f)
        {
            var lg = G("Lounge", common);
            if (pfCouch != null) Inst(pfCouch, lg, new Vector3(0f, y, 0.85f), 0f, "Couch");
            BoxMM("CoffeeTable", lg, new Vector3(-0.6f, y, 1.85f), new Vector3(0.6f, y + 0.42f, 2.35f), mWood);
            Inst(pfBench, lg, new Vector3(-2.7f, y, 2.6f), -90f, "Bench_1");
            Inst(pfBench, lg, new Vector3(2.7f, y, 2.6f), 90f, "Bench_2");
            if (pfPlant != null) { Inst(pfPlant, lg, new Vector3(-3.15f, y, 0.55f), 0f, "PotPlant"); Inst(pfPlant, lg, new Vector3(3.15f, y, 0.55f), 0f, "PotPlant"); }
        }

        // ------------------------------------------------------------ stairs (x -3.6..0, z 8.6..15.2)
        static void BuildStairwell(Transform st)
        {
            float top = FloorY(Floors) - T;
            var walls = G("Walls", st);
            BoxMM("Wall_West", walls, new Vector3(-CoreX - 0.1f, B, ZC2 + 0.1f), new Vector3(-CoreX + 0.1f, top, ZB + 0.1f), mWall);
            BoxMM("Wall_East", walls, new Vector3(-0.1f, B, ZC2 + 0.1f), new Vector3(0.1f, top, ZB + 0.1f), mWall);
            for (int i = 0; i < Floors; i++)
            {
                float y = FloorY(i), hh = (i == Floors - 1) ? WallH : H;
                var ops = new List<Op> { i < Floors - 1 ? new Op(-3.0f, -0.6f, 2.3f, 3.0f, true) : new Op(-3.0f, -0.6f, 0.9f, 2.4f, true) };
                WallX(walls, "Wall_Back_" + (i + 1), -CoreX + 0.1f, -0.1f, ZB, y, hh, mWall, ops);
                Plate("Sign_StairFloor_" + (i + 1), st, new Vector3(-0.115f, y + 1.8f, 9.4f), 90f, new Vector2(0.9f, 0.45f), "ชั้น " + (i + 1) + "\nFLOOR " + (i + 1), mSign, 3f);
            }
            float land = SLand;
            for (int i = 0; i < Floors - 1; i++)
            {
                float y = FloorY(i);
                var lv = G($"Flight_{i + 1}to{i + 2}", st);
                var fa = G("FlightA", lv);
                for (int k = 0; k < Steps - 1; k++)
                {
                    float z0 = SZ0 + k * Tread, ty = y + (k + 1) * Rise;
                    BoxMM("Step_" + (k + 1), fa, new Vector3(SA0, ty - 0.35f, z0), new Vector3(SA1, ty, z0 + Tread), mStair);
                }
                RampPlate(fa, "RampCollider", new Vector3((SA0 + SA1) / 2, y, SZ0 - Tread), new Vector3((SA0 + SA1) / 2, y + 1.6f, land), SA1 - SA0);
                BoxMM("MidLanding", lv, new Vector3(SA0, y + 1.6f - 0.2f, land), new Vector3(SB1, y + 1.6f, ZB - 0.1f), mStair);
                var fb = G("FlightB", lv);
                for (int j = 0; j < Steps; j++)
                {
                    float z1 = land + Tread - j * Tread, ty = y + 1.6f + (j + 1) * Rise;
                    BoxMM("Step_" + (j + 1), fb, new Vector3(SB0, ty - 0.35f, z1 - Tread), new Vector3(SB1, ty, z1), mStair);
                }
                RampPlate(fb, "RampCollider", new Vector3((SB0 + SB1) / 2, y + 1.6f, land + 2 * Tread), new Vector3((SB0 + SB1) / 2, y + 3.2f, land + Tread - (Steps - 1) * Tread), SB1 - SB0);
                var rr = G("Railings", lv);
                SlopedRailing(rr, "Rail_FlightA", new Vector3(SA1, y, SZ0 - Tread), new Vector3(SA1, y + 1.6f, land));
                SlopedRailing(rr, "Rail_FlightB", new Vector3(SB0, y + 1.6f, land + 2 * Tread), new Vector3(SB0, y + 3.2f, land + Tread - (Steps - 1) * Tread));
                Railing(rr, "Rail_LandingGap", new Vector3(SA1, y + 1.6f, land), new Vector3(SB0, y + 1.6f, land));
                float yn = FloorY(i + 1);
                if (i + 1 < Floors - 1) Railing(rr, "Rail_FloorGap", new Vector3(SA1, yn, SZ0 + 0.05f), new Vector3(SB0, yn, SZ0 + 0.05f));
                else Railing(rr, "Rail_TopVoid", new Vector3(SA0, yn, SZ0 + 0.05f), new Vector3(SB0, yn, SZ0 + 0.05f));
                float mlY = (i == Floors - 2) ? FloorY(Floors) - T - 0.03f : y + 1.6f + 2.9f;
                Inst(pfLight, lv, new Vector3(-1.8f, mlY, 13.9f), 0f, "MidLandingLight");
                if (i == 0)
                {
                    BoxMM("UnderStairInfill", lv, new Vector3(SA1, B, SZ0 + 0.3f), new Vector3(SB1, B + 1.35f, land), mWall);
                    BoxMM("UnderLandingInfill", lv, new Vector3(SA0, B, land), new Vector3(SB1, B + 1.35f, ZB - 0.1f), mWall);
                }
            }
        }
    }
}
