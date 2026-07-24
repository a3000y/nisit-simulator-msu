#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NisitSimulator.Interaction;

namespace NisitSimulator.EditorTools
{
    // สร้างห้องภายในหลายแบบจาก KayKit Furniture (วางเรียงไกลจากเมือง) + จุดเกิด + ประตูออก
    //   ห้องเรียน / ห้องสมุด / โรงอาหาร / ห้องนอน / ร้านค้า
    // ใช้: เมนู  Nisit -> Build Interiors
    public static class InteriorBuilder
    {
        private const string FurnitureFolder = "Assets/_Project/Art/Models/KayKit_Furniture";
        private const string RestaurantFolder = "Assets/_Project/Art/Models/KayKit_Restaurant";
        private const float FurnitureScaleMul = 1f;   // ปรับเฉพาะชุด Furniture ถ้ายังไม่เท่าครัว
        private const float ChairTarget = 0.45f;      // เก้าอี้ = กี่เท่าตัวละคร (0.3 เล็กไป / 0.7 ใหญ่) — ย่อทั้งชุดตามนี้

        // ตึกชื่อไหน -> เข้าห้องแบบไหน
        internal static readonly Dictionary<string, string> RoomOf = new Dictionary<string, string>
        {
            { "อาคารเรียน", "Classroom" }, { "คณะ IT", "ITLab" }, { "อาคารบริหาร", "Office" },
            { "ห้องสมุด", "Library" }, { "โรงอาหาร", "Cafeteria" }, { "อาคารชมรม", "ClubRoom" },
            { "หอพัก", "Dorm" }, { "ร้านค้า", "Shop" },
        };
        // ทำห้องต่อ "ตึก" (ตึกเดียวกันชนิดเดียวกันจะได้ห้องคนละหน้าตา)
        private static readonly string[] Buildings =
        { "อาคารเรียน", "คณะ IT", "อาคารบริหาร", "ห้องสมุด", "โรงอาหาร", "อาคารชมรม", "หอพัก", "ร้านค้า" };
        private static readonly string[] ChairVariants =
        { "chair_A", "chair_A_wood", "chair_B", "chair_B_wood", "chair_C" };

        private static Dictionary<string, GameObject> _lib;
        private static Transform _root;
        private static System.Random _rng;      // สุ่มต่อห้อง
        private static string _roomChair;       // เก้าอี้ของห้องนี้
        private static float _fScaleF = 1f, _fScaleR = 1f, _unit = 1.3f;   // ย่อของ Furniture / Restaurant แยกกัน
        private static System.Collections.Generic.HashSet<string> _restNames;
        private static int _layer;

        [MenuItem("Nisit/Build Interiors", false, 7)]
        public static void Build()
        {
            // โหลดแยกชุด เพื่อย่อให้ขนาดเท่ากัน (เก้าอี้ทั้ง 2 ชุด = ขนาดเดียวกัน)
            var libF = Load(FurnitureFolder);
            var libR = Load(RestaurantFolder);
            if (libF.Count == 0) { EditorUtility.DisplayDialog("Nisit", $"ไม่พบเฟอร์นิเจอร์ใน {FurnitureFolder}", "OK"); return; }

            _lib = new System.Collections.Generic.Dictionary<string, GameObject>(libF);
            _restNames = new System.Collections.Generic.HashSet<string>();
            foreach (var kv in libR)   // ของ Restaurant ที่ชื่อไม่ซ้ำ Furniture -> ใช้ชุด Restaurant
                if (!_lib.ContainsKey(kv.Key)) { _lib[kv.Key] = kv.Value; _restNames.Add(kv.Key); }

            _layer = LayerMask.NameToLayer("Interactable");
            if (_layer < 0) { EditorUtility.DisplayDialog("Nisit", "ไม่มี Layer Interactable — รัน Setup M1 Scene ก่อน", "OK"); return; }

            // วัดส่วนสูงตัวละครจาก CharacterController (แม่นกว่า renderer bounds ที่เกินจริง)
            _unit = 1.3f;
            var player = GameObject.Find("Player");
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null && cc.height > 0.01f) _unit = cc.height * Mathf.Abs(player.transform.lossyScale.y);
                else { float h = Bounds(player).size.y; if (h > 0.05f) _unit = h; }
            }

            // Restaurant (ห้องครัว) = อ้างอิงหลัก: เก้าอี้ = ChairTarget เท่าตัวละคร
            var chR = libR.GetValueOrDefault("chair_A") ?? libR.GetValueOrDefault("chair_stool");
            _fScaleR = (chR != null) ? ChairTarget * _unit / Mathf.Max(0.01f, Height(chR)) : _unit * ChairTarget;
            // Furniture = ย่อให้ "โต๊ะ" ขนาดเท่าห้องครัว (เทียบเก้าอี้ทำให้ใหญ่เกิน เพราะสัดส่วนต่างชุด)
            var rTab = libR.GetValueOrDefault("table_round_A");
            var fTab = libF.GetValueOrDefault("table_medium") ?? libF.GetValueOrDefault("table_small");
            float refR = rTab != null ? Height(rTab) : 1f;
            float refF = fTab != null ? Height(fTab) : 1f;
            _fScaleF = ((refF > 0.01f) ? _fScaleR * refR / refF : _fScaleR) * FurnitureScaleMul;

