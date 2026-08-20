using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.UI;

namespace NisitSimulator.Interaction
{
    // สัตว์ในมหาลัย — เดินไปมา (ใส่ MenuNPCWalker) · กด E ลูบ → พอใจเล็กน้อย + หันมาหาผู้เล่น
    //   ใช้กับโมเดลสัตว์ (Quaternius/KayKit) ที่วางในโฟลเดอร์ Animals — วางโดย Nisit -> Build Animals
    [RequireComponent(typeof(Collider))]
    public class PetAnimal : MonoBehaviour, IInteractable
    {
        [Header("ตัวตน")]
        public string animalName = "แมว";
        public string[] reactions = { "เหมียว~", "กระดิกหาง", "วิ่งวนรอบตัวคุณ" };

        [Header("รางวัลเมื่อลูบ")]
        public float satisfactionReward = 6f;
        public float rewardCooldown = 30f;

        private Transform player;
        private float lastReward = -999f;

        void Start()
        {
            var p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }

        public string GetPrompt() => $"กด E เพื่อลูบ{animalName}";

        public void Interact(GameObject who)
        {
            FacePlayer();
            string r = (reactions != null && reactions.Length > 0) ? reactions[Random.Range(0, reactions.Length)] : "น่ารักจัง";
            HUDController.Toast($"{animalName}: {r}");

            if (satisfactionReward != 0f && Time.time - lastReward >= rewardCooldown)
            {
                lastReward = Time.time;
                var st = (who != null ? who.GetComponent<PlayerStats>() : null)
                         ?? Object.FindFirstObjectByType<PlayerStats>();
                if (st != null) st.ChangeSatisfaction(satisfactionReward);
            }
        }

        void FacePlayer()
        {
            if (player == null) return;
            Vector3 to = player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
        }
    }
}
