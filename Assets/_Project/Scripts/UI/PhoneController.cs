using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.Systems;
using NisitSimulator.SaveLoad;
using NisitSimulator.Player;

namespace NisitSimulator.UI
{
    // 📱 โทรศัพท์นิสิต — กด TAB เปิด/ปิด · หน้าโฮมมีหลายแอป (ตามตาราง 3.2 + storyboard PDF)
    //   แอป: สถานะ / ปฏิทิน / ภารกิจ / เกรด / แผนที่
    // UI ถูกสร้าง+ต่อโดย Editor tool (Nisit -> Build Phone (TAB))
    public class PhoneController : MonoBehaviour
    {
        public enum App { Status = 0, Calendar = 1, Quests = 2, Grades = 3, Map = 4 }

        [Header("UI (เซ็ตโดย Editor)")]
        public GameObject panel;        // ราก (dim + ตัวเครื่อง)
        public GameObject homeView;     // หน้าโฮม (ตารางแอป)
        public GameObject appView;      // หน้าเนื้อหาแอป
        public TMP_Text clockBar;       // เวลาบนหัวเครื่อง (โชว์เสมอ)
        public TMP_Text appTitle;       // ชื่อแอป
        public TMP_Text appBody;        // เนื้อหาแอป
        public KeyCode toggleKey = KeyCode.Tab;

        [Header("ปุ่ม (เซ็ตโดย Editor)")]
        public Button[] appButtons;     // 0=สถานะ 1=ปฏิทิน 2=ภารกิจ 3=เกรด 4=แผนที่
        public Button backButton;

        [Header("มุมมองแอป (เซ็ตโดย Editor)")]
        public GameObject appCard;      // การ์ดข้อความ (แอปทั่วไป)
        public GameObject mapView;      // กรอบแผนที่ใหญ่
        public RawImage mapImage;       // ภาพจากกล้องมินิแมป
        public Camera minimapCam;       // กล้องมินิแมป (เปิดเฉพาะตอนดูแผนที่)

        public bool IsOpen { get; private set; }
        private App current;

        private Vector3 homeBase, appBase;
        private Coroutine slideCo;
        const float SlideDist = 440f;

        private PlayerStats stats;
        private GameClock clock;
        private ProgressionManager prog;
        private ExamController exam;
        private QuestSystem quests;
        private PlayerMovement move;

        void Start()
        {
            stats = Object.FindFirstObjectByType<PlayerStats>();
            clock = Object.FindFirstObjectByType<GameClock>();
            prog  = Object.FindFirstObjectByType<ProgressionManager>();
            exam  = Object.FindFirstObjectByType<ExamController>();
            quests = Object.FindFirstObjectByType<QuestSystem>();
            var player = GameObject.Find("Player");
            if (player != null) move = player.GetComponent<PlayerMovement>();

            // ต่อปุ่มแอป
            if (appButtons != null)
                for (int i = 0; i < appButtons.Length; i++)
                {
                    int idx = i;
                    if (appButtons[i] != null) appButtons[i].onClick.AddListener(() => OpenApp(idx));
                }
            if (backButton != null) backButton.onClick.AddListener(GoHome);

            // จำตำแหน่งฐานของหน้าจอ (ไว้ใช้สไลด์)
            if (homeView != null) homeBase = homeView.transform.localPosition;
            if (appView != null) appBase = appView.transform.localPosition;

            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();

            if (IsOpen)
            {
                if (clockBar != null) clockBar.text = clock != null ? clock.GetTimeString() : "";
                if (appView != null && appView.activeSelf) RefreshBody();
            }
        }

        public void Toggle()
        {
            bool open = !(panel != null && panel.activeSelf);
            SetOpen(open);
        }

        void SetOpen(bool open)
        {
            IsOpen = open;
            if (panel != null) panel.SetActive(open);
            if (move != null) move.enabled = !open;
            if (!open && minimapCam != null) minimapCam.enabled = false;   // ปิดกล้องแผนที่ตอนปิดโทรศัพท์
            if (open)
            {
                Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
                GoHome();
            }
        }

        // ---------- นำทาง (ปุ่มบนโทรศัพท์เรียก) ----------
        public void GoHome()
        {
            if (appView != null) appView.SetActive(false);
            if (minimapCam != null) minimapCam.enabled = false;   // เลิกดูแผนที่
            if (homeView != null)
            {
                homeView.SetActive(true);
                StartSlide(homeView.transform, homeBase, -SlideDist);   // เลื่อนเข้าจากซ้าย
            }
        }

