using UnityEngine;

namespace NisitSimulator.Interaction
{
    // จุดเกิดของผู้เล่นในหอพัก (ข้างเตียง) — ใส่ไว้ที่ GameObject ชื่อ "DormSpawnPoint"
    //   ทุกระบบ (เริ่มเกมใหม่ / เซฟเก่าไม่มีตำแหน่ง / ตื่นนอน / วาร์ป Dev) อ่านตำแหน่งจากที่นี่ที่เดียว ไม่ฝังพิกัดในสคริปต์
    //   ทิศ forward ของ Transform = ทิศที่ตัวละครหันตอนเกิด (หันไปทางเดิน/ประตูออก)
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
            if (slot <= 0 || extraSlots == null || extraSlots.Length == 0) return transform;
            var t = extraSlots[(slot - 1) % extraSlots.Length];
            return t != null ? t : transform;
        }

        public int SlotCount => 1 + (extraSlots != null ? extraSlots.Length : 0);

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
