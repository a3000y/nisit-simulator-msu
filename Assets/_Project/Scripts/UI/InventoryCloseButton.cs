using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.UI
{
    // ผูกปุ่ม "ปิด" ของกระเป๋าในขณะรันไทม์ (lambda ใน Editor ไม่ถูกเซฟลงฉาก)
    [RequireComponent(typeof(Button))]
    public class InventoryCloseButton : MonoBehaviour
    {
        public InventoryUI ui;

        void Start()
        {
            var btn = GetComponent<Button>();
            if (btn != null && ui != null) btn.onClick.AddListener(ui.Close);
        }
    }
}
