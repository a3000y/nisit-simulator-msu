using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Stats;
using NisitSimulator.TimeSystem;
using NisitSimulator.Player;
using NisitSimulator.UI;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;

namespace NisitSimulator.Interaction
{
    // ห้องเรียน — เข้าเรียนได้เฉพาะช่วงเวลาคาบเรียน และคาบละครั้งต่อวัน
    // ใส่ไว้ที่วัตถุห้องเรียน (Layer = Interactable)
    public class ClassStation : MonoBehaviour, IInteractable
    {
        [Header("ตารางเรียน (ชั่วโมงที่คาบเริ่ม)")]
        public int[] classHours = { 9, 13 };   // เช้า 9 โมง, บ่าย 13 น.
        // ขยายจาก 3 เป็น 4 ชั่วโมง เพราะนาฬิกาเดินเร็วขึ้น 3 เท่า คาบ 3 ชม. จะเหลือเวลาจริงแค่นาทีเดียว
        // เดินข้ามแมพไม่ทัน กลายเป็นเกมที่พลาดคาบเรียนเพราะเดินไปไม่ทัน ไม่ใช่เพราะเล่นผิด
        public int windowHours = 4;             // เข้าได้ภายในกี่ชั่วโมงหลังคาบเริ่ม (9-13, 13-17)

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
            var calendar = Object.FindAnyObjectByType<ProgressionManager>();
            if (!OnboardingFlow.IsPractice && calendar != null && (AcademicCalendar.IsWeekend(calendar.DayInYear) || AcademicCalendar.IsExamDay(calendar.DayInYear)))
            { HUDController.Toast("วันนี้ไม่มีคาบเรียน ดูตารางใน MSG REG ได้เลย"); return; }
            // หลักสูตรลงทะเบียน: เข้าเรียนได้เฉพาะช่วงคาบของวิชาที่ลงทะเบียนไว้
            var reg = NisitSimulator.Academics.CourseRegistrar.Instance;
            // เล่นคนเดียว: ตู้เข้าเรียนพาไปห้องของคาบปัจจุบัน + นั่งให้ (เร่งเวลาจนเลิกคาบ) แทนการเรียนทันที 2.5 วินาที
            if (reg != null && reg.IsActive && NisitSimulator.Academics.ClassroomNavigator.TryRedirectStation(interactor)) return;
            if (reg != null && reg.IsActive && !reg.TryGetOngoingSession(out _, out _))
            {
                HUDController.Toast("ตอนนี้ไม่มีคาบของวิชาที่คุณลงทะเบียน (ดูตารางเรียนในโทรศัพท์)");
                return;
            }

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

            var action = interactor.GetComponent<PlayerActionController>();
            if (action == null) action = interactor.AddComponent<PlayerActionController>();
            if (action.IsBusy) { HUDController.Toast("กำลังทำกิจกรรมอยู่"); return; }
            // ตรวจพลังงานก่อนนับเข้าเรียน (ไม่เสียสิทธิ์คาบนี้ถ้าถูกปฏิเสธ) · หักพลังงานตอนเรียนจบ → ได้ผลครั้งเดียวแล้วค่อยหมดแรงถ้าเหลือ 0
            if (!PlayerExhaustion.CanStartEnergyActivity(interactor, energyCost, "เข้าเรียน", out var why)) { HUDController.Toast(why); return; }

            attendedToday.Add(session);
            HUDController.Toast("เข้าเรียน... ตั้งใจฟังเลกเชอร์");
            action.PerformState(2.5f, () => GrantClass(interactor), "Typing");   // นั่งเรียน (พิมพ์/จด)
        }

        private void GrantClass(GameObject interactor)
        {
            if (interactor.TryGetComponent<PlayerStats>(out var stats))
            {
                float kMult = interactor.TryGetComponent<PlayerEffects>(out var fx) ? fx.knowledgeMult : 1f;
                // โบนัสจากเพื่อนสนิท (เพื่อนติวให้) — +5%/คน สูงสุด +25%
                int closeFriends = RelationshipManager.Instance.CountAtLeast(3);
                float friendMult = 1f + Mathf.Min(closeFriends, 5) * 0.05f;
                float want = knowledgePerClass * kMult * friendMult;

                // ChangeKnowledge หักความเครียดให้เอง แล้วคืนค่าที่ได้จริงมา
                float k = stats.ChangeKnowledge(want);
                // หลักสูตรลงทะเบียน: นับเข้าเรียนจากตารางเดียวกันกับแอป (วิชา/เวลา/ตึก)
                string course = NisitSimulator.Academics.CourseRegistrar.NotifyStudyTick(transform, k, want);
                stats.AddExp(expPerClass);
                stats.ChangeEnergy(-energyCost);
                stats.ChangeSatisfaction(3f);
                StatsTracker.Instance.Add("classes", 1);
                GameplayEvents.Raise(GameplayEvents.Class);

                HUDController.Toast($"เรียนจบคาบ +{k:0} EXP"
                    + (kMult > 1f ? " (ไฟแรง!)" : "")
                    + (closeFriends > 0 ? $" (เพื่อนติว +{Mathf.Min(closeFriends,5)*5}%)" : "")
                    + (k < want - 0.05f ? " (เครียดมาก เรียนได้น้อยลง)" : "")
                    + course);
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
