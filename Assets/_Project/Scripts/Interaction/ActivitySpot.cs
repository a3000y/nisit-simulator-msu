using System.Collections.Generic;
using UnityEngine;
using NisitSimulator.Player;
using NisitSimulator.Stats;
using NisitSimulator.UI;
using NisitSimulator.Core;
using NisitSimulator.TimeSystem;

namespace NisitSimulator.Interaction
{
    // จุดทำกิจกรรมแบบ "นั่ง" — กด E นั่ง (ค่าสถานะเปลี่ยน) / กด E อีกที ลุก
    // ใส่ไว้ที่ marker (Layer = Interactable) + มี seat anchor บอกจุดนั่ง
    public class ActivitySpot : MonoBehaviour, IInteractable
    {
        [Header("จุดนั่ง (ปล่อยว่าง = ใช้ตำแหน่งตัวเอง)")]
        public Transform seat;
        [Tooltip("ยกตัวขึ้นตอนนั่ง (กันจมพื้น) — ปรับได้ตอน Play")]
        public float sitYOffset = 0.4f;
        public string activityName = "นั่งพัก";

        [Header("ผลต่อสถานะ (บวก=เพิ่ม)")]
        public float energyChange = 0f;
        public float knowledgeChange = 0f;
        public float hungerChange = 0f;
        public float satisfactionChange = 0f;
        [Tooltip("บวก = เครียดขึ้น (เรียน/ทำงาน) ลบ = ผ่อนคลาย (พัก/ชมรม)")]
        public float stressChange = 0f;
        public int expReward = 0;
        [Header("กิจกรรมที่ให้ความรู้ = สะสมตามเวลาที่นั่ง")]
        [Tooltip("ได้ผลหนึ่งครั้งทุก ๆ กี่นาทีในเกมที่นั่งอยู่")]
        public float rewardEveryGameMinutes = 60f;
        [Tooltip("บังคับให้ผลทันทีครั้งเดียว แม้เป็นกิจกรรมที่ให้ความรู้")]
        public bool forceInstant = false;
        [Tooltip("พลังงานเหลือต่ำกว่านี้จะลุกเอง กันนั่งเรียนเพลินจนหมดแรง")]
        public float minEnergyToContinue = 8f;

        private bool _seated;
        private float _accum;
        private GameObject _who;
        private GameClock _clock;

        public string GetPrompt() => _seated ? "กด E เพื่อลุกขึ้น" : $"กด E เพื่อ{activityName}";

        public void Interact(GameObject interactor)
        {
            var act = interactor.GetComponent<PlayerActivity>();
            if (act == null) return;

            if (_seated)
            {
                act.Stand();
                StopSitting();
            }
            else
            {
                if (act.IsBusy) return;                 // กำลังนั่งที่อื่นอยู่
                // กิจกรรมที่ใช้พลังงาน (เรียน/ออกกำลังกาย/ฯลฯ) — ตรวจก่อนนั่ง ก่อนหักค่า/ให้รางวัล · หมดแรง = ปฏิเสธพร้อมเหตุผล
                if (UsesEnergy)
                {
                    float need = IsOverTime ? minEnergyToContinue + Mathf.Max(0f, -energyChange) : Mathf.Max(0f, -energyChange);
                    if (!PlayerExhaustion.CanStartEnergyActivity(interactor, need, activityName, out var why)) { HUDController.Toast(why); return; }
                }
                var t = seat != null ? seat : transform;
                act.Sit(t.position + Vector3.up * sitYOffset, t.rotation);
                _seated = true;
                _who = interactor;
                _accum = 0f;

                if (IsOverTime)
                    HUDController.Toast($"เริ่ม{activityName}... นั่งค้างไว้เพื่อสะสม EXP");
                else
                    ApplyEffect(interactor);   // กินข้าว / นั่งพัก — ได้ผลทันทีเหมือนเดิม
            }
        }

        // ── ทำไมกิจกรรมที่ให้ความรู้จึงต้องรอ ──
        // เดิมกด E ครั้งเดียวได้ความรู้เต็มก้อน ลุกแล้วนั่งใหม่ได้อีก วนไม่จำกัด
        // โต๊ะเรียนมี 13 ตัว ไล่กดไม่กี่นาทีก็ครบเป้าความรู้ทั้ง 4 ปี
        // เกมจึงไม่เหลือการตัดสินใจอะไรเลย กลายเป็นแค่ฉากไว้เดินถ่ายรูป
        // พอผูกกับเวลา การเรียนจึงแย่งเวลากับกินข้าว นอน ทำงาน เข้าสังคม — กลายเป็นวงจรจริง
        private bool IsOverTime => !forceInstant && knowledgeChange > 0f && rewardEveryGameMinutes > 0f;
        // ใช้พลังงาน = หักพลังงาน หรือเป็นการเรียน (กิน/นั่งพัก ที่ไม่หักพลังงานยังทำได้ตอนหมดแรง)
        private bool UsesEnergy => energyChange < 0f || knowledgeChange > 0f;