            if (!EditorUtility.DisplayDialog("Nisit — Build Interiors",
                "จะสร้างห้องภายใน 5 แบบ (เรียน/สมุด/โรงอาหาร/นอน/ร้านค้า) วางไกลจากเมือง\n\nทำต่อไหม?", "ทำเลย", "ยกเลิก"))
                return;

            var old = GameObject.Find("Interiors");
            if (old != null) Object.DestroyImmediate(old);
            var oldClassroom = GameObject.Find("Interior_Classroom");   // ของเก่าจากเวอร์ชันห้องเดียว
            if (oldClassroom != null) Object.DestroyImmediate(oldClassroom);
            _root = new GameObject("Interiors").transform;
            Undo.RegisterCreatedObjectUndo(_root.gameObject, "Build Interiors");

            float spacing = 40f * _unit;
            for (int i = 0; i < Buildings.Length; i++)
            {
                Vector3 O = new Vector3(1000f + i * spacing, 0f, 0f);
                _rng = new System.Random(i * 7919 + 17);              // seed คงที่ต่อตึก
                _roomChair = ChairVariants[_rng.Next(ChairVariants.Length)];
                string type = RoomOf.GetValueOrDefault(Buildings[i], "Classroom");
                BuildShell(O, Buildings[i]);   // จุดเกิด = Spawn_<ชื่อตึก>
                Furnish(type, O);
            }

            if (Object.FindFirstObjectByType<InteriorManager>() == null)
                new GameObject("InteriorManager").AddComponent<InteriorManager>();

