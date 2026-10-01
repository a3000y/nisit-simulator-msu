using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Interaction;

namespace NisitSimulator.GEBuilding
{
    // ตัวควบคุมมุมมองบุคคลที่หนึ่งสำหรับฉากอาคาร GE
    // WASD เดิน · Shift วิ่ง · เมาส์มอง · Space กระโดด · E โต้ตอบ (ประตู) · Esc ปลดเมาส์ / คลิกซ้ายล็อกกลับ
    [RequireComponent(typeof(CharacterController))]
    public class GEFirstPersonController : MonoBehaviour
    {
        [Header("การเดิน")]
        public float walkSpeed = 3.6f;
        public float runSpeed = 6f;
        public float gravity = -22f;
        public float jumpHeight = 0.9f;
        [Tooltip("ดึงตัวให้ติดพื้นตอนเดินลงบันได/ทางลาด (เมตร)")]
        public float groundSnapDistance = 0.5f;

        [Header("มุมมอง")]
        public Transform cameraPivot;
        public float mouseSensitivity = 2.2f;
        public float minPitch = -80f, maxPitch = 80f;

        [Header("โต้ตอบ (ปุ่ม E)")]
        public float interactDistance = 2.6f;
        public LayerMask interactMask = ~0;
        public KeyCode interactKey = KeyCode.E;
        public Font promptFont;

        [Header("ความปลอดภัย")]
        public Transform respawnPoint;
        [Tooltip("ตกต่ำกว่าระดับนี้จะเกิดใหม่ที่จุดเกิด")]
        public float killY = -15f;

        [Header("เดินอัตโนมัติ (ใช้ทดสอบ)")]
        public List<Vector3> autoPath = new List<Vector3>();
        public float autoReach = 0.35f;
        public int autoIndex;
        public bool autoDone = true;
        [HideInInspector] public string autoLog = "";
        [HideInInspector] public float minY = 999f, maxY = -999f;
        [HideInInspector] public int respawnCount;

        private CharacterController cc;
        private float yaw, pitch, vy, autoStuck;
        private IInteractable focus;
        private string prompt;
        private GUIStyle promptStyle;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            yaw = transform.eulerAngles.y;
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraPivot = cam.transform;
            }
        }

        void Start() => LockCursor(true);

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void StartAutoPath(List<Vector3> path)
        {
            autoPath = path; autoIndex = 0; autoDone = false; autoStuck = 0f; autoLog = "";
            minY = 999f; maxY = -999f;
        }

        [Tooltip("จำกัด delta time ต่อเฟรม กันทะลุเมื่อเฟรมกระตุก")]
        public float maxStep = 0.05f;

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, maxStep);
            Tick(dt, true);
        }

        // ตรรกะการเคลื่อนที่ทั้งหมด — เรียกจาก Update หรือจากสคริปต์ทดสอบด้วย dt คงที่
        public void Tick(float dt, bool readInput)
        {
            if (cc == null) cc = GetComponent<CharacterController>();
            bool auto = !autoDone && autoPath != null && autoIndex < autoPath.Count;

            // ---- มุมมอง ----
            if (readInput && Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
            else if (readInput && Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
            if (readInput && !auto && Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, minPitch, maxPitch);
            }

            // ---- ทิศการเดิน ----
            Vector3 wish = Vector3.zero;
            bool run = readInput && Input.GetKey(KeyCode.LeftShift);
            if (auto)
            {
                Vector3 d = autoPath[autoIndex] - transform.position; d.y = 0f;
                if (d.magnitude < autoReach)
                {
                    autoLog += $"ถึงจุด {autoIndex} pos={transform.position:F2}\n";
                    autoIndex++; autoStuck = 0f;
                    if (autoIndex >= autoPath.Count) autoDone = true;
                }
                else
                {
                    wish = d.normalized;
                    yaw = Mathf.MoveTowardsAngle(yaw, Quaternion.LookRotation(wish).eulerAngles.y, 540f * dt);
                }
                run = false;
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            if (!auto && readInput)
            {
                float h = Input.GetAxisRaw("Horizontal"), v = Input.GetAxisRaw("Vertical");
                wish = transform.right * h + transform.forward * v;
                if (wish.sqrMagnitude > 1f) wish.Normalize();
            }

            // ---- แรงโน้มถ่วง / กระโดด ----
            bool wasGrounded = cc.isGrounded;
            if (wasGrounded && vy < 0f) vy = -2f;
            if (wasGrounded && !auto && readInput && Input.GetButtonDown("Jump")) vy = Mathf.Sqrt(-2f * gravity * jumpHeight);
            vy += gravity * dt;

            float speed = run ? runSpeed : walkSpeed;
            Vector3 before = transform.position;
            cc.Move((wish * speed + Vector3.up * vy) * dt);

            // ---- ดึงติดพื้น (เดินลงบันไดไม่เด้ง) ----
            if (wasGrounded && !cc.isGrounded && vy <= 0f)
            {
                Vector3 origin = transform.position + cc.center;
                float castDist = cc.height * 0.5f - cc.radius + groundSnapDistance;
                if (Physics.SphereCast(origin, cc.radius * 0.95f, Vector3.down, out _, castDist, ~0, QueryTriggerInteraction.Ignore))
                    cc.Move(Vector3.down * groundSnapDistance);
            }

            if (auto)
            {
                Vector3 moved = transform.position - before; moved.y = 0f;
                autoStuck = moved.magnitude < 0.25f * speed * dt ? autoStuck + dt : 0f;
                if (autoStuck > 2.5f)
                {
                    autoLog += $"ติด/ถูกกั้นที่จุด {autoIndex} pos={transform.position:F2}\n";
                    autoDone = true;
                }
            }

            minY = Mathf.Min(minY, transform.position.y);
            maxY = Mathf.Max(maxY, transform.position.y);
            if (transform.position.y < killY) Respawn();

            // ---- โต้ตอบ ----
            UpdateFocus();
            if (readInput && focus != null && Input.GetKeyDown(interactKey)) focus.Interact(gameObject);
        }

        public void SetLook(float newYaw, float newPitch) { yaw = newYaw; pitch = newPitch; }
        public IInteractable CurrentFocus => focus;

        public void Respawn()
        {
            respawnCount++;
            cc.enabled = false;
            if (respawnPoint != null)
            {
                transform.position = respawnPoint.position;
                yaw = respawnPoint.eulerAngles.y;
            }
            cc.enabled = true;
            vy = 0f;
        }

        void UpdateFocus()
        {
            focus = null; prompt = null;
            if (cameraPivot == null) return;
            if (Physics.Raycast(cameraPivot.position, cameraPivot.forward, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Collide))
            {
                focus = hit.collider.GetComponentInParent<IInteractable>();
                if (focus != null) prompt = focus.GetPrompt();
            }
        }

        void OnGUI()
        {
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
                if (promptFont != null) promptStyle.font = promptFont;
                promptStyle.normal.textColor = Color.white;
            }
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (!string.IsNullOrEmpty(prompt))
            {
                var r = new Rect(cx - 220f, cy + 40f, 440f, 40f);
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(r, prompt, promptStyle);
            }
        }
    }
}
