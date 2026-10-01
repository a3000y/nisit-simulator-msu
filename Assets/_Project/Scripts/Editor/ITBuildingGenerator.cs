using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using NisitSimulator.GEBuilding;
using NisitSimulator.ITBuilding;

namespace NisitSimulator.EditorTools
{
    // สร้างอาคารคณะวิทยาการสารสนเทศ (IT · MSU) 4 ชั้น แบบเดินเข้าได้จริง แล้วบันทึกเป็น Prefab IT_Building
    // เมนู Nisit > IT Building > Build Prefab   (สร้างใน Scene ชั่วคราว → Save prefab → ลบของชั่วคราว)
    //
    // หน่วย = เมตรออกแบบ (ขนาดคนจริง) · วางในฉากที่ scale 0.75 เหมือนตึก GE/หอพัก ให้เข้ากับผู้เล่นเดิม (สูง 1.32 ม.)
    // ผังท้องถิ่น (ด้านหน้าอาคาร = -Z):
    //   x -18..18 · ปีกซ้าย x -18..-6 · โถงกลาง x -6..6 · ปีกขวา x 6..18
    //   ห้องฝั่งหน้า z -8..-1.6 · ทางเดินกลาง z -1.5..1.5 (กว้าง 3.0) · ห้องฝั่งหลัง z 1.6..8 · โถงกลางยื่นถึง z -9
    //   ฝั่งหลัง: บันไดหลัก x -6..0 · ห้องน้ำ x 0..6 · บันไดปลายอาคาร (ผนังส้มอิฐ) x 12..18
    public static class ITBuildingGenerator
    {
        public const float B = 1.2f;    // ความสูงพื้นชั้น 1 = ฐานยก (podium) สีเทา · บันไดหน้ากว้าง 8 ขั้นตามภาพอ้างอิง
        public const float H = 3.6f;    // สูงต่อชั้น
        public const float T = 0.25f;   // ความหนาพื้น
        public const float W = 0.2f;    // ผนังภายใน
        public const float WE = 0.3f;   // ผนังภายนอก
        public const int Floors = 4;
        public static float FY(int i) => B + i * H;
        public static float WallH => H - T;
        public const float Rise = 0.15f, Tread = 0.28f; public const int StepsPerFlight = 12;
        public const float LaneHalf = 2.875f, Spine = 0.15f;
        public const float FlightLen = (StepsPerFlight - 1) * Tread;   // 3.08
        public const float SlotDepth = 6.35f;                        // จากขอบทางเดิน z 1.5 ถึงผนังหลัง 7.85
        public const float DoorW = 1.3f, DoorH = 2.2f;

        const string MatDir = "Assets/_Project/Art/Materials/ITBuilding";
        const string PfDir = "Assets/_Project/Prefabs/ITBuilding";
        const string ClassPf = "Assets/_Project/Prefabs/Classroom/";

        static Material mWhite, mPanel, mDarkGray, mSlab, mCorridor, mRoomFloor, mLabFloor, mStair, mRail, mRailLight,
            mGlass, mGlassDark, mBrick, mDoor, mFrame, mBoard, mLight, mSign, mSignDark, mSignOrange, mWood, mMetal,
            mScreen, mScreenOn, mExit, mPlaza, mGrass, mPlanter, mFabric, mTileWall, mRestFloor;
        static TMP_FontAsset thaiFont;
        static GameObject pfDoor, pfRoomSign, pfLight, pfLightPanel, pfSunshade, pfStairLevel, pfExitSign, pfBench,
            pfRestroom, pfPCBench2, pfPCBench1, pfTeacherPC, pfWhiteboard, pfScreen, pfMeetTable, pfGroupTable,
            pfOfficeDesk, pfBookshelf, pfWorkbench, pfPodium, pfReception, pfKiosk, pfPlanterBox, pfWaterCooler;
        static GameObject pfDesk, pfChair, pfTDesk, pfTChair, pfCabinet;
        static GameObject pfLouverScreen, pfFlowerPot, pfFlagpole;
        static Material[] mPortraits;
        static Material mConcrete, mTerracotta;
        static readonly Dictionary<string, GameObject> windowPf = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, GameObject> roomPf = new Dictionary<string, GameObject>();

        // ------------------------------------------------------------------ room table
        public enum RoomType { Classroom, ComputerLab, GroupWork, Meeting, Faculty, ProjectLab, Seminar, Staff }
        public struct Slot { public float x0, x1; public bool front; public Slot(float a, float b, bool f) { x0 = a; x1 = b; front = f; } }
        // ลำดับห้อง n01..n07: หน้า (01–04) แล้วหลัง (05–07)
        public static readonly Slot[] RoomSlots =
        {
            new Slot(-18, -12, true), new Slot(-12, -6, true), new Slot(6, 12, true), new Slot(12, 18, true),
            new Slot(-18, -12, false), new Slot(-12, -6, false), new Slot(6, 12, false),
        };
        static readonly RoomType[][] FloorRooms =
        {
            new[] { RoomType.Classroom, RoomType.Classroom, RoomType.Classroom, RoomType.Staff, RoomType.Classroom, RoomType.Classroom, RoomType.Staff },
            new[] { RoomType.ComputerLab, RoomType.ComputerLab, RoomType.ComputerLab, RoomType.ComputerLab, RoomType.ComputerLab, RoomType.ComputerLab, RoomType.ComputerLab },
            new[] { RoomType.Classroom, RoomType.Classroom, RoomType.GroupWork, RoomType.GroupWork, RoomType.Meeting, RoomType.Classroom, RoomType.Meeting },
            new[] { RoomType.Faculty, RoomType.Faculty, RoomType.ProjectLab, RoomType.ProjectLab, RoomType.Seminar, RoomType.Faculty, RoomType.Seminar },
        };
        static readonly string[][] FloorRoomNames =
        {
            new[] { "ห้องเรียน", "ห้องเรียน", "ห้องเรียน", "ห้องเจ้าหน้าที่ / สำนักงานคณะ", "ห้องเรียน", "ห้องเรียน", "ห้องเจ้าหน้าที่ฝ่ายวิชาการ" },
            new[] { "ห้องปฏิบัติการคอมพิวเตอร์ 1", "ห้องปฏิบัติการคอมพิวเตอร์ 2", "ห้องปฏิบัติการคอมพิวเตอร์ 3", "ห้องปฏิบัติการคอมพิวเตอร์ 4", "ห้องปฏิบัติการคอมพิวเตอร์ 5", "ห้องปฏิบัติการคอมพิวเตอร์ 6", "ห้องปฏิบัติการเครือข่าย" },
            new[] { "ห้องเรียน", "ห้องเรียน", "ห้องทำงานกลุ่ม 1", "ห้องทำงานกลุ่ม 2", "ห้องประชุม 1", "ห้องเรียน", "ห้องประชุม 2" },
            new[] { "ห้องพักอาจารย์ 1", "ห้องพักอาจารย์ 2", "ห้องปฏิบัติการโครงงาน 1", "ห้องปฏิบัติการโครงงาน 2", "ห้องสัมมนา 1", "ห้องพักอาจารย์ 3", "ห้องสัมมนา 2" },
        };
        public static string RoomCode(int floor, int r) => $"IT{floor + 1}0{r + 1}";
        public static float SlotCenter(Slot s) => (s.x0 + s.x1) * 0.5f;
        public static float RoomCenterZ(Slot s) => s.front ? -(1.7f + 7.85f) * 0.5f : (1.7f + 7.85f) * 0.5f;
        // ช่องประตู (ตามแนว x ของอาคาร) ของห้อง
        public static Vector2 DoorSpan(Slot s)
        {
            float c = SlotCenter(s);
            return s.front ? new Vector2(c - 2.75f, c - 1.45f) : new Vector2(c + 1.45f, c + 2.75f);
        }
        public static readonly float MainStairX = -3.0f, EndStairX = 14.975f;

        struct Op
        {
            public float a, b, bottom, top;
            public Op(float a, float b, float bottom, float top) { this.a = a; this.b = b; this.bottom = bottom; this.top = top; }
        }

        [MenuItem("Nisit/IT Building/Build Prefab")]
        public static void BuildMenu() => Debug.Log(BuildPrefab());

