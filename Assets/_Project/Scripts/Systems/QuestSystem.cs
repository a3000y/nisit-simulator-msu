using System.Collections.Generic;
using UnityEngine;
using TMPro;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;

namespace NisitSimulator.Systems
{
    // ระบบภารกิจรายวัน: สุ่ม 3 ภารกิจ/วัน ติดตามความคืบหน้าจากค่าสถานะ ทำสำเร็จได้รางวัล รีเซ็ตทุกวัน
    // UI ถูกสร้าง+ต่อโดย Editor tool (Nisit -> Build Quest System)
    public class QuestSystem : MonoBehaviour
    {
        public enum Metric
        {
            KnowledgeGain, ExpGain, MoneyEarn, SatisfactionReach,
            HungerReach, EnergyReach, HealthReach, MoneySpend, MoneyReach
        }

        [System.Serializable]
        public class Quest
        {
            public string desc;
            public Metric metric;
            public float target;
            public int rewardMoney;
            public int rewardExp;
            public float rewardSat;
            [HideInInspector] public float progress;
            [HideInInspector] public bool done;
            public Quest(string d, Metric m, float t, int money, int exp, float sat)
            { desc = d; metric = m; target = t; rewardMoney = money; rewardExp = exp; rewardSat = sat; }
            public Quest Copy() => new Quest(desc, metric, target, rewardMoney, rewardExp, rewardSat);
        }

        [Header("UI (เซ็ตโดย Editor)")]
        public TMP_Text[] rows = new TMP_Text[3];
        public int dailyCount = 3;

        private PlayerStats stats;
        private GameClock clock;
        private ProgressionManager prog;
        private readonly List<Quest> active = new List<Quest>();

        // ตัวสะสม "วันนี้"
        private float kToday, xToday, mToday, mSpentToday;
        private float lastK; private int lastM, lastX;

        void Start()
        {
            stats = Object.FindFirstObjectByType<PlayerStats>();
            clock = Object.FindFirstObjectByType<GameClock>();
            prog  = Object.FindFirstObjectByType<ProgressionManager>();

            if (stats != null)
            {
                lastK = stats.Knowledge; lastM = stats.Money; lastX = stats.Exp;
                stats.OnKnowledgeChanged += OnKnowledge;
                stats.OnMoneyChanged += OnMoney;
                stats.OnExpChanged += OnExp;
                stats.OnSatisfactionChanged += OnSat;
                stats.OnEnergyChanged += OnVital;
                stats.OnHealthChanged += OnVital;
                stats.OnHungerChanged += OnVital;
            }
            if (clock != null) clock.OnDayChanged += OnNewDay;

            NewDay();
        }

        void OnDestroy()
        {
            if (stats != null)
            {
                stats.OnKnowledgeChanged -= OnKnowledge;
                stats.OnMoneyChanged -= OnMoney;
                stats.OnExpChanged -= OnExp;
                stats.OnSatisfactionChanged -= OnSat;
                stats.OnEnergyChanged -= OnVital;
                stats.OnHealthChanged -= OnVital;
                stats.OnHungerChanged -= OnVital;
            }
            if (clock != null) clock.OnDayChanged -= OnNewDay;
        }

        // ---------- สะสมความคืบหน้า ----------
        void OnKnowledge(float v) { float d = v - lastK; if (d > 0) kToday += d; lastK = v; Refresh(); }
        void OnMoney(int v)       { int d = v - lastM; if (d > 0) mToday += d; else if (d < 0) mSpentToday += -d; lastM = v; Refresh(); }
        void OnExp(int v)         { int d = v - lastX; if (d > 0) xToday += d; lastX = v; Refresh(); }
        void OnSat(float v)       { Refresh(); }
        void OnVital(float a, float b) { Refresh(); }

        void OnNewDay(int _) => NewDay();

        // ---------- สุ่มภารกิจใหม่ทุกวัน ----------
        void NewDay()
        {
            kToday = xToday = mToday = mSpentToday = 0f;
            if (stats != null) { lastK = stats.Knowledge; lastM = stats.Money; lastX = stats.Exp; }

            active.Clear();

            // 1 ภารกิจตามชั้นปี (การันตี → แต่ละปีรู้สึกต่างกัน)
            int year = prog != null ? prog.CurrentYear : 1;
            var yq = YearQuests(year);
            if (yq.Count > 0) active.Add(yq[Random.Range(0, yq.Count)].Copy());

            // ที่เหลือสุ่มจากคลังทั่วไป
            var pool = Templates();
            for (int i = active.Count; i < dailyCount && pool.Count > 0; i++)
            {
                int r = Random.Range(0, pool.Count);
                active.Add(pool[r].Copy());
                pool.RemoveAt(r);
            }
            Refresh();
        }

        float Value(Metric m)
        {
            switch (m)
            {
                case Metric.KnowledgeGain:      return kToday;
                case Metric.ExpGain:            return xToday;
                case Metric.MoneyEarn:          return mToday;
                case Metric.MoneySpend:         return mSpentToday;
                case Metric.SatisfactionReach:  return stats != null ? stats.Satisfaction : 0f;
                case Metric.HungerReach:        return stats != null ? stats.Hunger : 0f;
                case Metric.EnergyReach:        return stats != null ? stats.Energy : 0f;
                case Metric.HealthReach:        return stats != null ? stats.Health : 0f;
                case Metric.MoneyReach:         return stats != null ? stats.Money : 0f;
            }
            return 0f;
        }

