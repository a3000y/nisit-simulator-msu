using UnityEngine;

namespace NisitSimulator.UI
{
    // 🗺️ กด M เพื่อเปิด/ปิดแผนที่ (ซ่อนไว้ก่อน เพื่อให้จอโล่ง)
    // ปิดกล้อง Minimap ตอนไม่ใช้ด้วย = ประหยัดการเรนเดอร์
    public class MinimapToggle : MonoBehaviour
    {
        public GameObject mapUI;            // กรอบแผนที่ (โชว์ตอนเปิด)
        public Camera minimapCam;           // กล้อง Minimap
        public GameObject hintWhenClosed;   // ป้าย "กด M" (โชว์ตอนปิด)
        public KeyCode key = KeyCode.M;
        public bool startOpen = false;

        private bool open;
        public bool IsOpen => open;

        // โทรศัพท์ปิดกล้องแผนที่ตอนปิดแอป — ถ้ามินิแมป (M) ยังเปิดอยู่ ให้เปิดกล้องกลับ
        void LateUpdate()
        {
            if (open && minimapCam != null && !minimapCam.enabled) minimapCam.enabled = true;
        }

        void Start() { Apply(startOpen); }

        void Update()
        {
            if (Input.GetKeyDown(key)) Apply(!open);
        }

        // ให้ระบบอื่น (เช่น ปุ่มแผนที่ในโทรศัพท์) สั่งเปิดได้
        public void Open() { Apply(true); }
        public void Close() { Apply(false); }

        private void Apply(bool o)
        {
            open = o;
            if (mapUI != null) mapUI.SetActive(o);
            if (minimapCam != null) minimapCam.enabled = o;
            if (hintWhenClosed != null) hintWhenClosed.SetActive(!o);
        }
    }
}
