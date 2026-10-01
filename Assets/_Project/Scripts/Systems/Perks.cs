using System.Collections.Generic;
using UnityEngine;

namespace NisitSimulator.Systems
{
    // ความสามารถที่เลือกตอนเลเวลอัป — แต่ละอย่างอัปได้ 3 ระดับ
    //   เก็บเป็น static ให้ระบบอื่นอ่านตัวคูณได้ทันที (PlayerStats/StatDecay/ร้าน/งาน/เดิน/เพื่อน/สอบ)
    //   LevelSystem เป็นคนรีเซ็ต (เกมใหม่) หรือโหลดจากเซฟ (เล่นต่อ)
    public static class Perks
    {
        public class Def
        {
            public string id, name, desc;
            public int maxRank = 3;
        }

        public static readonly Def[] All =
        {
            new Def { id = "brain",    name = "หัวไว",          desc = "ได้รับ EXP เพิ่ม +8%" },
            new Def { id = "stamina",  name = "อึด",            desc = "พลังงานลดช้าลง 10%" },
            new Def { id = "appetite", name = "กินน้อย",        desc = "หิวช้าลง 10%" },
            new Def { id = "calm",     name = "ใจเย็น",         desc = "เครียดขึ้นช้าลง 12%" },
            new Def { id = "bargain",  name = "นักต่อรอง",      desc = "ซื้อของถูกลง 8%" },
            new Def { id = "worker",   name = "ขยันทำงาน",      desc = "ค่าจ้างพาร์ทไทม์ +15%" },
            new Def { id = "runner",   name = "ขาไว",           desc = "เดิน/วิ่งเร็วขึ้น 7%" },
            new Def { id = "social",   name = "มนุษย์สัมพันธ์", desc = "ความสนิทกับเพื่อน +25%" },
            new Def { id = "exam",     name = "เซียนสอบ",       desc = "คะแนนสอบ +4%" },
        };

        static readonly Dictionary<string, int> ranks = new Dictionary<string, int>();

        public static int Rank(string id) => ranks.TryGetValue(id, out var r) ? r : 0;
        public static bool IsMaxed(Def d) => Rank(d.id) >= d.maxRank;
        public static Def Find(string id) { foreach (var d in All) if (d.id == id) return d; return null; }

        public static void Add(string id)
        {
            var d = Find(id);
            if (d == null || IsMaxed(d)) return;
            ranks[id] = Rank(id) + 1;
        }

        public static void Reset() => ranks.Clear();

        public static string Serialize()
        {
            var parts = new List<string>();
            foreach (var kv in ranks) if (kv.Value > 0) parts.Add(kv.Key + ":" + kv.Value);
            return string.Join(",", parts.ToArray());
        }

        public static void Load(string s)
        {
            ranks.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var p in s.Split(','))
            {
                var kv = p.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[1], out var r) && Find(kv[0]) != null)
                    ranks[kv[0]] = Mathf.Clamp(r, 0, Find(kv[0]).maxRank);
            }
        }

        // ---------- ตัวคูณที่ระบบอื่นใช้ ----------
        public static float KnowledgeMul   => 1f + 0.08f * Rank("brain");
        public static float EnergyDrainMul => 1f - 0.10f * Rank("stamina");
        public static float HungerDrainMul => 1f - 0.10f * Rank("appetite");
        public static float StressGainMul  => 1f - 0.12f * Rank("calm");
        public static float PriceMul       => 1f - 0.08f * Rank("bargain");
        public static float WageMul        => 1f + 0.15f * Rank("worker");
        public static float MoveMul        => 1f + 0.07f * Rank("runner");
        public static float FriendMul      => 1f + 0.25f * Rank("social");
        public static float ExamBonus      => 0.04f * Rank("exam");

        public static int Price(int basePrice) => Mathf.Max(1, Mathf.RoundToInt(basePrice * PriceMul));
    }
}
