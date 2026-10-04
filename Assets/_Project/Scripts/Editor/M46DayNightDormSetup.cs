#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Interaction;
using NisitSimulator.TimeSystem;
using NisitSimulator.SaveLoad;

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
        public static string SetupDormBuilding(Transform building, DormRoomSlot[] settings = null, bool validatePoints = true)
        {
            if (building == null) return "❌ ไม่พบ Dorm_Building — ไม่ย้ายกลับหอเดิม";
            var root = building.Find("DormSpawn");
            if (root == null) root = Child(building, "DormSpawn");
            var sp = Child(root, "DormSpawnPoint");
            var dsp = sp.GetComponent<DormSpawnPoint>();
            if (dsp == null) dsp = sp.gameObject.AddComponent<DormSpawnPoint>();
            settings = settings ?? dsp.CopyRoomSettings();
            if (settings.Length != 4) throw new System.InvalidOperationException("ต้องตั้งห้อง/เตียงครบ 4 ช่อง");
            var rooms = new Transform[4]; var beds = new Transform[4];
            var identities = new HashSet<string>();
            for (int i = 0; i < 4; i++)
            {
                rooms[i] = building.Find(settings[i].roomPath);
                beds[i] = rooms[i] != null ? rooms[i].Find(settings[i].bedPath) : null;
                if (beds[i] == null || !identities.Add(settings[i].roomPath + "/" + settings[i].bedPath))
                    throw new System.InvalidOperationException("ห้อง/เตียงช่อง " + i + " หายหรือซ้ำ");
            }
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer < 0) throw new System.InvalidOperationException("ไม่พบ Layer Interactable");
            var cut = building.GetComponent<NisitSimulator.GEBuilding.GEBuildingCutaway>();
            if (cut != null) { cut.hideAssignedRoomObstructions = true; EditorUtility.SetDirty(cut); PrefabUtility.RecordPrefabInstancePropertyModifications(cut); }
            DormBuildingGenerator.EnsureEntranceDoors(building);
            root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; root.localScale = Vector3.one;
            dsp.usesWarpInterior = false; dsp.interiorName = ""; dsp.exteriorExit = null;
            dsp.roomId = settings[0].roomId;
            var oldSleep = root.Find("SleepSpots");
            if (oldSleep != null) Undo.DestroyObjectImmediate(oldSleep.gameObject);
            foreach (var station in building.GetComponentsInChildren<SleepStation>(true))
                if (station.assignedDormSlot >= 0 && station.name == "SleepInteract" &&
                    System.Array.IndexOf(beds, station.transform.parent) < 0) Undo.DestroyObjectImmediate(station.gameObject);

            var player = GameObject.Find("Player");
            var log = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                var room = rooms[i]; var bed = beds[i]; var cfg = settings[i];
                var spawn = Child(room, "DormRoomSpawn_" + (i + 1));
                spawn.localPosition = cfg.roomLocalPoint;
                spawn.localRotation = Quaternion.Euler(0f, 180f, 0f);
                if (validatePoints)
                {
                    Physics.SyncTransforms();
                    var ground = PlayerSpawnSystem.GroundSnap(player, spawn.position);
                    if (!PlayerSpawnSystem.FindGround(player, spawn.position + Vector3.up * 1.5f, 3f, out var hit) ||
                        !hit.transform.IsChildOf(building) || hit.normal.y < 0.9f ||
                        Mathf.Abs(hit.point.y - spawn.position.y) > 0.1f)
                        throw new System.InvalidOperationException("ช่อง " + i + " ต้องอยู่บนพื้นห้อง ไม่ใช่เฟอร์นิเจอร์");
                    spawn.position = hit.point;
                    if (!HasCharacterClearance(player, ground)) throw new System.InvalidOperationException("ช่อง " + i + " ชนเฟอร์นิเจอร์");
                }
                var wake = Child(room, "DormBedWake_" + (i + 1));
                wake.SetPositionAndRotation(spawn.position, spawn.rotation);
                var spot = Child(bed, "SleepInteract");
                spot.localPosition = Vector3.zero; spot.localRotation = Quaternion.identity; spot.localScale = Vector3.one;
                spot.gameObject.layer = layer; spot.gameObject.isStatic = false;
                var col = spot.GetComponent<BoxCollider>();
                if (col == null) col = spot.gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true; col.center = new Vector3(0f, 0.5f, 0f); col.size = new Vector3(1.6f, 1.2f, 2.5f);
                var st = spot.GetComponent<SleepStation>();
                if (st == null) st = spot.gameObject.AddComponent<SleepStation>();
                st.assignedDormSlot = i; st.useBedWakePoint = true; st.wakePoint = wake; st.extraWakePoints = new Transform[0];
                cfg.room = room; cfg.bed = bed; cfg.spawnPoint = spawn; cfg.wakePoint = wake; cfg.station = st;
                EditorUtility.SetDirty(st);
                PrefabUtility.RecordPrefabInstancePropertyModifications(st);
                log.Add("ช่อง " + i + " " + cfg.roomId + " / " + bed.name + " → " + spawn.position.ToString("F3"));
                // Compatibility aliases keep existing scene references usable.
                var alias = i == 0 ? sp : Child(root, "DormSpawnSlot_" + (i + 1));
                alias.SetPositionAndRotation(spawn.position, spawn.rotation);
            }
            dsp.roomSlots = settings;
            dsp.extraSlots = new[] { settings[1].spawnPoint, settings[2].spawnPoint, settings[3].spawnPoint };
            foreach (var door in building.GetComponentsInChildren<NisitSimulator.GEBuilding.GEDoor>(true))
            {
                Undo.RecordObject(door, "M46 closed dorm doors"); door.startOpen = false;
                if (door.hinge != null) door.hinge.localRotation = Quaternion.identity;
                EditorUtility.SetDirty(door); PrefabUtility.RecordPrefabInstancePropertyModifications(door);
            }
            EditorUtility.SetDirty(dsp); PrefabUtility.RecordPrefabInstancePropertyModifications(dsp); Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
            return string.Join("\n", log) + "\nเตียงกดนอน 4 จุด · จุดตื่นเฉพาะเตียง · outside · ประตูเริ่มปิด";
        }

        public static bool HasCharacterClearance(GameObject player, Vector3 pivot)
        {
            var cc = player != null ? player.GetComponent<CharacterController>() : null;
            if (cc == null) return false;
            var scale = player.transform.lossyScale;
            float radius = cc.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float half = Mathf.Max(0f, cc.height * Mathf.Abs(scale.y) * 0.5f - radius);
            Vector3 center = pivot + Vector3.Scale(cc.center, scale);
            foreach (var hit in Physics.OverlapCapsule(center + Vector3.up * half, center - Vector3.up * half,
                radius, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(player.transform)) return false;
            return true;
        }
        static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null)
            {
                // Keep prefab file IDs stable, and remove old scene overrides left by earlier setup versions.
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    var other = parent.GetChild(i);
                    if (other != t && other.name == name) Undo.DestroyObjectImmediate(other.gameObject);
                }
                return t;
            }
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

