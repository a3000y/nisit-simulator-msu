using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.Player;
using NisitSimulator.UI;

namespace NisitSimulator.Interaction
{
    // จุดนอนบนเตียง — กด E → ข้ามเวลาไปเช้า + ฟื้นพลังงาน (นอนได้เฉพาะช่วงเย็น/ค่ำ)
    public class SleepStation : MonoBehaviour, IInteractable
    {
        public int sleepFromHour = 18;   // นอนได้ตั้งแต่กี่โมง (เย็น)
        public int wakeHour = 7;         // ตื่นกี่โมง

        private GameClock _clock;
        private GameClock Clock => _clock != null ? _clock : (_clock = Object.FindFirstObjectByType<GameClock>());

        // นอนได้เฉพาะ เย็น(>=18) ถึง เช้ามืด(<7)
        private bool CanSleep()
        {
            int h = Clock != null ? Clock.Hour : sleepFromHour;
            return h >= sleepFromHour || h < wakeHour;
        }

        public string GetPrompt() =>
            CanSleep() ? "กด E เพื่อนอน (ข้ามไปเช้า)" : $"ยังไม่ถึงเวลานอน (หลัง {sleepFromHour}:00 น.)";

        public void Interact(GameObject interactor)
        {
            if (!CanSleep())
            {
                HUDController.Toast($"ยังไม่ง่วง! นอนได้หลัง {sleepFromHour}:00 น.");
                return;
            }

            var action = interactor.GetComponent<PlayerActionController>()
                         ?? interactor.AddComponent<PlayerActionController>();
            if (action.IsBusy) { HUDController.Toast("กำลังทำกิจกรรมอยู่"); return; }

            NisitSimulator.Core.SFXManager.Sleep();
            action.PerformState(2f, () => WakeUp(interactor), "Sleeping_A", "Sleeping_B");   // นอนท่าสุ่มก่อน
        }

        private void WakeUp(GameObject interactor)
        {
            if (Clock != null) Clock.SkipToNextMorning(wakeHour);
            if (interactor.TryGetComponent<PlayerStats>(out var s))
            {
                s.ChangeEnergy(999f);       // เต็ม
                s.ChangeHealth(12f);
                s.ChangeHunger(-25f);       // ตื่นมาหิว → ต้องไปกินข้าว
                s.ChangeSatisfaction(5f);
            }
            HUDController.Toast("หลับสบาย! พลังงานเต็ม (แต่หิวแล้ว)");
        }
    }
}
