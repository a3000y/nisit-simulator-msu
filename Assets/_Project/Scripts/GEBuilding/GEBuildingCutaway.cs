using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NisitSimulator.GEBuilding
{
    // ตัดชั้นบนออกเมื่อผู้เล่นอยู่ในอาคาร GE (สำหรับกล้อง Isometric จากด้านบน)
    //   • ทุก Renderer ที่อยู่สูงกว่าเพดานของชั้นที่ผู้เล่นยืนอยู่ → ShadowsOnly (มองไม่เห็น แต่ยังทอดเงา/มี Collider เหมือนเดิม)
    //   • ออกนอกอาคาร → คืนค่าเดิมทั้งหมด
    //   • ทำงานร่วมกับ WallFader เดิม (WallFader จางผนังที่บังกล้อง ส่วนสคริปต์นี้ซ่อนพื้น/หลังคาชั้นบน)
    // ค่าระยะทั้งหมดเป็นพิกัด "ท้องถิ่น" ของ prefab (ก่อน scale) ตามผังใน GEBuildingGenerator
    public class GEBuildingCutaway : MonoBehaviour
    {
        [Tooltip("ผู้เล่นที่ติดตาม (ว่าง = เป้าของกล้อง IsometricCameraRig หรือวัตถุแท็ก Player)")]
        public Transform player;
        [Header("ขอบเขตอาคาร (พิกัดท้องถิ่น)")]
        public Vector3 localMin = new Vector3(-20.5f, -1f, -0.5f);
        public Vector3 localMax = new Vector3(20.5f, 12f, 14.6f);
        [Header("ผังชั้น (พิกัดท้องถิ่น)")]
        public float firstFloorY = 0.45f;
        public float floorHeight = 3.6f;
        public float slabThickness = 0.25f;
        [Tooltip("นับเป็นชั้นถัดไปก่อนถึงพื้นจริงกี่เมตร (ช่วงปลายบันได)")]
        public float floorLookAhead = 0.4f;
        public float checkInterval = 0.15f;

        struct Entry { public Renderer r; public float localMinY; public ShadowCastingMode original; }
        private readonly List<Entry> entries = new List<Entry>();
        private int currentCut = int.MinValue;
        private float timer;

        public int CurrentCutFloor => currentCut;   // int.MaxValue = แสดงทั้งหมด

        void Awake()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                float ly = transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)).y;
                entries.Add(new Entry { r = r, localMinY = ly, original = r.shadowCastingMode });
            }
        }

        Transform FindPlayer()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var rig = cam.GetComponent<NisitSimulator.CameraRig.IsometricCameraRig>();
                if (rig != null && rig.target != null) return rig.target;
            }
            var fp = FindAnyObjectByType<GEFirstPersonController>();
            if (fp != null) return fp.transform;
            var tagged = GameObject.FindGameObjectWithTag("Player");
            return tagged != null ? tagged.transform : null;
        }

        // ชั้นที่ผู้เล่นอยู่ (0 = ชั้น 1) หรือ -1 ถ้าอยู่นอกอาคาร
        public int PlayerFloor()
        {
            if (player == null) player = FindPlayer();
            if (player == null) return -1;
            float feetY = player.position.y;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null && cc.enabled) feetY = cc.bounds.min.y;
            Vector3 lp = transform.InverseTransformPoint(new Vector3(player.position.x, feetY, player.position.z));
            bool inside = lp.x > localMin.x && lp.x < localMax.x && lp.z > localMin.z && lp.z < localMax.z && lp.y > localMin.y && lp.y < localMax.y;
            if (!inside) return -1;
            return Mathf.Max(0, Mathf.FloorToInt((lp.y - firstFloorY + floorLookAhead) / floorHeight));
        }

        void LateUpdate()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = checkInterval;
            Refresh();
        }

        public void Refresh()
        {
            int floor = PlayerFloor();
            int cut = floor < 0 ? int.MaxValue : floor;
            if (cut == currentCut) return;
            currentCut = cut;
            float cutY = cut == int.MaxValue ? float.MaxValue : firstFloorY + (cut + 1) * floorHeight - slabThickness - 0.05f;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.r == null) continue;
                e.r.shadowCastingMode = e.localMinY >= cutY ? ShadowCastingMode.ShadowsOnly : e.original;
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].r != null) entries[i].r.shadowCastingMode = entries[i].original;
            currentCut = int.MinValue;
        }
    }
}
