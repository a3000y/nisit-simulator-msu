using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Systems;
namespace NisitSimulator.SaveLoad
{
    public static class CalendarMigration
    {
        public static int MapV2TermDay(int day)
        {
            int[] days = { 2, 3, 4, 5, 6, 9, 10, 11, 12, 13 };
            return days[Mathf.Clamp(day, 1, 10) - 1];
        }
        public static int MapLegacyDay(int oldDayInYear)
        {
            int d = Mathf.Clamp(oldDayInYear, 1, 8);
            int sem = d <= 3 ? 0 : d <= 6 ? 1 : 2;
            int sd = sem < 2 ? d - sem * 3 : d - 6;
            int mapped = sem == 2 ? (sd == 1 ? 1 : 3) : sd == 1 ? 1 : sd == 2 ? 6 : 13;
            return AcademicCalendar.DayInYear(sem, mapped);
        }
        static void RemapKeys(List<string> keys)
        {
            if (keys == null) return;
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i]; if (string.IsNullOrEmpty(key)) continue;
                int colon = key.IndexOf(':');
                if (colon > 0 && int.TryParse(key.Substring(0, colon), out int day)) keys[i] = MapV2TermDay(day) + key.Substring(colon);
            }
        }
        public static bool Upgrade(SaveData d)
        {
            if (d == null) return false;
            if (d.arrivalVersion == 0) { d.arrivalVersion = 1; d.arrivalIntroDone = true; d.tourStep = 14; }
            if (d.calendarVersion >= AcademicCalendar.Version) return false;
            int version = d.calendarVersion, old = d.dayInYear;
            var term = d.hasAcademicRecord && d.academic != null ? d.academic.Current : null;
            if (version < 2) d.dayInYear = MapLegacyDay(old);
            else
            {
                int day = Mathf.Clamp(old, 1, 23), sem = day <= 10 ? 0 : day <= 20 ? 1 : 2;
                int sd = sem == 2 ? day - 20 : day - sem * 10;
                int mapped = sem == 2 ? sd : MapV2TermDay(sd);
                if (sem < 2 && sd == 1 && (term == null || !term.confirmed)) mapped = 1;
                d.dayInYear = AcademicCalendar.DayInYear(sem, mapped);
            }
            if (term != null && d.academic.enrollments != null)
                foreach (var e in d.academic.enrollments)
                {
                    if (e == null || e.termSerial != term.serial || e.graded || e.transfer) continue;
                    if (version == 2) { RemapKeys(e.meetingKeys); RemapKeys(e.classEventKeys); }
                    else
                    {
                        if (e.meetingKeys != null) e.meetingKeys.Clear();
                        if (e.meetingTicks != null) e.meetingTicks.Clear();
                        if (e.classEventKeys != null) e.classEventKeys.Clear();
                    }
                }
            // Preserve the elapsed-day counter: daily rewards and friendships must not jump.
            d.calendarVersion = AcademicCalendar.Version;
            Debug.Log($"[Save] แปลงปฏิทินรุ่น {version} เป็น {AcademicCalendar.Version}: {old} → {d.dayInYear}");
            return true;
        }
    }
}
