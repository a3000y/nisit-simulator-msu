using UnityEngine;

namespace NisitSimulator.Systems
{
    // ปฏิทินการศึกษา: แปลง "วันในปี" → ภาคเรียน / ฤดู / โทนสี (ใช้ร่วมกันทั้งระบบสอบและฤดูกาล)
    // Calendar V3: 14 / 14 / 3 days; each term starts on Sunday.
    public static class AcademicCalendar
    {
        // Regular terms: registration 1, classes 2–5 / 9–12, exams 6 / 13.
        // Legacy 3/3/2 and 10/10/3 saves are upgraded by CalendarMigration.
        public const int Version = 3;
        public static readonly int[] LegacyTermDays = { 3, 3, 2 };
        static readonly int[]    TermDays  = { 14, 14, 3 };
        static readonly string[] TermNames = { "ภาคต้น", "ภาคปลาย", "ภาคฤดูร้อน" };
        static readonly string[] Seasons   = { "ฤดูฝน", "ฤดูหนาว", "ฤดูร้อน" };

        // 12 เดือนตามปีการศึกษาไทย (1 วัน = 1 เดือน)
        static readonly string[] Months = {
            "มิถุนายน", "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม",        // ภาคต้น (5)
            "พฤศจิกายน", "ธันวาคม", "มกราคม", "กุมภาพันธ์", "มีนาคม",     // ภาคปลาย (5)
            "เมษายน", "พฤษภาคม",                                        // ภาคฤดูร้อน (2)
        };
        static readonly Color[]  Tints     = {
            new Color(0.36f, 0.52f, 0.68f, 0.13f),  // ฝน: ฟ้าอมเทา ครึ้ม
            new Color(0.60f, 0.68f, 0.95f, 0.11f),  // หนาว: ฟ้าอมม่วง เย็น
            new Color(1.00f, 0.78f, 0.42f, 0.12f),  // ร้อน: เหลืองส้ม อบอุ่น
        };

        // Option B: each term starts on Sunday, including the three-day summer break.
        static readonly string[] Weekdays = { "อา.", "จ.", "อ.", "พ.", "พฤ.", "ศ.", "ส." };
        public static System.DayOfWeek WeekdayOf(int dayInYear) => WeekdayOfSemesterDay(SemesterDay(dayInYear));
        public static System.DayOfWeek WeekdayOfSemesterDay(int day) => (System.DayOfWeek)((Mathf.Max(1, day) - 1) % 7);
        public static bool IsWeekend(int dayInYear) => IsWeekendSemesterDay(SemesterDay(dayInYear));
        public static bool IsWeekendSemesterDay(int day) => WeekdayOfSemesterDay(day) == System.DayOfWeek.Sunday || WeekdayOfSemesterDay(day) == System.DayOfWeek.Saturday;
        public static bool IsRegistrationDay(int dayInYear) => SemesterIndex(dayInYear) < 2 && SemesterDay(dayInYear) == 1;
        public static string ShortTermDayText(int day) => $"{Weekdays[(int)WeekdayOfSemesterDay(day)]} {Mathf.Max(1, day)}";
        public static string TermDayText(int day) => $"{Weekdays[(int)WeekdayOfSemesterDay(day)]} วันที่ {Mathf.Max(1, day)} ของภาค";
        public static string DateText(int dayInYear) => TermDayText(SemesterDay(dayInYear));
        public static int MidtermDay(int sem) => HasMidterm(sem) ? 6 : 0;
        public static int FinalDay(int sem) => sem < 2 ? 13 : SemesterLen(sem);
        public static bool IsExamDay(int dayInYear) => SemesterDay(dayInYear) == FinalDay(SemesterIndex(dayInYear)) ||
            (HasMidterm(SemesterIndex(dayInYear)) && SemesterDay(dayInYear) == MidtermDay(SemesterIndex(dayInYear)));

        // จำนวนวันรวมต่อปี (ต้องตรงกับ ProgressionManager.daysPerYear)
        public static int TotalDays { get { int s = 0; foreach (var d in TermDays) s += d; return s; } }
        public static int TermCount => TermDays.Length;

        // ภาคเรียนของวันนี้ (0=ต้น, 1=ปลาย, 2=ฤดูร้อน)
        public static int SemesterIndex(int dayInYear)
        {
            int d = Mathf.Max(1, dayInYear), acc = 0;
            for (int i = 0; i < TermDays.Length; i++)
            {
                acc += TermDays[i];
                if (d <= acc) return i;
            }
            return TermDays.Length - 1;
        }

        // วันในภาคเรียน (1..len)
        public static int SemesterDay(int dayInYear)
        {
            int d = Mathf.Max(1, dayInYear), acc = 0;
            for (int i = 0; i < TermDays.Length; i++)
            {
                if (d <= acc + TermDays[i]) return d - acc;
                acc += TermDays[i];
            }
            return TermDays[TermDays.Length - 1];
        }

        public static int SemesterLen(int sem) => TermDays[Mathf.Clamp(sem, 0, TermDays.Length - 1)];

        // สอบกลางภาคมีเฉพาะภาคต้น/ภาคปลาย (ฤดูร้อนไม่มี แม้ยาว 3 วัน) และภาคต้องยาว >= 3 วัน
        public static bool HasMidterm(int sem) => sem < 2 && SemesterLen(sem) >= 3;

        // วันที่ sem/semDay → วันในปี (1..TotalDays)
        public static int DayInYear(int sem, int semDay)
        {
            int d = 0;
            for (int i = 0; i < Mathf.Clamp(sem, 0, TermDays.Length - 1); i++) d += TermDays[i];
            return d + Mathf.Clamp(semDay, 1, SemesterLen(sem));
        }

        // ชื่อเดือนของวันนี้ (1 วัน = 1 เดือนตามปีการศึกษา)
        // ชื่อเดือนของวันนี้ — กระจาย 12 เดือนให้ทั่วปี ไม่ว่าปีจะยาวกี่วัน
        // เดิมใช้ dayInYear-1 ตรง ๆ พอลดวันต่อปีแล้วเกมจะจบปีแค่เดือนพฤศจิกายน
        public static string MonthName(int dayInYear)
        {
            int total = Mathf.Max(1, TotalDays);
            int d = Mathf.Clamp(dayInYear, 1, total);
            int idx = Mathf.Clamp((d - 1) * Months.Length / total, 0, Months.Length - 1);
            return Months[idx];
        }

        public static string SemesterName(int sem) => TermNames[Mathf.Clamp(sem, 0, TermNames.Length - 1)];
        public static string SeasonName(int sem)   => Seasons[Mathf.Clamp(sem, 0, Seasons.Length - 1)];
        public static Color SeasonTint(int sem)    => Tints[Mathf.Clamp(sem, 0, Tints.Length - 1)];
    }
}
