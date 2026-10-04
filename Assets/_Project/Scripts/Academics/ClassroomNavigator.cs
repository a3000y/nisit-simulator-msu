using UnityEngine;
using NisitSimulator.Interaction;
using NisitSimulator.Systems;
using NisitSimulator.UI;

namespace NisitSimulator.Academics
{
    // ===== พาไปห้องเรียนของคาบ (เล่นคนเดียว) =====
    //   • ประตูวาร์ป/ตู้เข้าเรียน (Door_คณะ IT = ClassKiosk, Door_อาคารเรียน, Door_อาคารบริหาร) + ClassStation:
    //       มีคาบอยู่ → วาร์ปเข้าห้องของคาบ + นั่งที่ว่างให้ (เริ่มเร่งเวลา) · คาบจะเริ่มใน 30 นาทีเกม → วาร์ปไปหน้าห้อง
    //       ไม่มีคาบ/คาบอยู่ตึกอื่น/multiplayer → ทำงานแบบเดิม
    //   • ปุ่ม "นำทางไปห้องคาบถัดไป" ในแอปลงทะเบียน → เสาแสง (BeaconFX) + แถบภารกิจ (ObjectiveHUD) เดิม
    public static class ClassroomNavigator
    {
        public const float EarlyWindowMinutes = 30f;

        // เรียกจาก BuildingDoor.Interact — คืน true ถ้าจัดการแล้ว (ไม่ต้องวาร์ปแบบเดิม)
        public static bool TryRedirectDoor(BuildingDoor door, GameObject who)
        {
            if (door == null || who == null || !ClassroomRules.IsSinglePlayer) return false;
            string building = door.name.StartsWith("Door_") ? door.name.Substring(5) : door.name;
            return TrySendToClass(who, building, door.interiorSpawn);
        }

        // ClassStation (ตู้เข้าเรียนแบบเดิม) — ไม่จำกัดตึก
        public static bool TryRedirectStation(GameObject who) => ClassroomRules.IsSinglePlayer && TrySendToClass(who, null, null);

        // legacyBuilding = null → คาบไหนก็ได้
        public static bool TrySendToClass(GameObject who, string legacyBuilding, Transform doorSpawn)
        {
            var reg = CourseRegistrar.Instance;
            if (reg == null || !reg.IsActive) return false;
            var clock = Object.FindFirstObjectByType<NisitSimulator.TimeSystem.GameClock>();
            float now = clock != null ? clock.TotalMinutes : 0f;

            // คาบของตึกนี้ที่กำลังเรียน (มาก่อน) หรือจะเริ่มภายใน 30 นาทีเกม — คาบที่อยู่ตึกอื่นไม่เกี่ยว
            if (!FindClassFor(reg, legacyBuilding, now, out var e, out var s, out bool ongoing)) return false;
            var zone = ClassroomZone.Find(s.roomId);
            if (zone == null) { Debug.LogWarning($"[Classroom] ไม่พบ ClassroomZone ของ {s.roomId} ในฉาก — ใช้ประตูแบบเดิม (รัน Nisit ▸ Classrooms ▸ Setup Classroom Zones)"); return false; }

            MovePlayerTo(who, zone, doorSpawn);
            if (ongoing)
            {
                var spot = zone.FindFreeStudySpot();
                if (spot != null) { spot.Interact(who); HUDController.Toast($"เข้าเรียน {e.code} ที่ {CourseRegistrar.RoomText(s)}"); }
                else HUDController.Toast($"มาถึงห้อง {s.roomId} แล้ว — ไม่มีที่นั่งว่าง หาโต๊ะนั่งเอง");
            }
            else HUDController.Toast($"มาถึงหน้าห้อง {s.roomId} — คาบ {e.code} เริ่ม {s.TimeText.Substring(0, 5)} (นั่งโต๊ะรอได้เลย)");
            ClearNavigation();
            return true;
        }

        public static bool FindClassFor(CourseRegistrar reg, string legacyBuilding, float now, out Enrollment enrollment, out ClassSession session, out bool ongoing)
        {
            enrollment = null; session = null; ongoing = false;
            var cat = ClassroomCatalog.LoadDefault();
            float bestStart = float.MaxValue;
            foreach (var e in reg.Service.CurrentEnrollments())
            {
                if (e.graded) continue;
                foreach (var s in reg.Service.SessionsFor(e))
                {
                    if (s.day != reg.SemDay || !s.HasRoom) continue;
                    var room = cat.Get(s.roomId);
                    if (legacyBuilding != null && (room == null || room.legacyBuilding != legacyBuilding)) continue;
                    bool on = now >= s.startMinute && now < s.endMinute;
                    bool soon = s.startMinute > now && s.startMinute - now <= EarlyWindowMinutes;
                    if (on) { enrollment = e; session = s; ongoing = true; return true; }
                    if (soon && s.startMinute < bestStart) { bestStart = s.startMinute; enrollment = e; session = s; }
                }
            }
            return session != null;
        }

