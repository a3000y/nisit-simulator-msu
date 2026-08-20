using UnityEngine;
using NisitSimulator.Core;

namespace NisitSimulator.Stats
{
    // ทำให้สถานะค่อยๆ ลดตามเวลา (Dynamics ตามทฤษฎี MDA ในเอกสาร)
    // ใส่ไว้ที่ตัวละคร Player ร่วมกับ PlayerStats
    [RequireComponent(typeof(PlayerStats))]
    public class StatDecay : MonoBehaviour
    {
        [Header("อัตราลดต่อวินาที (จูนให้พอดีเวลาช้า 1 นาที/วิ)")]
        public float hungerDecayPerSec = 0.12f;   // หิวขึ้นเรื่อยๆ
        public float energyDecayPerSec = 0.05f;   // เหนื่อยตามเวลา

        [Header("เมื่อหิวจัด (ความอิ่ม = 0)")]
        public float starvingEnergyDrain = 0.4f;  // หิวจัดทำให้พลังงานลดเร็ว
        public float starvingHealthDrain = 0.25f; // และเริ่มเสียสุขภาพ

        private PlayerStats stats;
        private NisitSimulator.Player.PlayerEffects effects;

        void Awake()
        {
            stats = GetComponent<PlayerStats>();
            effects = GetComponent<NisitSimulator.Player.PlayerEffects>() ?? gameObject.AddComponent<NisitSimulator.Player.PlayerEffects>();
        }

        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) return;
            float dt = Time.deltaTime;
            float drainMult = effects != null ? effects.energyDrainMult : 1f;

            stats.ChangeHunger(-hungerDecayPerSec * dt);
            stats.ChangeEnergy(-energyDecayPerSec * drainMult * dt);

            // ถ้าความอิ่มหมด → เริ่มหักพลังงานและสุขภาพ
            if (stats.Hunger <= 0f)
            {
                stats.ChangeEnergy(-starvingEnergyDrain * dt);
                stats.ChangeHealth(-starvingHealthDrain * dt);
            }
        }
    }
}
