using UnityEngine;
using UnityEngine.SceneManagement;
using NisitSimulator.Interaction;
using NisitSimulator.TimeSystem;
using NisitSimulator.CameraRig;

namespace NisitSimulator.SaveLoad
{
    // ===== วางตัวละครตอนเข้าฉาก / เกิดที่หอพัก =====
    //   ลำดับตอนเข้าฉากเกม (GameplayBootstrap.Start — หลัง Awake ของทุกระบบ ก่อนเฟรมแรกถูกวาด):
    //     1) SaveManager.ApplyIfPending()  → คืนสถานะ/เวลา (ไม่ย้ายตัวละคร)
    //     2) PlayerSpawnSystem.ResolveInitialSpawn(data)  → ที่เดียวที่วางตัวละคร:
    //          • New Game               → จุดเกิดหอพัก (DormSpawnPoint)
    //          • Continue (ตำแหน่งใช้ได้) → ตำแหน่ง/ทิศ/อาคารเดิมจากเซฟ (ไม่บังคับกลับหอ)
    //          • เซฟเก่าไม่มีตำแหน่ง/ตำแหน่งใช้ไม่ได้ → จุดเกิดหอพักเป็นตำแหน่งสำรอง
    //     3) DayNightCycle.ApplyNow + กล้องวางตรงตัวละครทันที
    //   หลังจากนี้ไม่มีสคริปต์ใดย้ายตัวละครกลับหอเอง (ยกเว้นผู้เล่นตื่นนอน/Dev วาร์ป)
    public static class PlayerSpawnSystem
    {
        public const string DefaultRoomId = "dorm_1";

        // เหตุผลการเกิดล่าสุด (ใช้ใน Dev Panel/ทดสอบ)
        public static string LastSpawnReason { get; private set; } = "";

        public static GameObject Player => GameObject.Find("Player");

        // ---------- เซฟ ----------
        public static void CollectSave(SaveData d)
        {
            var p = Player;
            if (p != null)
            {
                var t = p.transform;
                d.posX = t.position.x; d.posY = t.position.y; d.posZ = t.position.z;
                d.rotY = t.eulerAngles.y;
                d.hasPlayerTransform = true;
            }
            d.sceneName = SceneManager.GetActiveScene().name;

            var im = InteriorManager.Instance;
            if (im != null && im.IsInside)
            {
                d.insideInterior = true;
                d.interiorName = im.InteriorName;
                var r = im.ReturnPosition;
                d.interiorReturnX = r.x; d.interiorReturnY = r.y; d.interiorReturnZ = r.z;
                d.interiorReturnRotY = im.ReturnRotation.eulerAngles.y;
            }
            else { d.insideInterior = false; d.interiorName = ""; }

            var dorm = DormSpawnPoint.Main;
            d.dormRoomId = dorm != null ? dorm.roomId : DefaultRoomId;
        }

        // ---------- เข้าฉาก ----------
        public static void ResolveInitialSpawn(SaveData loaded)
        {
            var p = Player;
            if (p == null) { LastSpawnReason = "ไม่พบ Player"; return; }

            string why = "";
            if (loaded != null && TryRestoreFromSave(p, loaded, out why))
            {
                LastSpawnReason = "restored";
                Debug.Log($"[Spawn] เล่นต่อ: คืนตำแหน่งจากเซฟ {p.transform.position} (ในอาคาร={InteriorManager.Instance != null && InteriorManager.Instance.IsInside})");
            }
            else
            {
                SpawnAtDorm(p, NetworkSlotIndex());
                if (loaded == null) { LastSpawnReason = "new_game_dorm"; Debug.Log("[Spawn] เกมใหม่: เกิดที่หอพัก"); }
                else { LastSpawnReason = "fallback_dorm: " + why; Debug.LogWarning($"[Spawn] ตำแหน่งในเซฟใช้ไม่ได้ ({why}) → ใช้จุดเกิดหอพักแทน (ความคืบหน้าอื่นคืนครบ)"); }
            }

            if (DayNightCycle.Instance != null) DayNightCycle.Instance.ApplyNow();
            IsometricCameraRig.SnapAll();
        }

