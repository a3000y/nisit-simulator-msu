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

        [Tooltip("หอพัก: ซ่อนผนังและต้นไม้ที่บังกล้องขณะอยู่ในห้องที่มีช่องเกิด (Collider ยังอยู่)")]
        public bool hideAssignedRoomObstructions;
        public int HiddenRoomWallCount => roomWalls.Count;
        readonly Dictionary<Renderer, ShadowCastingMode> roomWalls = new Dictionary<Renderer, ShadowCastingMode>();
        readonly List<Renderer> roomOccluders = new List<Renderer>();
        bool roomOccludersCached;

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
            if (cut == currentCut) { RefreshRoomWalls(); return; }
            RestoreRoomWalls();
            currentCut = cut;
            float cutY = cut == int.MaxValue ? float.MaxValue : firstFloorY + (cut + 1) * floorHeight - slabThickness - 0.05f;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.r == null) continue;
                e.r.shadowCastingMode = e.localMinY >= cutY ? ShadowCastingMode.ShadowsOnly : e.original;
            }
            RefreshRoomWalls();
        }

        void RestoreRoomWalls()
        {
            foreach (var pair in roomWalls) if (pair.Key != null) pair.Key.shadowCastingMode = pair.Value;
            roomWalls.Clear();
        }

        void RefreshRoomWalls()
        {
            RestoreRoomWalls();
            if (!hideAssignedRoomObstructions || player == null) return;
            var dorm = GetComponentInChildren<NisitSimulator.Interaction.DormSpawnPoint>();
            if (dorm == null || dorm.roomSlots == null) return;
            bool inRoom = false;
            foreach (var slot in dorm.roomSlots)
            {
                if (slot.room == null) continue;
                var pos = slot.room.InverseTransformPoint(player.position);
                if (Mathf.Abs(pos.x) < 1.8f && pos.z > 0f && pos.z < 6.5f && pos.y > -0.1f && pos.y < floorHeight)
                { inRoom = true; break; }
            }
            var camera = Camera.main;
            if (!inRoom || camera == null) return;
            var cc = player.GetComponent<CharacterController>();
            var aim = cc != null ? cc.bounds.center : player.position + Vector3.up * 0.6f;
            var delta = aim - camera.transform.position;
            if (!roomOccludersCached)
            {
                roomOccludersCached = true;
                foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.GetComponentInParent<GEDoor>() != null) continue;
                    for (var part = renderer.transform; part != null && part != transform; part = part.parent)
                        if (part.name.StartsWith("Wall_")) { roomOccluders.Add(renderer); break; }
                }
                // Decorative tree meshes may have no canopy collider. Check their render bounds,
                // limited to this building's campus zone; never change their collision or materials.
                if (transform.parent != null)
                    foreach (var renderer in transform.parent.GetComponentsInChildren<Renderer>(true))
                        if (!renderer.transform.IsChildOf(transform) && renderer.name.StartsWith("SM_Env_Tree_"))
                            roomOccluders.Add(renderer);
            }
            var ray = new Ray(camera.transform.position, delta.normalized);
            foreach (var renderer in roomOccluders)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                var bounds = renderer.bounds; bounds.Expand(0.5f);
                if (!bounds.IntersectRay(ray, out float distance) || distance >= delta.magnitude - 0.15f) continue;
                roomWalls.Add(renderer, renderer.shadowCastingMode);
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].r != null) entries[i].r.shadowCastingMode = entries[i].original;
            RestoreRoomWalls();
            currentCut = int.MinValue;
        }
    }
}
