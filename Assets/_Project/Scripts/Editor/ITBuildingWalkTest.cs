using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using NisitSimulator.GEBuilding;
using NisitSimulator.Interaction;
using NisitSimulator.Player;
using G = NisitSimulator.EditorTools.ITBuildingGenerator;

namespace NisitSimulator.EditorTools
{
    // ทดสอบเส้นทางเดินอาคาร IT ใน Play Mode ด้วยผู้เล่นเดิมของ 01_Gameplay
    // ขยับ CharacterController จริงตามสูตรของ PlayerMovement (walkSpeed, gravity, ขยับแนวราบแล้วค่อยแรงโน้มถ่วง) ที่ 50 Hz
    // (Editor ที่อยู่เบื้องหลังจำกัดเฟรม จึงจำลองเป็นลูปในเฟรมเดียว แทนการกดคีย์จริง)
    public static class ITBuildingWalkTest
    {
        static Transform bt;          // IT_Building
        static CharacterController cc;
        static PlayerMovement pm;
        static float speed = 4f, gravity = -20f, vy;
        static int airFrames, totalFrames, longAir;
        static float airRun;
        static StringBuilder log;
        public static int Pass, Fail;

        static bool Setup()
        {
            var b = GameObject.Find("IT_Building");
            var p = GameObject.Find("Player");
            if (b == null || p == null) return false;
            bt = b.transform;
            cc = p.GetComponent<CharacterController>();
            pm = p.GetComponent<PlayerMovement>();
            if (pm != null) { speed = pm.walkSpeed; gravity = pm.gravity; pm.enabled = false; }
            return cc != null;
        }

        static Vector3 W(float x, float z, float yLocal = 0f) => bt.TransformPoint(new Vector3(x, yLocal, z));
        static Vector3 L(Vector3 world) => bt.InverseTransformPoint(world);
        static float FeetLocalY() => L(new Vector3(cc.transform.position.x, cc.bounds.min.y, cc.transform.position.z)).y;
        static Vector2 PosLocal() { var l = L(cc.transform.position); return new Vector2(l.x, l.z); }

        public static void Teleport(float x, float z, float floorLocalY)
        {
            var w = W(x, z, floorLocalY);
            float half = cc.height * 0.5f * cc.transform.lossyScale.y;
            cc.enabled = false;
            cc.transform.position = new Vector3(w.x, w.y + half - cc.center.y * cc.transform.lossyScale.y + 0.08f, w.z);
            cc.enabled = true;
            Physics.SyncTransforms();
            vy = -2f;
            for (int i = 0; i < 10; i++) Step(Vector3.zero);
        }

        static void Step(Vector3 dirWorld)
        {
            const float dt = 0.02f;
            cc.Move(dirWorld * speed * dt);
            if (cc.isGrounded && vy < 0) vy = -2f;
            vy += gravity * dt;
            cc.Move(Vector3.up * vy * dt);
            totalFrames++;
            if (!cc.isGrounded) { airFrames++; airRun += dt; if (airRun > 0.18f) longAir++; } else airRun = 0f;
        }

        // เดินไปจุด (x,z ท้องถิ่น) · คืน true ถ้าถึงภายในระยะ tol
        public static bool WalkTo(float x, float z, float maxTime = 20f, float tol = 0.25f)
        {
            float best = float.MaxValue, sinceBest = 0f, t = 0f;
            while (t < maxTime)
            {
                var target = W(x, z);
                var d = target - cc.transform.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist < tol * bt.lossyScale.x) return true;
                if (dist < best - 0.02f) { best = dist; sinceBest = 0f; } else sinceBest += 0.02f;
                if (sinceBest > 1.5f) return false;
                Step(d.normalized);
                t += 0.02f;
            }
            return false;
        }

