using UnityEngine;
using NisitSimulator.Core;
using NisitSimulator.TimeSystem;
using NisitSimulator.Systems;
using NisitSimulator.UI;
using NisitSimulator.Interaction;

namespace NisitSimulator.SaveLoad
{
    // จัดการเซฟ/โหลดในฉากเกม — ใส่ไว้ที่ GameManager
    // โหลดเซฟตอนเข้าเกม (ถ้ากด "เล่นต่อ") + ออโต้เซฟทุกวัน + ลบเซฟเมื่อจบเกม
    public class GameplayBootstrap : MonoBehaviour
    {
        private GameClock clock;

        void Start()
        {
            // เข้าฉากนี้จากห้อง Multiplayer ไหม (เชื่อมต่ออยู่ตอนโหลดฉาก) → autosave แยกไฟล์ ไม่ทับ/ลบเซฟเล่นคนเดียว
            var netMgr = Unity.Netcode.NetworkManager.Singleton;
            GameSession.IsMultiplayerGame = netMgr != null && netMgr.IsListening;

            // ระบบลงทะเบียนเรียน — สร้างก่อนโหลดเซฟ เพื่อรับข้อมูลการเรียนคืน (เกมใหม่ = เริ่มปี 1 ภาค 1 เปิดลงทะเบียน)
            NisitSimulator.Academics.CourseRegistrar.EnsureExists();
            DayNightCycle.EnsureExists();                  // แสงกลางวัน–กลางคืน (ฟังนาฬิกาเกมก่อนคืนเวลาจากเซฟ)

            // สถานะหมดแรง (แทน Game Over เมื่อพลังงาน 0) — ใส่ให้ Player ก่อนโหลดเซฟ ถ้าในฉากยังไม่มีคอมโพเนนต์
            var playerStats = Object.FindFirstObjectByType<NisitSimulator.Stats.PlayerStats>();
            if (playerStats != null) NisitSimulator.Player.PlayerExhaustion.EnsureOn(playerStats.gameObject);

            // ===== ลำดับเข้าฉาก: คืนสถานะจากเซฟ → วางตัวละคร (ที่เดียว) → แสงตรงเวลา ก่อนเฟรมแรกถูกวาด =====
            GameClock.WarpMultiplier = 1f;                 // โหลดเซฟ/เข้าฉากใหม่ → ความเร็วเวลาปกติเสมอ
            var loaded = SaveManager.ApplyIfPending();     // null = เกมใหม่ (เวลาเริ่มตาม GameClock.startHour = 07:00)
            PlayerSpawnSystem.ResolveInitialSpawn(loaded); // เกมใหม่/เซฟใช้ไม่ได้ → หอพัก · เล่นต่อ → ตำแหน่งเดิม
            SleepController.EnsureExists();                // นอนพักที่หอพัก (หน้ายืนยัน + เฟด)
            NisitSimulator.Net.WorldTimeSync.EnsureExists(); // Multiplayer: Host คุมเวลาโลก (เล่นคนเดียว = ไม่ทำงาน)
            NisitSimulator.Net.PartyRuntime.EnsureExists(); // Phase 1: scene-local team HUD and map overlays
            RegistrationUI.EnsureExists();        // แอป "ลงทะเบียนเรียน" ในโทรศัพท์ (TAB)
            NisitSimulator.Academics.ExamMinigame.ExamMinigameController.EnsureExists();   // มินิเกมสอบรายวิชา (เปิดเฉพาะตอนสอบที่ห้องสอบ)
            // ห้องเรียน: เร่งเวลาตอนเข้าเรียน + เหตุการณ์สุ่มระหว่างเรียน (ทำงานเฉพาะเล่นคนเดียว — ตรวจเองทุกเฟรม)
            NisitSimulator.Academics.ClassWarpController.EnsureExists();
            NisitSimulator.Academics.ClassEventSystem.EnsureExists();

            // สร้างระบบสถิติ/ความสำเร็จ + หน้ากด J (หลังโหลดเซฟ → baseline/คืนค่าถูกต้อง)
            _ = StatsTracker.Instance;
            _ = AchievementManager.Instance;
            AchievementsUI.EnsureExists();
            FloatingTextSpawner.EnsureExists();   // เด้งตัวเลข +เงิน/+ความรู้/+EXP
            LevelSystem.EnsureExists();           // เลเวลนิสิต + เลือกความสามารถ (กด L)
            DaySummaryUI.EnsureExists();          // สรุปผลตอนจบวัน

            clock = Object.FindFirstObjectByType<GameClock>();
            if (clock != null) clock.OnDayChanged += OnDay;

            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += OnState;
        }

        void OnDestroy()
        {
            if (clock != null) clock.OnDayChanged -= OnDay;
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= OnState;
        }

        // ออโต้เซฟทุกครั้งที่ขึ้นวันใหม่
        private void OnDay(int day) => SaveManager.Save();

        // จบเกม (ตาย/ตก/จบ) → ลบเซฟ ไม่ให้ "เล่นต่อ" รอบที่จบไปแล้ว
        private void OnState(GameState s)
        {
            if (s == GameState.GameOver || s == GameState.Win)
                SaveSystem.DeleteSave();
        }

        // ปิดเกมกลางคัน → เซฟไว้ (รวมกรณีปิดตอนกด Pause อยู่ = State Paused ซึ่ง IsActive เป็น false)
        void OnApplicationQuit()
        {
            if (GameManager.Instance != null
                && GameManager.Instance.State != GameState.GameOver
                && GameManager.Instance.State != GameState.Win)
                SaveManager.Save();
        }
    }
}
