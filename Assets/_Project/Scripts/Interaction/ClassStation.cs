using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;

namespace NisitSimulator.Interaction
{
    // ห้องเรียน — เข้าเรียนได้เฉพาะช่วงเวลาคาบเรียน และคาบละครั้งต่อวัน
    // ใส่ไว้ที่วัตถุห้องเรียน (Layer = Interactable)
    public class ClassStation : MonoBehaviour, IInteractable
    {
        [Header("ตารางเรียน (ชั่วโมงที่คาบเริ่ม)")]
        public int[] classHours = { 9, 13 };   // เช้า 9 โมง, บ่าย 13 น.
        public int windowHours = 3;             // เข้าได้ภายในกี่ชั่วโมงหลังคาบเริ่ม (9-12, 13-16)

        [Header("ผลตอบแทนต่อคาบ")]
        public float knowledgePerClass = 35f;
        public int expPerClass = 30;
        public float energyCost = 12f;

        private GameClock clock;
        private readonly HashSet<int> attendedToday = new HashSet<int>();

        void Start()
        {
            clock = Object.FindFirstObjectByType<GameClock>();
            if (clock != null) clock.OnDayChanged += _ => attendedToday.Clear(); // วันใหม่ เข้าเรียนได้อีก
        }

        public string GetPrompt() => "กด E เพื่อเข้าเรียน";

        public void Interact(GameObject interactor)
        {
            int session = CurrentOpenSession();

            if (session < 0)
            {
                HUDController.Toast("ยังไม่ถึงเวลาเรียน หรือเลิกคาบแล้ว");
                return;
            }
            if (attendedToday.Contains(session))
            {
                HUDController.Toast("เข้าเรียนคาบนี้ไปแล้ววันนี้");
                return;
            }

            attendedToday.Add(session);
            if (interactor.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.ChangeKnowledge(knowledgePerClass);
                stats.AddExp(expPerClass);
                stats.ChangeEnergy(-energyCost);
                stats.ChangeSatisfaction(3f);
                HUDController.Toast($"เข้าเรียนสำเร็จ +{knowledgePerClass:0} ความรู้");
            }
        }

        // คืน index คาบที่เปิดอยู่ตอนนี้ (-1 = ไม่มี)
        private int CurrentOpenSession()
        {
            if (clock == null) return 0; // ไม่มีนาฬิกา = เข้าได้ตลอด (กันพัง)
            int h = clock.Hour;
            for (int i = 0; i < classHours.Length; i++)
                if (h >= classHours[i] && h < classHours[i] + windowHours) return i;
            return -1;
        }
    }
}
