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
        private PlayerEffects effects;          // ผลกระทบชั่วคราว (ป่วย ฯลฯ)
        private Vector3 velocity;
        private float stepTimer;                // จับจังหวะเสียงฝีเท้า

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            stats = GetComponent<PlayerStats>();
            animator = GetComponentInChildren<Animator>();   // หา Animator จากโมเดลลูก
            effects = GetComponent<PlayerEffects>() ?? gameObject.AddComponent<PlayerEffects>();
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        void Update()
        {
            // หยุดขยับเมื่อเกม pause หรือจบ
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            // ทิศของกล้องบนระนาบพื้น (ตัดแกน Y)
            Vector3 camForward = cameraTransform.forward; camForward.y = 0f; camForward.Normalize();
            Vector3 camRight = cameraTransform.right;   camRight.y = 0f;   camRight.Normalize();

            Vector3 moveDir = (camForward * v + camRight * h).normalized;

            // วิ่งเมื่อกด Shift (ป่วย = เดินช้าลง)
            bool running = Input.GetKey(KeyCode.LeftShift);
            float moveMult = effects != null ? effects.moveMult : 1f;
            float speed = (running ? runSpeed : walkSpeed) * moveMult;
            controller.Move(moveDir * speed * Time.deltaTime);

            bool moving = moveDir.sqrMagnitude > 0.01f;

            // เสียงฝีเท้า (วิ่งถี่กว่าเดิน)
            if (moving && controller.isGrounded)
            {
                stepTimer -= Time.deltaTime * (running ? 1.6f : 1f);
                if (stepTimer <= 0f) { NisitSimulator.Core.SFXManager.Footstep(); stepTimer = 0.4f; }
            }
            else stepTimer = 0f;

            // หันหน้าตามทิศเดิน
            if (moving)
            {
                Quaternion target = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);

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
                if (Input.GetKeyDown(KeyCode.Space))
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
            }
        }
    }
}
