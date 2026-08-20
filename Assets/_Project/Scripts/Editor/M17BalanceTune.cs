#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.Player;
using NisitSimulator.TimeSystem;

namespace NisitSimulator.EditorTools
{
    // ⚖️ จูนบาลานซ์ลงฉากจริง (ค่าที่เซฟไว้ในฉากจะถูกทับด้วยค่าใหม่ที่พอดีกับเวลาช้า)
    //   ปรับ: ความเร็วเวลา + อัตราหิว/หมดแรง + พลังงานตอนเดิน/วิ่ง
    // ใช้: เมนู  Nisit -> Balance Tune
    public static class M17BalanceTune
    {
        const string GameplayPath = "Assets/_Project/Scenes/01_Gameplay.unity";

        [MenuItem("Nisit/Balance Tune")]
        public static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);

            var log = "";

            // เวลา: ติ๊กเหมือนนาฬิกาจริง
            var clock = Object.FindFirstObjectByType<GameClock>();
            if (clock != null) { clock.gameMinutesPerRealSecond = 1f; EditorUtility.SetDirty(clock); log += "• เวลา = 1 นาที/วินาที\n"; }

            // หิว/หมดแรง (ต่อวินาที)
            var decay = Object.FindFirstObjectByType<StatDecay>();
            if (decay != null)
            {
                decay.hungerDecayPerSec = 0.12f;
                decay.energyDecayPerSec = 0.05f;
                decay.starvingEnergyDrain = 0.4f;
                decay.starvingHealthDrain = 0.25f;
                EditorUtility.SetDirty(decay);
                log += "• หิว 0.12/วิ · เหนื่อย 0.05/วิ\n";
            }

            // พลังงานตอนเคลื่อนที่
            var move = Object.FindFirstObjectByType<PlayerMovement>();
            if (move != null)
            {
                move.walkEnergyDrain = 0.15f;
                move.runEnergyDrain = 0.4f;
                EditorUtility.SetDirty(move);
                log += "• เดินเปลือง 0.15/วิ · วิ่ง 0.4/วิ\n";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("<color=lime>[Nisit] จูนบาลานซ์ลงฉากแล้ว</color>\n" + log);
            EditorUtility.DisplayDialog("Nisit Simulator",
                "จูนบาลานซ์เรียบร้อย! ⚖️\n\n" + (string.IsNullOrEmpty(log) ? "ไม่พบคอมโพเนนต์ในฉาก (สร้าง HUD/Player ก่อน)" : log) +
                "\nกด Play ทดสอบได้เลย — หิว/หมดแรงช้าลง เล่นสบายขึ้น\n(ตกเย็นไปนอนเพื่อข้ามวัน)", "เยี่ยม!");
        }
    }
}
#endif
