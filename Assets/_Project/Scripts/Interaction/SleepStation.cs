using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;

namespace NisitSimulator.Interaction
{
    // จุดนอนบนเตียง — กด E → หน้ายืนยัน "นอนพักจนถึง 07:00" → เฟดดำ → ข้ามเวลาไป 07:00 ครั้งถัดไป + ฟื้นสถานะ
    //   นอนได้เฉพาะช่วงเย็น/ค่ำ (กติกาเดิม: ตั้งแต่ sleepFromHour ถึงก่อน wakeHour) · ห้ามนอนระหว่างสอบ/หน้าต่างอื่นหยุดเวลา
    //   ขั้นตอนการนอนทั้งหมดอยู่ที่ SleepController (กันกดซ้ำ/ข้ามหลายวัน/ฟื้นสถานะซ้ำ)
    public class SleepStation : MonoBehaviour, IInteractable
    {
        [Tooltip("ใช้จุดตื่นของเตียงนี้โดยตรงแทนชุดจุดตื่นรวมเดิม")]
        public bool useBedWakePoint;
        [Tooltip("ช่องเจ้าของเตียง (-1 = เตียงทั่วไป); ไม่เปลี่ยนกติกาการนอนเดิม")]
        public int assignedDormSlot = -1;
        public bool CanUseAssignedBed => assignedDormSlot < 0 || assignedDormSlot == NisitSimulator.SaveLoad.PlayerSpawnSystem.AssignedSlotIndex;

        public int sleepFromHour = 18;   // นอนได้ตั้งแต่กี่โมง (เย็น)
        public int wakeHour = 7;         // ตื่นกี่โมง

        [Tooltip("จุดยืนข้างเตียงตอนตื่น (ว่าง = DormSpawnPoint)")]
        public Transform wakePoint;

        [Tooltip("จุดตื่นข้างเตียงสำหรับ Multiplayer ตามช่อง 0–3")]
        public Transform[] extraWakePoints = new Transform[0];

        public Transform GetWakePoint(int slot)
        {
            if (extraWakePoints == null || extraWakePoints.Length == 0) return WakePoint;
            var point = extraWakePoints[Mathf.Max(0, slot) % extraWakePoints.Length];
            return point != null ? point : WakePoint;
        }

        [Header("ฟื้นฟูเมื่อตื่น (ค่าเดิมของเกม)")]
        public float energyRestore = 999f;       // เต็ม
        public float healthRestore = 12f;
        public float hungerChange = -25f;        // ตื่นมาหิว → ต้องไปกินข้าว
        public float satisfactionChange = 5f;
        public float stressChange = -25f;        // นอนคือทางคลายเครียดหลักของเกม (เดิม -35 → -25 ให้ความเครียดสะสมข้ามวันได้)

        private GameClock _clock;
        private GameClock Clock => _clock != null ? _clock : (_clock = Object.FindFirstObjectByType<GameClock>());

        // นอนได้เฉพาะ เย็น(>=18) ถึง เช้ามืด(<7)
        public bool IsSleepHour()
        {
            int h = Clock != null ? Clock.Hour : sleepFromHour;
            return h >= sleepFromHour || h < wakeHour;
        }

        public string GetPrompt()
        {
            if (!CanUseAssignedBed) return "เตียงของเพื่อนร่วมห้อง";
            if (SleepController.IsSleeping) return "";
            return IsSleepHour() ? $"กด E เพื่อนอน (ตื่น {wakeHour:00}:00 น.)" : $"ยังไม่ถึงเวลานอน (หลัง {sleepFromHour}:00 น.)";
        }

        public void Interact(GameObject interactor)
        {
            if (!CanUseAssignedBed) { HUDController.Toast("ใช้เตียงของตัวเองเพื่อพักผ่อน"); return; }
            if (!IsSleepHour())
            {
                HUDController.Toast($"ยังไม่ง่วง! นอนได้หลัง {sleepFromHour}:00 น.");
                return;
            }
            SleepController.EnsureExists().RequestSleep(this, interactor, false);
        }

        public Transform WakePoint
        {
            get
            {
                if (wakePoint != null) return wakePoint;
                var d = DormSpawnPoint.Main;
                return d != null ? d.transform : transform;
            }
        }

        // ผลการนอนครั้งล่าสุด (ใช้เลือกข้อความตอนตื่น)
        public static bool LastSleepRestless { get; private set; }

        public static bool IsRestless(float stress) => stress > StressBands.InsomniaAbove;

        // พลังงานที่เพิ่มจริงตอนตื่น: ปกติ = restore (999 = เต็ม) · นอนไม่หลับ = ฟื้นได้ถึง 75% ของค่าสูงสุดเท่านั้น (มีมากกว่านั้นอยู่แล้ว = ไม่ลด)
        public static float EnergyRestoreFor(float current, float max, float restore, bool restless)
        {
            if (!restless) return restore;
            float cap = max * StressBands.InsomniaEnergyFraction;
            return Mathf.Clamp(cap - current, 0f, Mathf.Max(0f, restore));
        }

        // ฟื้นสถานะตามกติกาการนอน — เรียกโดย SleepController ครั้งเดียวต่อการนอนหนึ่งครั้ง
        public void ApplyWakeEffects(GameObject interactor)
        {
            LastSleepRestless = false;
            if (interactor != null && interactor.TryGetComponent<PlayerStats>(out var s))
            {
                // นอนไม่หลับ: เข้านอนตอนเครียดเกิน 70 → พลังงานฟื้นได้แค่ 75% · ความเครียดลดน้อยลง
                bool restless = IsRestless(s.Stress);
                LastSleepRestless = restless;
                s.ChangeEnergy(EnergyRestoreFor(s.Energy, s.maxEnergy, energyRestore, restless));
                s.ChangeHealth(healthRestore);
                s.ChangeHunger(hungerChange);
                s.ChangeSatisfaction(satisfactionChange);
                s.ChangeStress(restless ? StressBands.InsomniaStressChange : stressChange);
            }
            NisitSimulator.Systems.GameplayEvents.Raise(NisitSimulator.Systems.GameplayEvents.Sleep);
        }
    }
}
