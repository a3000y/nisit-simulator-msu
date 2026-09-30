using System;
using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Stats;

namespace NisitSimulator.Systems
{
    // ระบบกระเป๋าไอเทม (เวอร์ชันย่อ) — ใช้ ShopItem เป็นข้อมูลไอเทม (ชื่อ/ไอคอน/ผลสถานะ)
    //   ซื้อจากร้าน → เข้ากระเป๋า, เปิดด้วยปุ่ม I แล้วกดใช้ → เพิ่มค่าสถานะ
    //   บันทึกลง SaveData.inventoryItemIds (เก็บเป็นชื่อไอเทม)
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Serializable]
        public class Stack { public ShopItem item; public int count; }

        [Tooltip("จำนวนช่องสูงสุด")]
        public int maxSlots = 16;

        private readonly List<Stack> stacks = new List<Stack>();
        public IReadOnlyList<Stack> Stacks => stacks;

        public event Action OnChanged;   // ให้ UI ฟังเพื่อรีเฟรชช่อง

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------- เพิ่ม/ใช้ ----------
        public bool Add(ShopItem item, int n = 1)
        {
            if (item == null || n <= 0) return false;
            var s = stacks.Find(x => x.item != null && x.item.name == item.name);
            if (s != null) s.count += n;
            else
            {
                if (stacks.Count >= maxSlots) { NisitSimulator.UI.HUDController.Toast("กระเป๋าเต็ม!"); return false; }
                stacks.Add(new Stack { item = item, count = n });
            }
            OnChanged?.Invoke();
            return true;
        }

        public void Use(int index)
        {
            if (index < 0 || index >= stacks.Count) return;
            var s = stacks[index];
            var stats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            if (stats != null)
            {
                stats.ChangeEnergy(s.item.energy);
                stats.ChangeHunger(s.item.hunger);
                stats.ChangeHealth(s.item.health);
                stats.ChangeKnowledge(s.item.knowledge);
                stats.ChangeSatisfaction(s.item.satisfaction);
            }
            NisitSimulator.UI.HUDController.Toast($"ใช้ {s.item.name}");
            GameplayEvents.Raise(GameplayEvents.UseItem);
            if (s.item.hunger > 0) GameplayEvents.Raise(GameplayEvents.Eat);
            NisitSimulator.Core.SFXManager.Eat();

            s.count--;
            if (s.count <= 0) stacks.RemoveAt(index);
            OnChanged?.Invoke();
        }

        public int TotalCount()
        {
            int n = 0;
            foreach (var s in stacks) n += s.count;
            return n;
        }

        // ---------- เทรด/ให้ของ (ค้นหา/เพิ่ม/ลบ ตามชื่อไอเทม) ----------
        public static ShopItem Resolve(string name)
            => BuildLookup().TryGetValue(name, out var it) ? it : null;

        public bool AddByName(string name)
        {
            var it = Resolve(name);
            return it != null && Add(it, 1);
        }

        public bool RemoveByName(string name)
        {
            var s = stacks.Find(x => x.item != null && x.item.name == name);
            if (s == null) return false;
            s.count--;
            if (s.count <= 0) stacks.Remove(s);
            OnChanged?.Invoke();
            return true;
        }

        // ---------- บันทึก/โหลด (เก็บเป็นชื่อไอเทม) ----------
        public List<string> ToSaveList()
        {
            var list = new List<string>();
            foreach (var s in stacks)
                for (int i = 0; i < s.count; i++) list.Add(s.item.name);
            return list;
        }

        public void LoadFromList(List<string> ids)
        {
            stacks.Clear();
            if (ids != null && ids.Count > 0)
            {
                var lookup = BuildLookup();
                foreach (var id in ids)
                    if (lookup.TryGetValue(id, out var it)) Add(it, 1);
            }
            OnChanged?.Invoke();
        }

        // ตารางค้นหาไอเทมจากชื่อ (รวมของร้านค้า + โรงอาหาร)
        static Dictionary<string, ShopItem> BuildLookup()
        {
            var d = new Dictionary<string, ShopItem>();
            foreach (var it in ShopController.DefaultCatalog()) d[it.name] = it;
            foreach (var it in ShopController.CafeteriaCatalog()) d[it.name] = it;
            return d;
        }
    }
}
