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
        [Tooltip("ตั้งอัตโนมัติจาก AcademicCalendar.TotalDays ตอนเริ่ม (ภาคต้น 10 + ภาคปลาย 10 + ฤดูร้อน 3 = 23) — ค่าในฉากไม่มีผล")]
        public int daysPerYear = 23;
        // เป้าความรู้สะสมของแต่ละปี (เพิ่มขึ้นเรื่อยๆ ตามเอกสาร 1.3.4.1)
        // บาลานซ์ใหม่: ต้องการเพิ่มปีละ 400/450/500/550 ≈ 50%→70% ของการเรียนเต็มที่ (~790/ปี)
        // ปีหลัง ๆ จึงเหลือเวลาไปทำงาน/เที่ยวน้อยลงจริง ตามเอกสาร 1.3.4.1
        // ปฏิทินรุ่น 2 ปีละ 23 วัน (เดิม 8) → คูณ 23/8 = {1150, 2450, 3900, 5450} (ใช้กับคณะที่ไม่ใช้หลักสูตรลงทะเบียน)
        public float[] knowledgeTargets = { 1150f, 2450f, 3900f, 5450f };

        [Header("เกณฑ์เกรดเฉลี่ย (รีไทร์)")]
        public float minGpa = 2.00f;
        [Tooltip("เริ่มตัดสินรีไทร์ด้วย GPA ตั้งแต่สิ้นปีนี้ (ปี 1 ผ่อนผัน)")]
        public int gpaCheckFromYear = 2;

        public int CurrentYear { get; private set; } = 1;
        public int DayInYear { get; private set; } = 1;
        // ปีการศึกษาที่เล่นจริง (นับทุกปีที่ผ่านไป) — แยกจาก CurrentYear (ชั้นปี) เพื่อรองรับเรียนล่าช้า/ภาคเพิ่มเติม
        //   โหมดเดิม: เท่ากับ CurrentYear เสมอ · โหมดหลักสูตร (CourseRegistrar): ชั้นปีขึ้นตามหน่วยกิต
        public int CalendarYear { get; private set; } = 1;
        public float CurrentTarget =>
            knowledgeTargets[Mathf.Clamp(CurrentYear - 1, 0, knowledgeTargets.Length - 1)];

        // event ให้ HUD ฟัง
        public event Action<int, float> OnYearChanged;      // (ปี, เป้าความรู้)
        public event Action<int, int> OnDayInYearChanged;   // (วันในปี, วันต่อปี)
        // ยิงก่อน OnDayInYearChanged — ให้ระบบลงทะเบียนเปิด/ปิดภาคก่อนที่ระบบสอบ/ฤดูกาลจะอ่านสถานะ
        public event Action<int, int> OnDayInYearChangedEarly;

        void RaiseDayInYear()
        {
            OnDayInYearChangedEarly?.Invoke(DayInYear, daysPerYear);
            OnDayInYearChanged?.Invoke(DayInYear, daysPerYear);
        }

        // ระบบลงทะเบียนตั้งชั้นปี (เลื่อนตามหน่วยกิต) — HUD/ระบบอื่นที่อ่าน CurrentYear เห็นชั้นปีจริง
        public void SetClassYear(int year)
        {
            year = Mathf.Clamp(year, 1, knowledgeTargets.Length);
            if (year == CurrentYear) return;
            CurrentYear = year;
            OnYearChanged?.Invoke(CurrentYear, CurrentTarget);
        }

        private PlayerStats stats;
        private GameClock clock;
        private ExamController exam;
        private int lastGlobalDay;

void Awake()
        {
            // จำนวนวันต่อปีมาจากปฏิทินเสมอ — กันค่าในฉาก/ตัวสร้างเก่า (M7SeasonBuilder ตั้ง 12) คลาดกับ AcademicCalendar
            if (daysPerYear != AcademicCalendar.TotalDays)
            {
                Debug.Log($"[Progression] daysPerYear {daysPerYear} → {AcademicCalendar.TotalDays} (ตาม AcademicCalendar)");
                daysPerYear = AcademicCalendar.TotalDays;
            }
        }

