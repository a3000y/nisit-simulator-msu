using System.Collections.Generic;
using UnityEngine;

namespace NisitSimulator.Systems
{
    // ของแต่งตัว/ชุดที่ต้องปลดล็อกด้วยความสำเร็จ (ชื่อ prefab → id ความสำเร็จ)
    //   หน้าสร้างตัวละครจะล็อกปุ่มไว้ + บอกว่าต้องทำความสำเร็จอะไร
    public static class CosmeticUnlocks
    {
        // key = ชื่อ GameObject ของ option (prefab/ชิ้นในโครง) · value = (id ความสำเร็จ, ชื่อเรียก)
        static readonly Dictionary<string, string[]> map = new Dictionary<string, string[]>
        {
            { "SM_Gen_Chr_Attach_Hat_02",         new[] { "quest10",  "หมวก 2" } },
            { "SM_Gen_Chr_Attach_Headset_02",     new[] { "class30",  "หูฟัง 3" } },
            { "SM_Gen_Chr_Attach_Sunglasses_01",  new[] { "friend5",  "แว่นกันแดด" } },
            { "SM_Gen_Chr_Attach_Hood_01",        new[] { "gift5",    "ฮู้ด" } },
            { "SM_Gen_Chr_Attach_Beard_02",       new[] { "survive20","เครา 2" } },
            { "SM_Gen_Chr_Attach_Hair_01_alt",    new[] { "level5",   "ทรงยาว 2" } },
            { "Nisit_Hair_Hair_06_Bun_01",        new[] { "streak3",  "ทรงมวย" } },
            { "Nisit_Business_Male_01",           new[] { "level10",  "ชุดทางการ (ชาย)" } },
            { "Nisit_Business_Female_01",         new[] { "level10",  "ชุดทางการ (หญิง)" } },
            { "Nisit_City_BusinessMan_Suit",      new[] { "gpa35",    "ชุดสูท" } },
            { "Nisit_City_BusinessWoman",         new[] { "gpa35",    "ชุดทำงาน (หญิง)" } },
        };

        public static string RequiredAch(GameObject option)
            => option != null && map.TryGetValue(option.name, out var v) ? v[0] : null;

        public static bool IsUnlocked(GameObject option) => AchievementManager.ProfileHas(RequiredAch(option));

        public static string LockHint(GameObject option)
        {
            var id = RequiredAch(option);
            return id != null ? AchievementManager.TitleOf(id) : "";
        }

        // ชื่อของที่ความสำเร็จนี้ปลดล็อก (คั่นด้วย , ) — ใช้ในแจ้งเตือน/สมุดความสำเร็จ
        public static string UnlocksText(string achId)
        {
            var names = new List<string>();
            foreach (var kv in map) if (kv.Value[0] == achId) names.Add(kv.Value[1]);
            return string.Join(", ", names.ToArray());
        }
    }
}
