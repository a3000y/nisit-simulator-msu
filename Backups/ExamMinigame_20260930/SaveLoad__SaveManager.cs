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

            // ระบบลงทะเบียนเรียน (ชั้นปี ภาค รายวิชา ความคืบหน้า ผลสอบ ประวัติเรียนซ้ำ)
            var registrar = NisitSimulator.Academics.CourseRegistrar.Instance;
            if (registrar != null) registrar.CollectSave(data);

            data.facultyIndex = GameSession.SelectedFacultyIndex;   // เก็บคณะลงเซฟ

            var player = GameObject.Find("Player");
            if (player != null)
            {
                var p = player.transform.position;
                data.posX = p.x; data.posY = p.y; data.posZ = p.z;
            }

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
        public static void ApplyIfPending()
        {
            if (!GameSession.PendingLoad) return;
            GameSession.PendingLoad = false;

            var data = SaveSystem.Load();
            if (data == null) return;

            GameSession.SelectedFacultyIndex = data.facultyIndex;   // คืนคณะจากเซฟ (ไม่รีเซ็ตเป็น IT อีก)

            var stats = Object.FindFirstObjectByType<PlayerStats>();
            if (stats != null)
                stats.LoadState(data.energy, data.health, data.hunger, data.knowledge,
                                data.satisfaction, data.money, data.exp, data.stress);

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

            var player = GameObject.Find("Player");
            if (player != null)
                MovePlayer(player, new Vector3(data.posX, data.posY, data.posZ));

            Debug.Log("[SaveManager] โหลดเซฟเรียบร้อย");
        }

        // ย้ายตัวละคร (ต้องปิด CharacterController ชั่วคราวก่อนตั้งตำแหน่ง)
        private static void MovePlayer(GameObject player, Vector3 pos)
        {
            if (pos == Vector3.zero) return; // ยังไม่เคยเซฟตำแหน่ง
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) { cc.enabled = false; player.transform.position = pos; cc.enabled = true; }
            else player.transform.position = pos;
        }
    }
}