void Start()
        {
            stats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            clock = UnityEngine.Object.FindFirstObjectByType<GameClock>();
            exam = UnityEngine.Object.FindFirstObjectByType<ExamController>();

            if (clock != null)
            {
                lastGlobalDay = clock.Day;
                clock.OnDayChanged += HandleNewDay;
            }

            OnYearChanged?.Invoke(CurrentYear, CurrentTarget);
            RaiseDayInYear();
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

            // สอบที่ค้างของเมื่อวาน (ไม่ได้ไป) → F ก่อน เพื่อให้ปลายภาควันสุดท้ายนับเข้า GPA สิ้นปีด้วย
            if (exam != null) exam.ResolveMissedExam();

            DayInYear++;
            if (DayInYear > daysPerYear)
                EvaluateYear();
            else
                RaiseDayInYear();
        }

        // ประเมินผลสิ้นปี
private void EvaluateYear()
        {
            // โหมดหลักสูตร (คณะสายคอมพิวเตอร์): เลื่อนชั้นปี/จบการศึกษาตามหน่วยกิตและรายวิชา (ตัดสินตอนประกาศผลภาค)
            //   ไม่ใช้เป้าความรู้ — สิ้นปีแค่เช็กเกณฑ์ GPA เดิม แล้วขึ้นปีการศึกษาใหม่ (ชั้นปีอาจคงเดิมถ้าหน่วยกิตไม่ถึง)
            var reg = NisitSimulator.Academics.CourseRegistrar.Instance;
            if (reg != null && reg.IsActive)
            {
                if (reg.HandleAcademicYearEnd(minGpa, gpaCheckFromYear, CalendarYear, out string prob)) return;
                CalendarYear++;
                DayInYear = 1;
                CurrentYear = Mathf.Clamp(reg.ClassYear, 1, knowledgeTargets.Length);
                OnYearChanged?.Invoke(CurrentYear, CurrentTarget);
                RaiseDayInYear();
                UI.HUDController.Toast($"เริ่มปีการศึกษาที่ {CalendarYear} · ชั้นปี {CurrentYear}: {YearTheme(CurrentYear)}" + prob);
                return;
            }

            float knowledge = stats != null ? stats.Knowledge : 0f;

            if (knowledge >= CurrentTarget)
            {
                // ความรู้ถึง แต่เกรดเฉลี่ยต่ำกว่าเกณฑ์ = รีไทร์ (ปี 1 ผ่อนผัน → ติดโปร)
                string probation = "";
                if (exam != null && exam.ExamsTaken > 0 && exam.GPA < minGpa)
                {
                    if (CurrentYear >= gpaCheckFromYear)
                    {
                        GameManager.Instance?.EndGame(EndReason.RetiredGPA);
                        return;
                    }
                    probation = $"\nติดโปร! GPA {exam.GPA:0.00} ต่ำกว่า {minGpa:0.00} — ต้องดึงขึ้นภายในสิ้นปี {gpaCheckFromYear}";
                }

                // ผ่าน!
                if (CurrentYear >= knowledgeTargets.Length)
                {
                    // ผ่านปีสุดท้าย = จบการศึกษา
                    GameManager.Instance?.EndGame(EndReason.Graduated);
                }
                else
                {
                    CurrentYear++;
                    CalendarYear = CurrentYear;
                    DayInYear = 1;
                    OnYearChanged?.Invoke(CurrentYear, CurrentTarget);
                    RaiseDayInYear();
                    UI.HUDController.Toast($"ขึ้นปี {CurrentYear}! {YearTheme(CurrentYear)} (เป้า {CurrentTarget:0})" + probation);

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
public void RestoreState(int year, int dayInYear, int calendarYear = 0)
        {
            CurrentYear = Mathf.Clamp(year, 1, knowledgeTargets.Length);
            DayInYear = Mathf.Max(1, dayInYear);
            CalendarYear = calendarYear > 0 ? calendarYear : CurrentYear;   // เซฟเก่าไม่มีฟิลด์นี้ = เท่ากับชั้นปี
            OnYearChanged?.Invoke(CurrentYear, CurrentTarget);
            RaiseDayInYear();
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
