using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.UI;

namespace NisitSimulator.Interaction
{
    // NPC คุยได้ — เข้าใกล้แล้วโบกมือ (Waving) · กด E คุย (Talking) + ได้บทพูดสุ่ม + พอใจเล็กน้อย
    //   • ยืนอยู่กับที่ (A) = ไม่ต้องมี MenuNPCWalker
    //   • เดินไปมา (B) = ใส่ MenuNPCWalker ด้วย (จะหยุดเดินชั่วขณะตอนทำท่า)
    //   ต้องรัน "Setup Character Animations" ก่อน ถึงจะมีท่า Waving/Talking ใน Animator
    //   วางโดย Editor tool: Nisit -> Build Talk NPCs
    [RequireComponent(typeof(Collider))]
    public class TalkNPC : MonoBehaviour, IInteractable
    {
        [Header("ตัวตน")]
        public string npcName = "รุ่นพี่";
        [TextArea] public string[] lines;          // สุ่มบทพูดตอนคุย

        [Header("รางวัลเมื่อคุย")]
        public float satisfactionReward = 5f;
        public float rewardCooldown = 40f;         // กันสแปม E รัว ๆ — ได้พอใจซ้ำได้ทุกกี่วินาที

        [Header("ทักทาย")]
        public float greetRange = 4.5f;            // ระยะที่เริ่มโบกมือ
        public float waveCooldown = 8f;

        private Animator anim;
        private MenuNPCWalker walker;              // มี = NPC เดินไปมา
        private Transform player;
        private int baseHash;                      // ท่าเดิม (ไว้กลับหลังทำท่า)
        private bool gesturing;
        private float gestureUntil, nextWave, lastReward = -999f;

        void Start()
        {
            anim = GetComponentInChildren<Animator>();
            walker = GetComponent<MenuNPCWalker>();
            var p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }

        void Update()
        {
            // กำลังทำท่าอยู่ → พอครบเวลากลับสู่ท่าเดิม (ยืน/เดิน)
            if (gesturing)
            {
                if (Time.time >= gestureUntil)
                {
                    if (anim != null && baseHash != 0) anim.CrossFade(baseHash, 0.2f);
                    gesturing = false;
                    if (walker != null) walker.enabled = true;   // เดินต่อ
                }
                return;
            }

            // เข้าใกล้ → โบกมือทักทาย (ใช้ได้ทั้ง NPC ยืนและเดิน)
            if (player == null) return;
            float dx = transform.position.x - player.position.x;
            float dz = transform.position.z - player.position.z;
            if (dx * dx + dz * dz <= greetRange * greetRange && Time.time >= nextWave)
            {
                PlayGesture("Waving", 2.2f);
                nextWave = Time.time + waveCooldown;
            }
        }

        public string GetPrompt() => $"กด E เพื่อคุยกับ {npcName}";

        public void Interact(GameObject who)
        {
            FacePlayer();
            PlayGesture("Talking", 3f);

            string line = (lines != null && lines.Length > 0) ? lines[Random.Range(0, lines.Length)] : "สวัสดี!";
            HUDController.Toast($"{npcName}: {line}");    // Toast มีเสียงแจ้งเตือนในตัว

            if (satisfactionReward != 0f && Time.time - lastReward >= rewardCooldown)
            {
                lastReward = Time.time;
                var st = (who != null ? who.GetComponent<PlayerStats>() : null)
                         ?? Object.FindFirstObjectByType<PlayerStats>();
                if (st != null) st.ChangeSatisfaction(satisfactionReward);
            }
        }

        // ---------- ท่าทาง (CrossFade แล้วกลับท่าเดิม) ----------
        void PlayGesture(string state, float dur)
        {
            if (anim != null)
            {
                if (!gesturing) baseHash = anim.GetCurrentAnimatorStateInfo(0).shortNameHash;   // จำท่าปัจจุบัน
                int h = Animator.StringToHash(state);
                if (anim.HasState(0, h)) anim.CrossFade(h, 0.15f);
            }
            gesturing = true; gestureUntil = Time.time + dur;
            if (walker != null) walker.enabled = false;    // หยุดเดินระหว่างทำท่า
        }

        void FacePlayer()
        {
            if (player == null) return;
            Vector3 to = player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
        }
    }
}
