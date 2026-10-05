using UnityEngine;
using NisitSimulator.Core;
using NisitSimulator.SaveLoad;
using NisitSimulator.TimeSystem;

namespace NisitSimulator.Stats
{
    // ทำให้สถานะค่อยๆ ลดตามเวลา (Dynamics ตามทฤษฎี MDA ในเอกสาร)
    // ใส่ไว้ที่ตัวละคร Player ร่วมกับ PlayerStats
    [RequireComponent(typeof(PlayerStats))]
    public class StatDecay : MonoBehaviour
    {
        [Header("อัตราลดต่อหนึ่งนาทีในเกม (ไม่ใช่วินาทีจริง)")]
        public float hungerDecayPerSec = 0.12f;   // หิวขึ้นเรื่อยๆ
        public float energyDecayPerSec = 0.05f;   // เหนื่อยตามเวลา

        [Header("เมื่อหิวจัด (ความอิ่ม = 0)")]
        public float starvingEnergyDrain = 0.4f;  // หิวจัดทำให้พลังงานลดเร็ว
        public float starvingHealthDrain = 0.25f; // และเริ่มเสียสุขภาพ

        [Header("ความเครียด (ต่อหนึ่งนาทีในเกม)")]
        [Tooltip("เครียดขึ้นเมื่อหิวจัดหรือหมดแรง")]
        public float stressRisePerMinute = 0.02f;
        [Tooltip("คลายลงเองเมื่ออิ่มและมีแรง — ช้ากว่าขาขึ้น ความเครียดจึงสะสมได้")]
        public float stressFallPerMinute = StressBands.NaturalFallPerMinute;   // 0.006 (เดิม 0.01) — ให้ความเครียดสะสมได้จริง
        [Tooltip("ต่ำกว่านี้ถือว่าหิว/หมดแรง")]
        public float lowThreshold = 25f;

        private PlayerStats stats;
        private NisitSimulator.Player.PlayerEffects effects;

        private GameClock clock;

        void Awake()
        {
            stats = GetComponent<PlayerStats>();
            effects = GetComponent<NisitSimulator.Player.PlayerEffects>();
            if (effects == null) effects = gameObject.AddComponent<NisitSimulator.Player.PlayerEffects>();
        }

        void Start()
        {
            clock = Object.FindFirstObjectByType<GameClock>();
        }

        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) return;
            if (GameClock.Suspended) return;   // อยู่ในห้องสอบมินิเกม → เวลาเกมหยุด สถานะไม่ลด

            // ผูกการลดสถานะกับ "เวลาในเกม" ไม่ใช่เวลาจริง
            // เดิมใช้ Time.deltaTime ตรง ๆ พอเร่งนาฬิกาสมดุลจะพังทันที
            // เช่น เร่ง 3 เท่า ความหิวจะมาช้าลง 3 เท่าเมื่อเทียบกับเวลาในเกม กินข้าววันละมื้อก็ไม่หิว
            float speed = clock != null ? clock.EffectiveMinutesPerSecond : 1f;   // รวมตัวคูณเร่งเวลาตอนเข้าเรียน
            Tick(Time.deltaTime * speed);
        }

        // ลดสถานะตามจำนวน "นาทีในเกม" — ตรรกะเดียวกับทุกเฟรม (Dev Panel ใช้ตอนจำลองเวลาเดินผ่าน)
        public void Tick(float dt)
        {
            if (stats == null) return;
            float drainMult = effects != null ? effects.energyDrainMult : 1f;
            float diff = GameSession.DecayMultiplier;   // ความยาก: ง่าย 0.7 / ปกติ 1 / ยาก 1.4

            stats.ChangeHunger(-hungerDecayPerSec * diff * NisitSimulator.Systems.Perks.HungerDrainMul * dt);   // "กินน้อย"
            stats.ChangeEnergy(-energyDecayPerSec * drainMult * diff * NisitSimulator.Systems.Perks.EnergyDrainMul * dt);   // "อึด"
            // ความเครียด: หิวหรือหมดแรงแล้วเครียดขึ้น อยู่ในสภาพดีแล้วค่อย ๆ คลาย
            // ตัวที่ทำให้เครียดก้อนใหญ่คือเรียนและสอบ (CourseData.StressGain) ส่วนนี้เป็นแค่พื้นหลัง
            bool strained = stats.Hunger <= lowThreshold || stats.Energy <= lowThreshold;
            //   วันที่ไม่มีคาบเรียน → คลายเร็วขึ้น ×1.5 (ได้พักจริง)
            float fall = stressFallPerMinute * (IsFreeDay() ? StressBands.FreeDayRecoveryMult : 1f);
            stats.ChangeStress((strained ? stressRisePerMinute : -fall) * dt);

            // ถ้าความอิ่มหมด → เริ่มหักพลังงานและสุขภาพ
            if (stats.Hunger <= 0f)
            {
                stats.ChangeEnergy(-starvingEnergyDrain * dt);
                stats.ChangeHealth(-starvingHealthDrain * dt);
            }
        }

        // ---------- วันว่าง (ไม่มีคาบเรียนวันนี้) — คิดครั้งเดียวต่อวันเกม ----------
        //   คิดใหม่เมื่อขึ้นวันใหม่ หรือทุก 5 วินาทีจริง (ลงทะเบียน/ยืนยันกลางวันก็อัปเดตตาม)
        int freeDayCachedFor = -1;
        float freeDayCheckedAt = -999f;
        bool freeDayCached;

        bool IsFreeDay()
        {
            int day = clock != null ? clock.Day : -1;
            if (day < 0) return false;
            if (day != freeDayCachedFor || Time.unscaledTime - freeDayCheckedAt > 5f)
            {
                freeDayCachedFor = day;
                freeDayCheckedAt = Time.unscaledTime;
                freeDayCached = ComputeFreeDay();
            }
            return freeDayCached;
        }

        // หลักสูตรลงทะเบียน: วันนี้ไม่มีคาบของวิชาที่ลงไว้ (รวมปิดภาค/วันสอบ) · คณะที่ไม่ใช้หลักสูตร = ไม่มีโบนัส
        static bool ComputeFreeDay()
        {
            var reg = NisitSimulator.Academics.CourseRegistrar.Instance;
            var p = Object.FindAnyObjectByType<NisitSimulator.Systems.ProgressionManager>();
            if (p != null && NisitSimulator.Systems.AcademicCalendar.IsWeekend(p.DayInYear)) return true;
            if (reg == null || !reg.IsActive || reg.Service == null) return false;
            int semDay = reg.SemDay;
            foreach (var e in reg.Service.CurrentEnrollments())
            {
                if (e.graded) continue;
                foreach (var s in reg.Service.SessionsFor(e)) if (s.day == semDay) return false;
            }
            return true;
        }

        // ให้ระบบอื่นสั่งคิดใหม่ (เช่น ยืนยันลงทะเบียนกลางวัน)
        public void InvalidateFreeDay() => freeDayCachedFor = -1;
    }
}
