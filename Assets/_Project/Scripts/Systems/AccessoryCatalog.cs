using UnityEngine;

namespace NisitSimulator.Systems
{
    // แคตตาล็อกของแต่ง (หมวก/แว่น/กระเป๋า/ของถือ) ที่ติดกับ "กระดูก" ของตัวละคร humanoid
    //   เก็บใน Resources ให้โหลดตอนรันได้ · ผู้ใช้ลากโมเดล prop ใส่ options[] ของแต่ละช่องใน Inspector
    //   สร้างโครงเริ่มต้นโดย Editor tool: Nisit -> Build Character Creator
    [CreateAssetMenu(fileName = "AccessoryCatalog", menuName = "Nisit/Accessory Catalog")]
    public class AccessoryCatalog : ScriptableObject
    {
        [System.Serializable]
        public class Slot
        {
            public string slotName = "หมวก";                       // ชื่อช่อง (โชว์ในหน้าแต่งตัว)
            public HumanBodyBones bone = HumanBodyBones.Head;      // กระดูกที่จะติด
            public Vector3 posOffset;                              // เยื้องตำแหน่ง (local ของกระดูก)
            public Vector3 eulerOffset;                            // หมุน
            public float scale = 1f;                               // ขนาด prop
            public GameObject[] options;                           // โมเดล prop (ผู้ใช้ใส่เอง)
            public string[] labels;                                // ชื่อโชว์ของแต่ละ option (ไม่ใส่ = "แบบ n")
            public Sprite[] icons;                                 // รูปตัวอย่างของแต่ละ option (ไม่ใส่ = โชว์ชื่อ)
            public int[] genders;                                  // เพศของแต่ละ option: 0 = ชาย, 1 = หญิง, 2 = ใส่ได้ทั้งคู่ (ไม่ใส่ = ทั้งคู่)
            public bool hairLike;                                  // ผม/หนวด — ซ่อนเมื่อชุดมีผมติดมาแล้ว (POLYGON City)
        }

        public Slot[] slots;

        static AccessoryCatalog _cache;
        public static AccessoryCatalog Load()
        {
            if (_cache == null) _cache = Resources.Load<AccessoryCatalog>("AccessoryCatalog");
            return _cache;
        }

        public int SlotCount => slots != null ? slots.Length : 0;
        public Slot GetSlot(int i) => (slots != null && i >= 0 && i < slots.Length) ? slots[i] : null;

        public string OptionLabel(int slot, int opt)
        {
            var s = GetSlot(slot);
            if (s != null && s.labels != null && opt >= 0 && opt < s.labels.Length && !string.IsNullOrEmpty(s.labels[opt]))
                return s.labels[opt];
            return "แบบ " + (opt + 1);
        }
        public Sprite OptionIcon(int slot, int opt)
        {
            var s = GetSlot(slot);
            return (s != null && s.icons != null && opt >= 0 && opt < s.icons.Length) ? s.icons[opt] : null;
        }
        public int OptionGender(int slot, int opt)
        {
            var s = GetSlot(slot);
            return (s != null && s.genders != null && opt >= 0 && opt < s.genders.Length) ? s.genders[opt] : 2;
        }
        public bool Fits(int slot, int opt, int gender) { int g = OptionGender(slot, opt); return g == 2 || g == gender; }
        public int OptionCount(int slot)
        {
            var s = GetSlot(slot);
            return (s != null && s.options != null) ? s.options.Length : 0;
        }
    }
}
