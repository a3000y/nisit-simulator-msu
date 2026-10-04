using UnityEngine;

namespace NisitSimulator.Systems
{
    // ปฏิทินการศึกษา: แปลง "วันในปี" → ภาคเรียน / ฤดู / โทนสี (ใช้ร่วมกันทั้งระบบสอบและฤดูกาล)
    // ปีหนึ่ง = 3 ภาคเรียน (12 วัน = 12 เดือน มิ.ย.→พ.ค.):
    //   ภาคต้น(ฝน) 5 [มิ.ย.-ต.ค.] → ภาคปลาย(หนาว) 5 [พ.ย.-มี.ค.] → ภาคฤดูร้อน(ร้อน) 2 [เม.ย.-พ.ค.]
    public static class AcademicCalendar
    {
        // ปฏิทินรุ่น 2 (ยืดเวลาเล่น ~14 ชม./4 ปี): ภาคต้น 10 · ภาคปลาย 10 · ฤดูร้อน 3 = ปีละ 23 วัน
        //   ภาคต้น/ปลาย: วันที่ 1–4 เรียน · 5 สอบกลางภาค · 6–9 เรียน · 10 สอบปลายภาค (ExamController.MidtermDay/FinalDay)
        //   รุ่นก่อน (รุ่น 1) = 3/3/2 — เซฟเก่าแปลงวันด้วย CalendarMigration ตอนโหลด
        public const int Version = 2;
        public static readonly int[] LegacyTermDays = { 3, 3, 2 };
        static readonly int[]    TermDays  = { 10, 10, 3 };
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
