using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.Systems;
using NisitSimulator.TimeSystem;

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
                money = stats.Money,
                exp = stats.Exp
            };

            var prog = Object.FindFirstObjectByType<ProgressionManager>();
            if (prog != null) { data.currentYear = prog.CurrentYear; data.dayInYear = prog.DayInYear; }

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
            if (exam != null) data.gradePoints = exam.GetGradePoints();  // ประวัติเกรด (GPA)

            var clock = Object.FindFirstObjectByType<GameClock>();
            if (clock != null) { data.gameDay = clock.Day; data.gameMinutes = clock.TotalMinutes; }  // เวลา

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
                                data.satisfaction, data.money, data.exp);

            var prog = Object.FindFirstObjectByType<ProgressionManager>();
            if (prog != null) prog.RestoreState(data.currentYear, data.dayInYear);

            var inv = Object.FindFirstObjectByType<InventoryManager>();
            if (inv != null) inv.LoadFromList(data.inventoryItemIds);   // คืนไอเทมในกระเป๋า

            var exam = Object.FindFirstObjectByType<ExamController>();
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
