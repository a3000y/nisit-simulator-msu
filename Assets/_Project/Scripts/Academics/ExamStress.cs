using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using NisitSimulator.Stats;

namespace NisitSimulator.Academics
{
    // ===== กังวลก่อนสอบตามการเตรียมตัว =====
    //   เริ่มวันสอบ: แต่ละวิชาที่ต้องสอบรอบนั้น +2..+8 ตามสัดส่วนการเข้าเรียนถึงวันก่อนสอบ (ครบ +2 · ไม่เคยเข้า +8) · รวมไม่เกิน +30
    //   ส่งข้อสอบแต่ละวิชา: คืน 50% ของที่วิชานั้นเพิ่ม (ได้ ≥70% คืน 80%)
    //   คณะที่ไม่ใช้หลักสูตร: +10 คงที่ → สอบเสร็จ -5
    //   กันคิดซ้ำ (โหลดเซฟวันเดียวกัน) ด้วยคีย์ใน AcademicRecord.stressKeys: "วันเกม|รหัสวิชา|จำนวน" / "วันเกม|รหัสวิชา|done" / "วันเกม|*"
    public static class ExamStress
    {
        public const float MinPerCourse = 2f;
        public const float MaxPerCourse = 8f;
        public const float DayCap = 30f;
        public const float ReliefShare = 0.5f;
        public const float GoodReliefShare = 0.8f;
        public const float GoodScore = 0.7f;
        public const float LegacyAnxiety = 10f;
        public const float LegacyRelief = -5f;
        const string LegacyCode = "_ALL";

        // ---------- ตรรกะล้วน ----------
        public static float PerCourse(float studyRatio) => Mathf.Lerp(MaxPerCourse, MinPerCourse, Mathf.Clamp01(studyRatio));

        // ความกังวลต่อวิชา หลังบีบให้รวมไม่เกิน DayCap
        public static List<float> Plan(IList<float> ratios)
        {
            var list = new List<float>();
            float sum = 0f;
            foreach (var r in ratios) { float a = PerCourse(r); list.Add(a); sum += a; }
            if (sum > DayCap)
            {
                float k = DayCap / sum;
                for (int i = 0; i < list.Count; i++) list[i] *= k;
            }
            return list;
        }

        public static float Relief(float anxiety, float score01) =>
            -Mathf.Max(0f, anxiety) * (score01 >= GoodScore ? GoodReliefShare : ReliefShare);

        // ---------- ผูกกับเกม ----------
        // session-only สำรองเมื่อไม่มี AcademicRecord
        static readonly List<string> fallbackKeys = new List<string>();

        static List<string> Keys()
        {
            var rec = CourseRegistrar.Instance != null ? CourseRegistrar.Instance.Record : null;
            if (rec == null) return fallbackKeys;
            if (rec.stressKeys == null) rec.stressKeys = new List<string>();
            return rec.stressKeys;
        }

        static int Today()
        {
            var clock = Object.FindFirstObjectByType<NisitSimulator.TimeSystem.GameClock>();
            return clock != null ? clock.Day : 0;
        }

        static PlayerStats Stats() => Object.FindFirstObjectByType<PlayerStats>();

        static string Prefix(int day, string code) => day.ToString(CultureInfo.InvariantCulture) + "|" + code + "|";

        // เรียกจาก ExamController ตอนวันนี้มีสอบ (ซ้ำได้ — คิดครั้งเดียวต่อวัน)
        public static void OnExamDay(bool final)
        {
            var stats = Stats();
            if (stats == null) return;
            var keys = Keys();
            int day = Today();
            string dayMark = day.ToString(CultureInfo.InvariantCulture) + "|*";
            if (keys.Contains(dayMark)) return;
            keys.Add(dayMark);
            PruneOld(keys, day);

            var reg = CourseRegistrar.Instance;
            float total = 0f;
            if (reg != null && reg.IsActive && reg.Service != null)
            {
                var codes = new List<string>();
                var ratios = new List<float>();
                int upto = Mathf.Max(0, reg.SemDay - 1);
                foreach (var e in reg.Service.CurrentEnrollments())
                {
                    if (e.graded || RegistrationService.HasExamScore(e, final)) continue;
                    codes.Add(e.code);
                    ratios.Add(reg.Service.StudyRatio(e, upto));
                }
                var plan = Plan(ratios);
                for (int i = 0; i < codes.Count; i++)
                {
                    keys.Add(Prefix(day, codes[i]) + plan[i].ToString("0.###", CultureInfo.InvariantCulture));
                    total += plan[i];
                }
            }
            else
            {
                keys.Add(Prefix(day, LegacyCode) + LegacyAnxiety.ToString(CultureInfo.InvariantCulture));
                total = LegacyAnxiety;
            }
            if (total <= 0f) return;
            stats.ChangeStress(total);
            Debug.Log($"[Stress] กังวลก่อนสอบ +{total:0.0}");
        }

        // ส่งข้อสอบวิชาหนึ่งแล้ว (score 0..1) → คืนความเครียดบางส่วน
        public static void OnCourseExamDone(string code, float score01)
        {
            if (string.IsNullOrEmpty(code) || score01 < 0f) return;
            var stats = Stats();
            if (stats == null) return;
            var keys = Keys();
            int day = Today();
            string prefix = Prefix(day, code);
            if (keys.Contains(prefix + "done")) return;
            float anxiety = -1f;
            foreach (var k in keys)
                if (k.StartsWith(prefix) && float.TryParse(k.Substring(prefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) { anxiety = v; break; }
            if (anxiety <= 0f) return;
            keys.Add(prefix + "done");
            stats.ChangeStress(Relief(anxiety, score01));
        }

        // คณะที่ไม่ใช้หลักสูตร: สอบเสร็จ → -5
        public static void OnLegacyExamDone()
        {
            var stats = Stats();
            if (stats == null) return;
            var keys = Keys();
            string prefix = Prefix(Today(), LegacyCode);
            if (keys.Contains(prefix + "done")) return;
            bool had = false;
            foreach (var k in keys) if (k.StartsWith(prefix)) { had = true; break; }
            if (!had) return;
            keys.Add(prefix + "done");
            stats.ChangeStress(LegacyRelief);
        }

        // เก็บเฉพาะคีย์ 3 วันล่าสุด (กันเซฟโตเรื่อย ๆ)
        static void PruneOld(List<string> keys, int today)
        {
            keys.RemoveAll(k =>
            {
                int bar = k.IndexOf('|');
                return bar > 0 && int.TryParse(k.Substring(0, bar), NumberStyles.Integer, CultureInfo.InvariantCulture, out int d) && d < today - 2;
            });
        }
    }
}
