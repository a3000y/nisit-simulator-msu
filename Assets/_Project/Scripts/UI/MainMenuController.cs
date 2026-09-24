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
        public GameObject slotPanel;       // แผง popup เลือกช่อง
        public Button slotCloseButton;
        private bool slotContinueMode;     // true = เปิดจาก "เล่นต่อ" · false = จาก "เล่นคนเดียว"

        void Start()
        {
            Time.timeScale = 1f;

            if (newGameButton) newGameButton.onClick.AddListener(() => OpenSlots(false));
            if (multiplayerButton) multiplayerButton.onClick.AddListener(MultiplayerGame);
            if (continueButton) continueButton.onClick.AddListener(() => OpenSlots(true));

            if (slotButtons != null)
                for (int i = 0; i < slotButtons.Length; i++)
                {
                    int idx = i;
                    if (slotButtons[i] != null) slotButtons[i].onClick.AddListener(() => OnSlotClicked(idx));
                }
            if (slotCloseButton && slotPanel) slotCloseButton.onClick.AddListener(() => slotPanel.SetActive(false));
            if (slotPanel) slotPanel.SetActive(false);
            if (settingsButton && settingsPanel)
                settingsButton.onClick.AddListener(() => Show(settingsPanel));
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

        // เปิดแผงเลือกช่อง — จาก "เล่นคนเดียว" (new) หรือ "เล่นต่อ" (continue)
        public void OpenSlots(bool continueMode)
        {
            slotContinueMode = continueMode;
            if (slotPanel == null) { if (continueMode) Continue(); else OpenCustomize(); return; }
            RefreshSlots();
            Show(slotPanel);
        }

        // เปิดแผงให้อยู่หน้าสุด (กัน MenuPolish ดันปุ่มหลักมาบัง)
        static void Show(GameObject panel)
        {
            if (panel == null) return;
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
        }

        void OnSlotClicked(int i)
        {
            GameSession.SaveSlot = i;
            if (slotContinueMode)
            {
                if (!SaveSystem.HasSave(i)) return;   // ช่องว่าง เล่นต่อไม่ได้
                if (slotPanel) slotPanel.SetActive(false);
                Continue();
            }
            else
            {
                if (slotPanel) slotPanel.SetActive(false);
                GameSession.OpenNetworkOnStart = false;
                OpenCustomize();   // เกมใหม่ → แต่งตัว → คณะ → เล่น
            }
        }

        void RefreshSlots()
        {
            if (slotButtons == null) return;
            for (int i = 0; i < slotButtons.Length; i++)
            {
                if (slotButtons[i] == null) continue;
                var lbl = slotButtons[i].GetComponentInChildren<TMP_Text>();
                string sum = SaveSystem.SummaryFor(i);
                if (lbl != null)
                    lbl.text = $"ช่อง {i + 1}\n<size=55%>{(sum != null ? sum : "ว่าง")}</size>";
                slotButtons[i].interactable = !slotContinueMode || sum != null;   // เล่นต่อ: เฉพาะช่องมีเซฟ
            }
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
            if (characterPanel != null) { Show(characterPanel); return; }
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
            if (facultyPanel != null) { Show(facultyPanel); return; }
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
