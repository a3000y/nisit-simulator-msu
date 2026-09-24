using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
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
        public Button creditsButton;       // เกี่ยวกับ/ผู้จัดทำ (เซ็ตโดย Editor)
        public GameObject creditsPanel;
        public Button creditsCloseButton;
        public GameObject characterPanel;  // หน้าแต่งตัวละคร (เซ็ตโดย Editor)
        public Button characterConfirmButton;
        public Button characterBackButton;
        public Button[] slotButtons;       // เลือกช่องเซฟ 1-3 (เซ็ตโดย Editor)

        void Start()
        {
            Time.timeScale = 1f;

            if (newGameButton) newGameButton.onClick.AddListener(() => { GameSession.OpenNetworkOnStart = false; OpenCustomize(); });
            if (multiplayerButton) multiplayerButton.onClick.AddListener(MultiplayerGame);
            if (continueButton) continueButton.onClick.AddListener(Continue);

            if (slotButtons != null)
                for (int i = 0; i < slotButtons.Length; i++)
                {
                    int idx = i;
                    if (slotButtons[i] != null) slotButtons[i].onClick.AddListener(() => SelectSlot(idx));
                }
            RefreshSlots();   // ตั้งป้ายช่อง + เปิด/ปิด "เล่นต่อ" ตามช่องปัจจุบัน
            if (settingsButton && settingsPanel)
                settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
            if (quitButton) quitButton.onClick.AddListener(Quit);
            if (comingSoonCloseButton && comingSoonPanel)
                comingSoonCloseButton.onClick.AddListener(() => comingSoonPanel.SetActive(false));

            if (creditsButton && creditsPanel)
                creditsButton.onClick.AddListener(() => creditsPanel.SetActive(true));
            if (creditsCloseButton && creditsPanel)
                creditsCloseButton.onClick.AddListener(() => creditsPanel.SetActive(false));

            if (characterConfirmButton) characterConfirmButton.onClick.AddListener(ConfirmCustomize);
            if (characterBackButton) characterBackButton.onClick.AddListener(CloseCustomize);

            if (settingsPanel) settingsPanel.SetActive(false);
            if (facultyPanel) facultyPanel.SetActive(false);
            if (comingSoonPanel) comingSoonPanel.SetActive(false);
            if (creditsPanel) creditsPanel.SetActive(false);
            if (characterPanel) characterPanel.SetActive(false);
        }

        // เลือกช่องเซฟ → อัปเดตปุ่ม "เล่นต่อ" + ป้ายช่อง
        public void SelectSlot(int i)
        {
            GameSession.SaveSlot = i;
            RefreshSlots();
        }

        void RefreshSlots()
        {
            if (slotButtons != null)
                for (int i = 0; i < slotButtons.Length; i++)
                {
                    if (slotButtons[i] == null) continue;
                    var lbl = slotButtons[i].GetComponentInChildren<TMP_Text>();
                    if (lbl != null)
                    {
                        string sum = SaveSystem.SummaryFor(i);
                        lbl.text = $"ช่อง {i + 1}\n<size=55%>{(sum != null ? sum : "ว่าง")}</size>";
                    }
                    slotButtons[i].transform.localScale = Vector3.one * (i == GameSession.SaveSlot ? 1.12f : 1f);
                }
            if (continueButton != null) continueButton.interactable = SaveSystem.HasSave();   // มีเซฟในช่องนี้ไหม
        }

        // ปุ่มเล่นหลายคน — เข้าฉากล็อบบี้ (Host/Join → แต่งตัว+เห็นเพื่อน → โฮสต์กดเริ่มพร้อมกัน)
        public void MultiplayerGame()
        {
            GameSession.OpenNetworkOnStart = false;
            SceneManager.LoadScene(GameSession.LobbyScene);
        }

        // (คงไว้เผื่อเรียกจากที่อื่น) ป็อปอัป "กำลังพัฒนา"
        public void ShowComingSoon()
        {
            if (comingSoonPanel != null) comingSoonPanel.SetActive(true);
        }

        // เปิดหน้าแต่งตัวละคร (จากปุ่มเล่นคนเดียว) → ยืนยันแล้วค่อยไปเลือกคณะ/เริ่มเกม
        public void OpenCustomize()
        {
            if (characterPanel != null) { characterPanel.SetActive(true); return; }
            NewGame();   // ไม่มีหน้าแต่งตัว → ไปต่อเลย
        }

        public void CloseCustomize()
        {
            if (characterPanel != null) characterPanel.SetActive(false);
        }

        public void ConfirmCustomize()
        {
            CloseCustomize();
            NewGame();   // ไปเลือกคณะ (ถ้ามี) แล้วเริ่มเกม
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
            GameSession.IsContinue = false;   // เกมใหม่ → รับเงินตั้งต้น
            SceneManager.LoadScene(GameSession.GameplayScene);
        }

        // เล่นต่อจากเซฟ
        public void Continue()
        {
            GameSession.PendingLoad = true;
            GameSession.IsContinue = true;    // เล่นต่อ → เงินคืนจากเซฟ ไม่บวกเงินตั้งต้นซ้ำ
            GameSession.OpenNetworkOnStart = false;
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