        // เดินผ่านชุดจุด
        static bool Route(string name, float[] pts, int expectFloor = -1, float maxTimeEach = 20f)
        {
            for (int i = 0; i < pts.Length; i += 2)
            {
                if (!WalkTo(pts[i], pts[i + 1], maxTimeEach))
                {
                    var pl = PosLocal();
                    Report(false, $"{name}: ติดก่อนถึงจุด ({pts[i]:F1},{pts[i + 1]:F1}) อยู่ที่ ({pl.x:F2},{pl.y:F2}) feetY={FeetLocalY():F2}");
                    return false;
                }
            }
            if (expectFloor >= 0)
            {
                float fy = FeetLocalY(), want = G.FY(expectFloor);
                if (Mathf.Abs(fy - want) > 0.35f) { Report(false, $"{name}: ถึงจุดแต่ระดับเท้า {fy:F2} ไม่ตรงชั้น {expectFloor + 1} ({want:F2})"); return false; }
            }
            Report(true, name);
            return true;
        }

        static void Report(bool ok, string msg)
        {
            if (ok) Pass++; else Fail++;
            log.AppendLine((ok ? "PASS " : "FAIL ") + msg);
        }

        static Vector2 Canon(G.Slot s, float cxCanon, float czCanon)
        {
            float cx = G.SlotCenter(s), cz = G.RoomCenterZ(s);
            return s.front ? new Vector2(cx - cxCanon, cz - czCanon) : new Vector2(cx + cxCanon, cz + czCanon);
        }

        static readonly Dictionary<G.RoomType, float[]> RoomPaths = new Dictionary<G.RoomType, float[]>
        {
            // จุดภายในห้องตามกรอบมาตรฐาน (x ไปทางผนังกระดาน = -, z ไปทางหน้าต่าง = +)
            { G.RoomType.Classroom,   new[] { 2.1f, -2.45f, 2.2f, -0.875f, -1.2f, -0.875f, 2.1f, -2.45f } },
            { G.RoomType.ComputerLab, new[] { 2.1f, -2.6f, -2.0f, -2.6f, -2.0f, 0f, 1.2f, 0f, -2.0f, 0f, -2.0f, -2.6f, 2.1f, -2.6f } },
            { G.RoomType.GroupWork,   new[] { 2.1f, -2.45f, 0f, -1.25f, -2.3f, -1.25f, 2.1f, -2.45f } },
            { G.RoomType.Meeting,     new[] { 2.1f, -2.45f, 0f, -1.4f, -2.35f, -1.4f, -2.35f, 0.6f, -2.35f, -1.4f, 2.1f, -2.45f } },
            { G.RoomType.Faculty,     new[] { 2.1f, -2.45f, 1.0f, -1.5f, 1.0f, 1.1f, 1.0f, -1.5f, 2.1f, -2.45f } },
            { G.RoomType.ProjectLab,  new[] { 2.1f, -2.45f, 0.3f, -1.5f, -2.2f, -1.5f, 2.1f, -2.45f } },
            { G.RoomType.Seminar,     new[] { 2.1f, -2.45f, -1.5f, -2.3f, -1.5f, 0.2f, 1.9f, 0.2f, -1.5f, 0.2f, -1.5f, -2.3f, 2.1f, -2.45f } },
            { G.RoomType.Staff,       new[] { 2.1f, -2.45f, 1.0f, -1.5f, 1.0f, 1.0f, -1.8f, 1.0f, 1.0f, 1.0f, 2.1f, -2.45f } },
        };

        static G.RoomType TypeOf(int floor, int r)
        {
            var f = typeof(G).GetField("FloorRooms", BindingFlags.NonPublic | BindingFlags.Static);
            var arr = (G.RoomType[][])f.GetValue(null);
            return arr[floor][r];
        }

        static void SetDoor(GEDoor d, bool open)
        {
            d.SetOpen(open);
            if (d.hinge != null) d.hinge.localRotation = Quaternion.Euler(0, open ? d.openAngle : 0f, 0);
            Physics.SyncTransforms();
        }

        static object DetectNearest()
        {
            var pi = cc.GetComponent<PlayerInteraction>();
            var m = typeof(PlayerInteraction).GetMethod("DetectNearest", BindingFlags.NonPublic | BindingFlags.Instance);
            m.Invoke(pi, null);
            return typeof(PlayerInteraction).GetField("current", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pi);
        }

