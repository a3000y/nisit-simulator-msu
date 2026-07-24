using UnityEngine;

namespace NisitSimulator.CameraRig
{
    // กล้องตามตัวละคร 2 โหมด (กด V สลับ):
    //   • Isometric (เริ่มต้น): มุมเฉียงบน orthographic — หมุน (เมาส์ขวา/Q-E) + ซูม (สกอลล์)
    //   • Third-Person (OTS): กล้องตามหลังไหล่ perspective — หมุน (เมาส์ขวา) + ซูม (สกอลล์)
    // ใส่ไว้ที่ Main Camera + ลาก Player ใส่ Target
    [RequireComponent(typeof(Camera))]
    public class IsometricCameraRig : MonoBehaviour
    {
        public enum Mode { Isometric, ThirdPerson }

        [Header("เป้าหมาย (ลากตัวละคร Player มาใส่)")]
        public Transform target;

        [Header("โหมดกล้อง")]
        public Mode mode = Mode.Isometric;
        public KeyCode toggleKey = KeyCode.V;
        public float followSmooth = 10f;
        public float yawAngle = 45f;              // ทิศหมุน (ใช้ร่วมกันทั้ง 2 โหมด)

        [Header("Isometric")]
        public float distance = 18f;
        public float pitchAngle = 35f;
        public float orthoSize = 7f;
        public float minOrtho = 3f, maxOrtho = 22f;

        [Header("Third-Person (OTS)")]
        public float otsDistance = 6f;            // ตั้งอัตโนมัติตามขนาดตัว
        public float otsHeight = 1.5f;
        public float shoulderOffset = 0.6f;
        public float otsFov = 50f;
        public float tpPitch = 12f;
        public float minPitch = -10f, maxPitch = 60f;

        [Header("ควบคุม")]
        public float rotateSpeed = 4f;
        public float mouseSensitivity = 3f;
        public bool holdRightMouseToRotate = true;
        public float zoomSpeed = 4f;

        private Camera cam;
        private Vector3 offset;
        private float minDist, maxDist;

        void Awake() { cam = GetComponent<Camera>(); }

        void Start()
        {
            float ch = MeasureTargetHeight();
            if (ch > 0.01f) { otsDistance = ch * 3.2f; otsHeight = ch * 0.9f; shoulderOffset = ch * 0.5f; }
            minDist = otsDistance * 0.7f; maxDist = otsDistance * 2.6f;
            if (target != null) yawAngle = target.eulerAngles.y + 45f;
            Apply();
            if (target != null) { UpdateOffset(); transform.position = target.position + offset; }
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) { mode = (mode == Mode.Isometric) ? Mode.ThirdPerson : Mode.Isometric; Apply(); }

            bool rotating = !holdRightMouseToRotate || Input.GetMouseButton(1);

            if (mode == Mode.Isometric)
            {
                if (rotating) yawAngle += Input.GetAxis("Mouse X") * rotateSpeed;
                if (Input.GetKey(KeyCode.Q)) yawAngle -= rotateSpeed * 12f * Time.deltaTime;
                if (Input.GetKey(KeyCode.E)) yawAngle += rotateSpeed * 12f * Time.deltaTime;

                float sc = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(sc) > 0.0001f)
                {
                    orthoSize = Mathf.Clamp(orthoSize - sc * zoomSpeed * orthoSize, minOrtho, maxOrtho);
                    cam.orthographicSize = orthoSize;
                }
            }
            else // Third-Person
            {
                if (rotating)
                {
                    yawAngle += Input.GetAxis("Mouse X") * mouseSensitivity;
                    tpPitch = Mathf.Clamp(tpPitch - Input.GetAxis("Mouse Y") * mouseSensitivity, minPitch, maxPitch);
                }
                float sc = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(sc) > 0.0001f)
                    otsDistance = Mathf.Clamp(otsDistance - sc * zoomSpeed * otsDistance, minDist, maxDist);
            }
        }

        void LateUpdate()
        {
            if (target == null) return;
            UpdateOffset();
            transform.position = Vector3.Lerp(transform.position, target.position + offset, followSmooth * Time.deltaTime);
            transform.rotation = (mode == Mode.Isometric)
                ? Quaternion.Euler(pitchAngle, yawAngle, 0f)
                : Quaternion.Euler(tpPitch, yawAngle, 0f);
        }

        private void UpdateOffset()
        {
            if (mode == Mode.Isometric)
                offset = Quaternion.Euler(pitchAngle, yawAngle, 0f) * new Vector3(0f, 0f, -distance);
            else
            {
                Quaternion rot = Quaternion.Euler(tpPitch, yawAngle, 0f);
                offset = Vector3.up * otsHeight - rot * Vector3.forward * otsDistance + rot * Vector3.right * shoulderOffset;
            }
        }

        private void Apply()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (mode == Mode.Isometric) { cam.orthographic = true; cam.orthographicSize = orthoSize; }
            else { cam.orthographic = false; cam.fieldOfView = otsFov; }
        }

        private float MeasureTargetHeight()
        {
            if (target == null) return 0f;
            var rends = target.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return 0f;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b.size.y;
        }
    }
}
