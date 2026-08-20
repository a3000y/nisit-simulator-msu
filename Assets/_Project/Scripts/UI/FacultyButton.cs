using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.UI
{
    // ปุ่มเลือกคณะ — ต่อสายตอน runtime (listener แบบ lambda ใน Editor ไม่ถูกเซฟลงฉาก)
    //   index >= 0 = เลือกคณะนั้น · index < 0 = ปุ่มย้อนกลับ
    [RequireComponent(typeof(Button))]
    public class FacultyButton : MonoBehaviour
    {
        public int index;

        void Start()
        {
            var mc = Object.FindFirstObjectByType<MainMenuController>();
            if (mc == null) return;
            var btn = GetComponent<Button>();
            if (index >= 0) btn.onClick.AddListener(() => mc.ChooseFaculty(index));
            else btn.onClick.AddListener(mc.CloseFaculty);
        }
    }
}
