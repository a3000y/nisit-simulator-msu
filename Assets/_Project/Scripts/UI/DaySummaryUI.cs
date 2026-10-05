using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Core;
using NisitSimulator.Stats;
using NisitSimulator.Systems;
using NisitSimulator.Player;
using NisitSimulator.TimeSystem;

namespace NisitSimulator.UI
{
    // สรุปผลตอนจบวัน — ขึ้นวันใหม่แล้วโชว์สิ่งที่ได้เมื่อวาน (ความรู้/เงิน/EXP/คาบเรียน/ภารกิจ/ต่อเนื่อง)
    //   + ภารกิจของวันนี้ · หยุดเวลาไว้จนกด "เริ่มวันใหม่"
    //   สร้างอัตโนมัติโดย GameplayBootstrap (EnsureExists)
    public class DaySummaryUI : MonoBehaviour
    {
        static DaySummaryUI _i;
        public static bool IsShowing => _i != null && _i.showing;
        public static void EnsureExists() { if (_i == null) new GameObject("DaySummaryUI").AddComponent<DaySummaryUI>(); }

        PlayerStats stats;
        GameClock clock;
        PlayerMovement move;
        QuestSystem quest;

        // ตัวสะสมของวัน
        float kGain; int moneyIn, moneyOut, xpGain; int classBase;
        float lastK; int lastM, lastX;
        int levelAtStart;
        int summaryDayInYear = 1;

        GameObject root;
        TMP_Text title, labels, values, questList;
        bool showing;
        float prevTimeScale = 1f;

        void Awake()
        {
            if (_i != null && _i != this) { Destroy(gameObject); return; }
            _i = this;
        }

        void Start()
        {
            stats = Object.FindFirstObjectByType<PlayerStats>();
            clock = Object.FindFirstObjectByType<GameClock>();
            quest = Object.FindFirstObjectByType<QuestSystem>();
            var p = GameObject.Find("Player");
            if (p != null) move = p.GetComponent<PlayerMovement>();

            ResetDay();
            if (stats != null)
            {
                stats.OnKnowledgeChanged += OnK;
                stats.OnMoneyChanged += OnM;
                stats.OnExpChanged += OnX;
            }
            if (clock != null) clock.OnDayChanged += OnDay;
            Build();
        }

        void OnDestroy()
        {
            if (stats != null) { stats.OnKnowledgeChanged -= OnK; stats.OnMoneyChanged -= OnM; stats.OnExpChanged -= OnX; }
            if (clock != null) clock.OnDayChanged -= OnDay;
            if (showing) Time.timeScale = prevTimeScale;
            if (_i == this) _i = null;
        }

        void ResetDay()
        {
            var p = Object.FindAnyObjectByType<ProgressionManager>();
            if (p != null) summaryDayInYear = p.DayInYear;
            kGain = 0; moneyIn = moneyOut = xpGain = 0;
            if (stats != null) { lastK = stats.Knowledge; lastM = stats.Money; lastX = stats.Exp; }
            classBase = StatsTracker.Instance.GetInt("classes");
            levelAtStart = LevelSystem.Instance != null ? LevelSystem.Instance.Level : 1;
        }

        void OnK(float v) { float d = v - lastK; if (d > 0) kGain += d; lastK = v; }
        void OnM(int v)   { int d = v - lastM; if (d > 0) moneyIn += d; else moneyOut += -d; lastM = v; }
        void OnX(int v)   { int d = v - lastX; if (d > 0) xpGain += d; lastX = v; }

        void OnDay(int day) => StartCoroutine(ShowNextFrame(day));

        // รอ 1 เฟรม ให้ระบบอื่น (ภารกิจ/ค่าขนม/เซฟ) จัดการวันใหม่ก่อน
        IEnumerator ShowNextFrame(int day)
        {
            yield return null;
            if (Time.timeSinceLevelLoad < 3f) { ResetDay(); yield break; }                    // ตอนโหลดเซฟ ไม่ใช่วันจริง
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) { ResetDay(); yield break; }   // จบเกม/หยุดอยู่
            Show(day);
        }