        // ---------- คืนตำแหน่งจากเซฟ ----------
        public static bool TryRestoreFromSave(GameObject p, SaveData d, out string why)
        {
            why = "";
            if (!string.IsNullOrEmpty(d.sceneName) && d.sceneName != SceneManager.GetActiveScene().name)
            { why = $"เซฟอยู่ฉาก {d.sceneName}"; return false; }

            var pos = new Vector3(d.posX, d.posY, d.posZ);
            if (!IsFinite(pos) || pos == Vector3.zero) { why = "เซฟไม่มีตำแหน่ง"; return false; }
            var rot = Quaternion.Euler(0f, d.hasPlayerTransform ? d.rotY : p.transform.eulerAngles.y, 0f);

            // สถานะอาคาร: เซฟใหม่มีข้อมูลครบ · เซฟเก่าอนุมานจากตำแหน่ง (ห้องภายในวางไว้ไกลจากแผนที่)
            bool inside = false; string iname = ""; Vector3 rpos = Vector3.zero; Quaternion rrot = Quaternion.identity;
            BuildingDoor door;
            if (d.hasPlayerTransform)
            {
                inside = d.insideInterior; iname = d.interiorName ?? "";
                rpos = new Vector3(d.interiorReturnX, d.interiorReturnY, d.interiorReturnZ);
                rrot = Quaternion.Euler(0f, d.interiorReturnRotY, 0f);
                door = inside ? FindDoorForInterior(pos, iname) : null;
            }
            else
            {
                // เซฟเก่า: ถ้าตำแหน่งอยู่ในห้องภายใน (ห่างจากจุดเกิดของห้องนั้นไม่เกิน 20 ม.) → ถือว่าอยู่ในอาคารนั้น
                door = FindDoorForInterior(pos, null);
                if (door != null) { inside = true; iname = door.interiorSpawn.name; }
            }
            if (inside && (!IsFinite(rpos) || rpos == Vector3.zero))
            {
                if (door == null) { why = "อยู่ในอาคารแต่ไม่พบประตูออก"; return false; }
                ExteriorOf(door, out rpos, out rrot);
            }

            if (!IsUsable(p, pos, out why)) return false;

            if (InteriorManager.Instance != null) InteriorManager.Instance.RestoreState(inside, iname, rpos, rrot);
            InteriorManager.Teleport(p.transform, pos, rot);
            return true;
        }

        // ---------- เกิดที่หอพัก ----------
        public static bool SpawnAtDorm(GameObject p, int slot = 0)
        {
            if (p == null) return false;
            var dorm = DormSpawnPoint.Main;
            if (dorm == null) { Debug.LogWarning("[Spawn] ไม่พบ DormSpawnPoint ในฉาก — ตัวละครอยู่ตำแหน่งเดิมของฉาก"); return false; }
            PlaceInDorm(p, dorm.GetSlot(slot), dorm);
            return true;
        }

        // วางตัวละครที่จุดใดจุดหนึ่งในหอพัก (ข้างเตียงตอนตื่น) พร้อมสถานะ "อยู่ในอาคาร" ที่ออกได้จริง
        public static void PlaceInDorm(GameObject p, Transform point, DormSpawnPoint dorm = null)
        {
            if (p == null || point == null) return;
            if (dorm == null) dorm = DormSpawnPoint.Main;
            var pos = GroundSnap(p, point.position);
            var rot = Quaternion.Euler(0f, point.eulerAngles.y, 0f);

            Vector3 exitPos; Quaternion exitRot;
            if (dorm != null && dorm.exteriorExit != null) { exitPos = GroundSnap(p, dorm.exteriorExit.position); exitRot = Quaternion.Euler(0f, dorm.exteriorExit.eulerAngles.y, 0f); }
            else
            {
                var door = dorm != null ? FindDoorByInteriorName(dorm.interiorName) : null;
                if (door != null) ExteriorOf(door, out exitPos, out exitRot);
                else { exitPos = pos; exitRot = rot; }
            }

            var im = InteriorManager.Instance;
            string iname = dorm != null ? dorm.interiorName : "Spawn_หอพัก";
            if (im != null) im.EnterAt(pos, rot, iname, exitPos, exitRot);
            else InteriorManager.Teleport(p.transform, pos, rot);
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.ApplyNow();
        }

        // ---------- ตรวจตำแหน่ง ----------
        // ใช้ได้ = ตัวเลขปกติ · อยู่ในขอบเขตแผนที่ · มีพื้นรองรับใต้เท้า · ไม่จมในกำแพง/เฟอร์นิเจอร์
        public static bool IsUsable(GameObject p, Vector3 pos, out string why)
        {
            why = "";
            if (!IsFinite(pos)) { why = "ตำแหน่งไม่ใช่ตัวเลข"; return false; }
            if (Mathf.Abs(pos.x) > 5000f || Mathf.Abs(pos.z) > 5000f || pos.y < -50f || pos.y > 500f) { why = "อยู่นอกแผนที่"; return false; }
            if (!FindGround(p, pos + Vector3.up * 1.0f, 6f, out _)) { why = "ไม่มีพื้นรองรับ (อาจตกฉาก)"; return false; }

            var cc = p.GetComponent<CharacterController>();
            if (cc != null)
            {
                float s = Mathf.Abs(p.transform.lossyScale.y);
                float r = cc.radius * Mathf.Max(Mathf.Abs(p.transform.lossyScale.x), Mathf.Abs(p.transform.lossyScale.z)) * 0.85f;
                float half = Mathf.Max(0f, cc.height * s * 0.5f - r);
                Vector3 c = pos + cc.center * s + Vector3.up * 0.08f;
                var hits = Physics.OverlapCapsule(c - Vector3.up * half, c + Vector3.up * half, r, ~0, QueryTriggerInteraction.Ignore);
                foreach (var h in hits)
                {
                    if (h == null || h.transform.IsChildOf(p.transform)) continue;
                    why = $"ชนกับ {h.name}"; return false;
                }
            }
            return true;
        }

