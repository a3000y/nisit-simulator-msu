using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Core;
using NisitSimulator.Stats;
using NisitSimulator.Player;
using NisitSimulator.SaveLoad;
using NisitSimulator.UI;

namespace NisitSimulator.Systems
{
    // ระบบสอบ: กลางภาค (วันที่ 2) + ปลายภาค (วันสุดท้ายของปี)
    // คะแนน = ตอบถูก (60%) + ความพร้อม/ความรู้สะสม (40%) → เกรด A-F + GPA
    // UI ถูกสร้าง+ต่อโดย Editor tool (Nisit -> Build Exam System)
    public class ExamController : MonoBehaviour
    {
        [System.Serializable]
        public class Question
        {
            public string q;
            public string[] choices;
            public int correct;
            public Question(string q, string[] c, int correct) { this.q = q; choices = c; this.correct = correct; }
        }

        [Header("UI (เซ็ตโดย Editor)")]
        public GameObject panel;
        public TMP_Text headerText;
        public TMP_Text progressText;
        public TMP_Text questionText;
        public Button[] answerButtons = new Button[4];
        public TMP_Text[] answerLabels = new TMP_Text[4];
        public GameObject resultPanel;
        public TMP_Text gradeText;
        public TMP_Text resultDetailText;
        public Button closeButton;

        [Header("จำนวนข้อต่อการสอบ")]
        public int questionsPerExam = 3;

        public float GPA { get; private set; }
        public bool IsOpen { get; private set; }
        public int ExamsTaken => gradePoints.Count;

        private ProgressionManager prog;
        private PlayerStats stats;
        private PlayerMovement move;

        // สถานะสอบค้างของวันนี้ (ต้องเดินไปห้องสอบเอง)
        private bool pendingExam, pendingFinal;
        private int pendingSem;
        private PlayerActionController heldAction;
        public bool HasPendingExam => pendingExam;

        private readonly List<float> gradePoints = new List<float>();
        private readonly HashSet<string> done = new HashSet<string>();
        private readonly List<Question> current = new List<Question>();
        private int index, correctCount;
        private bool isFinal;
        private int examSem;   // ภาคเรียนของการสอบที่กำลังทำ (ไว้บันทึกว่าสอบเสร็จแล้ว)

        void Start()
        {
            prog = Object.FindFirstObjectByType<ProgressionManager>();
            stats = Object.FindFirstObjectByType<PlayerStats>();
            var player = GameObject.Find("Player");
            if (player != null) move = player.GetComponent<PlayerMovement>();

            if (prog != null) prog.OnDayInYearChanged += HandleDayInYear;

            for (int i = 0; i < answerButtons.Length; i++)
            {
                int idx = i;
                if (answerButtons[i] != null) answerButtons[i].onClick.AddListener(() => Answer(idx));
            }
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panel != null) panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (prog != null) prog.OnDayInYearChanged -= HandleDayInYear;
        }

        // ---------- ตารางสอบ (รายภาคเรียน) ----------
        private void HandleDayInYear(int dayInYear, int daysPerYear)
        {
            if (prog == null) return;
            pendingExam = false;   // เคลียร์ของวันก่อน (ถ้าไม่ได้ไปสอบ = พลาด)

            int sem = AcademicCalendar.SemesterIndex(dayInYear);
            int len = AcademicCalendar.SemesterLen(sem);
            int semDay = AcademicCalendar.SemesterDay(dayInYear);
            int mid = Mathf.Max(1, Mathf.CeilToInt(len / 2f));

            if (semDay == len && !Completed(sem, "final"))
                SetPending(true, sem);
            else if (AcademicCalendar.HasMidterm(sem) && semDay == mid && !Completed(sem, "mid"))
                SetPending(false, sem);
        }

        void SetPending(bool final, int sem)
        {
            pendingExam = true; pendingFinal = final; pendingSem = sem;
            HUDController.Toast($"วันนี้มี{(final ? "สอบปลายภาค" : "สอบกลางภาค")} {AcademicCalendar.SemesterName(sem)}! ไปที่ห้องสอบ (กด E)");
        }

