using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.UI;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Systems
{
    // ระบบความสำเร็จ (achievements) — เช็คเงื่อนไขจากสถิติ/ความสัมพันธ์/เกรด/เลเวล แล้วปลดล็อก
    //   ปลดล็อกแล้วได้รางวัล (เงิน/EXP) + บางอันปลดล็อกของแต่งตัว/ชุดในหน้าสร้างตัวละคร (CosmeticUnlocks)
    //   auto-create · เช็คทุก 2 วิ · เก็บในโปรไฟล์ถาวร (PlayerPrefs) — ปลดแล้วไม่หายแม้เริ่มเกมใหม่
    public class AchievementManager : MonoBehaviour
    {
        // ข้อมูลความสำเร็จ (static — หน้าเมนู/แต่งตัวอ่านได้โดยไม่ต้องสร้าง manager)
        public class Info
        {
            public string id, title, desc;
            public int money, exp;
            public Info(string id, string title, string desc, int money, int exp)
            { this.id = id; this.title = title; this.desc = desc; this.money = money; this.exp = exp; }
        }

        public static readonly Info[] Defs =
        {
            new Info("class10",  "นักเรียนขยัน",     "เข้าเรียนครบ 10 คาบ",            60,  40),
            new Info("class30",  "ตัวยงห้องเรียน",    "เข้าเรียนครบ 30 คาบ",           150, 100),
            new Info("quest10",  "มือปราบภารกิจ",    "ทำภารกิจสำเร็จ 10 ครั้ง",         80,  60),
            new Info("friend5",  "มีเพื่อนเยอะ",     "มีเพื่อน 5 คน",                   80,  50),
            new Info("friend10", "ป็อปปูลาร์",       "มีเพื่อน 10 คน",                 150, 100),
            new Info("gift5",    "ใจบุญ",            "ให้ของขวัญเพื่อน 5 ครั้ง",         60,  40),
            new Info("earn2000", "นักหารายได้",      "หาเงินสะสมได้ 2,000฿",            100,  60),
            new Info("rich5000", "เศรษฐีน้อย",       "มีเงินในกระเป๋า 5,000฿",            0, 150),
            new Info("gpa30",    "เด็กเรียน",         "ทำเกรดเฉลี่ยถึง 3.00",            100,  80),
            new Info("gpa35",    "เกียรตินิยม",      "ทำเกรดเฉลี่ยถึง 3.50",            200, 150),
            new Info("know2000", "ขุมทรัพย์ความรู้", "สะสมความรู้รวม 2,000",             120,  80),
            new Info("year4",    "ใกล้จบแล้ว",       "ขึ้นถึงชั้นปีที่ 4",                150, 100),
            new Info("survive20","เอาตัวรอด",        "ใช้ชีวิตในมหาลัยครบ 20 วัน",       100,  80),
            // ใหม่: เลเวล / ภารกิจต่อเนื่อง
            new Info("level5",   "รุ่นพี่มือใหม่",    "ถึงเลเวล 5",                        60,   0),
            new Info("level10",  "นิสิตเก๋า",         "ถึงเลเวล 10",                      150,   0),
            new Info("streak3",  "วินัยดี",           "ทำภารกิจครบ 3 วันติด",              80,  60),
            new Info("streak7",  "ไม่มีวันหยุด",      "ทำภารกิจครบ 7 วันติด",             200, 150),
            new Info("allday10", "นักล่าภารกิจ",      "ทำภารกิจครบทั้งวันรวม 10 วัน",       150, 100),
        };

        public static Info Find(string id) { foreach (var d in Defs) if (d.id == id) return d; return null; }
        public static string TitleOf(string id) { var d = Find(id); return d != null ? d.title : id; }

        const string PrefKey = "nisit_ach";   // โปรไฟล์ถาวร (คงข้ามรอบ New Game+)

        // อ่านจากโปรไฟล์ถาวรตรง ๆ (ใช้ในเมนู/หน้าแต่งตัว — ไม่สร้าง manager)
        public static bool ProfileHas(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            if (_i != null) return _i.unlocked.Contains(id);
            string csv = PlayerPrefs.GetString(PrefKey, "");
            foreach (var s in csv.Split(',')) if (s == id) return true;
            return false;
        }

        static AchievementManager _i;
        public static AchievementManager Instance
        {
            get
            {
                if (_i == null)
                {
                    _i = Object.FindFirstObjectByType<AchievementManager>();
                    if (_i == null) _i = new GameObject("AchievementManager").AddComponent<AchievementManager>();
                }
                return _i;
            }
        }

        readonly Dictionary<string, System.Func<bool>> conds = new Dictionary<string, System.Func<bool>>();
        readonly HashSet<string> unlocked = new HashSet<string>();
        float nextCheck;

        PlayerStats _ps; ExamController _ex; ProgressionManager _pr;
        PlayerStats PS => _ps != null ? _ps : (_ps = Object.FindFirstObjectByType<PlayerStats>());
        ExamController EX => _ex != null ? _ex : (_ex = Object.FindFirstObjectByType<ExamController>());
        ProgressionManager PR => _pr != null ? _pr : (_pr = Object.FindFirstObjectByType<ProgressionManager>());

        int Stat(string k) => StatsTracker.Instance.GetInt(k);
        int Money() { var s = PS; return s != null ? s.Money : 0; }
        float Gpa() { var e = EX; return e != null ? e.GPA : 0f; }
        int Year() { var p = PR; return p != null ? p.CurrentYear : 1; }
        int Friends() => RelationshipManager.Instance.FriendCount;
        int Lvl() => LevelSystem.Instance != null ? LevelSystem.Instance.Level : 1;

        void Awake()
        {
            if (_i != null && _i != this) { Destroy(gameObject); return; }
            _i = this;
            Build();
            LoadAll();
        }

        void Build()
        {
            conds.Clear();
            conds["class10"]  = () => Stat("classes") >= 10;
            conds["class30"]  = () => Stat("classes") >= 30;
            conds["quest10"]  = () => Stat("quests") >= 10;
            conds["friend5"]  = () => Friends() >= 5;
            conds["friend10"] = () => Friends() >= 10;
            conds["gift5"]    = () => Stat("gifts") >= 5;
            conds["earn2000"] = () => Stat("moneyEarned") >= 2000;
            conds["rich5000"] = () => Money() >= 5000;
            conds["gpa30"]    = () => Gpa() >= 3.0f;
            conds["gpa35"]    = () => Gpa() >= 3.5f;
            conds["know2000"] = () => Stat("knowledgeGained") >= 2000;
            conds["year4"]    = () => Year() >= 4;
            conds["survive20"]= () => Stat("maxDay") >= 20;
            conds["level5"]   = () => Lvl() >= 5;
            conds["level10"]  = () => Lvl() >= 10;
            conds["streak3"]  = () => Stat("bestStreak") >= 3;
            conds["streak7"]  = () => Stat("bestStreak") >= 7;
            conds["allday10"] = () => Stat("allQuestDays") >= 10;
        }

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 2f;
            if (PS == null) return;   // ไม่ใช่ฉากเกม (เมนู) → ไม่เช็ค
            foreach (var d in Defs)
                if (!unlocked.Contains(d.id) && conds.TryGetValue(d.id, out var c) && c())
                    Unlock(d);
        }

        void Unlock(Info a)
        {
            unlocked.Add(a.id);
            SaveToPrefs();   // เก็บลงโปรไฟล์ถาวรทันที
            var s = PS;
            if (s != null)
            {
                s.ChangeSatisfaction(5f);   // โบนัสความภูมิใจ
                if (a.money > 0) s.ChangeMoney(a.money);
                if (a.exp > 0) s.AddExp(a.exp);
            }
            string reward = RewardText(a);
            string unlocks = CosmeticUnlocks.UnlocksText(a.id);
            HUDController.Toast($"ปลดล็อกความสำเร็จ: {a.title}!  {reward}" + (unlocks.Length > 0 ? $"  · ปลดล็อก {unlocks}" : ""));
            NisitSimulator.Core.SFXManager.Success();
        }

        public static string RewardText(Info a)
        {
            var parts = new List<string>();
            if (a.money > 0) parts.Add($"+{a.money}฿");
            if (a.exp > 0) parts.Add($"+{a.exp} EXP");
            return string.Join(" ", parts.ToArray());
        }

        // ---------- ให้ UI อ่าน ----------
        public IReadOnlyList<Info> All => Defs;
        public bool IsUnlocked(string id) => unlocked.Contains(id);
        public int UnlockedCount { get { int n = 0; foreach (var d in Defs) if (unlocked.Contains(d.id)) n++; return n; } }
        public int Total => Defs.Length;

        // ---------- เซฟ/โหลด ----------
        public void CollectSave(SaveData d)
        {
            d.unlockedAchievements = new List<string>(unlocked);
        }

        // โหลดจากโปรไฟล์ถาวร (PlayerPrefs) + รวมกับเซฟปัจจุบัน (union — ปลดล็อกแล้วไม่หาย)
        void LoadAll()
        {
            string csv = PlayerPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(csv))
                foreach (var id in csv.Split(',')) if (!string.IsNullOrEmpty(id)) unlocked.Add(id);

            if (GameSession.IsContinue)
            {
                var d = SaveSystem.Load();
                if (d != null && d.unlockedAchievements != null)
                    foreach (var id in d.unlockedAchievements) unlocked.Add(id);
            }
        }

        void SaveToPrefs()
        {
            PlayerPrefs.SetString(PrefKey, string.Join(",", unlocked));
            PlayerPrefs.Save();
        }
    }
}
