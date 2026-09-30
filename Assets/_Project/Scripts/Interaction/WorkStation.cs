using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.Player;
using NisitSimulator.UI;

namespace NisitSimulator.Interaction
{
    // งานพาร์ทไทม์ — เดินไปที่ทำงาน กด E → ตัวละครทำงานจริง → ได้ค่าจ้าง (เสียพลังงาน, จำกัดกะต่อวัน)
    // ใส่ไว้ที่วัตถุจุดทำงาน (Layer = Interactable + Collider)
    public class WorkStation : MonoBehaviour, IInteractable
    {
        [Header("ข้อมูลงาน")]
        public string jobName = "ร้านกาแฟ";
        public int wage = 60;
        public float energyCost = 25f;
        public float workSeconds = 3f;
        public int shiftsPerDay = 2;

        [Header("ผลเสริม")]
        public float knowledgeBonus = 0f;
        public float satisfactionChange = -2f;
        public int expReward = 10;

        private GameClock clock;
        private int shiftsToday;
        int Wage => Mathf.RoundToInt(wage * NisitSimulator.Systems.Perks.WageMul);   // + "ขยันทำงาน"

        void Start()
        {
            clock = Object.FindFirstObjectByType<GameClock>();
            if (clock != null) clock.OnDayChanged += _ => shiftsToday = 0;
        }

        public string GetPrompt()
        {
            if (shiftsToday >= shiftsPerDay) return $"{jobName}: วันนี้ทำครบแล้ว";
            return $"กด E ทำงาน {jobName} (+{Wage}฿ / พลังงาน -{energyCost:0})";
        }

        public void Interact(GameObject interactor)
        {
            if (shiftsToday >= shiftsPerDay)
            {
                HUDController.Toast($"วันนี้ทำงาน {jobName} ครบแล้ว ({shiftsPerDay} กะ)");
                return;
            }
            if (interactor.TryGetComponent<PlayerStats>(out var pre) && pre.Energy < energyCost)
            {
                HUDController.Toast("พลังงานไม่พอทำงาน ไปพักผ่อนก่อน");
                return;
            }

            var action = interactor.GetComponent<PlayerActionController>()
                         ?? interactor.AddComponent<PlayerActionController>();
            if (action.IsBusy) { HUDController.Toast("กำลังทำกิจกรรมอยู่"); return; }

            shiftsToday++;
            HUDController.Toast($"กำลังทำงาน {jobName}...");
            action.PerformState(workSeconds, () => Grant(interactor), "Typing");
        }

        private void Grant(GameObject interactor)
        {
            if (interactor.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.ChangeMoney(Wage);
                stats.ChangeEnergy(-energyCost);
                if (knowledgeBonus != 0) stats.ChangeKnowledge(knowledgeBonus);
                if (satisfactionChange != 0) stats.ChangeSatisfaction(satisfactionChange);
                if (expReward != 0) stats.AddExp(expReward);
                HUDController.Toast($"ทำงานเสร็จ! ได้ค่าจ้าง +{Wage}฿");
                NisitSimulator.Systems.GameplayEvents.Raise(NisitSimulator.Systems.GameplayEvents.Work);
            }
        }
    }
}
