using System.Collections.Generic;
using UnityEngine;
using TMPro;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;
using NisitSimulator.SaveLoad;
using NisitSimulator.Interaction;

namespace NisitSimulator.Systems
{
    // ระบบภารกิจรายวัน: 3 ภารกิจ/วัน (ภารกิจชั้นปี 1 + ภารกิจ "ไปทำ" 2) ทำสำเร็จได้รางวัล รีเซ็ตทุกวัน
    //   ภารกิจ "ไปทำ" นับจาก GameplayEvents (เข้าเรียน/กินข้าว/ทำงาน/คุย/ให้ของขวัญ/อ่านหนังสือ/ช้อป/ใช้ไอเทม)
    //   ทำครบทั้งวัน → โบนัส + นับวันต่อเนื่อง (streak) ยิ่งต่อเนื่องยิ่งได้เยอะ
    //   UI แถวภารกิจถูกสร้าง+ต่อโดย Editor tool (Nisit -> Build Quest System)
    public class QuestSystem : MonoBehaviour
    {
        public enum Metric
        {
            KnowledgeGain, ExpGain, MoneyEarn, SatisfactionReach,
            HungerReach, EnergyReach, HealthReach, MoneySpend, MoneyReach,
            Action   // นับจำนวนครั้งที่ทำกิจกรรม actionKey
        }

        [System.Serializable]
        public class Quest
        {
            public string desc;
            public Metric metric;
            public string actionKey;
            public float target;
            public int rewardMoney;
            public int rewardExp;
            public float rewardSat;
            [HideInInspector] public float progress;
            [HideInInspector] public bool done;
            public Quest(string d, Metric m, float t, int money, int exp, float sat)
            { desc = d; metric = m; target = t; rewardMoney = money; rewardExp = exp; rewardSat = sat; }
            public Quest(string d, string action, int count, int money, int exp, float sat)
            { desc = d; metric = Metric.Action; actionKey = action; target = count; rewardMoney = money; rewardExp = exp; rewardSat = sat; }
            public Quest Copy() => new Quest(desc, metric, target, rewardMoney, rewardExp, rewardSat) { actionKey = actionKey };
        }

        [Header("UI (เซ็ตโดย Editor)")]
        public TMP_Text[] rows = new TMP_Text[3];
        public int dailyCount = 3;

        private PlayerStats stats;
        private GameClock clock;
        private ProgressionManager prog;
        private readonly List<Quest> active = new List<Quest>();
        private TMP_Text header;

        // ตัวสะสม "วันนี้"
        private float kToday, xToday, mToday, mSpentToday;
        private float lastK; private int lastM, lastX;
        private readonly Dictionary<string, int> actToday = new Dictionary<string, int>();

        // ต่อเนื่อง
        private int streak, lastAllDoneDay = -99;
        private int questDay = -1;   // วันของนาฬิกาที่ภารกิจชุดนี้เป็นของ (กันสุ่มใหม่ซ้ำเมื่อ GameClock ยิงวันเดิมตอนเริ่มฉาก)
        private bool bonusToday;

        // ผลของเมื่อวาน (ให้หน้าสรุปวันอ่าน)
        public int LastDayDone { get; private set; }
        public int LastDayTotal { get; private set; }
        public bool LastDayAllDone { get; private set; }
        public int Streak => streak;
        public int DoneCount { get { int n = 0; foreach (var q in active) if (q.done) n++; return n; } }
        public int Total => active.Count;

        int Today => clock != null ? clock.Day : 1;

        void Start()
        {
            stats = Object.FindFirstObjectByType<PlayerStats>();
            clock = Object.FindFirstObjectByType<GameClock>();
            prog  = Object.FindFirstObjectByType<ProgressionManager>();
            FindHeader();

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
            GameplayEvents.OnAction += OnAction;

            // เล่นต่อ → คืนเควสของวันนี้ (ไม่สุ่มใหม่/ไม่รีเซ็ตความคืบหน้า) · ไม่ได้ = เกมใหม่/เซฟเก่า → สุ่มปกติ
            if (!(GameSession.IsContinue && TryRestore())) NewDay();
        }

