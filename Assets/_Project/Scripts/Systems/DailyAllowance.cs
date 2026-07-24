using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;

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
            if (stats != null && startMoney != 0) stats.ChangeMoney(startMoney);
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
