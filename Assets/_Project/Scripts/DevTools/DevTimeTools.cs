#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using NisitSimulator.Core;
using NisitSimulator.Stats;
using NisitSimulator.Systems;
using NisitSimulator.TimeSystem;
using NisitSimulator.Interaction;
using NisitSimulator.Academics;
using NisitSimulator.Academics.ExamMinigame;

namespace NisitSimulator.DevTools
{
    // ===== เครื่องมือเวลาและตำแหน่ง (Dev) =====
    //   [เปลี่ยนเวลาโดยตรง]  GameClock.RestoreClock / ProgressionManager.RestoreState — เส้นทางเดียวกับตอนโหลดเซฟ
    //       ไม่ยิง OnDayChanged → ไม่มีค่าขนม ไม่ autosave ไม่ตัดสินขาดสอบของวันที่ข้าม ไม่ลดสถานะตามเวลาที่ข้าม
    //       (แต่เปลี่ยน \"วันในปี\" จะยิงเหตุการณ์วัน: ระบบลงทะเบียน sync ภาค/ปิดช่วงลงทะเบียน · ระบบสอบตั้งตารางสอบของวันนั้น)
    //   [จำลองเวลาเดินผ่าน] GameClock.AdvanceMinutes + StatDecay.Tick ทีละ 1 นาทีเกม — ขั้นตอนเดียวกับเวลาเดินปกติ
    //       ข้ามวันยิง OnDayChanged ครั้งเดียวต่อวัน → ค่าขนม/autosave(ลงเซฟทดสอบ)/ขาดสอบ/ปิดภาค/ประกาศเกรด ทำงานตามจริง ไม่ซ้ำ
    //   ความเร็ว x1/x5/x10 เปลี่ยน GameClock.gameMinutesPerRealSecond เท่านั้น (ไม่แตะ Time.timeScale → ตัวจับเวลาสอบใช้ unscaled time เดินเท่าเดิม)
    public static class DevTimeTools
    {
        static float baseSpeed = -1f;
        static GameClock speedClock;
        public static int SpeedMult { get; private set; } = 1;
        public static bool Simulating { get; private set; }
        public static string Status = "";
        static bool cancel;

        public static GameClock Clock => UnityEngine.Object.FindFirstObjectByType<GameClock>();
        public static ProgressionManager Prog => UnityEngine.Object.FindFirstObjectByType<ProgressionManager>();

        public static void ResetSpeedState() { baseSpeed = -1f; speedClock = null; SpeedMult = 1; cancel = true; Simulating = false; Status = ""; }

        public static string TimeText(float minutes)
        {
            int m = Mathf.FloorToInt(minutes);
            return $"{(m / 60) % 24:00}:{m % 60:00}";
        }

        // ---------- ความเร็วเวลา ----------
        public static bool SetSpeed(int k, out string msg)
        {
            var c = Clock;
            if (c == null) { msg = "ไม่พบ GameClock"; return false; }
            if (speedClock != c) { speedClock = c; baseSpeed = c.gameMinutesPerRealSecond / Mathf.Max(1, SpeedMult); if (baseSpeed <= 0f) baseSpeed = 3f; }
            c.gameMinutesPerRealSecond = baseSpeed * k;
            SpeedMult = k;
            msg = $"ความเร็วเวลาโลก x{k} ({c.gameMinutesPerRealSecond:0.##} นาทีเกม/วินาทีจริง) — ตัวจับเวลาสอบไม่เปลี่ยน (unscaled)";
            return true;
        }

        // ---------- เปลี่ยนเวลาโดยตรง ----------
        public static bool SetTimeOfDay(int hour, int minute, out string msg)
        {
            var c = Clock;
            if (c == null) { msg = "ไม่พบ GameClock"; return false; }
            if (BlockedByExam(out msg)) return false;
            hour = Mathf.Clamp(hour, 0, 23); minute = Mathf.Clamp(minute, 0, 59);
            float before = c.TotalMinutes;
            c.RestoreClock(c.Day, hour * 60 + minute);
            msg = $"[ตั้งโดยตรง] เวลา {TimeText(before)} → {TimeText(c.TotalMinutes)} (วันเดิม · ไม่ยิงเหตุการณ์ข้ามวัน)";
            return true;
        }

