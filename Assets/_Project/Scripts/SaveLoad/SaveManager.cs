using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.Systems;
using NisitSimulator.TimeSystem;
using NisitSimulator.Player;
using NisitSimulator.Interaction;

namespace NisitSimulator.SaveLoad
{
    // รวบรวม/คืนค่าสถานะเกมทั้งหมด ↔ ไฟล์เซฟ
    public static class SaveManager
    {
        // เก็บสถานะปัจจุบันลงไฟล์
        public static void Save()
        {
            if (NisitSimulator.UI.OnboardingFlow.IsPractice) return;
            var stats = Object.FindFirstObjectByType<PlayerStats>();
            if (stats == null) return;

            var data = new SaveData
            {
                energy = stats.Energy,
                health = stats.Health,
                hunger = stats.Hunger,
                knowledge = stats.Knowledge,
                satisfaction = stats.Satisfaction,
                stress = stats.Stress,
                money = stats.Money,
                exp = stats.Exp
            };

            var prog = Object.FindFirstObjectByType<ProgressionManager>();
            if (prog != null) { data.currentYear = prog.CurrentYear; data.dayInYear = prog.DayInYear; data.calendarYear = prog.CalendarYear; }
            data.calendarVersion = AcademicCalendar.Version;

            // ระบบลงทะเบียนเรียน (ชั้นปี ภาค รายวิชา ความคืบหน้า ผลสอบ ประวัติเรียนซ้ำ)
            var registrar = NisitSimulator.Academics.CourseRegistrar.Instance;
            if (registrar != null) registrar.CollectSave(data);

            // มินิเกมสอบที่กำลังทำ (ชุดข้อ/ลำดับ/คำตอบ/เวลาคงเหลือ/คำใบ้)
            var miniExam = NisitSimulator.Academics.ExamMinigame.ExamMinigameController.Instance;
            if (miniExam != null) miniExam.CollectSave(data);

            data.facultyIndex = GameSession.SelectedFacultyIndex;   // เก็บคณะลงเซฟ

            // ตำแหน่ง + ทิศ + ฉาก + อาคารที่อยู่ (ระบบจุดเกิดหอพัก — Continue กลับมาที่เดิม ไม่ถูกบังคับกลับหอ)
            PlayerSpawnSystem.CollectSave(data);

            var inv = Object.FindFirstObjectByType<InventoryManager>();
            if (inv != null) data.inventoryItemIds = inv.ToSaveList();   // เก็บไอเทมในกระเป๋า

            var exam = Object.FindFirstObjectByType<ExamController>();
            if (exam != null) { data.gradePoints = exam.GetGradePoints(); data.doneExams = exam.GetDoneExams(); }  // ประวัติเกรด (GPA) + สอบที่ทำแล้ว

            var clock = Object.FindFirstObjectByType<GameClock>();
            if (clock != null) { data.gameDay = clock.Day; data.gameMinutes = clock.TotalMinutes; }  // เวลา

            // เควสรายวัน + ตัวสะสม
            var quest = Object.FindFirstObjectByType<QuestSystem>();
            if (quest != null) quest.CollectSave(data);

            // ภารกิจ GoTo ที่ค้าง
            var evt = Object.FindFirstObjectByType<EventManager>();
            if (evt != null) evt.CollectObjective(data);

            // ผลกระทบทั้งวัน (ป่วย/ไฟแรง)
            var fx = Object.FindFirstObjectByType<PlayerEffects>();
            if (fx != null) { data.fxMove = fx.moveMult; data.fxDrain = fx.energyDrainMult; data.fxKnow = fx.knowledgeMult; }

            // สถานะหมดแรง (กันโหลดแล้ววิ่งได้ทั้งที่ยังหมดแรง)
            var exh = stats.GetComponent<PlayerExhaustion>();
            data.exhausted = exh != null && exh.IsExhausted;

            // ความสัมพันธ์กับ NPC
            var rel = Object.FindFirstObjectByType<RelationshipManager>();
            if (rel != null) rel.CollectSave(data);

            // สถิติ + ความสำเร็จ
            var st2 = Object.FindFirstObjectByType<StatsTracker>();
            if (st2 != null) st2.CollectSave(data);
            var ach = Object.FindFirstObjectByType<AchievementManager>();
            if (ach != null) ach.CollectSave(data);
            var lvl = Object.FindFirstObjectByType<LevelSystem>();
            if (lvl != null) lvl.CollectSave(data);

            // เข้าเรียนของวันนี้ (ทุกห้อง)
            data.classAttendance = new List<string>();
            foreach (var cs in Object.FindObjectsByType<ClassStation>(FindObjectsSortMode.None))
                cs.CollectAttendance(data.classAttendance);

            SaveSystem.Save(data);
        }

