using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Academics;

namespace NisitSimulator.Interaction
{
    // ขอบเขตห้องเรียนจริงในฉาก (BoxCollider แบบ trigger ครอบห้อง) — ผูกห้องกับ ClassroomCatalog ด้วย roomId
    //   สร้าง/อัปเดตด้วยเมนู Nisit ▸ Classrooms ▸ Setup Classroom Zones (ห้ามแก้มือ — รันเมนูซ้ำได้)
    //   การเช็ก "อยู่ในห้อง" ใช้คณิตศาสตร์กล่อง (ไม่พึ่ง OnTrigger) → ทำงานแม้ผู้เล่นนั่งอยู่ (CharacterController ปิดตอนนั่ง)
    [RequireComponent(typeof(BoxCollider))]
    public class ClassroomZone : MonoBehaviour
    {
        public string roomId;
        [Tooltip("ชื่อตึกแบบเดิม (คณะ IT / อาคารเรียน / อาคารบริหาร) — ใช้ตอน Multiplayer และจับคู่ประตูวาร์ป")]
        public string legacyBuilding;
        [Tooltip("จุดยืนหน้าห้อง (ด้านใน) สำหรับวาร์ป/นำทาง")]
        public Transform entryPoint;
        [Tooltip("ห้องอยู่ในฉากภายในแบบวาร์ป (ชื่อ Spawn_ ใน Interiors) — ว่าง = ห้องในตึกเดินเข้าได้")]
        public string interiorSpawnName = "";

        static readonly List<ClassroomZone> _all = new List<ClassroomZone>();
        static readonly Dictionary<Transform, string> _spotRoom = new Dictionary<Transform, string>();
        public static IReadOnlyList<ClassroomZone> All => _all;

        BoxCollider _box;

        void OnEnable() { if (!_all.Contains(this)) _all.Add(this); _spotRoom.Clear(); }
        void OnDisable() { _all.Remove(this); _spotRoom.Clear(); }

        // จุดนี้อยู่ในห้องไหม (กล่องตาม transform ของ zone รองรับการหมุน)
        public bool Contains(Vector3 world)
        {
            if (_box == null) _box = GetComponent<BoxCollider>();
            if (_box == null) return false;
            Vector3 local = transform.InverseTransformPoint(world) - _box.center;
            Vector3 h = _box.size * 0.5f;
            return Mathf.Abs(local.x) <= h.x && Mathf.Abs(local.y) <= h.y && Mathf.Abs(local.z) <= h.z;
        }

        public Vector3 EntryPosition => entryPoint != null ? entryPoint.position : transform.TransformPoint(GetComponent<BoxCollider>().center);

        public static ClassroomZone FindAt(Vector3 world)
        {
            foreach (var z in _all) if (z != null && z.isActiveAndEnabled && z.Contains(world)) return z;
            return null;
        }

        public static ClassroomZone Find(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return null;
            foreach (var z in _all) if (z != null && z.roomId == roomId) return z;
            return null;
        }

        // ห้องของที่นั่ง (แคช — ที่นั่งไม่ย้าย) · null = ไม่อยู่ในห้องเรียน
        public static string RoomOfSpot(Transform spot)
        {
            if (spot == null) return null;
            if (_spotRoom.TryGetValue(spot, out var r)) return r;
            var z = FindAt(spot.position);
            r = z != null ? z.roomId : null;
            _spotRoom[spot] = r;
            return r;
        }

        public static ClassroomZone ZoneOfSpot(Transform spot)
        {
            string id = RoomOfSpot(spot);
            return id != null ? Find(id) : null;
        }

        // ที่นั่งเรียนที่ว่างในห้องนี้ (กิจกรรมให้ความรู้ + ยังไม่มีคนนั่ง) — ใกล้ entry ก่อน
        public ActivitySpot FindFreeStudySpot()
        {
            ActivitySpot best = null; float bd = float.MaxValue;
            foreach (var s in Object.FindObjectsByType<ActivitySpot>(FindObjectsSortMode.None))
            {
                if (s == null || !s.isActiveAndEnabled || !s.IsStudySpot || s.IsSeated) continue;
                if (RoomOfSpot(s.transform) != roomId) continue;
                float d = (s.transform.position - EntryPosition).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        public Classroom Data => ClassroomCatalog.LoadDefault().Get(roomId);

        void OnDrawGizmosSelected()
        {
            var b = GetComponent<BoxCollider>(); if (b == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
            Gizmos.DrawCube(b.center, b.size);
        }
    }
}
