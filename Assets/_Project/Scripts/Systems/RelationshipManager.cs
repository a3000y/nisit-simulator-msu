using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.UI;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Systems
{
    // ระบบความสัมพันธ์กับ NPC — คุยแล้วค่าสนิทเพิ่ม เลื่อนระดับ (แปลกหน้า→รู้จัก→เพื่อน→สนิท→ซี้)
    //   สร้างอัตโนมัติเมื่อถูกเรียกครั้งแรก (ไม่ต้องวางในฉาก) · คืนค่าจากเซฟตอน "เล่นต่อ"
    public class RelationshipManager : MonoBehaviour
    {
        // เกณฑ์คะแนนของแต่ละระดับ + ชื่อ
        static readonly int[] Thresholds = { 0, 20, 50, 90, 140 };
        static readonly string[] LevelNames = { "คนแปลกหน้า", "รู้จักกัน", "เพื่อน", "เพื่อนสนิท", "เพื่อนซี้" };
        public const int MaxPoints = 200;

        readonly Dictionary<string, int> points = new Dictionary<string, int>();
        readonly Dictionary<string, string> names = new Dictionary<string, string>();   // id -> ชื่อโชว์ (ลงทะเบียนโดย NPC)
        readonly List<string> order = new List<string>();                                // ลำดับที่พบ (ไว้โชว์ในรายชื่อ)

        static RelationshipManager _i;
        public static RelationshipManager Instance
        {
            get
            {
                if (_i == null)
                {
                    _i = Object.FindFirstObjectByType<RelationshipManager>();
                    if (_i == null)
                    {
                        var go = new GameObject("RelationshipManager");
                        _i = go.AddComponent<RelationshipManager>();
                    }
                }
                return _i;
            }
        }

        void Awake()
        {
            if (_i != null && _i != this) { Destroy(gameObject); return; }
            _i = this;
            if (GameSession.IsContinue) LoadFromSave();   // เล่นต่อ → คืนค่าสนิท
        }

        // NPC ลงทะเบียนตัวเองตอน Start (เพื่อให้หน้ารายชื่อเพื่อนโชว์ครบทุกคน)
        public void Register(string id, string displayName)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!names.ContainsKey(id)) order.Add(id);
            names[id] = displayName;
        }

        public IReadOnlyList<string> AllIds => order;
        public string DisplayName(string id) => (id != null && names.TryGetValue(id, out var n)) ? n : id;

        // นับ NPC ที่สนิทถึงระดับ lvl ขึ้นไป (ใช้โบนัส/สรุปตอนจบ)
        public int CountAtLeast(int lvl)
        {
            int c = 0;
            foreach (var kv in points) if (LevelOf(kv.Value) >= lvl) c++;
            return c;
        }
        public int FriendCount => CountAtLeast(2);   // "เพื่อน" ขึ้นไป

        // ---------- คะแนน/ระดับ ----------
        public int GetPoints(string id) => (id != null && points.TryGetValue(id, out var v)) ? v : 0;

        public static int LevelOf(int pts)
        {
            int lvl = 0;
            for (int i = 0; i < Thresholds.Length; i++) if (pts >= Thresholds[i]) lvl = i;
            return lvl;
        }
        public int GetLevel(string id) => LevelOf(GetPoints(id));
        public static string NameOfLevel(int lvl) => (lvl >= 0 && lvl < LevelNames.Length) ? LevelNames[lvl] : "";

        // หัวใจตามระดับ (ไว้โชว์ในป้าย) — ระดับ 0 = ไม่มี
        public static string Hearts(int lvl)
        {
            if (lvl <= 0) return "";
            return new string('♥', Mathf.Min(lvl, 4));
        }

        // เพิ่มคะแนนสนิท + เช็คเลื่อนระดับ (โบนัส + แจ้งเตือน)
        public void AddPoints(string id, string displayName, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount == 0) return;
            if (amount > 0) amount = Mathf.RoundToInt(amount * Perks.FriendMul);   // "มนุษย์สัมพันธ์"
            int before = GetPoints(id);
            int after = Mathf.Clamp(before + amount, 0, MaxPoints);
            points[id] = after;

            int lvlBefore = LevelOf(before), lvlAfter = LevelOf(after);
            if (lvlAfter > lvlBefore)
            {
                // เลื่อนระดับ → แจ้งเตือน + โบนัสความพอใจ (สนิทขึ้น = มีความสุข)
                HUDController.Toast($"🎉 คุณกับ {displayName} เป็น\"{NameOfLevel(lvlAfter)}\"แล้ว!");
                NisitSimulator.Core.SFXManager.Success();
                var st = Object.FindFirstObjectByType<PlayerStats>();
                if (st != null)
                {
                    st.ChangeSatisfaction(6f + lvlAfter * 2f);
                    if (lvlAfter >= 4) st.ChangeMoney(50);   // เพื่อนซี้ให้ของขวัญ
                }
            }
        }

        // ---------- เซฟ/โหลด ----------
        public void CollectSave(SaveData d)
        {
            d.relIds = new List<string>();
            d.relPoints = new List<int>();
            foreach (var kv in points) { d.relIds.Add(kv.Key); d.relPoints.Add(kv.Value); }
        }

        void LoadFromSave()
        {
            var d = SaveSystem.Load();
            if (d == null || d.relIds == null || d.relPoints == null) return;
            points.Clear();
            int n = Mathf.Min(d.relIds.Count, d.relPoints.Count);
            for (int i = 0; i < n; i++) points[d.relIds[i]] = d.relPoints[i];
        }
    }
}
