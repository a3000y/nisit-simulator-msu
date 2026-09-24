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

        private EndReason lastReason;

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
            lastReason = reason;
            if (panel != null) panel.SetActive(true);

            // หยุดตัวละคร + ปลดล็อกเมาส์ (กดปุ่มได้ + ไม่เดินหลังฉากจบ)
            var playerGo = GameObject.Find("Player");
            if (playerGo != null)
            {
                var mv = playerGo.GetComponent<NisitSimulator.Player.PlayerMovement>();
                if (mv != null) mv.enabled = false;
            }
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;

            // ---- ข้อมูลสรุป (ใช้ตัดสินเกียรตินิยม + เส้นทางต่อไป) ----
            var prog = Object.FindFirstObjectByType<ProgressionManager>();
            var exam = Object.FindFirstObjectByType<ExamController>();
            var statsP = Object.FindFirstObjectByType<NisitSimulator.Stats.PlayerStats>();
            int score = prog != null ? prog.CalculateScore() : 0;
            int year = prog != null ? prog.CurrentYear : 1;
            float gpa = exam != null ? exam.GPA : 0f;
            int money = statsP != null ? statsP.Money : 0;
            string faculty = FacultyCatalog.NameOf(GameSession.SelectedFacultyIndex);

            // สรุปชีวิต: เพื่อนที่ได้รู้จักตลอดการเล่น (life recap)
            var rel = RelationshipManager.Instance;
            int friends = rel.FriendCount;         // "เพื่อน" ขึ้นไป
            int close = rel.CountAtLeast(3);        // "เพื่อนสนิท" ขึ้นไป
            int best = rel.CountAtLeast(4);         // "เพื่อนซี้"
            var am = AchievementManager.Instance;

            string title, msg;
            Color color;
            switch (reason)
            {
                case EndReason.Graduated:
                    color = new Color(1f, 0.85f, 0.3f);
                    string honors = gpa >= 3.5f ? "เกียรตินิยมอันดับหนึ่ง"
                                  : gpa >= 3.25f ? "เกียรตินิยมอันดับสอง" : "";
                    title = honors != "" ? "เกียรตินิยม!" : "GRADUATION!";
                    msg = (honors != ""
                              ? $"ยินดีด้วย! คุณจบการศึกษาด้วย{honors}"
                              : "ยินดีด้วย! คุณเรียนจบการศึกษาสำเร็จ")
                          + "\n" + CareerPath(gpa, money, friends);
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

            int ng = PlayerPrefs.GetInt("nisit_ngplus", 0);

            if (scoreText != null)
                scoreText.text =
                    $"คณะ: {faculty}\n" +
                    $"ชั้นปีที่ไปถึง: {year}\n" +
                    $"เกรดเฉลี่ย (GPA): {gpa:0.00}\n" +
                    $"เพื่อนที่ได้รู้จัก: {friends} คน" + (close > 0 ? $" (สนิท {close}" + (best > 0 ? $", ซี้ {best}" : "") + ")" : "") + "\n" +
                    $"ความสำเร็จ: {am.UnlockedCount}/{am.Total}\n" +
                    (ng > 0 ? $"New Game+ รอบ {ng}\n" : "") +
                    $"คะแนนรวม: {score}" +
                    (reason == EndReason.Graduated ? "\n\n<size=80%><color=#B9C2D6>กด \"เล่นใหม่\" เพื่อเริ่ม New Game+ (โบนัสเงิน + เก็บความสำเร็จ)</color></size>" : "");
        }

        // เส้นทางหลังเรียนจบ — ตาม GPA + เงินเก็บ + เพื่อน (networking)
        private string CareerPath(float gpa, int money, int friends)
        {
            if (gpa >= 3.5f) return "บริษัทชั้นนำแย่งตัว + ได้ทุนเรียนต่อ ป.โท!";
            if (gpa >= 3.0f) return "ได้งานในบริษัทที่ใฝ่ฝันทันที!";
            if (gpa >= 2.0f)
                return friends >= 5
                    ? "เพื่อน ๆ ช่วยแนะนำงานดี ๆ ให้ เริ่มงานได้เลย!"
                    : (money >= 3000 ? "มีทุนเก็บ เปิดกิจการเล็ก ๆ ของตัวเอง!" : "ได้งานทั่วไป เริ่มต้นชีวิตวัยทำงาน");
            return "ต้องหางานอยู่พักหนึ่ง... แต่ประสบการณ์ในรั้วมหาลัยมีค่าเสมอ";
        }

        // โหลดฉากปัจจุบันใหม่ = เริ่มเกมใหม่ · ถ้าเพิ่งจบการศึกษา = เลื่อนขั้น New Game+ (โบนัสรอบถัดไป)
        public void Restart()
        {
            Time.timeScale = 1f;
            if (lastReason == EndReason.Graduated)
            {
                int ng = PlayerPrefs.GetInt("nisit_ngplus", 0) + 1;
                PlayerPrefs.SetInt("nisit_ngplus", ng);
                PlayerPrefs.Save();
            }
            NisitSimulator.SaveLoad.GameSession.IsContinue = false;   // เริ่มใหม่ (รับเงินตั้งต้น + โบนัส NG+)
            NisitSimulator.SaveLoad.GameSession.PendingLoad = false;
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
