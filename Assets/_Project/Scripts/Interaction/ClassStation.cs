using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.Player;
using NisitSimulator.UI;
using NisitSimulator.SaveLoad;

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

            // เล่นต่อ → คืน "คาบที่เข้าเรียนไปแล้ววันนี้" ของห้องนี้ (กันเข้าเรียนซ้ำ)
            if (GameSession.IsContinue)
            {
                var d = SaveSystem.Load();
                if (d != null && d.classAttendance != null)
                {
                    string key = StationKey();
                    foreach (var e in d.classAttendance)
                    {
                        int c = e.LastIndexOf(':');
                        if (c > 0 && e.Substring(0, c) == key && int.TryParse(e.Substring(c + 1), out int s))
                            attendedToday.Add(s);
                    }
                }
            }
        }

        // คีย์ประจำห้อง (ชื่อ + ตำแหน่งปัด) — คงที่ข้ามการโหลด ตราบใดที่ห้องไม่ถูกย้าย
        private string StationKey()
            => $"{name}_{Mathf.RoundToInt(transform.position.x)}_{Mathf.RoundToInt(transform.position.z)}";

        // ให้ SaveManager เรียกเก็บคาบที่เข้าเรียนแล้วของห้องนี้
        public void CollectAttendance(List<string> outList)
        {
            string key = StationKey();
            foreach (int s in attendedToday) outList.Add(key + ":" + s);
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

            var action = interactor.GetComponent<PlayerActionController>()
                         ?? interactor.AddComponent<PlayerActionController>();
            if (action.IsBusy) { HUDController.Toast("กำลังทำกิจกรรมอยู่"); return; }

            attendedToday.Add(session);
            HUDController.Toast("เข้าเรียน... ตั้งใจฟังเลกเชอร์");
            action.PerformState(2.5f, () => GrantClass(interactor), "Typing");   // นั่งเรียน (พิมพ์/จด)
        }

        private void GrantClass(GameObject interactor)
        {
            if (interactor.TryGetComponent<PlayerStats>(out var stats))
            {
                float kMult = interactor.TryGetComponent<PlayerEffects>(out var fx) ? fx.knowledgeMult : 1f;
                float k = knowledgePerClass * kMult;
                stats.ChangeKnowledge(k);
                stats.AddExp(expPerClass);
                stats.ChangeEnergy(-energyCost);
                stats.ChangeSatisfaction(3f);
                HUDController.Toast($"เรียนจบคาบ +{k:0} ความรู้" + (kMult > 1f ? " (ไฟแรง!)" : ""));
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