        void Refresh()
        {
            foreach (var q in active)
            {
                q.progress = Value(q.metric);
                if (!q.done && q.progress >= q.target)
                {
                    q.done = true;
                    if (stats != null)
                    {
                        if (q.rewardMoney != 0) stats.ChangeMoney(q.rewardMoney);
                        if (q.rewardExp != 0) stats.AddExp(q.rewardExp);
                        if (q.rewardSat != 0) stats.ChangeSatisfaction(q.rewardSat);
                    }
                    HUDController.Toast($"ภารกิจสำเร็จ! {q.desc}  (+{q.rewardMoney}฿)");
                    NisitSimulator.Core.SFXManager.Success();   // เสียงสำเร็จ (เด่นกว่า notify)
                }
            }
            UpdateUI();
        }

        void UpdateUI()
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                if (i < active.Count)
                {
                    var q = active[i];
                    if (q.done)
                    {
                        rows[i].text = $"<color=#7BE38B>{q.desc}  (สำเร็จ)</color>";
                    }
                    else
                    {
                        float p = Mathf.Min(q.progress, q.target);
                        rows[i].text = $"{q.desc}\n<size=80%><color=#B9C2D6>{p:0}/{q.target:0}</color></size>";
                    }
                }
                else rows[i].text = "";
            }
        }

        // สรุปภารกิจวันนี้ (ให้แอปโทรศัพท์เรียกใช้)
        public string SummaryText()
        {
            if (active.Count == 0) return "วันนี้ยังไม่มีภารกิจ";
            var sb = new System.Text.StringBuilder();
            foreach (var q in active)
            {
                if (q.done)
                    sb.AppendLine($"<color=#7BE38B>[เสร็จ] {q.desc}</color>");
                else
                    sb.AppendLine($"{q.desc}\n<size=80%><color=#B9C2D6>{Mathf.Min(q.progress, q.target):0}/{q.target:0}</color></size>");
            }
            return sb.ToString();
        }

        // ---------- คลังภารกิจ ----------
        List<Quest> Templates() => new List<Quest>
        {
            // การเรียน
            new Quest("ตั้งใจเรียน: ได้ความรู้ +60",   Metric.KnowledgeGain, 60f,  40, 20, 4f),
            new Quest("ขยันสุด ๆ: ได้ความรู้ +120",   Metric.KnowledgeGain, 120f, 80, 40, 6f),
            new Quest("ตักตวงความรู้ +90",             Metric.KnowledgeGain, 90f,  60, 30, 5f),
            new Quest("สะสม EXP +50 วันนี้",           Metric.ExpGain,       50f,  30, 0,  4f),
            // การเงิน
            new Quest("หารายได้: ได้เงิน +80฿",         Metric.MoneyEarn,     80f,  0,  25, 5f),
            new Quest("ช้อปปิ้ง: ใช้เงิน 60฿ วันนี้",    Metric.MoneySpend,    60f,  0,  15, 5f),
            new Quest("นักออม: มีเงินเก็บถึง 300฿",     Metric.MoneyReach,    300f, 0,  30, 6f),
            // ดูแลตัวเอง
            new Quest("ใช้ชีวิตให้มีความสุข: พอใจถึง 70", Metric.SatisfactionReach, 70f, 30, 15, 0f),
            new Quest("อิ่มท้อง: ความอิ่มถึง 80",       Metric.HungerReach,   80f,  20, 10, 3f),
            new Quest("พักผ่อนเพียงพอ: พลังงานถึง 80",  Metric.EnergyReach,   80f,  20, 10, 3f),
            new Quest("ดูแลสุขภาพ: สุขภาพถึง 90",       Metric.HealthReach,   90f,  30, 10, 3f),
        };

        // ---------- ภารกิจเฉพาะชั้นปี (แต่ละปีมีธีมต่างกัน) ----------
        List<Quest> YearQuests(int year)
        {
            switch (year)
            {
                case 1: return new List<Quest>   // น้องใหม่: ปรับตัว + รับน้อง
                {
                    new Quest("รับน้อง: ทำความรู้จักเพื่อน (พอใจถึง 65)", Metric.SatisfactionReach, 65f, 40, 20, 0f),
                    new Quest("ปรับตัวปี 1: ตั้งใจเรียน ได้ความรู้ +70",   Metric.KnowledgeGain,     70f, 50, 25, 5f),
                };
                case 2: return new List<Quest>   // ปี 2: วิชาเอก + ชมรม
                {
                    new Quest("กิจกรรมชมรม: ความพอใจถึง 75",              Metric.SatisfactionReach, 75f, 40, 25, 0f),
                    new Quest("โปรเจกต์กลุ่ม: ได้ความรู้ +100",            Metric.KnowledgeGain,    100f, 70, 35, 6f),
                };
                case 3: return new List<Quest>   // ปี 3: วิชาเข้มข้น + ฝึกงาน
                {
                    new Quest("ฝึกงานหน้าร้อน: หารายได้ +120฿",           Metric.MoneyEarn,        120f,  0, 40, 6f),
                    new Quest("วิชาเอกเข้มข้น: ได้ความรู้ +130",           Metric.KnowledgeGain,    130f, 90, 45, 7f),
                };
                default: return new List<Quest>  // ปี 4: โปรเจกต์จบ + เตรียมทำงาน
                {
                    new Quest("โปรเจกต์จบ: ได้ความรู้ +150",              Metric.KnowledgeGain,    150f, 120, 60, 8f),
                    new Quest("เตรียมเข้าทำงาน: มีเงินเก็บถึง 400฿",       Metric.MoneyReach,       400f,   0, 50, 8f),
                };
            }
        }
    }
}