        // คืนเควสจากเซฟ — คืน true ถ้าคืนสำเร็จ
        bool TryRestore()
        {
            var d = SaveSystem.Load();
            if (d == null || !d.hasQuestData || d.quests == null || d.quests.Count == 0) return false;

            active.Clear();
            foreach (var q in d.quests)
                active.Add(new Quest(q.desc, (Metric)q.metric, q.target, q.rewardMoney, q.rewardExp, q.rewardSat) { actionKey = q.actionKey, done = q.done });

            kToday = d.questAccK; xToday = d.questAccX; mToday = d.questAccM; mSpentToday = d.questAccSpent;
            lastK = d.questLastK; lastM = d.questLastM; lastX = d.questLastX;
            actToday.Clear();
            if (d.questActKeys != null && d.questActVals != null)
                for (int i = 0; i < d.questActKeys.Count && i < d.questActVals.Count; i++) actToday[d.questActKeys[i]] = d.questActVals[i];
            streak = d.questStreak; lastAllDoneDay = d.questLastAllDay; bonusToday = d.questBonusToday;
            questDay = d.gameDay;

            // อัปเดตหน้าจออย่างเดียว ไม่แจกรางวัลตอนนี้ — เพราะค่าสถานะอาจยังไม่ถูกคืน
            foreach (var q in active) q.progress = Value(q);
            UpdateUI();
            return true;
        }

        // ให้ SaveManager เก็บเควส + ตัวสะสมของวันนี้
        public void CollectSave(SaveData d)
        {
            d.hasQuestData = true;
            d.quests = new List<QuestSave>();
            foreach (var q in active)
                d.quests.Add(new QuestSave
                {
                    desc = q.desc, metric = (int)q.metric, actionKey = q.actionKey, target = q.target,
                    rewardMoney = q.rewardMoney, rewardExp = q.rewardExp, rewardSat = q.rewardSat, done = q.done
                });
            d.questAccK = kToday; d.questAccX = xToday; d.questAccM = mToday; d.questAccSpent = mSpentToday;
            d.questLastK = lastK; d.questLastM = lastM; d.questLastX = lastX;
            d.questActKeys = new List<string>(actToday.Keys);
            d.questActVals = new List<int>();
            foreach (var k in d.questActKeys) d.questActVals.Add(actToday[k]);
            d.questStreak = streak; d.questLastAllDay = lastAllDoneDay; d.questBonusToday = bonusToday;
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
            GameplayEvents.OnAction -= OnAction;
        }

        // ---------- สะสมความคืบหน้า ----------
        void OnKnowledge(float v) { float d = v - lastK; if (d > 0) kToday += d; lastK = v; Refresh(); }
        void OnMoney(int v)       { int d = v - lastM; if (d > 0) mToday += d; else if (d < 0) mSpentToday += -d; lastM = v; Refresh(); }
        void OnExp(int v)         { int d = v - lastX; if (d > 0) xToday += d; lastX = v; Refresh(); }
        void OnSat(float v)       { Refresh(); }
        void OnVital(float a, float b) { Refresh(); }
        void OnAction(string key)
        {
            actToday[key] = (actToday.TryGetValue(key, out var n) ? n : 0) + 1;
            Refresh();
        }

        void OnNewDay(int day)
        {
            if (day <= questDay) return;   // วันเดิม (เช่น GameClock.Start ยิงตอนเริ่มฉาก/หลังโหลดเซฟ) → ไม่สุ่มใหม่
            NewDay();
        }

        // ---------- สุ่มภารกิจใหม่ทุกวัน ----------
        void NewDay()
        {
            // เก็บผลของวันที่ผ่านมาไว้ให้หน้าสรุป
            LastDayTotal = active.Count; LastDayDone = DoneCount; LastDayAllDone = active.Count > 0 && LastDayDone == active.Count;
            // ไม่ครบเมื่อวาน → ขาดตอน
            if (lastAllDoneDay < Today - 1) streak = 0;
            bonusToday = false;

            questDay = Today;
            kToday = xToday = mToday = mSpentToday = 0f;
            actToday.Clear();
            if (stats != null) { lastK = stats.Knowledge; lastM = stats.Money; lastX = stats.Exp; }

            active.Clear();

            // 1 ภารกิจตามชั้นปี (การันตี → แต่ละปีรู้สึกต่างกัน)
            int year = prog != null ? prog.CurrentYear : 1;
            var yq = Available(YearQuests(year));
            if (yq.Count > 0) active.Add(yq[Random.Range(0, yq.Count)].Copy());

            // ภารกิจ "ไปทำ" — ไม่ซ้ำกิจกรรมกัน
            var pool = Available(ActionQuests());
            pool.RemoveAll(q => active.Exists(a => a.metric == Metric.Action && a.actionKey == q.actionKey));
            for (int i = active.Count; i < dailyCount && pool.Count > 0; i++)
            {
                int r = Random.Range(0, pool.Count);
                var q = pool[r];
                active.Add(q.Copy());
                pool.RemoveAll(x => x.actionKey == q.actionKey);
            }
            // สำรอง (ฉากไม่มีสถานีพอ) → ภารกิจค่าสถานะแบบเดิม
            var fallback = Templates();
            while (active.Count < dailyCount && fallback.Count > 0)
            {
                int r = Random.Range(0, fallback.Count);
                active.Add(fallback[r].Copy()); fallback.RemoveAt(r);
            }
            Refresh();
        }

