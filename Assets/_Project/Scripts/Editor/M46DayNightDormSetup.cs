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

            log.Add(SetupDormBuilding(GameObject.Find("Dorm_Building")?.transform));

            // ---------- 5) เวลาเริ่มเกมใหม่ ----------
            var clock = Object.FindFirstObjectByType<GameClock>();
            if (clock != null && clock.startHour != 7) { Undo.RecordObject(clock, "M46"); clock.startHour = 7; EditorUtility.SetDirty(clock); log.Add("GameClock.startHour = 7 (เกมใหม่เริ่ม 07:00)"); }

            EditorSceneManager.MarkSceneDirty(scene);
            return string.Join("\n", log);
        }

        // Local markers follow the building, including after a generator rebuild.
        public static string SetupDormBuilding(Transform building)
        {
            if (building == null) return "❌ ไม่พบ Dorm_Building — ไม่ย้ายกลับหอเดิม";
            DormBuildingGenerator.EnsureEntranceDoors(building);
            var root = building.Find("DormSpawn");
            if (root == null)
            {
                var existing = GameObject.Find("DormSpawn");
                root = existing != null && existing.scene == building.gameObject.scene
                    ? existing.transform : Child(building, "DormSpawn");
            }
            Undo.SetTransformParent(root, building, "M46 dorm spawn");
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            var sp = Child(root, "DormSpawnPoint");
            var dsp = sp.GetComponent<DormSpawnPoint>() ?? sp.gameObject.AddComponent<DormSpawnPoint>();
            dsp.usesWarpInterior = false;
            dsp.interiorName = "";
            dsp.exteriorExit = null;
            dsp.roomId = "dorm_building_101";
            // At scale .75: spacing >=1.5375m, >=1.29m beyond the front leaf sweep.
            var points = new[] { new Vector3(-1.2f, 0.3f, 2.8f), new Vector3(0.85f, 0.3f, 2.8f),
                                 new Vector3(-1.2f, 0.3f, 5f), new Vector3(0.85f, 0.3f, 5f) };
            var slots = new Transform[3];
            for (int i = 0; i < 4; i++)
            {
                var point = i == 0 ? sp : Child(root, "DormSpawnSlot_" + (i + 1));
                point.localPosition = points[i];
                point.localRotation = Quaternion.identity; // face +Z toward the central corridor
                if (i > 0) slots[i - 1] = point;
            }
            dsp.extraSlots = slots;
            var unusedExit = root.Find("DormExteriorExit");
            if (unusedExit != null) Undo.DestroyObjectImmediate(unusedExit.gameObject);

            var sleepRoot = Child(root, "SleepSpots");
            for (int i = sleepRoot.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(sleepRoot.GetChild(i).gameObject);
            var oldGlobal = GameObject.Find("SleepSpots");
            if (oldGlobal != null && oldGlobal.transform.parent == null) Undo.DestroyObjectImmediate(oldGlobal);
            int layer = LayerMask.NameToLayer("Interactable");
            var wakes = new Transform[4];
            var stations = new List<SleepStation>();
            for (int i = 0; i < 4; i++)
            {
                var room = building.Find("Floor_1/Rooms/Room_10" + (i + 1));
                var bed = room != null ? room.Find("Furniture/Bed_A") : null;
                if (bed == null) continue;
                var wake = Child(sleepRoot, "Wake_10" + (i + 1));
                wake.position = room.TransformPoint(new Vector3(0f, 0f, 4.2f));
                wake.rotation = room.rotation * Quaternion.Euler(0f, 180f, 0f);
                wakes[i] = wake;
                var spot = Child(sleepRoot, "SleepSpot_10" + (i + 1));
                spot.SetPositionAndRotation(bed.position, bed.rotation);
                if (layer >= 0) spot.gameObject.layer = layer;
                var bb = BoundsOf(bed);
                var col = spot.gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = spot.InverseTransformPoint(bb.center);
                // Bed local sizes; collider is only for interaction, furniture stays solid.
                col.size = new Vector3(1.6f, 1.2f, 2.5f);
                var st = spot.gameObject.AddComponent<SleepStation>();
                st.wakePoint = wake;
                stations.Add(st);
            }
            if (stations.Count != 4) throw new System.InvalidOperationException("Dorm_Building: ไม่พบเตียงชั้น 1 ครบ 4 ห้อง");
            foreach (var st in stations)
            {
                // All stations use the same client-slot mapping, so different beds cannot overlap in MP.
                st.extraWakePoints = wakes;
                EditorUtility.SetDirty(st);
            }
            foreach (var door in building.GetComponentsInChildren<NisitSimulator.GEBuilding.GEDoor>(true))
            {
                Undo.RecordObject(door, "M46 closed dorm doors");
                door.startOpen = false;
                if (door.hinge != null) door.hinge.localRotation = Quaternion.identity;
                EditorUtility.SetDirty(door);
                PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            }
            EditorUtility.SetDirty(dsp);
            Physics.SyncTransforms();
            var report = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                var point = dsp.GetSlot(i);
                report.Add(point.name + " " + point.position.ToString("F2") + ": " + CheckPoint(point.position));
            }
            report.Add("จุดนอน DM_Bed ห้อง 101–104: " + stations.Count + " · outside · ประตูเริ่มปิด");
            EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
            return string.Join("\n", report);
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