        public static bool SetAcademicDay(int calendarYear, int dayInYear, out string msg)
        {
            var p = Prog;
            if (p == null) { msg = "ไม่พบ ProgressionManager"; return false; }
            if (BlockedByExam(out msg)) return false;
            dayInYear = Mathf.Clamp(dayInYear, 1, p.daysPerYear);
            calendarYear = Mathf.Max(1, calendarYear);
            string before = $"ปีการศึกษา {p.CalendarYear} วัน {p.DayInYear}";
            p.RestoreState(p.CurrentYear, dayInYear, calendarYear);
            msg = $"[ตั้งโดยตรง] {before} → ปีการศึกษา {p.CalendarYear} วัน {p.DayInYear} ({AcademicCalendar.SemesterName(AcademicCalendar.SemesterIndex(p.DayInYear))} วันที่ {AcademicCalendar.SemesterDay(p.DayInYear)}) · ระบบลงทะเบียน/ตารางสอบ sync ตามวันใหม่ · ไม่มีค่าขนม/ขาดสอบของวันที่ข้าม";
            return true;
        }

        public static bool JumpInSemester(int semDay, int hour, out string msg)
        {
            var p = Prog;
            if (p == null) { msg = "ไม่พบ ProgressionManager"; return false; }
            int sem = AcademicCalendar.SemesterIndex(p.DayInYear);
            if (!SetAcademicDay(p.CalendarYear, DevHistoryBuilder.DayInYear(sem, semDay), out msg)) return false;
            SetTimeOfDay(hour, 0, out var m2);
            msg += " · " + m2;
            return true;
        }

        static bool BlockedByExam(out string msg)
        {
            msg = null;
            var mini = ExamMinigameController.Instance;
            if (mini != null && mini.HasSessionInProgress) { msg = "กำลังสอบ/Preview อยู่ — ส่งหรือเลิกสอบก่อนเปลี่ยนเวลา (กันตัวจับเวลาสอบ/รอบสอบเสียหาย)"; return true; }
            var exam = UnityEngine.Object.FindFirstObjectByType<ExamController>();
            if (exam != null && exam.IsOpen) { msg = "หน้าสอบแบบเดิมเปิดอยู่"; return true; }
            return false;
        }

        // ---------- จำลองเวลาเดินผ่าน ----------
        public static void Cancel() { cancel = true; }

        public static bool StartSimulation(MonoBehaviour host, float minutes, string label, Func<bool> stopWhen, out string msg)
        {
            if (Simulating) { msg = "กำลังจำลองเวลาอยู่"; return false; }
            if (Clock == null) { msg = "ไม่พบ GameClock"; return false; }
            if (BlockedByExam(out msg)) return false;
            var gm = GameManager.Instance;
            if (gm != null && (gm.State == GameState.GameOver || gm.State == GameState.Win)) { msg = "เกมจบแล้ว"; return false; }
            host.StartCoroutine(Run(minutes, label, stopWhen));
            msg = $"[จำลองเวลา] {label} — เดินเวลาผ่านขั้นตอนปกติของนาฬิกา (ข้ามวันยิงเหตุการณ์จริงครั้งเดียวต่อวัน)";
            return true;
        }

        // เดินเวลาทีละ 1 นาทีเกม (สูงสุด 30 นาที/เฟรม) + ลดสถานะตามเวลาเหมือนเวลาเดินปกติ · หยุดรอเมื่อมีหน้าต่างหยุดเวลา (สรุปวัน/เหตุการณ์)
        static IEnumerator Run(float minutes, string label, Func<bool> stopWhen)
        {
            Simulating = true; cancel = false;
            float left = minutes;
            int days = 0;
            var clock = Clock;
            Action<int> onDay = d => days++;
            clock.OnDayChanged += onDay;
            var decay = UnityEngine.Object.FindFirstObjectByType<StatDecay>();
            string endReason = "ครบเวลา";
            try
            {
                while (left > 0f)
                {
                    if (cancel) { endReason = "ยกเลิก"; break; }
                    if (clock == null) { endReason = "นาฬิกาถูกทำลาย (เปลี่ยนฉาก)"; break; }
                    var gm = GameManager.Instance;
                    if (gm != null && (gm.State == GameState.GameOver || gm.State == GameState.Win)) { endReason = "เกมจบ"; break; }
                    if (GameClock.Suspended) { endReason = "เข้าห้องสอบ (นาฬิกาหยุด)"; break; }
                    if (Time.timeScale == 0f || (gm != null && !gm.IsActive))
                    {
                        Status = $"{label}: รอปิดหน้าต่างที่หยุดเวลา (สรุปวัน/เหตุการณ์/หยุดเกม) — กด Space/Enter หรือคลิกปิด · เหลือ {left / 60f:0.0} ชม.";
                        yield return null;
                        continue;
                    }
                    float chunk = Mathf.Min(30f, left);
                    while (chunk > 0f)
                    {
                        float s = Mathf.Min(1f, chunk);
                        clock.AdvanceMinutes(s);
                        if (decay != null) decay.Tick(s);
                        chunk -= s; left -= s;
                        if (Time.timeScale == 0f || GameClock.Suspended) break;
                        if (stopWhen != null && stopWhen()) { left = 0f; endReason = "ถึงเป้าหมาย"; break; }
                    }
                    Status = $"{label}: วัน {clock.Day} {clock.GetTimeString()} · เหลือ {left / 60f:0.0} ชม. · ข้ามวันแล้ว {days}";
                    yield return null;
                }
            }
            finally
            {
                if (clock != null) clock.OnDayChanged -= onDay;
                Simulating = false;
                Status = "";
            }
            DevPanel.Log($"[จำลองเวลา] {label} จบ ({endReason}) · ข้ามวัน {days} ครั้ง · ตอนนี้วัน {(clock != null ? clock.Day : 0)} {(clock != null ? clock.GetTimeString() : "")}");
        }