        // เรียกจาก ExamStation เมื่อผู้เล่นกด E ที่ห้องสอบ
        public void TryTakeExam(GameObject interactor)
        {
            if (IsOpen) return;
            if (!pendingExam) { HUDController.Toast("วันนี้ไม่มีสอบ มาใหม่วันสอบนะ"); return; }

            var action = interactor.GetComponent<PlayerActionController>()
                         ?? interactor.AddComponent<PlayerActionController>();
            if (action.IsBusy) { HUDController.Toast("กำลังทำกิจกรรมอยู่"); return; }

            heldAction = action;
            action.BeginHold("Sitting");   // นั่งค้างระหว่างทำข้อสอบ
            Begin(pendingFinal, pendingSem);
        }

        // key ประจำการสอบ (ปี-ภาค-ชนิด) · Completed = สอบเสร็จแล้วหรือยัง (กันสอบซ้ำ/save-scum)
        private string Key(int sem, string type) => (prog != null ? prog.CurrentYear : 0) + "-" + sem + "-" + type;
        private bool Completed(int sem, string type) => done.Contains(Key(sem, type));

        // ---------- เริ่มสอบ ----------
        public void Begin(bool final, int sem)
        {
            if (panel == null) return;
            isFinal = final;
            examSem = sem;
            index = 0; correctCount = 0;
            current.Clear();
            current.AddRange(PickQuestions(questionsPerExam));

            IsOpen = true;
            panel.SetActive(true);
            if (resultPanel != null) resultPanel.SetActive(false);
            if (headerText != null)
                headerText.text = (final ? "สอบปลายภาค" : "สอบกลางภาค") + " — " + AcademicCalendar.SemesterName(sem);
            if (move != null) move.enabled = false;
            Time.timeScale = 0f;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;

            ShowQuestion();
        }

        private void ShowQuestion()
        {
            var q = current[index];
            if (progressText != null) progressText.text = $"ข้อ {index + 1}/{current.Count}";
            if (questionText != null) questionText.text = q.q;
            for (int i = 0; i < answerButtons.Length; i++)
            {
                bool has = i < q.choices.Length;
                if (answerButtons[i] != null) answerButtons[i].gameObject.SetActive(has);
                if (has && answerLabels[i] != null) answerLabels[i].text = q.choices[i];
            }
        }

        private void Answer(int choice)
        {
            if (!IsOpen || index >= current.Count) return;
            if (choice == current[index].correct) correctCount++;

            index++;
            if (index < current.Count) ShowQuestion();
            else Finish();
        }

