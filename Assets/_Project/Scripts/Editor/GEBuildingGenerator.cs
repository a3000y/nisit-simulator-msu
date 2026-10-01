using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using NisitSimulator.GEBuilding;

namespace NisitSimulator.EditorTools
{
    // สร้างอาคารเรียน GE (3 ชั้น) ลงใน Scene ที่เปิดอยู่ — เมนู Nisit > GE Building > Rebuild In Active Scene
    // ปรับขนาด/ผังได้จากค่าคงที่ด้านล่าง แล้วกด Rebuild ใหม่ (ลบ root "GE_Building" เดิมก่อนสร้าง)
    //
    // ผัง (เมตร): ตัวอาคาร x -20..20, z 0..12 · แกนกลาง x -6..6 ยื่นถึง z 14
    //   ทางเดินหน้า z 0..3 (เปิดโล่ง มีราวกันตก) · ห้องเรียน z 3..12 (ปีกละ 2 ห้อง/ชั้น)
    //   แกนกลาง: โถง/พื้นที่นั่งพัก z 0..7 · บันได x -6..0, z 7..14 · ห้องน้ำ x 0..6, z 7..14
    public static class GEBuildingGenerator
    {
        public const float B = 0.45f;   // ความสูงพื้นชั้น 1 จากลาน
        public const float H = 3.6f;    // ความสูงต่อชั้น
        public const float T = 0.25f;   // ความหนาพื้น
        public const float W = 0.2f;    // ความหนาผนัง
        public const int Floors = 3;
        public static float FloorY(int i) => B + i * H;
        public static float WallH => H - T;
        const float Rise = 0.15f, Tread = 0.28f; const int Steps = 12;

        const string MatDir = "Assets/_Project/Art/Materials/GEBuilding";
        const string PfDir = "Assets/_Project/Prefabs/GEBuilding";
        const string ClassPf = "Assets/_Project/Prefabs/Classroom/";

        static Material mWall, mAccent, mSlab, mRoomFloor, mRestFloor, mStair, mRail, mGlass, mGrass, mPlaza,
            mDoor, mFrame, mBoard, mLight, mSign, mSignDark, mCeramic, mWood, mMetal, mTileWall, mMirror;
        static TMP_FontAsset thaiFont;
        static Font guiFont;
        static GameObject pfDoor, pfClassroom, pfRoomSign, pfBench, pfRestroom, pfLight, pfPlayer;

        struct Op
        {
            public float a, b, bottom, top; public bool glass;
            public Op(float a, float b, float bottom, float top, bool glass) { this.a = a; this.b = b; this.bottom = bottom; this.top = top; this.glass = glass; }
        }

        [MenuItem("Nisit/GE Building/Rebuild In Active Scene")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            EnsureFolder("Assets/_Project/Art/Materials", "GEBuilding");
            EnsureFolder("Assets/_Project/Prefabs", "GEBuilding");
            thaiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/Mitr SDF.asset");
            guiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Art/Fonts/Mitr/Mitr-Regular.ttf");
            MakeMaterials();
            MakePrefabs();

            var old = GameObject.Find("GE_Building");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("GE_Building").transform;

            BuildExterior(G("Exterior", root));
            var structure = G("Structure", root);
            BuildSlabsAndRoof(structure);
            for (int i = 0; i < Floors; i++) BuildFloor(G("Floor_" + (i + 1), root), i);
            BuildStairwell(G("Stairwell", root));
            BuildLightingAndPlayer(root);
            root.gameObject.AddComponent<GEBuildingCutaway>();

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.GetComponentInParent<GEDoor>() == null && t.GetComponentInParent<GEFirstPersonController>() == null)
                    t.gameObject.isStatic = true;

            int colliders = root.GetComponentsInChildren<Collider>().Length;
            int lights = root.GetComponentsInChildren<Light>().Length;
            int doors = root.GetComponentsInChildren<GEDoor>().Length;
            return $"GE_Building built: colliders={colliders} lights={lights} doors={doors}";
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

        static Material Mat(string name, Color c, float smooth, float metal = 0f)
        {
            string p = MatDir + "/M_GE_" + name + ".mat";
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, p); }
            m.shader = lit;
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void MakeMaterials()
        {
            mWall = Mat("Wall", new Color(0.94f, 0.93f, 0.90f), 0.1f);
            mAccent = Mat("Accent", new Color(0.30f, 0.52f, 0.56f), 0.25f);
            mSlab = Mat("Slab", new Color(0.80f, 0.80f, 0.78f), 0.3f);
            mRoomFloor = Mat("RoomFloor", new Color(0.82f, 0.75f, 0.64f), 0.35f);
            mRestFloor = Mat("RestroomFloor", new Color(0.74f, 0.80f, 0.84f), 0.45f);
            mTileWall = Mat("RestroomWall", new Color(0.86f, 0.91f, 0.93f), 0.4f);
            mStair = Mat("Stair", new Color(0.72f, 0.70f, 0.66f), 0.25f);
            mRail = Mat("Rail", new Color(0.28f, 0.30f, 0.33f), 0.5f, 0.6f);
            mGrass = Mat("Grass", new Color(0.50f, 0.66f, 0.40f), 0.05f);
            mPlaza = Mat("Plaza", new Color(0.78f, 0.74f, 0.68f), 0.15f);
            mDoor = Mat("Door", new Color(0.60f, 0.45f, 0.31f), 0.3f);
            mFrame = Mat("Frame", new Color(0.86f, 0.87f, 0.88f), 0.5f, 0.5f);
            mBoard = Mat("Whiteboard", new Color(0.98f, 0.98f, 0.98f), 0.75f);
            mSign = Mat("SignPlate", new Color(0.24f, 0.45f, 0.49f), 0.3f);
            mSignDark = Mat("SignDark", new Color(0.20f, 0.22f, 0.25f), 0.3f);
            mCeramic = Mat("Ceramic", new Color(0.97f, 0.97f, 0.97f), 0.8f);
            mWood = Mat("Wood", new Color(0.78f, 0.62f, 0.44f), 0.3f);
            mMetal = Mat("Metal", new Color(0.62f, 0.64f, 0.67f), 0.6f, 0.7f);
            mMirror = Mat("Mirror", new Color(0.80f, 0.86f, 0.90f), 0.95f, 0.9f);
            mGlass = Mat("Glass", new Color(0.72f, 0.86f, 0.95f, 0.25f), 0.95f);
            mGlass.SetFloat("_Surface", 1); mGlass.SetFloat("_Blend", 0);
            mGlass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mGlass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mGlass.SetFloat("_SrcBlendAlpha", 1); mGlass.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mGlass.SetFloat("_ZWrite", 0); mGlass.SetOverrideTag("RenderType", "Transparent");
            mGlass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mGlass.renderQueue = 3000;
            mLight = Mat("LightPanel", Color.white, 0.2f);
            mLight.EnableKeyword("_EMISSION"); mLight.SetColor("_EmissionColor", new Color(1f, 0.98f, 0.94f) * 1.5f);
            mLight.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.SaveAssets();
        }