        public static string BuildPrefab()
        {
            EnsureFolder("Assets/_Project/Art/Materials", "ITBuilding");
            EnsureFolder("Assets/_Project/Prefabs", "ITBuilding");
            thaiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/Mitr SDF.asset");
            MakeMaterials();
            MakePrefabs();

            var root = new GameObject("IT_Building").transform;
            try
            {
                BuildStructure(G("Structure", root));
                for (int i = 0; i < Floors; i++) BuildFloor(G("Floor_" + (i + 1), root), i);
                BuildStairs(G("Stairs", root));
                NoShadowAll(root.Find("Stairs").gameObject);
                BuildExterior(G("Exterior", root));

                var cut = root.gameObject.AddComponent<GEBuildingCutaway>();
                cut.localMin = new Vector3(-18.4f, -1f, -9.4f);
                cut.localMax = new Vector3(18.4f, 17f, 8.4f);
                cut.firstFloorY = B; cut.floorHeight = H; cut.slabThickness = T;
                var lz = root.gameObject.AddComponent<ITBuildingLightZone>();
                lz.cutaway = cut;
                var groups = new Transform[Floors];
                for (int i = 0; i < Floors; i++) groups[i] = root.Find("Floor_" + (i + 1) + "/Lights");
                lz.floorLightGroups = groups;

                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.GetComponentInParent<GEDoor>() != null) continue;
                    var flags = StaticEditorFlags.OccludeeStatic;
                    if (t.GetComponent<TextMeshPro>() == null) flags |= StaticEditorFlags.BatchingStatic;
                    var rr = t.GetComponent<Renderer>();
                    if (rr != null && rr.sharedMaterial != mGlass && rr.sharedMaterial != mGlassDark && t.GetComponent<TextMeshPro>() == null)
                    {
                        var sz = rr.bounds.size;
                        if (Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)) >= 2f) flags |= StaticEditorFlags.OccluderStatic;   // ชิ้นใหญ่ (ผนัง/พื้น) เท่านั้นเป็น Occluder
                    }
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                }

                int colliders = root.GetComponentsInChildren<Collider>(true).Length;
                int lights = root.GetComponentsInChildren<Light>(true).Length;
                int doors = root.GetComponentsInChildren<GEDoor>(true).Length;
                int rends = root.GetComponentsInChildren<Renderer>(true).Length;
                var pf = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PfDir + "/IT_Building.prefab");
                return $"IT_Building prefab saved ({AssetDatabase.GetAssetPath(pf)}): renderers={rends} colliders={colliders} lights={lights} doors={doors}";
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        // ------------------------------------------------------------------ helpers
        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

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
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!col) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static GameObject BoxMM(string name, Transform parent, Vector3 min, Vector3 max, Material m, bool col = true)
            => Box(name, parent, (min + max) * 0.5f, max - min, m, col);

        static GameObject NoShadow(GameObject g) { g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; return g; }

        static Material Mat(string name, Color c, float smooth, float metal = 0f)
        {
            string p = MatDir + "/M_IT_" + name + ".mat";
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, p); }
            m.shader = lit;
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Transparent(Material m)
        {
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", 1); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
            return m;
        }

        static Material Emissive(Material m, Color e)
        {
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", e);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        static void MakeMaterials()
        {
            mWhite = Mat("WhiteFrame", new Color(0.95f, 0.95f, 0.94f), 0.15f);
            mPanel = Mat("GrayPanel", new Color(0.70f, 0.72f, 0.74f), 0.2f);
            mDarkGray = Mat("DarkGray", new Color(0.36f, 0.38f, 0.41f), 0.25f);
            mSlab = Mat("Slab", new Color(0.82f, 0.82f, 0.80f), 0.25f);
            mCorridor = Mat("CorridorTile", new Color(0.86f, 0.86f, 0.84f), 0.55f);
            mRoomFloor = Mat("RoomFloor", new Color(0.80f, 0.76f, 0.68f), 0.35f);
            mLabFloor = Mat("LabFloor", new Color(0.62f, 0.68f, 0.74f), 0.4f);
            mRestFloor = Mat("RestroomFloor", new Color(0.74f, 0.80f, 0.84f), 0.45f);
            mTileWall = Mat("InteriorWall", new Color(0.93f, 0.93f, 0.91f), 0.1f);
            mStair = Mat("StairGranite", new Color(0.66f, 0.66f, 0.64f), 0.35f);
            mRail = Mat("RailDark", new Color(0.30f, 0.32f, 0.35f), 0.5f, 0.6f);
            mRailLight = Mat("RailLight", new Color(0.92f, 0.91f, 0.86f), 0.4f, 0.3f);
            mBrick = Mat("OrangeBrick", new Color(0.80f, 0.42f, 0.22f), 0.12f);
            mDoor = Mat("Door", new Color(0.56f, 0.42f, 0.30f), 0.3f);
            mFrame = Mat("Frame", new Color(0.84f, 0.86f, 0.87f), 0.5f, 0.5f);
            mBoard = Mat("Whiteboard", new Color(0.98f, 0.98f, 0.98f), 0.75f);
            mSign = Mat("SignNavy", new Color(0.12f, 0.20f, 0.38f), 0.3f);
            mSignDark = Mat("SignDark", new Color(0.18f, 0.20f, 0.23f), 0.3f);
            mSignOrange = Mat("SignOrange", new Color(0.90f, 0.47f, 0.18f), 0.3f);
            mWood = Mat("Wood", new Color(0.76f, 0.60f, 0.42f), 0.3f);
            mMetal = Mat("Metal", new Color(0.62f, 0.64f, 0.67f), 0.6f, 0.7f);
            mFabric = Mat("Fabric", new Color(0.24f, 0.42f, 0.55f), 0.1f);
            mPlaza = Mat("Plaza", new Color(0.80f, 0.77f, 0.71f), 0.15f);
            mGrass = Mat("Grass", new Color(0.46f, 0.64f, 0.36f), 0.05f);
            mPlanter = Mat("Planter", new Color(0.55f, 0.56f, 0.57f), 0.2f);
            mScreen = Mat("ScreenOff", new Color(0.06f, 0.07f, 0.09f), 0.8f);
            mScreenOn = Emissive(Mat("ScreenOn", new Color(0.10f, 0.25f, 0.40f), 0.8f), new Color(0.18f, 0.42f, 0.70f) * 0.9f);
            mExit = Emissive(Mat("ExitGreen", new Color(0.05f, 0.55f, 0.25f), 0.3f), new Color(0.1f, 0.9f, 0.35f) * 1.2f);
            mLight = Emissive(Mat("LightPanel", Color.white, 0.2f), new Color(1f, 0.98f, 0.94f) * 1.6f);
            mGlass = Transparent(Mat("GlassTeal", new Color(0.42f, 0.74f, 0.74f, 0.42f), 0.95f, 0.1f));
            mGlassDark = Transparent(Mat("GlassTealDeep", new Color(0.26f, 0.55f, 0.58f, 0.62f), 0.95f, 0.2f));
            mConcrete = Mat("PodiumConcrete", new Color(0.55f, 0.57f, 0.60f), 0.15f);
            mTerracotta = Mat("LouverTerracotta", new Color(0.78f, 0.52f, 0.38f), 0.25f);
            mPortraits = new Material[3];
            for (int k = 0; k < 3; k++) mPortraits[k] = PortraitMaterial(k);
            AssetDatabase.SaveAssets();
        }

        // แผงภาพขาวดำบนระเบียง: สร้าง texture ภาพเงาคนแบบทั่วไป (ไม่ใช่บุคคลจริง)
        static Material PortraitMaterial(int k)
        {
            string dir = "Assets/_Project/Art/Textures";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/_Project/Art", "Textures");
            if (!AssetDatabase.IsValidFolder(dir + "/ITBuilding")) AssetDatabase.CreateFolder(dir, "ITBuilding");
            string tp = dir + "/ITBuilding/T_IT_Portrait_" + k + ".png";
            int w = 96, hgt = 128;
            var tex = new Texture2D(w, hgt, TextureFormat.RGB24, false);
            float bg = 0.93f - k * 0.04f;
            float hx = w * (0.5f + (k - 1) * 0.06f), hy = hgt * 0.62f, hr = w * (0.17f + k * 0.015f);
            for (int yy = 0; yy < hgt; yy++)
                for (int xx = 0; xx < w; xx++)
                {
                    float v = bg - 0.10f * yy / hgt;
                    float dx = xx - hx, dy = yy - hy;
                    float sy = hgt * 0.40f;
                    float shoulderHalf = w * 0.20f + (sy - yy) * 0.55f;
                    if (yy < sy && Mathf.Abs(xx - hx) < shoulderHalf) v = 0.22f + 0.06f * k;                     // ไหล่/ลำตัว
                    if (yy < sy && yy > sy * 0.25f && Mathf.Abs(xx - hx) < (sy - yy) * 0.18f + 2f) v = 0.88f;      // คอเสื้อ
                    if (yy > sy - 6 && yy < sy + 4 && Mathf.Abs(xx - hx) < w * 0.07f) v = 0.55f;                 // คอ
                    float e = dx * dx / (hr * hr) + dy * dy / (hr * hr * 1.45f);
                    if (e < 1f) v = 0.62f - 0.25f * e;                                                         // ใบหน้า (ไล่เงา)
                    if (e < 1.05f && dy > hr * 0.55f) v = 0.12f;                                               // ผม
                    if (xx < 3 || xx > w - 4 || yy < 3 || yy > hgt - 4) v = 0.98f;
                    tex.SetPixel(xx, yy, new Color(v, v, v));
                }
            tex.Apply();
            System.IO.File.WriteAllBytes(tp, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(tp);
            var t2 = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            var m = Mat("Portrait_" + k, Color.white, 0.1f);
            m.SetTexture("_BaseMap", t2);
            return m;
        }

        static TextMeshPro Text(string name, Transform parent, Vector3 pos, Vector2 size, string text, Color color, float maxSize = 3f, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            if (thaiFont != null) tmp.font = thaiFont;
            tmp.text = text;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.05f; tmp.fontSizeMax = maxSize;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = size;
            go.transform.localPosition = pos;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return tmp;
        }

        // ผนังแนว X (ระนาบ z = zc) เจาะช่องตาม ops (a,b = ช่วง x · bottom/top = สูงจากฐานผนัง)
        static Transform WallX(Transform p, string name, float x0, float x1, float zc, float y0, float h, Material m, List<Op> ops = null, float th = W)
        {
            var g = G(name, p);
            ops = ops ?? new List<Op>();
            ops.Sort((u, v) => u.a.CompareTo(v.a));
            float cur = x0; int n = 0;
            foreach (var o in ops)
            {
                if (o.a > cur + 0.001f) BoxMM("Seg_" + n++, g, new Vector3(cur, y0, zc - th / 2), new Vector3(o.a, y0 + h, zc + th / 2), m);
                if (o.bottom > 0.001f) BoxMM("Below_" + n, g, new Vector3(o.a, y0, zc - th / 2), new Vector3(o.b, y0 + o.bottom, zc + th / 2), m);
                if (o.top < h - 0.001f) BoxMM("Above_" + n, g, new Vector3(o.a, y0 + o.top, zc - th / 2), new Vector3(o.b, y0 + h, zc + th / 2), m);
                cur = o.b; n++;
            }
            if (x1 > cur + 0.001f) BoxMM("Seg_" + n, g, new Vector3(cur, y0, zc - th / 2), new Vector3(x1, y0 + h, zc + th / 2), m);
            return g;
        }

        // ผนังแนว Z (ระนาบ x = xc)
        static Transform WallZ(Transform p, string name, float z0, float z1, float xc, float y0, float h, Material m, List<Op> ops = null, float th = W)
        {
            var g = G(name, p);
            ops = ops ?? new List<Op>();
            ops.Sort((u, v) => u.a.CompareTo(v.a));
            float cur = z0; int n = 0;
            foreach (var o in ops)
            {
                if (o.a > cur + 0.001f) BoxMM("Seg_" + n++, g, new Vector3(xc - th / 2, y0, cur), new Vector3(xc + th / 2, y0 + h, o.a), m);
                if (o.bottom > 0.001f) BoxMM("Below_" + n, g, new Vector3(xc - th / 2, y0, o.a), new Vector3(xc + th / 2, y0 + o.bottom, o.b), m);
                if (o.top < h - 0.001f) BoxMM("Above_" + n, g, new Vector3(xc - th / 2, y0 + o.top, o.a), new Vector3(xc + th / 2, y0 + h, o.b), m);
                cur = o.b; n++;
            }
            if (z1 > cur + 0.001f) BoxMM("Seg_" + n, g, new Vector3(xc - th / 2, y0, cur), new Vector3(xc + th / 2, y0 + h, z1), m);
            return g;
        }

        // ราวกันตกแนวนอน: กระจก + ราวจับ, Collider สูง 1.2 ม.
        static Transform Railing(Transform p, string name, Vector3 a, Vector3 b, Material handRail = null)
        {
            Vector3 dir = b - a; float len = dir.magnitude;
            var r = G(name, p);
            r.localPosition = (a + b) * 0.5f;
            r.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0, 90, 0);
            var bc = r.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.6f, 0); bc.size = new Vector3(len, 1.2f, 0.12f);
            NoShadow(Box("Glass", r, new Vector3(0, 0.5f, 0), new Vector3(len, 0.85f, 0.03f), mGlass, false));
            Box("HandRail", r, new Vector3(0, 1.05f, 0), new Vector3(len, 0.06f, 0.08f), handRail ?? mRail, false);
            Box("BaseRail", r, new Vector3(0, 0.04f, 0), new Vector3(len, 0.08f, 0.08f), handRail ?? mRail, false);
            int posts = Mathf.Max(1, Mathf.CeilToInt(len / 1.5f));
            for (int i = 0; i <= posts; i++)
                Box("Post_" + i, r, new Vector3(-len / 2 + len * i / posts, 0.53f, 0), new Vector3(0.05f, 1.06f, 0.05f), handRail ?? mRail, false);
            return r;
        }

        // แผ่น Collider ลาด (ผิวบนตรงเส้น a -> b)
        static Transform RampPlate(Transform p, string name, Vector3 a, Vector3 b, float width, Material visual = null, float thickness = 0.12f)
        {
            Vector3 dir = b - a; float len = dir.magnitude;
            var r = G(name, p);
            r.localPosition = (a + b) * 0.5f;
            r.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            if (visual != null) Box("Surface", r, new Vector3(0, -thickness / 2, 0), new Vector3(width, thickness, len), visual, true);
            else { var bc = r.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, -thickness / 2, 0); bc.size = new Vector3(width, thickness, len); }
            return r;
        }

        // ราวจับตามแนวลาด (ประดับ ไม่มี Collider — ด้านข้างมีผนัง/ผนังกลางกันอยู่แล้ว)
        static void SlopedHandrail(Transform p, string name, Vector3 a, Vector3 b, Material m)
        {
            Vector3 dir = b - a; float len = dir.magnitude;
            var r = G(name, p);
            r.localPosition = (a + b) * 0.5f + Vector3.up * 0.9f;
            r.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Box("Rail", r, Vector3.zero, new Vector3(0.06f, 0.06f, len), m, false);
            Box("Bracket_A", r, new Vector3(0, -0.12f, -len / 2 + 0.2f), new Vector3(0.03f, 0.2f, 0.03f), m, false);
            Box("Bracket_B", r, new Vector3(0, -0.12f, len / 2 - 0.2f), new Vector3(0.03f, 0.2f, 0.03f), m, false);
        }

        // วัตถุภายใน (เฟอร์นิเจอร์/ผนังภายใน/ป้าย) ไม่ต้องทอดเงาแดด → ลด Shadow Casters
        static void NoShadowAll(GameObject g)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static GameObject SavePrefab(GameObject temp, string name, bool castShadows = false)
        {
            if (!castShadows) NoShadowAll(temp);
            var pf = PrefabUtility.SaveAsPrefabAsset(temp, PfDir + "/" + name + ".prefab");
            Object.DestroyImmediate(temp);
            return pf;
        }

        static GameObject Inst(GameObject prefab, Transform parent, Vector3 pos, float rotY, string name = null)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0, rotY, 0);
            if (name != null) g.name = name;
            return g;
        }

        static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        // ------------------------------------------------------------------ prefabs
        static GameObject WindowPrefab(float w, float h, bool stairRails = false)
        {
            string key = (stairRails ? "IT_StairOpening_" : "IT_Window_") + Mathf.RoundToInt(w * 100) + "x" + Mathf.RoundToInt(h * 100);
            if (windowPf.TryGetValue(key, out var cached)) return cached;
            var root = new GameObject(key);
            float f = 0.06f, d = WE + 0.04f;
            if (stairRails)
            {
                // ช่องเปิดบันไดด้านข้าง: ราวแนวนอนสีอ่อน + Collider กันตก (ทึบตามแนวราว)
                for (float y = -h / 2 + 0.12f; y < h / 2 - 0.05f; y += 0.16f)
                    Box("Rail", root.transform, new Vector3(0, y, 0), new Vector3(w, 0.05f, 0.06f), mRailLight, false);
                Box("Post_L", root.transform, new Vector3(-w / 2 + 0.04f, 0, 0), new Vector3(0.08f, h, 0.08f), mRailLight, false);
                Box("Post_R", root.transform, new Vector3(w / 2 - 0.04f, 0, 0), new Vector3(0.08f, h, 0.08f), mRailLight, false);
                var bc = root.AddComponent<BoxCollider>(); bc.size = new Vector3(w, h, 0.12f);
            }
            else
            {
                NoShadow(Box("Glass", root.transform, Vector3.zero, new Vector3(w, h, 0.04f), mGlass, true));
                Box("Frame_Top", root.transform, new Vector3(0, h / 2 - f / 2, 0), new Vector3(w, f, d), mFrame, false);
                Box("Frame_Bottom", root.transform, new Vector3(0, -h / 2 + f / 2, 0), new Vector3(w, f, d), mFrame, false);
                Box("Frame_L", root.transform, new Vector3(-w / 2 + f / 2, 0, 0), new Vector3(f, h, d), mFrame, false);
                Box("Frame_R", root.transform, new Vector3(w / 2 - f / 2, 0, 0), new Vector3(f, h, d), mFrame, false);
                int panes = Mathf.Max(1, Mathf.RoundToInt(w / 1.1f));
                for (int i = 1; i < panes; i++)
                    Box("Mullion_" + i, root.transform, new Vector3(-w / 2 + w * i / panes, 0, 0), new Vector3(0.05f, h, 0.1f), mFrame, false);
                Box("Sill_Out", root.transform, new Vector3(0, -h / 2 - 0.03f, -d / 2 - 0.06f), new Vector3(w + 0.1f, 0.05f, 0.14f), mWhite, false);
            }
            var pf = SavePrefab(root, key);
            windowPf[key] = pf;
            return pf;
        }

        static void MakePrefabs()
        {
            windowPf.Clear(); roomPf.Clear();
            pfDesk = Load(ClassPf + "StudentDesk.prefab"); pfChair = Load(ClassPf + "StudentChair.prefab");
            pfTDesk = Load(ClassPf + "TeacherDesk.prefab"); pfTChair = Load(ClassPf + "TeacherChair.prefab");
            pfCabinet = Load(ClassPf + "StorageCabinet.prefab");
            pfBench = Load("Assets/_Project/Prefabs/GEBuilding/GE_Bench.prefab");
            pfRestroom = Load("Assets/_Project/Prefabs/GEBuilding/GE_RestroomFixtures.prefab");

            // แผงไฟเพดาน (Emissive อย่างเดียว) + ไฟเพดานพร้อม Point Light (ไม่มีเงา)
            var lp = new GameObject("IT_LightPanel");
            NoShadow(Box("Panel", lp.transform, Vector3.zero, new Vector3(1.2f, 0.04f, 0.6f), mLight, false));
            Box("Rim", lp.transform, new Vector3(0, 0.025f, 0), new Vector3(1.26f, 0.02f, 0.66f), mFrame, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            pfLightPanel = SavePrefab(lp, "IT_LightPanel");
            var l = new GameObject("IT_CeilingLight");
            Inst(pfLightPanel, l.transform, Vector3.zero, 0f, "Panel");
            var pt = G("PointLight", l.transform, new Vector3(0, -0.4f, 0));
            var lt = pt.gameObject.AddComponent<Light>();
            lt.type = LightType.Point; lt.range = 7.5f; lt.intensity = 2.2f; lt.color = new Color(1f, 0.97f, 0.92f);
            lt.shadows = LightShadows.None; lt.renderMode = LightRenderMode.Auto;
            pfLight = SavePrefab(l, "IT_CeilingLight");

            // ประตูบานพับ: ช่อง 1.3 x 2.2 ตามแกน X ท้องถิ่น (0..1.3), ระนาบผนัง z = 0, เปิดเข้า +Z
            var d = new GameObject("IT_Door");
            var fr = G("Frame", d.transform);
            Box("Jamb_Hinge", fr, new Vector3(0.025f, DoorH / 2, 0), new Vector3(0.05f, DoorH, W + 0.04f), mFrame, true);
            Box("Jamb_Latch", fr, new Vector3(DoorW - 0.025f, DoorH / 2, 0), new Vector3(0.05f, DoorH, W + 0.04f), mFrame, true);
            Box("Head", fr, new Vector3(DoorW / 2, DoorH - 0.025f, 0), new Vector3(DoorW, 0.05f, W + 0.04f), mFrame, false);
            var hinge = G("Hinge", d.transform, new Vector3(0.06f, 0, 0));
            int interLayer = LayerMask.NameToLayer("Interactable");
            float leafW = DoorW - 0.13f;
            var leaf = Box("Leaf", hinge, new Vector3(leafW / 2, 1.075f, 0), new Vector3(leafW, 2.12f, 0.045f), mDoor, true);
            if (interLayer >= 0) leaf.layer = interLayer;
            Box("Handle_A", hinge, new Vector3(leafW - 0.12f, 1.0f, 0.05f), new Vector3(0.14f, 0.03f, 0.03f), mMetal, false);
            Box("Handle_B", hinge, new Vector3(leafW - 0.12f, 1.0f, -0.05f), new Vector3(0.14f, 0.03f, 0.03f), mMetal, false);
            NoShadow(Box("VisionPanel", hinge, new Vector3(leafW / 2, 1.5f, 0), new Vector3(0.3f, 0.6f, 0.05f), mGlass, false));
            if (interLayer >= 0) d.layer = interLayer;
            var trig = d.AddComponent<BoxCollider>();
            trig.isTrigger = true; trig.center = new Vector3(DoorW / 2, 1.0f, 0f); trig.size = new Vector3(DoorW + 0.2f, 2.0f, 1.2f);
            var door = d.AddComponent<GEDoor>();
            door.hinge = hinge; door.openAngle = -90f; door.startOpen = true;
            hinge.localRotation = Quaternion.Euler(0, -90f, 0);
            pfDoor = SavePrefab(d, "IT_Door");

            // ป้ายหมายเลขห้อง (ข้อความหันไปทาง -Z)
            var s = new GameObject("IT_RoomSign");
            Box("Plate", s.transform, Vector3.zero, new Vector3(0.62f, 0.32f, 0.03f), mSign, false);
            Box("Accent", s.transform, new Vector3(0, -0.145f, -0.005f), new Vector3(0.62f, 0.03f, 0.03f), mSignOrange, false);
            Text("Code", s.transform, new Vector3(0, 0.055f, -0.02f), new Vector2(0.56f, 0.15f), "IT000", Color.white, 1.4f, FontStyles.Bold);
            Text("Name", s.transform, new Vector3(0, -0.075f, -0.02f), new Vector2(0.58f, 0.09f), "ห้อง", new Color(0.88f, 0.92f, 1f), 0.6f);
            pfRoomSign = SavePrefab(s, "IT_RoomSign");

            // ป้ายทางออก (เรืองแสงเขียว) ข้อความหัน -Z
            var ex = new GameObject("IT_ExitSign");
            NoShadow(Box("Box", ex.transform, Vector3.zero, new Vector3(0.7f, 0.26f, 0.06f), mExit, false));
            Text("Label", ex.transform, new Vector3(0, 0, -0.035f), new Vector2(0.66f, 0.22f), "ทางออก  EXIT", Color.white, 1.2f, FontStyles.Bold);
            Text("LabelBack", ex.transform, new Vector3(0, 0, 0.035f), new Vector2(0.66f, 0.22f), "ทางออก  EXIT", Color.white, 1.2f, FontStyles.Bold).transform.localRotation = Quaternion.Euler(0, 180, 0);
            pfExitSign = SavePrefab(ex, "IT_ExitSign");

            // แผงบังแดดแนวนอน (ยาว 5.6 ม.) — จุดอ้างอิง = ขอบบนหน้าต่างที่ผิวผนังด้านนอก, ยื่นไป -Z
            var sh = new GameObject("IT_Sunshade");
            Box("Shelf", sh.transform, new Vector3(0, 0.03f, -0.45f), new Vector3(5.6f, 0.06f, 0.9f), mWhite, false);
            Box("Louver_1", sh.transform, new Vector3(0, 0.30f, -0.38f), new Vector3(5.6f, 0.05f, 0.62f), mPanel, false);
            Box("Louver_2", sh.transform, new Vector3(0, 0.54f, -0.32f), new Vector3(5.6f, 0.05f, 0.5f), mPanel, false);
            foreach (var x in new[] { -2.75f, 2.75f })
                Box("Bracket", sh.transform, new Vector3(x, 0.28f, -0.42f), new Vector3(0.06f, 0.6f, 0.84f), mWhite, false);
            pfSunshade = SavePrefab(sh, "IT_Sunshade", true);

            // แผงบังแดดแนวนอนสีอิฐ + ครีบขาวแนวตั้ง หน้าปีก +X (ภาพที่ 2–3) — จุดอ้างอิง = พื้นชั้นที่ผิวผนังนอก, ยื่นไป -Z
            var ls = new GameObject("IT_LouverScreen");
            for (int k = 0; k < 7; k++)
                Box("Slat_" + k, ls.transform, new Vector3(0, 1.0f + k * 0.28f, -0.55f), new Vector3(5.5f, 0.05f, 0.32f), k % 3 == 1 ? mWhite : mTerracotta, false);
            foreach (var x in new[] { -2.8f, -1.4f, 0f, 1.4f, 2.8f })
                Box("Fin", ls.transform, new Vector3(x, (H - T) / 2, -0.4f), new Vector3(0.07f, H - T, 0.75f), mWhite, false);
            Box("Shelf", ls.transform, new Vector3(0, 0.88f, -0.45f), new Vector3(5.7f, 0.07f, 0.9f), mWhite, false);
            pfLouverScreen = SavePrefab(ls, "IT_LouverScreen", true);

            // กระถางดอกไม้ (บนผนังข้างบันไดหน้า)
            var fp = new GameObject("IT_FlowerPot");
            Box("Pot", fp.transform, new Vector3(0, 0.18f, 0), new Vector3(0.36f, 0.36f, 0.36f), Mat("PotClay", new Color(0.70f, 0.42f, 0.28f), 0.2f), false);
            var bloom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bloom.name = "Bloom"; bloom.transform.SetParent(fp.transform, false);
            bloom.transform.localPosition = new Vector3(0, 0.48f, 0); bloom.transform.localScale = new Vector3(0.55f, 0.38f, 0.55f);
            bloom.GetComponent<Renderer>().sharedMaterial = Mat("FlowerPink", new Color(0.78f, 0.30f, 0.62f), 0.2f);
            Object.DestroyImmediate(bloom.GetComponent<Collider>());
            pfFlowerPot = SavePrefab(fp, "IT_FlowerPot");

            // เสาธง (ธงสีทั่วไป)
            var fg = new GameObject("IT_Flagpole");
            Box("Pole", fg.transform, new Vector3(0, 3.0f, 0), new Vector3(0.08f, 6.0f, 0.08f), mMetal, true);
            Box("Base", fg.transform, new Vector3(0, 0.1f, 0), new Vector3(0.4f, 0.2f, 0.4f), mConcrete, false);
            Box("Flag", fg.transform, new Vector3(0, 5.3f, 0.55f), new Vector3(0.02f, 0.7f, 1.05f), mSign, false);
            pfFlagpole = SavePrefab(fg, "IT_Flagpole", true);

            // ไม้กระถาง/กล่องต้นไม้ภายใน
            var plantSrc = Load("Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_PotPlant_01.prefab");
            var pb = new GameObject("IT_PlanterBox");
            Box("Box", pb.transform, new Vector3(0, 0.25f, 0), new Vector3(0.6f, 0.5f, 0.6f), mPlanter, true);
            if (plantSrc != null) Inst(plantSrc, pb.transform, new Vector3(0, 0.5f, 0), 0f, "Plant");
            else Box("Leaves", pb.transform, new Vector3(0, 0.9f, 0), new Vector3(0.6f, 0.8f, 0.6f), mGrass, false);
            pfPlanterBox = SavePrefab(pb, "IT_PlanterBox");

            var wc = new GameObject("IT_WaterCooler");
            Box("Body", wc.transform, new Vector3(0, 0.55f, 0), new Vector3(0.4f, 1.1f, 0.4f), mWhite, true);
            Box("Bottle", wc.transform, new Vector3(0, 1.3f, 0), new Vector3(0.28f, 0.4f, 0.28f), mGlassDark, false);
            Box("Tap", wc.transform, new Vector3(0, 0.85f, -0.21f), new Vector3(0.12f, 0.06f, 0.04f), mMetal, false);
            pfWaterCooler = SavePrefab(wc, "IT_WaterCooler");

            // เฟอร์นิเจอร์ (หันหน้าผู้ใช้ไปทาง -X เหมือนโต๊ะเรียน GE)
            pfPCBench2 = PCBench("IT_PCBench2", 2);
            pfPCBench1 = PCBench("IT_PCBench1", 1);

            var tp = new GameObject("IT_TeacherStation");
            Inst(pfTDesk, tp.transform, Vector3.zero, -90f, "TeacherDesk");
            Inst(pfTChair, tp.transform, new Vector3(-0.5f, 0, 0), 90f, "TeacherChair");
            Monitor(tp.transform, new Vector3(0.12f, 0.76f, 0.3f), 90f);
            Box("Keyboard", tp.transform, new Vector3(-0.15f, 0.775f, 0.3f), new Vector3(0.16f, 0.02f, 0.42f), mDarkGray, false);
            Box("Tower", tp.transform, new Vector3(0.1f, 0.25f, -0.5f), new Vector3(0.4f, 0.45f, 0.2f), mDarkGray, false);
            pfTeacherPC = SavePrefab(tp, "IT_TeacherStation");

            // ไวท์บอร์ดติดผนัง (ผิวหัน +X, ติดผนังที่ x = 0)
            var wb = new GameObject("IT_Whiteboard");
            Box("Frame", wb.transform, new Vector3(0.015f, 1.5f, 0), new Vector3(0.03f, 1.25f, 2.65f), mFrame, false);
            Box("Surface", wb.transform, new Vector3(0.035f, 1.5f, 0), new Vector3(0.02f, 1.15f, 2.55f), mBoard, false);
            Box("Tray", wb.transform, new Vector3(0.07f, 0.9f, 0), new Vector3(0.08f, 0.03f, 2.2f), mFrame, false);
            pfWhiteboard = SavePrefab(wb, "IT_Whiteboard");

            // จอแสดงผล/โปรเจกเตอร์ติดผนัง (ผิวหัน +X)
            var sc = new GameObject("IT_WallScreen");
            Box("Bezel", sc.transform, new Vector3(0.03f, 1.75f, 0), new Vector3(0.06f, 1.0f, 1.7f), mSignDark, false);
            NoShadow(Box("Display", sc.transform, new Vector3(0.065f, 1.75f, 0), new Vector3(0.01f, 0.9f, 1.6f), mScreenOn, false));
            pfScreen = SavePrefab(sc, "IT_WallScreen");

            var mt = new GameObject("IT_MeetingTable");
            Box("Top", mt.transform, new Vector3(0, 0.74f, 0), new Vector3(3.0f, 0.05f, 1.1f), mWood, false);
            Box("Base_A", mt.transform, new Vector3(-1.0f, 0.36f, 0), new Vector3(0.12f, 0.72f, 0.7f), mDarkGray, false);
            Box("Base_B", mt.transform, new Vector3(1.0f, 0.36f, 0), new Vector3(0.12f, 0.72f, 0.7f), mDarkGray, false);
            var mtc = mt.AddComponent<BoxCollider>(); mtc.center = new Vector3(0, 0.5f, 0); mtc.size = new Vector3(3.0f, 1.0f, 1.1f);
            pfMeetTable = SavePrefab(mt, "IT_MeetingTable");

            var gt = new GameObject("IT_GroupTable");
            Box("Top", gt.transform, new Vector3(0, 0.74f, 0), new Vector3(1.2f, 0.05f, 1.2f), mWood, false);
            Box("Leg", gt.transform, new Vector3(0, 0.36f, 0), new Vector3(0.14f, 0.72f, 0.14f), mDarkGray, false);
            Box("Foot", gt.transform, new Vector3(0, 0.02f, 0), new Vector3(0.6f, 0.04f, 0.6f), mDarkGray, false);
            Box("Laptop", gt.transform, new Vector3(0.1f, 0.775f, -0.2f), new Vector3(0.34f, 0.02f, 0.24f), mMetal, false);
            var gtc = gt.AddComponent<BoxCollider>(); gtc.center = new Vector3(0, 0.5f, 0); gtc.size = new Vector3(1.2f, 1.0f, 1.2f);
            pfGroupTable = SavePrefab(gt, "IT_GroupTable");

            // โต๊ะทำงาน (ผู้ใช้นั่งด้าน -Z, โต๊ะยาวตามแกน X)
            var od = new GameObject("IT_OfficeDesk");
            Box("Top", od.transform, new Vector3(0, 0.74f, 0), new Vector3(1.4f, 0.04f, 0.7f), mWood, false);
            Box("Side_L", od.transform, new Vector3(-0.67f, 0.36f, 0), new Vector3(0.04f, 0.72f, 0.66f), mWood, false);
            Box("Side_R", od.transform, new Vector3(0.67f, 0.36f, 0), new Vector3(0.04f, 0.72f, 0.66f), mWood, false);
            Box("Partition", od.transform, new Vector3(0, 1.05f, 0.34f), new Vector3(1.4f, 0.6f, 0.03f), mFabric, false);
            Monitor(od.transform, new Vector3(0, 0.76f, 0.15f), 0f);
            Box("Keyboard", od.transform, new Vector3(0, 0.775f, -0.12f), new Vector3(0.42f, 0.02f, 0.16f), mDarkGray, false);
            var odc = od.AddComponent<BoxCollider>(); odc.center = new Vector3(0, 0.5f, 0); odc.size = new Vector3(1.4f, 1.0f, 0.7f);
            Inst(pfTChair, od.transform, new Vector3(0, 0, -0.65f), 0f, "Chair");
            pfOfficeDesk = SavePrefab(od, "IT_OfficeDesk");

            var bs = new GameObject("IT_Bookshelf");
            Box("Body", bs.transform, new Vector3(0, 0.95f, 0), new Vector3(1.0f, 1.9f, 0.36f), mWood, true);
            Color[] bookCols = { new Color(0.7f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.7f), new Color(0.9f, 0.6f, 0.2f), new Color(0.3f, 0.6f, 0.35f) };
            for (int k = 0; k < 4; k++)
                Box("Books_" + k, bs.transform, new Vector3(0, 0.3f + k * 0.45f, -0.05f), new Vector3(0.9f, 0.28f, 0.28f),
                    Mat("Books_" + k, bookCols[k], 0.2f), false);
            pfBookshelf = SavePrefab(bs, "IT_Bookshelf");

            var wbn = new GameObject("IT_Workbench");
            Box("Top", wbn.transform, new Vector3(0, 0.9f, 0), new Vector3(2.0f, 0.06f, 0.9f), mWood, false);
            Box("Frame", wbn.transform, new Vector3(0, 0.45f, 0), new Vector3(1.9f, 0.84f, 0.8f), mDarkGray, false);
            Box("Laptop", wbn.transform, new Vector3(-0.5f, 0.94f, 0), new Vector3(0.34f, 0.02f, 0.24f), mMetal, false);
            Box("Board", wbn.transform, new Vector3(0.2f, 0.94f, 0.1f), new Vector3(0.3f, 0.02f, 0.2f), Mat("PCB", new Color(0.1f, 0.45f, 0.25f), 0.4f), false);
            Box("Printer3D", wbn.transform, new Vector3(0.7f, 1.15f, 0.1f), new Vector3(0.45f, 0.45f, 0.45f), mDarkGray, false);
            var wbc = wbn.AddComponent<BoxCollider>(); wbc.center = new Vector3(0, 0.6f, 0); wbc.size = new Vector3(2.0f, 1.2f, 0.9f);
            pfWorkbench = SavePrefab(wbn, "IT_Workbench");

            var pd = new GameObject("IT_Podium");
            Box("Body", pd.transform, new Vector3(0, 0.55f, 0), new Vector3(0.6f, 1.1f, 0.5f), mWood, true);
            Box("Top", pd.transform, new Vector3(0, 1.12f, 0), new Vector3(0.66f, 0.04f, 0.56f), mDarkGray, false);
            Box("Logo", pd.transform, new Vector3(0.31f, 0.75f, 0), new Vector3(0.02f, 0.3f, 0.36f), mSignOrange, false);
            pfPodium = SavePrefab(pd, "IT_Podium");

            // เคาน์เตอร์ประชาสัมพันธ์ (ยาวตามแกน X, ด้านบริการ = -Z)
            var rc = new GameObject("IT_ReceptionDesk");
            Box("Counter", rc.transform, new Vector3(0, 0.55f, 0), new Vector3(3.0f, 1.1f, 0.6f), mWhite, true);
            Box("Top", rc.transform, new Vector3(0, 1.12f, -0.05f), new Vector3(3.1f, 0.05f, 0.75f), mWood, false);
            Box("Band", rc.transform, new Vector3(0, 0.7f, -0.305f), new Vector3(3.0f, 0.12f, 0.02f), mSignOrange, false);
            Monitor(rc.transform, new Vector3(-0.7f, 0.78f, 0.05f), 180f);
            Monitor(rc.transform, new Vector3(0.7f, 0.78f, 0.05f), 180f);
            Inst(pfTChair, rc.transform, new Vector3(-0.7f, 0, 0.75f), 180f, "StaffChair_A");
            Inst(pfTChair, rc.transform, new Vector3(0.7f, 0, 0.75f), 180f, "StaffChair_B");
            pfReception = SavePrefab(rc, "IT_ReceptionDesk");

            // ตู้จุดเข้าเรียนตามตาราง (ใช้คู่กับ BuildingDoor เดิม "Door_คณะ IT")
            var kk = new GameObject("IT_ClassKiosk");
            Box("Body", kk.transform, new Vector3(0, 0.8f, 0), new Vector3(0.7f, 1.6f, 0.35f), mSign, true);
            NoShadow(Box("Display", kk.transform, new Vector3(0, 1.15f, -0.18f), new Vector3(0.56f, 0.7f, 0.02f), mScreenOn, false));
            Box("Accent", kk.transform, new Vector3(0, 1.62f, 0), new Vector3(0.72f, 0.06f, 0.37f), mSignOrange, false);
            Text("Title", kk.transform, new Vector3(0, 1.3f, -0.2f), new Vector2(0.52f, 0.14f), "เข้าห้องเรียน", Color.white, 1.0f, FontStyles.Bold);
            Text("Sub", kk.transform, new Vector3(0, 1.05f, -0.2f), new Vector2(0.52f, 0.2f), "ตามตารางเรียน\nกด E", new Color(0.85f, 0.95f, 1f), 0.6f);
            pfKiosk = SavePrefab(kk, "IT_ClassKiosk");

            // บันได 1 ช่วงชั้น (U-turn): z 0 = ขอบทางเดิน, กว้าง 5.75 (ช่อง A x<0 ขึ้นไป +Z, ช่อง B x>0 ขึ้นกลับ -Z)
            pfStairLevel = MakeStairLevel();

            // ห้องแต่ละประเภท
            foreach (RoomType t in System.Enum.GetValues(typeof(RoomType))) roomPf[t.ToString()] = MakeRoom(t);
        }

        static void Monitor(Transform p, Vector3 pos, float rotY)
        {
            var m = G("Monitor", p, pos, rotY);
            Box("Stand", m, new Vector3(0, 0.08f, 0.04f), new Vector3(0.18f, 0.16f, 0.12f), mDarkGray, false);
            Box("Bezel", m, new Vector3(0, 0.33f, 0), new Vector3(0.56f, 0.34f, 0.04f), mDarkGray, false);
            NoShadow(Box("Screen", m, new Vector3(0, 0.33f, -0.022f), new Vector3(0.52f, 0.30f, 0.005f), mScreenOn, false));
        }

        // โต๊ะคอมพิวเตอร์ (ยาวตามแกน Z, ผู้ใช้หันไป -X, เก้าอี้อยู่ +X)
        static GameObject PCBench(string name, int seats)
        {
            float len = seats * 0.8f;
            var b = new GameObject(name);
            Box("Top", b.transform, new Vector3(0, 0.74f, 0), new Vector3(0.62f, 0.04f, len), mWhite, false);
            Box("Modesty", b.transform, new Vector3(-0.28f, 0.42f, 0), new Vector3(0.03f, 0.6f, len - 0.04f), mPanel, false);
            Box("Leg_A", b.transform, new Vector3(0, 0.36f, -len / 2 + 0.03f), new Vector3(0.6f, 0.72f, 0.04f), mPanel, false);
            Box("Leg_B", b.transform, new Vector3(0, 0.36f, len / 2 - 0.03f), new Vector3(0.6f, 0.72f, 0.04f), mPanel, false);
            var bc = b.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.5f, 0); bc.size = new Vector3(0.62f, 1.0f, len);
            for (int i = 0; i < seats; i++)
            {
                float z = -len / 2 + 0.4f + i * 0.8f;
                Monitor(b.transform, new Vector3(-0.14f, 0.76f, z), -90f);
                Box("Keyboard_" + i, b.transform, new Vector3(0.12f, 0.775f, z), new Vector3(0.16f, 0.02f, 0.42f), mDarkGray, false);
                Box("Mouse_" + i, b.transform, new Vector3(0.12f, 0.775f, z + 0.3f), new Vector3(0.1f, 0.025f, 0.06f), mDarkGray, false);
                Box("Tower_" + i, b.transform, new Vector3(-0.1f, 0.24f, z + 0.25f), new Vector3(0.4f, 0.45f, 0.18f), mDarkGray, false);
                Inst(pfChair, b.transform, new Vector3(0.52f, 0, z), -90f, "Chair_" + i);
            }
            return SavePrefab(b, name);
        }

        static GameObject MakeStairLevel()
        {
            var s = new GameObject("IT_StairLevel");
            float aC = -(LaneHalf + Spine) / 2, bC = (LaneHalf + Spine) / 2, lw = LaneHalf - Spine;
            var fa = G("FlightA", s.transform);
            for (int k = 0; k < StepsPerFlight - 1; k++)
            {
                float top = (k + 1) * Rise;
                BoxMM("Step_" + (k + 1), fa, new Vector3(-LaneHalf, top - 0.4f, k * Tread), new Vector3(-Spine, top, (k + 1) * Tread), mStair, false);
            }
            RampPlate(fa, "RampCollider", new Vector3(aC, 0f, -Tread), new Vector3(aC, StepsPerFlight * Rise, FlightLen), lw);
            float landY = StepsPerFlight * Rise;   // 1.8
            BoxMM("Landing", s.transform, new Vector3(-LaneHalf, landY - 0.25f, FlightLen), new Vector3(LaneHalf, landY, SlotDepth), mStair, true);
            var fb = G("FlightB", s.transform);
            for (int j = 0; j < StepsPerFlight - 1; j++)
            {
                float top = landY + (j + 1) * Rise;
                BoxMM("Step_" + (j + 1), fb, new Vector3(Spine, top - 0.4f, FlightLen - (j + 1) * Tread), new Vector3(LaneHalf, top, FlightLen - j * Tread), mStair, false);
            }
            RampPlate(fb, "RampCollider", new Vector3(bC, landY, FlightLen + Tread), new Vector3(bC, H, 0f), lw);
            // ผนังกลางระหว่างช่วงบันได (สูงเต็มชั้น)
            BoxMM("SpineWall", s.transform, new Vector3(-Spine, 0f, 0f), new Vector3(Spine, H, FlightLen), mTileWall, true);
            var hr = G("Handrails", s.transform);
            SlopedHandrail(hr, "Rail_A_Spine", new Vector3(-Spine - 0.05f, 0f, 0f), new Vector3(-Spine - 0.05f, landY, FlightLen), mRailLight);
            SlopedHandrail(hr, "Rail_A_Wall", new Vector3(-LaneHalf + 0.06f, 0f, 0f), new Vector3(-LaneHalf + 0.06f, landY, FlightLen), mRailLight);
            SlopedHandrail(hr, "Rail_B_Spine", new Vector3(Spine + 0.05f, landY, FlightLen), new Vector3(Spine + 0.05f, H, 0f), mRailLight);
            SlopedHandrail(hr, "Rail_B_Wall", new Vector3(LaneHalf - 0.06f, landY, FlightLen), new Vector3(LaneHalf - 0.06f, H, 0f), mRailLight);
            var lr = G("Rail_Landing", hr, new Vector3(0, landY + 0.9f, SlotDepth - 0.08f));
            Box("Rail", lr, Vector3.zero, new Vector3(LaneHalf * 2 - 0.1f, 0.06f, 0.06f), mRailLight, false);
            Inst(pfLightPanel, s.transform, new Vector3(0, landY + H - T - 0.03f, (FlightLen + SlotDepth) / 2), 0f, "LandingLightPanel");
            return SavePrefab(s, "IT_StairLevel");
        }

        // ห้องแบบมาตรฐาน: ภายใน x ±2.875, z ±3.075 · ประตูอยู่ผนัง z = -3.075 ช่วง x 1.2..2.5 · หน้าต่างผนัง z = +3.075 · ผนังหน้าห้อง x = -2.875
        static GameObject MakeRoom(RoomType t)
        {
            var r = new GameObject("IT_Room_" + t);
            var tr = r.transform;
            const float WX = -2.875f;
            float ceil = WallH - 0.03f;
            switch (t)
            {
                case RoomType.Classroom:
                {
                    Inst(pfWhiteboard, tr, new Vector3(WX, 0, 0), 0f, "Whiteboard");
                    Inst(pfTDesk, tr, new Vector3(-1.95f, 0, 0.3f), -90f, "TeacherDesk");
                    Inst(pfTChair, tr, new Vector3(-2.45f, 0, 0.3f), 90f, "TeacherChair");
                    var seats = G("StudentSeats", tr);
                    float[] rows = { -0.8f, 0.6f, 2.0f }; float[] cols = { -1.75f, 0f, 1.75f };
                    for (int a = 0; a < rows.Length; a++)
                        for (int k = 0; k < cols.Length; k++)
                        {
                            if (a == 2 && k == 0) continue;   // เว้นที่หน้าประตู
                            var seat = G($"Seat_R{a + 1}_C{k + 1}", seats, new Vector3(rows[a], 0, cols[k]));
                            Inst(pfDesk, seat, Vector3.zero, -90f);
                            Inst(pfChair, seat, new Vector3(0.5f, 0, 0), -90f);
                        }
                    Inst(pfCabinet, tr, new Vector3(-1.6f, 0, 2.8f), 180f, "Cabinet");
                    break;
                }
                case RoomType.ComputerLab:
                {
                    Inst(pfScreen, tr, new Vector3(WX, 0, -0.9f), 0f, "WallScreen");
                    Inst(pfWhiteboard, tr, new Vector3(WX, 0, 1.6f), 0f, "Whiteboard").transform.localScale = new Vector3(1, 1, 0.55f);
                    Inst(pfTeacherPC, tr, new Vector3(-2.0f, 0, 1.2f), 0f, "TeacherStation");
                    var benches = G("PCBenches", tr);
                    float[] rows = { -0.9f, 0.5f, 1.9f };
                    for (int a = 0; a < rows.Length; a++)
                    {
                        Inst(pfPCBench2, benches, new Vector3(rows[a], 0, 1.35f), 0f, $"Bench_R{a + 1}_Window");
                        if (a < 2) Inst(pfPCBench2, benches, new Vector3(rows[a], 0, -1.35f), 0f, $"Bench_R{a + 1}_Corridor");
                        else Inst(pfPCBench1, benches, new Vector3(rows[a], 0, -0.95f), 0f, $"Bench_R{a + 1}_Corridor");
                    }
                    break;
                }
                case RoomType.GroupWork:
                {
                    Inst(pfWhiteboard, tr, new Vector3(WX, 0, 0.2f), 0f, "Whiteboard");
                    foreach (var c in new[] { new Vector2(-1.45f, 1.0f), new Vector2(1.45f, 1.0f) })
                    {
                        var g = G("Table_" + (c.x < 0 ? "A" : "B"), tr, new Vector3(c.x, 0, c.y));
                        Inst(pfGroupTable, g, Vector3.zero, 0f, "Table");
                        Inst(pfTChair, g, new Vector3(0, 0, -0.9f), 0f, "Chair_S");
                        Inst(pfTChair, g, new Vector3(0, 0, 0.9f), 180f, "Chair_N");
                        Inst(pfTChair, g, new Vector3(-0.9f, 0, 0), 90f, "Chair_W");
                        Inst(pfTChair, g, new Vector3(0.9f, 0, 0), -90f, "Chair_E");
                    }
                    Inst(pfPlanterBox, tr, new Vector3(2.45f, 0, 2.65f), 0f, "Planter");
                    break;
                }
                case RoomType.Meeting:
                {
                    Inst(pfScreen, tr, new Vector3(WX, 0, 0.5f), 0f, "WallScreen");
                    Inst(pfMeetTable, tr, new Vector3(0.2f, 0, 0.6f), 0f, "MeetingTable");
                    foreach (var x in new[] { -0.8f, 0.2f, 1.2f })
                    {
                        Inst(pfTChair, tr, new Vector3(x, 0, -0.3f), 0f, "Chair_S");
                        Inst(pfTChair, tr, new Vector3(x, 0, 1.5f), 180f, "Chair_N");
                    }
                    Inst(pfTChair, tr, new Vector3(2.1f, 0, 0.6f), -90f, "Chair_Head");
                    Inst(pfCabinet, tr, new Vector3(2.62f, 0, -1.2f), -90f, "Cabinet");
                    break;
                }
                case RoomType.Faculty:
                {
                    foreach (var x in new[] { -1.85f, 0f, 1.85f })
                        Inst(pfOfficeDesk, tr, new Vector3(x, 0, 2.55f), 0f, "OfficeDesk");
                    Inst(pfBookshelf, tr, new Vector3(WX + 0.2f, 0, -0.9f), -90f, "Bookshelf_A");
                    Inst(pfBookshelf, tr, new Vector3(WX + 0.2f, 0, 0.3f), -90f, "Bookshelf_B");
                    var mtab = G("SmallTable", tr, new Vector3(-0.6f, 0, -0.5f));
                    Inst(pfGroupTable, mtab, Vector3.zero, 0f, "Table").transform.localScale = new Vector3(0.75f, 1f, 0.75f);
                    Inst(pfTChair, mtab, new Vector3(0, 0, 0.8f), 180f, "Chair_N");
                    Inst(pfTChair, mtab, new Vector3(0, 0, -0.8f), 0f, "Chair_S");
                    Inst(pfPlanterBox, tr, new Vector3(2.45f, 0, 0.8f), 0f, "Planter");
                    break;
                }
                case RoomType.ProjectLab:
                {
                    Inst(pfWhiteboard, tr, new Vector3(WX, 0, 0f), 0f, "Whiteboard");
                    Inst(pfWorkbench, tr, new Vector3(-0.9f, 0, 1.2f), 0f, "Workbench_A");
                    Inst(pfWorkbench, tr, new Vector3(1.5f, 0, 1.2f), 0f, "Workbench_B");
                    foreach (var x in new[] { -1.4f, -0.4f, 1.0f, 2.0f })
                        Inst(pfChair, tr, new Vector3(x, 0, 0.35f), 0f, "Stool");
                    Inst(pfBookshelf, tr, new Vector3(0.4f, 0, 2.85f), 180f, "PartsShelf");
                    Inst(pfCabinet, tr, new Vector3(-2.0f, 0, 2.8f), 180f, "Cabinet");
                    break;
                }
                case RoomType.Seminar:
                {
                    Inst(pfScreen, tr, new Vector3(WX, 0, 0.2f), 0f, "WallScreen");
                    Inst(pfPodium, tr, new Vector3(-2.25f, 0, -1.3f), 0f, "Podium");
                    var seats = G("Seats", tr);
                    float[] rows = { -0.7f, 0.5f, 1.7f }; float[] cols = { -1.2f, -0.5f, 0.9f, 1.6f, 2.3f };
                    for (int a = 0; a < rows.Length; a++)
                        foreach (var c in cols)
                            Inst(pfChair, seats, new Vector3(rows[a], 0, c), -90f, $"Chair_R{a + 1}");
                    break;
                }
                case RoomType.Staff:
                {
                    Inst(pfOfficeDesk, tr, new Vector3(-1.5f, 0, 2.55f), 0f, "OfficeDesk_A");
                    Inst(pfOfficeDesk, tr, new Vector3(0.4f, 0, 2.55f), 0f, "OfficeDesk_B");
                    Inst(pfCabinet, tr, new Vector3(WX + 0.26f, 0, -1.0f), 90f, "Cabinet_A");
                    Inst(pfCabinet, tr, new Vector3(WX + 0.26f, 0, 0.8f), 90f, "Cabinet_B");
                    var counter = G("ServiceCounter", tr, new Vector3(-0.6f, 0, -0.9f));
                    Box("Counter", counter, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 1.0f, 1.6f), mWhite, true);
                    Box("Top", counter, new Vector3(0, 1.02f, 0), new Vector3(0.7f, 0.04f, 1.7f), mWood, false);
                    Inst(pfWaterCooler, tr, new Vector3(2.5f, 0, 2.7f), 0f, "WaterCooler");
                    break;
                }
            }
            Inst(pfLightPanel, tr, new Vector3(0, ceil, 0), 0f, "CeilingLightPanel");
            return SavePrefab(r, "IT_Room_" + t);
        }

        // ------------------------------------------------------------------ structure: slabs, roof, facade frame
        static void BuildStructure(Transform s)
        {
            var slabs = G("FloorSlabs", s);
            // ชั้น 1 = ฐานยกพื้น
            var p1 = G("Slab_Floor1", slabs);
            BoxMM("Plinth_Main", p1, new Vector3(-18.15f, 0, -8.15f), new Vector3(18.15f, B, 8.15f), mCorridor);
            BoxMM("Plinth_Hall", p1, new Vector3(-6.15f, 0, -9.15f), new Vector3(6.15f, B, -8.15f), mCorridor);
            for (int i = 1; i < Floors; i++)
            {
                float y = FY(i);
                var f = G("Slab_Floor" + (i + 1), slabs);
                SlabWithHoles(f, y);
            }
            float roofY = FY(Floors);
            var roof = G("Roof", s);
            BoxMM("Roof_Main", roof, new Vector3(-18.3f, roofY - T, -8.3f), new Vector3(18.3f, roofY, 8.3f), mSlab);
            BoxMM("Roof_Hall", roof, new Vector3(-6.3f, roofY - T, -9.3f), new Vector3(6.3f, roofY, -8.3f), mSlab);
            // ขอบหลังคา (ปีก 0.9 ม. · โถงกลางยกสูง 2.2 ม. ให้เด่น)
            float pw = 0.9f;
            BoxMM("Parapet_FrontL", roof, new Vector3(-18.3f, roofY, -8.3f), new Vector3(-6.3f, roofY + pw, -8.1f), mWhite);
            BoxMM("Parapet_FrontR", roof, new Vector3(6.3f, roofY, -8.3f), new Vector3(18.3f, roofY + pw, -8.1f), mWhite);
            BoxMM("Parapet_Back", roof, new Vector3(-18.3f, roofY, 8.1f), new Vector3(18.3f, roofY + pw, 8.3f), mWhite);
            BoxMM("Parapet_West", roof, new Vector3(-18.3f, roofY, -8.3f), new Vector3(-18.1f, roofY + pw, 8.3f), mWhite);
            BoxMM("Parapet_East", roof, new Vector3(18.1f, roofY, -8.3f), new Vector3(18.3f, roofY + pw, 8.3f), mWhite);
            // โถงกลาง: หลังคาแผ่นยื่นใหญ่ ใต้ฝ้าสีเข้ม (ตามภาพอ้างอิงภาพที่ 1)
            var crown = G("HallRoofOverhang", roof);
            BoxMM("Upstand_L", crown, new Vector3(-6.3f, roofY, -9.3f), new Vector3(-6.0f, roofY + 1.0f, 0f), mWhite);
            BoxMM("Upstand_R", crown, new Vector3(6.0f, roofY, -9.3f), new Vector3(6.3f, roofY + 1.0f, 0f), mWhite);
            BoxMM("Overhang_Top", crown, new Vector3(-6.8f, roofY + 1.0f, -12.6f), new Vector3(6.8f, roofY + 1.35f, 0.3f), mWhite);
            BoxMM("Overhang_Soffit", crown, new Vector3(-6.75f, roofY + 0.95f, -12.55f), new Vector3(6.75f, roofY + 1.0f, 0.25f), mDarkGray, false);
            BoxMM("Overhang_Fascia", crown, new Vector3(-6.8f, roofY + 0.85f, -12.65f), new Vector3(6.8f, roofY + 1.4f, -12.55f), mWhite, false);
            // หอบันไดปลายอาคาร สีส้มอิฐ สูงเหนือหลังคา + ช่องเปิดแนวตั้ง (ภาพที่ 2–3)
            var ov = G("StairOverruns", roof);
            BoxMM("EndStair_Tower", ov, new Vector3(12.0f, roofY, 1.5f), new Vector3(18.3f, roofY + 3.6f, 8.3f), mBrick);
            BoxMM("EndStair_Cap", ov, new Vector3(11.95f, roofY + 3.6f, 1.45f), new Vector3(18.35f, roofY + 3.7f, 8.35f), mBrick, false);
            BoxMM("Tower_Slot_Tall", ov, new Vector3(18.31f, roofY + 0.4f, 4.3f), new Vector3(18.33f, roofY + 3.0f, 5.1f), mDarkGray, false);
            BoxMM("Tower_Slot_Small", ov, new Vector3(18.31f, roofY + 2.2f, 6.4f), new Vector3(18.33f, roofY + 3.0f, 7.0f), mDarkGray, false);
            BoxMM("MainStair_Overrun", ov, new Vector3(-6.1f, roofY, 1.5f), new Vector3(0.1f, roofY + 1.8f, 8.1f), mPanel);
            // อุปกรณ์บนหลังคา
            var units = G("RoofUnits", roof);
            foreach (var x in new[] { -15f, -11f, 8f })
                BoxMM("ACUnit", units, new Vector3(x, roofY, 3f), new Vector3(x + 1.6f, roofY + 0.9f, 4.4f), mPanel, false);

            // โครงหน้าอาคาร: เสาขาวที่แนวแบ่งห้อง + คานขอบพื้น (แยกตามชั้นเพื่อให้ตัดชั้นได้)
            for (int i = 0; i < Floors; i++)
            {
                float y = FY(i);
                var fr = G("FacadeFrame_" + (i + 1), s);
                foreach (var x in new[] { -18f, -12f, 12f, 18f })
                {
                    BoxMM("Pier_Front", fr, new Vector3(x - 0.25f, y, -8.45f), new Vector3(x + 0.25f, y + WallH, -8.0f), mWhite, false);
                    BoxMM("Pier_Back", fr, new Vector3(x - 0.25f, y, 8.0f), new Vector3(x + 0.25f, y + WallH, 8.45f), mWhite, false);
                }
                foreach (var x in new[] { -6f, 6f })
                    BoxMM("Pier_Back", fr, new Vector3(x - 0.25f, y, 8.0f), new Vector3(x + 0.25f, y + WallH, 8.45f), mWhite, false);
                // คานขอบพื้น (บนสุดของแต่ละชั้น)
                float by = y + WallH;
                BoxMM("Band_FrontL", fr, new Vector3(-18.3f, by, -8.4f), new Vector3(-6.15f, by + T, -8.0f), mWhite, false);
                BoxMM("Band_FrontR", fr, new Vector3(6.15f, by, -8.4f), new Vector3(18.3f, by + T, -8.0f), mWhite, false);
                BoxMM("Band_Back", fr, new Vector3(-18.3f, by, 8.0f), new Vector3(18.3f, by + T, 8.4f), mWhite, false);
                BoxMM("Band_West", fr, new Vector3(-18.4f, by, -8.4f), new Vector3(-18.0f, by + T, 8.4f), mWhite, false);
                BoxMM("Band_East", fr, new Vector3(18.0f, by, -8.4f), new Vector3(18.4f, by + T, 1.5f), mWhite, false);
                // กรอบโถงกลาง (เสาและคานรอบผนังกระจก)
                foreach (var x in new[] { -6.15f, -2f, 2f, 6.15f })
                    BoxMM("HallMullionPier", fr, new Vector3(x - 0.12f, y, -9.45f), new Vector3(x + 0.12f, y + WallH, -9.15f), mWhite, false);
                BoxMM("HallBand", fr, new Vector3(-6.3f, by, -9.45f), new Vector3(6.3f, by + T, -9.0f), mWhite, false);
            }
        }

        static void SlabWithHoles(Transform f, float y)
        {
            float y0 = y - T;
            BoxMM("Front", f, new Vector3(-18.15f, y0, -8.15f), new Vector3(18.15f, y, 1.5f), mCorridor);
            BoxMM("Hall", f, new Vector3(-6.15f, y0, -9.15f), new Vector3(6.15f, y, -8.15f), mCorridor);
            // ช่องบันไดหลัก x -5.9..-0.1 และบันไดปลายอาคาร x 12.1..17.85 (z 1.5..7.85)
            BoxMM("Back_A", f, new Vector3(-18.15f, y0, 1.5f), new Vector3(-5.9f, y, 8.15f), mCorridor);
            BoxMM("Back_MainStairEdge", f, new Vector3(-5.9f, y0, 7.85f), new Vector3(-0.1f, y, 8.15f), mCorridor);
            BoxMM("Back_B", f, new Vector3(-0.1f, y0, 1.5f), new Vector3(12.1f, y, 8.15f), mCorridor);
            BoxMM("Back_EndStairEdge", f, new Vector3(12.1f, y0, 7.85f), new Vector3(18.15f, y, 8.15f), mCorridor);
            BoxMM("Back_EndStairSide", f, new Vector3(17.85f, y0, 1.5f), new Vector3(18.15f, y, 7.85f), mCorridor);
        }

        // ------------------------------------------------------------------ floors
        static void BuildFloor(Transform fl, int i)
        {
            float y = FY(i), h = WallH;
            int f = i + 1;
            var ext = G("ExteriorWalls", fl);
            var walls = G("InteriorWalls", fl);
            var rooms = G("Rooms", fl);
            var hall = G(i == 0 ? "Hall_Lobby" : "Hall_Lounge", fl);
            var lights = G("Lights", fl);
            var signs = G("Signs", fl);
            var corridor = G("Corridor", fl);

            // ---------- ผนังภายนอก ----------
            float sill = 0.9f, head = 2.7f;
            var front = new List<Op>(); var back = new List<Op>(); var backOrange = new List<Op>();
            foreach (var rs in RoomSlots)
            {
                float c = SlotCenter(rs);
                (rs.front ? front : back).Add(new Op(c - 2.2f, c + 2.2f, sill, head));
            }
            // หลัง: ห้องน้ำ (หน้าต่างสูง) · บันไดหลัก (หน้าต่างระดับชานพัก)
            back.Add(new Op(0.8f, 2.2f, 1.8f, head)); back.Add(new Op(3.8f, 5.2f, 1.8f, head));
            back.Add(new Op(-5.0f, -1.0f, 2.0f, h));
            backOrange.Add(new Op(14.6f, 15.4f, 0.6f, h));   // ช่องแนวตั้งแคบ (ภาพที่ 3)
            var wf = WallX(ext, "Wall_Front_West", -17.85f, -6.15f, -8f, y, h, mPanel, front.FindAll(o => o.a < 0), WE);
            var wfe = WallX(ext, "Wall_Front_East", 6.15f, 17.85f, -8f, y, h, mPanel, front.FindAll(o => o.a > 0), WE);
            WallX(ext, "Wall_Back", -17.85f, 12.1f, 8f, y, h, mPanel, back, WE);
            WallX(ext, "Wall_Back_StairOrange", 12.1f, 17.85f, 8f, y, h, mBrick, backOrange, WE);
            // ผนังปลายอาคาร: ตะวันตก (-X) หน้าต่าง 2 ห้อง + ปลายทางเดิน · ตะวันออก (+X) ห้อง + ประตูทางออกข้าง (ชั้น 1) + ผนังส้มส่วนบันได
            var westOps = new List<Op> { new Op(-7.0f, -2.6f, sill, head), new Op(-0.8f, 0.8f, sill, head), new Op(2.6f, 7.0f, sill, head) };
            WallZ(ext, "Wall_End_West", -8.15f, 8.15f, -18f, y, h, mPanel, westOps, WE);
            // ชั้น 3–4: ผนังปลายปีกเป็นแผงขาวทึบ ติดป้ายคณะ (ภาพที่ 3)
            bool signWall = i >= 2;
            var eastOps = new List<Op>();
            if (!signWall) eastOps.Add(new Op(-7.0f, -2.6f, sill, head));
            if (i == 0) eastOps.Add(new Op(-0.65f, 0.65f, 0f, DoorH)); else eastOps.Add(new Op(-0.8f, 0.8f, sill, head));
            WallZ(ext, "Wall_End_East", -8.15f, 1.5f, 18f, y, h, signWall ? mWhite : mPanel, eastOps, WE);
            WallZ(ext, "Wall_End_StairOrange", 1.5f, 8.15f, 18f, y, h, mBrick, new List<Op> { new Op(4.4f, 5.2f, 0.6f, h), new Op(6.4f, 7.0f, 2.2f, 2.8f) }, WE);
            // หน้าต่างภายนอก (Prefab ใช้ซ้ำ)
            var win = G("Windows", ext);
            var wWide = WindowPrefab(4.4f, head - sill);
            var wSmall = WindowPrefab(1.4f, head - 1.8f);
            var wStair = WindowPrefab(4.0f, h - 2.0f);
            var wEnd = WindowPrefab(1.6f, head - sill);
            var shades = G("Sunshades", ext);
            foreach (var rs in RoomSlots)
            {
                float c = SlotCenter(rs);
                float zc = rs.front ? -8f : 8f;
                Inst(wWide, win, new Vector3(c, y + (sill + head) / 2, zc), 0f, "Window_" + RoomCode(i, System.Array.IndexOf(RoomSlots, rs)));
                if (!rs.front) Inst(pfSunshade, shades, new Vector3(c, y + head + 0.08f, 8.15f), 180f, "Sunshade");
                else if (c > 0) Inst(pfLouverScreen, shades, new Vector3(c, y, -8.15f), 0f, "LouverScreen");   // ปีก +X: แผงบังแดดแนวนอนสีอิฐ + ครีบขาว
            }
            // ปีก -X ด้านหน้า: ระเบียงยื่น + ราวเหล็ก + แผงภาพขาวดำ (ภาพที่ 1)
            if (i >= 1)
            {
                var bal = G("Balcony_FrontWest", ext);
                BoxMM("Slab", bal, new Vector3(-18.3f, y - T, -9.6f), new Vector3(-6.15f, y, -8.15f), mWhite, false);
                foreach (var ry in new[] { 0.45f, 1.0f })
                    BoxMM("Rail", bal, new Vector3(-18.25f, y + ry - 0.03f, -9.58f), new Vector3(-6.2f, y + ry + 0.03f, -9.52f), mRailLight, false);
                for (float x = -18.1f; x <= -6.2f; x += 1.5f)
                    BoxMM("Post", bal, new Vector3(x - 0.025f, y, -9.58f), new Vector3(x + 0.025f, y + 1.0f, -9.52f), mRailLight, false);
                if (i <= 2)
                {
                    float[] px = i == 1 ? new[] { -16.6f, -13.4f, -10.2f, -7.4f } : new[] { -17.2f, -14.6f, -11.6f, -8.6f };
                    for (int k = 0; k < px.Length; k++)
                    {
                        var pnl = G("PortraitPanel_" + k, bal, new Vector3(px[k], y + 1.55f, -9.64f));
                        Box("Frame", pnl, Vector3.zero, new Vector3(1.75f, 2.3f, 0.04f), mWhite, false);
                        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);   // Quad = UV ตั้งตรง (ด้านที่มองเห็นคือ -Z)
                        q.name = "Print"; q.transform.SetParent(pnl, false);
                        q.transform.localPosition = new Vector3(0, 0, -0.025f); q.transform.localScale = new Vector3(1.6f, 2.15f, 1f);
                        q.GetComponent<Renderer>().sharedMaterial = mPortraits[(k + i) % mPortraits.Length];
                        Object.DestroyImmediate(q.GetComponent<Collider>());
                    }
                }
            }
            if (i == 2)
            {
                // ป้ายคณะบนผนังขาวปลายปีก (หันออกด้าน +X) + ตราสัญลักษณ์ทั่วไป
                var fs2 = G("Sign_Faculty_EndWall", ext, new Vector3(18.17f, y + 2.9f, -4.6f), -90f);
                Text("Thai", fs2, new Vector3(0.7f, 0.25f, 0), new Vector2(5.4f, 0.55f), "คณะวิทยาการสารสนเทศ", new Color(0.16f, 0.14f, 0.33f), 6f, FontStyles.Bold);
                Text("Eng", fs2, new Vector3(0.7f, -0.3f, 0), new Vector2(5.4f, 0.45f), "FACULTY OF INFORMATICS", new Color(0.16f, 0.14f, 0.33f), 5f, FontStyles.Bold);
                var crest = G("Crest", fs2, new Vector3(-2.6f, 0f, 0.01f));
                Box("Shield", crest, Vector3.zero, new Vector3(0.95f, 1.2f, 0.04f), mWhite, false);
                Box("Rim", crest, new Vector3(0, 0, 0.005f), new Vector3(1.05f, 1.3f, 0.03f), mDarkGray, false);
                Box("Spire", crest, new Vector3(0, 0.05f, -0.03f), new Vector3(0.12f, 0.8f, 0.02f), mSign, false);
                Box("Base", crest, new Vector3(0, -0.4f, -0.03f), new Vector3(0.6f, 0.08f, 0.02f), mSign, false);
            }
            Inst(wSmall, win, new Vector3(1.5f, y + (1.8f + head) / 2, 8f), 0f, "Window_RestroomM");
            Inst(wSmall, win, new Vector3(4.5f, y + (1.8f + head) / 2, 8f), 0f, "Window_RestroomF");
            Inst(wStair, win, new Vector3(-3.0f, y + (2.0f + h) / 2, 8f), 0f, "Window_MainStair");
            Inst(pfSunshade, shades, new Vector3(3.0f, y + head + 0.08f, 8.15f), 180f, "Sunshade_Restroom");
            Inst(WindowPrefab(0.8f, h - 0.6f, true), win, new Vector3(15.0f, y + (0.6f + h) / 2, 8f), 0f, "Slot_EndStairBack");
            Inst(WindowPrefab(0.8f, h - 0.6f, true), win, new Vector3(18f, y + (0.6f + h) / 2, 4.8f), 90f, "Slot_EndStairSide");
            Inst(WindowPrefab(0.6f, 0.6f, true), win, new Vector3(18f, y + 2.5f, 6.7f), 90f, "Slot_EndStairSmall");
            if (i >= 1)
            {
                // บันไดภายนอกสีขาว + ราวเงิน เกาะผนังส้ม (ตกแต่ง ไม่มีทางขึ้นจากพื้น — ภาพที่ 2–3)
                var es = G("ExteriorStair_Deco", ext);
                float x0 = 18.2f, x1 = 19.4f;
                int n = 12;
                // ซิกแซก: ชั้นคี่ขึ้นไปทาง -Z, ชั้นคู่ขึ้นไปทาง +Z
                bool down = i % 2 == 1;
                for (int k = 0; k < n; k++)
                {
                    float top = y - H + 1.8f + (k + 1) * 0.15f;
                    if (i == 1 && top < y - 1.0f) continue;
                    float zA = down ? 6.6f - k * 0.38f : 3.0f + k * 0.38f;
                    float zB = down ? zA - 0.38f : zA + 0.38f;
                    BoxMM("Step", es, new Vector3(x0, top - 0.12f, Mathf.Min(zA, zB)), new Vector3(x1, top, Mathf.Max(zA, zB)), mWhite, false);
                }
                float lz0 = down ? 1.7f : 7.56f;
                BoxMM("Landing", es, new Vector3(x0, y - 0.12f, lz0), new Vector3(x1 + 0.2f, y, lz0 + 0.9f), mWhite, false);
                BoxMM("LandingLow", es, new Vector3(x0, y - 1.92f, down ? 6.6f : 2.1f), new Vector3(x1 + 0.2f, y - 1.8f, down ? 7.5f : 3.0f), mWhite, false);
                BoxMM("RailTop", es, new Vector3(x1 + 0.12f, y + 0.95f, 2.0f), new Vector3(x1 + 0.16f, y + 1.0f, 7.6f), mMetal, false);
                BoxMM("RailMid", es, new Vector3(x1 + 0.12f, y + 0.5f, 2.0f), new Vector3(x1 + 0.16f, y + 0.54f, 7.6f), mMetal, false);
                for (float z = 2.1f; z < 7.6f; z += 1.1f)
                    BoxMM("RailPost", es, new Vector3(x1 + 0.11f, y - 1.0f, z), new Vector3(x1 + 0.17f, y + 1.0f, z + 0.05f), mMetal, false);
            }
            Inst(wWide, win, new Vector3(-18f, y + (sill + head) / 2, -4.8f), 90f, "Window_WestFront");
            Inst(wWide, win, new Vector3(-18f, y + (sill + head) / 2, 4.8f), 90f, "Window_WestBack");
            Inst(wEnd, win, new Vector3(-18f, y + (sill + head) / 2, 0f), 90f, "Window_WestCorridor");
            if (!signWall) Inst(wWide, win, new Vector3(18f, y + (sill + head) / 2, -4.8f), 90f, "Window_EastFront");
            if (i > 0) Inst(wEnd, win, new Vector3(18f, y + (sill + head) / 2, 0f), 90f, "Window_EastCorridor");

            // โถงกลางด้านหน้า: ผนังกระจกเต็มชั้น (ชั้น 1 เว้นทางเข้า 3 ม.)
            var cw = G("HallCurtainWall", ext);
            BoxMM("Side_West", cw, new Vector3(-6.15f, y, -9.15f), new Vector3(-5.85f, y + h, -7.85f), mWhite);
            BoxMM("Side_East", cw, new Vector3(5.85f, y, -9.15f), new Vector3(6.15f, y + h, -7.85f), mWhite);
            if (i == 0)
            {
                foreach (var sg in new[] { -1f, 1f })
                {
                    float a = sg < 0 ? -5.85f : 1.6f, b = sg < 0 ? -1.6f : 5.85f;
                    NoShadow(Box("Glass", cw, new Vector3((a + b) / 2, y + h / 2, -9f), new Vector3(b - a, h, 0.06f), mGlass, true));
                    for (float x = a + 1.4f; x < b - 0.3f; x += 1.4f)
                        Box("Mullion", cw, new Vector3(x, y + h / 2, -9f), new Vector3(0.06f, h, 0.12f), mFrame, false);
                    Box("Transom", cw, new Vector3((a + b) / 2, y + 2.6f, -9f), new Vector3(b - a, 0.06f, 0.12f), mFrame, false);
                }
                foreach (var x in new[] { -1.6f, 1.6f })
                    BoxMM("EntrancePost", cw, new Vector3(x - 0.1f, y, -9.12f), new Vector3(x + 0.1f, y + h, -8.88f), mDarkGray);
                NoShadow(Box("EntranceTransomGlass", cw, new Vector3(0, y + (2.8f + h) / 2, -9f), new Vector3(3.0f, h - 2.8f, 0.06f), mGlass, true));
                Box("EntranceHead", cw, new Vector3(0, y + 2.8f, -9f), new Vector3(3.2f, 0.12f, 0.16f), mDarkGray, false);
                // ประตูบานเลื่อนกระจก (เปิดค้าง) เป็นฉากข้างทางเข้า
                NoShadow(Box("SlidingLeaf_L", cw, new Vector3(-2.35f, y + 1.4f, -8.92f), new Vector3(1.4f, 2.6f, 0.04f), mGlassDark, false));
                NoShadow(Box("SlidingLeaf_R", cw, new Vector3(2.35f, y + 1.4f, -8.92f), new Vector3(1.4f, 2.6f, 0.04f), mGlassDark, false));
            }
            else
            {
                NoShadow(Box("Glass", cw, new Vector3(0, y + h / 2, -9f), new Vector3(11.7f, h, 0.06f), mGlass, true));
                for (float x = -4.2f; x < 5f; x += 1.4f)
                    Box("Mullion", cw, new Vector3(x, y + h / 2, -9f), new Vector3(0.06f, h, 0.12f), mFrame, false);
                Box("Transom", cw, new Vector3(0, y + 1.0f, -9f), new Vector3(11.7f, 0.08f, 0.12f), mFrame, false);
                // ราวกันตกภายในหน้ากระจก (ระดับ 1.0 ม.)
                var hr = G("GuardRail", cw, new Vector3(0, y + 1.0f, -8.85f));
                Box("Rail", hr, Vector3.zero, new Vector3(11.6f, 0.06f, 0.06f), mRail, false);
            }

            // ---------- ผนังภายใน ----------
            var corrFront = new List<Op>(); var corrBack = new List<Op>();
            for (int r = 0; r < RoomSlots.Length; r++)
            {
                var ds = DoorSpan(RoomSlots[r]);
                (RoomSlots[r].front ? corrFront : corrBack).Add(new Op(ds.x, ds.y, 0f, DoorH));
            }
            WallX(walls, "Wall_Corridor_FrontWest", -17.85f, -5.9f, -1.6f, y, h, mTileWall, corrFront.FindAll(o => o.a < 0));
            WallX(walls, "Wall_Corridor_FrontEast", 5.9f, 17.85f, -1.6f, y, h, mTileWall, corrFront.FindAll(o => o.a > 0));
            WallX(walls, "Wall_Corridor_BackWest", -17.85f, -6.1f, 1.6f, y, h, mTileWall, corrBack.FindAll(o => o.a < 0));
            WallX(walls, "Wall_Corridor_Restrooms", 0.1f, 5.9f, 1.6f, y, h, mTileWall, new List<Op> { new Op(0.35f, 0.35f + DoorW, 0f, DoorH), new Op(3.35f, 3.35f + DoorW, 0f, DoorH) });
            WallX(walls, "Wall_Corridor_BackEast", 6.1f, 11.9f, 1.6f, y, h, mTileWall, corrBack.FindAll(o => o.a > 0));
            foreach (var x in new[] { -12f, 12f })
                WallZ(walls, "Wall_Partition_Front_" + x, -7.85f, -1.7f, x, y, h, mTileWall);
            foreach (var x in new[] { -6f, 6f })
                WallZ(walls, "Wall_HallSide_" + x, -7.85f, -1.5f, x, y, h, mTileWall);
            WallZ(walls, "Wall_Partition_Back_-12", 1.7f, 7.85f, -12f, y, h, mTileWall);
            WallZ(walls, "Wall_MainStair_West", 1.5f, 7.85f, -6f, y, h, mTileWall);
            WallZ(walls, "Wall_MainStair_East", 1.5f, 7.85f, 0f, y, h, mTileWall);
            WallZ(walls, "Wall_RestroomDivider", 1.7f, 7.85f, 3f, y, h, mTileWall);
            WallZ(walls, "Wall_Restroom_East", 1.5f, 7.85f, 6f, y, h, mTileWall);
            WallZ(walls, "Wall_EndStair_West", 1.5f, 7.85f, 12f, y, h, mTileWall);

            // ---------- ห้อง ----------
            for (int r = 0; r < RoomSlots.Length; r++)
            {
                var rs = RoomSlots[r];
                string code = RoomCode(i, r);
                var type = FloorRooms[i][r];
                string nm = FloorRoomNames[i][r];
                float cx = SlotCenter(rs), cz = RoomCenterZ(rs);
                var room = G("Room_" + code, rooms);
                Material fm = type == RoomType.ComputerLab || type == RoomType.ProjectLab ? mLabFloor : mRoomFloor;
                BoxMM("FloorFinish", room, new Vector3(cx - 2.88f, y, cz - 3.075f), new Vector3(cx + 2.88f, y + 0.01f, cz + 3.075f), fm, false);
                Inst(roomPf[type.ToString()], room, new Vector3(cx, y, cz), rs.front ? 180f : 0f, "Interior_" + type);
                AddPointLight(lights, "RoomLight_" + code, new Vector3(cx, y + h - 0.45f, cz), 7.5f, 2.2f);
                var ds = DoorSpan(rs);
                // บานพับอยู่ฝั่งผนังกั้นห้อง → บานเปิดแนบผนังข้าง ไม่ขวางทางเดินในห้อง (openAngle +90 = เปิดไปทาง -Z ท้องถิ่น)
                var dg = rs.front ? Inst(pfDoor, room, new Vector3(ds.x, y, -1.6f), 0f, "Door_" + code)
                                  : Inst(pfDoor, room, new Vector3(ds.y, y, 1.6f), 180f, "Door_" + code);
                var gd = dg.GetComponent<GEDoor>();
                gd.openAngle = 90f;
                gd.hinge.localRotation = Quaternion.Euler(0, 90f, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(gd);
                PrefabUtility.RecordPrefabInstancePropertyModifications(gd.hinge);
                var sg = Inst(pfRoomSign, signs, new Vector3(rs.front ? ds.y + 0.55f : ds.x - 0.55f, y + 1.75f, rs.front ? -1.485f : 1.485f), rs.front ? 180f : 0f, "Sign_" + code);
                var tmps = sg.GetComponentsInChildren<TextMeshPro>();
                foreach (var tm in tmps) { if (tm.name == "Code") tm.text = code; else if (tm.name == "Name") tm.text = nm; }
                // ป้ายในห้อง (ผนังหน้าห้อง)
                var inner = Text("RoomLabel", room, Vector3.zero, new Vector2(2.2f, 0.25f), code + "  " + nm, new Color(0.12f, 0.2f, 0.38f), 2f);
                float wx = rs.front ? cx + 2.875f - 0.03f : cx - 2.875f + 0.03f;
                inner.transform.localPosition = new Vector3(wx, y + 2.55f, cz);
                inner.transform.localRotation = Quaternion.Euler(0, rs.front ? 90f : -90f, 0);
            }

            // ---------- ห้องน้ำ ----------
            var rest = G("Restrooms", fl);
            string[] rn = { "ห้องน้ำชาย", "ห้องน้ำหญิง" }; string[] ids = { "Male", "Female" };
            for (int k = 0; k < 2; k++)
            {
                float rx0 = k * 3f;
                var rm = G("Restroom_" + ids[k], rest);
                BoxMM("FloorFinish", rm, new Vector3(rx0 + 0.1f, y, 1.7f), new Vector3(rx0 + 2.9f, y + 0.01f, 7.85f), mRestFloor, false);
                Inst(pfRestroom, rm, new Vector3(rx0 + 1.5f, y, 4.43f), 0f, "Fixtures");
                Inst(pfDoor, rm, new Vector3(rx0 + 0.35f, y, 1.6f), 0f, "Door_Restroom_" + ids[k]);
                var plate = G("Sign_" + ids[k], signs, new Vector3(rx0 + 1.0f, y + 2.5f, 1.485f));
                Box("Plate", plate, Vector3.zero, new Vector3(1.0f, 0.3f, 0.03f), k == 0 ? mSign : mSignOrange, false);
                Text("Label", plate, new Vector3(0, 0, -0.02f), new Vector2(0.92f, 0.24f), rn[k] + (k == 0 ? "  MALE" : "  FEMALE"), Color.white, 1.5f);
            }

            // ---------- โถงกลาง ----------
            BoxMM("HallFloorAccent", hall, new Vector3(-5.85f, y, -8.95f), new Vector3(5.85f, y + 0.008f, -1.55f), mRoomFloor, false);
            if (i == 0)
            {
                Inst(pfReception, hall, new Vector3(-3.6f, y, -4.6f), 0f, "InformationDesk");
                var ps = G("Sign_Information", hall, new Vector3(-3.6f, y + 2.65f, -4.95f));
                Box("Plate", ps, Vector3.zero, new Vector3(2.4f, 0.45f, 0.04f), mSign, false);
                Text("Label", ps, new Vector3(0, 0.05f, -0.03f), new Vector2(2.3f, 0.26f), "ประชาสัมพันธ์", Color.white, 2.5f, FontStyles.Bold);
                Text("Sub", ps, new Vector3(0, -0.15f, -0.03f), new Vector2(2.3f, 0.12f), "INFORMATION", new Color(1f, 0.75f, 0.5f), 1.2f);
                var couch = Load("Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Couch_01.prefab");
                var seat = G("SeatingArea", hall);
                if (couch != null)
                {
                    Inst(couch, seat, new Vector3(3.6f, y, -7.6f), 0f, "Couch_A");
                    Inst(couch, seat, new Vector3(5.1f, y, -5.6f), -90f, "Couch_B");
                }
                Inst(pfGroupTable, seat, new Vector3(3.6f, y, -5.6f), 0f, "CoffeeTable").transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
                Inst(pfBench, seat, new Vector3(2.6f, y, -3.0f), 0f, "Bench");
                Inst(pfPlanterBox, hall, new Vector3(-5.4f, y, -8.5f), 0f, "Planter_W");
                Inst(pfPlanterBox, hall, new Vector3(5.4f, y, -8.5f), 0f, "Planter_E");
                Inst(pfKiosk, hall, new Vector3(5.45f, y, -2.6f), -90f, "ClassKiosk");
                G("ClassKiosk_DoorPoint", hall, new Vector3(4.6f, y, -2.6f), -90f);   // จุดยืนกด E (ย้าย Door_คณะ IT เดิมมาที่นี่ตอนวางในฉาก)
            }
            else
            {
                Inst(pfBench, hall, new Vector3(-3.2f, y, -8.1f), 180f, "WindowBench_A");
                Inst(pfBench, hall, new Vector3(3.2f, y, -8.1f), 180f, "WindowBench_B");
                if (i == 2)
                {
                    Inst(pfGroupTable, hall, new Vector3(-3.0f, y, -5.0f), 0f, "StudyTable_A");
                    Inst(pfGroupTable, hall, new Vector3(3.0f, y, -5.0f), 0f, "StudyTable_B");
                    foreach (var x in new[] { -3.0f, 3.0f })
                    {
                        Inst(pfTChair, hall, new Vector3(x, y, -5.9f), 0f, "Chair");
                        Inst(pfTChair, hall, new Vector3(x, y, -4.1f), 180f, "Chair");
                    }
                }
                else
                {
                    Inst(pfBench, hall, new Vector3(-3.2f, y, -4.6f), 0f, "LoungeBench_A");
                    Inst(pfBench, hall, new Vector3(3.2f, y, -4.6f), 0f, "LoungeBench_B");
                }
                Inst(pfPlanterBox, hall, new Vector3(-5.4f, y, -8.5f), 0f, "Planter_W");
                Inst(pfPlanterBox, hall, new Vector3(5.4f, y, -8.5f), 0f, "Planter_E");
            }
            if (i > 0) Inst(pfWaterCooler, hall, new Vector3(5.6f, y, -2.2f), -90f, "WaterCooler");

            // ป้ายบอกชั้น + ผังชั้น (ผนังข้างโถง หันเข้าโถง)
            var fs = G("Sign_Floor", signs, new Vector3(-5.885f, y + 2.0f, -5.2f), -90f);
            Box("Plate", fs, Vector3.zero, new Vector3(1.6f, 0.9f, 0.03f), mSign, false);
            Box("Accent", fs, new Vector3(0, -0.42f, -0.005f), new Vector3(1.6f, 0.06f, 0.03f), mSignOrange, false);
            Text("Label", fs, new Vector3(0, 0.12f, -0.02f), new Vector2(1.5f, 0.5f), "ชั้น " + f, Color.white, 5f, FontStyles.Bold);
            Text("Sub", fs, new Vector3(0, -0.24f, -0.02f), new Vector2(1.5f, 0.16f), $"FLOOR {f}  ·  IT{f}01 – IT{f}07", new Color(0.9f, 0.95f, 1f), 1.6f);
            var dir = G("Sign_Directory", signs, new Vector3(5.885f, y + 1.9f, -5.6f), 90f);
            if (i == 0) dir.localPosition = new Vector3(5.885f, y + 2.1f, -5.6f);
            Box("Plate", dir, Vector3.zero, new Vector3(2.6f, 1.15f, 0.03f), mSignDark, false);
            string[] usage = { "โถงต้อนรับ · ห้องเรียน · ห้องเจ้าหน้าที่", "ห้องปฏิบัติการคอมพิวเตอร์", "ห้องเรียน · ห้องทำงานกลุ่ม · ห้องประชุม", "ห้องอาจารย์ · ห้องปฏิบัติการโครงงาน · ห้องสัมมนา" };
            string dirText = "คณะวิทยาการสารสนเทศ  ชั้น " + f + "\n" + usage[i] +
                             "\nชั้น 1 ต้อนรับ/เรียน · ชั้น 2 แล็บคอม · ชั้น 3 เรียน/ประชุม · ชั้น 4 อาจารย์/สัมมนา" +
                             "\nบันไดหลัก: หลังโถงกลาง   ·   บันไดหนีไฟ: ปลายปีกตะวันตก";
            Text("Label", dir, new Vector3(0, 0, -0.02f), new Vector2(2.45f, 1.0f), dirText, Color.white, 1.6f);

            // ทางเดินกลาง: ป้ายทางออกแขวน + ถังขยะ
            foreach (var x in new[] { -9f, 9f })
            {
                var es = Inst(pfExitSign, corridor, new Vector3(x, y + WallH - 0.35f, 0f), x < 0 ? 90f : -90f, "ExitSign_Hanging");
                Box("Hanger", es.transform, new Vector3(0, 0.2f, 0), new Vector3(0.02f, 0.2f, 0.02f), mRail, false);
                var arrow = Text("Arrow", es.transform, new Vector3(0, -0.2f, -0.035f), new Vector2(0.6f, 0.14f), x < 0 ? "▲ บันไดหลัก" : "▲ บันไดหลัก  ·  บันไดหนีไฟ ด้านหลัง", Color.white, 0.7f);
                arrow.transform.localPosition = new Vector3(0, -0.22f, -0.035f);
            }
            // ป้ายทางออกเหนือทางเข้าบันได
            Inst(pfExitSign, signs, new Vector3(MainStairX, y + 2.75f, 1.45f), 0f, "ExitSign_MainStair");
            Inst(pfExitSign, signs, new Vector3(EndStairX, y + 2.75f, 1.45f), 0f, "ExitSign_EndStair");
            if (i == 0)
            {
                Inst(pfExitSign, signs, new Vector3(0f, y + 3.0f, -8.85f), 180f, "ExitSign_MainEntrance");
                Inst(pfExitSign, signs, new Vector3(17.8f, y + 2.5f, 0f), -90f, "ExitSign_SideExit");
            }

            // ไฟ (กลุ่มต่อชั้น)
            float ly = y + h - 0.03f;
            foreach (var x in new[] { -15f, -9f, -3f, 3f, 9f, 15f })
                Inst(pfLightPanel, corridor, new Vector3(x, ly, 0f), 90f, "CorridorLightPanel");
            foreach (var x in new[] { -13.5f, -4.5f, 4.5f, 13.5f })
                AddPointLight(lights, "CorridorLight", new Vector3(x, y + h - 0.45f, 0f), 7.5f, 2.0f);
            foreach (var x in new[] { -3f, 3f })
            {
                Inst(pfLightPanel, hall, new Vector3(x, ly, -5.5f), 0f, "HallLightPanel");
                AddPointLight(lights, "HallLight", new Vector3(x, y + h - 0.45f, -5.5f), 7.5f, 2.2f);
            }
            foreach (var grp in new[] { walls, rooms, hall, lights, signs, corridor, rest })
                NoShadowAll(grp.gameObject);
            // ไฟใน prefab ซ้อน (ห้องน้ำ GE_RestroomFixtures) → ปิดตัวเดิม แล้วสร้างไฟแทนในกลุ่ม Lights ของชั้น (ให้ LightZone คุม)
            foreach (var l in fl.GetComponentsInChildren<Light>(true))
            {
                if (l.transform.IsChildOf(lights)) continue;
                var nl = AddPointLight(lights, "Proxy_" + l.transform.parent.parent.parent.name, l.transform.position, l.range, l.intensity);
                nl.type = l.type; nl.spotAngle = l.spotAngle; nl.innerSpotAngle = l.innerSpotAngle; nl.color = l.color;
                nl.transform.rotation = l.transform.rotation;
                l.enabled = false;
            }
        }

        static Light AddPointLight(Transform group, string name, Vector3 localPos, float range, float intensity)
        {
            var t = G(name, group);
            t.localPosition = localPos;
            var l = t.gameObject.AddComponent<Light>();
            l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = new Color(1f, 0.97f, 0.92f);
            l.shadows = LightShadows.None;
            return l;
        }

        // ------------------------------------------------------------------ stairs
        static void BuildStairs(Transform st)
        {
            foreach (var spec in new[] { new { name = "MainStair", x = MainStairX }, new { name = "EndStair", x = EndStairX } })
            {
                var g = G(spec.name, st);
                for (int i = 0; i < Floors - 1; i++)
                {
                    var lv = G($"{spec.name}_Floor{i + 1}to{i + 2}", g);
                    Inst(pfStairLevel, lv, new Vector3(spec.x, FY(i), 1.5f), 0f, "StairLevel");
                    // ไฟชานพัก (กลุ่มไฟของชั้นล่างของช่วงนี้)
                    var lightGroup = st.parent.Find($"Floor_{i + 1}/Lights");
                    AddPointLight(lightGroup, "StairLight_" + spec.name, new Vector3(spec.x, FY(i) + 1.8f + WallH - 0.5f, 1.5f + (FlightLen + SlotDepth) / 2), 7f, 2f);
                }
                // ชั้นบนสุด: ผนังกลางต่อถึงหลังคา + ราวกันตกปิดช่องว่างเหนือช่วงขาขึ้น (ช่อง A)
                var top = G($"{spec.name}_TopFloor", g);
                float yTop = FY(Floors - 1);
                BoxMM("SpineWall", top, new Vector3(spec.x - Spine, yTop, 1.5f), new Vector3(spec.x + Spine, yTop + WallH, 1.5f + FlightLen), mTileWall);
                Railing(top, "GuardRail_VoidA", new Vector3(spec.x - LaneHalf + 0.02f, yTop, 1.55f), new Vector3(spec.x - Spine - 0.02f, yTop, 1.55f), mRailLight);
                // ปิดใต้ช่วงบันไดชั้น 1 (ช่อง B) กันเดินเข้าใต้บันได
                var cl = G($"{spec.name}_UnderStairClosure", g);
                BoxMM("ClosurePanel", cl, new Vector3(spec.x + Spine, FY(0), 1.5f), new Vector3(spec.x + LaneHalf, FY(0) + 2.9f, 1.7f), mTileWall);
                var pnl = G("ClosureSign", cl, new Vector3(spec.x + (Spine + LaneHalf) / 2, FY(0) + 1.6f, 1.485f));
                Box("Plate", pnl, Vector3.zero, new Vector3(0.9f, 0.22f, 0.02f), mSignDark, false);
                Text("Label", pnl, new Vector3(0, 0, -0.015f), new Vector2(0.86f, 0.18f), "ห้องเก็บของ · STORAGE", Color.white, 0.8f);
                // ป้ายชั้นในช่องบันไดทุกชั้น (ผนังข้างช่อง B หันเข้าหาบันได) + ป้ายเหนือทางเข้า
                for (int i = 0; i < Floors; i++)
                {
                    var s = G($"Sign_StairFloor_{i + 1}", g, new Vector3(spec.x + LaneHalf - 0.015f, FY(i) + 2.0f, 2.4f), 90f);
                    Box("Plate", s, Vector3.zero, new Vector3(0.9f, 0.55f, 0.02f), mSign, false);
                    Text("Label", s, new Vector3(0, 0.06f, -0.015f), new Vector2(0.84f, 0.3f), "ชั้น " + (i + 1), Color.white, 3f, FontStyles.Bold);
                    Text("Sub", s, new Vector3(0, -0.17f, -0.015f), new Vector2(0.84f, 0.12f), "FLOOR " + (i + 1), new Color(1f, 0.75f, 0.5f), 1.2f);
                    var e = G($"Sign_StairEntry_{i + 1}", g, new Vector3(spec.x, FY(i) + 2.45f, 1.48f));
                    Box("Plate", e, Vector3.zero, new Vector3(1.6f, 0.28f, 0.02f), mSign, false);
                    Text("Label", e, new Vector3(0, 0, -0.015f), new Vector2(1.55f, 0.22f), (spec.name == "MainStair" ? "บันไดหลัก" : "บันไดหนีไฟ") + "  ชั้น " + (i + 1) + "  ▲▼", Color.white, 1.2f);
                }
            }
        }

        // ------------------------------------------------------------------ exterior: entrance, ramp, signs, landscape
        // ภายนอกตามภาพอ้างอิง: ฐานยกสีเทา + ตัวอักษร "IT · MSU" · บันไดหน้ากว้างแบบอัฒจันทร์ + กระถางดอกไม้/ราว
        // ทางลาดหักกลับ (1:12) · ระเบียงชานหน้า · เสาธง · ทางออกข้าง 8 ขั้น
        static void BuildExterior(Transform ex)
        {
            const float StairHalf = 5.6f, Tr = 0.35f; const int Ns = 8;   // 8 ขั้น (B = 1.2)
            float stairEnd = -12f - (Ns - 1) * Tr;                        // -14.45

            var plaza = G("FrontPlaza", ex);
            BoxMM("Paving", plaza, new Vector3(-13f, 0f, -16.2f), new Vector3(13f, 0.05f, -8.4f), mPlaza);
            BoxMM("Paving_ToPath", plaza, new Vector3(-6.5f, 0f, -19.5f), new Vector3(6.5f, 0.05f, -16.2f), mPlaza);

            // ฐานยก (podium) สีเทารอบอาคาร
            var pod = G("Podium", ex);
            BoxMM("Skirt_Back", pod, new Vector3(-18.25f, 0, 8.15f), new Vector3(18.25f, B, 8.25f), mConcrete, false);
            BoxMM("Skirt_West", pod, new Vector3(-18.25f, 0, -8.25f), new Vector3(-18.15f, B, 8.25f), mConcrete, false);
            BoxMM("Skirt_East", pod, new Vector3(18.15f, 0, -8.25f), new Vector3(18.25f, B, 8.25f), mConcrete, false);
            BoxMM("Skirt_FrontW", pod, new Vector3(-18.25f, 0, -8.25f), new Vector3(-6.15f, B, -8.15f), mConcrete, false);
            BoxMM("Skirt_FrontE", pod, new Vector3(15.6f, 0, -8.25f), new Vector3(18.25f, B, -8.15f), mConcrete, false);

            // ชานหน้าทางเข้า (ระดับชั้น 1)
            var porch = G("EntrancePorch", ex);
            BoxMM("Porch", porch, new Vector3(-6.15f, 0, -12f), new Vector3(6.15f, B, -9.15f), mCorridor);
            BoxMM("PorchSkirt_W", porch, new Vector3(-6.25f, 0, -12f), new Vector3(-6.15f, B - 0.02f, -9.15f), mConcrete, false);

            // ระเบียงยกหน้าปีก +X (ผนังเทาติดตัวอักษร IT · MSU ใหญ่)
            var ter = G("Terrace_ITMSU", ex);
            BoxMM("Terrace", ter, new Vector3(6.15f, 0, -12f), new Vector3(15.5f, B, -8.15f), mCorridor);
            BoxMM("Wall_Front", ter, new Vector3(6.15f, 0, -12.1f), new Vector3(15.6f, B, -12.0f), mConcrete, false);
            BoxMM("Wall_Side", ter, new Vector3(15.5f, 0, -12.1f), new Vector3(15.6f, B, -8.15f), mConcrete, false);
            var big = G("Letters_ITMSU", ter, new Vector3(10.9f, B * 0.5f, -12.12f));
            Text("Text", big, Vector3.zero, new Vector2(8.6f, 1.0f), "IT · MSU", Color.white, 12f, FontStyles.Bold);
            var trails = G("Rails", ter);
            Railing(trails, "Rail_Front", new Vector3(6.25f, B, -11.95f), new Vector3(15.45f, B, -11.95f), mRailLight);
            Railing(trails, "Rail_Side", new Vector3(15.45f, B, -11.95f), new Vector3(15.45f, B, -8.3f), mRailLight);
            for (float x = 7f; x < 15.2f; x += 1.6f) Inst(pfFlowerPot, ter, new Vector3(x, B, -11.6f), 0f, "FlowerPot");

            // บันไดหน้ากว้าง (อัฒจันทร์) x ±5.6 + ผนังข้างแบบขั้น + ราว + กระถางดอกไม้
            var steps = G("FrontSteps", porch);
            for (int k = 1; k < Ns; k++)
            {
                float top = B - k * 0.15f, z1 = -12f - (k - 1) * Tr, z0 = z1 - Tr;
                BoxMM("Step_" + k, steps, new Vector3(-StairHalf, 0, z0), new Vector3(StairHalf, top, z1), mStair);
                foreach (var sx in new[] { -1f, 1f })
                {
                    float a = sx < 0 ? -6.15f : StairHalf, b = sx < 0 ? -StairHalf : 6.15f;
                    BoxMM("Cheek_" + k, steps, new Vector3(a, 0, z0), new Vector3(b, top + 0.45f, z1), mConcrete);
                    if (k % 2 == 1) Inst(pfFlowerPot, steps, new Vector3((a + b) / 2, top + 0.45f, (z0 + z1) / 2), 0f, "FlowerPot");
                }
            }
            RampPlate(steps, "StepRampCollider", new Vector3(0, 0.05f, stairEnd - 0.23f), new Vector3(0, B, -12.0f), StairHalf * 2);
            foreach (var sx in new[] { -1f, 1f })
                SlopedRail(steps, sx < 0 ? "Rail_StairW" : "Rail_StairE", new Vector3(sx * (StairHalf + 0.1f), 0.05f + 0.45f, stairEnd), new Vector3(sx * (StairHalf + 0.1f), B + 0.45f, -12.0f));

            var rails = G("PorchRailings", porch);
            Railing(rails, "Rail_WestFront", new Vector3(-6.05f, B, -11.95f), new Vector3(-6.05f, B, -11.85f), mRailLight);
            Railing(rails, "Rail_WestBack", new Vector3(-6.05f, B, -10.1f), new Vector3(-6.05f, B, -9.25f), mRailLight);

            // ทางลาด 1:12 หักกลับ: ช่วง 1 ไป -X ถึงชานพัก · ช่วง 2 ลงไป -Z สู่ลาน
            var ramp = G("AccessRamp", ex);
            float mid = (B + 0.05f) * 0.5f, run1 = (B - mid) * 12f, run2 = (mid - 0.05f) * 12f;
            float lx = -6.15f - run1;                                   // ปลายช่วง 1
            RampPlate(ramp, "Run1", new Vector3(lx, mid, -11.0f), new Vector3(-6.15f, B, -11.0f), 1.5f, mStair, 0.25f);
            BoxMM("Landing", ramp, new Vector3(lx - 1.6f, 0, -11.75f), new Vector3(lx, mid, -10.25f), mStair);
            float l2x = lx - 0.8f;
            RampPlate(ramp, "Run2", new Vector3(l2x, 0.05f, -11.75f - run2), new Vector3(l2x, mid, -11.75f), 1.5f, mStair, 0.25f);
            SlopedRail(ramp, "Rail_Run1_Out", new Vector3(lx, mid, -11.82f), new Vector3(-6.15f, B, -11.82f));
            SlopedRail(ramp, "Rail_Run1_In", new Vector3(lx, mid, -10.18f), new Vector3(-6.15f, B, -10.18f));
            SlopedRail(ramp, "Rail_Run2_W", new Vector3(lx - 1.62f, 0.05f, -11.75f - run2), new Vector3(lx - 1.62f, mid, -11.75f));
            SlopedRail(ramp, "Rail_Run2_E", new Vector3(lx + 0.02f, 0.05f, -11.75f - run2), new Vector3(lx + 0.02f, mid, -11.85f));
            Railing(ramp, "Rail_Landing_Back", new Vector3(lx - 1.6f, mid, -10.2f), new Vector3(lx, mid, -10.2f), mRailLight);
            Railing(ramp, "Rail_Landing_End", new Vector3(lx - 1.65f, mid, -10.25f), new Vector3(lx - 1.65f, mid, -11.7f), mRailLight);

            // ทางออกข้าง (ชั้น 1 ปลายทางเดินด้าน +X): ประตู + ชาน + บันได 8 ขั้นลงทางเดินเดิม
            var side = G("SideExit", ex);
            Inst(pfDoor, side, new Vector3(18f, B, -0.65f), -90f, "Door_SideExit");
            BoxMM("Landing", side, new Vector3(18.15f, 0, -1.1f), new Vector3(19.0f, B, 1.1f), mStair);
            for (int k = 1; k < Ns; k++)
                BoxMM("Step_" + k, side, new Vector3(19.0f + (k - 1) * 0.28f, 0, -1.1f), new Vector3(19.0f + k * 0.28f, B - k * 0.15f, 1.1f), mStair);
            float sEnd = 19.0f + (Ns - 1) * 0.28f;
            RampPlate(side, "StepRampCollider", new Vector3(sEnd + 0.19f, 0.02f, 0f), new Vector3(19.0f, B, 0f), 2.2f);
            SlopedRail(side, "Rail_N", new Vector3(sEnd, 0.02f, 1.15f), new Vector3(19.0f, B, 1.15f));
            SlopedRail(side, "Rail_S", new Vector3(sEnd, 0.02f, -1.15f), new Vector3(19.0f, B, -1.15f));
            BoxMM("Canopy", side, new Vector3(18.15f, B + 2.6f, -1.3f), new Vector3(19.4f, B + 2.75f, 1.3f), mWhite, false);

            // กันสาดทางเข้า + ป้ายคณะ + เสากลมสูงรับหลังคายื่น (ภาพที่ 1)
            var canopy = G("EntranceCanopy", ex);
            float cy = B + 3.0f;
            BoxMM("CanopySlab", canopy, new Vector3(-5.2f, cy, -12.2f), new Vector3(5.2f, cy + 0.25f, -9.15f), mWhite, false);
            BoxMM("Fascia", canopy, new Vector3(-5.2f, cy - 0.35f, -12.3f), new Vector3(5.2f, cy + 0.35f, -12.15f), mSign, false);
            BoxMM("FasciaAccent", canopy, new Vector3(-5.2f, cy - 0.4f, -12.32f), new Vector3(5.2f, cy - 0.33f, -12.15f), mSignOrange, false);
            foreach (var x in new[] { -5.0f, 5.0f })
            {
                var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                col.name = "RoundColumn"; col.transform.SetParent(canopy, false);
                float h0 = B, h1 = FY(Floors) + 1.0f;
                col.transform.localPosition = new Vector3(x, (h0 + h1) / 2, -11.6f);
                col.transform.localScale = new Vector3(0.5f, (h1 - h0) / 2, 0.5f);
                col.GetComponent<Renderer>().sharedMaterial = mWhite;
            }
            foreach (var x in new[] { -3f, 0f, 3f })
                NoShadow(Box("Downlight", canopy, new Vector3(x, cy - 0.01f, -10.6f), new Vector3(0.4f, 0.02f, 0.4f), mLight, false));
            var cs = G("Sign_Faculty", canopy, new Vector3(0, cy, -12.34f));
            Text("Thai", cs, new Vector3(0, 0.12f, 0), new Vector2(9.8f, 0.42f), "คณะวิทยาการสารสนเทศ", Color.white, 6f, FontStyles.Bold);
            Text("Eng", cs, new Vector3(0, -0.2f, 0), new Vector2(9.8f, 0.2f), "FACULTY OF INFORMATICS  ·  MAHASARAKHAM UNIVERSITY", new Color(1f, 0.82f, 0.6f), 3f);
            var cl = G("Lights", canopy);
            var cl1 = G("NightLight_ITCanopy", cl, new Vector3(0, cy - 0.3f, -10.6f));
            var cll = cl1.gameObject.AddComponent<Light>(); cll.type = LightType.Point; cll.range = 8f; cll.intensity = 2.5f; cll.color = new Color(1f, 0.92f, 0.8f); cll.shadows = LightShadows.None;

            // ภูมิทัศน์
            var land = G("Landscaping", ex);
            var tree = Load("Assets/Synty/PolygonCity/Prefabs/Environments/SM_Env_Tree_02.prefab");
            var tree3 = Load("Assets/Synty/PolygonCity/Prefabs/Environments/SM_Env_Tree_03.prefab");
            var bush = Load("Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Bush_01.prefab");
            var bush2 = Load("Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Bush_02.prefab");
            var parkBench = Load("Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_ParkBench_01.prefab");
            var bin = Load("Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Trashbin_01.prefab");
            // แนวพุ่มไม้หน้าปีก -X (หลังทางลาด) และรอบลาน
            var pl = G("Hedge_FrontWest", land);
            BoxMM("Curb", pl, new Vector3(-17.6f, 0, -9.4f), new Vector3(-6.6f, 0.35f, -8.4f), mPlanter);
            BoxMM("Soil", pl, new Vector3(-17.52f, 0.35f, -9.32f), new Vector3(-6.68f, 0.37f, -8.48f), mGrass, false);
            if (bush != null)
                for (float x = -16.8f; x < -7f; x += 1.3f)
                    Inst(((int)(x * 10) % 2 == 0 || bush2 == null) ? bush : bush2, pl, new Vector3(x, 0.35f, -8.9f), x * 37f % 360f, "Bush").transform.localScale = Vector3.one * 0.75f;
            if (tree != null)
                foreach (var p in new[] { new Vector3(-9.5f, 0.05f, -17.2f), new Vector3(16.8f, 0.05f, -10.0f) })
                {
                    var tb = G("TreeBed", land, p);
                    BoxMM("Bed", tb, new Vector3(-1.1f, 0, -1.1f), new Vector3(1.1f, 0.4f, 1.1f), mPlanter);
                    BoxMM("Soil", tb, new Vector3(-1f, 0.4f, -1f), new Vector3(1f, 0.42f, 1f), mGrass, false);
                    Inst(p.x > 0 && tree3 != null ? tree3 : tree, tb, new Vector3(0, 0.4f, 0), p.x * 13f, "Tree").transform.localScale = Vector3.one * 0.9f;
                }
            if (parkBench != null)
            {
                Inst(parkBench, land, new Vector3(-3.2f, 0.05f, -16.0f), 0f, "ParkBench_1");
                Inst(parkBench, land, new Vector3(3.2f, 0.05f, -16.0f), 0f, "ParkBench_2");
                Inst(parkBench, land, new Vector3(9.0f, 0.05f, -15.4f), 0f, "ParkBench_3");
            }
            if (bin != null) Inst(bin, land, new Vector3(-6.9f, 0.05f, -15.2f), 0f, "TrashBin");
            // เสาธง (ภาพที่ 2)
            var flags = G("Flagpoles", land);
            Color[] fc = { new Color(0.12f, 0.20f, 0.38f), new Color(0.90f, 0.47f, 0.18f), Color.white, new Color(0.2f, 0.5f, 0.75f), new Color(0.85f, 0.75f, 0.2f) };
            for (int k = 0; k < 5; k++)
            {
                var f = Inst(pfFlagpole, flags, new Vector3(8.0f + k * 1.6f, 0.05f, -13.0f), 0f, "Flagpole_" + k);
                if (k > 0) f.transform.Find("Flag").GetComponent<Renderer>().sharedMaterial = Mat("Flag_" + k, fc[k], 0.2f);
            }
            // เสาไฟลาน (ชื่อ NightLight → DayNightCycle เดิมเปิด/ปิดตามเวลา)
            foreach (var lpos in new[] { new Vector2(-7.5f, -16.4f), new Vector2(-11.9f, -15.6f) })
            {
                var lp = G("LampPost", land, new Vector3(lpos.x, 0.05f, lpos.y));
                Box("Pole", lp, new Vector3(0, 1.6f, 0), new Vector3(0.1f, 3.2f, 0.1f), mDarkGray, true);
                NoShadow(Box("Head", lp, new Vector3(0, 3.25f, 0), new Vector3(0.45f, 0.12f, 0.45f), mLight, false));
                var nl = G("NightLight_ITPlaza", lp, new Vector3(0, 3.0f, 0));
                var l = nl.gameObject.AddComponent<Light>(); l.type = LightType.Point; l.range = 9f; l.intensity = 2.2f; l.color = new Color(1f, 0.9f, 0.75f); l.shadows = LightShadows.None;
            }
        }

        static void BuildExteriorOld(Transform ex)
        {
            // ลานหน้าอาคาร (แบบเดิมก่อนได้ภาพอ้างอิง — ไม่ได้ใช้แล้ว)
            var plaza = G("FrontPlaza", ex);
            BoxMM("Paving", plaza, new Vector3(-13f, 0f, -15.5f), new Vector3(13f, 0.05f, -9.15f), mPlaza);
            BoxMM("Paving_ToPath", plaza, new Vector3(-6.5f, 0f, -19.5f), new Vector3(6.5f, 0.05f, -15.5f), mPlaza);

            // ชานหน้าทางเข้า + บันไดกว้าง + ทางลาด
            var porch = G("EntrancePorch", ex);
            BoxMM("Porch", porch, new Vector3(-6.15f, 0, -12f), new Vector3(6.15f, B, -9.15f), mCorridor);
            var steps = G("FrontSteps", porch);
            BoxMM("Step_2", steps, new Vector3(-5f, 0, -12.3f), new Vector3(5f, 0.30f, -12f), mStair);
            BoxMM("Step_1", steps, new Vector3(-5f, 0, -12.6f), new Vector3(5f, 0.15f, -12.3f), mStair);
            RampPlate(steps, "StepRampCollider", new Vector3(0, 0.05f, -12.9f), new Vector3(0, B, -12.0f), 10f);
            var rails = G("PorchRailings", porch);
            Railing(rails, "Rail_East", new Vector3(6.0f, B, -11.9f), new Vector3(6.0f, B, -9.25f), mRailLight);
            Railing(rails, "Rail_FrontW", new Vector3(-6.0f, B, -11.95f), new Vector3(-5.05f, B, -11.95f), mRailLight);
            Railing(rails, "Rail_FrontE", new Vector3(5.05f, B, -11.95f), new Vector3(6.0f, B, -11.95f), mRailLight);
            Railing(rails, "Rail_WestBack", new Vector3(-6.0f, B, -10.0f), new Vector3(-6.0f, B, -9.25f), mRailLight);
            // ทางลาดคนพิการ (1:12) ลงไปทาง -X
            var ramp = G("AccessRamp", ex);
            float rampEnd = -6.15f - (B - 0.05f) * 12f;
            RampPlate(ramp, "RampSurface", new Vector3(rampEnd, 0.05f, -11.0f), new Vector3(-6.15f, B, -11.0f), 1.6f, mStair, 0.25f);
            SlopedRail(ramp, "Rail_Outer", new Vector3(rampEnd, 0.05f, -11.85f), new Vector3(-6.15f, B, -11.85f));
            SlopedRail(ramp, "Rail_Inner", new Vector3(rampEnd, 0.05f, -10.15f), new Vector3(-6.15f, B, -10.15f));

            // ทางออกข้าง (ชั้น 1 ปลายทางเดินด้าน +X): ประตู + บันได 3 ขั้น
            var side = G("SideExit", ex);
            Inst(pfDoor, side, new Vector3(18f, B, -0.65f), -90f, "Door_SideExit");
            BoxMM("Landing", side, new Vector3(18.15f, 0, -1.1f), new Vector3(18.75f, B, 1.1f), mStair);
            BoxMM("Step_2", side, new Vector3(18.75f, 0, -1.1f), new Vector3(19.05f, 0.30f, 1.1f), mStair);
            BoxMM("Step_1", side, new Vector3(19.05f, 0, -1.1f), new Vector3(19.35f, 0.15f, 1.1f), mStair);
            RampPlate(side, "StepRampCollider", new Vector3(19.65f, 0.02f, 0f), new Vector3(18.75f, B, 0f), 2.2f);
            BoxMM("Canopy", side, new Vector3(18.15f, B + 2.6f, -1.2f), new Vector3(19.1f, B + 2.75f, 1.2f), mWhite, false);

            // กันสาดทางเข้า + ป้ายคณะ
            var canopy = G("EntranceCanopy", ex);
            float cy = B + 3.0f;
            BoxMM("CanopySlab", canopy, new Vector3(-5.2f, cy, -12.2f), new Vector3(5.2f, cy + 0.25f, -9.15f), mWhite, false);
            BoxMM("Fascia", canopy, new Vector3(-5.2f, cy - 0.35f, -12.3f), new Vector3(5.2f, cy + 0.35f, -12.15f), mSign, false);
            BoxMM("FasciaAccent", canopy, new Vector3(-5.2f, cy - 0.4f, -12.32f), new Vector3(5.2f, cy - 0.33f, -12.15f), mSignOrange, false);
            foreach (var x in new[] { -4.9f, 4.9f })
                BoxMM("Column", canopy, new Vector3(x - 0.14f, B, -12.0f), new Vector3(x + 0.14f, cy, -11.72f), mWhite);
            foreach (var x in new[] { -3f, 0f, 3f })
                NoShadow(Box("Downlight", canopy, new Vector3(x, cy - 0.01f, -10.6f), new Vector3(0.4f, 0.02f, 0.4f), mLight, false));
            var cs = G("Sign_Faculty", canopy, new Vector3(0, cy, -12.34f));
            Text("Thai", cs, new Vector3(0, 0.12f, 0), new Vector2(9.8f, 0.42f), "คณะวิทยาการสารสนเทศ", Color.white, 6f, FontStyles.Bold);
            Text("Eng", cs, new Vector3(0, -0.2f, 0), new Vector2(9.8f, 0.2f), "FACULTY OF INFORMATICS  ·  MAHASARAKHAM UNIVERSITY", new Color(1f, 0.82f, 0.6f), 3f);
            var cl = G("Lights", canopy);
            var cl1 = G("NightLight_ITCanopy", cl, new Vector3(0, cy - 0.3f, -10.6f));
            var cll = cl1.gameObject.AddComponent<Light>(); cll.type = LightType.Point; cll.range = 8f; cll.intensity = 2.5f; cll.color = new Color(1f, 0.92f, 0.8f); cll.shadows = LightShadows.None;

            // ป้ายตั้งบนลาน
            var mono = G("Sign_Plaza", ex, new Vector3(9.5f, 0.05f, -13.6f), -12f);
            BoxMM("Base", mono, new Vector3(-2.1f, 0, -0.3f), new Vector3(2.1f, 1.25f, 0.3f), mSign);
            Box("Accent", mono, new Vector3(0, 1.28f, 0), new Vector3(4.3f, 0.06f, 0.62f), mSignOrange, false);
            Text("Logo", mono, new Vector3(0, 0.93f, -0.32f), new Vector2(3.9f, 0.34f), "IT · MSU", new Color(1f, 0.78f, 0.5f), 4f, FontStyles.Bold);
            Text("Thai", mono, new Vector3(0, 0.58f, -0.32f), new Vector2(3.9f, 0.3f), "คณะวิทยาการสารสนเทศ", Color.white, 3f);
            Text("Eng", mono, new Vector3(0, 0.3f, -0.32f), new Vector2(3.9f, 0.16f), "FACULTY OF INFORMATICS", new Color(0.85f, 0.9f, 1f), 2f);

            // ภูมิทัศน์ (Asset Synty เดิม)
            var land = G("Landscaping", ex);
            var tree = Load("Assets/Synty/PolygonCity/Prefabs/Environments/SM_Env_Tree_02.prefab");
            var tree3 = Load("Assets/Synty/PolygonCity/Prefabs/Environments/SM_Env_Tree_03.prefab");
            var bush = Load("Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Bush_01.prefab");
            var bush2 = Load("Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Bush_02.prefab");
            var flowers = Load("Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Flowers_02.prefab");
            var parkBench = Load("Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_ParkBench_01.prefab");
            var bin = Load("Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Trashbin_01.prefab");
            // กระบะต้นไม้หน้าปีกอาคาร
            foreach (var sgn in new[] { -1f, 1f })
            {
                float a = sgn < 0 ? -17.6f : 6.6f, b = sgn < 0 ? -6.6f : 17.6f;
                var pl = G(sgn < 0 ? "Planter_FrontWest" : "Planter_FrontEast", land);
                BoxMM("Curb", pl, new Vector3(a, 0, -9.3f), new Vector3(b, 0.35f, -8.5f), mPlanter);
                BoxMM("Soil", pl, new Vector3(a + 0.08f, 0.35f, -9.22f), new Vector3(b - 0.08f, 0.37f, -8.58f), mGrass, false);
                for (float x = a + 0.8f; x < b - 0.4f; x += 1.6f)
                {
                    var bp = bush != null ? Inst(((int)(x * 10) % 2 == 0 || bush2 == null) ? bush : bush2, pl, new Vector3(x, 0.35f, -8.9f), x * 37f % 360f, "Bush") : null;
                    if (bp != null) bp.transform.localScale = Vector3.one * 0.8f;
                }
            }
            // ต้นไม้มุมลาน + ที่นั่ง
            if (tree != null)
            {
                foreach (var p in new[] { new Vector3(-12f, 0.05f, -14f), new Vector3(11.8f, 0.05f, -12.4f) })
                {
                    var tb = G("TreeBed", land, p);
                    BoxMM("Bed", tb, new Vector3(-1.1f, 0, -1.1f), new Vector3(1.1f, 0.4f, 1.1f), mPlanter);
                    BoxMM("Soil", tb, new Vector3(-1f, 0.4f, -1f), new Vector3(1f, 0.42f, 1f), mGrass, false);
                    var tt = Inst(p.x > 0 && tree3 != null ? tree3 : tree, tb, new Vector3(0, 0.4f, 0), p.x * 13f, "Tree");
                    tt.transform.localScale = Vector3.one * 0.9f;
                }
            }
            if (flowers != null)
                foreach (var x in new[] { -10.4f, -13.6f, 9.9f, 13.7f })
                    Inst(flowers, land, new Vector3(x, 0.05f, -12.6f), x * 21f, "Flowers");
            if (parkBench != null)
            {
                Inst(parkBench, land, new Vector3(-9.0f, 0.05f, -14.8f), 0f, "ParkBench_1");
                Inst(parkBench, land, new Vector3(-3.2f, 0.05f, -15.0f), 0f, "ParkBench_2");
                Inst(parkBench, land, new Vector3(3.2f, 0.05f, -15.0f), 0f, "ParkBench_3");
            }
            else
            {
                Inst(pfBench, land, new Vector3(-9.0f, 0.05f, -14.8f), 180f, "Bench_1");
                Inst(pfBench, land, new Vector3(3.2f, 0.05f, -15.0f), 180f, "Bench_2");
            }
            Inst(pfBench, land, new Vector3(15.6f, 0.05f, -11.6f), 90f, "Bench_East");
            if (bin != null) Inst(bin, land, new Vector3(-6.8f, 0.05f, -13.2f), 0f, "TrashBin");
            // เสาไฟลาน (ชื่อ NightLight → DayNightCycle เดิมเปิด/ปิดตามเวลา)
            foreach (var x in new[] { -7.5f, 7.5f })
            {
                var lp = G("LampPost", land, new Vector3(x, 0.05f, -15.2f));
                Box("Pole", lp, new Vector3(0, 1.6f, 0), new Vector3(0.1f, 3.2f, 0.1f), mDarkGray, true);
                NoShadow(Box("Head", lp, new Vector3(0, 3.25f, 0), new Vector3(0.45f, 0.12f, 0.45f), mLight, false));
                var nl = G("NightLight_ITPlaza", lp, new Vector3(0, 3.0f, 0));
                var l = nl.gameObject.AddComponent<Light>(); l.type = LightType.Point; l.range = 9f; l.intensity = 2.2f; l.color = new Color(1f, 0.9f, 0.75f); l.shadows = LightShadows.None;
            }
        }

        static void SlopedRail(Transform p, string name, Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a; float len = dir.magnitude;
            var r = G(name, p);
            r.localPosition = (a + b) * 0.5f;
            r.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Box("HandRail", r, new Vector3(0, 0.95f, 0), new Vector3(0.06f, 0.06f, len), mRailLight, false);
            Box("MidRail", r, new Vector3(0, 0.5f, 0), new Vector3(0.04f, 0.04f, len), mRailLight, false);
            int posts = Mathf.Max(1, Mathf.CeilToInt(len / 1.5f));
            for (int i = 0; i <= posts; i++)
                Box("Post_" + i, r, new Vector3(0, 0.48f, -len / 2 + len * i / posts), new Vector3(0.05f, 0.96f, 0.05f), mRailLight, false);
            var col = G(name + "_Collider", p);
            col.localPosition = (a + b) * 0.5f + Vector3.up * 0.55f;
            col.localRotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z).normalized, Vector3.up);
            var bc = col.gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.1f, 1.1f + Mathf.Abs(dir.y), new Vector3(dir.x, 0, dir.z).magnitude);
        }
    }
}
