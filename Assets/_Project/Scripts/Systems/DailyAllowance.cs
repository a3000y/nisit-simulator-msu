using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Systems
{
    // แหล่งเงิน — เงินตั้งต้น + ค่าขนมรายวัน (ให้ร้านค้ามีเงินหมุน)
    public class DailyAllowance : MonoBehaviour
    {
        public int startMoney = 100;
        public int perDay = 60;

        private GameClock clock;
        private PlayerStats stats;
        private int lastDay;

        void Start()
        {
            stats = Object.FindFirstObjectByType<PlayerStats>();
            clock = Object.FindFirstObjectByType<GameClock>();
            // ให้เงินตั้งต้นเฉพาะเกมใหม่ — ถ้า "เล่นต่อ" เงินคืนจากเซฟแล้ว (กันบวกซ้ำ)
            if (stats != null && startMoney != 0 && !GameSession.IsContinue)
            {
                int ng = PlayerPrefs.GetInt("nisit_ngplus", 0);   // โบนัสตามรอบ New Game+
                int bonus = ng * 100;
                stats.ChangeMoney(startMoney + bonus);
                if (bonus > 0) HUDController.Toast($"โบนัส New Game+ รอบ {ng}: +{bonus}฿");
            }
            if (clock != null) { lastDay = clock.Day; clock.OnDayChanged += OnDay; }
        }

        void OnDestroy() { if (clock != null) clock.OnDayChanged -= OnDay; }

        void OnDay(int day)
        {
            if (day <= lastDay) return;
            lastDay = day;
            if (stats != null) { stats.ChangeMoney(perDay); HUDController.Toast($"ได้เงินค่าขนม +{perDay}฿"); }
        }
    }
}