        // ---------------------------------------------------------------- suites
        public static string Run(string suite)
        {
            log = new StringBuilder(); Pass = Fail = 0; airFrames = totalFrames = longAir = 0; airRun = 0f;
            if (!Application.isPlaying) return "ต้องอยู่ใน Play Mode";
            if (!Setup()) return "ไม่พบ IT_Building / Player";
            try
            {
                if (suite == "entry" || suite == "all") SuiteEntry();
                if (suite == "corridors" || suite == "all") SuiteCorridors();
                if (suite == "rooms" || suite == "all") SuiteRooms();
                if (suite == "stairs" || suite == "all") SuiteStairs();
                if (suite == "doors" || suite == "all") SuiteDoors();
                if (suite == "guards" || suite == "all") SuiteGuards();
                if (suite == "endtoend" || suite == "all") SuiteEndToEnd();
            }
            catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); }
            finally { if (pm != null) pm.enabled = true; }
            log.Insert(0, $"[{suite}] PASS {Pass} / FAIL {Fail} · frames={totalFrames} airborne={airFrames} longAir(>0.18s)={longAir}\n");
            return log.ToString();
        }

        static void SuiteEntry()
        {
            // จากทางเดินเดิม (Walk_ToGate) → ลาน → บันไดหน้า → โถง → ทางเดินกลาง
            Teleport(0f, -19.0f, 0.05f);
            Route("ลาน→บันไดหน้า→ชาน→โถงต้อนรับ→ทางเดินกลาง", new[] { 0f, -14f, 0f, -11f, 0f, -8.5f, 0f, -4f, 0f, 0f }, 0);
            // ทางลาด
            Teleport(-13.85f, -19.3f, 0.05f);
            Route("ลาน→ทางลาดหักกลับ (2 ช่วง)→ชาน→โถง", new[] { -13.85f, -18.4f, -13.85f, -11.0f, -6.6f, -11.0f, -4.5f, -10.6f, -1.0f, -9.6f, -1.0f, -6f }, 0);
            // ระเบียง IT · MSU หน้าปีก +X
            Teleport(0f, -10.5f, G.B);
            Route("ชานหน้า→ระเบียง IT · MSU→กลับ", new[] { 8f, -10.5f, 14.5f, -10.0f, 8f, -10.5f, 0f, -10.5f }, 0);
            // ทางออกข้าง (ปลายทางเดินชั้น 1) → ทางเดินเดิม Walk_ITNorth
            Teleport(15f, 0f, G.FY(0));
            Route("ทางเดินชั้น 1→ประตูทางออกข้าง→บันไดข้าง 8 ขั้น→ทางเดินเดิม", new[] { 17.3f, 0f, 19.0f, 0f, 21.6f, 0f });
            Route("ทางเดินเดิม→กลับเข้าทางออกข้าง", new[] { 18.4f, 0f, 16.5f, 0f }, 0);
        }

        static void SuiteCorridors()
        {
            for (int f = 0; f < G.Floors; f++)
            {
                Teleport(0f, 0f, G.FY(f));
                Route($"ชั้น {f + 1}: ทางเดินกลางตลอดแนว (ปีกตะวันออก→ตะวันตก)", new[] { -17.2f, 0f, 17.2f, 0f, 0f, 0f }, f);
                if (f == 0)
                    Route("ชั้น 1: โถงต้อนรับ (ประชาสัมพันธ์ ที่นั่งพัก ตู้เข้าเรียน)", new[] { 0f, -1f, 0f, -8.3f, -4.3f, -8.3f, -4.3f, -6.0f, 0f, -6.5f, 1.2f, -4.2f, 4.2f, -4.2f, 4.2f, -1.0f, 0f, 0f }, f);
                else
                    Route($"ชั้น {f + 1}: โถงกลาง/พื้นที่นั่งพัก", new[] { 0f, -1f, 0f, -7.0f, -5.2f, -7.0f, 5.2f, -7.0f, 0f, -7.0f, 0f, 0f }, f);
            }
        }

        static void SuiteRooms()
        {
            for (int f = 0; f < G.Floors; f++)
            {
                for (int r = 0; r < G.RoomSlots.Length; r++)
                {
                    var s = G.RoomSlots[r];
                    var ds = G.DoorSpan(s);
                    float dx = (ds.x + ds.y) / 2;
                    Teleport(dx, 0f, G.FY(f));
                    var pts = new List<float> { dx, s.front ? -1.0f : 1.0f };
                    var path = RoomPaths[TypeOf(f, r)];
                    for (int i = 0; i < path.Length; i += 2) { var c = Canon(s, path[i], path[i + 1]); pts.Add(c.x); pts.Add(c.y); }
                    pts.Add(dx); pts.Add(0f);
                    Route($"ชั้น {f + 1}: เข้า–ออกห้อง {G.RoomCode(f, r)} ({TypeOf(f, r)})", pts.ToArray(), f);
                }
                // ห้องน้ำชาย/หญิง + เข้าห้องส้วม
                for (int k = 0; k < 2; k++)
                {
                    float rx0 = k * 3f, dmx = rx0 + 0.35f + G.DoorW / 2;
                    Teleport(dmx, 0f, G.FY(f));
                    Route($"ชั้น {f + 1}: ห้องน้ำ{(k == 0 ? "ชาย" : "หญิง")} + ห้องส้วม",
                        new[] { dmx, 1.0f, dmx, 2.6f, rx0 + 0.85f, 4.9f, rx0 + 0.8f, 6.5f, rx0 + 0.85f, 4.9f, dmx, 2.6f, dmx, 0f }, f);
                }
            }
        }

        static void StairUpDown(string name, float sx)
        {
            float a = sx - (G.LaneHalf + G.Spine) / 2, b = sx + (G.LaneHalf + G.Spine) / 2;
            Teleport(a, 0.2f, G.FY(0));
            for (int f = 0; f < G.Floors - 1; f++)
                Route($"{name}: ขึ้นชั้น {f + 1}→{f + 2}", new[] { a, 1.0f, a, 4.3f, a, 6.2f, b, 6.2f, b, 2.0f, b, 0.3f, sx, -0.6f, a, -0.6f, a, 0.2f }, f + 1, 25f);
            // ลง
            for (int f = G.Floors - 1; f > 0; f--)
                Route($"{name}: ลงชั้น {f + 1}→{f}", new[] { b, 0.3f, b, 2.0f, b, 6.2f, a, 6.2f, a, 4.3f, a, 1.0f, a, -0.6f, b, -0.6f }, f - 1, 25f);
        }

        static void SuiteStairs()
        {
            StairUpDown("บันไดหลัก", G.MainStairX);
            StairUpDown("บันไดปลายอาคาร", G.EndStairX);
            // วิ่งลงบันได (ความเร็ววิ่ง)
            float keep = speed; if (pm != null) speed = pm.runSpeed;
            float a = G.MainStairX - (G.LaneHalf + G.Spine) / 2, b = G.MainStairX + (G.LaneHalf + G.Spine) / 2;
            Teleport(b, -0.6f, G.FY(3));
            Route("บันไดหลัก: วิ่งลง ชั้น 4→3", new[] { b, 0.3f, b, 2.0f, b, 6.2f, a, 6.2f, a, 4.3f, a, 1.0f, a, -0.6f }, 2, 25f);
            speed = keep;
        }

        static void SuiteDoors()
        {
            var doors = bt.GetComponentsInChildren<GEDoor>(true);
            int detectOk = 0, blockOk = 0, passOk = 0, n = 0;
            var fails = new List<string>();
            foreach (var d in doors)
            {
                n++;
                // ตำแหน่งท้องถิ่นของประตู: root = ขอบบานพับ, แกน X ท้องถิ่น = แนวช่องประตู, +Z = ด้านที่บานเปิดเข้า
                Vector3 mid = d.transform.TransformPoint(new Vector3(G.DoorW / 2, 0, 0));
                Vector3 outside = d.transform.TransformPoint(new Vector3(G.DoorW / 2, 0, -1.1f));
                Vector3 inside = d.transform.TransformPoint(new Vector3(G.DoorW / 2, 0, 1.2f));
                var lo = L(outside); var li = L(inside);
                float fy = L(d.transform.position).y;
                SetDoor(d, true);
                Teleport(lo.x, lo.z, fy);
                var cur = DetectNearest();
                bool det = cur as GEDoor == d;
                if (det) detectOk++; else fails.Add($"{d.name}@{d.transform.parent.name}: PlayerInteraction เจอ {(cur == null ? "ไม่มี" : ((Component)cur).name)}");
                // ปิด → ต้องเดินผ่านไม่ได้
                SetDoor(d, false);
                Teleport(lo.x, lo.z, fy);
                bool passedClosed = WalkTo(li.x, li.z, 3f, 0.3f);
                if (!passedClosed) blockOk++; else fails.Add($"{d.name}@{d.transform.parent.name}: ปิดแล้วยังเดินทะลุได้");
                // เปิด → ต้องผ่านได้ (ไปและกลับ)
                SetDoor(d, true);
                Teleport(lo.x, lo.z, fy);
                bool go = WalkTo(li.x, li.z, 5f, 0.3f) && WalkTo(lo.x, lo.z, 5f, 0.3f);
                if (go) passOk++; else { var p = PosLocal(); fails.Add($"{d.name}@{d.transform.parent.name}: เปิดแล้วเดินผ่านไม่ได้ ค้างที่ ({p.x:F2},{p.y:F2})"); }
            }
            Report(detectOk == n, $"ประตู: PlayerInteraction เดิมตรวจเจอ {detectOk}/{n}");
            Report(blockOk == n, $"ประตู: ปิดแล้วกั้นทาง {blockOk}/{n}");
            Report(passOk == n, $"ประตู: เปิดแล้วเดินผ่านไป–กลับ {passOk}/{n}");
            foreach (var f in fails) log.AppendLine("   - " + f);
            foreach (var d in doors) SetDoor(d, d.startOpen);
            // ตู้เข้าเรียน (Door_คณะ IT เดิม)
            var marker = GameObject.Find("BuildingDoors/Door_คณะ IT");
            if (marker != null)
            {
                var lm = L(marker.transform.position);
                Teleport(lm.x - 0.3f, lm.z, G.FY(0));
                var cur = DetectNearest();
                Report(cur as BuildingDoor != null && ((Component)cur).name == "Door_คณะ IT", $"ตู้เข้าเรียนตามตาราง: PlayerInteraction เจอ {(cur == null ? "ไม่มี" : ((Component)cur).name)} (prompt: {(cur as IInteractable)?.GetPrompt()})");
            }
        }

        static void Guard(string name, float sx, float sz, float floorY, float tx, float tz, System.Func<Vector2, bool> okPos)
        {
            Teleport(sx, sz, floorY);
            WalkTo(tx, tz, 3f, 0.1f);
            // ดันต่ออีก 1 วินาที
            var w = W(tx, tz); var d = w - cc.transform.position; d.y = 0;
            for (int i = 0; i < 50; i++) Step(d.normalized);
            var p = PosLocal(); float fy = FeetLocalY();
            bool ok = okPos(p) && Mathf.Abs(fy - floorY) < 0.3f;
            Report(ok, $"กันตก/กันทะลุ: {name} → หยุดที่ ({p.x:F2},{p.y:F2}) feetY={fy:F2} (พื้น {floorY:F2})");
        }

        static void SuiteGuards()
        {
            float y4 = G.FY(3), y2 = G.FY(1), y3 = G.FY(2);
            float aM = G.MainStairX - (G.LaneHalf + G.Spine) / 2, aE = G.EndStairX - (G.LaneHalf + G.Spine) / 2;
            Guard("ชั้น 4 ช่องเปิดเหนือบันไดหลัก (ราวกันตก)", aM, -0.8f, y4, aM, 3.0f, p => p.y < 1.5f);
            Guard("ชั้น 4 ช่องเปิดเหนือบันไดปลายอาคาร (ราวกันตก)", aE, -0.8f, y4, aE, 3.0f, p => p.y < 1.5f);
            Guard("ชั้น 2 ผนังกระจกโถงกลาง", 0f, -6f, y2, 0f, -11f, p => p.y > -9.0f);
            Guard("ชั้น 4 ผนังกระจกโถงกลาง", 2f, -6f, y4, 2f, -11f, p => p.y > -9.0f);
            Guard("ชั้น 4 หน้าต่างห้อง IT403", 7.4f, -6.9f, y4, 7.4f, -10f, p => p.y > -8.0f);
            Guard("ชั้น 2 หน้าต่างปลายทางเดินตะวันตก", -16.5f, 0f, y2, -21f, 0f, p => p.x > -18.0f);
            Guard("ชั้น 3 หน้าต่างปลายทางเดินตะวันออก", 16.5f, 0f, y3, 21f, 0f, p => p.x < 18.0f);
            Guard("ชานพักบันไดปลายอาคาร → ช่องเปิดราวสีอ่อน (ผนังส้ม)", G.EndStairX, 6.2f, G.FY(1) + 1.8f, 21f, 6.2f, p => p.x < 18.0f);
            Guard("ชานพักบันไดหลัก → หน้าต่างหลัง", G.MainStairX, 6.0f, G.FY(2) + 1.8f, G.MainStairX, 10f, p => p.y < 8.0f);
            Guard("ระเบียง IT · MSU → ราวด้านหน้า (สูง 1.2 ม.)", 10f, -10.8f, G.B, 10f, -14.5f, p => p.y > -12.0f);
            Guard("ชานหน้าทางเข้า → ราวด้านตะวันตก", -5.4f, -9.6f, G.B, -8.5f, -9.6f, p => p.x > -6.1f);
            Guard("บันไดหน้า → ผนังข้าง/ราว", 0f, -13.2f, G.B - 0.6f, 9f, -13.2f, p => p.x < 5.7f);
            Guard("ใต้บันไดชั้น 1 (แผงปิด)", G.MainStairX + 1.5f, -0.8f, G.FY(0), G.MainStairX + 1.5f, 3.5f, p => p.y < 1.5f);
        }

        static void SuiteEndToEnd()
        {
            // ลาน → เข้าตึก → ขึ้นบันไดหลักถึงชั้น 4 → ห้องสัมมนา IT405 → ลงบันไดปลายอาคาร → ออกทางข้าง → กลับลาน
            Teleport(0f, -19.0f, 0.05f);
            float aM = G.MainStairX - (G.LaneHalf + G.Spine) / 2, bM = G.MainStairX + (G.LaneHalf + G.Spine) / 2;
            float aE = G.EndStairX - (G.LaneHalf + G.Spine) / 2, bE = G.EndStairX + (G.LaneHalf + G.Spine) / 2;
            bool ok = Route("E2E: ลาน→โถง→ทางเดิน", new[] { 0f, -12f, 0f, -8f, 0f, -0.3f, aM, -0.3f }, 0);
            for (int f = 0; f < 3 && ok; f++)
                ok = Route($"E2E: บันไดหลัก {f + 1}→{f + 2}", new[] { aM, 1.0f, aM, 4.3f, aM, 6.2f, bM, 6.2f, bM, 2.0f, bM, 0.3f, G.MainStairX, -0.6f, aM, -0.6f }, f + 1, 25f);
            if (ok)
            {
                var s = G.RoomSlots[4]; var ds = G.DoorSpan(s); float dx = (ds.x + ds.y) / 2; var c = Canon(s, -1.5f, 0.2f);
                ok = Route("E2E: ห้องสัมมนา IT405 ชั้น 4", new[] { dx, 0f, dx, 1.0f, dx, 2.6f, Canon(s, -1.5f, -2.3f).x, Canon(s, -1.5f, -2.3f).y, c.x, c.y, Canon(s, -1.5f, -2.3f).x, Canon(s, -1.5f, -2.3f).y, dx, 2.6f, dx, 0f }, 3);
            }
            for (int f = 3; f > 0 && ok; f--)
                ok = Route($"E2E: บันไดปลายอาคาร {f + 1}→{f}", new[] { bE, -0.6f, bE, 0.3f, bE, 2.0f, bE, 6.2f, aE, 6.2f, aE, 4.3f, aE, 1.0f, aE, -0.6f }, f - 1, 25f);
            if (ok) ok = Route("E2E: ทางออกข้าง→ทางเดินเดิม", new[] { 17.3f, 0f, 19.0f, 0f, 21.6f, 0f });
            if (ok) Route("E2E: อ้อมหน้าตึกกลับลาน→บันไดหน้า→โถง", new[] { 22.2f, -2.0f, 19.6f, -6f, 18.0f, -14.3f, 13f, -17.0f, 0f, -17.0f, 0f, -13f, 0f, -8.5f, 0f, -2f }, 0);
        }
    }
}