        void Start()
        {
            _clock = Object.FindFirstObjectByType<GameClock>();
        }

        void Update()
        {
            if (!_seated || !IsOverTime || _who == null) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) return;

            // หมดแรงระหว่างนั่ง (เช่น เหตุการณ์หักพลังงาน) → ลุกอย่างปลอดภัย ไม่ให้รางวัลรอบที่ยังไม่ครบ
            var ex = _who.GetComponent<PlayerExhaustion>();
            if (ex != null && ex.IsExhausted)
            {
                var a = _who.GetComponent<PlayerActivity>();
                if (a != null) a.Stand();
                HUDController.Toast($"หมดแรง หยุด{activityName}แล้ว");
                StopSitting();
                return;
            }

            float speed = _clock != null ? _clock.gameMinutesPerRealSecond : 1f;
            _accum += Time.deltaTime * speed;

            while (_accum >= rewardEveryGameMinutes)
            {
                _accum -= rewardEveryGameMinutes;
                ApplyEffect(_who);

                // กันนั่งเรียนจนพลังงานหมดแล้วเกมจบทันทีโดยไม่มีสัญญาณเตือน
                if (_who != null && _who.TryGetComponent<PlayerStats>(out var s) && s.Energy <= minEnergyToContinue)
                {
                    var act = _who.GetComponent<PlayerActivity>();
                    if (act != null) act.Stand();
                    HUDController.Toast($"เหนื่อยเกินไป หยุด{activityName}แล้ว ไปพักก่อน");
                    StopSitting();
                    return;
                }
            }
        }

        private void StopSitting()
        {
            _seated = false;
            _who = null;
            _accum = 0f;
        }

        private void ApplyEffect(GameObject who)
        {
            if (who.TryGetComponent<PlayerStats>(out var s))
            {
                s.ChangeEnergy(energyChange);
                float gainedK = s.ChangeKnowledge(knowledgeChange);   // คืนค่าหลังหักความเครียดแล้ว
                s.ChangeHunger(hungerChange);
                s.ChangeSatisfaction(satisfactionChange);
                s.ChangeStress(stressChange);
                s.AddExp(expReward);
                // นั่งเรียนครบ 1 ชม. → แจ้งระบบลงทะเบียน (นับเข้าเรียนถ้าตรงคาบและตึกในตาราง / นอกคาบ = อ่านทบทวน)
                string course = knowledgeChange > 0f
                    ? NisitSimulator.Academics.CourseRegistrar.NotifyStudyTick(transform, gainedK, knowledgeChange)
                    : "";
                HUDController.Toast(BuildToast(gainedK) + course);   // ป๊อปอัปสรุปผลบนจอ
                NisitSimulator.Systems.GameplayEvents.Raise(knowledgeChange > 0f ? NisitSimulator.Systems.GameplayEvents.Study : NisitSimulator.Systems.GameplayEvents.Relax);
            }
        }

        // สรุปผลที่เปลี่ยนเป็นข้อความ (เฉพาะค่าที่ไม่เป็น 0)
        // สรุปผลที่เปลี่ยนเป็นข้อความ (เฉพาะค่าที่ไม่เป็น 0)
        private string BuildToast(float actualKnowledge)
        {
            var parts = new List<string>();
            if (actualKnowledge != 0f)    parts.Add($"EXP {actualKnowledge:+0.#;-0.#}");
            if (energyChange != 0f)       parts.Add($"พลังงาน {energyChange:+0;-0}");
            if (hungerChange != 0f)       parts.Add($"ความอิ่ม {hungerChange:+0;-0}");
            if (satisfactionChange != 0f) parts.Add($"พอใจ {satisfactionChange:+0;-0}");
            if (stressChange != 0f)       parts.Add($"เครียด {stressChange:+0;-0}");
            if (expReward != 0)           parts.Add($"EXP {expReward:+0;-0}");

            string line = parts.Count > 0 ? $"{activityName}  ({string.Join("  ", parts.ToArray())})" : activityName;

            // บอกผู้เล่นตรง ๆ ว่าทำไมได้ความรู้น้อยกว่าที่ควร ไม่งั้นจะนึกว่าเกมบัก
            if (knowledgeChange > 0f && actualKnowledge < knowledgeChange - 0.05f)
                line += "  — เครียดมาก เรียนได้น้อยลง";
            return line;
        }
    }
}
