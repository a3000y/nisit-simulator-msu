#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.Interaction;

namespace NisitSimulator.EditorTools
{
    // 🏫 ผูกห้องเรียนจริงในฉาก 01_Gameplay กับ ClassroomCatalog — รันซ้ำได้ (idempotent) ไม่สร้างซ้ำ ไม่ลบ/เปลี่ยนชื่อของเดิม
    //   • ทุกห้องที่ใช้สอน (ตึก IT / ตึก GE) → ลูก "ClassroomZone" (BoxCollider trigger ครอบห้อง, Ignore Raycast)
    //       + "Entry" (จุดยืนในห้องหน้าประตู) + "ClassSeats/ClassSeat_N" (ActivitySpot บนเก้าอี้นักเรียน, เลเยอร์ Interactable)
    //   • ห้องวาร์ปอาคารบริหาร (ADM-101) → "Interiors/ClassroomZone_ADM-101" (ใช้ที่นั่งเดิม ไม่สร้างเพิ่ม)
    //   • เขียน Resources/Classrooms/ClassroomCatalog.asset (ความจุ = จำนวนที่นั่งจริง)
    //   ใช้: เมนู Nisit ▸ Classrooms ▸ Setup Classroom Zones (เปิดฉาก 01_Gameplay ก่อน) — สำรองฉากลง Scenes/Backups ก่อนแก้
    public static class M47ClassroomBuilder
    {
        public const string CatalogDir = "Assets/_Project/Resources/Classrooms";
        public const string CatalogPath = CatalogDir + "/ClassroomCatalog.asset";
        const string ZoneName = "ClassroomZone";
        const string SeatsName = "ClassSeats";
        const float RoomHeight = 2.6f;
        const float BelowFloor = 0.4f;   // ขยายกล่องลงใต้พื้นเล็กน้อย — ที่นั่ง/เท้าผู้เล่นอยู่ระดับพื้นพอดี
        const float SitLift = 0.15f;   // เท่ากับเก้าอี้ในห้องของ SeatingSetup

        [MenuItem("Nisit/Classrooms/Setup Classroom Zones (ห้องเรียนจริง)")]
        public static void SetupMenu()
        {
            var report = Setup(backup: true, save: true);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Nisit Simulator", report.Length > 1500 ? report.Substring(0, 1500) + "\n…(ดู Console)" : report, "ตกลง");
        }

        // คืนรายงาน · backup = สำรองฉากก่อน · save = บันทึกฉาก
        public static string Setup(bool backup, bool save)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("01_Gameplay.unity")) return "เปิดฉาก 01_Gameplay ก่อน (ตอนนี้: " + scene.path + ")";
            if (EditorApplication.isPlaying) return "ออกจาก Play Mode ก่อน";
            if (backup) BackupScene(scene.path);

            int interact = LayerMask.NameToLayer("Interactable");
            if (interact < 0) return "ไม่มี Layer Interactable";
            float unit = 1.3f;
            var player = GameObject.Find("Player");
            if (player != null) { var r = player.GetComponentInChildren<Renderer>(); if (r != null) unit = r.bounds.size.y; }

            var rooms = ClassroomDefaults.Rooms();
            var byId = new Dictionary<string, Classroom>();
            foreach (var r in rooms) byId[r.roomId] = r;

