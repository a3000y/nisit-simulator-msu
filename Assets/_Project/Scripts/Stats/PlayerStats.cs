using System;
using UnityEngine;

namespace NisitSimulator.Stats
{
    // ค่าสถานะตัวละครทั้งหมด (ตามเอกสาร บท 1.3.1.3 + บท 3)
    // ยิง event ทุกครั้งที่ค่าเปลี่ยน เพื่อให้ UI/ระบบอื่นอัปเดตตามโดยไม่ผูกกันแน่น
    public class PlayerStats : MonoBehaviour
    {
        [Header("ค่าสูงสุดของแต่ละสถานะ")]
        public float maxEnergy = 100f;
        public float maxHealth = 100f;
        public float maxHunger = 100f;

        [Header("ค่าเริ่มต้น")]
        [SerializeField] private float energy = 100f;      // พลังงาน — ลดเมื่อเดิน/ทำกิจกรรม
        [SerializeField] private float health = 100f;      // สุขภาพ
        [SerializeField] private float hunger = 100f;      // ความอิ่ม (100=อิ่ม, 0=หิวมาก)
        [SerializeField] private float knowledge = 0f;     // ความรู้ — เพิ่มเมื่อเรียน/อ่านหนังสือ
        [SerializeField] private float satisfaction = 50f; // ความพึงพอใจ
        [SerializeField] private int money = 0;            // เงิน
        [SerializeField] private int exp = 0;              // ค่าประสบการณ์

        // event: ส่งค่าปัจจุบันกับค่าสูงสุดไปให้แถบสถานะ
        public event Action<float, float> OnEnergyChanged;
        public event Action<float, float> OnHealthChanged;
        public event Action<float, float> OnHungerChanged;
        public event Action<float> OnKnowledgeChanged;
        public event Action<float> OnSatisfactionChanged;
        public event Action<int> OnMoneyChanged;
        public event Action<int> OnExpChanged;

        // event: ยิงเมื่อสถานะถึงจุดวิกฤต (พลังงาน/สุขภาพหมด) → ใช้ trigger Game Over
        public event Action OnCriticalState;

        void Start()
        {
            // แจ้งค่าเริ่มต้นให้ UI ครั้งแรก
            BroadcastAll();
        }

        // ---------- ตัวปรับค่า (บวก=เพิ่ม, ลบ=ลด) ----------

        public void ChangeEnergy(float amount)
        {
            energy = Mathf.Clamp(energy + amount, 0f, maxEnergy);
            OnEnergyChanged?.Invoke(energy, maxEnergy);
            CheckCritical();
        }

        public void ChangeHealth(float amount)
        {
            health = Mathf.Clamp(health + amount, 0f, maxHealth);
            OnHealthChanged?.Invoke(health, maxHealth);
            CheckCritical();
        }

        public void ChangeHunger(float amount)
        {
            hunger = Mathf.Clamp(hunger + amount, 0f, maxHunger);
            OnHungerChanged?.Invoke(hunger, maxHunger);
        }

        public void ChangeKnowledge(float amount)
        {
            knowledge = Mathf.Max(0f, knowledge + amount);
            OnKnowledgeChanged?.Invoke(knowledge);
        }

        public void ChangeSatisfaction(float amount)
        {
            satisfaction = Mathf.Clamp(satisfaction + amount, 0f, 100f);
            OnSatisfactionChanged?.Invoke(satisfaction);
        }

        public void ChangeMoney(int amount)
        {
            money += amount;
            OnMoneyChanged?.Invoke(money);
        }

        public bool TrySpendMoney(int cost)
        {
            if (money < cost) return false;   // เงินไม่พอ
            ChangeMoney(-cost);
            return true;
        }

        public void AddExp(int amount)
        {
            exp += amount;
            OnExpChanged?.Invoke(exp);
        }

        // ---------- อ่านค่า (property) ----------
        public float Energy => energy;
        public float Health => health;
        public float Hunger => hunger;
        public float Knowledge => knowledge;
        public float Satisfaction => satisfaction;
        public int Money => money;
        public int Exp => exp;

        // ---------- โหลดค่าจากเซฟ (M4) ----------
        public void LoadState(float e, float h, float hun, float know, float sat, int mon, int xp)
        {
            energy = e; health = h; hunger = hun; knowledge = know;
            satisfaction = sat; money = mon; exp = xp;
            BroadcastAll();
        }

        // เช็คว่าถึงจุดวิกฤตไหม (พลังงานหรือสุขภาพหมด)
        private void CheckCritical()
        {
            if (energy <= 0f || health <= 0f)
                OnCriticalState?.Invoke();
        }

        private void BroadcastAll()
        {
            OnEnergyChanged?.Invoke(energy, maxEnergy);
            OnHealthChanged?.Invoke(health, maxHealth);
            OnHungerChanged?.Invoke(hunger, maxHunger);
            OnKnowledgeChanged?.Invoke(knowledge);
            OnSatisfactionChanged?.Invoke(satisfaction);
            OnMoneyChanged?.Invoke(money);
            OnExpChanged?.Invoke(exp);
        }

        // TODO (M3): ให้ค่าพลังงาน/ความอิ่มค่อยๆ ลดตามเวลา — เชื่อมกับ GameClock
    }
}
