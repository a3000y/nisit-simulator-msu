using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NisitSimulator.Core;

namespace NisitSimulator.UI
{
    // เมนูหยุดชั่วคราว — กด Esc เปิด/ปิด (เมื่อไม่มีหน้าต่างอื่นเปิดอยู่) → เล่นต่อ / กลับเมนูหลัก / ออกเกม
    //   UI สร้าง+ต่อโดย Editor tool (Nisit -> Build Pause Menu)
    public class PauseMenu : MonoBehaviour
    {
        public GameObject panel;
        public Button resumeButton, menuButton, quitButton;

        private NisitSimulator.Player.PlayerMovement move;
        private bool isOpen;

        void Start()
        {
            var p = GameObject.Find("Player");
            if (p != null) move = p.GetComponent<NisitSimulator.Player.PlayerMovement>();
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (menuButton != null) menuButton.onClick.AddListener(ToMenu);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (isOpen) Resume();
            else if (CanPause()) Pause();
        }

        // เปิด Pause ได้เมื่อ: เกมกำลังเล่น + ไม่มีหน้าต่างอื่นคุมอยู่ (move ยังเปิด = ไม่มีร้าน/โทรศัพท์/สอบ ฯลฯ เปิด)
        bool CanPause()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsActive) return false;
            return move == null || move.enabled;
        }

        void Pause()
        {
            isOpen = true;
            if (panel != null) panel.SetActive(true);
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            if (GameManager.Instance != null) GameManager.Instance.PauseGame();   // State=Paused → timeScale=0
        }

        public void Resume()
        {
            isOpen = false;
            if (panel != null) panel.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.ResumeGame();   // → timeScale=1
        }

        void ToMenu()
        {
            Time.timeScale = 1f;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && (nm.IsClient || nm.IsServer)) nm.Shutdown();   // ตัดการเชื่อมต่อ MP ก่อนออก
            SceneManager.LoadScene(NisitSimulator.SaveLoad.GameSession.MenuScene);
        }

        void Quit()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
