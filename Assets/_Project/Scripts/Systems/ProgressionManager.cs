using System;
using UnityEngine;
using NisitSimulator.Core;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;

namespace NisitSimulator.Systems
{
    // ระบบเลื่อนชั้นปี 1→4 + เงื่อนไขแพ้/ชนะ (หัวใจของเกม)
    // ใส่ไว้ที่ GameManager
    public class ProgressionManager : MonoBehaviour
    {
        [Header("ตั้งค่าปีการศึกษา")]
        public int daysPerYear = 3;
        // เป้าความรู้สะสมของแต่ละปี (เพิ่มขึ้นเรื่อยๆ ตามเอกสาร 1.3.4.1)
        public float[] knowledgeTargets = { 80f, 180f, 300f, 440f };

        public int CurrentYear { get; private set; } = 1;
        public int DayInYear { get; private set; } = 1;
        public float CurrentTarget =>
            knowledgeTargets[Mathf.Clamp(CurrentYear - 1, 0, knowledgeTargets.Length - 1)];

        // event ให้ HUD ฟัง
        public event Action<int, float> OnYearChanged;      // (ปี, เป้าความรู้)
        public event Action<int, int> OnDayInYearChanged;   // (วันในปี, วันต่อปี)

        private PlayerStats stats;
        private GameClock clock;
        private int lastGlobalDay;

        void Start()
        {
            stats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            clock = UnityEngine.Object.FindFirstObjectByType<GameClock>();

            if (clock != null)
            {
                lastGlobalDay = clock.Day;
                clock.OnDayChanged += HandleNewDay;
            }

            OnYearChanged?.Invoke(CurrentYear, CurrentTarget);
            OnDayInYearChanged?.Invoke(DayInYear, daysPerYear);
        }

        void OnDestroy()
        {
            if (clock != null) clock.OnDayChanged -= HandleNewDay;
        }

        // ขึ้นวันใหม่ → นับวันในปี พอครบก็ตัดสินว่าผ่านหรือตก
        private void HandleNewDay(int globalDay)
        {
            if (globalDay <= lastGlobalDay) return;
            lastGlobalDay = globalDay;

            DayInYear++;
            if (DayInYear > daysPerYear)
                EvaluateYear();
            else
                OnDayInYearChanged?.Invoke(DayInYear, daysPerYear);
        }

        // ประเมินผลสิ้นปี
        private void EvaluateYear()
        {
            float knowledge = stats != null ? stats.Knowledge : 0f;

            if (knowledge >= CurrentTarget)
            {
                // ผ่าน!
                if (CurrentYear >= knowledgeTargets.Length)
                {
                    // ผ่านปีสุดท้าย = จบการศึกษา
                    GameManager.Instance?.EndGame(EndReason.Graduated);
                }
                else
                {
                    CurrentYear++;
                    DayInYear = 1;
                    OnYearChanged?.Invoke(CurrentYear, CurrentTarget);
                    OnDayInYearChanged?.Invoke(DayInYear, daysPerYear);
                    UI.HUDController.Toast($"ผ่านขึ้นปี {CurrentYear}! เป้าใหม่ {CurrentTarget:0} ความรู้");
                }
            }
            else
            {
                // ความรู้ไม่ถึงเป้า = สอบตก/รีไทร์
                GameManager.Instance?.EndGame(EndReason.Flunked);
            }
        }

        // โหลดความคืบหน้าจากเซฟ (M4)
        public void RestoreState(int year, int dayInYear)
        {
            CurrentYear = Mathf.Clamp(year, 1, knowledgeTargets.Length);
            DayInYear = Mathf.Max(1, dayInYear);
            OnYearChanged?.Invoke(CurrentYear, CurrentTarget);
            OnDayInYearChanged?.Invoke(DayInYear, daysPerYear);
        }

        // คำนวณคะแนนรวม (แสดงตอนจบเกม ตามสตอรี่บอร์ด)
        public int CalculateScore()
        {
            if (stats == null) return 0;
            return Mathf.RoundToInt(
                stats.Knowledge * 10f +
                stats.Money * 5f +
                stats.Satisfaction * 3f +
                (CurrentYear - 1) * 500f +
                stats.Exp * 2f);
        }
    }
}
