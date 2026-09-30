#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Interaction;
using NisitSimulator.TimeSystem;

namespace NisitSimulator.EditorTools
{
    // 🌗 M46: ติดตั้งระบบกลางวัน–กลางคืน + จุดเกิดหอพัก + เตียงนอน ในฉาก 01_Gameplay (รันซ้ำได้ ไม่สร้างของซ้ำ)
    //   • DayNightCycle (ใช้ Directional Light เดิม · เก็บค่าแสงเดิมของฉากเป็นชุด "กลางวัน")
    //   • NightLight (Point Light ไม่มีเงา) บนเสาไฟถนน SM_Prop_LightPole_Base ทุกต้น — เปิดเฉพาะกลางคืน
    //   • DormSpawn/DormSpawnPoint (ข้างเตียง หันไปทางประตู) + จุดเกิดเพิ่มสำหรับ Multiplayer + จุดออกหน้าประตูหอพัก
    //   • DormSpawn/SleepSpots: จุดนอนที่เตียงในหอพัก (SleepStation) พร้อมจุดตื่นข้างเตียง
    //   • GameClock.startHour = 7 (เกมใหม่เริ่ม 07:00)
    // ใช้: เมนู Nisit -> Setup Day-Night + Dorm Spawn (M46)
    public static class M46DayNightDormSetup
    {
        const string DormInterior = "Spawn_หอพัก";

        [MenuItem("Nisit/Setup Day-Night + Dorm Spawn (M46)", false, 60)]
        public static void SetupMenu()
        {
            string report = Setup();
            EditorUtility.DisplayDialog("Nisit — M46", report + "\n\nกด Ctrl+S แล้ว Play", "OK");
        }

        public static string Setup()
        {
            var log = new List<string>();
            var scene = EditorSceneManager.GetActiveScene();

            // ---------- 1) DayNightCycle ----------
            var sun = RenderSettings.sun;
            if (sun == null) { var dl = GameObject.Find("Directional Light"); if (dl != null) sun = dl.GetComponent<Light>(); }
            var dn = Object.FindFirstObjectByType<DayNightCycle>();
            if (dn == null)
            {
                var go = new GameObject("DayNightCycle");
                Undo.RegisterCreatedObjectUndo(go, "M46");
                dn = go.AddComponent<DayNightCycle>();
                log.Add("สร้าง DayNightCycle");
            }
            dn.sun = sun;
            dn.clock = Object.FindFirstObjectByType<GameClock>();
            if (!dn.dayCapturedFromScene)
            {
                dn.day = DayNightCycle.CaptureFromScene(sun);   // รักษาแสงกลางวันเดิมของฉาก
                dn.dayCapturedFromScene = true;
                log.Add("เก็บค่าแสงเดิมของฉากเป็นชุด \"กลางวัน\"");
            }
            if (sun != null && sun.lightmapBakeType == LightmapBakeType.Baked) log.Add("⚠ Directional Light เป็น Baked — ควรเป็น Realtime");
            EditorUtility.SetDirty(dn);

            // ---------- 2) ไฟถนน ----------
            int lamps = 0, lampsNew = 0;
            dn.outdoorLights.Clear();
            var props = GameObject.Find("NewCampus/Props");
            if (props != null)
                foreach (var t in props.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith("SM_Prop_LightPole_Base")) continue;
                    lamps++;
                    var existing = t.Find("NightLight");
                    Light l;
                    if (existing == null)
                    {
                        var rs = t.GetComponentsInChildren<Renderer>();
                        if (rs.Length == 0) continue;
                        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                        var lg = new GameObject("NightLight");
                        Undo.RegisterCreatedObjectUndo(lg, "M46");
                        lg.transform.SetParent(t, false);
                        lg.transform.position = new Vector3(b.center.x, b.max.y - 0.35f, b.center.z);
                        l = lg.AddComponent<Light>();
                        l.type = LightType.Point;
                        l.color = new Color(1f, 0.84f, 0.62f);
                        l.intensity = 14f;   // URP ใช้ inverse-square: เสาสูง ~4.3 ม. ต้องค่านี้ถึงเห็นวงไฟบนพื้น
                        l.range = 12f;
                        l.shadows = LightShadows.None;
                        l.lightmapBakeType = LightmapBakeType.Realtime;
                        l.enabled = false;   // เปิดโดย DayNightCycle ตอนกลางคืน
                        lampsNew++;
                    }
                    else l = existing.GetComponent<Light>();
                    if (l != null) dn.outdoorLights.Add(l);
                }
            log.Add($"ไฟถนน: {lamps} ต้น (สร้างแสงใหม่ {lampsNew})");

