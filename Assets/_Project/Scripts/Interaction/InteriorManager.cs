using UnityEngine;

namespace NisitSimulator.Interaction
{
    // จัดการเข้า-ออกอาคาร: เทเลพอร์ตผู้เล่นไป "ฉากภายใน" (ที่วางไว้ไกลๆ) แล้วกลับที่เดิม
    // มีตัวเดียวในฉาก (singleton) — สร้างโดย Nisit -> Build Interior
    public class InteriorManager : MonoBehaviour
    {
        public static InteriorManager Instance;

        private Vector3 _returnPos;
        private Quaternion _returnRot;
        private bool _inside;

        public bool IsInside => _inside;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // เข้าอาคาร: จำตำแหน่งข้างนอกไว้ แล้วย้ายไปจุดเกิดในห้อง
        public void Enter(Transform spawn)
        {
            var player = GameObject.Find("Player");
            if (player == null || spawn == null) return;
            if (!_inside) { _returnPos = player.transform.position; _returnRot = player.transform.rotation; }
            _inside = true;
            Teleport(player.transform, spawn.position, spawn.rotation);
        }

        // ออกจากอาคาร: กลับไปตำแหน่งข้างนอกที่จำไว้
        public void Exit()
        {
            if (!_inside) return;
            _inside = false;
            var player = GameObject.Find("Player");
            if (player == null) return;
            Teleport(player.transform, _returnPos, _returnRot);
        }

        // ย้ายตำแหน่ง (ต้องปิด CharacterController ชั่วคราว ไม่งั้นมันล็อกตำแหน่งไว้)
        private void Teleport(Transform p, Vector3 pos, Quaternion rot)
        {
            var cc = p.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            p.position = pos;
            p.rotation = rot;
            if (cc != null) cc.enabled = true;
        }
    }
}
