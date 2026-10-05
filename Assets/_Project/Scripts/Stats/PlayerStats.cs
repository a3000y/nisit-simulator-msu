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
        public float maxStress = 100f;

        [Header("ผลของความเครียดต่อการเรียนรู้")]
        [Tooltip("เครียดต่ำกว่านี้ไม่มีผลเสีย — เครียดนิดหน่อยเป็นเรื่องปกติ")]
        public float stressNoPenaltyBelow = 30f;
        [Range(0.1f, 1f)]
        [Tooltip("ตัวคูณความรู้เมื่อเครียดเต็ม 100 — 0.5 คือเรียนได้ครึ่งเดียว")]
        public float minKnowledgeMultAtMaxStress = 0.5f;

        [Header("ค่าเริ่มต้น")]
        [SerializeField] private float energy = 100f;      // พลังงาน — ลดเมื่อเดิน/ทำกิจกรรม
        [SerializeField] private float health = 100f;      // สุขภาพ
        [SerializeField] private float hunger = 100f;      // ความอิ่ม (100=อิ่ม, 0=หิวมาก)
        [SerializeField] private float knowledge = 0f;     // ความรู้ — เพิ่มเมื่อเรียน/อ่านหนังสือ
        [SerializeField] private float satisfaction = 50f; // ความพึงพอใจ
        [SerializeField] private float stress = 0f;        // ความเครียด — 0 สบายดี, 100 เครียดจัด
        [SerializeField] private int money = 0;            // เงิน
        [SerializeField] private int exp = 0;              // ค่าประสบการณ์

        // event: ส่งค่าปัจจุบันกับค่าสูงสุดไปให้แถบสถานะ
        public event Action<float, float> OnEnergyChanged;
        public event Action<float, float> OnHealthChanged;
        public event Action<float, float> OnHungerChanged;
        public event Action<float> OnKnowledgeChanged;
        public event Action<float> OnSatisfactionChanged;
        public event Action<float, float> OnStressChanged;
        public event Action<int> OnMoneyChanged;
        public event Action<int> OnExpChanged;

        // event: ยิงเมื่อสุขภาพหมด → ใช้ trigger Game Over
        //   พลังงานหมด "ไม่" ยิง event นี้แล้ว → เข้าสถานะหมดแรงแทน (PlayerExhaustion ฟัง OnEnergyChanged)
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

        // คืนค่าความรู้ที่ได้จริงหลังหักความเครียดแล้ว
        // หักที่นี่ที่เดียว ผู้เรียกจึงไม่มีทางลืมใส่ และ UI เอาค่าที่คืนไปแสดงได้ตรงความจริง
public float ChangeKnowledge(float amount)
        {
            if (amount > 0f)
                amount *= KnowledgeMultiplier * NisitSimulator.Systems.Perks.KnowledgeMul;

            int expDelta = Mathf.RoundToInt(amount);
            AddExp(expDelta);
            return expDelta;
        }

        public void ChangeSatisfaction(float amount)
        {
            satisfaction = Mathf.Clamp(satisfaction + amount, 0f, 100f);
            OnSatisfactionChanged?.Invoke(satisfaction);
        }

        // ความเครียด — บวกคือเครียดขึ้น (เรียนหนัก หิว อดนอน) ลบคือผ่อนคลาย (นอน พัก เข้าสังคม)
        public void ChangeStress(float amount)
        {
            if (amount > 0f) amount *= NisitSimulator.Systems.Perks.StressGainMul;   // ความสามารถ "ใจเย็น"
            stress = Mathf.Clamp(stress + amount, 0f, maxStress);
            OnStressChanged?.Invoke(stress, maxStress);
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
            if (amount == 0) return;
            exp = Mathf.Max(0, exp + amount);
            knowledge = exp; // เก็บ field เดิมไว้เพื่อความเข้ากันได้กับ scene/save เก่า
            OnExpChanged?.Invoke(exp);
            OnKnowledgeChanged?.Invoke(exp);
        }

        // ---------- อ่านค่า (property) ----------
        public float Energy => energy;
        public float Health => health;
        public float Hunger => hunger;
        public float Knowledge => exp; // compatibility alias: ความรู้เดิมคือ EXP ค่าเดียวกัน
        public float Satisfaction => satisfaction;
        public float Stress => stress;

        // ตัวคูณความรู้ตามช่วงความเครียด 4 ช่วง (StressBands): สบาย 1.0 · ตึงตัว 1.05 · เครียด 0.9 · เครียดจัด 0.8 → minKnowledgeMultAtMaxStress
        //   stressNoPenaltyBelow เก็บไว้ให้ฉาก/เซฟเดิมไม่พัง (ไม่ได้ใช้แล้ว — เส้นแบ่งช่วงอยู่ใน StressBands)
        public float KnowledgeMultiplier => StressBands.KnowledgeMult(stress, maxStress, minKnowledgeMultAtMaxStress);
        public StressBand StressBand => StressBands.BandOf(stress);
        public int Money => money;
        public int Exp => exp;

        // ---------- โหลดค่าจากเซฟ (M4) ----------
        // พารามิเตอร์ str เป็น optional เซฟเก่าที่ไม่มีความเครียดจึงโหลดได้ตามปกติ
public void LoadState(float e, float h, float hun, float know, float sat, int mon, int xp, float str = 0f)
        {
            energy = Mathf.Clamp(e, 0f, maxEnergy);      // จำกัด 0..สูงสุด (เซฟพลังงาน 0 → หมดแรง ไม่ใช่ Game Over)
            health = Mathf.Clamp(h, 0f, maxHealth);
            hunger = Mathf.Clamp(hun, 0f, maxHunger);
            satisfaction = sat;
            money = mon;
            stress = Mathf.Clamp(str, 0f, maxStress);

            // เซฟเก่าเคยแยกความรู้กับ EXP: ใช้ค่าที่สูงกว่าเพื่อไม่ให้ความคืบหน้าหาย
            exp = Mathf.Max(xp, Mathf.RoundToInt(know));
            knowledge = exp;
            BroadcastAll();
        }

        // เช็คว่าถึงจุดวิกฤตไหม — เฉพาะสุขภาพหมด (พลังงาน 0 = หมดแรง พักแล้วเล่นต่อได้ ไม่จบเกม)
        private void CheckCritical()
        {
            if (health <= 0f)
                OnCriticalState?.Invoke();
        }

        private void BroadcastAll()
        {
            OnEnergyChanged?.Invoke(energy, maxEnergy);
            OnHealthChanged?.Invoke(health, maxHealth);
            OnHungerChanged?.Invoke(hunger, maxHunger);
            OnKnowledgeChanged?.Invoke(exp);
            OnSatisfactionChanged?.Invoke(satisfaction);
            OnStressChanged?.Invoke(stress, maxStress);
            OnMoneyChanged?.Invoke(money);
            OnExpChanged?.Invoke(exp);
        }

        // TODO (M3): ให้ค่าพลังงาน/ความอิ่มค่อยๆ ลดตามเวลา — เชื่อมกับ GameClock
    }
}