            Selection.activeGameObject = _root.gameObject;
            Debug.Log($"<color=lime>[Nisit] ✅ สร้างห้องภายในเสร็จ {Buildings.Length} ห้อง (ห้องละตึก สุ่มให้ต่างกัน)\n" +
                      "ต่อไปกด Nisit -> Setup Building Doors แล้ว Setup Seating</color>");
        }

        // ---------- โครงห้อง (พื้น/ผนัง/ประตูออก/จุดเกิด) ----------
        private static void BuildShell(Vector3 O, string key)
        {
            float fw = 12f * _unit, fd = 9f * _unit, wh = 2.4f * _unit, wt = 0.3f * _unit, gap = 2.6f * _unit;
            var floorMat = Mat(Tint(new Color(0.78f, 0.68f, 0.52f), 0.12f));   // สีพื้น/ผนังสุ่มต่อห้อง
            var wallMat = Mat(Tint(new Color(0.88f, 0.88f, 0.85f), 0.09f));

            Prim("Floor", new Vector3(O.x, -0.1f * _unit, O.z), new Vector3(fw, 0.2f * _unit, fd), floorMat);
            Prim("Wall_N", new Vector3(O.x, wh * 0.5f, O.z + fd * 0.5f), new Vector3(fw + wt, wh, wt), wallMat);
            Prim("Wall_E", new Vector3(O.x + fw * 0.5f, wh * 0.5f, O.z), new Vector3(wt, wh, fd), wallMat);
            Prim("Wall_W", new Vector3(O.x - fw * 0.5f, wh * 0.5f, O.z), new Vector3(wt, wh, fd), wallMat);
            float segW = (fw - gap) * 0.5f;
            Prim("Wall_S1", new Vector3(O.x - gap * 0.5f - segW * 0.5f, wh * 0.5f, O.z - fd * 0.5f), new Vector3(segW, wh, wt), wallMat);
            Prim("Wall_S2", new Vector3(O.x + gap * 0.5f + segW * 0.5f, wh * 0.5f, O.z - fd * 0.5f), new Vector3(segW, wh, wt), wallMat);

            var spawn = new GameObject("Spawn_" + key);
            spawn.transform.SetParent(_root);
            spawn.transform.position = new Vector3(O.x, 0.1f * _unit, O.z - fd * 0.5f + 1.8f * _unit);

            float doorH = 1.9f * _unit;
            var exit = Prim("ExitDoor", new Vector3(O.x, doorH * 0.5f, O.z - fd * 0.5f), new Vector3(gap * 0.92f, doorH, 0.18f * _unit), Mat(new Color(0.3f, 0.7f, 0.42f)));
            exit.GetComponent<Collider>().isTrigger = true;
            exit.layer = _layer;
            exit.AddComponent<InteriorExit>();

            // กำแพงล่องหนปิดช่องประตู — กันเดินทะลุออกนอกห้อง (ยังกด E ออกได้ เพราะประตูเขียวเป็น trigger ซ้อนอยู่)
            var block = Prim("ExitBlock", new Vector3(O.x, wh * 0.5f, O.z - fd * 0.5f), new Vector3(gap, wh, wt), wallMat);
            block.GetComponent<Renderer>().enabled = false;   // ล่องหน เหลือแค่ collider
        }

        // สุ่มสีเล็กน้อยจากสีฐาน (ต่อห้อง)
        private static Color Tint(Color c, float d)
        {
            return new Color(
                Mathf.Clamp01(c.r + (float)(_rng.NextDouble() - 0.5) * 2f * d),
                Mathf.Clamp01(c.g + (float)(_rng.NextDouble() - 0.5) * 2f * d),
                Mathf.Clamp01(c.b + (float)(_rng.NextDouble() - 0.5) * 2f * d));
        }

        // กระดานดำติดผนังหน้าห้อง (บอร์ดเขียว + กรอบไม้)
        private static void MakeBlackboard(Vector3 O, float fw, float fd, float wt, float u)
        {
            float bw = fw * 0.42f, bh = 1.1f * u, cy = 1.35f * u;
            float zw = O.z + fd * 0.5f - wt;
            Prim("BoardFrame", new Vector3(O.x, cy, zw), new Vector3(bw + 0.12f * u, bh + 0.12f * u, 0.06f * u), Mat(new Color(0.45f, 0.30f, 0.18f)));
            Prim("Board", new Vector3(O.x, cy, zw - 0.04f * u), new Vector3(bw, bh, 0.03f * u), Mat(new Color(0.13f, 0.33f, 0.22f)));
        }

        // ไวท์บอร์ดติดผนังหน้าห้อง (บอร์ดขาว + กรอบเงิน + รอยปากกา) — เหมือน MakeBlackboard แต่ขาว
        private static void MakeWhiteboard(Vector3 O, float fw, float fd, float wt, float u)
        {
            float bw = fw * 0.42f, bh = 1.1f * u, cy = 1.35f * u;
            float zw = O.z + fd * 0.5f - wt;
            Prim("BoardFrame", new Vector3(O.x, cy, zw), new Vector3(bw + 0.12f * u, bh + 0.12f * u, 0.06f * u), Mat(new Color(0.62f, 0.63f, 0.66f)));
            Prim("Whiteboard", new Vector3(O.x, cy, zw - 0.04f * u), new Vector3(bw, bh, 0.03f * u), Mat(new Color(0.96f, 0.96f, 0.94f)));
            Prim("Mark", new Vector3(O.x - bw * 0.22f, cy + 0.18f * u, zw - 0.06f * u), new Vector3(bw * 0.3f, 0.03f * u, 0.02f * u), Mat(new Color(0.20f, 0.40f, 0.80f)));
            Prim("Mark", new Vector3(O.x + bw * 0.12f, cy - 0.10f * u, zw - 0.06f * u), new Vector3(bw * 0.35f, 0.03f * u, 0.02f * u), Mat(new Color(0.80f, 0.32f, 0.30f)));
        }

        // จอคอมพิวเตอร์บนโต๊ะ (จอ + ขาตั้ง + คีย์บอร์ด) — ขนาดอ้างจากความสูงโต๊ะ (s) สเกลเดียวกับเฟอร์นิเจอร์
        //   userDir = ทิศที่ผู้ใช้นั่ง (จอหันไปทางนั้น): -1 = ผู้ใช้อยู่ -z / +1 = +z
        private static void MakeMonitor(float x, float z, float topY, float userDir)
        {
            float s = topY;   // ความสูงโต๊ะ = หน่วยอ้างอิงขนาด
            var dark = Mat(new Color(0.17f, 0.17f, 0.21f));
            var screenCol = Mat(new Color(0.22f, 0.40f, 0.62f));
            var kbCol = Mat(new Color(0.74f, 0.75f, 0.78f));
            float back = -userDir * 0.5f * s;     // จอไปด้านหลังโต๊ะ (ไกลผู้ใช้)
            float front = userDir * 0.42f * s;     // คีย์บอร์ดด้านหน้า (ใกล้ผู้ใช้)
            Prim("MonStand", new Vector3(x, topY + 0.20f * s, z + back), new Vector3(0.10f * s, 0.40f * s, 0.10f * s), dark);   // ขาตั้ง
            float scy = topY + 0.62f * s;
            Prim("MonBezel", new Vector3(x, scy, z + back), new Vector3(0.95f * s, 0.52f * s, 0.07f * s), dark);                // เบเซล
            Prim("MonScreen", new Vector3(x, scy, z + back + userDir * 0.045f * s), new Vector3(0.80f * s, 0.40f * s, 0.03f * s), screenCol);   // สกรีน
            Prim("Keyboard", new Vector3(x, topY + 0.05f * s, z + front), new Vector3(0.70f * s, 0.07f * s, 0.30f * s), kbCol); // คีย์บอร์ด
        }

        // โปสเตอร์/แผนที่ติดผนัง (แนบผนัง คุมตำแหน่งเอง ไม่ลอย)
        //   sideWall=true: ผนังซ้าย/ขวา (บางแกน x, faceDir +1 ซ้าย / -1 ขวา)
        //   sideWall=false: ผนังหน้า/หลัง (บางแกน z, faceDir +1 หน้า / -1 หลัง)
        private static void MakePoster(Vector3 pos, float u, float faceDir, bool sideWall = true)
        {
            Vector3 frame = sideWall ? new Vector3(0.06f * u, 1.15f * u, 1.5f * u) : new Vector3(1.5f * u, 1.15f * u, 0.06f * u);
            Vector3 panel = sideWall ? new Vector3(0.03f * u, 1.0f * u, 1.35f * u) : new Vector3(1.35f * u, 1.0f * u, 0.03f * u);
            Vector3 off = sideWall ? new Vector3(faceDir * 0.04f * u, 0f, 0f) : new Vector3(0f, 0f, faceDir * 0.04f * u);
            Prim("PosterFrame", pos, frame, Mat(new Color(0.45f, 0.30f, 0.18f)));
            Prim("Poster", pos + off, panel, Mat(new Color(0.34f, 0.55f, 0.72f)));
        }

        // ชั้นหนังสือทรงสูง สร้างจาก primitive (มีหนังสือสีๆ คุมตำแหน่งเอง ไม่ลอย)
        //   basePos = จุดกึ่งกลางฐานที่ชิดผนังหลัง (พื้น), ตู้ยื่นเข้าห้องทาง -z
        private static void MakeBookshelf(Vector3 basePos, float u, float bw)
        {
            float sh = 2.3f * u, dp = 0.55f * u, th = 0.08f * u;
            var wood = Mat(new Color(0.5f, 0.34f, 0.2f));
            float cz = basePos.z - dp * 0.5f;   // กึ่งกลางความลึก (ยื่นเข้าห้อง)
            Prim("ShelfBack", new Vector3(basePos.x, basePos.y + sh * 0.5f, basePos.z - th * 0.5f), new Vector3(bw, sh, th), wood);
            Prim("ShelfSideL", new Vector3(basePos.x - bw * 0.5f + th * 0.5f, basePos.y + sh * 0.5f, cz), new Vector3(th, sh, dp), wood);
            Prim("ShelfSideR", new Vector3(basePos.x + bw * 0.5f - th * 0.5f, basePos.y + sh * 0.5f, cz), new Vector3(th, sh, dp), wood);
            Prim("ShelfTop", new Vector3(basePos.x, basePos.y + sh - th * 0.5f, cz), new Vector3(bw, th, dp), wood);
            Color[] cols = { new Color(0.75f, 0.28f, 0.22f), new Color(0.24f, 0.46f, 0.7f), new Color(0.32f, 0.6f, 0.36f), new Color(0.86f, 0.72f, 0.32f), new Color(0.56f, 0.36f, 0.66f) };
            for (int s = 0; s < 4; s++)
            {
                float by = basePos.y + 0.4f * u + s * 0.6f * u;
                Prim("ShelfBoard", new Vector3(basePos.x, by, cz), new Vector3(bw - th * 2f, th, dp), wood);
                int nb = 7;
                for (int b = 0; b < nb; b++)
                {
                    float bx = basePos.x + (b - (nb - 1) * 0.5f) * (bw * 0.82f / nb);
                    float bh = (0.3f + 0.07f * ((b + s * 2) % 3)) * u;
                    Prim("Book", new Vector3(bx, by + th * 0.5f + bh * 0.5f, cz), new Vector3(bw * 0.09f, bh, dp * 0.6f), Mat(cols[(b + s) % cols.Length]));
                }
            }
        }

        private static float RandYaw() => (new[] { 0f, 90f, 180f, 270f })[_rng.Next(4)];

        // ต้นไม้กระถาง (ขนาดกลาง ขยายให้ดูเต็ม)
        private static void Plant(Vector3 pos, float scale = 1.6f)
        {
            string[] p = { "cactus_medium_A", "cactus_medium_B" };
            Put(p[_rng.Next(p.Length)], pos, RandYaw(), scale, true);
        }
        // กรอบรูปติดผนัง
        private static void WallFrame(Vector3 pos, float yaw)
        {
            string[] f = { "pictureframe_medium", "pictureframe_small_A", "pictureframe_small_B", "pictureframe_large_B" };
            Put(f[_rng.Next(f.Length)], pos, yaw, 1f, false);
        }

        // ---------- จัดของแต่ละห้อง (แต่ละห้องแต่งไม่เหมือนกัน) ----------
        private static void Furnish(string key, Vector3 O)
        {
            float fw = 12f * _unit, fd = 9f * _unit, wt = 0.3f * _unit, u = _unit;
            switch (key)
            {
                case "Classroom":
                    MakeBlackboard(O, fw, fd, wt, u);   // กระดานดำจริง
                    // โต๊ะครูหน้าห้อง (คืนกลับ)
                    Put("table_medium", new Vector3(O.x, 0f, O.z + fd * 0.5f - 1.6f * u), 180f, 1f, true);
                    Put(_roomChair, new Vector3(O.x, 0f, O.z + fd * 0.5f - 0.8f * u), 180f, 1f, true);
                    Put("book_set", new Vector3(O.x, 0f, O.z + fd * 0.5f - 1.6f * u), 0f, 1f, true, TopOf("table_medium"));   // หนังสือกลางโต๊ะครู
                    // โต๊ะนักเรียน — จัดเป็นแถวเป็นแนว เต็มห้อง
                    int cols = 4, rows = 3;
                    for (int ci = 0; ci < cols; ci++)
                        for (int r = 0; r < rows; r++)
                        {
                            float x = O.x + (ci - (cols - 1) * 0.5f) * 2.2f * u;
                            float z = O.z + 1.2f * u - r * 1.6f * u;   // ชิดกันขึ้น ใกล้หน้าห้อง
                            Put("table_small", new Vector3(x, 0f, z), 0f, 1f, true);
                            Put(_roomChair, new Vector3(x, 0f, z - 0.55f * u), 0f, 1f, true);   // ชิดโต๊ะขึ้น
                        }
                    // ตู้ cabinet_medium เรียงผนังขวา
                    for (int k = -1; k <= 1; k++)
                        Put("cabinet_medium", new Vector3(O.x + fw * 0.5f - 0.7f * u, 0f, O.z - 0.5f * u - k * 2f * u), 270f, 1f, true);
                    Plant(new Vector3(O.x - fw * 0.44f, 0f, O.z - fd * 0.40f));
                    Plant(new Vector3(O.x + fw * 0.44f, 0f, O.z - fd * 0.40f));
                    MakePoster(new Vector3(O.x - fw * 0.5f + wt, 1.35f * u, O.z - 1.5f * u), u, 1f);   // โปสเตอร์/แผนที่ ผนังซ้าย (ฝั่งตรงข้ามล็อกเกอร์)
                    break;

                case "Library":
                    // ชั้นหนังสือทรงสูง (สร้างเอง มีหนังสือสีๆ ไม่ลอย) เรียง 3 ตู้ผนังหลัง
                    for (int i = 0; i < 3; i++)
                        MakeBookshelf(new Vector3(O.x + (i - 1) * 3.7f * u, 0f, O.z + fd * 0.5f - wt), u, 3.4f * u);

                    // โต๊ะอ่านหนังสือ 2 คอลัมน์ × 2 แถว (4 ตัว) + เก้าอี้ 2 ข้าง + หนังสือบนโต๊ะ
                    float[] colX = { O.x - 2.6f * u, O.x + 2.6f * u };
                    for (int cx = 0; cx < 2; cx++)
                        for (int r = 0; r < 2; r++)
                        {
                            float x = colX[cx], z = O.z + 0.6f * u - r * 2.8f * u;
                            Put("table_medium", new Vector3(x, 0f, z), 0f, 1f, true);
                            Put(_roomChair, new Vector3(x - 1.1f * u, 0f, z), 90f, 1f, true);
                            Put(_roomChair, new Vector3(x + 1.1f * u, 0f, z), 270f, 1f, true);
                            Put("book_set", new Vector3(x, 0f, z), 0f, 1f, true, TopOf("table_medium"));
                        }

                    // มุมอ่านหนังสือ (โซฟา + โคมไฟ + พรม) มุมหน้า-ซ้าย
                    Put("armchair", new Vector3(O.x - fw * 0.5f + 1.5f * u, 0f, O.z - fd * 0.5f + 1.8f * u), 45f, 1f, true);
                    Put("lamp_standing", new Vector3(O.x - fw * 0.5f + 0.8f * u, 0f, O.z - fd * 0.5f + 1.0f * u), 0f, 1f, true);
                    Put("rug_oval_A", new Vector3(O.x - fw * 0.5f + 1.7f * u, 0.02f * u, O.z - fd * 0.5f + 1.9f * u), 0f, 1.4f, false);

                    // ต้นไม้มุมหน้า-ขวา + โปสเตอร์ 2 ผนังข้าง (สมดุล)
                    Plant(new Vector3(O.x + fw * 0.42f, 0f, O.z - fd * 0.40f));
                    MakePoster(new Vector3(O.x + fw * 0.5f - wt, 1.5f * u, O.z - 0.5f * u), u, -1f);
                    MakePoster(new Vector3(O.x - fw * 0.5f + wt, 1.5f * u, O.z + 1.8f * u), u, 1f);
                    break;

                case "Cafeteria":
                    // ครัว: เคาน์เตอร์โมดูลาร์ต่อกันชิดผนัง (วัดความกว้างจริงเพื่อวางชิดพอดี)
                    string[] run = { "kitchencounter_straight_A", "kitchencounter_sink", "kitchencounter_straight_A", "kitchencounter_straight_B" };
                    var cSize = NativeSize("kitchencounter_straight_A") * _fScaleR;
                    float mw = cSize.x, md = cSize.z, ch = cSize.y;          // กว้าง/ลึก/สูงเคาน์เตอร์
                    float zBack = O.z + fd * 0.5f - wt - md * 0.5f;
                    float startX = O.x - (run.Length - 1) * 0.5f * mw;
                    int stoveIdx = 2;
                    for (int i = 0; i < run.Length; i++)
                        Put(run[i], new Vector3(startX + i * mw, 0f, zBack), 180f, 1f, true);
                    Put("stove_multi_countertop", new Vector3(startX + stoveIdx * mw, 0f, zBack), 180f, 1f, true, ch);   // ฐานเตาแตะบนเคาน์เตอร์
                    Put("extractorhood", new Vector3(startX + stoveIdx * mw, 0f, O.z + fd * 0.5f - wt), 180f, 1f, true, ch + 0.8f * u);   // แขวนเหนือเตา
                    float frW = NativeSize("fridge_A").x * _fScaleR;
                    Put("fridge_A", new Vector3(startX + (run.Length - 0.5f) * mw + frW * 0.5f, 0f, zBack), 180f, 1f, true);   // ตู้เย็นชิดปลาย
                    Put("menu", new Vector3(O.x, 1.5f * u, O.z + fd * 0.5f - wt), 180f, 1.2f, false);
                    var cafeSpot = new GameObject("CafeCounterSpot");   // จุดสั่งอาหาร (หน้าเคาน์เตอร์)
                    cafeSpot.transform.SetParent(_root);
                    cafeSpot.transform.position = new Vector3(O.x, 0.1f * u, zBack - 1.6f * u);
                    // โซนนั่งกิน: โต๊ะกลมตามมุมห้อง (เว้นกลาง + ทางประตู)
                    float tTop = NativeSize("table_round_A").y * _fScaleR;
                    float chOff = NativeSize("table_round_A").x * _fScaleR * 0.5f + 0.25f * u;
                    float[,] spots = { { -0.32f, -0.30f }, { 0.32f, -0.30f }, { -0.32f, 0.05f }, { 0.32f, 0.05f } };
                    string[] foods = { "food_burger", "food_dinner", "food_dinner", "food_burger" };
                    for (int i = 0; i < 4; i++)
                    {
                        float x = O.x + spots[i, 0] * fw, z = O.z + spots[i, 1] * fd;
                        Put("table_round_A", new Vector3(x, 0f, z), 0f, 1f, true);
                        Put(_roomChair, new Vector3(x - chOff, 0f, z), 90f, 1f, true);
                        Put(_roomChair, new Vector3(x + chOff, 0f, z), 270f, 1f, true);
                        Put(_roomChair, new Vector3(x, 0f, z - chOff), 0f, 1f, true);
                        Put(foods[i], new Vector3(x, 0f, z), 0f, 1f, true, tTop);   // อาหารวางบนโต๊ะพอดี
                    }
                    Put("shelf_B_small", new Vector3(O.x - fw * 0.5f + 0.6f * u, 0f, O.z - 1.3f * u), 90f, 1f, true);   // ชั้นของข้างผนัง
                    // ของบนเคาน์เตอร์ + ต้นไม้
                    Put("pot_A", new Vector3(startX + 0.2f * mw, 0f, zBack), 0f, 1f, true, ch);
                    Put("dishrack_plates", new Vector3(startX + 1f * mw, 0f, zBack), 180f, 1f, true, ch);
                    Plant(new Vector3(O.x + fw * 0.42f, 0f, O.z - fd * 0.36f));
                    MakePoster(new Vector3(O.x + fw * 0.5f - wt, 1.4f * u, O.z - 1f * u), u, -1f);   // โปสเตอร์ผนังขวา
                    break;

                case "Dorm":
                    // --- ผนังกั้นแบ่งเป็นบล็อค (บูธเตียงซ้าย/ขวา) ---
                    float pt = 0.15f * u, ph = 2.2f * u, partLen = fd * 0.55f;
                    var pWall = Mat(new Color(0.80f, 0.78f, 0.72f));
                    Prim("Partition", new Vector3(O.x, ph * 0.5f, O.z + fd * 0.5f - partLen * 0.5f), new Vector3(pt, ph, partLen), pWall);   // กั้นกลาง (แนว z)
                    Prim("Partition", new Vector3(O.x - 4.3f * u, ph * 0.5f, O.z + fd * 0.5f - partLen), new Vector3(3.4f * u, ph, pt), pWall);   // หน้าบูธซ้าย (เว้นประตูทางกลาง)
                    Prim("Partition", new Vector3(O.x + 4.3f * u, ph * 0.5f, O.z + fd * 0.5f - partLen), new Vector3(3.4f * u, ph, pt), pWall);   // หน้าบูธขวา

                    // --- แต่ละบล็อค: เตียง + หมอน + โต๊ะหัวเตียง + โคมไฟ ---
                    float bedTop = TopOf("bed_single_A");
                    float[] bxD = { O.x - 3f * u, O.x + 3f * u };
                    for (int i = 0; i < 2; i++)
                    {
                        Put("bed_single_A", new Vector3(bxD[i], 0f, O.z + fd * 0.5f - 2f * u), 180f, 1f, true);
                        Put(i == 0 ? "pillow_A" : "pillow_B", new Vector3(bxD[i], 0f, O.z + fd * 0.5f - 0.9f * u), 0f, 1f, true, bedTop);
                        float nx = bxD[i] + (i == 0 ? -1.5f : 1.5f) * u;
                        Put("table_small", new Vector3(nx, 0f, O.z + fd * 0.5f - 1.2f * u), 0f, 1f, true);
                        Put("lamp_table", new Vector3(nx, 0f, O.z + fd * 0.5f - 1.2f * u), 0f, 1f, true, TopOf("table_small"));
                    }

                    // --- โซนกลาง/หน้า (ส่วนรวม): พรม + โต๊ะอ่านหนังสือ + โซฟา + ต้นไม้ + โคมไฟ + โปสเตอร์ ---
                    Put("rug_rectangle_A", new Vector3(O.x, 0.02f * u, O.z - fd * 0.5f + 2f * u), 0f, 1.5f, false);
                    Put("table_small", new Vector3(O.x - 2.5f * u, 0f, O.z - fd * 0.5f + 1.5f * u), 0f, 1f, true);
                    Put(_roomChair, new Vector3(O.x - 2.5f * u, 0f, O.z - fd * 0.5f + 2.5f * u), 180f, 1f, true);
                    Put("book_set", new Vector3(O.x - 2.5f * u, 0f, O.z - fd * 0.5f + 1.5f * u), 0f, 1f, true, TopOf("table_small"));
                    Put("armchair", new Vector3(O.x + 2.5f * u, 0f, O.z - fd * 0.5f + 1.8f * u), 200f, 1f, true);
                    Put("lamp_standing", new Vector3(O.x - fw * 0.5f + 0.7f * u, 0f, O.z - fd * 0.5f + 0.8f * u), 0f, 1f, true);
                    Plant(new Vector3(O.x + fw * 0.5f - 0.7f * u, 0f, O.z - fd * 0.5f + 0.8f * u));
                    MakePoster(new Vector3(O.x - fw * 0.5f + wt, 1.5f * u, O.z - 1f * u), u, 1f);
                    break;

                case "Shop":
                    float shTop = TopOf("table_round_A");
                    float shOff = NativeSize("table_round_A").x * _fScaleR * 0.5f + 0.25f * u;

                    // --- เคาน์เตอร์ขายของจริง (KayKit) ชิดผนังขวา + ของบนเคาน์เตอร์ + จุดกด E ---
                    float ccw = NativeSize("kitchencounter_straight_A").x * _fScaleR;
                    float ctrX = O.x + fw * 0.5f - 0.7f * u;
                    float ctrTop = TopOf("kitchencounter_straight_A");
                    for (int i = 0; i < 2; i++)
                        Put("kitchencounter_straight_A", new Vector3(ctrX, 0f, O.z + 1.4f * u - i * ccw), 270f, 1f, true);
                    float ctrMidZ = O.z + 1.4f * u - 0.5f * ccw;
                    Put("jar_A_small", new Vector3(ctrX, 0f, ctrMidZ + 0.4f * u), 0f, 1f, true, ctrTop);
                    Put("food_burger", new Vector3(ctrX, 0f, ctrMidZ - 0.4f * u), 0f, 1f, true, ctrTop);
                    var shopSpot = new GameObject("ShopCounterSpot");   // จุดให้ ShopStation เกาะ (ฝั่งผู้เล่นยืน)
                    shopSpot.transform.SetParent(_root);
                    shopSpot.transform.position = new Vector3(ctrX - 1.4f * u, 0.1f * u, ctrMidZ);

                    // --- โต๊ะกลม + อาหาร + เก้าอี้ 2 ชุด (โซนนั่งกิน ฝั่งซ้าย) ---
                    float[] shx = { O.x - 3.2f * u, O.x - 0.3f * u };
                    for (int t = 0; t < 2; t++)
                    {
                        float x = shx[t], z = O.z - 0.8f * u;
                        Put("table_round_A", new Vector3(x, 0f, z), 0f, 1f, true);
                        Put("food_dinner", new Vector3(x, 0f, z), 0f, 1f, true, shTop);
                        Put(_roomChair, new Vector3(x - shOff, 0f, z), 90f, 1f, true);
                        Put(_roomChair, new Vector3(x + shOff, 0f, z), 270f, 1f, true);
                        Put(_roomChair, new Vector3(x, 0f, z - shOff), 0f, 1f, true);
                    }

                    // --- ลังผลไม้ 3 ลัง เรียงผนังหลัง (ฝั่งซ้าย) ---
                    string[] shCrates = { "crate_tomatoes", "crate_carrots", "crate_onions" };
                    for (int c = 0; c < 3; c++)
                        Put(shCrates[c], new Vector3(O.x - 3.4f * u + c * 1.6f * u, 0f, O.z + fd * 0.5f - 0.9f * u), 0f, 1f, true);

                    // --- ต้นไม้ 2 มุม + โปสเตอร์ผนังซ้าย ---
                    Plant(new Vector3(O.x - fw * 0.44f, 0f, O.z - fd * 0.40f));
                    Plant(new Vector3(O.x + fw * 0.44f, 0f, O.z - fd * 0.40f));
                    MakePoster(new Vector3(O.x - fw * 0.5f + wt, 1.5f * u, O.z - 0.5f * u), u, 1f);
                    break;

                case "ITLab":   // คณะ IT — ห้องแล็บคอม (โต๊ะเรียน + จอคอมทุกโต๊ะ + ไวท์บอร์ด)
                    MakeWhiteboard(O, fw, fd, wt, u);
                    float labTop = TopOf("table_small");
                    // โต๊ะครู + จอ
                    Put("table_medium", new Vector3(O.x, 0f, O.z + fd * 0.5f - 1.6f * u), 180f, 1f, true);
                    Put(_roomChair, new Vector3(O.x, 0f, O.z + fd * 0.5f - 0.8f * u), 180f, 1f, true);
                    MakeMonitor(O.x, O.z + fd * 0.5f - 1.6f * u, TopOf("table_medium"), 1f);   // จอครู (ครูนั่งเหนือ หันจอเข้าหาครู)
                    // โต๊ะนักเรียน 4×3 + จอทุกตัว
                    for (int ci = 0; ci < 4; ci++)
                        for (int r = 0; r < 3; r++)
                        {
                            float x = O.x + (ci - 1.5f) * 2.2f * u, z = O.z + 1.0f * u - r * 1.7f * u;
                            Put("table_small", new Vector3(x, 0f, z), 0f, 1f, true);
                            Put(_roomChair, new Vector3(x, 0f, z - 0.55f * u), 0f, 1f, true);
                            MakeMonitor(x, z, labTop, -1f);   // นักเรียนนั่งใต้ (-z) จอหันใต้
                        }
                    Plant(new Vector3(O.x - fw * 0.44f, 0f, O.z - fd * 0.40f));
                    Plant(new Vector3(O.x + fw * 0.44f, 0f, O.z - fd * 0.40f));
                    MakePoster(new Vector3(O.x - fw * 0.5f + wt, 1.5f * u, O.z - 1f * u), u, 1f);
                    break;

                case "Office":  // อาคารบริหาร — สำนักงาน (โต๊ะทำงาน + จอคอม + ตู้เอกสาร + โซฟารับรอง)
                    float offTop = TopOf("table_medium");
                    float owHalf = NativeSize("table_medium").x * _fScaleF * 0.5f;   // ครึ่งความกว้างโต๊ะจริง (วางของไม่หลุดขอบ)
                    float[] ofx = { O.x - 2.9f * u, O.x + 2.9f * u };
                    float[] ofz = { O.z + 1.6f * u, O.z - 1.4f * u };
                    for (int a = 0; a < 2; a++)
                        for (int b = 0; b < 2; b++)
                        {
                            float x = ofx[a], z = ofz[b];
                            Put("table_medium", new Vector3(x, 0f, z), 0f, 1f, true);
                            Put(_roomChair, new Vector3(x, 0f, z - 1.0f * u), 0f, 1f, true);   // เก้าอี้ทำงานทางใต้
                            MakeMonitor(x, z, offTop, -1f);
                            Put("book_set", new Vector3(x + owHalf * 0.45f, 0f, z), 0f, 0.8f, true, offTop);   // แฟ้ม/เอกสาร บนโต๊ะ
                        }
                    // ตู้เอกสารเรียงผนังหลัง + โซฟารับรอง + ต้นไม้
                    for (int k = -1; k <= 1; k++)
                        Put("cabinet_medium", new Vector3(O.x + k * 2.4f * u, 0f, O.z + fd * 0.5f - 0.7f * u), 0f, 1f, true);
                    Put("couch", new Vector3(O.x, 0f, O.z - fd * 0.5f + 1.4f * u), 0f, 1f, true);
                    Plant(new Vector3(O.x - fw * 0.44f, 0f, O.z - fd * 0.40f));
                    Plant(new Vector3(O.x + fw * 0.44f, 0f, O.z - fd * 0.40f));
                    MakePoster(new Vector3(O.x + fw * 0.5f - wt, 1.5f * u, O.z + 0.2f * u), u, -1f);
                    break;

                case "ClubRoom":   // อาคารชมรม — ห้องประชุมชมรม (โต๊ะกลมประชุม + เก้าอี้ล้อม + ไวท์บอร์ด + โซฟา)
                    MakeWhiteboard(O, fw, fd, wt, u);
                    float clOff = NativeSize("table_round_A").x * _fScaleR * 0.5f + 0.25f * u;
                    for (int m = 0; m < 2; m++)
                    {
                        float z = O.z + 1.0f * u - m * 2.9f * u;
                        Put("table_round_A", new Vector3(O.x, 0f, z), 0f, 1f, true);
                        Put(_roomChair, new Vector3(O.x - clOff, 0f, z), 90f, 1f, true);
                        Put(_roomChair, new Vector3(O.x + clOff, 0f, z), 270f, 1f, true);
                        Put(_roomChair, new Vector3(O.x, 0f, z - clOff), 0f, 1f, true);
                        Put(_roomChair, new Vector3(O.x, 0f, z + clOff), 180f, 1f, true);
                    }
                    Put("couch", new Vector3(O.x - fw * 0.5f + 1.9f * u, 0f, O.z - fd * 0.5f + 1.6f * u), 30f, 1f, true);
                    Put("lamp_standing", new Vector3(O.x + fw * 0.5f - 0.9f * u, 0f, O.z - fd * 0.5f + 1.2f * u), 0f, 1f, true);
                    Plant(new Vector3(O.x + fw * 0.44f, 0f, O.z - fd * 0.40f));
                    MakePoster(new Vector3(O.x - fw * 0.5f + wt, 1.5f * u, O.z - 1f * u), u, 1f);
                    break;
            }
        }

        // ---------- helpers ----------
        private static void Put(string name, Vector3 pos, float yaw, float extraScale, bool ground, float groundY = 0f)
        {
            var model = _lib.GetValueOrDefault(name);
            if (model == null) { Debug.LogWarning($"[Nisit] ไม่พบเฟอร์นิเจอร์ {name}"); return; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(_root);
            float fs = (_restNames != null && _restNames.Contains(name)) ? _fScaleR : _fScaleF;   // ย่อตามชุด
            inst.transform.localScale = Vector3.one * fs * extraScale;
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            inst.transform.position = pos;
            if (ground) { var b = Bounds(inst); inst.transform.position += new Vector3(0f, groundY - b.min.y, 0f); }   // ยกให้ฐานอยู่ที่ groundY
            AddColliders(inst);   // ทำให้เดินทะลุไม่ได้
        }

        // ใส่ MeshCollider ให้ทุก mesh ในเฟอร์นิเจอร์ (เดินชนได้ ไม่ทะลุ)
        private static void AddColliders(GameObject inst)
        {
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
        }

        private static GameObject Prim(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(_root);
            go.transform.localScale = scale; go.transform.position = pos;
            var r = go.GetComponent<Renderer>(); if (mat != null) r.sharedMaterial = mat;
            return go;
        }

        private static Material Mat(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var m = new Material(sh); m.SetColor("_BaseColor", c); m.SetColor("_Color", c); return m;
        }

        private static float Height(GameObject prefab)
        {
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tmp.transform.localScale = Vector3.one; tmp.transform.rotation = Quaternion.identity; tmp.transform.position = Vector3.zero;
            float h = Bounds(tmp).size.y; Object.DestroyImmediate(tmp); return h;
        }

        // ความสูงผิวบน (world) ของเฟอร์นิเจอร์ตามชื่อ (ไว้วางของบนโต๊ะ/ชั้น)
        private static float TopOf(string name)
            => NativeSize(name).y * ((_restNames != null && _restNames.Contains(name)) ? _fScaleR : _fScaleF);

        // ขนาด native (scale 1) ของโมเดลตามชื่อ
        private static Vector3 NativeSize(string name)
        {
            var m = _lib.GetValueOrDefault(name);
            if (m == null) return Vector3.one;
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(m);
            tmp.transform.localScale = Vector3.one; tmp.transform.rotation = Quaternion.identity; tmp.transform.position = Vector3.zero;
            var s = Bounds(tmp).size; Object.DestroyImmediate(tmp); return s;
        }
        private static Bounds Bounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
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
    }
}
#endif
