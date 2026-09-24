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
        public int OptionCount(int slot)
        {
            var s = GetSlot(slot);
            return (s != null && s.options != null) ? s.options.Length : 0;
        }
    }
}