        // หาพื้นใต้จุด (ข้ามคอลลิเดอร์ของตัวละครเองและ trigger)
        public static bool FindGround(GameObject p, Vector3 from, float dist, out RaycastHit ground)
        {
            ground = default;
            float best = float.MaxValue; bool found = false;
            foreach (var h in Physics.RaycastAll(from, Vector3.down, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (p != null && h.transform.IsChildOf(p.transform)) continue;
                if (h.distance < best) { best = h.distance; ground = h; found = true; }
            }
            return found;
        }

        // วางเท้าให้แตะพื้นพอดี (ไม่จมพื้น/ไม่ลอย)
        public static Vector3 GroundSnap(GameObject p, Vector3 point)
        {
            if (!FindGround(p, point + Vector3.up * 1.5f, 5f, out var g)) return point + Vector3.up * FootOffset(p);
            return new Vector3(point.x, g.point.y + FootOffset(p), point.z);
        }

        public static float FootOffset(GameObject p)
        {
            var cc = p != null ? p.GetComponent<CharacterController>() : null;
            if (cc == null) return 0f;
            float s = Mathf.Abs(p.transform.lossyScale.y);
            return (cc.height * 0.5f - cc.center.y) * s + cc.skinWidth + 0.01f;
        }

        // ---------- ประตู/อาคาร ----------
        // หา "ประตูด้านนอก" ของห้องภายในที่ตำแหน่งนี้อยู่ (ห้องภายในทุกห้องอยู่ห่างจากแผนที่หลักมาก)
        static BuildingDoor FindDoorForInterior(Vector3 pos, string interiorName)
        {
            if (!string.IsNullOrEmpty(interiorName))
            {
                var byName = FindDoorByInteriorName(interiorName);
                if (byName != null) return byName;
            }
            BuildingDoor best = null; float bestD = 20f;
            foreach (var d in Object.FindObjectsByType<BuildingDoor>(FindObjectsSortMode.None))
            {
                if (d.interiorSpawn == null) continue;
                var a = d.interiorSpawn.position; a.y = 0f; var b = pos; b.y = 0f;
                float dist = Vector3.Distance(a, b);
                if (dist < bestD) { bestD = dist; best = d; }
            }
            return best;
        }

        public static BuildingDoor FindDoorByInteriorName(string interiorName)
        {
            if (string.IsNullOrEmpty(interiorName)) return null;
            foreach (var d in Object.FindObjectsByType<BuildingDoor>(FindObjectsSortMode.None))
                if (d.interiorSpawn != null && d.interiorSpawn.name == interiorName) return d;
            return null;
        }

        // จุดยืนนอกประตู: ถ้าเป็นหอพักใช้ exteriorExit · อื่น ๆ ใช้ตำแหน่ง marker ประตู (จุดที่ผู้เล่นยืนกด E)
        static void ExteriorOf(BuildingDoor door, out Vector3 pos, out Quaternion rot)
        {
            var dorm = DormSpawnPoint.Main;
            if (dorm != null && dorm.exteriorExit != null && door.interiorSpawn != null && door.interiorSpawn.name == dorm.interiorName)
            {
                pos = dorm.exteriorExit.position; rot = Quaternion.Euler(0f, dorm.exteriorExit.eulerAngles.y, 0f);
            }
            else { pos = door.transform.position; rot = Quaternion.Euler(0f, door.transform.eulerAngles.y, 0f); }
            pos = GroundSnap(Player, pos);
        }

        // ---------- Multiplayer: ผู้เล่นแต่ละเครื่องใช้จุดเกิดคนละจุด (ไม่ซ้อนกัน) ----------
        public static int NetworkSlotIndex()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsClient || !nm.IsConnectedClient) return 0;
            var dorm = DormSpawnPoint.Main;
            int n = dorm != null ? dorm.SlotCount : 1;
            return (int)(nm.LocalClientId % (ulong)Mathf.Max(1, n));
        }

        static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
