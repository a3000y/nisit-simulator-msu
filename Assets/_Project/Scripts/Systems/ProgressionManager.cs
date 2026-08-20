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
        public int daysPerYear = 12;  // 12 เดือน: ต้น 5 + ปลาย 5 + ฤดูร้อน 2
        // เป้าความรู้สะสมของแต่ละปี (เพิ่มขึ้นเรื่อยๆ ตามเอกสาร 1.3.4.1)
        public float[] knowledgeTargets = { 180f, 420f, 720f, 1080f };

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
            Invoke(nameof(ShowYearIntro), 1.2f);   // แจ้งธีมชั้นปีตอนเข้าเกม
        }

        // ธีมประจำแต่ละชั้นปี (แสดงตอนเริ่ม/ขึ้นปี · ให้แอปโทรศัพท์เรียกได้)
        public string YearTheme(int year)
        {
            switch (year)
            {
                case 1:  return "น้องใหม่ — ปรับตัว + รับน้อง";
                case 2:  return "ลุยวิชาเอก + กิจกรรมชมรม";
                case 3:  return "วิชาเข้มข้น + ฝึกงาน";
                default: return "ปีสุดท้าย — โปรเจกต์จบ + เตรียมทำงาน";
            }
        }

        void ShowYearIntro() => UI.HUDController.Toast($"ปี {CurrentYear}: {YearTheme(CurrentYear)}");

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
                    UI.HUDController.Toast($"ขึ้นปี {CurrentYear}! {YearTheme(CurrentYear)} (เป้า {CurrentTarget:0})");

                    // ท่าดีใจ + เสียงแฟนแฟร์ตอนเลื่อนชั้นปี
                    var pl = GameObject.Find("Player");
                    var pac = pl != null ? pl.GetComponent<Player.PlayerActionController>() : null;
                    if (pac != null) pac.PerformState(2.5f, null, "Cheering");
                    SFXManager.Fanfare();
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
