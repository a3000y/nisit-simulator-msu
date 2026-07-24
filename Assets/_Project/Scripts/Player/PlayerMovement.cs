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

        [Header("อ้างอิงกล้อง (ปล่อยว่างให้ใช้ Camera.main)")]
        public Transform cameraTransform;

        [Header("พลังงานที่เสียตอนเคลื่อนที่ (ต่อวินาที)")]
        public float walkEnergyDrain = 0.6f;
        public float runEnergyDrain = 1.4f;

        private CharacterController controller;
        private PlayerStats stats;
        private Animator animator;              // ตัวเล่นแอนิเมชัน (ถ้ามีโมเดล)
        private Vector3 velocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            stats = GetComponent<PlayerStats>();
            animator = GetComponentInChildren<Animator>();   // หา Animator จากโมเดลลูก
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

            // วิ่งเมื่อกด Shift
            bool running = Input.GetKey(KeyCode.LeftShift);
            float speed = running ? runSpeed : walkSpeed;
            controller.Move(moveDir * speed * Time.deltaTime);

            bool moving = moveDir.sqrMagnitude > 0.01f;

            // หันหน้าตามทิศเดิน
            if (moving)
            {
                Quaternion target = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);

                // หักพลังงานตามการเคลื่อนที่ (วิ่งเปลืองกว่าเดิน)
                if (stats != null)
                    stats.ChangeEnergy(-(running ? runEnergyDrain : walkEnergyDrain) * Time.deltaTime);
            }

            // แรงโน้มถ่วง
            if (controller.isGrounded && velocity.y < 0) velocity.y = -2f;
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
