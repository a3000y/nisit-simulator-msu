using System.Text;
using UnityEngine;
using TMPro;
using Unity.Netcode;

namespace NisitSimulator.Net
{
    // 👥 รายชื่อผู้เล่นในห้อง — กด F2 เปิด/ปิด · อ่านจาก NetworkAvatar ที่ spawn อยู่ (เห็นครบทุก client)
    //   UI สร้าง+ต่อโดย M29 Setup Multiplayer
    public class PlayerListUI : MonoBehaviour
    {
        public GameObject panel;
        public TMP_Text listText;
        public KeyCode key = KeyCode.F2;

        void Start() { if (panel != null) panel.SetActive(false); }

        void Update()
        {
            if (Input.GetKeyDown(key) && panel != null) panel.SetActive(!panel.activeSelf);
            if (panel != null && panel.activeSelf) Refresh();
        }

        void Refresh()
        {
            if (listText == null) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient)
            {
                listText.text = "ยังไม่เชื่อมต่อ";
                return;
            }

            var sb = new StringBuilder();
            int n = 0;
            foreach (var av in Object.FindObjectsByType<NetworkAvatar>(FindObjectsSortMode.None))
            {
                n++;
                sb.AppendLine(av.IsOwner ? $"<b>• {av.DisplayName} (คุณ)</b>" : $"• {av.DisplayName}");
            }
            listText.text = $"<b>ผู้เล่นในห้อง ({n})</b>\n" + sb;
        }
    }
}
