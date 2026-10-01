using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NisitSimulator.Core;

namespace NisitSimulator.UI
{
    // เมนูหยุดชั่วคราว — กด Esc เปิด/ปิด (เมื่อไม่มีหน้าต่างอื่นเปิดอยู่) → เล่นต่อ / กลับเมนูหลัก / ออกเกม
    //   UI สร้าง+ต่อโดย Editor tool (Nisit -> Build Pause Menu)
    //   [DefaultExecutionOrder(-100)] ให้ประเมิน Esc "ก่อน" แผงอื่น (กระเป๋า/ร้าน/แชท) จะปิดตัว+คืน move.enabled
    //   ในเฟรมเดียวกัน — กัน Esc ปิดแผงแล้วเด้ง Pause ขึ้นทันทีในเฟรมนั้น
    [DefaultExecutionOrder(-100)]
    public class PauseMenu : MonoBehaviour
    {
        public GameObject panel;
        public Button resumeButton, menuButton, quitButton;
        public Button settingsButton;      // เปิดแผงตั้งค่าเสียงในเกม (เซ็ตโดย Editor)
        public GameObject settingsPanel;

        private NisitSimulator.Player.PlayerMovement move;
        private bool isOpen;

        void Start()
        {
            var p = GameObject.Find("Player");
            if (p != null) move = p.GetComponent<NisitSimulator.Player.PlayerMovement>();
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (menuButton != null) menuButton.onClick.AddListener(ToMenu);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (settingsButton != null && settingsPanel != null)
                settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
            if (panel != null) panel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            // ถ้าแผงตั้งค่าเปิดอยู่ → Esc ปิดแค่แผงตั้งค่า (กลับไปหน้า Pause) ไม่ออกจาก Pause
            if (settingsPanel != null && settingsPanel.activeSelf) { settingsPanel.SetActive(false); return; }
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
            ShowMultiplayerNote();
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            if (GameManager.Instance != null) GameManager.Instance.PauseGame();   // State=Paused → timeScale=0
        }

        public void Resume()
        {
            isOpen = false;
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (panel != null) panel.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.ResumeGame();   // → timeScale=1
        }

        // Multiplayer: Pause หยุดแค่เครื่องนี้ — บอกผู้เล่นว่าโลก/เวลายังเดินต่อสำหรับทุกคน (Host ไม่หยุดเวลาโลก ดู GameClock)
        TMPro.TMP_Text mpNote;
        void ShowMultiplayerNote()
        {
            var sync = NisitSimulator.Net.WorldTimeSync.Instance;
            bool mp = sync != null && sync.IsMultiplayerSession;
            if (mp && mpNote == null && panel != null)
            {
                var src = panel.GetComponentInChildren<TMPro.TMP_Text>(true);
                var go = new GameObject("MPPauseNote", typeof(RectTransform));
                go.transform.SetParent(panel.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 24f); rt.sizeDelta = new Vector2(900f, 60f);
                mpNote = go.AddComponent<TMPro.TextMeshProUGUI>();
                if (src != null) mpNote.font = src.font;
                mpNote.fontSize = 24f; mpNote.alignment = TMPro.TextAlignmentOptions.Center;
                mpNote.color = new Color(0.95f, 0.45f, 0.35f);
                mpNote.raycastTarget = false;
                mpNote.text = "กำลังเล่นหลายคน — โลกและเวลายังเดินต่อ ผู้เล่นคนอื่นไม่ได้หยุดตามคุณ";
            }
            if (mpNote != null) mpNote.gameObject.SetActive(mp);
        }

        void ToMenu()
        {
            // เซฟก่อนออก (เหมือน OnApplicationQuit) — ไม่งั้นความคืบหน้ากลางวันหายตอนกลับเมนู
            // ใช้ State ตรงๆ เพราะตอนนี้ Paused อยู่ (IsActive จะ false) — เซฟถ้ายังไม่จบเกม
            if (GameManager.Instance != null
                && GameManager.Instance.State != GameState.GameOver
                && GameManager.Instance.State != GameState.Win)
                NisitSimulator.SaveLoad.SaveManager.Save();

            Time.timeScale = 1f;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && (nm.IsClient || nm.IsServer)) { NisitSimulator.Net.WorldTimeSync.ExpectDisconnect = true; nm.Shutdown(); }   // ตัดการเชื่อมต่อ MP ก่อนออก
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
