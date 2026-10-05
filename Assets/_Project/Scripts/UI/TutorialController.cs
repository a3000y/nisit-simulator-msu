using UnityEngine;
using UnityEngine.UI;

namespace NisitSimulator.UI
{
    // คู่มือในเกม — โผล่ครั้งแรกที่เล่น + กด F1 เปิด/ปิดได้ทุกเมื่อ (สอนปุ่ม + เป้าหมาย)
    //   UI ถูกสร้าง+ต่อโดย Editor tool (Nisit -> Build Tutorial)
    public class TutorialController : MonoBehaviour
    {
        public GameObject panel;
        public Button closeButton;

        private NisitSimulator.Player.PlayerMovement move;

        void Start()
        {
            var player = GameObject.Find("Player");
            if (player != null) move = player.GetComponent<NisitSimulator.Player.PlayerMovement>();
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (panel != null) panel.SetActive(false);

            if (!NisitSimulator.Systems.ArrivalIntroController.Active && PlayerPrefs.GetInt("tut_seen", 0) == 0) Show();   // ครั้งแรกที่เล่น
        }

        void Update()
        {
            if (NisitSimulator.Systems.ArrivalIntroController.Active) return;
            if (Input.GetKeyDown(KeyCode.F1)) Toggle();           // เปิดคู่มืออีกครั้งได้ทุกเมื่อ
        }

        public void Toggle()
        {
            if (panel != null && panel.activeSelf) Hide(); else Show();
        }

        public void Show()
        {
            if (panel == null) return;
            panel.SetActive(true);
            if (move != null) move.enabled = false;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            NisitSimulator.Core.SFXManager.Page();
            Time.timeScale = 0f;                                  // หยุดเวลาไว้อ่าน (สถานะไม่ลด)
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
            Time.timeScale = 1f;
            if (move != null) move.enabled = true;
            PlayerPrefs.SetInt("tut_seen", 1); PlayerPrefs.Save();
        }
    }
}
