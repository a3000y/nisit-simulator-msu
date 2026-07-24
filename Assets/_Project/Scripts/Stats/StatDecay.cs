using UnityEngine;
using NisitSimulator.Core;

namespace NisitSimulator.Stats
{
    // ทำให้สถานะค่อยๆ ลดตามเวลา (Dynamics ตามทฤษฎี MDA ในเอกสาร)
    // ใส่ไว้ที่ตัวละคร Player ร่วมกับ PlayerStats
    [RequireComponent(typeof(PlayerStats))]
    public class StatDecay : MonoBehaviour
    {
        [Header("อัตราลดต่อวินาที")]
        public float hungerDecayPerSec = 0.5f;   // หิวขึ้นเรื่อยๆ
        public float energyDecayPerSec = 0.2f;    // เหนื่อยตามเวลา

        [Header("เมื่อหิวจัด (ความอิ่ม = 0)")]
        public float starvingEnergyDrain = 1f;    // หิวจัดทำให้พลังงานลดเร็ว
        public float starvingHealthDrain = 0.5f;  // และเริ่มเสียสุขภาพ

        private PlayerStats stats;

        void Awake() => stats = GetComponent<PlayerStats>();

        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) return;
            float dt = Time.deltaTime;

            stats.ChangeHunger(-hungerDecayPerSec * dt);
            stats.ChangeEnergy(-energyDecayPerSec * dt);

            // ถ้าความอิ่มหมด → เริ่มหักพลังงานและสุขภาพ
            if (stats.Hunger <= 0f)
            {
                stats.ChangeEnergy(-starvingEnergyDrain * dt);
                stats.ChangeHealth(-starvingHealthDrain * dt);
            }
        }
    }
}