        static void MovePlayerTo(GameObject who, ClassroomZone zone, Transform doorSpawn)
        {
            var im = InteriorManager.Instance;
            if (!string.IsNullOrEmpty(zone.interiorSpawnName))
            {
                // ห้องอยู่ในฉากภายในแบบวาร์ป (เช่น อาคารบริหาร) → เข้าอาคารตามระบบเดิมก่อน (จำจุดออก)
                Transform spawn = doorSpawn != null && doorSpawn.name == zone.interiorSpawnName ? doorSpawn : FindSpawn(zone.interiorSpawnName);
                if (im != null && spawn != null) { im.Enter(spawn); return; }
            }
            else if (im != null && im.IsInside) im.Exit();   // อยู่ในห้องวาร์ปอื่นอยู่ → ออกก่อน
            var p = zone.EntryPosition;
            var rot = Quaternion.LookRotation(Flat(zone.transform.TransformPoint(zone.GetComponent<BoxCollider>().center) - p), Vector3.up);
            InteriorManager.Teleport(who.transform, p, rot);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude < 1e-4f ? Vector3.forward : v; }

        static Transform FindSpawn(string name)
        {
            var root = GameObject.Find("Interiors");
            return root != null ? root.transform.Find(name) : null;
        }

        // ---------- นำทาง ----------
        static GameObject beacon;
        static string navRoom;
        static bool usedObjectiveHud;

        public static string NavRoom => navRoom;

        // คืนข้อความผลลัพธ์ (สำหรับ toast/แอป)
        public static string NavigateToNextClass()
        {
            if (!ClassroomRules.IsSinglePlayer) return "การนำทางไปห้องเรียนใช้ได้เฉพาะเล่นคนเดียว";
            var reg = CourseRegistrar.Instance;
            if (reg == null || !reg.IsActive) return "ไม่มีตารางเรียน";
            if (!reg.TryGetNextSession(out var e, out var s, out int day)) return "ไม่มีคาบเรียนที่เหลือในภาคนี้";
            if (!s.HasRoom) return $"คาบ {e.code} ไม่มีข้อมูลห้อง — ไปที่ {s.building}";
            var zone = ClassroomZone.Find(s.roomId);
            if (zone == null) return $"ไม่พบห้อง {s.roomId} ในฉาก";
            Vector3 target = zone.EntryPosition;
            if (!string.IsNullOrEmpty(zone.interiorSpawnName))
            {
                // ห้องในฉากวาร์ป → ชี้ไปที่ประตูอาคารด้านนอก
                var door = GameObject.Find("BuildingDoors/Door_" + zone.legacyBuilding);
                if (door != null) target = door.transform.position;
            }
            SetBeacon(target);
            navRoom = s.roomId;
            string when = day == reg.SemDay ? "วันนี้" : $"วันที่ {day} ของภาค";
            string text = $"ไปห้อง {s.roomId} ({e.code} {when} {s.TimeText.Substring(0, 5)})";
            var hud = Object.FindFirstObjectByType<ObjectiveHUD>(FindObjectsInactive.Include);
            var ev = Object.FindFirstObjectByType<EventManager>();
            bool eventObjective = ev != null && ev.HasObjective;
            usedObjectiveHud = hud != null && !eventObjective;   // มีภารกิจ GoTo ค้าง → ไม่ทับ (ใช้แค่เสาแสง)
            if (usedObjectiveHud) hud.Set(target, text);
            return $"นำทาง: {text} — {CourseRegistrar.RoomText(s)} · เดินตามเสาแสง";
        }

        public static void ClearNavigation()
        {
            if (beacon != null) Object.Destroy(beacon);
            beacon = null;
            if (usedObjectiveHud)
            {
                var ev = Object.FindFirstObjectByType<EventManager>();
                var hud = Object.FindFirstObjectByType<ObjectiveHUD>(FindObjectsInactive.Include);
                if (hud != null && (ev == null || !ev.HasObjective)) hud.Clear();
            }
            usedObjectiveHud = false;
            navRoom = null;
        }

        // เรียกทุกเฟรมจาก ClassNavWatcher: ถึงห้องแล้ว → ล้างป้าย
        public static void Update(Vector3 playerPos)
        {
            if (navRoom == null) return;
            var z = ClassroomZone.Find(navRoom);
            if (z != null && z.Contains(playerPos)) { HUDController.Toast($"ถึงห้อง {navRoom} แล้ว"); ClearNavigation(); }
            else if (beacon == null) ClearNavigation();
        }

        static void SetBeacon(Vector3 pos)
        {
            if (beacon != null) Object.Destroy(beacon);
            beacon = new GameObject("ClassNavBeacon");
            beacon.transform.position = pos;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            mat.color = new Color(0.55f, 0.85f, 1f, 1f);
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(beam.GetComponent<Collider>());
            beam.name = "Beam"; beam.transform.SetParent(beacon.transform, false);
            beam.transform.localPosition = new Vector3(0f, 3f, 0f); beam.transform.localScale = new Vector3(0.25f, 3f, 0.25f);
            beam.GetComponent<Renderer>().sharedMaterial = mat;
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(ring.GetComponent<Collider>());
            ring.name = "Ring"; ring.transform.SetParent(beacon.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.05f, 0f); ring.transform.localScale = new Vector3(1.6f, 0.02f, 1.6f);
            ring.GetComponent<Renderer>().sharedMaterial = mat;
            var fx = beacon.AddComponent<BeaconFX>();
            fx.beam = beam.transform; fx.ring = ring.transform;
            beacon.AddComponent<ClassNavWatcher>();
        }
    }

    // ติดกับเสาแสงนำทาง — ล้างเองเมื่อผู้เล่นเข้าห้อง
    public class ClassNavWatcher : MonoBehaviour
    {
        Transform player;
        void Update()
        {
            if (player == null) { var p = GameObject.Find("Player"); if (p != null) player = p.transform; }
            if (player != null) ClassroomNavigator.Update(player.position);
        }
    }
}