        // ถ้าเมนูสั่ง "เล่นต่อ" → โหลดเซฟมาใส่
        //   คืน SaveData ที่โหลด (null = เกมใหม่/ไม่มีเซฟ) — "ไม่" ย้ายผู้เล่นที่นี่
        //   GameplayBootstrap ส่งต่อให้ PlayerSpawnSystem.ResolveInitialSpawn ซึ่งเป็นที่เดียวที่วางตัวละครตอนเข้าฉาก
        //   (กันสคริปต์จุดเกิดย้ายตัวละครกลับหอหลังคืนตำแหน่งจากเซฟแล้ว)
        public static SaveData ApplyIfPending()
        {
            if (!GameSession.PendingLoad) return null;
            GameSession.PendingLoad = false;

            var data = SaveSystem.Load();
            if (data == null) { GameSession.IsContinue = false; return null; }
            CalendarMigration.Upgrade(data);   // เซฟปฏิทินเก่า (ปีละ 8 วัน) → แปลงวันก่อนทุกระบบอ่าน

            GameSession.SelectedFacultyIndex = data.facultyIndex;   // คืนคณะจากเซฟ (ไม่รีเซ็ตเป็น IT อีก)

            var stats = Object.FindFirstObjectByType<PlayerStats>();
            if (stats != null)
            {
                stats.LoadState(data.energy, data.health, data.hunger, data.knowledge,
                                data.satisfaction, data.money, data.exp, data.stress);
                // พลังงาน 0 / เซฟที่หมดแรงค้าง → เข้าสถานะหมดแรง (ไม่ใช่ Game Over) · ไม่ฟื้นพลัง/ไม่ข้ามเวลา
                PlayerExhaustion.EnsureOn(stats.gameObject).RestoreFromSave(data.exhausted);
            }

            // คืน "สอบที่ทำแล้ว" ก่อน RestoreState (เพราะ RestoreState จะยิง OnDayInYearChanged → เช็กว่าจะเปิดสอบไหม)
            var exam = Object.FindFirstObjectByType<ExamController>();
            if (exam != null) exam.RestoreDoneExams(data.doneExams);

            // คืนข้อมูลลงทะเบียนก่อน RestoreState (RestoreState ยิง event วัน → ระบบลงทะเบียนต้องรู้ภาคปัจจุบันแล้ว ไม่งั้นจะเปิดภาคซ้ำ)
            NisitSimulator.Academics.CourseRegistrar.EnsureExists().RestoreFrom(data);

            var prog = Object.FindFirstObjectByType<ProgressionManager>();
            if (prog != null) prog.RestoreState(data.currentYear, data.dayInYear, data.calendarYear);

            var inv = Object.FindFirstObjectByType<InventoryManager>();
            if (inv != null) inv.LoadFromList(data.inventoryItemIds);   // คืนไอเทมในกระเป๋า

            if (exam != null) exam.RestoreGrades(data.gradePoints);     // คืน GPA

            var clock = Object.FindFirstObjectByType<GameClock>();
            if (clock != null) clock.RestoreClock(data.gameDay, data.gameMinutes);  // คืนเวลา

            // ตำแหน่งผู้เล่นคืนโดย PlayerSpawnSystem.ResolveInitialSpawn หลังฉากพร้อม (ตรวจว่าใช้งานได้ก่อน ไม่งั้นใช้จุดเกิดหอพัก)

            // มินิเกมสอบที่ค้าง (หลังคืนหลักสูตร/วัน/เวลา) — ไม่สุ่มข้อใหม่ ไม่คืนคำใบ้ ไม่เพิ่มเวลา
            NisitSimulator.Academics.ExamMinigame.ExamMinigameController.EnsureExists().RestoreFrom(data);

            Debug.Log("[SaveManager] โหลดเซฟเรียบร้อย");
            return data;
        }
    }
}
