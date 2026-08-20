using UnityEngine;
using TMPro;

namespace NisitSimulator.UI
{
    // แถบภารกิจ (A) — โชว์ข้อความ + ระยะ + ลูกศรชี้ทางไปเป้าหมาย (หมุนตามทิศบนจอ)
    // UI สร้าง+ต่อโดย Editor tool (Nisit -> Build Event System)
    public class ObjectiveHUD : MonoBehaviour
    {
        public GameObject root;         // แถบ (เปิด/ปิด)
        public TMP_Text label;          // ข้อความ + ระยะ
        public RectTransform arrow;     // ลูกศร (หมุนชี้ทาง)

        private Camera cam;
        private Transform player;
        private Vector3 target;
        private bool active;
        private string text;

        void Start()
        {
            cam = Camera.main;
            var p = GameObject.Find("Player");
            if (p != null) player = p.transform;
            if (root != null) root.SetActive(false);
        }

        public void Set(Vector3 t, string s)
        {
            target = t; text = s; active = true;
            if (root != null) root.SetActive(true);
        }

        public void Clear()
        {
            active = false;
            if (root != null) root.SetActive(false);
        }

        void Update()
        {
            if (!active || player == null) return;
            if (cam == null) cam = Camera.main;

            float dx = player.position.x - target.x, dz = player.position.z - target.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (label != null) label.text = $"ภารกิจ: {text}   <color=#FFD766>{dist:0} ม.</color>";

            if (arrow != null && cam != null)
            {
                Vector3 ts = cam.WorldToScreenPoint(target);
                Vector3 ps = cam.WorldToScreenPoint(player.position + Vector3.up);
                if (ts.z < 0f) { ts.x = Screen.width - ts.x; ts.y = Screen.height - ts.y; }   // เป้าอยู่ข้างหลังกล้อง
                Vector2 d = new Vector2(ts.x - ps.x, ts.y - ps.y);
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                arrow.localEulerAngles = new Vector3(0f, 0f, ang - 90f);   // ลูกศรชี้ขึ้นเป็นค่าเริ่มต้น
            }
        }
    }
}
