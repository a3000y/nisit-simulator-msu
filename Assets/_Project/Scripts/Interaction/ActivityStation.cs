using UnityEngine;
using NisitSimulator.Stats;
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
            if (interactor.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.ChangeEnergy(energyChange);
                stats.ChangeHunger(hungerChange);
                stats.ChangeHealth(healthChange);
                stats.ChangeKnowledge(knowledgeChange);
                stats.ChangeSatisfaction(satisfactionChange);
                stats.AddExp(expReward);
                HUDController.Toast($"{activityName}สำเร็จ!");
            }
        }
    }
}
