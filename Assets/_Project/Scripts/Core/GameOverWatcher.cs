using UnityEngine;
using NisitSimulator.Stats;

namespace NisitSimulator.Core
{
    // เฝ้าดูสถานะวิกฤต — เมื่อสุขภาพหมด สั่งเกมเข้าสู่ Game Over
    //   พลังงานหมดไม่จบเกมแล้ว (PlayerStats ไม่ยิง OnCriticalState จากพลังงาน → เข้าสถานะหมดแรงของ PlayerExhaustion)
    // ใส่ไว้ที่ GameManager (หรือ object ใดก็ได้ในฉาก)
    public class GameOverWatcher : MonoBehaviour
    {
        private PlayerStats stats;

        void Start()
        {
            stats = Object.FindFirstObjectByType<PlayerStats>();
            if (stats != null)
                stats.OnCriticalState += HandleGameOver;
        }

        void OnDestroy()
        {
            if (stats != null)
                stats.OnCriticalState -= HandleGameOver;
        }

        private void HandleGameOver()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.EndGame(EndReason.Died);
            Debug.Log("<color=red>[GameOver] สุขภาพหมด! (ฉาก 6 - เสียชีวิต)</color>");
        }
    }
}
