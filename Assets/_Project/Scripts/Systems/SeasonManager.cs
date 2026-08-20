using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.UI;

namespace NisitSimulator.Systems
{
    // ระบบฤดูกาล: เปลี่ยนโทนสีบรรยากาศ + ป้ายบอกภาคเรียน/ฤดู ตาม "วันในปี"
    // UI ถูกสร้าง+ต่อโดย Editor tool (Nisit -> Build Season System)
    public class SeasonManager : MonoBehaviour
    {
        [Header("UI (เซ็ตโดย Editor)")]
        public Image tintOverlay;     // แผ่นโทนสีเต็มจอ (โปร่ง)
        public TMP_Text seasonLabel;  // ป้าย "ภาคต้น • ฤดูฝน"

        public int CurrentSemester { get; private set; } = -1;

        private ProgressionManager prog;
        private Color targetTint;

        void Start()
        {
            prog = Object.FindFirstObjectByType<ProgressionManager>();
            if (prog != null)
            {
                prog.OnDayInYearChanged += OnDay;
                Apply(prog.DayInYear, prog.daysPerYear);
            }
        }

        void OnDestroy()
        {
            if (prog != null) prog.OnDayInYearChanged -= OnDay;
        }

        private void OnDay(int dayInYear, int daysPerYear) => Apply(dayInYear, daysPerYear);

        void Update()
        {
            // ค่อย ๆ ไล่โทนสีเข้าหาฤดูปัจจุบัน (ไม่เปลี่ยนวูบ)
            if (tintOverlay != null)
                tintOverlay.color = Color.Lerp(tintOverlay.color, targetTint, Time.deltaTime * 1.5f);
        }

        private void Apply(int dayInYear, int daysPerYear)
        {
            int sem = AcademicCalendar.SemesterIndex(dayInYear);

            if (seasonLabel != null)
                seasonLabel.text =
                    $"<size=27><b>{AcademicCalendar.MonthName(dayInYear)}</b></size>\n" +
                    $"<size=20>{AcademicCalendar.SemesterName(sem)} • {AcademicCalendar.SeasonName(sem)}</size>";

            targetTint = AcademicCalendar.SeasonTint(sem);

            if (sem != CurrentSemester)
            {
                bool first = CurrentSemester < 0;
                CurrentSemester = sem;
                if (first && tintOverlay != null) tintOverlay.color = targetTint;   // ครั้งแรกตั้งทันที
                if (!first)
                    HUDController.Toast($"เข้าสู่{AcademicCalendar.SemesterName(sem)} ({AcademicCalendar.SeasonName(sem)})");
            }
        }
    }
}
