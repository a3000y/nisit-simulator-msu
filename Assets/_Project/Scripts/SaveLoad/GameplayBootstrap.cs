using UnityEngine;
using NisitSimulator.Core;
using NisitSimulator.TimeSystem;
using NisitSimulator.Systems;
using NisitSimulator.UI;

namespace NisitSimulator.SaveLoad
{
    // จัดการเซฟ/โหลดในฉากเกม — ใส่ไว้ที่ GameManager
    // โหลดเซฟตอนเข้าเกม (ถ้ากด "เล่นต่อ") + ออโต้เซฟทุกวัน + ลบเซฟเมื่อจบเกม
    public class GameplayBootstrap : MonoBehaviour
    {
        private GameClock clock;

        void Start()
        {
            SaveManager.ApplyIfPending();

            // สร้างระบบสถิติ/ความสำเร็จ + หน้ากด J (หลังโหลดเซฟ → baseline/คืนค่าถูกต้อง)
            _ = StatsTracker.Instance;
            _ = AchievementManager.Instance;
            AchievementsUI.EnsureExists();

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