        public static float MinutesUntil(float targetMinuteOfDay, bool nextDay)
        {
            var c = Clock; if (c == null) return 0f;
            float cur = c.TotalMinutes;
            float d = targetMinuteOfDay - cur;
            if (nextDay || d <= 0f) d += 24f * 60f;
            return d;
        }

        // ---------- กลางวัน–กลางคืน / หอพัก ----------
        public static bool WarpToDorm(out string msg)
        {
            var pl = Player;
            if (pl == null) { msg = "ไม่พบผู้เล่น"; return false; }
            if (BlockedByExam(out msg)) return false;
            if (!NisitSimulator.SaveLoad.PlayerSpawnSystem.SpawnAtDorm(pl, 0)) { msg = "ไม่พบ DormSpawnPoint ในฉาก"; return false; }
            msg = "วาร์ปไปโถง Dorm_Building (DormSpawnPoint) — อยู่ในโลกภายนอก";
            return true;
        }

        // ทดสอบนอนผ่านเส้นทางจริงของ SleepController (ข้ามหน้ายืนยันและเงื่อนไข "หลัง 18:00" เท่านั้น — ยังกันกรณีสอบ/หน้าต่างหยุดเวลา)
        public static bool TestSleep(out string msg)
        {
            var pl = Player;
            var st = UnityEngine.Object.FindFirstObjectByType<SleepStation>();
            if (pl == null || st == null) { msg = "ไม่พบผู้เล่น/เตียง (SleepStation)"; return false; }
            if (BlockedByExam(out msg)) return false;
            var c = Clock;
            if (c == null) { msg = "ไม่พบ GameClock"; return false; }
            var sc = SleepController.EnsureExists();
            if (SleepController.IsSleeping) { msg = "กำลังนอนอยู่"; return false; }
            GameClock.ComputeWake(c.Day, c.TotalMinutes, st.wakeHour, out int wakeDay);
            string before = $"วันที่ {c.Day} {TimeText(c.TotalMinutes)}";
            sc.RequestSleep(st, pl, true);
            if (!SleepController.IsSleeping) { msg = "นอนไม่ได้ในสถานะนี้ (ดูข้อความแจ้งเตือน)"; return false; }
            msg = $"[ทดสอบนอน] {before} → ตื่นวันที่ {wakeDay} {st.wakeHour:00}:00 · บันทึกลงโปรไฟล์ทดสอบ (ไม่ทับเซฟจริง)";
            return true;
        }

        // ---------- วาร์ป ----------
        public static GameObject Player => GameObject.Find("Player");

        public static bool WarpToDoor(BuildingDoor door, out string msg)
        {
            var pl = Player;
            if (pl == null || door == null) { msg = "ไม่พบผู้เล่น/ประตู"; return false; }
            if (BlockedByExam(out msg)) return false;
            var im = InteriorManager.Instance;
            if (im != null && im.IsInside) im.Exit();
            door.Interact(pl);   // เส้นทางเข้าอาคารจริง (InteriorManager จำจุดออก)
            msg = $"วาร์ปเข้า {door.activityName.Replace("เข้า", "")} ผ่านประตูจริง";
            return true;
        }

        public static bool WarpOutdoor(Vector3 pos, string label, out string msg)
        {
            var pl = Player;
            if (pl == null) { msg = "ไม่พบผู้เล่น"; return false; }
            if (BlockedByExam(out msg)) return false;
            var im = InteriorManager.Instance;
            if (im != null && im.IsInside) im.Exit();
            var cc = pl.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            pl.transform.position = pos;
            if (cc != null) cc.enabled = true;
            msg = "วาร์ปไป " + label;
            return true;
        }

        public static BuildingDoor DoorFor(string building)
        {
            foreach (var d in UnityEngine.Object.FindObjectsByType<BuildingDoor>(FindObjectsSortMode.None))
                if (d.interiorSpawn != null && d.interiorSpawn.name == "Spawn_" + building) return d;
            return null;
        }
    }
}
#endif