        // กรองภารกิจที่ทำได้จริงในฉากนี้ (มีสถานี/NPC ที่เกี่ยวข้อง)
        List<Quest> Available(List<Quest> list)
        {
            list.RemoveAll(q => q.metric == Metric.Action && !CanDo(q.actionKey));
            return list;
        }

        static readonly Dictionary<string, bool> canDoCache = new Dictionary<string, bool>();
        static bool CanDo(string key)
        {
            if (canDoCache.TryGetValue(key, out var c)) return c;
            bool ok = true;
            switch (key)
            {
                case GameplayEvents.Class: ok = Object.FindFirstObjectByType<ClassStation>(FindObjectsInactive.Include) != null; break;
                case GameplayEvents.Work:  ok = Object.FindFirstObjectByType<WorkStation>(FindObjectsInactive.Include) != null; break;
                case GameplayEvents.Study:
                case GameplayEvents.Relax: ok = Object.FindFirstObjectByType<ActivitySpot>(FindObjectsInactive.Include) != null
                                             || Object.FindFirstObjectByType<ActivityStation>(FindObjectsInactive.Include) != null; break;
                case GameplayEvents.Talk:
                case GameplayEvents.Gift:  ok = Object.FindFirstObjectByType<TalkNPC>(FindObjectsInactive.Include) != null; break;
                case GameplayEvents.Eat:
                case GameplayEvents.Buy:   ok = Object.FindFirstObjectByType<ShopController>(FindObjectsInactive.Include) != null; break;
            }
            canDoCache[key] = ok;
            return ok;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearCache() => canDoCache.Clear();

        float Value(Quest q)
        {
            switch (q.metric)
            {
                case Metric.Action:             return q.actionKey != null && actToday.TryGetValue(q.actionKey, out var n) ? n : 0;
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
                q.progress = Value(q);
                if (!q.done && q.progress >= q.target)
                {
                    q.done = true;
                    if (stats != null)
                    {
                        if (q.rewardMoney != 0) stats.ChangeMoney(q.rewardMoney);
                        if (q.rewardExp != 0) stats.AddExp(q.rewardExp);
                        if (q.rewardSat != 0) stats.ChangeSatisfaction(q.rewardSat);
                    }
                    HUDController.Toast($"ภารกิจสำเร็จ! {q.desc}  (+{q.rewardMoney}฿ +{q.rewardExp} EXP)");
                    NisitSimulator.Core.SFXManager.Success();
                    StatsTracker.Instance.Add("quests", 1);
                }
            }
            CheckAllDone();
            UpdateUI();
        }

        // ทำครบทุกข้อของวัน → โบนัส (ครั้งเดียวต่อวัน) + ต่อเนื่อง
        void CheckAllDone()
        {
            if (bonusToday || active.Count == 0 || DoneCount < active.Count) return;
            bonusToday = true;
            streak = (lastAllDoneDay == Today - 1) ? streak + 1 : 1;
            lastAllDoneDay = Today;
            int s = Mathf.Min(streak, 10);
            int money = 40 + 15 * (s - 1), exp = 30 + 10 * (s - 1);
            if (stats != null) { stats.ChangeMoney(money); stats.AddExp(exp); stats.ChangeSatisfaction(4f); }
            StatsTracker.Instance.Add("allQuestDays", 1);
            if (streak > StatsTracker.Instance.GetInt("bestStreak")) StatsTracker.Instance.Set("bestStreak", streak);
            HUDController.Toast(streak > 1
                ? $"ทำภารกิจครบ {streak} วันติด! โบนัส +{money}฿ +{exp} EXP"
                : $"ทำภารกิจครบวันนี้! โบนัส +{money}฿ +{exp} EXP (ทำครบพรุ่งนี้อีกได้เพิ่ม)");
        }

        void FindHeader()
        {
            if (rows == null || rows.Length == 0 || rows[0] == null) return;
            var parent = rows[0].transform.parent;
            if (parent == null) return;
            foreach (var t in parent.GetComponentsInChildren<TMP_Text>(true))
                if (t != null && System.Array.IndexOf(rows, t) < 0 && t.text.StartsWith("ภารกิจวันนี้")) { header = t; break; }
        }

        void UpdateUI()
        {
            if (header != null)
                header.text = streak > 0 ? $"ภารกิจวันนี้  <size=75%><color=#F2A63A>ต่อเนื่อง {streak} วัน</color></size>" : "ภารกิจวันนี้";
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                if (i < active.Count)
                {
                    var q = active[i];
                    if (q.done)
                        rows[i].text = $"<color=#7BE38B>{q.desc}  (สำเร็จ)</color>";
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
            if (streak > 0) sb.AppendLine($"<color=#F2A63A>ทำครบต่อเนื่อง {streak} วัน</color>");
            return sb.ToString();
        }

        // รายการภารกิจวันนี้แบบบรรทัดเดียว (หน้าสรุปวัน)
        public string TodayPlainText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var q in active) sb.AppendLine($"•  {q.desc}  <size=80%><color=#8A83A6>(+{q.rewardMoney}฿ +{q.rewardExp} EXP)</color></size>");
            return sb.ToString();
        }

