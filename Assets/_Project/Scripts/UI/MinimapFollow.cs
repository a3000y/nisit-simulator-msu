using UnityEngine;

namespace NisitSimulator.UI
{
    // 🗺️ กล้อง Minimap มองจากด้านบน ตามตัวผู้เล่น + หันทิศให้ "ตรงกับฉากจริง"
    // ค่าเริ่มต้น: หมุนตามกล้องหลัก (isometric) → ที่เห็นบนจอ = ที่เห็นบนแผนที่
    // แปะไว้ที่กล้อง Minimap (orthographic) — ตัวเครื่องก้มลง 90°
    public class MinimapFollow : MonoBehaviour
    {
        public Transform target;           // ตัวผู้เล่น (ว่าง = หา "Player" อัตโนมัติ)
        public Transform cameraTransform;  // กล้องหลัก (ว่าง = ใช้ Camera.main)
        public float height = 45f;         // ความสูงกล้องเหนือหัว

        [Header("ทิศของแผนที่")]
        public bool matchMainCamera = true;    // หมุนตามกล้องหลัก (ตรงกับที่เห็นบนจอ) — แนะนำ
        public bool rotateWithPlayer = false;  // หมุนตามหน้าตัวละคร (ใช้เมื่อปิด matchMainCamera)

        void Start()
        {
            if (target == null)
            {
                var p = GameObject.Find("Player");
                if (p != null) target = p.transform;
            }
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        void LateUpdate()
        {
            if (target == null) return;
            transform.position = new Vector3(target.position.x, target.position.y + height, target.position.z);

            float yaw = 0f;
            if (matchMainCamera && cameraTransform != null) yaw = cameraTransform.eulerAngles.y;
            else if (rotateWithPlayer) yaw = target.eulerAngles.y;

            transform.rotation = Quaternion.Euler(90f, yaw, 0f);
        }
    }
}
