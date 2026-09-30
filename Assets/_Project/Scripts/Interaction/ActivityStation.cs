using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.Player;
using NisitSimulator.UI;

namespace NisitSimulator.Interaction
{
    // จุดทำกิจกรรมทั่วไป (โรงอาหาร=เพิ่มความอิ่ม, หอพัก=เพิ่มพลังงาน ฯลฯ)
    // ปรับค่าสถานะเมื่อกด E — ตั้งค่าได้จาก Inspector
    // ใส่ไว้ที่วัตถุในฉาก + Collider + Layer = Interactable
    public class ActivityStation : MonoBehaviour, IInteractable
    {
        [Header("ป้ายบอก")]
        [SerializeField] private string activityName = "ทำกิจกรรม";

        [Header("ท่าทาง (ว่าง = นั่งปกติ · หลายท่า = สุ่ม)")]
        public string[] actionStates;

        [Header("ผลของกิจกรรม (บวก=เพิ่ม, ลบ=ลด)")]
        [SerializeField] private float energyChange = 0f;
        [SerializeField] private float hungerChange = 0f;
        [SerializeField] private float healthChange = 0f;
        [SerializeField] private float knowledgeChange = 0f;
        [SerializeField] private float satisfactionChange = 0f;
        [SerializeField] private int expReward = 0;

        public string GetPrompt() => $"กด E เพื่อ{activityName}";

        public void Interact(GameObject interactor)
        {
            var action = interactor.GetComponent<PlayerActionController>()
                         ?? interactor.AddComponent<PlayerActionController>();
            if (action.IsBusy) { HUDController.Toast("กำลังทำกิจกรรมอยู่"); return; }

            HUDController.Toast($"กำลัง{activityName}...");
            if (actionStates != null && actionStates.Length > 0)
                action.PerformState(1.8f, () => Grant(interactor), actionStates);   // ท่าเจาะจง (สุ่ม)
            else
                action.Perform(1.8f, () => Grant(interactor));                       // นั่งปกติ
        }

        private void Grant(GameObject interactor)
        {
            if (interactor.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.ChangeEnergy(energyChange);
                stats.ChangeHunger(hungerChange);
                stats.ChangeHealth(healthChange);
                stats.ChangeKnowledge(knowledgeChange);
                stats.ChangeSatisfaction(satisfactionChange);
                stats.AddExp(expReward);
                HUDController.Toast($"{activityName}สำเร็จ!");
                NisitSimulator.Systems.GameplayEvents.Raise(hungerChange > 0f ? NisitSimulator.Systems.GameplayEvents.Eat
                    : knowledgeChange > 0f ? NisitSimulator.Systems.GameplayEvents.Study : NisitSimulator.Systems.GameplayEvents.Relax);
            }
        }
    }
}
