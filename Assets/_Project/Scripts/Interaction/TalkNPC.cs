using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.Systems;
using NisitSimulator.TimeSystem;
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
        public float greetRange = 4.5f;            // ระยะที่เริ่มโบกมือ + หันมอง
        public float waveCooldown = 8f;

        [Header("กิจกรรมตอนยืนเฉย ๆ (ชื่อ state ท่า — เฉพาะตัวยืน)")]
        public string[] idleActions;               // เช่น Talking/Waving/Cheering — สุ่มทำเป็นระยะ

        [Header("B) ให้ภารกิจ (quest-giver)")]
        public bool isQuestGiver;
        public string questTargetDoor;             // ประตูเป้าหมาย เช่น Door_ห้องสมุด
        public string questText = "ไปทำภารกิจที่เป้าหมาย";
        public string questGiveLine = "ช่วยไปทำภารกิจให้หน่อยสิ เดี๋ยวมีรางวัล!";
        public float questRewardSat = 10f;
        public int questRewardMoney = 40, questRewardExp = 25;
        public float questCooldown = 120f;         // ให้ภารกิจใหม่ได้ทุกกี่วินาที

        [Header("C) เปิดร้าน (vendor)")]
        public bool isVendor;
        public bool vendorIsShop = true;           // true = ร้านค้า(เก็บกระเป๋า) · false = โรงอาหาร(กินทันที)

        [Header("ความสัมพันธ์")]
        public int friendshipPerDay = 12;          // คะแนนสนิทที่ได้จากการคุยครั้งแรกของแต่ละวัน

        private Animator anim;
        private MenuNPCWalker walker;              // มี = NPC เดินไปมา
        private Transform player;
        private EventManager em;
        private GameClock clock;
        private string relId;                      // คีย์ความสัมพันธ์ (คงที่จากตำแหน่งเริ่ม)
        private int lastFriendDay = -1;
        private int baseHash;                      // ท่าเดิม (ไว้กลับหลังทำท่า)
        private bool gesturing;
        private float gestureUntil, nextWave, nextIdle, lastReward = -999f, lastQuest = -999f;

        void Start()
        {
            anim = GetComponentInChildren<Animator>();
            walker = GetComponent<MenuNPCWalker>();
            var p = GameObject.Find("Player");
            if (p != null) player = p.transform;
            if (isQuestGiver) em = Object.FindFirstObjectByType<EventManager>();
            clock = Object.FindFirstObjectByType<GameClock>();
            // คีย์คงที่ (จากตำแหน่งเริ่ม — คนเดินก็ยังคงคีย์เดิม)
            relId = $"{npcName}_{Mathf.RoundToInt(transform.position.x)}_{Mathf.RoundToInt(transform.position.z)}";
            if (!isVendor) RelationshipManager.Instance.Register(relId, npcName);   // ลงทะเบียนในรายชื่อเพื่อน
            GiftUI.EnsureExists();   // ระบบให้ของขวัญ (กด H ใกล้ NPC)
        }

        public string RelId => relId;
        public string NpcName => npcName;
        public bool CanBefriend => !isVendor;

        // รับของขวัญจากผู้เล่น → เพิ่มค่าสนิท (เรียกจาก GiftUI)
        public void ReceiveGift(string itemName, int friendship)
        {
            FacePlayer();
            PlayGesture("Talking", 2.5f);
            HUDController.Toast($"{npcName}: ว้าว ขอบคุณสำหรับ{itemName}นะ! ดีใจจัง");
            RelationshipManager.Instance.AddPoints(relId, npcName, friendship);
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

            if (player == null) return;
            float dx = transform.position.x - player.position.x;
            float dz = transform.position.z - player.position.z;
            float sq = dx * dx + dz * dz;

            // เข้าใกล้ → โบกมือทักทาย + หันหน้ามอง (เฉพาะตัวยืน)
            if (sq <= greetRange * greetRange)
            {
                if (Time.time >= nextWave) { PlayGesture("Waving", 2.2f); nextWave = Time.time + waveCooldown; }
                LookAtPlayerSmooth();
                return;
            }

            // ยืนเฉย ๆ (ไม่ใช่คนเดิน) → สุ่มทำกิจกรรมเป็นระยะ (คุย/โบก/เชียร์) ให้ดูมีชีวิต
            if (walker == null && idleActions != null && idleActions.Length > 0 && Time.time >= nextIdle)
            {
                PlayGesture(idleActions[Random.Range(0, idleActions.Length)], Random.Range(3.5f, 6f));
                nextIdle = Time.time + Random.Range(7f, 13f);
            }
        }

        // หันหน้าตามผู้เล่นแบบนุ่มนวล (คนเดินจะหันตามทางเดินอยู่แล้ว ไม่ต้อง)
        void LookAtPlayerSmooth()
        {
            if (walker != null || player == null) return;
            Vector3 to = player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 3f * Time.deltaTime);
        }

        public string GetPrompt()
        {
            // โชว์ระดับความสัมพันธ์ + หัวใจ (เฉพาะ NPC ที่คุยได้ทั่วไป)
            if (isVendor) return $"กด E ซื้อของกับ {npcName}";
            int lvl = RelationshipManager.Instance.GetLevel(relId);
            string hearts = RelationshipManager.Hearts(lvl);
            return string.IsNullOrEmpty(hearts)
                ? $"กด E เพื่อคุยกับ {npcName}"
                : $"กด E เพื่อคุยกับ {npcName} <color=#FF7BA6>{hearts}</color>";
        }

        public void Interact(GameObject who)
        {
            FacePlayer();
            PlayGesture("Talking", 3f);

            // C) พ่อค้า/แม่ค้า → เปิดร้าน
            if (isVendor)
            {
                var shop = FindShop(vendorIsShop);
                if (shop != null) { HUDController.Toast($"{npcName}: เชิญเลือกได้เลยจ้ะ~"); shop.Open(); }
                else HUDController.Toast($"{npcName}: ขอโทษ ตอนนี้ร้านปิดอยู่");
                return;
            }

            // B) ให้ภารกิจเดินไปทำ (ถ้าเป็น quest-giver + พร้อม + ไม่มีภารกิจค้าง)
            if (isQuestGiver && Time.time - lastQuest >= questCooldown)
            {
                if (em == null) em = Object.FindFirstObjectByType<EventManager>();
                if (em != null)
                {
                    // ยิ่งสนิท ยิ่งได้รางวัลเควสเยอะ (+20%/ระดับ)
                    float m = 1f + 0.2f * RelationshipManager.Instance.GetLevel(relId);
                    var c = new EventManager.Choice
                    {
                        kind = EventManager.Kind.GoTo,
                        targetDoor = questTargetDoor,
                        objectiveText = questText,
                        satisfaction = questRewardSat * m,
                        money = Mathf.RoundToInt(questRewardMoney * m),
                        exp = Mathf.RoundToInt(questRewardExp * m),
                        result = $"ภารกิจสำเร็จ! {npcName} ขอบคุณมาก"
                    };
                    if (em.StartObjectiveExternal(c))
                    {
                        lastQuest = Time.time;
                        HUDController.Toast($"{npcName}: {questGiveLine}");
                    }
                    else HUDController.Toast($"{npcName}: ทำภารกิจที่ค้างให้เสร็จก่อนนะ");
                    return;
                }
            }

            // ปกติ → คุย (สุ่มบทพูด) + พอใจ + เพิ่มความสนิท
            int lvl = RelationshipManager.Instance.GetLevel(relId);
            string line = (lines != null && lines.Length > 0) ? lines[Random.Range(0, lines.Length)] : "สวัสดี!";
            HUDController.Toast($"{npcName}: {line}");    // Toast มีเสียงแจ้งเตือนในตัว

            if (satisfactionReward != 0f && Time.time - lastReward >= rewardCooldown)
            {
                lastReward = Time.time;
                var st = (who != null ? who.GetComponent<PlayerStats>() : null)
                         ?? Object.FindFirstObjectByType<PlayerStats>();
                if (st != null) st.ChangeSatisfaction(satisfactionReward * (1f + 0.25f * lvl));   // สนิทมาก = คุยแล้วสุขใจกว่า
            }

            // ความสนิท: ได้จากการคุย "ครั้งแรกของแต่ละวัน" (สไตล์ life-sim — แวะหาเพื่อนทุกวัน)
            int day = clock != null ? clock.Day : lastFriendDay + 1;
            if (day > lastFriendDay)
            {
                lastFriendDay = day;
                RelationshipManager.Instance.AddPoints(relId, npcName, friendshipPerDay);
            }
        }

        // หา ShopController ที่ต้องการ (shop=เก็บกระเป๋า / cafeteria=กินทันที) แม้ตอนปิดอยู่
        static ShopController FindShop(bool wantShop)
        {
            var all = Object.FindObjectsByType<ShopController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var s in all) if (s.storeToInventory == wantShop) return s;
            return all.Length > 0 ? all[0] : null;
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
