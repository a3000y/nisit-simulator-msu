using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using NisitSimulator.GEBuilding;
using NisitSimulator.Interaction;
using NisitSimulator.Player;

namespace NisitSimulator.EditorTools
{
    // ทดสอบผัง มมส ขามเรียง v2 ใน Play Mode ด้วยผู้เล่นเดิม (CharacterController จริง, สูตรเดียวกับ PlayerMovement, 50 Hz)
    //   KRLayoutWalkTest.Run("all" | "routes" | "doors" | "warp" | "ground")
    public static class KRLayoutWalkTest
    {
        static CharacterController cc; static PlayerMovement pm;
        static float speed = 4.62f, gravity = -20f, vy;
        static int airFrames, totalFrames, longAir; static float airRun, maxDrop;
        static StringBuilder log; public static int Pass, Fail;

        static bool Setup()
        {
            var p = GameObject.Find("Player"); if (p == null) return false;
            cc = p.GetComponent<CharacterController>(); pm = p.GetComponent<PlayerMovement>();
            if (pm != null) { speed = pm.runSpeed; gravity = pm.gravity; pm.enabled = false; }
            return cc != null;
        }

        static void Report(bool ok, string msg) { if (ok) Pass++; else Fail++; log.AppendLine((ok ? "PASS " : "FAIL ") + msg); }

        public static void Teleport(Vector3 w)
        {
            float half = cc.height * 0.5f * cc.transform.lossyScale.y;
            cc.enabled = false;
            cc.transform.position = new Vector3(w.x, w.y + half - cc.center.y * cc.transform.lossyScale.y + 0.08f, w.z);
            cc.enabled = true; Physics.SyncTransforms(); vy = -2f;
            for (int i = 0; i < 10; i++) Step(Vector3.zero);
        }

        static float Feet() => cc.bounds.min.y;

        static void Step(Vector3 dir)
        {
            const float dt = 0.02f;
            cc.Move(dir * speed * dt);
            if (cc.isGrounded && vy < 0) vy = -2f;
            vy += gravity * dt;
            cc.Move(Vector3.up * vy * dt);
            totalFrames++;
            if (!cc.isGrounded) { airFrames++; airRun += dt; if (airRun > 0.18f) longAir++; } else airRun = 0f;
        }

        static bool WalkTo(Vector3 target, float maxTime = 60f, float tol = 0.35f)
        {
            float best = float.MaxValue, since = 0f, t = 0f;
            while (t < maxTime)
            {
                var d = target - cc.transform.position; d.y = 0f; float dist = d.magnitude;
                if (dist < tol) return true;
                if (dist < best - 0.02f) { best = dist; since = 0f; } else since += 0.02f;
                if (since > 1.5f) return false;
                float before = Feet();
                Step(d.normalized); t += 0.02f;
                maxDrop = Mathf.Max(maxDrop, before - Feet());
            }
            return false;
        }

        static bool Route(string name, params Vector3[] pts)
        {
            int f0 = totalFrames, a0 = longAir; float len = 0f; var prev = cc.transform.position;
            foreach (var p in pts)
            {
                if (!WalkTo(p))
                {
                    var c = cc.transform.position;
                    var blocker = Physics.OverlapCapsule(c + Vector3.up * 0.2f, c + Vector3.up * 1.0f, 0.42f, ~0, QueryTriggerInteraction.Ignore);
                    string bl = ""; foreach (var b in blocker) if (b.gameObject != cc.gameObject && b.name != "Ground") bl += b.name + ",";
                    Report(false, $"{name}: ติดก่อนถึง ({p.x:F1},{p.z:F1}) ค้างที่ ({c.x:F1},{c.y:F2},{c.z:F1}) ชน [{bl}]");
                    return false;
                }
                var q = cc.transform.position; len += Vector2.Distance(new Vector2(prev.x, prev.z), new Vector2(q.x, q.z)); prev = q;
            }
            float sec = (totalFrames - f0) * 0.02f;
            Report(longAir == a0, $"{name}: ถึงปลายทาง ระยะ {len:F0} ม. วิ่ง {sec:F0} วิ (เดิน ~{len / Mathf.Max(0.1f, pm ? pm.walkSpeed : 2.64f):F0} วิ) · ลอยนาน {longAir - a0} ครั้ง");
            return true;
        }

        static Vector3 P(float x, float z, float y = 0f) => new Vector3(x, y, z);

        static object DetectNearest()
        {
            var pi = cc.GetComponent<PlayerInteraction>();
            typeof(PlayerInteraction).GetMethod("DetectNearest", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pi, null);
            return typeof(PlayerInteraction).GetField("current", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pi);
        }

