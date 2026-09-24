using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.Core;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Systems
{
    // เก็บสถิติสะสมตลอดการเล่น (เงินที่หาได้/ความรู้/เวลาเล่น/คาบเรียน/เควส/ของขวัญ ฯลฯ)
    //   auto-create (ไม่ต้องวางในฉาก) · poll ค่าเองทุกเฟรม (เลี่ยงปัญหาลำดับ event ตอนโหลดเซฟ)
    public class StatsTracker : MonoBehaviour
    {
        static StatsTracker _i;
        public static StatsTracker Instance
        {
            get
            {
                if (_i == null)
                {
                    _i = Object.FindFirstObjectByType<StatsTracker>();
                    if (_i == null) _i = new GameObject("StatsTracker").AddComponent<StatsTracker>();
                }
                return _i;
            }
        }

        readonly Dictionary<string, float> data = new Dictionary<string, float>();

        PlayerStats stats;
        GameClock clock;
        int lastMoney;
        float lastKnow;
        bool primed;

        void Awake()
        {
            if (_i != null && _i != this) { Destroy(gameObject); return; }
            _i = this;
            if (GameSession.IsContinue) Load();
        }

        void Update()
        {
            if (stats == null) stats = Object.FindFirstObjectByType<PlayerStats>();
            if (clock == null) clock = Object.FindFirstObjectByType<GameClock>();

            if (!primed)
            {
                if (stats != null) { lastMoney = stats.Money; lastKnow = stats.Knowledge; primed = true; }
                return;
            }

            if (stats != null)
            {
                if (stats.Money > lastMoney) Add("moneyEarned", stats.Money - lastMoney);
                lastMoney = stats.Money;
                if (stats.Knowledge > lastKnow) Add("knowledgeGained", stats.Knowledge - lastKnow);
                lastKnow = stats.Knowledge;
            }

            // เวลาเล่น (นับเฉพาะตอนเกมเดินอยู่ ไม่นับตอน pause)
            if (GameManager.Instance == null || GameManager.Instance.IsActive)
                Add("playtime", Time.unscaledDeltaTime);

            if (clock != null) Set("maxDay", Mathf.Max(Get("maxDay"), clock.Day));
        }

        public float Get(string key) => data.TryGetValue(key, out var v) ? v : 0f;
        public int GetInt(string key) => Mathf.RoundToInt(Get(key));
        public void Add(string key, float amount) { data[key] = Get(key) + amount; }
        public void Set(string key, float value) { data[key] = value; }

        // เวลาเล่นเป็นข้อความ (ชม./นาที)
        public string PlaytimeText()
        {
            int sec = GetInt("playtime");
            int h = sec / 3600, m = (sec % 3600) / 60;
            return h > 0 ? $"{h} ชม. {m} นาที" : $"{m} นาที";
        }

        // ---------- เซฟ/โหลด ----------
        public void CollectSave(SaveData d)
        {
            d.statKeys = new List<string>();
            d.statVals = new List<float>();
            foreach (var kv in data) { d.statKeys.Add(kv.Key); d.statVals.Add(kv.Value); }
        }

        void Load()
        {
            var d = SaveSystem.Load();
            if (d == null || d.statKeys == null || d.statVals == null) return;
            data.Clear();
            int n = Mathf.Min(d.statKeys.Count, d.statVals.Count);
            for (int i = 0; i < n; i++) data[d.statKeys[i]] = d.statVals[i];
        }
    }
}