        void Show(int newDay)
        {
            int classes = StatsTracker.Instance.GetInt("classes") - classBase;
            var lv = LevelSystem.Instance;

            title.text = "สรุป " + AcademicCalendar.DateText(summaryDayInYear);

            var L = new System.Text.StringBuilder();
            var V = new System.Text.StringBuilder();
            L.AppendLine("เงินที่ได้");       V.AppendLine($"<color=#3A9E60>+{moneyIn:n0}฿</color>  <size=80%><color=#8A83A6>ใช้ไป {moneyOut:n0}฿</color></size>");
            L.AppendLine("EXP");             V.AppendLine($"<color=#3A9E60>+{xpGain}</color>  <size=80%><color=#8A83A6>{(lv != null ? lv.ProgressText() : "")}</color></size>");
            if (lv != null && lv.Level > levelAtStart) { L.AppendLine("เลเวล"); V.AppendLine($"<color=#D9922B>Lv.{levelAtStart} → Lv.{lv.Level}</color>"); }
            L.AppendLine("เข้าเรียน");        V.AppendLine($"{classes} คาบ");
            if (quest != null)
            {
                L.AppendLine("ภารกิจ");
                string qs = $"{quest.LastDayDone}/{quest.LastDayTotal}";
                if (quest.LastDayAllDone) qs = $"<color=#3A9E60>{qs} ครบ!</color>";
                V.AppendLine(qs);
                L.AppendLine("ต่อเนื่อง");
                V.AppendLine(quest.Streak > 0 ? $"<color=#D9922B>{quest.Streak} วัน</color>" : "<color=#8A83A6>0 วัน — ทำภารกิจให้ครบเพื่อเริ่มนับ</color>");
            }
            labels.text = L.ToString();
            values.text = V.ToString();
            questList.text = quest != null ? "<b>ภารกิจวันนี้</b>\n" + quest.TodayPlainText() : "";

            showing = true;
            root.SetActive(true);
            prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            if (move != null) move.enabled = false;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            SFXManager.Whoosh();
        }

        void Close()
        {
            showing = false;
            root.SetActive(false);
            Time.timeScale = prevTimeScale;
            if (move != null) move.enabled = true;
            ResetDay();
        }

        void Update()
        {
            if (showing && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))) Close();
        }

        void Build()
        {
            var c = GrowthUI.MakeCanvas(transform, "Day Summary Canvas", 94);
            root = c.gameObject;
            GrowthUI.Dim(c.transform);
            var card = GrowthUI.Box(c.transform, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 640f), GrowthUI.CardCol);
            GrowthUI.Box(card.transform, "Head", new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(520f, 80f), GrowthUI.Strip, false);
            title = GrowthUI.Text(card.transform, "สรุปวัน", new Vector2(0f, 264f), new Vector2(800f, 56f), 40, GrowthUI.Title);

            labels = GrowthUI.Text(card.transform, "", new Vector2(-250f, 70f), new Vector2(220f, 290f), 26, GrowthUI.Soft, TextAlignmentOptions.TopRight);
            values = GrowthUI.Text(card.transform, "", new Vector2(110f, 70f), new Vector2(460f, 290f), 26, GrowthUI.Ink, TextAlignmentOptions.TopLeft);
            labels.lineSpacing = values.lineSpacing = 12f;

            var qbox = GrowthUI.Box(card.transform, "Quests", new Vector2(0.5f, 0.5f), new Vector2(0f, -165f), new Vector2(780f, 150f), new Color(1f, 1f, 1f, 0.7f), false);
            questList = GrowthUI.Text(qbox.transform, "", Vector2.zero, new Vector2(740f, 136f), 21, GrowthUI.Ink, TextAlignmentOptions.TopLeft);

            var go = GrowthUI.Button(card.transform, "เริ่มวันใหม่", new Vector2(0f, -278f), new Vector2(300f, 60f), new Color(0.60f, 0.86f, 0.68f), 28);
            go.onClick.AddListener(Close);
            root.SetActive(false);
        }
    }
}
