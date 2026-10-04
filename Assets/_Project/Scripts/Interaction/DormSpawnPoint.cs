using UnityEngine;

namespace NisitSimulator.Interaction
{
    // จุดเกิดของผู้เล่นในหอพัก (ข้างเตียง) — ใส่ไว้ที่ GameObject ชื่อ "DormSpawnPoint"
    //   ทุกระบบ (เริ่มเกมใหม่ / เซฟเก่าไม่มีตำแหน่ง / ตื่นนอน / วาร์ป Dev) อ่านตำแหน่งจากที่นี่ที่เดียว ไม่ฝังพิกัดในสคริปต์
    //   ทิศ forward ของ Transform = ทิศที่ตัวละครหันตอนเกิด (หันไปทางเดิน/ประตูออก)
    [System.Serializable]
    public class DormRoomSlot
    {
        public string roomPath;
        public string bedPath;
        public string roomId;
        [Tooltip("ตำแหน่งข้างเตียงในพิกัดห้อง (Generator ตรวจพื้น/พื้นที่ก่อนใช้)")]
        public Vector3 roomLocalPoint;
        public Transform room, bed, spawnPoint, wakePoint;
        public SleepStation station;

        public DormRoomSlot CopySettings() => new DormRoomSlot {
            roomPath = roomPath, bedPath = bedPath, roomId = roomId, roomLocalPoint = roomLocalPoint
        };
        public static DormRoomSlot[] Defaults() => new[] {
            Make("101", "A", -0.6f), Make("101", "B", 0.6f),
            Make("102", "A", -0.6f), Make("102", "B", 0.6f)
        };
        static DormRoomSlot Make(string code, string bed, float x) => new DormRoomSlot {
            roomPath = "Floor_1/Rooms/Room_" + code, bedPath = "Furniture/Bed_" + bed,
            roomId = "dorm_building_" + code, roomLocalPoint = new Vector3(x, 0f, 4f)
        };
    }

    public class DormSpawnPoint : MonoBehaviour
    {
        [Tooltip("ใช้ interior วาร์ปสำหรับหอเดิม; ปิดสำหรับ Dorm_Building ที่เดินเข้าได้")]
        public bool usesWarpInterior = true;

        [Tooltip("รหัสห้องพัก (เก็บลงเซฟ เผื่อมีหลายห้อง)")]
        public string roomId = "dorm_1";

        [Tooltip("ชื่อจุดเกิดของห้องภายในที่ InteriorManager ใช้ (ต้องตรงกับ BuildingDoor.interiorSpawn ของหอพัก)")]
        public string interiorName = "Spawn_หอพัก";

        [Tooltip("จุดยืนหน้าประตูหอพักด้านนอก — กดออกจากห้องแล้วไปโผล่ที่นี่")]
        public Transform exteriorExit;

        [Tooltip("จุดเกิดเพิ่มสำหรับ Multiplayer (ผู้เล่นคนที่ 2, 3, ...) ไม่ให้เกิดซ้อนกัน")]
        public Transform[] extraSlots = new Transform[0];

        [Header("ห้องและเตียงต่อช่องผู้เล่น 0–3 (รัน M46 Setup หลังปรับ)")]
        public DormRoomSlot[] roomSlots = new DormRoomSlot[0];

        public DormRoomSlot RoomSlot(int slot)
        {
            if (roomSlots == null || roomSlots.Length == 0) return null;
            return roomSlots[Mathf.Max(0, slot) % roomSlots.Length];
        }
        public string RoomIdFor(int slot) => RoomSlot(slot)?.roomId ?? roomId;
        public SleepStation StationFor(int slot) => RoomSlot(slot)?.station;
        public DormRoomSlot[] CopyRoomSettings()
        {
            if (roomSlots == null || roomSlots.Length == 0) return DormRoomSlot.Defaults();
            var settings = new DormRoomSlot[roomSlots.Length];
            for (int i = 0; i < settings.Length; i++) settings[i] = roomSlots[i].CopySettings();
            return settings;
        }

        public static DormSpawnPoint Main
        {
            get
            {
                if (_main != null) return _main;
                _main = FindFirstObjectByType<DormSpawnPoint>();
                return _main;
            }
        }
        static DormSpawnPoint _main;

        void Awake() { if (_main == null) _main = this; }
        void OnDestroy() { if (_main == this) _main = null; }

        // slot 0 = จุดหลัก (ข้างเตียง) · slot 1.. = จุดเพิ่มวนตามลำดับ
        public Transform GetSlot(int slot)
        {
            var roomPoint = RoomSlot(slot)?.spawnPoint;
            if (roomPoint != null) return roomPoint;
            if (slot <= 0 || extraSlots == null || extraSlots.Length == 0) return transform;
            var t = extraSlots[(slot - 1) % extraSlots.Length];
            return t != null ? t : transform;
        }

        public int SlotCount => roomSlots != null && roomSlots.Length > 0 ? roomSlots.Length : 1 + (extraSlots != null ? extraSlots.Length : 0);

        void OnDrawGizmos()
        {
            DrawMarker(transform, new Color(0.3f, 1f, 0.5f));
            if (extraSlots != null) foreach (var s in extraSlots) if (s != null) DrawMarker(s, new Color(0.3f, 0.7f, 1f));
            if (exteriorExit != null) DrawMarker(exteriorExit, new Color(1f, 0.8f, 0.2f));
        }

        static void DrawMarker(Transform t, Color c)
        {
            Gizmos.color = c;
            Gizmos.DrawWireSphere(t.position + Vector3.up * 0.3f, 0.3f);
            Gizmos.DrawLine(t.position + Vector3.up * 0.3f, t.position + Vector3.up * 0.3f + t.forward * 0.8f);
        }
    }
}
