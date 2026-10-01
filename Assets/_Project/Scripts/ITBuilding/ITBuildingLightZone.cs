using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.GEBuilding;

namespace NisitSimulator.ITBuilding
{
    // เปิดไฟภายในอาคาร IT เฉพาะชั้นที่ผู้เล่นอยู่ (ลดจำนวนไฟ Realtime ที่ทำงานพร้อมกัน)
    //   • อยู่ในอาคาร → เปิดไฟกลุ่มของชั้นนั้นชั้นเดียว (ชั้นอื่นถูก GEBuildingCutaway ซ่อน/บังอยู่แล้ว)
    //   • อยู่นอกอาคารแต่ใกล้ (≤ nearDistance) → เปิดเฉพาะชั้น 1 (โถงทางเข้ามองเห็นผ่านกระจก)
    //   • ไกลกว่านั้น → ปิดทั้งหมด (แผงไฟ Emissive ยังเรืองแสงตามปกติ)
    // ใช้ PlayerFloor() ของ GEBuildingCutaway ตัวเดียวกับที่ตัดชั้นบน
    public class ITBuildingLightZone : MonoBehaviour
    {
        public GEBuildingCutaway cutaway;
        [Tooltip("กลุ่มไฟของแต่ละชั้น (index 0 = ชั้น 1)")]
        public Transform[] floorLightGroups;
        public float nearDistance = 30f;
        public float checkInterval = 0.25f;

        private readonly List<Light[]> lights = new List<Light[]>();
        private int state = int.MinValue;
        private float timer;

        public int ActiveFloor => state;

        void Awake()
        {
            if (cutaway == null) cutaway = GetComponent<GEBuildingCutaway>();
            lights.Clear();
            if (floorLightGroups != null)
                foreach (var g in floorLightGroups)
                    lights.Add(g != null ? g.GetComponentsInChildren<Light>(true) : new Light[0]);
        }

        void OnEnable() { state = int.MinValue; timer = 0f; }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = checkInterval;
            Refresh();
        }

        public void Refresh()
        {
            int floor = cutaway != null ? cutaway.PlayerFloor() : -1;
            int want;
            if (floor >= 0) want = Mathf.Min(floor, lights.Count - 1);
            else
            {
                var p = cutaway != null ? cutaway.player : null;
                bool near = p != null && Vector3.Distance(p.position, transform.position) <= nearDistance;
                want = near ? -1 : -2;   // -1 = ใกล้ (เปิดชั้น 1) · -2 = ไกล (ปิดหมด)
            }
            if (want == state) return;
            state = want;
            for (int i = 0; i < lights.Count; i++)
            {
                bool on = want >= 0 ? i == want : (want == -1 && i == 0);
                foreach (var l in lights[i]) if (l != null) l.enabled = on;
            }
        }
    }
}
