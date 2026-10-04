using UnityEngine;
using NisitSimulator.Systems;

namespace NisitSimulator.SaveLoad
{
    // ===== แปลงเซฟจากปฏิทินรุ่นเก่า (3/3/2 = ปีละ 8 วัน) → รุ่นปัจจุบัน (10/10/3 = ปีละ 23 วัน) — ตรรกะล้วน ทดสอบได้ =====
    //   เรียกเป็นอย่างแรกหลังโหลดไฟล์ (SaveManager.ApplyIfPending) ก่อนระบบลงทะเบียน/สอบอ่านวันในปี
    //   ภาคต้น/ปลาย: วันที่ 1 → 1 · วันสอบกลางภาค (2) → วันสอบกลางภาคใหม่ (5) · วันสุดท้าย (3) → วันสุดท้ายใหม่ (10)
    //   ฤดูร้อน: วันที่ 1 → 1 · วันสุดท้าย (2) → วันสุดท้ายใหม่ (3)
    //   ความคืบหน้าการเรียน/คะแนนสอบ/โบนัส/สอบที่ทำแล้ว เก็บครบ · ล้างเฉพาะคีย์ชั่วโมงต่อคาบและคีย์เหตุการณ์ของเทอมปัจจุบัน (ลำดับคาบเปลี่ยน)
    public static class CalendarMigration
    {
        public static int MapLegacyDay(int oldDayInYear)
        {
            var legacy = AcademicCalendar.LegacyTermDays;
            int total = 0; foreach (var x in legacy) total += x;
            int d = Mathf.Clamp(oldDayInYear, 1, total), acc = 0, sem = legacy.Length - 1, semDay = 1;
            for (int i = 0; i < legacy.Length; i++)
            {
                if (d <= acc + legacy[i]) { sem = i; semDay = d - acc; break; }
                acc += legacy[i];
            }
            int oldLen = legacy[sem];
            int newLen = AcademicCalendar.SemesterLen(sem);
            int oldMid = Mathf.Max(1, Mathf.CeilToInt(oldLen / 2f));
            int mapped;
            if (semDay >= oldLen) mapped = newLen;                                                            // วันสุดท้าย (สอบปลายภาค)
            else if (sem < 2 && oldLen >= 3 && semDay == oldMid) mapped = ExamController.MidtermDay(sem);    // วันสอบกลางภาค
            else mapped = Mathf.Clamp(semDay, 1, newLen);
            return AcademicCalendar.DayInYear(sem, mapped);
        }

        // คืน true ถ้ามีการแปลง
        public static bool Upgrade(SaveData d)
        {
            if (d == null || d.calendarVersion >= AcademicCalendar.Version) return false;
            int before = d.dayInYear;
            d.dayInYear = MapLegacyDay(d.dayInYear);
            if (d.hasAcademicRecord && d.academic != null && d.academic.hasCurrent && d.academic.current != null)
            {
                int serial = d.academic.current.serial;
                foreach (var e in d.academic.enrollments)
                {
                    if (e == null || e.termSerial != serial || e.graded || e.transfer) continue;
                    e.meetingKeys.Clear(); e.meetingTicks.Clear();
                    if (e.classEventKeys != null) e.classEventKeys.Clear();
                }
            }
            d.calendarVersion = AcademicCalendar.Version;
            Debug.Log($"[Save] แปลงปฏิทินเซฟเก่า: วันในปี {before} → {d.dayInYear} (ปฏิทินรุ่น {AcademicCalendar.Version})");
            return true;
        }
    }
}
