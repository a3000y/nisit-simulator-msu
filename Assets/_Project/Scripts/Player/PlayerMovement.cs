using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.Core;

namespace NisitSimulator.Player
{
    // ควบคุมการเดินของตัวละครแบบ Isometric (WASD สัมพันธ์กับมุมกล้อง)
    // ใส่ไว้ที่ตัวละคร Player ที่มี CharacterController
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("การเคลื่อนที่")]
        public float walkSpeed = 4f;
        public float runSpeed = 7f;        // วิ่งเมื่อกด Shift (ตามสโคป 1.3.1.1)
        public float rotationSpeed = 12f;

        [Header("หันตัวด้วยคลิกขวา")]
        [Tooltip("กดค้างคลิกขวาเพื่อหันตัวละครไปทางเมาส์ (กล้องย้ายไปใช้ปุ่มกลางแทนแล้ว)")]
        public bool rightClickToFace = true;
        [Tooltip("ระดับพื้นที่ใช้คำนวณจุดที่เมาส์ชี้ ปกติคือระดับเท้าตัวละคร")]
        public float facePlaneHeightOffset = 0f;
        public float gravity = -20f;
        public float jumpHeight = 1.3f;   // ความสูงกระโดด (กด Space)

        [Header("อ้างอิงกล้อง (ปล่อยว่างให้ใช้ Camera.main)")]
        public Transform cameraTransform;

        [Header("พลังงานที่เสียตอนเคลื่อนที่ (ต่อวินาที)")]
        public float walkEnergyDrain = 0.15f;
        public float runEnergyDrain = 0.4f;

        private CharacterController controller;
        private PlayerStats stats;
        private Animator animator;              // ตัวเล่นแอนิเมชัน (ถ้ามีโมเดล)
        private float airTime;                  // เวลาที่ลอยจากพื้น (ใช้กับท่า Fall)
        private bool hasGroundedParam;

        void CacheAnimParams()
        {
            hasGroundedParam = false;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters) if (p.name == "Grounded") { hasGroundedParam = true; break; }
        }
        private PlayerEffects effects;          // ผลกระทบชั่วคราว (ป่วย ฯลฯ)
        private PlayerExhaustion exhaustion;    // สถานะหมดแรง (ไม่มีคอมโพเนนต์ = ปกติ)
        private Vector3 velocity;
        // ซิงก์เสียงฝีเท้ากับกระดูกเท้าจริง (เล่นตอนเท้าลงต่ำสุด = แตะพื้น)
        private Transform leftFoot, rightFoot;
        private float lfPrevY, rfPrevY;
        private bool lfDescending, rfDescending;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            stats = GetComponent<PlayerStats>();
            animator = GetComponentInChildren<Animator>();   // หา Animator จากโมเดลลูก
            CacheAnimParams();
            if (animator != null && animator.isHuman)         // อ้างอิงกระดูกเท้า (Humanoid) ไว้ซิงก์เสียงเดิน
            {
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
            effects = GetComponent<PlayerEffects>() ?? gameObject.AddComponent<PlayerEffects>();
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        // เรียกหลังสลับโมเดล (PlayerModelSwapper) — หา Animator/กระดูกเท้าใหม่
        public void RefreshAnimator()
        {
            animator = GetComponentInChildren<Animator>();
            CacheAnimParams();
            if (animator != null && animator.isHuman)
            {
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
        }

        void Update()
        {
            // หยุดขยับเมื่อเกม pause หรือจบ
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) return;

            // กัน NRE ถ้ากล้องยังไม่พร้อม (ถูกสลับ/สร้างทีหลัง) — หาใหม่ ไม่มีก็ข้ามเฟรมนี้
            if (cameraTransform == null)
            {
                if (Camera.main != null) cameraTransform = Camera.main.transform;
                else return;
            }

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            // ทิศของกล้องบนระนาบพื้น (ตัดแกน Y)
            Vector3 camForward = cameraTransform.forward; camForward.y = 0f; camForward.Normalize();
            Vector3 camRight = cameraTransform.right;   camRight.y = 0f;   camRight.Normalize();

            Vector3 moveDir = (camForward * v + camRight * h).normalized;

            // วิ่งเมื่อกด Shift (ป่วย = เดินช้าลง · หมดแรง = วิ่งไม่ได้ + เดินช้าลง)
            if (exhaustion == null) exhaustion = GetComponent<PlayerExhaustion>();
            bool exhausted = exhaustion != null && exhaustion.IsExhausted;
            bool running = Input.GetKey(KeyCode.LeftShift) && !exhausted;
            if (exhausted && Input.GetKeyDown(KeyCode.LeftShift)) exhaustion.NotifyBlocked("วิ่ง");
            float moveMult = effects != null ? effects.moveMult : 1f;
            // ตัวคูณหมดแรงเป็น "ตัวคูณแยก" อ่านค่าทุกเฟรม (ไม่สะสม) → ไม่เขียนทับอาการป่วย/ความสามารถ และไม่ลดซ้ำทุกเฟรม
            float exhaustMult = exhaustion != null ? exhaustion.MoveSpeedMultiplier : 1f;
            float speed = (running ? runSpeed : walkSpeed) * moveMult * NisitSimulator.Systems.Perks.MoveMul * exhaustMult;   // "ขาไว"
            controller.Move(moveDir * speed * Time.deltaTime);

            bool moving = moveDir.sqrMagnitude > 0.01f;

            // เสียงฝีเท้า — เล่นตอน "เท้าลงต่ำสุด" (แตะพื้น) จากตำแหน่งกระดูกเท้าจริง → ตรงเป๊ะทุกอนิเมชัน
            if (moving && controller.isGrounded)
            {
                StepFoot(leftFoot, ref lfPrevY, ref lfDescending);
                StepFoot(rightFoot, ref rfPrevY, ref rfDescending);
            }

            // หันหน้าตามทิศเดิน
            if (moving)
            {
                Quaternion target = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
            }
            else if (rightClickToFace && Input.GetMouseButton(1))
            {
                // คลิกขวาค้างไว้เพื่อหันหน้าตอนยืนนิ่ง (ตามสเปกปุ่มควบคุม)
                FaceMouse();

                // หักพลังงานตามการเคลื่อนที่ (วิ่งเปลืองกว่าเดิน · ป่วย = เหนื่อยเร็ว)
                if (stats != null)
                {
                    float drainMult = effects != null ? effects.energyDrainMult : 1f;
                    stats.ChangeEnergy(-(running ? runEnergyDrain : walkEnergyDrain) * drainMult * Time.deltaTime);
                }
            }

            // แรงโน้มถ่วง + กระโดด (Space)
            if (controller.isGrounded)
            {
                if (velocity.y < 0) velocity.y = -2f;
                if (Input.GetKeyDown(KeyCode.Space) && exhausted) exhaustion.NotifyBlocked("กระโดด");   // หมดแรง = กระโดดไม่ได้
                else if (Input.GetKeyDown(KeyCode.Space))
                {
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);   // สูตรความสูงกระโดด
                    NisitSimulator.Core.SFXManager.Jump();
                }
            }
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);

            // ส่งความเร็วเข้า Animator (0=ยืน, 0.5=เดิน, 1=วิ่ง) แบบนุ่มนวล
            if (animator != null)
            {
                float animSpeed = moving ? (running ? 1f : 0.5f) : 0f;
                animator.SetFloat("Speed", animSpeed, 0.12f, Time.deltaTime);

                // ท่าลอยกลางอากาศ (กระโดด/ตก) — หน่วงเวลานิดหน่อยกันกระพริบตอนเดินลงขั้นบันได
                airTime = controller.isGrounded ? 0f : airTime + Time.deltaTime;
                if (hasGroundedParam) animator.SetBool("Grounded", airTime < 0.18f);
            }
        }

        // เล่นเสียงฝีเท้าตอนเท้าถึง "จุดต่ำสุด" (เปลี่ยนจากลงเป็นขึ้น) = แตะพื้นจริง
        void StepFoot(Transform foot, ref float prevY, ref bool wasDescending)
        {
            if (foot == null) return;
            float y = foot.position.y;
            bool descending = y < prevY - 0.001f;   // deadzone กัน jitter
            bool ascending  = y > prevY + 0.001f;
            if (wasDescending && ascending)          // ผ่านจุดต่ำสุด → แตะพื้น
                NisitSimulator.Core.SFXManager.Footstep();
            if (descending) wasDescending = true;
            else if (ascending) wasDescending = false;
            prevY = y;
        }
    

        // หันตัวละครไปทางจุดที่เมาส์ชี้บนพื้นระดับเท้า
        private void FaceMouse()
        {
            var cam = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main;
            if (cam == null) return;

            var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y + facePlaneHeightOffset, 0f));
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!plane.Raycast(ray, out float dist)) return;

            Vector3 point = ray.GetPoint(dist);
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;

            var look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * Time.deltaTime);
        }
}
}