        // ---------- สรุปผล + เกรด ----------
        private void Finish()
        {
            float quiz = current.Count > 0 ? (float)correctCount / current.Count : 0f;
            float target = prog != null ? Mathf.Max(1f, prog.CurrentTarget) : 100f;
            float readiness = stats != null ? Mathf.Clamp01(stats.Knowledge / target) : 0.5f;
            float score = 0.6f * quiz + 0.4f * readiness;

            // ความรู้จากสอบเยอะขึ้น (~2 เท่า) → จูงใจให้ไปสอบ (ไม่บังคับ แต่คุ้มมาก)
            string grade; float gp; float kBonus; int exp, money; float sat;
            if (score >= 0.85f)      { grade = "A";  gp = 4f; kBonus = 80; exp = 60; money = 100; sat = 12; }
            else if (score >= 0.70f) { grade = "B";  gp = 3f; kBonus = 55; exp = 40; money = 0;   sat = 8;  }
            else if (score >= 0.55f) { grade = "C";  gp = 2f; kBonus = 35; exp = 25; money = 0;   sat = 3;  }
            else if (score >= 0.40f) { grade = "D";  gp = 1f; kBonus = 15; exp = 12; money = 0;   sat = -3; }
            else                     { grade = "F";  gp = 0f; kBonus = 3;  exp = 5;  money = 0;   sat = -12; }

            if (isFinal) { kBonus *= 1.5f; exp = Mathf.RoundToInt(exp * 1.5f); }

            gradePoints.Add(gp);
            done.Add(Key(examSem, isFinal ? "final" : "mid"));   // ทำเสร็จแล้ว → กันสอบซ้ำ (แม้โหลดเซฟ)
            float sum = 0f; foreach (var g in gradePoints) sum += g;
            GPA = gradePoints.Count > 0 ? sum / gradePoints.Count : 0f;

            if (gp >= 2f) NisitSimulator.Core.SFXManager.Success();
            else NisitSimulator.Core.SFXManager.Error();

            if (stats != null)
            {
                stats.ChangeKnowledge(kBonus);
                stats.AddExp(exp);
                stats.ChangeSatisfaction(sat);
                if (money > 0) stats.ChangeMoney(money);
            }

            if (resultPanel != null) resultPanel.SetActive(true);
            if (gradeText != null)
            {
                gradeText.text = grade;
                gradeText.color = GradeColor(grade);
            }
            if (resultDetailText != null)
            {
                string reward = $"ความรู้ +{kBonus:0}   EXP +{exp}";
                if (money > 0) reward += $"   ทุน +{money}฿";
                resultDetailText.text =
                    $"ตอบถูก {correctCount}/{current.Count}  •  ความพร้อม {readiness * 100:0}%\n" +
                    $"{reward}\nGPA สะสม: {GPA:0.00}";
            }
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            IsOpen = false;
            pendingExam = false;                       // สอบเสร็จแล้ว
            if (heldAction != null) { heldAction.EndHold(); heldAction = null; }   // ลุกจากท่านั่ง
            Time.timeScale = 1f;
            if (move != null) move.enabled = true;

            if (stats != null && stats.Health > 0 && stats.Energy > 0)
                HUDController.Toast($"สอบเสร็จ! GPA {GPA:0.00}");
        }

        // ---------- เซฟ/โหลดประวัติเกรด (คืน GPA เมื่อเล่นต่อ) ----------
        public List<float> GetGradePoints() => new List<float>(gradePoints);

        public void RestoreGrades(List<float> gp)
        {
            gradePoints.Clear();
            if (gp != null) gradePoints.AddRange(gp);
            float sum = 0f; foreach (var g in gradePoints) sum += g;
            GPA = gradePoints.Count > 0 ? sum / gradePoints.Count : 0f;
        }

        // เซฟ/โหลด "สอบเสร็จแล้ว" (กัน save-scum สอบซ้ำ) — ต้อง RestoreDoneExams ก่อน ProgressionManager.RestoreState
        public List<string> GetDoneExams() => new List<string>(done);
        public void RestoreDoneExams(List<string> keys)
        {
            done.Clear();
            if (keys != null) foreach (var k in keys) done.Add(k);
        }

        static Color GradeColor(string g)
        {
            switch (g)
            {
                case "A": return new Color(0.30f, 0.80f, 0.42f);
                case "B": return new Color(0.35f, 0.68f, 0.95f);
                case "C": return new Color(0.95f, 0.80f, 0.30f);
                case "D": return new Color(0.95f, 0.60f, 0.25f);
                default:  return new Color(0.92f, 0.34f, 0.34f);
            }
        }

        // ---------- คลังข้อสอบ (ดึงตามคณะของผู้เล่นจาก FacultyCatalog) ----------
        private List<Question> PickQuestions(int n)
        {
            var bank = FacultyCatalog.GetQuestions(GameSession.SelectedFacultyIndex);
            var pick = new List<Question>();
            for (int i = 0; i < n && bank.Count > 0; i++)
            {
                int r = Random.Range(0, bank.Count);
                pick.Add(bank[r]);
                bank.RemoveAt(r);
            }
            return pick;
        }
    }
}
