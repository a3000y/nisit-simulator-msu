using UnityEngine;

namespace NisitSimulator.Systems
{
    // ปฏิทินการศึกษา: แปลง "วันในปี" → ภาคเรียน / ฤดู / โทนสี (ใช้ร่วมกันทั้งระบบสอบและฤดูกาล)
    // ปีหนึ่ง = 3 ภาคเรียน (12 วัน = 12 เดือน มิ.ย.→พ.ค.):
    //   ภาคต้น(ฝน) 5 [มิ.ย.-ต.ค.] → ภาคปลาย(หนาว) 5 [พ.ย.-มี.ค.] → ภาคฤดูร้อน(ร้อน) 2 [เม.ย.-พ.ค.]
    public static class AcademicCalendar
    {
        static readonly int[]    TermDays  = { 5, 5, 2 };
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

        // ภาคสั้น (< 3 วัน) ไม่มีสอบกลางภาค
        public static bool HasMidterm(int sem) => SemesterLen(sem) >= 3;

        // ชื่อเดือนของวันนี้ (1 วัน = 1 เดือนตามปีการศึกษา)
        public static string MonthName(int dayInYear) => Months[Mathf.Clamp(dayInYear - 1, 0, Months.Length - 1)];

        public static string SemesterName(int sem) => TermNames[Mathf.Clamp(sem, 0, TermNames.Length - 1)];
        public static string SeasonName(int sem)   => Seasons[Mathf.Clamp(sem, 0, Seasons.Length - 1)];
        public static Color SeasonTint(int sem)    => Tints[Mathf.Clamp(sem, 0, Tints.Length - 1)];
    }
}
