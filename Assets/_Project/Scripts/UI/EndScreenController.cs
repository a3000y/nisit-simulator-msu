using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using NisitSimulator.Core;
using NisitSimulator.Systems;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.UI
{
    // หน้าจอจบเกม — แสดงต่างกันตามเหตุผล (ตาย/ตก/จบการศึกษา) ตามสตอรี่บอร์ดฉาก 6/7/8
    public class EndScreenController : MonoBehaviour
    {
        [Header("อ้างอิง UI")]
        public GameObject panel;        // รากของหน้าจอ (ซ่อนไว้ตอนเริ่ม)
        public TMP_Text titleText;
        public TMP_Text messageText;
        public TMP_Text scoreText;
        public Button restartButton;
        public Button quitButton;

        void Start()
        {
            if (panel != null) panel.SetActive(false);

            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (quitButton != null) quitButton.onClick.AddListener(QuitToMenu);

            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += HandleState;
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= HandleState;
        }

        private void HandleState(GameState state)
        {
            if (state != GameState.GameOver && state != GameState.Win) return;
            Show(GameManager.Instance.LastEndReason);
        }

        private void Show(EndReason reason)
        {
            if (panel != null) panel.SetActive(true);

            // หยุดตัวละคร + ปลดล็อกเมาส์ (กดปุ่มได้ + ไม่เดินหลังฉากจบ)
            var playerGo = GameObject.Find("Player");
            if (playerGo != null)
            {
                var mv = playerGo.GetComponent<NisitSimulator.Player.PlayerMovement>();
                if (mv != null) mv.enabled = false;
            }
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;

            string title, msg;
            Color color;
            switch (reason)
            {
                case EndReason.Graduated:
                    title = "GRADUATION!"; color = new Color(1f, 0.85f, 0.3f);
                    msg = "ยินดีด้วย! คุณเรียนจบการศึกษาสำเร็จ";
                    NisitSimulator.Core.SFXManager.Fanfare();   // เสียงแฟนแฟร์ตอนจบการศึกษา
                    break;
                case EndReason.Flunked:
                    title = "FLUNKED OUT"; color = new Color(0.9f, 0.4f, 0.3f);
                    msg = "ความรู้ไม่ถึงเป้าหมาย คุณถูกรีไทร์ ลองใหม่อีกครั้ง!";
                    break;
                default: // Died
                    title = "GAME OVER"; color = new Color(0.85f, 0.3f, 0.3f);
                    msg = "พลังงาน/สุขภาพหมด อาชีพนักศึกษาของคุณจบลง";
                    break;
            }

            if (titleText != null) { titleText.text = title; titleText.color = color; }
            if (messageText != null) messageText.text = msg;

            var prog = Object.FindFirstObjectByType<ProgressionManager>();
            var exam = Object.FindFirstObjectByType<ExamController>();
            int score = prog != null ? prog.CalculateScore() : 0;
            int year = prog != null ? prog.CurrentYear : 1;
            float gpa = exam != null ? exam.GPA : 0f;
            string faculty = FacultyCatalog.NameOf(GameSession.SelectedFacultyIndex);

            // สรุปชีวิต: เพื่อนที่ได้รู้จักตลอดการเล่น (life recap)
            var rel = RelationshipManager.Instance;
            int friends = rel.FriendCount;         // "เพื่อน" ขึ้นไป
            int close = rel.CountAtLeast(3);        // "เพื่อนสนิท" ขึ้นไป
            int best = rel.CountAtLeast(4);         // "เพื่อนซี้"
            var am = AchievementManager.Instance;

            if (scoreText != null)
                scoreText.text =
                    $"คณะ: {faculty}\n" +
                    $"ชั้นปีที่ไปถึง: {year}\n" +
                    $"เกรดเฉลี่ย (GPA): {gpa:0.00}\n" +
                    $"เพื่อนที่ได้รู้จัก: {friends} คน" + (close > 0 ? $" (สนิท {close}" + (best > 0 ? $", ซี้ {best}" : "") + ")" : "") + "\n" +
                    $"ความสำเร็จ: {am.UnlockedCount}/{am.Total}\n" +
                    $"คะแนนรวม: {score}";
        }

        // โหลดฉากปัจจุบันใหม่ = เริ่มเกมใหม่
        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // กลับไปเมนูหลัก (ฉาก 1)
        public void QuitToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(NisitSimulator.SaveLoad.GameSession.MenuScene);
        }
    }
}
