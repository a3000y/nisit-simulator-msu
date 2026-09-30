using System;
using UnityEngine;

namespace NisitSimulator.Interaction
{
    // จัดการเข้า-ออกอาคาร: เทเลพอร์ตผู้เล่นไป "ฉากภายใน" (ที่วางไว้ไกลๆ) แล้วกลับที่เดิม
    // มีตัวเดียวในฉาก (singleton) — สร้างโดย Nisit -> Build Interior
    public class InteriorManager : MonoBehaviour
    {
        public static InteriorManager Instance;

        // ยิงเมื่อสถานะ "อยู่ในอาคาร" เปลี่ยน (DayNightCycle ใช้สลับชุดแสงภายใน/ภายนอกทันที)
        public static event Action<bool> OnInsideChanged;

        private Vector3 _returnPos;
        private Quaternion _returnRot;
        private bool _inside;
        private string _interiorName = "";   // ชื่อจุดเกิดในห้อง (เช่น Spawn_หอพัก) — เก็บลงเซฟ

        public bool IsInside => _inside;
        public Vector3 ReturnPosition => _returnPos;
        public Quaternion ReturnRotation => _returnRot;
        public string InteriorName => _inside ? _interiorName : "";

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // เข้าอาคาร: จำตำแหน่งข้างนอกไว้ แล้วย้ายไปจุดเกิดในห้อง
        public void Enter(Transform spawn)
        {
            var player = GameObject.Find("Player");
            if (player == null || spawn == null) return;
            if (!_inside) { _returnPos = player.transform.position; _returnRot = player.transform.rotation; }
            SetInside(true, spawn.name);
            Teleport(player.transform, spawn.position, spawn.rotation);
        }

        // เข้า/อยู่ในอาคารโดยไม่ได้เดินผ่านประตู (เกิดในหอพักตอนเริ่มเกม / ตื่นนอน / วาร์ป Dev)
        //   กำหนดจุดออกเป็นหน้าประตูอาคารนั้นเอง → ประตูออกใช้งานได้ทันที
        public void EnterAt(Vector3 pos, Quaternion rot, string interiorName, Vector3 returnPos, Quaternion returnRot)
        {
            var player = GameObject.Find("Player");
            _returnPos = returnPos; _returnRot = returnRot;
            SetInside(true, interiorName);
            if (player != null) Teleport(player.transform, pos, rot);
        }

        // คืนสถานะจากเซฟ (ไม่ย้ายผู้เล่น — ผู้เรียกย้ายเอง)
        public void RestoreState(bool inside, string interiorName, Vector3 returnPos, Quaternion returnRot)
        {
            _returnPos = returnPos; _returnRot = returnRot;
            SetInside(inside, interiorName);
        }

        // ออกจากอาคาร: กลับไปตำแหน่งข้างนอกที่จำไว้
        public void Exit()
        {
            if (!_inside) return;
            var player = GameObject.Find("Player");
            SetInside(false, "");
            if (player == null) return;
            Teleport(player.transform, _returnPos, _returnRot);
        }

        void SetInside(bool inside, string name)
        {
            bool changed = inside != _inside;
            _inside = inside;
            _interiorName = inside ? (name ?? "") : "";
            if (changed) OnInsideChanged?.Invoke(inside);
        }

        // ย้ายตำแหน่ง (ต้องปิด CharacterController ชั่วคราว ไม่งั้นมันล็อกตำแหน่งไว้) + กล้องตามทันที (ไม่ลากข้ามแผนที่)
        public static void Teleport(Transform p, Vector3 pos, Quaternion rot)
        {
            var cc = p.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            p.position = pos;
            p.rotation = rot;
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
            NisitSimulator.CameraRig.IsometricCameraRig.SnapAll();
        }
    }
}