        public static string Run(string suite)
        {
            log = new StringBuilder(); Pass = Fail = 0; airFrames = totalFrames = longAir = 0; airRun = 0f; maxDrop = 0f;
            if (!Application.isPlaying) return "ต้องอยู่ใน Play Mode";
            if (!Setup()) return "ไม่พบ Player";
            try
            {
                if (suite == "ground" || suite == "all") SuiteGround();
                if (suite == "routes" || suite == "all") SuiteRoutes();
                if (suite == "warp" || suite == "all") SuiteWarpDoors();
                if (suite == "doors" || suite == "all") SuiteBuildingDoors();
            }
            catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); }
            finally { if (pm != null) pm.enabled = true; }
            log.Insert(0, $"[KR {suite}] PASS {Pass} / FAIL {Fail} · frames={totalFrames} airborne={airFrames} longAir={longAir} maxDropPerFrame={maxDrop:F2}\n");
            return log.ToString();
        }

        // ผู้เล่นยืนบนพื้น ณ จุดสำคัญ (ไม่ตก/ไม่จม)
        static void SuiteGround()
        {
            var spots = new Dictionary<string, Vector3>
            {
                { "จุดยืนผู้เล่น (ลานกลาง)", GameObject.Find("Player").transform.position },
                { "หน้าหอพักเดิม (DormExteriorExit)", GameObject.Find("DormSpawn/DormExteriorExit").transform.position },
                { "ทล.2202 กลางวงรอบ", P(-16f, 0f) }, { "ถนนวงรอบ ตะวันออก", P(105f, 0f) }, { "ถนนวงรอบ ใต้", P(0f, -105f) },
                { "ทางม้าลาย ถนนกลางเหนือ", P(11f, 25f) }, { "ทางเท้าหน้า GE", P(30f, 30f) }, { "ทางเท้าหน้า IT", P(22f, -43f) },
                { "ถนนหอพัก", P(-60f, 118f) }, { "สนามฟุตบอล", P(-44f, -56f) },
            };
            foreach (var kv in spots)
            {
                Teleport(new Vector3(kv.Value.x, 0f, kv.Value.z));
                for (int i = 0; i < 25; i++) Step(Vector3.zero);
                float f = Feet();
                Report(cc.isGrounded && Mathf.Abs(f) < 0.12f, $"ยืนบนพื้น: {kv.Key} feetY={f:F3} grounded={cc.isGrounded}");
            }
        }

        static void SuiteRoutes()
        {
            var ge = GameObject.Find("GE_Building").transform; var it = GameObject.Find("IT_Building").transform; var dm = GameObject.Find("Dorm_Building").transform;
            Vector3 geLobby = ge.TransformPoint(new Vector3(0f, 0.45f, 3f));
            Vector3 itLobby = it.TransformPoint(new Vector3(0f, 1.2f, -5.5f));
            Vector3 dmLobby = dm.TransformPoint(new Vector3(0f, 0.3f, 3.0f));
            Vector3 dmFront = dm.TransformPoint(new Vector3(0f, 0f, -2.5f));
            var exit = GameObject.Find("DormSpawn/DormExteriorExit").transform.position;

            Teleport(exit);
            Route("หอพักนิสิต(เดิม) → ถนนหอพัก → ทล.2202 → ทางม้าลาย → ข้ามวงรอบ → ถนนกลางเหนือ → GE โถง",
                P(exit.x, 122.75f), P(-23.25f, 122.75f), P(-23.25f, 112.5f), P(-8.75f, 112.5f), P(-8.75f, 29.75f), P(30f, 29.75f), P(30f, 33.6f), geLobby);
            Route("GE → ข้ามถนนกลางเหนือ → ลานกลาง → ทางม้าลายถนนกลางใต้ → IT โถง",
                P(30f, 33.6f), P(30f, 29.75f), P(11f, 29.75f), P(11f, 20.25f), P(11f, 5f), P(17f, 0f), P(17f, -12f), P(11f, -17f), P(11f, -33.25f), P(11f, -42.75f), P(22f, -42.75f), P(22f, -45.2f), itLobby);
            Route("IT → ทล.2202 (ทางเท้าตะวันออก) → ข้ามวงรอบเหนือ → ทางม้าลาย → หอพักอาคารใหม่ โถง",
                P(22f, -45.2f), P(22f, -42.75f), P(-8.75f, -42.75f), P(-8.75f, 112.5f), P(-23.25f, 112.5f), P(-23.25f, 122.75f), P(dmFront.x, 122.75f), dmFront, dmLobby);
            Teleport(P(PlazaX(), -14f));
            Route("ลานกลาง → ทางม้าลาย ทล.2202 → สำนักศึกษาทั่วไป (อาคารเรียน)",
                P(PlazaX() - 5f, -6f), P(17f, 2f), P(11f, 6f), P(-8.75f, 6f), P(-23.25f, 6f), P(-26.0f, 6f));
            Teleport(P(PlazaX(), -14f));
            Route("ลานกลาง → ทางเดินตะวันออก → ถนน East → คณะฝั่งตะวันออก (HS/MBS)",
                P(PlazaX() + 5f, -12f), P(33f, -17f), P(52.8f, -17f), P(63.2f, -17f), P(81.5f, -17f), P(81.5f, 12f), P(83.0f, 12f));
            Teleport(P(105f, 0f));
            var arc = new List<Vector3>(); for (int k = 0; k <= 12; k++) { float an = k * 7.5f * Mathf.Deg2Rad; arc.Add(P(Mathf.Cos(an) * 105f, Mathf.Sin(an) * 105f)); }
            Route("วิ่งรอบถนนวงรอบ ¼ วง (ตะวันออก→เหนือ)", arc.ToArray());
        }

        static float PlazaX() => KRCampusLayout.PlazaC.x;

        // ประตูวาร์ปอาคารเดิม: PlayerInteraction เจอ + ทางเข้าไม่ถูกบัง
        static void SuiteWarpDoors()
        {
            foreach (Transform d in GameObject.Find("BuildingDoors").transform)
            {
                var stand = d.position + d.forward * 0.8f;   // ยืนหน้าประตู (forward ของ marker ชี้ออกจากอาคาร)
                if (d.name == "Door_คณะ IT") stand = d.position;   // ตู้เข้าเรียนในโถง IT
                Teleport(new Vector3(stand.x, d.position.y, stand.z));
                var cur = DetectNearest();
                bool ok = cur is BuildingDoor && ((Component)cur).name == d.name;
                Report(ok, $"ประตูวาร์ป {d.name}: PlayerInteraction เจอ {(cur == null ? "ไม่มี" : ((Component)cur).name)}");
            }
            var exam = GameObject.Find("ExamPoint");
            if (exam)
            {
                Teleport(exam.transform.position + exam.transform.forward * 0.3f);
                var cur = DetectNearest();
                Report(cur != null && ((Component)cur).gameObject == exam, $"จุดสอบ ExamPoint: PlayerInteraction เจอ {(cur == null ? "ไม่มี" : ((Component)cur).name)}");
            }
        }

        // ประตูภายใน GE + หอพักใหม่ (IT มีชุดทดสอบของตัวเอง)
        static void SuiteBuildingDoors()
        {
            foreach (var bn in new[] { "GE_Building", "Dorm_Building" })
            {
                var doors = GameObject.Find(bn).GetComponentsInChildren<GEDoor>(true);
                int det = 0, block = 0, pass = 0, n = 0; var fails = new List<string>();
                foreach (var d in doors)
                {
                    n++;
                    var trig = d.GetComponent<BoxCollider>(); float cx = trig ? trig.center.x : 0.6f;
                    Vector3 outside = d.transform.TransformPoint(new Vector3(cx, 0, -1.1f)), inside = d.transform.TransformPoint(new Vector3(cx, 0, 1.2f));
                    float fy = d.transform.position.y;
                    SetDoor(d, true); Teleport(new Vector3(outside.x, fy, outside.z));
                    var cur = DetectNearest();
                    if (cur as GEDoor == d) det++; else fails.Add($"{d.name}: เจอ {(cur == null ? "ไม่มี" : ((Component)cur).name)}");
                    SetDoor(d, false); Teleport(new Vector3(outside.x, fy, outside.z));
                    if (!WalkTo(new Vector3(inside.x, fy, inside.z), 3f, 0.3f)) block++; else fails.Add($"{d.name}: ปิดแล้วยังทะลุ");
                    SetDoor(d, true); Teleport(new Vector3(outside.x, fy, outside.z));
                    if (WalkTo(new Vector3(inside.x, fy, inside.z), 5f, 0.3f) && WalkTo(new Vector3(outside.x, fy, outside.z), 5f, 0.3f)) pass++; else fails.Add($"{d.name}: เปิดแล้วผ่านไม่ได้");
                    SetDoor(d, d.startOpen);
                }
                Report(det == n, $"{bn}: PlayerInteraction เจอประตู {det}/{n}");
                Report(block == n, $"{bn}: ปิดแล้วกั้น {block}/{n}");
                Report(pass == n, $"{bn}: เปิดแล้วเดินผ่านไป–กลับ {pass}/{n}");
                for (int i = 0; i < Mathf.Min(8, fails.Count); i++) log.AppendLine("   - " + fails[i]);
            }
        }

        static void SetDoor(GEDoor d, bool open)
        {
            d.SetOpen(open);
            if (d.hinge != null) d.hinge.localRotation = Quaternion.Euler(0, open ? d.openAngle : 0f, 0);
            Physics.SyncTransforms();
        }
    }
}
