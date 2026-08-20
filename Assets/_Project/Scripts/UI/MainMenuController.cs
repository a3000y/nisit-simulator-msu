using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.UI
{
    // ควบคุมเมนูหลัก (ฉาก 1): เล่นคนเดียว / เล่นต่อ / ตั้งค่า / ออก
    public class MainMenuController : MonoBehaviour
    {
        public Button newGameButton;
        public Button multiplayerButton;   // เล่นหลายคน (ยังไม่เปิดใช้ — โชว์ป็อปอัป "กำลังพัฒนา")
        public Button continueButton;
        public Button settingsButton;
        public Button quitButton;
        public GameObject settingsPanel;
        public GameObject facultyPanel;    // หน้าเลือกคณะ (เซ็ตโดย Editor)
        public GameObject comingSoonPanel; // ป็อปอัป "กำลังพัฒนา" (เซ็ตโดย Editor)
        public Button comingSoonCloseButton;

        void Start()
        {
            Time.timeScale = 1f;

            if (newGameButton) newGameButton.onClick.AddListener(NewGame);
            if (multiplayerButton) multiplayerButton.onClick.AddListener(ShowComingSoon);
            if (continueButton)
            {
                continueButton.onClick.AddListener(Continue);
                continueButton.interactable = SaveSystem.HasSave();   // ปิดถ้าไม่มีเซฟ
            }
            if (settingsButton && settingsPanel)
                settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
            if (quitButton) quitButton.onClick.AddListener(Quit);
            if (comingSoonCloseButton && comingSoonPanel)
                comingSoonCloseButton.onClick.AddListener(() => comingSoonPanel.SetActive(false));

            if (settingsPanel) settingsPanel.SetActive(false);
            if (facultyPanel) facultyPanel.SetActive(false);
            if (comingSoonPanel) comingSoonPanel.SetActive(false);
        }

        // ปุ่มเล่นหลายคน (ยังไม่เปิด) — แสดงป็อปอัปแจ้งว่ากำลังพัฒนา
        public void ShowComingSoon()
        {
            if (comingSoonPanel != null) comingSoonPanel.SetActive(true);
        }

        // เริ่มใหม่ — เปิดหน้าเลือกคณะก่อน (ถ้ามี) ไม่งั้นเริ่มเลย
        public void NewGame()
        {
            if (facultyPanel != null) { facultyPanel.SetActive(true); return; }
            StartNewGame();
        }

        // เลือกคณะแล้วเริ่มเกม (เรียกจากปุ่มเลือกคณะ)
        public void ChooseFaculty(int index)
        {
            GameSession.SelectedFacultyIndex = index;
            StartNewGame();
        }

        public void CloseFaculty()
        {
            if (facultyPanel != null) facultyPanel.SetActive(false);
        }

        private void StartNewGame()
        {
            SaveSystem.DeleteSave();
            GameSession.PendingLoad = false;
            SceneManager.LoadScene(GameSession.GameplayScene);
        }

        // เล่นต่อจากเซฟ
        public void Continue()
        {
            GameSession.PendingLoad = true;
            SceneManager.LoadScene(GameSession.GameplayScene);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