            // ---------- 3) จุดเกิดหอพัก ----------
            var interiors = GameObject.Find("Interiors");
            var dormSpawnRef = interiors != null ? interiors.transform.Find(DormInterior) : null;
            if (dormSpawnRef == null) { log.Add("❌ ไม่พบ Interiors/" + DormInterior + " (ห้องหอพัก) — รัน Build Interiors ก่อน"); return string.Join("\n", log); }
            var room = dormSpawnRef.position;   // จุดกลางห้องฝั่งประตู (1316.8, y, -3.56)

            var root = GameObject.Find("DormSpawn");
            if (root == null) { root = new GameObject("DormSpawn"); Undo.RegisterCreatedObjectUndo(root, "M46"); }

            // เตียงในห้องหอพัก (เรียงซ้าย→ขวา)
            var beds = new List<Transform>();
            foreach (Transform c in interiors.transform)
                if (c.name.StartsWith("bed") && Mathf.Abs(c.position.x - room.x) < 12f && Mathf.Abs(c.position.z - room.z) < 12f) beds.Add(c);
            beds.Sort((a, b) => a.position.x.CompareTo(b.position.x));

            var sp = Child(root.transform, "DormSpawnPoint");
            var dsp = sp.GetComponent<DormSpawnPoint>() ?? sp.gameObject.AddComponent<DormSpawnPoint>();
            dsp.interiorName = DormInterior;
            dsp.roomId = "dorm_1";

            // ข้างเตียง (ฝั่งทางเดิน) หันไปทางประตูห้อง (ทิศใต้ = yaw 180)
            Vector3 SideOfBed(Transform bed)
            {
                var bb = BoundsOf(bed);
                float side = bed.position.x < room.x ? bb.max.x + 0.95f : bb.min.x - 0.95f;   // ด้านที่หันเข้ากลางห้อง
                return new Vector3(side, room.y - 0.13f, bb.center.z - 0.2f);
            }
            if (beds.Count > 0) sp.position = SideOfBed(beds[0]);
            else sp.position = room + new Vector3(0f, -0.13f, 1.0f);
            sp.rotation = Quaternion.Euler(0f, 180f, 0f);

            var slots = new List<Transform>();
            if (beds.Count > 1) { var s1 = Child(root.transform, "DormSpawnSlot_2"); s1.position = SideOfBed(beds[1]); s1.rotation = Quaternion.Euler(0f, 180f, 0f); slots.Add(s1); }
            var s2 = Child(root.transform, "DormSpawnSlot_3"); s2.position = room + new Vector3(-1.2f, -0.13f, 1.4f); s2.rotation = Quaternion.Euler(0f, 180f, 0f); slots.Add(s2);
            var s3 = Child(root.transform, "DormSpawnSlot_4"); s3.position = room + new Vector3(1.2f, -0.13f, 1.4f); s3.rotation = Quaternion.Euler(0f, 180f, 0f); slots.Add(s3);
            dsp.extraSlots = slots.ToArray();

            // จุดออกหน้าประตูหอพักด้านนอก
            BuildingDoor dormDoor = null;
            foreach (var d in Object.FindObjectsByType<BuildingDoor>(FindObjectsSortMode.None))
                if (d.interiorSpawn != null && d.interiorSpawn.name == DormInterior) dormDoor = d;
            if (dormDoor != null)
            {
                var ex = Child(root.transform, "DormExteriorExit");
                ex.position = dormDoor.transform.position + dormDoor.transform.forward * 1.2f;
                ex.rotation = Quaternion.Euler(0f, dormDoor.transform.eulerAngles.y, 0f);
                dsp.exteriorExit = ex;
            }
            else log.Add("⚠ ไม่พบประตูหอพักด้านนอก (BuildingDoor ของ " + DormInterior + ")");

