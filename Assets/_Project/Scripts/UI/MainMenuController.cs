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
        public Button continueButton;
        public Button settingsButton;
        public Button quitButton;
        public GameObject settingsPanel;

        void Start()
        {
            Time.timeScale = 1f;

            if (newGameButton) newGameButton.onClick.AddListener(NewGame);
            if (continueButton)
            {
                continueButton.onClick.AddListener(Continue);
                continueButton.interactable = SaveSystem.HasSave();   // ปิดถ้าไม่มีเซฟ
            }
            if (settingsButton && settingsPanel)
                settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
            if (quitButton) quitButton.onClick.AddListener(Quit);

            if (settingsPanel) settingsPanel.SetActive(false);
        }

        // เริ่มใหม่ — ลบเซฟเก่า
        public void NewGame()
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