        public void OpenApp(int appIndex)
        {
            current = (App)appIndex;
            if (homeView != null) homeView.SetActive(false);
            if (appView == null) return;

            appView.SetActive(true);
            bool isMap = current == App.Map;
            if (appCard != null) appCard.SetActive(!isMap);
            if (mapView != null) mapView.SetActive(isMap);
            if (minimapCam != null) minimapCam.enabled = isMap;   // เปิดกล้องเฉพาะตอนดูแผนที่ (ภาพสด)
            if (appTitle != null) appTitle.text = TitleFor(current);
            if (!isMap) RefreshBody();

            NisitSimulator.Core.SFXManager.Page();                // เสียงเปลี่ยนหน้าแอป
            StartSlide(appView.transform, appBase, SlideDist);    // เลื่อนเข้าจากขวา
        }

        static string TitleFor(App a)
        {
            switch (a)
            {
                case App.Status: return "สถานะตัวละคร";
                case App.Calendar: return "ปฏิทินการศึกษา";
                case App.Quests: return "ภารกิจวันนี้";
                case App.Grades: return "ผลการเรียน";
                case App.Map: return "แผนที่มหาลัย";
            }
            return "แอป";
        }

        // ---------- สไลด์เข้า (ease-out) ----------
        void StartSlide(Transform t, Vector3 home, float fromX)
        {
            if (slideCo != null) StopCoroutine(slideCo);
            slideCo = StartCoroutine(SlideCo(t, home, fromX));
        }

        IEnumerator SlideCo(Transform t, Vector3 home, float fromX)
        {
            const float dur = 0.22f;
            float e = 0f;
            Vector3 start = home + new Vector3(fromX, 0f, 0f);
            t.localPosition = start;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(e / dur), 3f);   // ease-out-cubic
                t.localPosition = Vector3.LerpUnclamped(start, home, k);
                yield return null;
            }
            t.localPosition = home;
            slideCo = null;
        }

        // ---------- เนื้อหาแต่ละแอป ----------
        void RefreshBody()
        {
            if (appTitle == null || appBody == null) return;

            switch (current)
            {
                case App.Status:   appTitle.text = "สถานะตัวละคร";   appBody.text = StatusText();   break;
                case App.Calendar: appTitle.text = "ปฏิทินการศึกษา"; appBody.text = CalendarText(); break;
                case App.Quests:   appTitle.text = "ภารกิจวันนี้";    appBody.text = quests != null ? quests.SummaryText() : "-"; break;
                case App.Grades:   appTitle.text = "ผลการเรียน";     appBody.text = GradesText();   break;
            }
        }

        string StatusText()
        {
            float en = stats != null ? stats.Energy : 0;
            float he = stats != null ? stats.Health : 0;
            float hu = stats != null ? stats.Hunger : 0;
            float kn = stats != null ? stats.Knowledge : 0;
            float sa = stats != null ? stats.Satisfaction : 0;
            float target = prog != null ? prog.CurrentTarget : 0;
            return
                $"พลังงาน\t<b>{en:0}</b> / 100\n" +
                $"สุขภาพ\t<b>{he:0}</b> / 100\n" +
                $"ความอิ่ม\t<b>{hu:0}</b> / 100\n" +
                $"ความรู้\t<b>{kn:0}</b>  (เป้า {target:0})\n" +
                $"ความพอใจ\t<b>{sa:0}</b>";
        }

        string CalendarText()
        {
            int year = prog != null ? prog.CurrentYear : 1;
            int diy  = prog != null ? prog.DayInYear : 1;
            int day  = clock != null ? clock.Day : 1;
            int sem  = AcademicCalendar.SemesterIndex(diy);
            string month = AcademicCalendar.MonthName(diy);
            string semName = AcademicCalendar.SemesterName(sem);
            string season = AcademicCalendar.SeasonName(sem);
            string faculty = FacultyCatalog.NameOf(GameSession.SelectedFacultyIndex);

            string examLine = (exam != null && exam.HasPendingExam)
                ? "\n<color=#FF6B6B><b>[ วันนี้มีสอบ! ไปที่ตึกคณะคุณ ]</b></color>"
                : "";

            return
                $"<b>วันที่ {day}</b>\n" +
                $"{month}\n" +
                $"{semName}  |  ฤดู{season}\n" +
                $"ชั้นปีที่ {year}\n" +
                $"คณะ{faculty}" +
                examLine;
        }

        string GradesText()
        {
            float gpa = exam != null ? exam.GPA : 0f;
            int taken = exam != null ? exam.ExamsTaken : 0;
            int mo = stats != null ? stats.Money : 0;
            int xp = stats != null ? stats.Exp : 0;
            return
                $"<size=140%><b>GPA {gpa:0.00}</b></size>\n" +
                $"สอบไปแล้ว {taken} ครั้ง\n" +
                "<color=#556>----------------------</color>\n" +
                $"เงิน\t<b>{mo}</b> บาท\n" +
                $"EXP\t<b>{xp}</b>";
        }
    }
}