            // ตรวจว่าจุดเกิดไม่อยู่ในกำแพง/เฟอร์นิเจอร์ และมีพื้นรองรับ
            foreach (var t in new List<Transform> { sp }.Concat(slots))
                log.Add($"{t.name} {t.position.ToString("F2")}: {CheckPoint(t.position)}");
            if (dsp.exteriorExit != null) log.Add($"DormExteriorExit {dsp.exteriorExit.position.ToString("F2")}: {CheckPoint(dsp.exteriorExit.position)}");
            EditorUtility.SetDirty(dsp);

            // ---------- 4) เตียงนอน ----------
            int layer = LayerMask.NameToLayer("Interactable");
            var sleepRoot = Child(root.transform, "SleepSpots");
            for (int i = sleepRoot.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(sleepRoot.GetChild(i).gameObject);
            var oldGlobal = GameObject.Find("SleepSpots");   // ของเดิมจาก Nisit/Setup Sleep (ถ้าเคยรัน) — กันเตียงซ้อน 2 จุด
            if (oldGlobal != null && oldGlobal.transform.parent == null) { Undo.DestroyObjectImmediate(oldGlobal); log.Add("ลบ SleepSpots เดิม (แทนด้วย DormSpawn/SleepSpots)"); }
            float unit = 1.3f;
            var player = GameObject.Find("Player");
            if (player != null) { var cc = player.GetComponent<CharacterController>(); if (cc != null) unit = cc.height * Mathf.Abs(player.transform.lossyScale.y); }
            for (int i = 0; i < beds.Count; i++)
            {
                var wb = BoundsOf(beds[i]);
                var m = new GameObject("SleepSpot_" + (i + 1));
                Undo.RegisterCreatedObjectUndo(m, "M46");
                m.transform.SetParent(sleepRoot, false);
                m.transform.position = new Vector3(wb.center.x, room.y - 0.13f, wb.center.z);
                if (layer >= 0) m.layer = layer;
                var col = m.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, wb.extents.y, 0f);
                col.size = wb.size + Vector3.one * (0.5f * unit);
                var st = m.AddComponent<SleepStation>();
                st.wakePoint = i == 0 ? sp : (i - 1 < slots.Count ? slots[i - 1] : sp);
            }
            log.Add($"เตียงนอนในหอพัก: {beds.Count} เตียง");

            // ---------- 5) เวลาเริ่มเกมใหม่ ----------
            var clock = Object.FindFirstObjectByType<GameClock>();
            if (clock != null && clock.startHour != 7) { Undo.RecordObject(clock, "M46"); clock.startHour = 7; EditorUtility.SetDirty(clock); log.Add("GameClock.startHour = 7 (เกมใหม่เริ่ม 07:00)"); }

            EditorSceneManager.MarkSceneDirty(scene);
            return string.Join("\n", log);
        }

        static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "M46");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static Bounds BoundsOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one);
            var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // ตรวจจุด: มีพื้น + แคปซูลขนาดตัวละครไม่ชนอะไร
        static string CheckPoint(Vector3 p)
        {
            bool ground = Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore);
            var ov = Physics.OverlapCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.0f, 0.3f, ~0, QueryTriggerInteraction.Ignore);
            var names = new List<string>();
            foreach (var o in ov) if (o.name != "Player") names.Add(o.name);
            return (ground ? $"พื้น {hit.collider.name}" : "❌ ไม่มีพื้น") + (names.Count == 0 ? " · ไม่ชน" : " · ❌ ชน " + string.Join(",", names));
        }
    }

    static class M46ListExt
    {
        public static List<T> Concat<T>(this List<T> a, List<T> b) { var r = new List<T>(a); r.AddRange(b); return r; }
    }
}
#endif