        static TextMeshPro Text(string name, Transform parent, Vector3 pos, Vector2 size, string text, Color color, float maxSize = 3f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            if (thaiFont != null) tmp.font = thaiFont;
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.05f; tmp.fontSizeMax = maxSize;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = size;
            go.transform.localPosition = pos;
            return tmp;
        }

        // ผนังแนวแกน X (ระนาบ z = zc) พร้อมช่องประตู/หน้าต่าง (a,b = ช่วง x, bottom/top = ความสูงจากฐานผนัง)
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
                if (o.glass) Window(g, "Window_" + n, new Vector3((o.a + o.b) / 2, y0 + (o.bottom + o.top) / 2, zc), o.b - o.a, o.top - o.bottom, th, 0f);
                cur = o.b;
            }
            if (x1 > cur + 0.001f) BoxMM("Seg_" + n, g, new Vector3(cur, y0, zc - th / 2), new Vector3(x1, y0 + h, zc + th / 2), m);
            return g;
        }

        // ผนังแนวแกน Z (ระนาบ x = xc)
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
                if (o.glass) Window(g, "Window_" + n, new Vector3(xc, y0 + (o.bottom + o.top) / 2, (o.a + o.b) / 2), o.b - o.a, o.top - o.bottom, th, 90f);
                cur = o.b;
            }
            if (z1 > cur + 0.001f) BoxMM("Seg_" + n, g, new Vector3(xc - th / 2, y0, cur), new Vector3(xc + th / 2, y0 + h, z1), m);
            return g;
        }

        static void Window(Transform p, string name, Vector3 center, float width, float height, float th, float rotY)
        {
            var w = G(name, p, center, rotY);
            var glass = Box("Glass", w, Vector3.zero, new Vector3(width, height, 0.03f), mGlass, true);
            glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            float f = 0.06f, d = th + 0.02f;
            Box("Frame_Top", w, new Vector3(0, height / 2 - f / 2, 0), new Vector3(width, f, d), mFrame, false);
            Box("Frame_Bottom", w, new Vector3(0, -height / 2 + f / 2, 0), new Vector3(width, f, d), mFrame, false);
            Box("Frame_L", w, new Vector3(-width / 2 + f / 2, 0, 0), new Vector3(f, height, d), mFrame, false);
            Box("Frame_R", w, new Vector3(width / 2 - f / 2, 0, 0), new Vector3(f, height, d), mFrame, false);
            int panes = Mathf.Max(1, Mathf.RoundToInt(width / 1.2f));
            for (int i = 1; i < panes; i++)
                Box("Mullion_" + i, w, new Vector3(-width / 2 + width * i / panes, 0, 0), new Vector3(0.05f, height, 0.08f), mFrame, false);
        }

        // ราวกันตกแนวนอน (a -> b ที่ระดับพื้นเดียวกัน): กระจก + ราวจับ, Collider สูง 1.1 ม.
        static Transform Railing(Transform p, string name, Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a; float len = dir.magnitude;
            var r = G(name, p);
            r.position = (a + b) * 0.5f;
            r.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0, 90, 0);   // local X = ทิศราว
            var bc = r.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.6f, 0); bc.size = new Vector3(len, 1.2f, 0.12f);
            Box("Glass", r, new Vector3(0, 0.5f, 0), new Vector3(len, 0.85f, 0.03f), mGlass, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box("HandRail", r, new Vector3(0, 1.05f, 0), new Vector3(len, 0.06f, 0.08f), mRail, false);
            Box("BaseRail", r, new Vector3(0, 0.04f, 0), new Vector3(len, 0.08f, 0.08f), mRail, false);
            int posts = Mathf.Max(1, Mathf.CeilToInt(len / 1.5f));
            for (int i = 0; i <= posts; i++)
                Box("Post_" + i, r, new Vector3(-len / 2 + len * i / posts, 0.53f, 0), new Vector3(0.05f, 1.06f, 0.05f), mRail, false);
            return r;
        }

        // ราวกันตกตามแนวลาด (บันได/ทางลาด): a,b อยู่บนเส้นผิวที่เดิน
        static Transform SlopedRailing(Transform p, string name, Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a; float len = dir.magnitude;
            var r = G(name, p);
            r.position = (a + b) * 0.5f;
            r.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);   // local Z = ทิศราว
            // Collider: แบ่งเป็นท่อนแนวตั้งสั้น ๆ ไล่ตามความชัน (ปลายราวไม่ยื่นเข้าทางเดิน)
            var cols = G("Colliders", p);
            Vector3 flat = new Vector3(dir.x, 0, dir.z); float hl = flat.magnitude;
            int segs = Mathf.Max(1, Mathf.CeilToInt(hl / 0.4f));
            Quaternion yawRot = Quaternion.LookRotation(flat.normalized, Vector3.up);
            for (int s = 0; s < segs; s++)
            {
                float t0 = (float)s / segs, t1 = (float)(s + 1) / segs;
                Vector3 pa = Vector3.Lerp(a, b, t0), pb = Vector3.Lerp(a, b, t1);
                float yLo = Mathf.Min(pa.y, pb.y) - 0.3f, yHi = Mathf.Max(pa.y, pb.y) + 1.1f;
                var seg = new GameObject(name + "_Col_" + s).transform;
                seg.SetParent(cols, false);
                Vector3 mid = (pa + pb) * 0.5f; seg.position = new Vector3(mid.x, (yLo + yHi) * 0.5f, mid.z);
                seg.rotation = yawRot;
                var sc = seg.gameObject.AddComponent<BoxCollider>();
                sc.size = new Vector3(0.12f, yHi - yLo, hl / segs);
            }
            Box("Glass", r, new Vector3(0, 0.5f, 0), new Vector3(0.03f, 0.85f, len), mGlass, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box("HandRail", r, new Vector3(0, 0.98f, 0), new Vector3(0.08f, 0.06f, len + 0.1f), mRail, false);
            return r;
        }

        // แผ่น Collider ลาด (ผิวบนตรงกับเส้น a -> b) — ทำให้เดินขึ้นลงบันไดลื่น
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

        // ------------------------------------------------------------------ prefabs
        static GameObject SavePrefab(GameObject temp, string name)
        {
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

        static void MakePrefabs()
        {
            // ไฟเพดาน
            var l = new GameObject("GE_CeilingLight");
            Box("Panel", l.transform, Vector3.zero, new Vector3(1.2f, 0.04f, 0.3f), mLight, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var spot = G("SpotLight", l.transform, new Vector3(0, -0.05f, 0));
            spot.localRotation = Quaternion.Euler(90, 0, 0);
            var lt = spot.gameObject.AddComponent<Light>();
            lt.type = LightType.Spot; lt.spotAngle = 130f; lt.innerSpotAngle = 70f; lt.range = 7f; lt.intensity = 3.2f;
            lt.color = new Color(1f, 0.97f, 0.92f); lt.shadows = LightShadows.None;
            pfLight = SavePrefab(l, "GE_CeilingLight");

            // ประตูบานพับ (ช่องเปิด 1.2 x 2.2 ม. ตามแนวแกน X ท้องถิ่น, เปิดเข้า +Z)
            var d = new GameObject("GE_Door");
            var fr = G("Frame", d.transform);
            Box("Jamb_Hinge", fr, new Vector3(0.025f, 1.1f, 0), new Vector3(0.05f, 2.2f, W + 0.04f), mFrame, true);
            Box("Jamb_Latch", fr, new Vector3(1.175f, 1.1f, 0), new Vector3(0.05f, 2.2f, W + 0.04f), mFrame, true);
            Box("Head", fr, new Vector3(0.6f, 2.175f, 0), new Vector3(1.2f, 0.05f, W + 0.04f), mFrame, false);
            var hinge = G("Hinge", d.transform, new Vector3(0.06f, 0, 0));
            var leaf = Box("Leaf", hinge, new Vector3(0.54f, 1.075f, 0), new Vector3(1.07f, 2.12f, 0.045f), mDoor, true);
            leaf.layer = LayerMask.NameToLayer("Interactable") >= 0 ? LayerMask.NameToLayer("Interactable") : 0;
            Box("Handle_A", hinge, new Vector3(0.95f, 1.0f, 0.05f), new Vector3(0.14f, 0.03f, 0.03f), mMetal, false);
            Box("Handle_B", hinge, new Vector3(0.95f, 1.0f, -0.05f), new Vector3(0.14f, 0.03f, 0.03f), mMetal, false);
            Box("VisionPanel", hinge, new Vector3(0.54f, 1.55f, 0), new Vector3(0.3f, 0.5f, 0.05f), mGlass, false);
            // Trigger บน root ให้ PlayerInteraction เดิม (OverlapSphere บนเลเยอร์ Interactable + TryGetComponent) เจอประตู
            int interLayer = LayerMask.NameToLayer("Interactable");
            if (interLayer >= 0) d.layer = interLayer;
            var trig = d.AddComponent<BoxCollider>();
            trig.isTrigger = true; trig.center = new Vector3(0.6f, 1.0f, 0f); trig.size = new Vector3(1.4f, 2.0f, 1.2f);
            var door = d.AddComponent<GEDoor>();
            door.hinge = hinge; door.openAngle = -90f; door.startOpen = true;
            hinge.localRotation = Quaternion.Euler(0, -90f, 0);
            pfDoor = SavePrefab(d, "GE_Door");

            // ป้ายหมายเลขห้อง
            var s = new GameObject("GE_RoomSign");
            Box("Plate", s.transform, Vector3.zero, new Vector3(0.5f, 0.26f, 0.03f), mSign, false);
            Text("Label", s.transform, new Vector3(0, 0, -0.02f), new Vector2(0.46f, 0.22f), "GE000\nห้องเรียน", Color.white, 1.2f);
            pfRoomSign = SavePrefab(s, "GE_RoomSign");

            // ม้านั่ง
            var b = new GameObject("GE_Bench");
            Box("Seat", b.transform, new Vector3(0, 0.44f, 0), new Vector3(1.6f, 0.06f, 0.45f), mWood, false);
            Box("Back", b.transform, new Vector3(0, 0.8f, 0.2f), new Vector3(1.6f, 0.35f, 0.05f), mWood, false);
            foreach (var x in new[] { -0.7f, 0.7f })
            {
                Box("Leg", b.transform, new Vector3(x, 0.21f, 0), new Vector3(0.06f, 0.42f, 0.4f), mRail, false);
                Box("BackPost", b.transform, new Vector3(x, 0.65f, 0.2f), new Vector3(0.05f, 0.5f, 0.05f), mRail, false);
            }
            var bbc = b.AddComponent<BoxCollider>(); bbc.center = new Vector3(0, 0.5f, 0.02f); bbc.size = new Vector3(1.6f, 1.0f, 0.5f);
            pfBench = SavePrefab(b, "GE_Bench");

            // อุปกรณ์ห้องน้ำ (ห้องภายใน 2.8 x 6.8 ม., ประตูอยู่ด้าน -Z, จุดศูนย์กลางห้อง)
            var rr = new GameObject("GE_RestroomFixtures");
            var counter = G("SinkCounter", rr.transform);
            Box("Counter", counter, new Vector3(1.12f, 0.42f, -1.2f), new Vector3(0.55f, 0.84f, 2.0f), mTileWall, true);
            Box("Top", counter, new Vector3(1.12f, 0.86f, -1.2f), new Vector3(0.58f, 0.04f, 2.04f), mSignDark, false);
            foreach (var z in new[] { -1.8f, -0.6f })
            {
                Box("Basin", counter, new Vector3(1.1f, 0.89f, z), new Vector3(0.38f, 0.04f, 0.45f), mCeramic, false);
                Box("Tap", counter, new Vector3(1.3f, 0.98f, z), new Vector3(0.04f, 0.14f, 0.04f), mMetal, false);
            }
            Box("Mirror", counter, new Vector3(1.385f, 1.5f, -1.2f), new Vector3(0.02f, 0.85f, 1.9f), mMirror, false);
            var stalls = G("Stalls", rr.transform);
            // แผงหน้าห้องส้วม z = 1.2 มีช่องเข้า 0.8 ม. สองช่อง
            Box("Front_L", stalls, new Vector3(-1.3f, 1.1f, 1.2f), new Vector3(0.2f, 1.9f, 0.05f), mAccent, true);
            Box("Front_M", stalls, new Vector3(0f, 1.1f, 1.2f), new Vector3(0.4f, 1.9f, 0.05f), mAccent, true);
            Box("Front_R", stalls, new Vector3(1.3f, 1.1f, 1.2f), new Vector3(0.2f, 1.9f, 0.05f), mAccent, true);
            Box("Divider", stalls, new Vector3(0f, 1.1f, 2.3f), new Vector3(0.05f, 1.9f, 2.2f), mAccent, true);
            foreach (var x in new[] { -0.7f, 0.7f })
            {
                Box("Toilet", stalls, new Vector3(x, 0.21f, 2.95f), new Vector3(0.38f, 0.42f, 0.55f), mCeramic, true);
                Box("Tank", stalls, new Vector3(x, 0.62f, 3.28f), new Vector3(0.45f, 0.42f, 0.18f), mCeramic, false);
            }
            Inst(pfLight, rr.transform, new Vector3(0, WallH - 0.03f, -1.0f), 0, "CeilingLight");
            pfRestroom = SavePrefab(rr, "GE_RestroomFixtures");

            // ภายในห้องเรียน (ห้อง 7 x 9 ม., กระดานผนังด้าน -X, จุดศูนย์กลางห้อง)
            var c = new GameObject("GE_ClassroomInterior");
            var board = G("Whiteboard", c.transform);
            Box("Frame", board, new Vector3(-3.385f, 1.5f, 0), new Vector3(0.02f, 1.3f, 3.7f), mFrame, true);
            Box("Surface", board, new Vector3(-3.37f, 1.5f, 0), new Vector3(0.02f, 1.2f, 3.6f), mBoard, false);
            Box("Tray", board, new Vector3(-3.34f, 0.86f, 0), new Vector3(0.08f, 0.03f, 3.2f), mFrame, false);
            var teacher = G("TeacherArea", c.transform);
            Inst(AssetDatabase.LoadAssetAtPath<GameObject>(ClassPf + "TeacherDesk.prefab"), teacher, new Vector3(-2.6f, 0, 0), -90f);
            Inst(AssetDatabase.LoadAssetAtPath<GameObject>(ClassPf + "TeacherChair.prefab"), teacher, new Vector3(-3.0f, 0, 0), 90f);
            var desk = AssetDatabase.LoadAssetAtPath<GameObject>(ClassPf + "StudentDesk.prefab");
            var chair = AssetDatabase.LoadAssetAtPath<GameObject>(ClassPf + "StudentChair.prefab");
            var seats = G("StudentSeats", c.transform);
            float[] rows = { -0.7f, 0.36f, 1.42f, 2.48f };
            float[] cols = { -3.3f, -1.1f, 1.1f, 3.3f };   // 4 คอลัมน์ ทางเดินกว้าง (รองรับผู้เล่นหลักที่ scale 0.75)
            for (int r = 0; r < rows.Length; r++)
                for (int k = 0; k < cols.Length; k++)
                {
                    var seat = G($"Seat_R{r + 1}_C{k + 1}", seats, new Vector3(rows[r], 0, cols[k]));
                    Inst(desk, seat, Vector3.zero, -90f);
                    Inst(chair, seat, new Vector3(0.5f, 0, 0), -90f);
                }
            var lights = G("Lights", c.transform);
            Inst(pfLight, lights, new Vector3(-0.5f, WallH - 0.03f, -2.2f), 90f, "CeilingLight_A");
            Inst(pfLight, lights, new Vector3(-0.5f, WallH - 0.03f, 2.2f), 90f, "CeilingLight_B");
            pfClassroom = SavePrefab(c, "GE_ClassroomInterior");

            // ผู้เล่นมุมมองบุคคลที่หนึ่ง
            var p = new GameObject("GE_FPSPlayer");
            p.tag = "Player";
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) p.layer = playerLayer;
            var cc = p.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.35f; cc.slopeLimit = 50f; cc.skinWidth = 0.03f; cc.minMoveDistance = 0f;
            var ctrl = p.AddComponent<GEFirstPersonController>();
            var camT = G("CameraPivot", p.transform, new Vector3(0, 1.62f, 0));
            camT.tag = "MainCamera";
            var cam = camT.gameObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.fieldOfView = 70f; cam.farClipPlane = 300f;
            camT.gameObject.AddComponent<AudioListener>();
            var urpCam = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpCam != null) camT.gameObject.AddComponent(urpCam);
            ctrl.cameraPivot = camT;
            ctrl.promptFont = guiFont;
            int mask = ~0;
            if (playerLayer >= 0) mask &= ~(1 << playerLayer);
            mask &= ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
            ctrl.interactMask = mask;
            pfPlayer = SavePrefab(p, "GE_FPSPlayer");
        }

        // ------------------------------------------------------------------ exterior
        static void BuildExterior(Transform ex)
        {
            BoxMM("Ground_Grass", ex, new Vector3(-70, -0.3f, -60), new Vector3(70, 0, 60), mGrass);
            BoxMM("Plaza", ex, new Vector3(-16, 0, -18), new Vector3(16, 0.02f, 0), mPlaza);

            var porch = G("EntrancePorch", ex);
            BoxMM("Porch", porch, new Vector3(-6, 0, -3), new Vector3(6, B, 0), mSlab);
            var steps = G("FrontSteps", porch);
            BoxMM("Step_1", steps, new Vector3(-3, 0, -3.35f), new Vector3(3, 0.30f, -3f), mStair);
            BoxMM("Step_2", steps, new Vector3(-3, 0, -3.7f), new Vector3(3, 0.15f, -3.35f), mStair);
            var rails = G("PorchRailings", porch);
            Railing(rails, "Rail_West", new Vector3(-5.95f, B, -2.95f), new Vector3(-5.95f, B, -0.05f));
            Railing(rails, "Rail_FrontL", new Vector3(-5.95f, B, -2.95f), new Vector3(-3.05f, B, -2.95f));
            Railing(rails, "Rail_FrontR", new Vector3(3.05f, B, -2.95f), new Vector3(5.95f, B, -2.95f));
            Railing(rails, "Rail_East", new Vector3(5.95f, B, -1.45f), new Vector3(5.95f, B, -0.05f));

            // ทางลาด (ความชัน ~1:12) จากลานขึ้นชานหน้าตึก
            var ramp = G("AccessRamp", ex);
            float rampEnd = 6f + B * 12f;
            RampPlate(ramp, "RampSurface", new Vector3(rampEnd, 0.02f, -2.25f), new Vector3(6f, B, -2.25f), 1.5f, mStair, 0.2f);
            SlopedRailing(ramp, "Rail_Outer", new Vector3(rampEnd, 0.02f, -2.97f), new Vector3(6f, B, -2.97f));
            SlopedRailing(ramp, "Rail_Inner", new Vector3(rampEnd, 0.02f, -1.53f), new Vector3(6f, B, -1.53f));

            // หลังคากันสาดทางเข้า + ป้าย
            var canopy = G("EntranceCanopy", ex);
            BoxMM("CanopySlab", canopy, new Vector3(-5f, 3.55f, -3.3f), new Vector3(5f, 3.8f, 0f), mSlab, false);
            BoxMM("Fascia", canopy, new Vector3(-5f, 3.0f, -3.35f), new Vector3(5f, 3.8f, -3.2f), mAccent, false);
            foreach (var x in new[] { -4.7f, 4.7f })
                BoxMM("Column", canopy, new Vector3(x - 0.15f, B, -3.1f), new Vector3(x + 0.15f, 3.55f, -2.8f), mWall);
            var sign = G("Sign_Main", canopy, new Vector3(0, 3.4f, -3.37f));
            Text("Title", sign, new Vector3(0, 0.12f, 0), new Vector2(8.5f, 0.5f), "อาคารเรียน GE", Color.white, 6f);
            Text("Subtitle", sign, new Vector3(0, -0.24f, 0), new Vector2(8f, 0.22f), "GENERAL EDUCATION BUILDING", new Color(0.9f, 0.95f, 0.95f), 3f);

            var mono = G("Sign_Plaza", ex, new Vector3(-8.5f, 0, -9f), 15f);
            BoxMM("Base", mono, new Vector3(-1.8f, 0, -0.25f), new Vector3(1.8f, 1.3f, 0.25f), mSignDark);
            Box("AccentBar", mono, new Vector3(0, 1.32f, 0), new Vector3(3.7f, 0.06f, 0.52f), mAccent, false);
            Text("Title", mono, new Vector3(0, 0.82f, -0.27f), new Vector2(3.3f, 0.5f), "อาคารเรียน GE", Color.white, 6f);
            Text("Subtitle", mono, new Vector3(0, 0.42f, -0.27f), new Vector2(3.3f, 0.25f), "General Education Building", new Color(0.85f, 0.9f, 0.9f), 3f);

            // ภูมิทัศน์ (ใช้ Asset Synty ที่มีอยู่)
            var land = G("Landscaping", ex);
            var bush = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Bush_01.prefab");
            var bench = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_ParkBench_01.prefab");
            var bin = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Trashbin_01.prefab");
            if (bush != null)
                foreach (var x in new[] { -18f, -15f, -12f, -9f, 14f, 17f })
                    Inst(bush, land, new Vector3(x, 0, -1.0f), 0f, "Bush");
            if (bench != null)
            {
                Inst(bench, land, new Vector3(10f, 0.02f, -9f), 0f, "ParkBench_1");
                Inst(bench, land, new Vector3(-12f, 0.02f, -13f), 0f, "ParkBench_2");
            }
            if (bin != null) Inst(bin, land, new Vector3(-3.6f, 0.02f, -4.6f), 0f, "TrashBin");
        }

        // ------------------------------------------------------------------ slabs / roof / facade
        static void BuildSlabsAndRoof(Transform s)
        {
            var slabs = G("FloorSlabs", s);
            // ชั้น 1 = ฐานยกพื้น
            BoxMM("Plinth_Main", slabs, new Vector3(-20, 0, 0), new Vector3(20, B, 12), mSlab);
            BoxMM("Plinth_Core", slabs, new Vector3(-6, 0, 12), new Vector3(6, B, 14), mSlab);
            for (int i = 1; i < Floors; i++)
            {
                float y = FloorY(i);
                var f = G("Slab_Floor" + (i + 1), slabs);
                // เว้นช่องบันได x -6..0, z 9..14
                BoxMM("Front", f, new Vector3(-20, y - T, 0), new Vector3(20, y, 9), mSlab);
                BoxMM("BackLeft", f, new Vector3(-20, y - T, 9), new Vector3(-6, y, 12), mSlab);
                BoxMM("BackRight", f, new Vector3(0, y - T, 9), new Vector3(20, y, 12), mSlab);
                BoxMM("BackCore", f, new Vector3(0, y - T, 12), new Vector3(6, y, 14), mSlab);
            }
            float roofY = FloorY(Floors);
            var roof = G("Roof", s);
            BoxMM("Roof_Main", roof, new Vector3(-20.3f, roofY - T, -0.3f), new Vector3(20.3f, roofY, 12.3f), mSlab);
            BoxMM("Roof_Core", roof, new Vector3(-6.3f, roofY - T, 12.3f), new Vector3(6.3f, roofY, 14.3f), mSlab);
            BoxMM("Parapet_Front", roof, new Vector3(-20.3f, roofY, -0.3f), new Vector3(20.3f, roofY + 0.9f, -0.1f), mAccent);
            BoxMM("Parapet_Back", roof, new Vector3(-20.3f, roofY, 12.1f), new Vector3(20.3f, roofY + 0.9f, 12.3f), mWall);
            BoxMM("Parapet_L", roof, new Vector3(-20.3f, roofY, -0.3f), new Vector3(-20.1f, roofY + 0.9f, 12.3f), mWall);
            BoxMM("Parapet_R", roof, new Vector3(20.1f, roofY, -0.3f), new Vector3(20.3f, roofY + 0.9f, 12.3f), mWall);
            var facadeSign = G("Sign_Facade", roof, new Vector3(0, roofY + 0.45f, -0.32f));
            Text("Title", facadeSign, Vector3.zero, new Vector2(12f, 0.75f), "อาคารเรียน GE  ·  GENERAL EDUCATION", Color.white, 8f);

            // ครีบบังแดดด้านหลัง (สไตล์โมเดิร์น)
            var fins = G("FacadeFins", s);
            for (float x = -20f; x <= 20.01f; x += 3.5f)
            {
                if (x > -6.01f && x < 6.01f) continue;
                BoxMM("Fin", fins, new Vector3(x - 0.08f, 0, 12f), new Vector3(x + 0.08f, roofY, 12.6f), mAccent);
            }
            for (float x = -6f; x <= 6.01f; x += 3f)
                BoxMM("Fin_Core", fins, new Vector3(x - 0.08f, 0, 14f), new Vector3(x + 0.08f, roofY, 14.6f), mAccent);
        }

        // ------------------------------------------------------------------ floors
        static readonly float[][] Rooms = { new[] { -20f, -13f }, new[] { -13f, -6f }, new[] { 6f, 13f }, new[] { 13f, 20f } };

        static void BuildFloor(Transform fl, int i)
        {
            float y = FloorY(i), h = WallH;
            int f = i + 1;
            var walls = G("Walls", fl);
            var rooms = G("Classrooms", fl);
            var lights = G("Lights", fl);
            var signs = G("Signs", fl);

            // เสาหน้าอาคาร
            var cols = G("Columns", fl);
            foreach (var x in new[] { -19.85f, -13f, -6f, 6f, 13f, 19.85f })
                BoxMM("Column", cols, new Vector3(x - 0.15f, y, 0f), new Vector3(x + 0.15f, y + h, 0.3f), mWall);

            // ราวกันตกหน้าทางเดิน
            var rails = G("FrontRailings", fl);
            Railing(rails, "Rail_L2", new Vector3(-19.7f, y, 0.1f), new Vector3(-13.15f, y, 0.1f));
            Railing(rails, "Rail_L1", new Vector3(-12.85f, y, 0.1f), new Vector3(-6.15f, y, 0.1f));
            Railing(rails, "Rail_R1", new Vector3(6.15f, y, 0.1f), new Vector3(12.85f, y, 0.1f));
            Railing(rails, "Rail_R2", new Vector3(13.15f, y, 0.1f), new Vector3(19.7f, y, 0.1f));
            if (i == 0)
            {
                // ทางเข้าหลักเปิดโล่ง x -3..3 · ด้านข้างเป็นผนังกระจก
                var gw = G("LobbyGlassFront", fl);
                foreach (var sgn in new[] { -1f, 1f })
                {
                    float a = sgn < 0 ? -5.85f : 3.0f, b = sgn < 0 ? -3.0f : 5.85f;
                    Box("Glass", gw, new Vector3((a + b) / 2, y + h / 2, 0.15f), new Vector3(b - a, h, 0.04f), mGlass, true).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    Box("Mullion", gw, new Vector3((a + b) / 2, y + h / 2, 0.15f), new Vector3(0.06f, h, 0.08f), mFrame, false);
                    Box("Transom", gw, new Vector3((a + b) / 2, y + 2.4f, 0.15f), new Vector3(b - a, 0.06f, 0.08f), mFrame, false);
                }
                foreach (var x in new[] { -3f, 3f })
                    BoxMM("EntrancePost", gw, new Vector3(x - 0.1f, y, 0.05f), new Vector3(x + 0.1f, y + h, 0.25f), mAccent);
            }
            else Railing(rails, "Rail_Lounge", new Vector3(-5.85f, y, 0.1f), new Vector3(5.85f, y, 0.1f));

            // ผนังหน้าห้องเรียน (ฝั่งทางเดิน z = 3) + ประตู + ป้ายห้อง
            var opsL = new List<Op>(); var opsR = new List<Op>();
            for (int r = 0; r < Rooms.Length; r++)
            {
                float x0 = Rooms[r][0], x1 = Rooms[r][1], cx = (x0 + x1) / 2;
                bool right = x0 > 0;
                string code = $"GE{f}0{r + 1}";
                float da = right ? x0 + 0.6f : x1 - 1.8f;
                var ops = right ? opsR : opsL;
                ops.Add(new Op(da, da + 1.2f, 0f, 2.2f, false));
                ops.Add(right ? new Op(x0 + 2.6f, x1 - 0.6f, 1.3f, 2.6f, true) : new Op(x0 + 0.6f, x1 - 2.6f, 1.3f, 2.6f, true));

                var room = G("Room_" + code, rooms);
                BoxMM("FloorFinish", room, new Vector3(x0 + 0.1f, y, 3.1f), new Vector3(x1 - 0.1f, y + 0.01f, 11.8f), mRoomFloor, false);
                Inst(pfClassroom, room, new Vector3(cx, y, 7.5f), right ? 0f : 180f, "Interior");
                Inst(pfDoor, room, new Vector3(da, y, 3.0f), 0f, "Door_" + code);
                var sg = Inst(pfRoomSign, signs, new Vector3(right ? x0 + 2.2f : x1 - 2.2f, y + 1.7f, 2.885f), 0f, "Sign_" + code);
                sg.GetComponentInChildren<TextMeshPro>().text = code + "\nห้องเรียน";
                var boardLabel = Text("BoardLabel", room, Vector3.zero, new Vector2(1.6f, 0.22f), code, new Color(0.2f, 0.35f, 0.4f), 2f);
                boardLabel.transform.position = new Vector3(right ? x0 + 0.1f + 0.04f : x1 - 0.1f - 0.04f, y + 2.35f, 7.5f);
                boardLabel.transform.rotation = Quaternion.Euler(0, right ? -90f : 90f, 0);
            }
            WallX(walls, "Wall_Corridor_Left", -20f, -6f, 3f, y, h, mWall, opsL);
            WallX(walls, "Wall_Corridor_Right", 6f, 20f, 3f, y, h, mWall, opsR);

            // ผนังหลังห้องเรียน (หน้าต่าง)
            var backL = new List<Op>(); var backR = new List<Op>();
            foreach (var rm in Rooms)
            {
                var list = rm[0] > 0 ? backR : backL;
                list.Add(new Op(rm[0] + 0.9f, rm[0] + 3.2f, 0.9f, 2.6f, true));
                list.Add(new Op(rm[0] + 3.8f, rm[0] + 6.1f, 0.9f, 2.6f, true));
            }
            WallX(walls, "Wall_Back_Left", -20f, -6f, 11.9f, y, h, mWall, backL);
            WallX(walls, "Wall_Back_Right", 6f, 20f, 11.9f, y, h, mWall, backR);

            // ผนังข้าง / ผนังกั้นห้อง
            var side = new List<Op> { new Op(0.6f, 2.4f, 1.0f, 2.6f, true), new Op(5f, 10f, 0.9f, 2.6f, true) };
            WallZ(walls, "Wall_Side_West", 0f, 12f, -19.9f, y, h, mWall, new List<Op>(side));
            WallZ(walls, "Wall_Side_East", 0f, 12f, 19.9f, y, h, mWall, new List<Op>(side));
            WallZ(walls, "Wall_Partition_L", 3.1f, 11.8f, -13f, y, h, mWall);
            WallZ(walls, "Wall_Partition_R", 3.1f, 11.8f, 13f, y, h, mWall);
            WallZ(walls, "Wall_Core_West", 3.1f, 7f, -6f, y, h, mWall);
            WallZ(walls, "Wall_Core_East", 3.1f, 14f, 6f, y, h, mWall);

            // ห้องน้ำ (x 0..6, z 7..14)
            var rest = G("Restrooms", fl);
            WallX(rest, "Wall_RestroomFront", 0.1f, 5.9f, 7f, y, h, mWall, new List<Op> { new Op(0.3f, 1.5f, 0f, 2.2f, false), new Op(3.3f, 4.5f, 0f, 2.2f, false) });
            WallZ(rest, "Wall_RestroomDivider", 7.1f, 13.8f, 3f, y, h, mTileWall);
            WallX(rest, "Wall_RestroomBack", 0.1f, 5.9f, 13.9f, y, h, mWall, new List<Op> { new Op(0.8f, 2.2f, 1.8f, 2.6f, true), new Op(3.8f, 5.2f, 1.8f, 2.6f, true) });
            string[] names = { "ห้องน้ำชาย", "ห้องน้ำหญิง" };
            string[] ids = { "Male", "Female" };
            for (int k = 0; k < 2; k++)
            {
                float rx0 = k * 3f, rcx = rx0 + 1.5f;
                var rm = G("Restroom_" + ids[k], rest);
                BoxMM("FloorFinish", rm, new Vector3(rx0 + 0.1f, y, 7.1f), new Vector3(rx0 + 2.9f, y + 0.01f, 13.8f), mRestFloor, false);
                Inst(pfRestroom, rm, new Vector3(rcx, y, 10.45f), 0f, "Fixtures");
                Inst(pfDoor, rm, new Vector3(rx0 + 0.3f, y, 7f), 0f, "Door_Restroom_" + ids[k]);
                var plate = G("Sign_" + ids[k], signs, new Vector3(rx0 + 0.9f, y + 2.55f, 6.885f));
                Box("Plate", plate, Vector3.zero, new Vector3(1.0f, 0.3f, 0.03f), mSign, false);
                Text("Label", plate, new Vector3(0, 0, -0.02f), new Vector2(0.92f, 0.24f), names[k] + (k == 0 ? "  Male" : "  Female"), Color.white, 1.5f);
            }

            // โถง / พื้นที่นั่งพัก
            var lounge = G(i == 0 ? "Lobby" : "Lounge", fl);
            Inst(pfBench, lounge, new Vector3(-3.2f, y, 4.6f), 180f, "Bench_1");
            Inst(pfBench, lounge, new Vector3(3.0f, y, 4.6f), 180f, "Bench_2");
            var plant = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_PotPlant_01.prefab");
            if (plant != null)
            {
                Inst(plant, lounge, new Vector3(-5.4f, y, 3.6f), 0f, "PotPlant");
                Inst(plant, lounge, new Vector3(5.4f, y, 3.6f), 0f, "PotPlant");
            }
            // ป้ายบอกชั้น (ผนังโถงฝั่งตะวันตก หันเข้าโถง)
            var fs = G("Sign_Floor", signs, new Vector3(-5.885f, y + 1.9f, 5.0f), -90f);
            Box("Plate", fs, Vector3.zero, new Vector3(1.4f, 0.7f, 0.03f), mSign, false);
            Text("Label", fs, new Vector3(0, 0.08f, -0.02f), new Vector2(1.3f, 0.4f), "ชั้น " + f, Color.white, 4f);
            Text("Sub", fs, new Vector3(0, -0.2f, -0.02f), new Vector2(1.3f, 0.16f), $"FLOOR {f}  ·  GE{f}01 – GE{f}04", new Color(0.9f, 0.95f, 0.95f), 1.5f);
            // ป้ายบอกทาง (ผนังโถงฝั่งตะวันออก)
            var dir = G("Sign_Directory", signs, new Vector3(5.885f, y + 1.75f, 5.0f), 90f);
            Box("Plate", dir, Vector3.zero, new Vector3(2.4f, 1.0f, 0.03f), mSignDark, false);
            string dirText = i == 0
                ? "ยินดีต้อนรับสู่อาคารเรียน GE\nชั้น 1 GE101–104 · ชั้น 2 GE201–204 · ชั้น 3 GE301–304\n◀ ห้อง GE"+f+"01–02     บันได / ห้องน้ำ ▲     ห้อง GE"+f+"03–04 ▶"
                : $"ชั้น {f}\n◀ ห้อง GE{f}01–02     บันได / ห้องน้ำ ▲     ห้อง GE{f}03–04 ▶";
            Text("Label", dir, new Vector3(0, 0, -0.02f), new Vector2(2.25f, 0.85f), dirText, Color.white, 2f);

            // ไฟทางเดิน / โถง
            float ly = y + h - 0.03f;
            foreach (var x in new[] { -16.5f, -9.5f, 9.5f, 16.5f })
                Inst(pfLight, lights, new Vector3(x, ly, 1.5f), 0f, "CorridorLight");
            Inst(pfLight, lights, new Vector3(-3f, ly, 3.5f), 0f, "LoungeLight");
            Inst(pfLight, lights, new Vector3(3f, ly, 3.5f), 0f, "LoungeLight");
            Inst(pfLight, lights, new Vector3(-3f, ly, 8f), 0f, "StairLandingLight");
        }

        // ------------------------------------------------------------------ stairs
        static void BuildStairwell(Transform st)
        {
            float top = FloorY(Floors) - T;
            var walls = G("Walls", st);
            BoxMM("Wall_West", walls, new Vector3(-6.1f, B, 7f), new Vector3(-5.9f, top, 14f), mWall);
            BoxMM("Wall_East", walls, new Vector3(-0.1f, B, 7f), new Vector3(0.1f, top, 14f), mWall);
            for (int i = 0; i < Floors; i++)
            {
                float y = FloorY(i), hh = (i == Floors - 1) ? WallH : H;
                var ops = new List<Op> { i < Floors - 1 ? new Op(-5.0f, -1.0f, 2.5f, 3.4f, true) : new Op(-5.0f, -1.0f, 0.9f, 2.6f, true) };
                WallX(walls, "Wall_Back_" + (i + 1), -5.9f, -0.1f, 13.9f, y, hh, mWall, ops);
                // ป้ายบอกชั้นที่ชานบันได
                var s = G("Sign_StairFloor_" + (i + 1), st, new Vector3(-0.115f, y + 1.9f, 8.0f), 90f);
                Box("Plate", s, Vector3.zero, new Vector3(1.0f, 0.5f, 0.03f), mSign, false);
                Text("Label", s, new Vector3(0, 0, -0.02f), new Vector2(0.9f, 0.4f), "ชั้น " + (i + 1) + "\nFLOOR " + (i + 1), Color.white, 3f);
            }

            for (int i = 0; i < Floors - 1; i++)
            {
                float y = FloorY(i);
                var lv = G($"Flight_{i + 1}to{i + 2}", st);
                // ช่วงที่ 1 (x -5.9..-3.5) ขึ้นไปทาง +Z ถึงชานพัก
                var fa = G("FlightA", lv);
                for (int k = 0; k < Steps - 1; k++)
                {
                    float z0 = 9f + k * Tread, topY = y + (k + 1) * Rise;
                    BoxMM("Step_" + (k + 1), fa, new Vector3(-5.9f, topY - 0.35f, z0), new Vector3(-3.5f, topY, z0 + Tread), mStair);
                }
                RampPlate(fa, "RampCollider", new Vector3(-4.7f, y, 9f - Tread), new Vector3(-4.7f, y + 1.8f, 9f + (Steps - 1) * Tread), 2.4f);
                // ชานพัก
                float landZ = 9f + (Steps - 1) * Tread;   // 12.08
                BoxMM("MidLanding", lv, new Vector3(-5.9f, y + 1.8f - 0.2f, landZ), new Vector3(-0.1f, y + 1.8f, 13.8f), mStair);
                // ช่วงที่ 2 (x -2.5..-0.1) ขึ้นไปทาง -Z ถึงชั้นถัดไป
                var fb = G("FlightB", lv);
                for (int j = 0; j < Steps; j++)
                {
                    float z1 = 12.36f - j * Tread, topY = y + 1.8f + (j + 1) * Rise;
                    BoxMM("Step_" + (j + 1), fb, new Vector3(-2.5f, topY - 0.35f, z1 - Tread), new Vector3(-0.1f, topY, z1), mStair);
                }
                RampPlate(fb, "RampCollider", new Vector3(-1.3f, y + 1.8f, 12.36f + Tread), new Vector3(-1.3f, y + 3.6f, 12.36f - (Steps - 1) * Tread), 2.4f);
                // ราวกันตกด้านช่องบันได
                var rr = G("Railings", lv);
                SlopedRailing(rr, "Rail_FlightA", new Vector3(-3.5f, y, 9f - Tread), new Vector3(-3.5f, y + 1.8f, landZ));
                SlopedRailing(rr, "Rail_FlightB", new Vector3(-2.5f, y + 1.8f, 12.36f + Tread), new Vector3(-2.5f, y + 3.6f, 12.36f - (Steps - 1) * Tread));
                Railing(rr, "Rail_LandingGap", new Vector3(-3.5f, y + 1.8f, landZ), new Vector3(-2.5f, y + 1.8f, landZ));
                float yn = FloorY(i + 1);
                if (i + 1 < Floors - 1)
                    Railing(rr, "Rail_FloorGap", new Vector3(-3.5f, yn, 9.05f), new Vector3(-2.5f, yn, 9.05f));
                else
                    Railing(rr, "Rail_TopVoid", new Vector3(-5.9f, yn, 9.05f), new Vector3(-2.5f, yn, 9.05f));
                float mlY = (i == Floors - 2) ? FloorY(Floors) - T - 0.03f : y + 1.8f + 3.2f;
                Inst(pfLight, lv, new Vector3(-3f, mlY, 13.0f), 0f, "MidLandingLight");
                if (i == 0)
                {
                    // ปิดช่องใต้บันไดชั้นล่าง (กันเดินเข้าไปติด)
                    BoxMM("UnderStairInfill", lv, new Vector3(-3.5f, B, 9.3f), new Vector3(-0.1f, B + 1.55f, 12.08f), mWall);
                    BoxMM("UnderLandingInfill", lv, new Vector3(-5.9f, B, 12.08f), new Vector3(-0.1f, B + 1.55f, 13.8f), mWall);
                }
            }
        }

        // ------------------------------------------------------------------ lighting & player
        static void BuildLightingAndPlayer(Transform root)
        {
            var lg = G("Lighting", root);
            var sunT = G("Sun", lg);
            sunT.rotation = Quaternion.Euler(48f, 32f, 0f);
            var sun = sunT.gameObject.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f; sun.color = new Color(1f, 0.96f, 0.88f); sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.76f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.60f, 0.57f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.34f, 0.31f);

            var pl = G("Player", root);
            var spawn = G("PlayerSpawn", pl, new Vector3(0, 0.05f, -13f), 0f);
            var player = Inst(pfPlayer, pl, spawn.position, 0f, "GE_FPSPlayer");
            var ctrl = player.GetComponent<GEFirstPersonController>();
            ctrl.respawnPoint = spawn;
            EditorUtility.SetDirty(ctrl);
        }
    }
}