        // ---------- คลังภารกิจ "ไปทำ" ----------
        List<Quest> ActionQuests() => new List<Quest>
        {
            new Quest("เข้าเรียนให้ครบ 2 คาบ",           GameplayEvents.Class,   2, 40, 30, 4f),
            new Quest("กินข้าวที่โรงอาหาร",              GameplayEvents.Eat,     1, 20, 15, 3f),
            new Quest("ทำงานพาร์ทไทม์ 1 กะ",             GameplayEvents.Work,    1, 20, 20, 2f),
            new Quest("คุยกับเพื่อน 3 ครั้ง",              GameplayEvents.Talk,    3, 25, 20, 4f),
            new Quest("ให้ของขวัญเพื่อน 1 ชิ้น",           GameplayEvents.Gift,    1, 30, 25, 5f),
            new Quest("อ่านหนังสือ/ติว 3 รอบ",            GameplayEvents.Study,   3, 35, 25, 3f),
            new Quest("ซื้อของที่ร้านค้า",                GameplayEvents.Buy,     1, 15, 10, 2f),
            new Quest("ใช้ไอเทมจากกระเป๋า 1 ชิ้น",        GameplayEvents.UseItem, 1, 15, 10, 2f),
            new Quest("พักผ่อนคลายเครียด 2 รอบ",          GameplayEvents.Relax,   2, 20, 15, 5f),
        };

        // ---------- ภารกิจค่าสถานะ (สำรอง) ----------
        List<Quest> Templates() => new List<Quest>
        {
            new Quest("ตั้งใจเรียน: ได้ความรู้ +60",   Metric.KnowledgeGain, 60f,  40, 20, 4f),
            new Quest("หารายได้: ได้เงิน +80฿",         Metric.MoneyEarn,     80f,  0,  25, 5f),
            new Quest("ใช้ชีวิตให้มีความสุข: พอใจถึง 70", Metric.SatisfactionReach, 70f, 30, 15, 0f),
            new Quest("อิ่มท้อง: ความอิ่มถึง 80",       Metric.HungerReach,   80f,  20, 10, 3f),
            new Quest("พักผ่อนเพียงพอ: พลังงานถึง 80",  Metric.EnergyReach,   80f,  20, 10, 3f),
        };

        // ---------- ภารกิจเฉพาะชั้นปี (แต่ละปีมีธีมต่างกัน) ----------
        List<Quest> YearQuests(int year)
        {
            switch (year)
            {
                case 1: return new List<Quest>   // น้องใหม่: ปรับตัว + รับน้อง
                {
                    new Quest("รับน้อง: คุยทำความรู้จักเพื่อน 2 ครั้ง", GameplayEvents.Talk,  2, 40, 20, 5f),
                    new Quest("ปรับตัวปี 1: ตั้งใจเรียน ได้ความรู้ +70", Metric.KnowledgeGain, 70f, 50, 25, 5f),
                };
                case 2: return new List<Quest>   // ปี 2: วิชาเอก + ชมรม
                {
                    new Quest("กิจกรรมชมรม: พักผ่อนกับเพื่อน 2 รอบ",   GameplayEvents.Relax, 2, 40, 25, 6f),
                    new Quest("โปรเจกต์กลุ่ม: ได้ความรู้ +100",          Metric.KnowledgeGain, 100f, 70, 35, 6f),
                };
                case 3: return new List<Quest>   // ปี 3: วิชาเข้มข้น + ฝึกงาน
                {
                    new Quest("ฝึกงาน: ทำงานพาร์ทไทม์ 2 กะ",            GameplayEvents.Work,  2, 30, 40, 4f),
                    new Quest("วิชาเอกเข้มข้น: ได้ความรู้ +130",         Metric.KnowledgeGain, 130f, 90, 45, 7f),
                };
                default: return new List<Quest>  // ปี 4: โปรเจกต์จบ + เตรียมทำงาน
                {
                    new Quest("โปรเจกต์จบ: ได้ความรู้ +150",            Metric.KnowledgeGain, 150f, 120, 60, 8f),
                    new Quest("เตรียมเข้าทำงาน: มีเงินเก็บถึง 400฿",     Metric.MoneyReach,    400f,   0, 50, 8f),
                };
            }
        }
    }
}