            // หา Room_ITxyz / Room_GExyz ในฉาก
            var found = new Dictionary<string, Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.parent == null || !t.name.StartsWith("Room_")) continue;
                if (t.parent.name != "Rooms" && t.parent.name != "Classrooms") continue;
                string raw = t.name.Substring(5);               // IT201 / GE101
                if (raw.Length < 5) continue;
                string id = raw.Substring(0, 2) + "-" + raw.Substring(2);
                if (byId.ContainsKey(id)) found[id] = t;
            }

            var log = new System.Text.StringBuilder();
            int zones = 0, seats = 0;
            var missing = new List<string>();
            foreach (var r in rooms)
            {
                if (r.roomId == ClassroomDefaults.AdminOffice)
                {
                    int n = SetupAdminZone(r);
                    if (n < 0) { missing.Add(r.roomId); continue; }
                    r.capacity = n; zones++;
                    log.AppendLine($"  {r.roomId} (ห้องวาร์ป{r.building}) ที่นั่งเดิม {n}");
                    continue;
                }
                if (!found.TryGetValue(r.roomId, out var room)) { missing.Add(r.roomId); continue; }
                int c = SetupRoom(room, r, interact, unit);
                r.capacity = c; zones++; seats += c;
            }

            WriteCatalog(rooms);
            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Nisit] ห้องเรียน: {zones}/{rooms.Count} ห้อง · ที่นั่งเรียนในตึกจริง {seats} ที่ · Catalog → {CatalogPath}");
            if (missing.Count > 0) sb.AppendLine("ไม่พบห้องในฉาก: " + string.Join(", ", missing));
            sb.Append(log);
            return sb.ToString();
        }

        static void BackupScene(string path)
        {
            string dir = "Assets/_Project/Scenes/Backups";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/_Project/Scenes", "Backups");
            string dst = $"{dir}/01_Gameplay_BeforeClassroomZones_{System.DateTime.Now:yyyyMMdd_HHmmss}.unity";
            var s = EditorSceneManager.GetActiveScene();
            if (s.isDirty) EditorSceneManager.SaveScene(s, dst, true);   // สำรองสถานะปัจจุบัน (รวมที่ยังไม่บันทึก)
            else AssetDatabase.CopyAsset(path, dst);
        }

        // ---------- ห้องในตึกเดินเข้าได้ ----------
        static int SetupRoom(Transform room, Classroom data, int interactLayer, float unit)
        {
            var floor = room.Find("FloorFinish");
            Bounds fb = floor != null && floor.TryGetComponent<Renderer>(out var fr) ? fr.bounds : RendererBounds(room);
            float floorY = fb.max.y;

            var zt = room.Find(ZoneName);
            GameObject zgo = zt != null ? zt.gameObject : new GameObject(ZoneName);
            if (zt == null) zgo.transform.SetParent(room, true);
            zgo.layer = 2;   // Ignore Raycast — ไม่โดน raycast ของกล้อง/WallFader และไม่ใช่ Interactable
            zgo.transform.position = new Vector3(fb.center.x, floorY + (RoomHeight - BelowFloor) * 0.5f, fb.center.z);
            zgo.transform.rotation = Quaternion.identity;
            SetWorldScaleOne(zgo.transform);

            var box = zgo.GetComponent<BoxCollider>(); if (box == null) box = zgo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(Mathf.Max(0.5f, fb.size.x - 0.1f), RoomHeight + BelowFloor, Mathf.Max(0.5f, fb.size.z - 0.1f));

            var zone = zgo.GetComponent<ClassroomZone>(); if (zone == null) zone = zgo.AddComponent<ClassroomZone>();
            zone.roomId = data.roomId;
            zone.legacyBuilding = data.legacyBuilding;
            zone.interiorSpawnName = "";

            // จุดยืนในห้องหน้าประตู
            var et = zgo.transform.Find("Entry");
            var entry = et != null ? et.gameObject : new GameObject("Entry");
            entry.transform.SetParent(zgo.transform, true);
            Transform door = null;
            foreach (Transform c in room) if (c.name.StartsWith("Door_")) { door = c; break; }
            Vector3 center = new Vector3(fb.center.x, floorY, fb.center.z);
            Vector3 ep = center;
            if (door != null)
            {
                var dp = new Vector3(door.position.x, floorY, door.position.z);
                var dir = center - dp; dir.y = 0f;
                ep = dp + dir.normalized * Mathf.Min(1.0f, dir.magnitude * 0.6f);
            }
            entry.transform.position = ep + Vector3.up * 0.05f;
            entry.transform.rotation = Quaternion.LookRotation(Flat(center - ep), Vector3.up);
            zone.entryPoint = entry.transform;

            // ที่นั่ง — สร้างใหม่ทุกครั้ง (ลบชุดเดิมของเมนูนี้เท่านั้น)
            var old = zgo.transform.Find(SeatsName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var seatsRoot = new GameObject(SeatsName);
            seatsRoot.transform.SetParent(zgo.transform, false);
            int n = 0;
            foreach (var chair in room.GetComponentsInChildren<Transform>(true))
            {
                if (!IsStudentChair(chair)) continue;
                MakeSeat(seatsRoot.transform, chair, data, interactLayer, unit, n++);
            }
            EditorUtility.SetDirty(zgo);
            return n;
        }

        static bool IsStudentChair(Transform t)
        {
            if (!t.name.Contains("Chair") || t.name.Contains("Teacher")) return false;
            if (t.parent != null && t.parent.name.Contains("Chair")) return false;   // ชิ้นส่วนย่อยของเก้าอี้
            return t.GetComponentInChildren<Renderer>() != null;
        }

        static void MakeSeat(Transform root, Transform chair, Classroom data, int layer, float unit, int index)
        {
            var wb = RendererBounds(chair);
            var marker = new GameObject("ClassSeat_" + index);
            marker.transform.SetParent(root, true);
            marker.transform.position = new Vector3(wb.center.x, wb.min.y, wb.center.z);
            marker.transform.rotation = Quaternion.identity;
            SetWorldScaleOne(marker.transform);
            marker.layer = layer;
            var col = marker.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, wb.size.y * 0.5f, 0f);
            col.size = wb.size + Vector3.one * 0.5f;

            var seat = new GameObject("Seat").transform;
            seat.SetParent(marker.transform, false);
            seat.position = new Vector3(wb.center.x, wb.min.y, wb.center.z);
            seat.rotation = chair.rotation;

            var spot = marker.AddComponent<ActivitySpot>();
            spot.seat = seat;
            spot.sitYOffset = SitLift * unit + 0.5f * wb.size.y;
            switch (data.type)
            {
                case ClassroomType.ComputerLab:
                    spot.activityName = "เรียนคอมพิวเตอร์"; spot.knowledgeChange = 9f; spot.energyChange = -4f; spot.stressChange = 3f; spot.satisfactionChange = 2f; spot.expReward = 6; break;
                case ClassroomType.Seminar:
                    spot.activityName = "เรียนสัมมนา"; spot.knowledgeChange = 8f; spot.energyChange = -4f; spot.stressChange = 3f; spot.satisfactionChange = 1f; spot.expReward = 5; break;
                default:
                    spot.activityName = "เรียน"; spot.knowledgeChange = 8f; spot.energyChange = -4f; spot.stressChange = 3f; spot.satisfactionChange = 1f; spot.expReward = 5; break;
            }
        }

        // ---------- ห้องวาร์ปอาคารบริหาร (ฝึกงาน) ----------
        static int SetupAdminZone(Classroom data)
        {
            var interiors = GameObject.Find("Interiors");
            if (interiors == null) return -1;
            var spawn = interiors.transform.Find("Spawn_" + data.legacyBuilding);
            if (spawn == null) return -1;
            // พื้นห้อง = "Floor" ที่ใกล้จุดเกิดที่สุด
            Transform floor = null; float best = float.MaxValue;
            foreach (Transform c in interiors.transform)
                if (c.name == "Floor") { float d = (c.position - spawn.position).sqrMagnitude; if (d < best) { best = d; floor = c; } }
            if (floor == null) return -1;
            var fb = RendererBounds(floor);

            string name = ZoneName + "_" + data.roomId;
            var zt = interiors.transform.Find(name);
            var zgo = zt != null ? zt.gameObject : new GameObject(name);
            if (zt == null) zgo.transform.SetParent(interiors.transform, true);
            zgo.layer = 2;
            zgo.transform.position = new Vector3(fb.center.x, fb.max.y + (RoomHeight - BelowFloor) * 0.5f, fb.center.z);
            zgo.transform.rotation = Quaternion.identity;
            SetWorldScaleOne(zgo.transform);
            var box = zgo.GetComponent<BoxCollider>(); if (box == null) box = zgo.AddComponent<BoxCollider>();
            box.isTrigger = true; box.center = Vector3.zero;
            box.size = new Vector3(fb.size.x - 0.1f, RoomHeight + BelowFloor, fb.size.z - 0.1f);
            var zone = zgo.GetComponent<ClassroomZone>(); if (zone == null) zone = zgo.AddComponent<ClassroomZone>();
            zone.roomId = data.roomId;
            zone.legacyBuilding = data.legacyBuilding;
            zone.interiorSpawnName = spawn.name;
            zone.entryPoint = spawn;

            int n = 0;
            foreach (var s in Object.FindObjectsByType<ActivitySpot>(FindObjectsSortMode.None))
                if (s.IsStudySpot && zone.Contains(s.transform.position)) n++;
            EditorUtility.SetDirty(zgo);
            return n;
        }

        static void WriteCatalog(List<Classroom> rooms)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources")) AssetDatabase.CreateFolder("Assets/_Project", "Resources");
            if (!AssetDatabase.IsValidFolder(CatalogDir)) AssetDatabase.CreateFolder("Assets/_Project/Resources", "Classrooms");
            var cat = AssetDatabase.LoadAssetAtPath<ClassroomCatalog>(CatalogPath);
            if (cat == null)
            {
                cat = ScriptableObject.CreateInstance<ClassroomCatalog>();
                cat.rooms = rooms;
                AssetDatabase.CreateAsset(cat, CatalogPath);
            }
            else cat.rooms = rooms;
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            ClassroomCatalog.ClearCache();
        }

        // ---------- helpers ----------
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude < 1e-4f ? Vector3.forward : v; }

        static void SetWorldScaleOne(Transform t)
        {
            var p = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(1f / Mathf.Max(1e-4f, p.x), 1f / Mathf.Max(1e-4f, p.y), 1f / Mathf.Max(1e-4f, p.z));
        }

        static Bounds RendererBounds(Transform t)
        {
            var rends = t.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(t.position, Vector3.one * 0.5f);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
    }
}
#endif
