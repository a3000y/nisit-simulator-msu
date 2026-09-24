using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.UI;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Systems
{
    // ระบบความสำเร็จ (achievements) — เช็คเงื่อนไขจากสถิติ/ความสัมพันธ์/เกรด แล้วปลดล็อก
    //   auto-create · เช็คทุก 2 วิ · เซฟรายการที่ปลดล็อกแล้ว
    public class AchievementManager : MonoBehaviour
    {
        public class Ach
        {
            public string id, title, desc;
            public System.Func<bool> cond;
            public Ach(string id, string title, string desc, System.Func<bool> cond)
            { this.id = id; this.title = title; this.desc = desc; this.cond = cond; }
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

        readonly List<Ach> all = new List<Ach>();
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

        void Awake()
        {
            if (_i != null && _i != this) { Destroy(gameObject); return; }
            _i = this;
            Build();
            if (GameSession.IsContinue) LoadUnlocked();
        }

        void Build()
        {
            all.Clear();
            all.Add(new Ach("class10",  "นักเรียนขยัน",  "เข้าเรียนครบ 10 คาบ",       () => Stat("classes") >= 10));
            all.Add(new Ach("class30",  "ตัวยงห้องเรียน", "เข้าเรียนครบ 30 คาบ",       () => Stat("classes") >= 30));
            all.Add(new Ach("quest10",  "มือปราบภารกิจ", "ทำภารกิจสำเร็จ 10 ครั้ง",   () => Stat("quests") >= 10));
            all.Add(new Ach("friend5",  "มีเพื่อนเยอะ",  "มีเพื่อน 5 คน",             () => Friends() >= 5));
            all.Add(new Ach("friend10", "ป็อปปูลาร์",    "มีเพื่อน 10 คน",            () => Friends() >= 10));
            all.Add(new Ach("gift5",    "ใจบุญ",         "ให้ของขวัญเพื่อน 5 ครั้ง",  () => Stat("gifts") >= 5));
            all.Add(new Ach("earn2000", "นักหารายได้",   "หาเงินสะสมได้ 2,000฿",      () => Stat("moneyEarned") >= 2000));
            all.Add(new Ach("rich5000", "เศรษฐีน้อย",    "มีเงินในกระเป๋า 5,000฿",    () => Money() >= 5000));
            all.Add(new Ach("gpa30",    "เด็กเรียน",      "ทำเกรดเฉลี่ยถึง 3.00",      () => Gpa() >= 3.0f));
            all.Add(new Ach("gpa35",    "เกียรตินิยม",   "ทำเกรดเฉลี่ยถึง 3.50",      () => Gpa() >= 3.5f));
            all.Add(new Ach("know2000", "ขุมทรัพย์ความรู้","สะสมความรู้รวม 2,000",     () => Stat("knowledgeGained") >= 2000));
            all.Add(new Ach("year4",    "ใกล้จบแล้ว",     "ขึ้นถึงชั้นปีที่ 4",         () => Year() >= 4));
            all.Add(new Ach("survive20","เอาตัวรอด",      "ใช้ชีวิตในมหาลัยครบ 20 วัน", () => Stat("maxDay") >= 20));
        }

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 2f;
            foreach (var a in all)
                if (!unlocked.Contains(a.id) && a.cond != null && a.cond())
                    Unlock(a);
        }

        void Unlock(Ach a)
        {
            unlocked.Add(a.id);
            HUDController.Toast($"🏆 ปลดล็อกความสำเร็จ: {a.title}!");
            NisitSimulator.Core.SFXManager.Success();
            var s = PS; if (s != null) s.ChangeSatisfaction(5f);   // โบนัสความภูมิใจ
        }

        // ---------- ให้ UI อ่าน ----------
        public IReadOnlyList<Ach> All => all;
        public bool IsUnlocked(string id) => unlocked.Contains(id);
        public int UnlockedCount => unlocked.Count;
        public int Total => all.Count;

        // ---------- เซฟ/โหลด ----------
        public void CollectSave(SaveData d)
        {
            d.unlockedAchievements = new List<string>(unlocked);
        }

        void LoadUnlocked()
        {
            var d = SaveSystem.Load();
            if (d == null || d.unlockedAchievements == null) return;
            unlocked.Clear();
            foreach (var id in d.unlockedAchievements) unlocked.Add(id);
        }
    }
}
